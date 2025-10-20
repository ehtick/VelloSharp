#![cfg_attr(not(test), deny(clippy::all))]
#![allow(clippy::missing_safety_doc)]
#![allow(missing_docs)]
#![allow(missing_debug_implementations)]

use std::{
    cell::RefCell,
    cmp::Ordering,
    ffi::{CString, c_char},
    mem, ptr, slice,
};

use kurbo::{
    Affine, BezPath, Cap, CubicBez, Join, Line, ParamCurve, ParamCurveArclen, ParamCurveDeriv,
    PathEl, PathSeg, Point, QuadBez, Rect, Shape, Stroke, StrokeOpts, Vec2, dash as kurbo_dash,
    stroke as kurbo_stroke,
};

thread_local! {
    static LAST_ERROR: RefCell<Option<CString>> = const { RefCell::new(None) };
}

const DEFAULT_ARC_TOLERANCE: f64 = 1e-3;
const MIN_SEGMENT_LENGTH: f64 = 1e-9;
const MAX_REGION_RECTS: usize = 8_192;
const MERGE_EPSILON: f64 = 1e-6;

fn clear_last_error() {
    LAST_ERROR.with(|slot| slot.borrow_mut().take());
}

fn set_last_error(msg: impl Into<String>) {
    let msg = msg.into();
    let cstr = CString::new(msg).unwrap_or_else(|_| CString::new("invalid error message").unwrap());
    LAST_ERROR.with(|slot| *slot.borrow_mut() = Some(cstr));
}

unsafe fn slice_from_raw<'a, T>(ptr: *const T, len: usize) -> Result<&'a [T], KurboStatus> {
    if len == 0 {
        Ok(&[])
    } else if ptr.is_null() {
        Err(KurboStatus::NullPointer)
    } else {
        Ok(unsafe { slice::from_raw_parts(ptr, len) })
    }
}

#[repr(i32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum KurboStatus {
    Success = 0,
    NullPointer = 1,
    InvalidArgument = 2,
    Singular = 3,
    OutOfMemory = 4,
    Unsupported = 5,
}

#[unsafe(no_mangle)]
pub extern "C" fn kurbo_last_error_message() -> *const c_char {
    LAST_ERROR.with(|slot| match slot.borrow().as_ref() {
        Some(cstr) => cstr.as_ptr(),
        None => ptr::null(),
    })
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct KurboPoint {
    pub x: f64,
    pub y: f64,
}

impl From<Point> for KurboPoint {
    fn from(value: Point) -> Self {
        Self {
            x: value.x,
            y: value.y,
        }
    }
}

impl From<KurboPoint> for Point {
    fn from(value: KurboPoint) -> Self {
        Point::new(value.x, value.y)
    }
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct KurboVec2 {
    pub x: f64,
    pub y: f64,
}

impl From<Vec2> for KurboVec2 {
    fn from(value: Vec2) -> Self {
        Self {
            x: value.x,
            y: value.y,
        }
    }
}

impl From<KurboVec2> for Vec2 {
    fn from(value: KurboVec2) -> Self {
        Vec2::new(value.x, value.y)
    }
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct KurboRect {
    pub x0: f64,
    pub y0: f64,
    pub x1: f64,
    pub y1: f64,
}

impl From<Rect> for KurboRect {
    fn from(value: Rect) -> Self {
        Self {
            x0: value.x0,
            y0: value.y0,
            x1: value.x1,
            y1: value.y1,
        }
    }
}

impl From<KurboRect> for Rect {
    fn from(value: KurboRect) -> Self {
        Rect::new(value.x0, value.y0, value.x1, value.y1)
    }
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct KurboAffine {
    pub m11: f64,
    pub m12: f64,
    pub m21: f64,
    pub m22: f64,
    pub dx: f64,
    pub dy: f64,
}

impl From<Affine> for KurboAffine {
    fn from(value: Affine) -> Self {
        let coeffs = value.as_coeffs();
        Self {
            m11: coeffs[0],
            m12: coeffs[1],
            m21: coeffs[2],
            m22: coeffs[3],
            dx: coeffs[4],
            dy: coeffs[5],
        }
    }
}

impl From<KurboAffine> for Affine {
    fn from(value: KurboAffine) -> Self {
        Affine::new([
            value.m11, value.m12, value.m21, value.m22, value.dx, value.dy,
        ])
    }
}

#[repr(i32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum KurboPathVerb {
    MoveTo = 0,
    LineTo = 1,
    QuadTo = 2,
    CubicTo = 3,
    Close = 4,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct KurboPathElement {
    pub verb: KurboPathVerb,
    pub _padding: i32,
    pub x0: f64,
    pub y0: f64,
    pub x1: f64,
    pub y1: f64,
    pub x2: f64,
    pub y2: f64,
}

impl From<PathEl> for KurboPathElement {
    fn from(value: PathEl) -> Self {
        match value {
            PathEl::MoveTo(p0) => Self {
                verb: KurboPathVerb::MoveTo,
                _padding: 0,
                x0: p0.x,
                y0: p0.y,
                x1: 0.0,
                y1: 0.0,
                x2: 0.0,
                y2: 0.0,
            },
            PathEl::LineTo(p0) => Self {
                verb: KurboPathVerb::LineTo,
                _padding: 0,
                x0: p0.x,
                y0: p0.y,
                x1: 0.0,
                y1: 0.0,
                x2: 0.0,
                y2: 0.0,
            },
            PathEl::QuadTo(p0, p1) => Self {
                verb: KurboPathVerb::QuadTo,
                _padding: 0,
                x0: p0.x,
                y0: p0.y,
                x1: p1.x,
                y1: p1.y,
                x2: 0.0,
                y2: 0.0,
            },
            PathEl::CurveTo(p0, p1, p2) => Self {
                verb: KurboPathVerb::CubicTo,
                _padding: 0,
                x0: p0.x,
                y0: p0.y,
                x1: p1.x,
                y1: p1.y,
                x2: p2.x,
                y2: p2.y,
            },
            PathEl::ClosePath => Self {
                verb: KurboPathVerb::Close,
                _padding: 0,
                x0: 0.0,
                y0: 0.0,
                x1: 0.0,
                y1: 0.0,
                x2: 0.0,
                y2: 0.0,
            },
        }
    }
}

fn path_el_from_element(element: &KurboPathElement) -> Result<PathEl, KurboStatus> {
    let convert_point = |x: f64, y: f64| Point::new(x, y);
    Ok(match element.verb {
        KurboPathVerb::MoveTo => PathEl::MoveTo(convert_point(element.x0, element.y0)),
        KurboPathVerb::LineTo => PathEl::LineTo(convert_point(element.x0, element.y0)),
        KurboPathVerb::QuadTo => PathEl::QuadTo(
            convert_point(element.x0, element.y0),
            convert_point(element.x1, element.y1),
        ),
        KurboPathVerb::CubicTo => PathEl::CurveTo(
            convert_point(element.x0, element.y0),
            convert_point(element.x1, element.y1),
            convert_point(element.x2, element.y2),
        ),
        KurboPathVerb::Close => PathEl::ClosePath,
    })
}

#[repr(C)]
pub struct KurboBezPathHandle {
    path: BezPath,
}

#[repr(i32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum KurboStrokeJoin {
    Miter = 0,
    Round = 1,
    Bevel = 2,
}

impl From<KurboStrokeJoin> for Join {
    fn from(value: KurboStrokeJoin) -> Self {
        match value {
            KurboStrokeJoin::Miter => Join::Miter,
            KurboStrokeJoin::Round => Join::Round,
            KurboStrokeJoin::Bevel => Join::Bevel,
        }
    }
}

#[repr(i32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum KurboStrokeCap {
    Butt = 0,
    Round = 1,
    Square = 2,
}

impl From<KurboStrokeCap> for Cap {
    fn from(value: KurboStrokeCap) -> Self {
        match value {
            KurboStrokeCap::Butt => Cap::Butt,
            KurboStrokeCap::Round => Cap::Round,
            KurboStrokeCap::Square => Cap::Square,
        }
    }
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct KurboStrokeStyle {
    pub width: f64,
    pub miter_limit: f64,
    pub start_cap: KurboStrokeCap,
    pub end_cap: KurboStrokeCap,
    pub join: KurboStrokeJoin,
    pub dash_offset: f64,
    pub dash_pattern: *const f64,
    pub dash_length: usize,
}

impl KurboStrokeStyle {
    fn to_stroke(&self) -> Result<Stroke, KurboStatus> {
        if !self.width.is_finite() || self.width < 0.0 {
            set_last_error("Stroke width must be non-negative and finite");
            return Err(KurboStatus::InvalidArgument);
        }
        if !self.miter_limit.is_finite() || self.miter_limit <= 0.0 {
            set_last_error("Miter limit must be positive and finite");
            return Err(KurboStatus::InvalidArgument);
        }

        let mut stroke = Stroke::new(self.width)
            .with_miter_limit(self.miter_limit)
            .with_start_cap(self.start_cap.into())
            .with_end_cap(self.end_cap.into())
            .with_join(self.join.into());

        if self.dash_length > 0 {
            if self.dash_pattern.is_null() {
                set_last_error("Dash pattern pointer is null");
                return Err(KurboStatus::NullPointer);
            }
            let dash_slice = unsafe { slice_from_raw(self.dash_pattern, self.dash_length) }
                .map_err(|status| {
                    set_last_error("Invalid dash pattern");
                    status
                })?;
            if dash_slice
                .iter()
                .any(|value| !value.is_finite() || *value < 0.0)
            {
                set_last_error("Dash pattern entries must be non-negative and finite");
                return Err(KurboStatus::InvalidArgument);
            }
            if dash_slice.iter().all(|value| *value == 0.0) {
                set_last_error("Dash pattern cannot be all zeros");
                return Err(KurboStatus::InvalidArgument);
            }
            stroke = stroke.with_dashes(self.dash_offset, dash_slice.iter().copied());
        }

        Ok(stroke)
    }
}

#[derive(Clone)]
struct KurboPathMeasureSegment {
    segment: PathSeg,
    length: f64,
    cumulative_start: f64,
}

#[derive(Clone)]
struct KurboPathMeasureContour {
    segments: Vec<KurboPathMeasureSegment>,
    length: f64,
    is_closed: bool,
    start_point: Point,
}

pub struct KurboPathMeasureHandle {
    contours: Vec<KurboPathMeasureContour>,
    current_contour: usize,
    tolerance: f64,
}

pub struct KurboRegionHandle {
    rects: Vec<Rect>,
}

#[unsafe(no_mangle)]
pub extern "C" fn kurbo_affine_identity(out_affine: *mut KurboAffine) -> KurboStatus {
    clear_last_error();
    let Some(out) = (unsafe { out_affine.as_mut() }) else {
        set_last_error("Output pointer is null");
        return KurboStatus::NullPointer;
    };
    *out = KurboAffine::from(Affine::IDENTITY);
    KurboStatus::Success
}

#[unsafe(no_mangle)]
pub extern "C" fn kurbo_affine_mul(
    lhs: KurboAffine,
    rhs: KurboAffine,
    out_affine: *mut KurboAffine,
) -> KurboStatus {
    clear_last_error();
    let Some(out) = (unsafe { out_affine.as_mut() }) else {
        set_last_error("Output pointer is null");
        return KurboStatus::NullPointer;
    };
    let result = Affine::from(lhs) * Affine::from(rhs);
    *out = KurboAffine::from(result);
    KurboStatus::Success
}

#[unsafe(no_mangle)]
pub extern "C" fn kurbo_affine_invert(
    affine: KurboAffine,
    out_affine: *mut KurboAffine,
) -> KurboStatus {
    clear_last_error();
    let Some(out) = (unsafe { out_affine.as_mut() }) else {
        set_last_error("Output pointer is null");
        return KurboStatus::NullPointer;
    };
    let value = Affine::from(affine);
    let determinant = value.determinant();
    if !determinant.is_finite() || determinant.abs() <= f64::EPSILON {
        set_last_error("Affine transform is singular");
        return KurboStatus::Singular;
    }
    let inverse = value.inverse();
    if !inverse.as_coeffs().iter().all(|c| c.is_finite()) {
        set_last_error("Affine transform is singular");
        return KurboStatus::Singular;
    }
    *out = KurboAffine::from(inverse);
    KurboStatus::Success
}

#[unsafe(no_mangle)]
pub extern "C" fn kurbo_affine_transform_point(
    affine: KurboAffine,
    point: KurboPoint,
    out_point: *mut KurboPoint,
) -> KurboStatus {
    clear_last_error();
    let Some(out) = (unsafe { out_point.as_mut() }) else {
        set_last_error("Output pointer is null");
        return KurboStatus::NullPointer;
    };
    let result = Affine::from(affine) * Point::from(point);
    *out = KurboPoint::from(result);
    KurboStatus::Success
}

#[unsafe(no_mangle)]
pub extern "C" fn kurbo_affine_transform_vec(
    affine: KurboAffine,
    vec: KurboVec2,
    out_vec: *mut KurboVec2,
) -> KurboStatus {
    clear_last_error();
    let Some(out) = (unsafe { out_vec.as_mut() }) else {
        set_last_error("Output pointer is null");
        return KurboStatus::NullPointer;
    };
    let coeffs = Affine::from(affine).as_coeffs();
    let vector = Vec2::from(vec);
    let result = Vec2::new(
        coeffs[0] * vector.x + coeffs[1] * vector.y,
        coeffs[2] * vector.x + coeffs[3] * vector.y,
    );
    *out = KurboVec2::from(result);
    KurboStatus::Success
}

#[unsafe(no_mangle)]
pub extern "C" fn kurbo_bez_path_create() -> *mut KurboBezPathHandle {
    clear_last_error();
    Box::into_raw(Box::new(KurboBezPathHandle {
        path: BezPath::new(),
    }))
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn kurbo_bez_path_destroy(path: *mut KurboBezPathHandle) {
    if !path.is_null() {
        unsafe { drop(Box::from_raw(path)) };
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn kurbo_bez_path_clear(path: *mut KurboBezPathHandle) -> KurboStatus {
    clear_last_error();
    let Some(handle) = (unsafe { path.as_mut() }) else {
        set_last_error("Path pointer is null");
        return KurboStatus::NullPointer;
    };
    handle.path.truncate(0);
    KurboStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn kurbo_bez_path_move_to(
    path: *mut KurboBezPathHandle,
    point: KurboPoint,
) -> KurboStatus {
    clear_last_error();
    let Some(handle) = (unsafe { path.as_mut() }) else {
        set_last_error("Path pointer is null");
        return KurboStatus::NullPointer;
    };
    handle.path.move_to(Point::from(point));
    KurboStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn kurbo_bez_path_line_to(
    path: *mut KurboBezPathHandle,
    point: KurboPoint,
) -> KurboStatus {
    clear_last_error();
    let Some(handle) = (unsafe { path.as_mut() }) else {
        set_last_error("Path pointer is null");
        return KurboStatus::NullPointer;
    };
    handle.path.line_to(Point::from(point));
    KurboStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn kurbo_bez_path_quad_to(
    path: *mut KurboBezPathHandle,
    ctrl: KurboPoint,
    point: KurboPoint,
) -> KurboStatus {
    clear_last_error();
    let Some(handle) = (unsafe { path.as_mut() }) else {
        set_last_error("Path pointer is null");
        return KurboStatus::NullPointer;
    };
    handle.path.quad_to(Point::from(ctrl), Point::from(point));
    KurboStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn kurbo_bez_path_cubic_to(
    path: *mut KurboBezPathHandle,
    ctrl1: KurboPoint,
    ctrl2: KurboPoint,
    point: KurboPoint,
) -> KurboStatus {
    clear_last_error();
    let Some(handle) = (unsafe { path.as_mut() }) else {
        set_last_error("Path pointer is null");
        return KurboStatus::NullPointer;
    };
    handle
        .path
        .curve_to(Point::from(ctrl1), Point::from(ctrl2), Point::from(point));
    KurboStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn kurbo_bez_path_close(path: *mut KurboBezPathHandle) -> KurboStatus {
    clear_last_error();
    let Some(handle) = (unsafe { path.as_mut() }) else {
        set_last_error("Path pointer is null");
        return KurboStatus::NullPointer;
    };
    handle.path.close_path();
    KurboStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn kurbo_bez_path_len(
    path: *const KurboBezPathHandle,
    out_len: *mut usize,
) -> KurboStatus {
    clear_last_error();
    let Some(handle) = (unsafe { path.as_ref() }) else {
        set_last_error("Path pointer is null");
        return KurboStatus::NullPointer;
    };
    let Some(out) = (unsafe { out_len.as_mut() }) else {
        set_last_error("Output pointer is null");
        return KurboStatus::NullPointer;
    };
    *out = handle.path.elements().len();
    KurboStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn kurbo_bez_path_apply_affine(
    path: *mut KurboBezPathHandle,
    affine: KurboAffine,
) -> KurboStatus {
    clear_last_error();
    let Some(handle) = (unsafe { path.as_mut() }) else {
        set_last_error("Path pointer is null");
        return KurboStatus::NullPointer;
    };
    handle.path.apply_affine(Affine::from(affine));
    KurboStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn kurbo_bez_path_bounds(
    path: *const KurboBezPathHandle,
    out_rect: *mut KurboRect,
) -> KurboStatus {
    clear_last_error();
    let Some(handle) = (unsafe { path.as_ref() }) else {
        set_last_error("Path pointer is null");
        return KurboStatus::NullPointer;
    };
    let Some(out) = (unsafe { out_rect.as_mut() }) else {
        set_last_error("Output pointer is null");
        return KurboStatus::NullPointer;
    };
    *out = KurboRect::from(handle.path.bounding_box());
    KurboStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn kurbo_bez_path_copy_elements(
    path: *const KurboBezPathHandle,
    elements: *mut KurboPathElement,
    capacity: usize,
    out_len: *mut usize,
) -> KurboStatus {
    clear_last_error();
    let Some(handle) = (unsafe { path.as_ref() }) else {
        set_last_error("Path pointer is null");
        return KurboStatus::NullPointer;
    };
    let Some(out_len) = (unsafe { out_len.as_mut() }) else {
        set_last_error("Output length pointer is null");
        return KurboStatus::NullPointer;
    };
    let elements_slice = handle.path.elements();
    let len = elements_slice.len();
    *out_len = len;
    if len == 0 {
        return KurboStatus::Success;
    }
    if capacity < len {
        set_last_error("Destination buffer is too small");
        return KurboStatus::InvalidArgument;
    }
    let Some(out_ptr) = (!elements.is_null()).then_some(elements) else {
        set_last_error("Destination buffer pointer is null");
        return KurboStatus::NullPointer;
    };
    for (index, element) in elements_slice.iter().enumerate() {
        unsafe {
            *out_ptr.add(index) = KurboPathElement::from(*element);
        }
    }
    KurboStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn kurbo_bez_path_from_elements(
    elements: *const KurboPathElement,
    len: usize,
) -> *mut KurboBezPathHandle {
    clear_last_error();
    let slice = match unsafe { slice_from_raw(elements, len) } {
        Ok(slice) => slice,
        Err(status) => {
            set_last_error(format!("Invalid elements slice: {:?}", status));
            return ptr::null_mut();
        }
    };
    let mut path = BezPath::new();
    for element in slice {
        match path_el_from_element(element) {
            Ok(el) => path.push(el),
            Err(status) => {
                set_last_error(format!("Invalid path element: {:?}", status));
                return ptr::null_mut();
            }
        }
    }
    Box::into_raw(Box::new(KurboBezPathHandle { path }))
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn kurbo_bez_path_append(
    path: *mut KurboBezPathHandle,
    other: *const KurboBezPathHandle,
) -> KurboStatus {
    clear_last_error();
    let Some(dest) = (unsafe { path.as_mut() }) else {
        set_last_error("Destination path pointer is null");
        return KurboStatus::NullPointer;
    };
    let Some(src) = (unsafe { other.as_ref() }) else {
        set_last_error("Source path pointer is null");
        return KurboStatus::NullPointer;
    };
    dest.path.extend(src.path.elements().iter().copied());
    KurboStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn kurbo_bez_path_translate(
    path: *mut KurboBezPathHandle,
    offset: KurboVec2,
) -> KurboStatus {
    clear_last_error();
    let Some(handle) = (unsafe { path.as_mut() }) else {
        set_last_error("Path pointer is null");
        return KurboStatus::NullPointer;
    };
    handle
        .path
        .apply_affine(Affine::translate(Vec2::from(offset)));
    KurboStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn kurbo_bez_path_stroke(
    path: *const KurboBezPathHandle,
    style: KurboStrokeStyle,
    tolerance: f64,
    out_path: *mut *mut KurboBezPathHandle,
) -> KurboStatus {
    if out_path.is_null() {
        set_last_error("Output handle pointer is null");
        return KurboStatus::NullPointer;
    }

    clear_last_error();

    let Some(handle) = (unsafe { path.as_ref() }) else {
        set_last_error("Path pointer is null");
        return KurboStatus::NullPointer;
    };

    if !tolerance.is_finite() || tolerance <= 0.0 {
        set_last_error("Tolerance must be positive and finite");
        return KurboStatus::InvalidArgument;
    }

    let stroke = match style.to_stroke() {
        Ok(stroke) => stroke,
        Err(status) => return status,
    };

    let stroked = kurbo_stroke(
        handle.path.elements().iter().copied(),
        &stroke,
        &StrokeOpts::default(),
        tolerance,
    );

    let result = KurboBezPathHandle { path: stroked };

    unsafe {
        *out_path = Box::into_raw(Box::new(result));
    }

    KurboStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn kurbo_bez_path_dash(
    path: *const KurboBezPathHandle,
    dash_offset: f64,
    dash_pattern: *const f64,
    dash_length: usize,
    out_path: *mut *mut KurboBezPathHandle,
) -> KurboStatus {
    if out_path.is_null() {
        set_last_error("Output handle pointer is null");
        return KurboStatus::NullPointer;
    }

    clear_last_error();

    let Some(handle) = (unsafe { path.as_ref() }) else {
        set_last_error("Path pointer is null");
        return KurboStatus::NullPointer;
    };

    if dash_length < 2 || dash_length % 2 != 0 {
        set_last_error("Dash pattern must contain an even number of entries (>= 2)");
        return KurboStatus::InvalidArgument;
    }

    if dash_pattern.is_null() {
        set_last_error("Dash pattern pointer is null");
        return KurboStatus::NullPointer;
    }

    let dash_slice = match unsafe { slice_from_raw(dash_pattern, dash_length) } {
        Ok(slice) => slice,
        Err(status) => {
            set_last_error("Invalid dash pattern slice");
            return status;
        }
    };

    if dash_slice
        .iter()
        .any(|value| !value.is_finite() || *value <= 0.0)
    {
        set_last_error("Dash pattern values must be positive and finite");
        return KurboStatus::InvalidArgument;
    }

    let dashed_iter = kurbo_dash(
        handle.path.elements().iter().copied(),
        dash_offset,
        dash_slice,
    );
    let dashed_path: BezPath = dashed_iter.collect();

    let result = KurboBezPathHandle { path: dashed_path };

    unsafe {
        *out_path = Box::into_raw(Box::new(result));
    }

    KurboStatus::Success
}

fn add_measure_segment(
    segments: &mut Vec<KurboPathMeasureSegment>,
    segment: PathSeg,
    tolerance: f64,
    cumulative_length: &mut f64,
) {
    let tol = tolerance.max(DEFAULT_ARC_TOLERANCE);
    let length = segment.arclen(tol);
    if !length.is_finite() || length <= MIN_SEGMENT_LENGTH {
        return;
    }
    segments.push(KurboPathMeasureSegment {
        cumulative_start: *cumulative_length,
        length,
        segment,
    });
    *cumulative_length += length;
}

fn normalize_rect(rect: KurboRect) -> Option<Rect> {
    let KurboRect { x0, y0, x1, y1 } = rect;
    if !x0.is_finite() || !y0.is_finite() || !x1.is_finite() || !y1.is_finite() {
        return None;
    }
    let (mut left, right) = if x0 <= x1 { (x0, x1) } else { (x1, x0) };
    let (mut top, bottom) = if y0 <= y1 { (y0, y1) } else { (y1, y0) };
    if (right - left).abs() <= MIN_SEGMENT_LENGTH || (bottom - top).abs() <= MIN_SEGMENT_LENGTH {
        return None;
    }
    // Clamp tiny negative zeros to zero to avoid -0.0 propagation.
    if left == -0.0 {
        left = 0.0;
    }
    if top == -0.0 {
        top = 0.0;
    }
    Some(Rect::new(left, top, right, bottom))
}

fn nearly_equal(a: f64, b: f64) -> bool {
    (a - b).abs() <= MERGE_EPSILON * (1.0 + a.abs().max(b.abs()))
}

fn merge_rectangles(rects: &mut Vec<Rect>) {
    if rects.len() < 2 {
        return;
    }
    rects.sort_by(
        |a, b| match a.y0.partial_cmp(&b.y0).unwrap_or(Ordering::Equal) {
            Ordering::Equal => a.x0.partial_cmp(&b.x0).unwrap_or(Ordering::Equal),
            other => other,
        },
    );

    fn try_merge(target: &mut Rect, candidate: &Rect) -> bool {
        if nearly_equal(target.y0, candidate.y0)
            && nearly_equal(target.y1, candidate.y1)
            && candidate.x0 <= target.x1 + MERGE_EPSILON
            && candidate.x1 >= target.x0 - MERGE_EPSILON
        {
            target.x0 = target.x0.min(candidate.x0);
            target.x1 = target.x1.max(candidate.x1);
            true
        } else if nearly_equal(target.x0, candidate.x0)
            && nearly_equal(target.x1, candidate.x1)
            && candidate.y0 <= target.y1 + MERGE_EPSILON
            && candidate.y1 >= target.y0 - MERGE_EPSILON
        {
            target.y0 = target.y0.min(candidate.y0);
            target.y1 = target.y1.max(candidate.y1);
            true
        } else {
            false
        }
    }

    let mut current = Vec::with_capacity(rects.len());
    for rect in rects.drain(..) {
        let mut merged = false;
        for existing in &mut current {
            if try_merge(existing, &rect) {
                merged = true;
                break;
            }
        }
        if !merged {
            current.push(rect);
        }
    }

    let mut changed = true;
    while changed {
        changed = false;
        let mut next = Vec::with_capacity(current.len());
        for rect in current.into_iter() {
            let mut merged = false;
            for existing in &mut next {
                if try_merge(existing, &rect) {
                    merged = true;
                    changed = true;
                    break;
                }
            }
            if !merged {
                next.push(rect);
            }
        }
        current = next;
    }

    rects.extend(current.into_iter());
}

fn finalize_contour(
    contours: &mut Vec<KurboPathMeasureContour>,
    segments: Vec<KurboPathMeasureSegment>,
    is_closed: bool,
    start_point: Point,
) {
    if segments.is_empty() {
        return;
    }
    let length = segments
        .last()
        .map(|segment| segment.cumulative_start + segment.length)
        .unwrap_or(0.0);
    if length <= MIN_SEGMENT_LENGTH {
        return;
    }
    contours.push(KurboPathMeasureContour {
        segments,
        length,
        is_closed,
        start_point,
    });
}

fn build_measure_contours(path: &BezPath, tolerance: f64) -> Vec<KurboPathMeasureContour> {
    let mut contours = Vec::new();
    let mut segments = Vec::new();
    let mut cumulative_length = 0.0;
    let mut start_point = Point::ZERO;
    let mut current_point = Point::ZERO;
    let mut have_subpath = false;
    let mut is_closed = false;

    for el in path.elements() {
        match *el {
            PathEl::MoveTo(p) => {
                if have_subpath {
                    let contour_segments = mem::take(&mut segments);
                    finalize_contour(&mut contours, contour_segments, is_closed, start_point);
                    cumulative_length = 0.0;
                }
                start_point = p;
                current_point = p;
                have_subpath = true;
                is_closed = false;
            }
            PathEl::LineTo(p) => {
                if !have_subpath {
                    continue;
                }
                if current_point != p {
                    let seg = PathSeg::Line(Line::new(current_point, p));
                    add_measure_segment(&mut segments, seg, tolerance, &mut cumulative_length);
                    current_point = p;
                }
            }
            PathEl::QuadTo(p1, p2) => {
                if !have_subpath {
                    continue;
                }
                let seg = PathSeg::Quad(QuadBez::new(current_point, p1, p2));
                add_measure_segment(&mut segments, seg, tolerance, &mut cumulative_length);
                current_point = p2;
            }
            PathEl::CurveTo(p1, p2, p3) => {
                if !have_subpath {
                    continue;
                }
                let seg = PathSeg::Cubic(CubicBez::new(current_point, p1, p2, p3));
                add_measure_segment(&mut segments, seg, tolerance, &mut cumulative_length);
                current_point = p3;
            }
            PathEl::ClosePath => {
                if !have_subpath {
                    continue;
                }
                if current_point != start_point {
                    let seg = PathSeg::Line(Line::new(current_point, start_point));
                    add_measure_segment(&mut segments, seg, tolerance, &mut cumulative_length);
                    current_point = start_point;
                }
                is_closed = true;
                let contour_segments = mem::take(&mut segments);
                finalize_contour(&mut contours, contour_segments, is_closed, start_point);
                cumulative_length = 0.0;
                have_subpath = false;
                is_closed = false;
            }
        }
    }

    if have_subpath {
        let contour_segments = mem::take(&mut segments);
        finalize_contour(&mut contours, contour_segments, is_closed, start_point);
    }

    contours
}

fn current_contour(handle: &KurboPathMeasureHandle) -> Option<&KurboPathMeasureContour> {
    handle.contours.get(handle.current_contour)
}

fn compute_position_tangent(
    contour: &KurboPathMeasureContour,
    distance: f64,
    tolerance: f64,
) -> (Point, Vec2) {
    if contour.segments.is_empty() {
        return (contour.start_point, Vec2::new(0.0, 0.0));
    }

    let clamped = distance.clamp(0.0, contour.length);
    let tol = tolerance.max(DEFAULT_ARC_TOLERANCE);
    for segment in &contour.segments {
        let seg_start = segment.cumulative_start;
        let seg_end = seg_start + segment.length;
        if clamped > seg_end {
            continue;
        }
        let local = (clamped - seg_start).clamp(0.0, segment.length);
        let t = if segment.length <= MIN_SEGMENT_LENGTH {
            if local <= MIN_SEGMENT_LENGTH {
                0.0
            } else {
                1.0
            }
        } else if local <= MIN_SEGMENT_LENGTH {
            0.0
        } else if local >= segment.length - MIN_SEGMENT_LENGTH {
            1.0
        } else {
            segment.segment.inv_arclen(local, tol).clamp(0.0, 1.0)
        };
        let point = segment.segment.eval(t);
        let derivative = match &segment.segment {
            PathSeg::Line(line) => line.deriv().eval(t).to_vec2(),
            PathSeg::Quad(quad) => quad.deriv().eval(t).to_vec2(),
            PathSeg::Cubic(cubic) => cubic.deriv().eval(t).to_vec2(),
        };
        let magnitude = derivative.hypot();
        let tangent = if magnitude > 0.0 {
            Vec2::new(derivative.x / magnitude, derivative.y / magnitude)
        } else {
            Vec2::new(0.0, 0.0)
        };
        return (point, tangent);
    }

    let last_segment = contour.segments.last().unwrap();
    let point = last_segment.segment.end();
    (point, Vec2::new(0.0, 0.0))
}

fn extract_contour_segment(
    contour: &KurboPathMeasureContour,
    start: f64,
    stop: f64,
    tolerance: f64,
) -> Option<BezPath> {
    if contour.segments.is_empty() {
        return None;
    }
    let start = start.clamp(0.0, contour.length);
    let stop = stop.clamp(0.0, contour.length);
    if stop <= start + MIN_SEGMENT_LENGTH {
        return None;
    }

    let mut path = BezPath::new();
    let mut last_point: Option<Point> = None;
    let mut wrote_first_segment = false;
    let tol = tolerance.max(DEFAULT_ARC_TOLERANCE);

    for segment in &contour.segments {
        let seg_start = segment.cumulative_start;
        let seg_end = seg_start + segment.length;
        if stop <= seg_start {
            break;
        }
        if start >= seg_end {
            continue;
        }

        let local_start = (start - seg_start).clamp(0.0, segment.length);
        let local_end = (stop - seg_start).clamp(0.0, segment.length);
        if local_end <= local_start + MIN_SEGMENT_LENGTH {
            continue;
        }

        let seg_length = segment.length.max(MIN_SEGMENT_LENGTH);
        let mut sub_seg = segment.segment.clone();
        if local_start > MIN_SEGMENT_LENGTH || local_end < seg_length - MIN_SEGMENT_LENGTH {
            let start_t = if seg_length <= MIN_SEGMENT_LENGTH {
                0.0
            } else {
                segment.segment.inv_arclen(local_start, tol).clamp(0.0, 1.0)
            };
            let end_t = if seg_length <= MIN_SEGMENT_LENGTH {
                1.0
            } else {
                segment.segment.inv_arclen(local_end, tol).clamp(0.0, 1.0)
            };
            if end_t <= start_t {
                continue;
            }
            sub_seg = segment.segment.subsegment(start_t..end_t);
        }

        let seg_start_point = sub_seg.start();
        let seg_end_point = sub_seg.end();

        if !wrote_first_segment {
            path.move_to(seg_start_point);
            wrote_first_segment = true;
        } else if last_point.map_or(true, |p| p != seg_start_point) {
            path.line_to(seg_start_point);
        }

        match sub_seg {
            PathSeg::Line(line) => path.line_to(line.p1),
            PathSeg::Quad(quad) => path.quad_to(quad.p1, quad.p2),
            PathSeg::Cubic(cubic) => path.curve_to(cubic.p1, cubic.p2, cubic.p3),
        }

        last_point = Some(seg_end_point);
    }

    if path.elements().is_empty() {
        None
    } else {
        Some(path)
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn kurbo_path_measure_create(
    path: *const KurboBezPathHandle,
    tolerance: f64,
) -> *mut KurboPathMeasureHandle {
    if path.is_null() {
        set_last_error("Path pointer is null");
        return ptr::null_mut();
    }

    clear_last_error();
    let path_ref = unsafe { &*path };
    let tol = if tolerance.is_finite() && tolerance > 0.0 {
        tolerance
    } else {
        DEFAULT_ARC_TOLERANCE
    };
    let contours = build_measure_contours(&path_ref.path, tol);
    let current = contours
        .iter()
        .position(|contour| !contour.segments.is_empty())
        .unwrap_or(contours.len());
    Box::into_raw(Box::new(KurboPathMeasureHandle {
        contours,
        current_contour: current,
        tolerance: tol,
    }))
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn kurbo_path_measure_destroy(handle: *mut KurboPathMeasureHandle) {
    if !handle.is_null() {
        unsafe { drop(Box::from_raw(handle)) };
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn kurbo_path_measure_get_length(
    handle: *const KurboPathMeasureHandle,
    out_length: *mut f64,
) -> KurboStatus {
    clear_last_error();
    let Some(out) = (unsafe { out_length.as_mut() }) else {
        set_last_error("Output pointer is null");
        return KurboStatus::NullPointer;
    };
    let Some(handle_ref) = (unsafe { handle.as_ref() }) else {
        set_last_error("Path measure handle is null");
        return KurboStatus::NullPointer;
    };
    if let Some(contour) = current_contour(handle_ref) {
        *out = contour.length;
    } else {
        *out = 0.0;
    }
    KurboStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn kurbo_path_measure_is_closed(
    handle: *const KurboPathMeasureHandle,
    out_closed: *mut bool,
) -> KurboStatus {
    clear_last_error();
    let Some(out) = (unsafe { out_closed.as_mut() }) else {
        set_last_error("Output pointer is null");
        return KurboStatus::NullPointer;
    };
    let Some(handle_ref) = (unsafe { handle.as_ref() }) else {
        set_last_error("Path measure handle is null");
        return KurboStatus::NullPointer;
    };
    *out = current_contour(handle_ref)
        .map(|c| c.is_closed)
        .unwrap_or(false);
    KurboStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn kurbo_path_measure_get_pos_tan(
    handle: *const KurboPathMeasureHandle,
    distance: f64,
    out_position: *mut KurboPoint,
    out_tangent: *mut KurboVec2,
) -> KurboStatus {
    clear_last_error();
    let Some(pos_out) = (unsafe { out_position.as_mut() }) else {
        set_last_error("Position output pointer is null");
        return KurboStatus::NullPointer;
    };
    let Some(tan_out) = (unsafe { out_tangent.as_mut() }) else {
        set_last_error("Tangent output pointer is null");
        return KurboStatus::NullPointer;
    };
    let Some(handle_ref) = (unsafe { handle.as_ref() }) else {
        set_last_error("Path measure handle is null");
        return KurboStatus::NullPointer;
    };
    if let Some(contour) = current_contour(handle_ref) {
        let (point, tangent) = compute_position_tangent(contour, distance, handle_ref.tolerance);
        *pos_out = KurboPoint::from(point);
        *tan_out = KurboVec2::from(tangent);
    } else {
        *pos_out = KurboPoint { x: 0.0, y: 0.0 };
        *tan_out = KurboVec2 { x: 0.0, y: 0.0 };
    }
    KurboStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn kurbo_path_measure_next_contour(
    handle: *mut KurboPathMeasureHandle,
    out_has_next: *mut bool,
) -> KurboStatus {
    clear_last_error();
    let Some(out) = (unsafe { out_has_next.as_mut() }) else {
        set_last_error("Output pointer is null");
        return KurboStatus::NullPointer;
    };
    let Some(handle_mut) = (unsafe { handle.as_mut() }) else {
        set_last_error("Path measure handle is null");
        return KurboStatus::NullPointer;
    };

    if handle_mut.current_contour >= handle_mut.contours.len() {
        *out = false;
        return KurboStatus::Success;
    }

    let mut next = handle_mut.current_contour + 1;
    while next < handle_mut.contours.len() && handle_mut.contours[next].segments.is_empty() {
        next += 1;
    }

    if next < handle_mut.contours.len() {
        handle_mut.current_contour = next;
        *out = true;
    } else {
        handle_mut.current_contour = handle_mut.contours.len();
        *out = false;
    }

    KurboStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn kurbo_path_measure_get_segment(
    handle: *const KurboPathMeasureHandle,
    start_distance: f64,
    stop_distance: f64,
    start_with_move_to: bool,
    dst: *mut KurboBezPathHandle,
    out_has_segment: *mut bool,
) -> KurboStatus {
    clear_last_error();

    let Some(dst_handle) = (unsafe { dst.as_mut() }) else {
        set_last_error("Destination path handle is null");
        return KurboStatus::NullPointer;
    };
    let Some(has_segment_out) = (unsafe { out_has_segment.as_mut() }) else {
        set_last_error("Output pointer is null");
        return KurboStatus::NullPointer;
    };
    *has_segment_out = false;

    let Some(handle_ref) = (unsafe { handle.as_ref() }) else {
        set_last_error("Path measure handle is null");
        return KurboStatus::NullPointer;
    };
    let Some(contour) = current_contour(handle_ref) else {
        return KurboStatus::Success;
    };

    let Some(segment_path) =
        extract_contour_segment(contour, start_distance, stop_distance, handle_ref.tolerance)
    else {
        return KurboStatus::Success;
    };

    let mut elements = segment_path.elements().iter().copied().peekable();
    let mut is_first_element = true;
    while let Some(element) = elements.next() {
        match element {
            PathEl::MoveTo(p) => {
                if is_first_element {
                    if start_with_move_to || dst_handle.path.is_empty() {
                        dst_handle.path.move_to(p);
                    } else if let Some(current) = dst_handle.path.current_position() {
                        if current != p {
                            dst_handle.path.line_to(p);
                        }
                    } else {
                        dst_handle.path.move_to(p);
                    }
                } else {
                    dst_handle.path.move_to(p);
                }
            }
            PathEl::LineTo(p) => dst_handle.path.line_to(p),
            PathEl::QuadTo(p1, p2) => dst_handle.path.quad_to(p1, p2),
            PathEl::CurveTo(p1, p2, p3) => dst_handle.path.curve_to(p1, p2, p3),
            PathEl::ClosePath => dst_handle.path.close_path(),
        }
        is_first_element = false;
    }

    *has_segment_out = true;
    KurboStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn kurbo_region_create(
    rects: *const KurboRect,
    rect_count: usize,
    out_handle: *mut *mut KurboRegionHandle,
) -> KurboStatus {
    if out_handle.is_null() {
        return KurboStatus::NullPointer;
    }

    clear_last_error();
    let slice = match unsafe { slice_from_raw(rects, rect_count) } {
        Ok(slice) => slice,
        Err(status) => return status,
    };

    if slice.len() > MAX_REGION_RECTS {
        set_last_error("Rectangle count exceeds supported region size");
        return KurboStatus::Unsupported;
    }

    let mut normalized = Vec::with_capacity(slice.len());
    for rect in slice {
        if let Some(r) = normalize_rect(*rect) {
            normalized.push(r);
        }
    }

    if normalized.len() > MAX_REGION_RECTS {
        set_last_error("Rectangle count exceeds supported region size after filtering");
        return KurboStatus::Unsupported;
    }

    merge_rectangles(&mut normalized);

    unsafe {
        *out_handle = Box::into_raw(Box::new(KurboRegionHandle { rects: normalized }));
    }

    KurboStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn kurbo_region_destroy(handle: *mut KurboRegionHandle) {
    if !handle.is_null() {
        unsafe { drop(Box::from_raw(handle)) };
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn kurbo_region_get_rect_count(
    handle: *const KurboRegionHandle,
    out_count: *mut usize,
) -> KurboStatus {
    clear_last_error();
    let Some(out) = (unsafe { out_count.as_mut() }) else {
        set_last_error("Output pointer is null");
        return KurboStatus::NullPointer;
    };
    let Some(region) = (unsafe { handle.as_ref() }) else {
        set_last_error("Region handle is null");
        return KurboStatus::NullPointer;
    };
    *out = region.rects.len();
    KurboStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn kurbo_region_copy_rects(
    handle: *const KurboRegionHandle,
    dst: *mut KurboRect,
    capacity: usize,
    out_count: *mut usize,
) -> KurboStatus {
    clear_last_error();
    let Some(out) = (unsafe { out_count.as_mut() }) else {
        set_last_error("Output pointer is null");
        return KurboStatus::NullPointer;
    };
    let Some(region) = (unsafe { handle.as_ref() }) else {
        set_last_error("Region handle is null");
        return KurboStatus::NullPointer;
    };

    *out = region.rects.len();
    if dst.is_null() {
        return KurboStatus::Success;
    }
    if capacity < region.rects.len() {
        set_last_error("Destination buffer is too small");
        return KurboStatus::InvalidArgument;
    }
    for (index, rect) in region.rects.iter().enumerate() {
        unsafe {
            *dst.add(index) = KurboRect::from(*rect);
        }
    }
    KurboStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn kurbo_region_get_bounds(
    handle: *const KurboRegionHandle,
    out_rect: *mut KurboRect,
) -> KurboStatus {
    clear_last_error();
    let Some(out) = (unsafe { out_rect.as_mut() }) else {
        set_last_error("Output pointer is null");
        return KurboStatus::NullPointer;
    };
    let Some(region) = (unsafe { handle.as_ref() }) else {
        set_last_error("Region handle is null");
        return KurboStatus::NullPointer;
    };
    if region.rects.is_empty() {
        *out = KurboRect {
            x0: 0.0,
            y0: 0.0,
            x1: 0.0,
            y1: 0.0,
        };
        return KurboStatus::Success;
    }
    let mut bounds = region.rects[0];
    for rect in &region.rects[1..] {
        bounds = bounds.union(*rect);
    }
    *out = KurboRect::from(bounds);
    KurboStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn kurbo_rect_union(
    rect_a: KurboRect,
    rect_b: KurboRect,
    out_rect: *mut KurboRect,
) -> KurboStatus {
    clear_last_error();
    let Some(out) = (unsafe { out_rect.as_mut() }) else {
        set_last_error("Output pointer is null");
        return KurboStatus::NullPointer;
    };
    let result = Rect::from(rect_a).union(Rect::from(rect_b));
    *out = KurboRect::from(result);
    KurboStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn kurbo_rect_intersect(
    rect_a: KurboRect,
    rect_b: KurboRect,
    out_rect: *mut KurboRect,
) -> KurboStatus {
    clear_last_error();
    let Some(out) = (unsafe { out_rect.as_mut() }) else {
        set_last_error("Output pointer is null");
        return KurboStatus::NullPointer;
    };
    let result = Rect::from(rect_a).intersect(Rect::from(rect_b));
    *out = KurboRect::from(result);
    KurboStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn kurbo_rect_is_empty(rect: KurboRect, out_empty: *mut bool) -> KurboStatus {
    clear_last_error();
    let Some(out) = (unsafe { out_empty.as_mut() }) else {
        set_last_error("Output pointer is null");
        return KurboStatus::NullPointer;
    };
    *out = Rect::from(rect).is_zero_area();
    KurboStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn kurbo_vec2_length(vec: KurboVec2, out_length: *mut f64) -> KurboStatus {
    clear_last_error();
    let Some(out) = (unsafe { out_length.as_mut() }) else {
        set_last_error("Output pointer is null");
        return KurboStatus::NullPointer;
    };
    *out = Vec2::from(vec).hypot();
    KurboStatus::Success
}

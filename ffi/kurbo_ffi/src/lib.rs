#![cfg_attr(not(test), deny(clippy::all))]
#![allow(clippy::missing_safety_doc)]
#![allow(missing_docs)]
#![allow(missing_debug_implementations)]

use std::{
    cell::RefCell,
    ffi::{CString, c_char},
    ptr, slice,
};

use kurbo::{Affine, BezPath, PathEl, Point, Rect, Shape, Vec2};

thread_local! {
    static LAST_ERROR: RefCell<Option<CString>> = const { RefCell::new(None) };
}

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

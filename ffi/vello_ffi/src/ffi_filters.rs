use std::{ptr, slice, sync::Arc};

use crate::{
    VelloBlendCompose, VelloBlendMix, VelloColor, VelloPoint, VelloStatus, clear_last_error,
    set_last_error,
};

const COLOR_MATRIX_ELEMENT_COUNT: usize = 20;

#[repr(i32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum VelloFilterKind {
    Blur = 0,
    DropShadow = 1,
    Blend = 2,
    ColorMatrix = 3,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloFilterBlur {
    pub sigma_x: f32,
    pub sigma_y: f32,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloFilterDropShadow {
    pub offset: VelloPoint,
    pub sigma_x: f32,
    pub sigma_y: f32,
    pub color: VelloColor,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloFilterBlend {
    pub mix: VelloBlendMix,
    pub compose: VelloBlendCompose,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloFilterColorMatrix {
    pub matrix: [f32; COLOR_MATRIX_ELEMENT_COUNT],
}

enum ShimFilter {
    Blur(VelloFilterBlur),
    DropShadow(VelloFilterDropShadow),
    Blend(VelloFilterBlend),
    ColorMatrix(VelloFilterColorMatrix),
}

pub struct VelloFilterHandle {
    inner: Arc<ShimFilter>,
}

impl VelloFilterHandle {
    fn new(filter: ShimFilter) -> Self {
        Self {
            inner: Arc::new(filter),
        }
    }

    fn kind(&self) -> VelloFilterKind {
        match self.inner.as_ref() {
            ShimFilter::Blur(_) => VelloFilterKind::Blur,
            ShimFilter::DropShadow(_) => VelloFilterKind::DropShadow,
            ShimFilter::Blend(_) => VelloFilterKind::Blend,
            ShimFilter::ColorMatrix(_) => VelloFilterKind::ColorMatrix,
        }
    }
}

fn validate_sigma(value: f32, axis: &str) -> Result<f32, String> {
    if !value.is_finite() {
        Err(format!("{axis} sigma must be finite"))
    } else if value < 0.0 {
        Err(format!("{axis} sigma must be non-negative"))
    } else {
        Ok(value)
    }
}

#[unsafe(no_mangle)]
pub extern "C" fn vello_filter_blur_create(sigma_x: f32, sigma_y: f32) -> *mut VelloFilterHandle {
    clear_last_error();

    let sigma_x = match validate_sigma(sigma_x, "X") {
        Ok(value) => value,
        Err(message) => {
            set_last_error(message);
            return ptr::null_mut();
        }
    };
    let sigma_y = match validate_sigma(sigma_y, "Y") {
        Ok(value) => value,
        Err(message) => {
            set_last_error(message);
            return ptr::null_mut();
        }
    };

    let filter = ShimFilter::Blur(VelloFilterBlur { sigma_x, sigma_y });
    Box::into_raw(Box::new(VelloFilterHandle::new(filter)))
}

#[unsafe(no_mangle)]
pub extern "C" fn vello_filter_drop_shadow_create(
    offset: VelloPoint,
    sigma_x: f32,
    sigma_y: f32,
    color: VelloColor,
) -> *mut VelloFilterHandle {
    clear_last_error();

    let sigma_x = match validate_sigma(sigma_x, "X") {
        Ok(value) => value,
        Err(message) => {
            set_last_error(message);
            return ptr::null_mut();
        }
    };
    let sigma_y = match validate_sigma(sigma_y, "Y") {
        Ok(value) => value,
        Err(message) => {
            set_last_error(message);
            return ptr::null_mut();
        }
    };

    let filter = ShimFilter::DropShadow(VelloFilterDropShadow {
        offset,
        sigma_x,
        sigma_y,
        color,
    });
    Box::into_raw(Box::new(VelloFilterHandle::new(filter)))
}

#[unsafe(no_mangle)]
pub extern "C" fn vello_filter_blend_create(
    mix: VelloBlendMix,
    compose: VelloBlendCompose,
) -> *mut VelloFilterHandle {
    clear_last_error();

    if matches!(mix, VelloBlendMix::Clip) {
        set_last_error("Clip mix cannot be used with blend filters");
        return ptr::null_mut();
    }

    let filter = ShimFilter::Blend(VelloFilterBlend { mix, compose });
    Box::into_raw(Box::new(VelloFilterHandle::new(filter)))
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_filter_color_matrix_create(
    matrix: *const f32,
    length: usize,
) -> *mut VelloFilterHandle {
    clear_last_error();

    if matrix.is_null() {
        set_last_error("Color matrix pointer is null");
        return ptr::null_mut();
    }

    if length != COLOR_MATRIX_ELEMENT_COUNT {
        set_last_error("Color matrix must contain 20 elements");
        return ptr::null_mut();
    }

    let values = unsafe { slice::from_raw_parts(matrix, length) };
    let mut matrix_values = [0f32; COLOR_MATRIX_ELEMENT_COUNT];
    matrix_values.copy_from_slice(values);

    let filter = ShimFilter::ColorMatrix(VelloFilterColorMatrix {
        matrix: matrix_values,
    });
    Box::into_raw(Box::new(VelloFilterHandle::new(filter)))
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_filter_retain(
    handle: *const VelloFilterHandle,
) -> *mut VelloFilterHandle {
    if handle.is_null() {
        return ptr::null_mut();
    }

    clear_last_error();

    let original = unsafe { &*handle };
    let clone = VelloFilterHandle {
        inner: Arc::clone(&original.inner),
    };
    Box::into_raw(Box::new(clone))
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_filter_release(handle: *mut VelloFilterHandle) {
    if handle.is_null() {
        return;
    }

    clear_last_error();

    unsafe {
        drop(Box::from_raw(handle));
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_filter_get_kind(
    handle: *const VelloFilterHandle,
    out_kind: *mut VelloFilterKind,
) -> VelloStatus {
    if handle.is_null() || out_kind.is_null() {
        return VelloStatus::NullPointer;
    }

    clear_last_error();

    let handle_ref = unsafe { &*handle };
    unsafe {
        *out_kind = handle_ref.kind();
    }
    VelloStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_filter_get_blur(
    handle: *const VelloFilterHandle,
    out_params: *mut VelloFilterBlur,
) -> VelloStatus {
    if handle.is_null() || out_params.is_null() {
        return VelloStatus::NullPointer;
    }

    clear_last_error();

    let handle_ref = unsafe { &*handle };
    match handle_ref.inner.as_ref() {
        ShimFilter::Blur(params) => {
            unsafe {
                *out_params = *params;
            }
            VelloStatus::Success
        }
        _ => {
            set_last_error("Filter is not a blur");
            VelloStatus::InvalidArgument
        }
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_filter_get_drop_shadow(
    handle: *const VelloFilterHandle,
    out_params: *mut VelloFilterDropShadow,
) -> VelloStatus {
    if handle.is_null() || out_params.is_null() {
        return VelloStatus::NullPointer;
    }

    clear_last_error();

    let handle_ref = unsafe { &*handle };
    match handle_ref.inner.as_ref() {
        ShimFilter::DropShadow(params) => {
            unsafe {
                *out_params = *params;
            }
            VelloStatus::Success
        }
        _ => {
            set_last_error("Filter is not a drop shadow");
            VelloStatus::InvalidArgument
        }
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_filter_get_blend(
    handle: *const VelloFilterHandle,
    out_params: *mut VelloFilterBlend,
) -> VelloStatus {
    if handle.is_null() || out_params.is_null() {
        return VelloStatus::NullPointer;
    }

    clear_last_error();

    let handle_ref = unsafe { &*handle };
    match handle_ref.inner.as_ref() {
        ShimFilter::Blend(params) => {
            unsafe {
                *out_params = *params;
            }
            VelloStatus::Success
        }
        _ => {
            set_last_error("Filter is not a blend");
            VelloStatus::InvalidArgument
        }
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_filter_get_color_matrix(
    handle: *const VelloFilterHandle,
    out_params: *mut VelloFilterColorMatrix,
) -> VelloStatus {
    if handle.is_null() || out_params.is_null() {
        return VelloStatus::NullPointer;
    }

    clear_last_error();

    let handle_ref = unsafe { &*handle };
    match handle_ref.inner.as_ref() {
        ShimFilter::ColorMatrix(params) => {
            unsafe {
                *out_params = *params;
            }
            VelloStatus::Success
        }
        _ => {
            set_last_error("Filter is not a color matrix");
            VelloStatus::InvalidArgument
        }
    }
}

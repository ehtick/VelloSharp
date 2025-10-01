#![cfg_attr(not(test), deny(clippy::all))]
#![allow(clippy::missing_safety_doc)]
#![allow(missing_docs)]

use std::{
    cell::RefCell,
    ffi::{CStr, CString, c_char},
    ptr,
};

use accesskit::{ActionRequest, TreeUpdate};

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

unsafe fn write_out_ptr<T>(out: *mut *mut T, value: *mut T) -> AccessKitStatus {
    if out.is_null() {
        set_last_error("output pointer is null");
        AccessKitStatus::NullPointer
    } else {
        unsafe {
            *out = value;
        }
        AccessKitStatus::Success
    }
}

#[repr(i32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum AccessKitStatus {
    Success = 0,
    NullPointer = 1,
    InvalidArgument = 2,
    JsonError = 3,
    OutOfMemory = 4,
}

#[repr(C)]
#[derive(Debug)]
pub struct AccessKitTreeUpdateHandle(TreeUpdate);

#[repr(C)]
#[derive(Debug)]
pub struct AccessKitActionRequestHandle(ActionRequest);

fn parse_json_input<'a>(json: *const c_char) -> Result<&'a str, AccessKitStatus> {
    if json.is_null() {
        set_last_error("JSON input pointer is null");
        return Err(AccessKitStatus::NullPointer);
    }

    let cstr = unsafe { CStr::from_ptr(json) };
    match cstr.to_str() {
        Ok(value) => Ok(value),
        Err(_) => {
            set_last_error("JSON input is not valid UTF-8");
            Err(AccessKitStatus::InvalidArgument)
        }
    }
}

unsafe fn serialize_to_c_string(json: String, out_json: *mut *mut c_char) -> AccessKitStatus {
    match CString::new(json) {
        Ok(cstring) => {
            let ptr = cstring.into_raw();
            unsafe { write_out_ptr(out_json, ptr) }
        }
        Err(_) => {
            set_last_error("JSON output contains interior null byte");
            AccessKitStatus::JsonError
        }
    }
}

#[unsafe(no_mangle)]
pub extern "C" fn accesskit_last_error_message() -> *const c_char {
    LAST_ERROR.with(|slot| match slot.borrow().as_ref() {
        Some(cstring) => cstring.as_ptr(),
        None => ptr::null(),
    })
}

#[unsafe(no_mangle)]
pub extern "C" fn accesskit_string_free(value: *mut c_char) {
    if value.is_null() {
        return;
    }

    unsafe {
        drop(CString::from_raw(value));
    }
}

#[unsafe(no_mangle)]
pub extern "C" fn accesskit_tree_update_from_json(
    json: *const c_char,
    out_handle: *mut *mut AccessKitTreeUpdateHandle,
) -> AccessKitStatus {
    clear_last_error();

    let json_str = match parse_json_input(json) {
        Ok(value) => value,
        Err(status) => return status,
    };

    match serde_json::from_str::<TreeUpdate>(json_str) {
        Ok(update) => {
            let boxed = Box::new(AccessKitTreeUpdateHandle(update));
            let ptr = Box::into_raw(boxed);
            unsafe { write_out_ptr(out_handle, ptr) }
        }
        Err(err) => {
            set_last_error(format!("failed to parse TreeUpdate JSON: {err}"));
            AccessKitStatus::JsonError
        }
    }
}

#[unsafe(no_mangle)]
pub extern "C" fn accesskit_tree_update_clone(
    handle: *const AccessKitTreeUpdateHandle,
    out_handle: *mut *mut AccessKitTreeUpdateHandle,
) -> AccessKitStatus {
    clear_last_error();

    if handle.is_null() {
        set_last_error("tree update handle is null");
        return AccessKitStatus::NullPointer;
    }

    let clone = unsafe { (*handle).0.clone() };
    let boxed = Box::new(AccessKitTreeUpdateHandle(clone));
    let ptr = Box::into_raw(boxed);
    unsafe { write_out_ptr(out_handle, ptr) }
}

#[unsafe(no_mangle)]
pub extern "C" fn accesskit_tree_update_to_json(
    handle: *const AccessKitTreeUpdateHandle,
    out_json: *mut *mut c_char,
) -> AccessKitStatus {
    clear_last_error();

    if handle.is_null() {
        set_last_error("tree update handle is null");
        return AccessKitStatus::NullPointer;
    }

    match serde_json::to_string(&unsafe { &(*handle).0 }) {
        Ok(json) => unsafe { serialize_to_c_string(json, out_json) },
        Err(err) => {
            set_last_error(format!("failed to serialize TreeUpdate to JSON: {err}"));
            AccessKitStatus::JsonError
        }
    }
}

#[unsafe(no_mangle)]
pub extern "C" fn accesskit_tree_update_destroy(handle: *mut AccessKitTreeUpdateHandle) {
    if handle.is_null() {
        return;
    }

    unsafe {
        drop(Box::from_raw(handle));
    }
}

#[unsafe(no_mangle)]
pub extern "C" fn accesskit_action_request_from_json(
    json: *const c_char,
    out_handle: *mut *mut AccessKitActionRequestHandle,
) -> AccessKitStatus {
    clear_last_error();

    let json_str = match parse_json_input(json) {
        Ok(value) => value,
        Err(status) => return status,
    };

    match serde_json::from_str::<ActionRequest>(json_str) {
        Ok(request) => {
            let boxed = Box::new(AccessKitActionRequestHandle(request));
            let ptr = Box::into_raw(boxed);
            unsafe { write_out_ptr(out_handle, ptr) }
        }
        Err(err) => {
            set_last_error(format!("failed to parse ActionRequest JSON: {err}"));
            AccessKitStatus::JsonError
        }
    }
}

#[unsafe(no_mangle)]
pub extern "C" fn accesskit_action_request_to_json(
    handle: *const AccessKitActionRequestHandle,
    out_json: *mut *mut c_char,
) -> AccessKitStatus {
    clear_last_error();

    if handle.is_null() {
        set_last_error("action request handle is null");
        return AccessKitStatus::NullPointer;
    }

    match serde_json::to_string(&unsafe { &(*handle).0 }) {
        Ok(json) => unsafe { serialize_to_c_string(json, out_json) },
        Err(err) => {
            set_last_error(format!("failed to serialize ActionRequest to JSON: {err}"));
            AccessKitStatus::JsonError
        }
    }
}

#[unsafe(no_mangle)]
pub extern "C" fn accesskit_action_request_destroy(handle: *mut AccessKitActionRequestHandle) {
    if handle.is_null() {
        return;
    }

    unsafe {
        drop(Box::from_raw(handle));
    }
}

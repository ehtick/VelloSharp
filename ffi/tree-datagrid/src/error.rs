use std::{
    cell::RefCell,
    ffi::{CString, c_char},
};

thread_local! {
    static LAST_ERROR: RefCell<Option<CString>> = const { RefCell::new(None) };
}

pub fn clear_last_error() {
    LAST_ERROR.with(|slot| slot.borrow_mut().take());
}

pub fn set_last_error(message: impl Into<String>) {
    let msg = message.into();
    let cstring = CString::new(msg).unwrap_or_else(|_| CString::new("invalid error").unwrap());
    LAST_ERROR.with(|slot| slot.borrow_mut().replace(cstring));
}

pub fn last_error_ptr() -> *const c_char {
    LAST_ERROR.with(|slot| {
        slot.borrow()
            .as_ref()
            .map_or(std::ptr::null(), |value| value.as_ptr())
    })
}

//! Core native editing primitives for the unified visual editor.

#![allow(clippy::missing_panics_doc)]
#![allow(clippy::missing_errors_doc)]

use once_cell::sync::OnceCell;

static INITIALIZED: OnceCell<()> = OnceCell::new();

/// Initializes the editor core bridge.
#[unsafe(no_mangle)]
pub extern "C" fn vello_editor_core_initialize() -> bool {
    INITIALIZED.get_or_init(|| ());
    true
}

/// Returns `true` if the editor core bridge has been initialized.
#[unsafe(no_mangle)]
pub extern "C" fn vello_editor_core_is_initialized() -> bool {
    INITIALIZED.get().is_some()
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn initializes_editor_core() {
        assert!(vello_editor_core_initialize());
        assert!(vello_editor_core_is_initialized());
    }
}

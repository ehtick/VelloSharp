//! Core native library for VelloSharp gauge primitives.

#![allow(clippy::missing_panics_doc)]
#![allow(clippy::missing_errors_doc)]

use once_cell::sync::OnceCell;

static INITIALIZED: OnceCell<()> = OnceCell::new();

/// Initializes the native gauges module.
///
/// The placeholder implementation simply records initialization so that
/// managed bindings can validate linkage during early integration work.
#[unsafe(no_mangle)]
pub extern "C" fn vello_gauges_initialize() -> bool {
    INITIALIZED.get_or_init(|| ());
    true
}

/// Returns `true` when the gauges module has been initialized.
#[unsafe(no_mangle)]
pub extern "C" fn vello_gauges_is_initialized() -> bool {
    INITIALIZED.get().is_some()
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn initializes_once() {
        assert!(vello_gauges_initialize());
        assert!(vello_gauges_is_initialized());
    }
}

//! Native runtime entry points for VelloSharp SCADA dashboards.

#![allow(clippy::missing_panics_doc)]
#![allow(clippy::missing_errors_doc)]

use once_cell::sync::OnceCell;

static INITIALIZED: OnceCell<()> = OnceCell::new();

/// Initializes the SCADA runtime bridge.
#[unsafe(no_mangle)]
pub extern "C" fn vello_scada_runtime_initialize() -> bool {
    INITIALIZED.get_or_init(|| ());
    true
}

/// Returns `true` when the SCADA runtime bridge has been initialized.
#[unsafe(no_mangle)]
pub extern "C" fn vello_scada_runtime_is_initialized() -> bool {
    INITIALIZED.get().is_some()
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn initializes_runtime() {
        assert!(vello_scada_runtime_initialize());
        assert!(vello_scada_runtime_is_initialized());
    }
}

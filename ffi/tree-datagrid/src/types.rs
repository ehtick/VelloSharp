#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum FrozenKind {
    None,
    Leading,
    Trailing,
}

#[derive(Clone, Copy, Debug)]
pub struct ColumnStrip {
    pub offset: f64,
    pub width: f64,
    pub frozen: FrozenKind,
    pub key: u32,
}

impl ColumnStrip {
    pub fn new(offset: f64, width: f64, frozen: FrozenKind, key: u32) -> Self {
        Self {
            offset,
            width,
            frozen,
            key,
        }
    }
}

#[derive(Clone, Copy, Debug, Default)]
pub struct FrozenColumns {
    pub leading: u32,
    pub trailing: u32,
}

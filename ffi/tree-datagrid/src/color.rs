use vello::peniko::Color;

#[repr(C)]
#[derive(Debug, Clone, Copy, Default)]
pub struct VelloTdgColor {
    pub r: f32,
    pub g: f32,
    pub b: f32,
    pub a: f32,
}

impl VelloTdgColor {
    pub fn to_color(self) -> Color {
        let clamp = |value: f32| value.clamp(0.0, 1.0);
        Color::new([clamp(self.r), clamp(self.g), clamp(self.b), clamp(self.a)])
    }

    pub fn with_alpha(self, alpha: f32) -> Self {
        Self {
            r: self.r,
            g: self.g,
            b: self.b,
            a: alpha,
        }
    }

    pub fn lighten(self, amount: f32) -> Self {
        let lerp = |component: f32| component + (1.0 - component) * amount.clamp(0.0, 1.0);
        Self {
            r: lerp(self.r),
            g: lerp(self.g),
            b: lerp(self.b),
            a: self.a,
        }
    }
}

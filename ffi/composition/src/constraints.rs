use std::fmt;

/// Scalar constraint describing the minimum, preferred, and maximum size for a dimension.
#[derive(Clone, Copy, Debug, PartialEq)]
pub struct ScalarConstraint {
    pub min: f64,
    pub preferred: f64,
    pub max: f64,
}

impl ScalarConstraint {
    /// Creates a constraint where all bounds are the same value.
    pub const fn tight(value: f64) -> Self {
        Self {
            min: value,
            preferred: value,
            max: value,
        }
    }

    /// Creates a constraint that clamps a dimension to the given min and max, with the preferred value set to min.
    pub const fn min_max(min: f64, max: f64) -> Self {
        Self {
            min,
            preferred: min,
            max,
        }
    }

    /// Creates a custom constraint.
    pub const fn new(min: f64, preferred: f64, max: f64) -> Self {
        Self {
            min,
            preferred,
            max,
        }
    }

    /// Returns a sanitised constraint ensuring invariants hold.
    pub fn normalised(self) -> Self {
        let mut min = sanitise_non_negative(self.min);
        let mut max = sanitise_non_negative(self.max);
        if max.is_nan() || !max.is_finite() {
            max = f64::INFINITY;
        }

        if min.is_nan() {
            min = 0.0;
        }
        if !min.is_finite() {
            min = 0.0;
        }
        if max < min {
            max = min;
        }

        let mut preferred = self.preferred;
        if preferred.is_nan() {
            preferred = min;
        }
        if !preferred.is_finite() {
            preferred = min.max(1.0).min(max);
        }
        preferred = preferred.clamp(min, max);

        Self {
            min,
            preferred,
            max,
        }
    }

    /// Resolves the constraint against the available amount, returning a clamped size value.
    pub fn resolve(self, available: f64) -> f64 {
        let constraint = self.normalised();
        if constraint.max == constraint.min {
            return constraint.max;
        }

        let mut avail = available;
        if avail.is_nan() {
            avail = constraint.preferred;
        }
        if !avail.is_finite() {
            avail = constraint.preferred;
        }
        avail = avail.clamp(constraint.min, constraint.max);

        if avail >= constraint.preferred {
            constraint.preferred
        } else {
            avail
        }
    }

    /// Returns the clamped minimum, preferred, and maximum tuple for convenience.
    pub fn span(self) -> (f64, f64, f64) {
        let constraint = self.normalised();
        (constraint.min, constraint.preferred, constraint.max)
    }
}

/// Two-dimensional layout constraints for width and height.
#[derive(Clone, Copy, Debug, PartialEq)]
pub struct LayoutConstraints {
    pub width: ScalarConstraint,
    pub height: ScalarConstraint,
}

impl LayoutConstraints {
    pub const fn new(width: ScalarConstraint, height: ScalarConstraint) -> Self {
        Self { width, height }
    }

    pub fn tight(width: f64, height: f64) -> Self {
        Self {
            width: ScalarConstraint::tight(width),
            height: ScalarConstraint::tight(height),
        }
    }

    pub fn normalised(self) -> Self {
        Self {
            width: self.width.normalised(),
            height: self.height.normalised(),
        }
    }

    pub fn resolve(self, available: LayoutSize) -> LayoutSize {
        let normalised = self.normalised();
        LayoutSize {
            width: normalised.width.resolve(available.width),
            height: normalised.height.resolve(available.height),
        }
    }
}

impl Default for LayoutConstraints {
    fn default() -> Self {
        Self {
            width: ScalarConstraint::min_max(0.0, f64::INFINITY),
            height: ScalarConstraint::min_max(0.0, f64::INFINITY),
        }
    }
}

/// Convenience type used when solving layout.
#[derive(Clone, Copy, Debug, Default, PartialEq)]
pub struct LayoutSize {
    pub width: f64,
    pub height: f64,
}

impl LayoutSize {
    pub const fn new(width: f64, height: f64) -> Self {
        Self { width, height }
    }

    pub fn clamp_non_negative(mut self) -> Self {
        if !self.width.is_finite() {
            self.width = 0.0;
        }
        if !self.height.is_finite() {
            self.height = 0.0;
        }
        if self.width.is_nan() {
            self.width = 0.0;
        }
        if self.height.is_nan() {
            self.height = 0.0;
        }
        self.width = self.width.max(0.0);
        self.height = self.height.max(0.0);
        self
    }
}

impl fmt::Display for ScalarConstraint {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(
            f,
            "ScalarConstraint[min={}, preferred={}, max={}]",
            self.min, self.preferred, self.max
        )
    }
}

#[inline]
fn sanitise_non_negative(value: f64) -> f64 {
    if value.is_nan() || value.is_sign_negative() {
        0.0
    } else {
        value
    }
}

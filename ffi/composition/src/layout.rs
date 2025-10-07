use time::OffsetDateTime;

#[derive(Debug, Clone, Copy, Default)]
pub struct PlotArea {
    pub left: f64,
    pub top: f64,
    pub width: f64,
    pub height: f64,
}

impl PlotArea {
    #[inline]
    pub fn right(&self) -> f64 {
        self.left + self.width
    }

    #[inline]
    pub fn bottom(&self) -> f64 {
        self.top + self.height
    }
}

#[derive(Debug, Clone, Default)]
pub struct AxisTick {
    pub position: f64,
    pub label: String,
}

#[derive(Debug, Clone, Default)]
pub struct AxisLayout {
    pub time_ticks: Vec<AxisTick>,
    pub value_ticks: Vec<AxisTick>,
}

impl AxisLayout {
    #[inline]
    pub fn new(time_ticks: Vec<AxisTick>, value_ticks: Vec<AxisTick>) -> Self {
        Self {
            time_ticks,
            value_ticks,
        }
    }
}

pub const MIN_PLOT_DIMENSION: f64 = 32.0;

const PLOT_LEFT_MARGIN_RATIO: f64 = 0.12;
const PLOT_RIGHT_MARGIN_RATIO: f64 = 0.04;
const PLOT_TOP_MARGIN_RATIO: f64 = 0.08;
const PLOT_BOTTOM_MARGIN_RATIO: f64 = 0.12;
const PLOT_LEFT_MARGIN_MIN: f64 = 48.0;
const PLOT_BOTTOM_MARGIN_MIN: f64 = 32.0;

#[allow(clippy::float_cmp)]
pub fn compute_plot_area(width: f64, height: f64) -> PlotArea {
    if width <= 0.0 || height <= 0.0 {
        return PlotArea {
            left: 0.0,
            top: 0.0,
            width: width.max(MIN_PLOT_DIMENSION),
            height: height.max(MIN_PLOT_DIMENSION),
        };
    }

    let mut left_margin = if width >= PLOT_LEFT_MARGIN_MIN * 2.0 {
        PLOT_LEFT_MARGIN_MIN
    } else {
        width * PLOT_LEFT_MARGIN_RATIO
    };
    let mut right_margin = width * PLOT_RIGHT_MARGIN_RATIO;
    let mut top_margin = height * PLOT_TOP_MARGIN_RATIO;
    let mut bottom_margin = if height >= PLOT_BOTTOM_MARGIN_MIN * 2.0 {
        PLOT_BOTTOM_MARGIN_MIN
    } else {
        height * PLOT_BOTTOM_MARGIN_RATIO
    };

    let horizontal_sum = (left_margin + right_margin).max(1e-6);
    if horizontal_sum + MIN_PLOT_DIMENSION > width {
        let available = (width - MIN_PLOT_DIMENSION).max(0.0);
        let scale = if horizontal_sum.abs() < 1e-6 {
            0.0
        } else {
            available / horizontal_sum
        };
        left_margin *= scale;
        right_margin *= scale;
    }

    let vertical_sum = (top_margin + bottom_margin).max(1e-6);
    if vertical_sum + MIN_PLOT_DIMENSION > height {
        let available = (height - MIN_PLOT_DIMENSION).max(0.0);
        let scale = if vertical_sum.abs() < 1e-6 {
            0.0
        } else {
            available / vertical_sum
        };
        top_margin *= scale;
        bottom_margin *= scale;
    }

    let mut plot_width = (width - left_margin - right_margin).max(MIN_PLOT_DIMENSION);
    if plot_width > width {
        plot_width = width;
    }
    let mut left = left_margin.min(width - plot_width).max(0.0);
    let mut right = (left + plot_width).min(width);
    if right - left < MIN_PLOT_DIMENSION {
        right = (left + MIN_PLOT_DIMENSION).min(width);
        left = (right - MIN_PLOT_DIMENSION).max(0.0);
    }
    plot_width = (right - left).max(MIN_PLOT_DIMENSION);

    let mut plot_height = (height - top_margin - bottom_margin).max(MIN_PLOT_DIMENSION);
    if plot_height > height {
        plot_height = height;
    }
    let mut top = top_margin.min(height - plot_height).max(0.0);
    let mut bottom = (top + plot_height).min(height);
    if bottom - top < MIN_PLOT_DIMENSION {
        bottom = (top + MIN_PLOT_DIMENSION).min(height);
        top = (bottom - MIN_PLOT_DIMENSION).max(0.0);
    }
    plot_height = (bottom - top).max(MIN_PLOT_DIMENSION);

    PlotArea {
        left,
        top,
        width: plot_width,
        height: plot_height,
    }
}

pub fn compute_axis_layout(
    range_start: f64,
    range_end: f64,
    min_value: f64,
    max_value: f64,
    max_ticks: usize,
) -> AxisLayout {
    AxisLayout {
        time_ticks: compute_time_ticks(range_start, range_end, max_ticks.max(2)),
        value_ticks: compute_value_ticks(min_value, max_value, max_ticks.max(2)),
    }
}

fn compute_time_ticks(range_start: f64, range_end: f64, max_ticks: usize) -> Vec<AxisTick> {
    let mut ticks = Vec::new();
    if !range_start.is_finite() || !range_end.is_finite() || range_end <= range_start {
        return ticks;
    }

    let total = range_end - range_start;
    let approx_step = total / (max_ticks as f64);
    let step = pick_time_step(approx_step);
    if step <= 0.0 {
        return ticks;
    }

    let first_tick = (range_start / step).ceil() * step;
    let mut value = first_tick;
    while value <= range_end + step * 0.5 {
        let position = ((value - range_start) / total).clamp(0.0, 1.0);
        ticks.push(AxisTick {
            position,
            label: format_time_label(value, step),
        });
        value += step;
    }

    if ticks.is_empty() {
        ticks.push(AxisTick {
            position: 0.0,
            label: format_time_label(range_start, step),
        });
        ticks.push(AxisTick {
            position: 1.0,
            label: format_time_label(range_end, step),
        });
    }

    ticks
}

fn pick_time_step(target: f64) -> f64 {
    const STEPS: &[f64] = &[
        0.5, 1.0, 2.0, 5.0, 10.0, 15.0, 30.0, 60.0, 120.0, 300.0, 600.0, 900.0, 1800.0, 3600.0,
        7200.0, 14_400.0, 28_800.0, 43_200.0, 86_400.0,
    ];

    for step in STEPS {
        if *step >= target {
            return *step;
        }
    }

    *STEPS.last().unwrap_or(&60.0)
}

fn format_time_label(timestamp: f64, step: f64) -> String {
    if !timestamp.is_finite() {
        return "NaN".to_string();
    }

    let secs = timestamp.floor();
    let subseconds = ((timestamp - secs) * 1_000.0).round() as i64;
    let secs_i64 = secs as i64;

    if let Ok(dt) = OffsetDateTime::from_unix_timestamp(secs_i64) {
        let time = dt.time();
        if step < 1.0 {
            let millis = subseconds.clamp(0, 999);
            format!(
                "{:02}:{:02}:{:02}.{:03}",
                time.hour(),
                time.minute(),
                time.second(),
                millis
            )
        } else if step < 60.0 {
            format!(
                "{:02}:{:02}:{:02}",
                time.hour(),
                time.minute(),
                time.second()
            )
        } else {
            format!("{:02}:{:02}", time.hour(), time.minute())
        }
    } else {
        format!("{timestamp:.0}")
    }
}

fn compute_value_ticks(min_value: f64, max_value: f64, max_ticks: usize) -> Vec<AxisTick> {
    let mut ticks = Vec::new();
    if !min_value.is_finite() || !max_value.is_finite() || max_value <= min_value {
        return ticks;
    }

    let range = nice_number(max_value - min_value, false);
    let step = nice_number(range / ((max_ticks - 1) as f64), true);
    if step <= 0.0 {
        return ticks;
    }

    let mut tick = (min_value / step).floor() * step;
    let tick_max = (max_value / step).ceil() * step;
    while tick <= tick_max + step * 0.5 {
        let position = ((tick - min_value) / (max_value - min_value)).clamp(0.0, 1.0);
        ticks.push(AxisTick {
            position,
            label: format_value_label(tick, step),
        });
        tick += step;
    }

    ticks
}

fn nice_number(range: f64, round: bool) -> f64 {
    if range <= 0.0 || !range.is_finite() {
        return 0.0;
    }

    let exponent = range.log10().floor();
    let fraction = range / 10f64.powf(exponent);

    let nice_fraction = if round {
        if fraction < 1.5 {
            1.0
        } else if fraction < 3.0 {
            2.0
        } else if fraction < 7.0 {
            5.0
        } else {
            10.0
        }
    } else if fraction <= 1.0 {
        1.0
    } else if fraction <= 2.0 {
        2.0
    } else if fraction <= 5.0 {
        5.0
    } else {
        10.0
    };

    nice_fraction * 10f64.powf(exponent)
}

fn format_value_label(value: f64, step: f64) -> String {
    if !value.is_finite() || !step.is_finite() {
        return "NaN".to_string();
    }

    let mut decimals = 0usize;
    if step > 0.0 {
        let log10 = step.log10();
        if log10 < 0.0 {
            decimals = (-log10).ceil() as usize;
        }
    }
    decimals = decimals.min(6);
    format!("{value:.decimals$}")
}

#[cfg(test)]
mod tests {
    use super::*;

    fn assert_close(lhs: f64, rhs: f64) {
        assert!(
            (lhs - rhs).abs() <= 1e-6,
            "expected {lhs} ≈ {rhs}, delta={}",
            (lhs - rhs).abs()
        );
    }

    #[test]
    fn compute_plot_area_uses_expected_margins() {
        let plot = compute_plot_area(1920.0, 1080.0);
        assert_close(plot.left, 48.0);
        assert_close(plot.top, 86.4);
        assert_close(plot.width, 1795.2);
        assert_close(plot.height, 961.6);
    }

    #[test]
    fn compute_plot_area_preserves_minimum_dimensions() {
        let plot = compute_plot_area(40.0, 28.0);
        assert_close(plot.left, 4.8);
        assert_close(plot.top, 0.0);
        assert_close(plot.width, 33.6);
        assert_close(plot.height, MIN_PLOT_DIMENSION);
    }

    #[test]
    fn axis_layout_generates_ticks_for_value_and_time_ranges() {
        let layout = compute_axis_layout(0.0, 3600.0, -12.0, 108.0, 6);
        assert!(layout.time_ticks.len() >= 4, "expected multiple time ticks");
        assert!(
            layout
                .time_ticks
                .iter()
                .all(|tick| tick.position >= 0.0 && tick.position <= 1.0),
            "time ticks must be normalised"
        );
        assert!(
            layout.value_ticks.len() >= 4,
            "expected multiple value ticks"
        );
        assert!(
            layout
                .value_ticks
                .iter()
                .all(|tick| tick.position >= 0.0 && tick.position <= 1.0),
            "value ticks must be normalised"
        );
        assert!(
            layout
                .value_ticks
                .windows(2)
                .all(|pair| pair[0].position <= pair[1].position),
            "value ticks must be monotonically increasing"
        );
    }
}

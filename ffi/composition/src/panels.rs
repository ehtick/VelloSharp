use crate::constraints::{LayoutConstraints, LayoutSize, ScalarConstraint};
use crate::linear_layout::{LinearLayoutItem, LinearLayoutSlot, solve_linear_layout};

const EPSILON: f64 = 1e-6;

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum LayoutOrientation {
    Horizontal,
    Vertical,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum LayoutAlignment {
    Start,
    Center,
    End,
    Stretch,
}

#[derive(Clone, Copy, Debug, Default, PartialEq)]
pub struct LayoutThickness {
    pub left: f64,
    pub top: f64,
    pub right: f64,
    pub bottom: f64,
}

impl LayoutThickness {
    pub const ZERO: Self = Self {
        left: 0.0,
        top: 0.0,
        right: 0.0,
        bottom: 0.0,
    };

    pub fn horizontal(&self) -> f64 {
        self.left.max(0.0) + self.right.max(0.0)
    }

    pub fn vertical(&self) -> f64 {
        self.top.max(0.0) + self.bottom.max(0.0)
    }
}

#[derive(Clone, Copy, Debug, Default, PartialEq)]
pub struct LayoutRect {
    pub x: f64,
    pub y: f64,
    pub width: f64,
    pub height: f64,
    pub primary_offset: f64,
    pub primary_length: f64,
    pub line_index: u32,
}

impl LayoutRect {
    pub const fn new(x: f64, y: f64, width: f64, height: f64) -> Self {
        Self {
            x,
            y,
            width,
            height,
            primary_offset: y,
            primary_length: height,
            line_index: 0,
        }
    }
}

#[derive(Clone, Copy, Debug)]
pub struct StackLayoutChild {
    pub constraints: LayoutConstraints,
    pub weight: f64,
    pub margin: LayoutThickness,
    pub cross_alignment: LayoutAlignment,
}

impl StackLayoutChild {
    pub fn new(constraints: LayoutConstraints) -> Self {
        Self {
            constraints,
            weight: 1.0,
            margin: LayoutThickness::ZERO,
            cross_alignment: LayoutAlignment::Stretch,
        }
    }
}

#[derive(Clone, Copy, Debug)]
pub struct StackLayoutOptions {
    pub orientation: LayoutOrientation,
    pub spacing: f64,
    pub padding: LayoutThickness,
    pub cross_alignment: LayoutAlignment,
}

impl Default for StackLayoutOptions {
    fn default() -> Self {
        Self {
            orientation: LayoutOrientation::Vertical,
            spacing: 0.0,
            padding: LayoutThickness::ZERO,
            cross_alignment: LayoutAlignment::Stretch,
        }
    }
}

pub fn solve_stack_layout(
    children: &[StackLayoutChild],
    options: StackLayoutOptions,
    available: LayoutSize,
) -> Vec<LayoutRect> {
    if children.is_empty() {
        return Vec::new();
    }

    let spacing = options.spacing.max(0.0);
    let padding = clamp_thickness(options.padding);
    let orientation = options.orientation;

    let (available_main, available_cross) = match orientation {
        LayoutOrientation::Horizontal => (
            (available.width - padding.horizontal()).max(0.0),
            (available.height - padding.vertical()).max(0.0),
        ),
        LayoutOrientation::Vertical => (
            (available.height - padding.vertical()).max(0.0),
            (available.width - padding.horizontal()).max(0.0),
        ),
    };

    let mut layout_items = Vec::with_capacity(children.len());
    for child in children {
        let constraint = match orientation {
            LayoutOrientation::Horizontal => child.constraints.width,
            LayoutOrientation::Vertical => child.constraints.height,
        };
        let mut item = LinearLayoutItem::new(constraint);
        item.weight = sanitise_weight(child.weight);
        item.margin_leading = match orientation {
            LayoutOrientation::Horizontal => child.margin.left,
            LayoutOrientation::Vertical => child.margin.top,
        }
        .max(0.0);
        item.margin_trailing = match orientation {
            LayoutOrientation::Horizontal => child.margin.right,
            LayoutOrientation::Vertical => child.margin.bottom,
        }
        .max(0.0);
        layout_items.push(item);
    }

    let mut slots = solve_linear_layout(&layout_items, available_main, spacing);
    if slots.len() != children.len() {
        slots = adjust_slot_count(slots, children.len());
    }

    let mut results = Vec::with_capacity(children.len());
    for (index, (child, slot)) in children.iter().zip(slots.iter()).enumerate() {
        let (x, y, width, height, primary_offset, primary_length) = match orientation {
            LayoutOrientation::Horizontal => {
                let width = slot.length.max(0.0);
                let height = resolve_cross(
                    child.constraints.height,
                    available_cross,
                    child.margin.top,
                    child.margin.bottom,
                    options.cross_alignment,
                    child.cross_alignment,
                );
                let (cross_offset, cross_length) = align_cross(
                    available_cross,
                    height,
                    child.margin.top,
                    child.margin.bottom,
                    padding.top,
                    padding.bottom,
                    options.cross_alignment,
                    child.cross_alignment,
                );

                let x = padding.left + slot.offset + child.margin.left.max(0.0);
                let width =
                    (width - child.margin.left.max(0.0) - child.margin.right.max(0.0)).max(0.0);
                let y = cross_offset;
                let height = height.min(cross_length);
                (x, y, width, height, padding.left + slot.offset, slot.length)
            }
            LayoutOrientation::Vertical => {
                let height = slot.length.max(0.0);
                let width = resolve_cross(
                    child.constraints.width,
                    available_cross,
                    child.margin.left,
                    child.margin.right,
                    options.cross_alignment,
                    child.cross_alignment,
                );
                let (cross_offset, cross_length) = align_cross(
                    available_cross,
                    width,
                    child.margin.left,
                    child.margin.right,
                    padding.left,
                    padding.right,
                    options.cross_alignment,
                    child.cross_alignment,
                );

                let y = padding.top + slot.offset + child.margin.top.max(0.0);
                let height =
                    (height - child.margin.top.max(0.0) - child.margin.bottom.max(0.0)).max(0.0);
                let x = cross_offset;
                let width = width.min(cross_length);
                (x, y, width, height, padding.top + slot.offset, slot.length)
            }
        };

        let mut rect = LayoutRect {
            x,
            y,
            width,
            height,
            primary_offset,
            primary_length,
            line_index: index as u32,
        };
        clamp_rect(&mut rect);
        results.push(rect);
    }

    results
}

fn solve_grid_tracks(
    tracks: &[GridTrack],
    children: &[GridLayoutChild],
    available_size: f64,
    spacing: f64,
    padding_leading: f64,
    horizontal: bool,
) -> Vec<(f64, f64)> {
    let count = tracks.len();
    let spacing_total = spacing * (count.saturating_sub(1) as f64);
    let mut sizes = vec![0.0f64; count];
    let mut min_sizes = vec![0.0f64; count];
    let mut star_weights = vec![0.0f64; count];

    let mut fixed_total = 0.0;

    for (index, track) in tracks.iter().enumerate() {
        match track.kind {
            GridTrackKind::Fixed(value) => {
                sizes[index] = value.max(track.min).min(track.max);
                fixed_total += sizes[index];
            }
            GridTrackKind::Auto => {
                sizes[index] = 0.0;
            }
            GridTrackKind::Star(weight) => {
                star_weights[index] = weight;
            }
        }
    }

    for child in children {
        let start = if horizontal {
            child.column as usize
        } else {
            child.row as usize
        };
        let span = if horizontal {
            child.column_span.max(1) as usize
        } else {
            child.row_span.max(1) as usize
        };
        let span = span.min(count.saturating_sub(start).max(1));

        let constraint = if horizontal {
            child.constraints.width.normalised()
        } else {
            child.constraints.height.normalised()
        };

        let desired = constraint.preferred.max(constraint.min);
        let share = desired / span as f64;

        for offset in 0..span {
            let index = start + offset;
            if index >= count {
                break;
            }
            if matches!(tracks[index].kind, GridTrackKind::Auto) {
                sizes[index] = sizes[index].max(share).max(tracks[index].min);
            }
            min_sizes[index] = min_sizes[index].max(constraint.min / span as f64);
        }
    }

    let mut auto_total = 0.0;
    for (index, size) in sizes.iter_mut().enumerate() {
        if matches!(tracks[index].kind, GridTrackKind::Auto) {
            let clamped = size.clamp(tracks[index].min, tracks[index].max);
            *size = clamped;
            auto_total += clamped;
        }
    }

    let available_length = if available_size.is_infinite() {
        fixed_total + auto_total
    } else {
        (available_size - padding_leading - spacing_total).max(0.0)
    };

    let remaining = (available_length - fixed_total - auto_total).max(0.0);

    let total_star_weight: f64 = star_weights.iter().sum();
    if total_star_weight > EPSILON {
        for (index, weight) in star_weights.iter().enumerate() {
            if *weight <= EPSILON {
                continue;
            }

            let share = remaining * (*weight / total_star_weight);
            let size = share.clamp(tracks[index].min, tracks[index].max);
            sizes[index] = size.max(min_sizes[index]);
        }
    }

    let mut offsets = Vec::with_capacity(count);
    let mut cursor = padding_leading;
    for size in sizes.iter() {
        offsets.push((cursor, *size));
        cursor += size + spacing;
    }

    offsets
}

fn resolve_grid_slot(
    offsets: &[(f64, f64)],
    start: usize,
    span: usize,
    spacing: f64,
    margin_leading: f64,
    margin_trailing: f64,
    constraint: ScalarConstraint,
    alignment: LayoutAlignment,
) -> (f64, f64) {
    let mut total = 0.0;
    let mut start_offset = offsets
        .get(start)
        .map(|(offset, _)| *offset + margin_leading.max(0.0))
        .unwrap_or(0.0);
    for idx in start..start + span {
        if let Some((offset, size)) = offsets.get(idx) {
            if idx == start {
                start_offset = *offset + margin_leading.max(0.0);
            }
            total += size;
        }
    }
    total += spacing * (span.saturating_sub(1) as f64);
    total -= margin_leading.max(0.0) + margin_trailing.max(0.0);
    total = total.max(0.0);

    let resolved = constraint.resolve(total);

    let length = match alignment {
        LayoutAlignment::Stretch => total.max(resolved),
        LayoutAlignment::Start | LayoutAlignment::Center | LayoutAlignment::End => resolved,
    };

    let position = match alignment {
        LayoutAlignment::Start | LayoutAlignment::Stretch => start_offset,
        LayoutAlignment::Center => start_offset + (total - length).max(0.0) * 0.5,
        LayoutAlignment::End => start_offset + (total - length).max(0.0),
    };

    (position, length.max(0.0))
}

fn resolve_cross(
    constraint: ScalarConstraint,
    available: f64,
    margin_leading: f64,
    margin_trailing: f64,
    panel_alignment: LayoutAlignment,
    child_alignment: LayoutAlignment,
) -> f64 {
    let total_margin = margin_leading.max(0.0) + margin_trailing.max(0.0);
    let resolved = constraint.resolve(available.max(0.0).max(total_margin));
    match (panel_alignment, child_alignment) {
        (_, LayoutAlignment::Stretch) => (available - total_margin).max(resolved),
        (LayoutAlignment::Stretch, _) => (available - total_margin).max(resolved),
        _ => resolved,
    }
}

fn align_cross(
    available: f64,
    resolved: f64,
    margin_leading: f64,
    margin_trailing: f64,
    padding_leading: f64,
    _padding_trailing: f64,
    panel_alignment: LayoutAlignment,
    child_alignment: LayoutAlignment,
) -> (f64, f64) {
    let margin_leading = margin_leading.max(0.0);
    let margin_trailing = margin_trailing.max(0.0);
    let available = (available - margin_leading - margin_trailing).max(0.0);
    let offset_base = padding_leading + margin_leading;
    let length = match (panel_alignment, child_alignment) {
        (LayoutAlignment::Stretch, _) | (_, LayoutAlignment::Stretch) => available,
        _ => resolved.min(available),
    };

    let offset = match child_alignment {
        LayoutAlignment::Start | LayoutAlignment::Stretch => offset_base,
        LayoutAlignment::Center => offset_base + (available - length).max(0.0) * 0.5,
        LayoutAlignment::End => padding_leading + (available - length).max(0.0) + margin_trailing,
    };

    (offset, length.max(0.0))
}

fn align_cross_value(
    available: f64,
    resolved: f64,
    margin_leading: f64,
    margin_trailing: f64,
    padding_leading: f64,
    _padding_trailing: f64,
    panel_alignment: LayoutAlignment,
    child_alignment: LayoutAlignment,
) -> f64 {
    align_cross(
        available,
        resolved,
        margin_leading,
        margin_trailing,
        padding_leading,
        _padding_trailing,
        panel_alignment,
        child_alignment,
    )
    .0
}

fn align_length(
    available: f64,
    desired: f64,
    margin_leading: f64,
    margin_trailing: f64,
    alignment: LayoutAlignment,
) -> f64 {
    let desired = desired.max(0.0);
    let available = (available - margin_leading.max(0.0) - margin_trailing.max(0.0)).max(0.0);
    match alignment {
        LayoutAlignment::Stretch => available.max(desired),
        LayoutAlignment::Start | LayoutAlignment::Center | LayoutAlignment::End => {
            desired.min(available)
        }
    }
}

fn align_position(
    position: f64,
    available: f64,
    length: f64,
    margin_leading: f64,
    margin_trailing: f64,
    alignment: LayoutAlignment,
) -> f64 {
    let available = (available - margin_leading.max(0.0) - margin_trailing.max(0.0)).max(0.0);
    match alignment {
        LayoutAlignment::Start | LayoutAlignment::Stretch => position,
        LayoutAlignment::Center => position + (available - length).max(0.0) * 0.5,
        LayoutAlignment::End => position + (available - length).max(0.0),
    }
}

fn clamp_length(value: f64) -> f64 {
    if value.is_nan() { 0.0 } else { value.max(0.0) }
}

fn clamp_rect(rect: &mut LayoutRect) {
    if rect.x.is_nan() {
        rect.x = 0.0;
    }
    if rect.y.is_nan() {
        rect.y = 0.0;
    }
    if rect.width.is_nan() || rect.width.is_sign_negative() {
        rect.width = 0.0;
    }
    if rect.height.is_nan() || rect.height.is_sign_negative() {
        rect.height = 0.0;
    }
    if rect.primary_offset.is_nan() {
        rect.primary_offset = 0.0;
    }
    if rect.primary_length.is_nan() || rect.primary_length.is_sign_negative() {
        rect.primary_length = 0.0;
    }
}

fn clamp_thickness(thickness: LayoutThickness) -> LayoutThickness {
    LayoutThickness {
        left: thickness.left.max(0.0),
        top: thickness.top.max(0.0),
        right: thickness.right.max(0.0),
        bottom: thickness.bottom.max(0.0),
    }
}

fn adjust_slot_count(mut slots: Vec<LinearLayoutSlot>, target: usize) -> Vec<LinearLayoutSlot> {
    if slots.len() == target {
        return slots;
    }
    slots.resize(
        target,
        LinearLayoutSlot {
            offset: 0.0,
            length: 0.0,
        },
    );
    slots
}

fn sanitise_weight(weight: f64) -> f64 {
    if weight.is_nan() || weight <= 0.0 || !weight.is_finite() {
        1.0
    } else {
        weight
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn constraints(width: (f64, f64, f64), height: (f64, f64, f64)) -> LayoutConstraints {
        LayoutConstraints {
            width: ScalarConstraint::new(width.0, width.1, width.2),
            height: ScalarConstraint::new(height.0, height.1, height.2),
        }
    }

    #[test]
    fn stack_layout_vertical_allocates_space() {
        let children = vec![
            StackLayoutChild::new(constraints((0.0, 50.0, 50.0), (0.0, 20.0, 20.0))),
            StackLayoutChild::new(constraints((0.0, 50.0, f64::INFINITY), (0.0, 20.0, 20.0))),
        ];
        let available = LayoutSize::new(100.0, 200.0);
        let options = StackLayoutOptions {
            orientation: LayoutOrientation::Vertical,
            spacing: 4.0,
            padding: LayoutThickness {
                left: 5.0,
                top: 5.0,
                right: 5.0,
                bottom: 5.0,
            },
            cross_alignment: LayoutAlignment::Stretch,
        };

        let result = solve_stack_layout(&children, options, available);
        assert_eq!(result.len(), 2);
        assert_eq!(result[0].y, 5.0);
        assert!(
            (result[1].y - (5.0 + result[0].primary_length + 4.0)).abs() < 1e-6,
            "expected spacing between items"
        );
    }

    #[test]
    fn wrap_layout_breaks_lines() {
        let child = WrapLayoutChild::new(constraints((0.0, 60.0, 60.0), (0.0, 20.0, 20.0)));
        let children = vec![child; 4];
        let available = LayoutSize::new(120.0, 200.0);
        let options = WrapLayoutOptions {
            orientation: LayoutOrientation::Horizontal,
            item_spacing: 0.0,
            line_spacing: 0.0,
            padding: LayoutThickness::ZERO,
            line_alignment: LayoutAlignment::Start,
            cross_alignment: LayoutAlignment::Stretch,
        };

        let result = solve_wrap_layout(&children, options, available);
        assert_eq!(result.items.len(), 4);
        assert_eq!(result.lines.len(), 2);
        assert_eq!(result.lines[0].count, 2);
        assert_eq!(result.lines[1].count, 2);
    }

    #[test]
    fn grid_layout_places_children() {
        let columns = [GridTrack::fixed(50.0), GridTrack::star(1.0)];
        let rows = [GridTrack::auto(), GridTrack::star(1.0)];

        let children = vec![
            GridLayoutChild::new(constraints((0.0, 50.0, 50.0), (0.0, 30.0, 30.0)), 0, 0),
            {
                let mut child =
                    GridLayoutChild::new(constraints((0.0, 50.0, 50.0), (0.0, 30.0, 30.0)), 1, 1);
                child.column_span = 1;
                child.row_span = 1;
                child
            },
        ];

        let options = GridLayoutOptions::default();
        let available = LayoutSize::new(200.0, 200.0);
        let rects = solve_grid_layout(&columns, &rows, &children, options, available);
        assert_eq!(rects.len(), 2);
        assert!(rects[1].x > rects[0].x);
        assert!(rects[1].y > rects[0].y);
    }

    #[test]
    fn dock_layout_positions_sides() {
        let children = vec![
            DockLayoutChild::new(
                constraints((0.0, 40.0, 40.0), (0.0, 200.0, 200.0)),
                DockSide::Left,
            ),
            DockLayoutChild::new(
                constraints((0.0, 40.0, 40.0), (0.0, 200.0, 200.0)),
                DockSide::Right,
            ),
            DockLayoutChild::new(
                constraints((0.0, 100.0, 100.0), (0.0, 100.0, 100.0)),
                DockSide::Fill,
            ),
        ];

        let options = DockLayoutOptions::default();
        let available = LayoutSize::new(300.0, 200.0);
        let rects = solve_dock_layout(&children, options, available);
        assert_eq!(rects.len(), 3);
        assert!(rects[0].x < rects[2].x);
        assert!(rects[1].x > rects[2].x);
    }
}

#[derive(Clone, Copy, Debug)]
pub struct WrapLayoutChild {
    pub constraints: LayoutConstraints,
    pub margin: LayoutThickness,
    pub line_break: bool,
}

impl WrapLayoutChild {
    pub fn new(constraints: LayoutConstraints) -> Self {
        Self {
            constraints,
            margin: LayoutThickness::ZERO,
            line_break: false,
        }
    }
}

#[derive(Clone, Copy, Debug)]
pub struct WrapLayoutOptions {
    pub orientation: LayoutOrientation,
    pub item_spacing: f64,
    pub line_spacing: f64,
    pub padding: LayoutThickness,
    pub line_alignment: LayoutAlignment,
    pub cross_alignment: LayoutAlignment,
}

impl Default for WrapLayoutOptions {
    fn default() -> Self {
        Self {
            orientation: LayoutOrientation::Horizontal,
            item_spacing: 0.0,
            line_spacing: 0.0,
            padding: LayoutThickness::ZERO,
            line_alignment: LayoutAlignment::Start,
            cross_alignment: LayoutAlignment::Stretch,
        }
    }
}

pub struct WrapLayoutLine {
    pub line_index: u32,
    pub start: usize,
    pub count: usize,
    pub primary_offset: f64,
    pub primary_length: f64,
}

pub struct WrapLayoutResult {
    pub items: Vec<LayoutRect>,
    pub lines: Vec<WrapLayoutLine>,
}

pub fn solve_wrap_layout(
    children: &[WrapLayoutChild],
    options: WrapLayoutOptions,
    available: LayoutSize,
) -> WrapLayoutResult {
    if children.is_empty() {
        return WrapLayoutResult {
            items: Vec::new(),
            lines: Vec::new(),
        };
    }

    let padding = clamp_thickness(options.padding);
    let item_spacing = options.item_spacing.max(0.0);
    let line_spacing = options.line_spacing.max(0.0);
    let orientation = options.orientation;

    let (
        available_main,
        available_cross,
        padding_leading,
        padding_cross_leading,
        padding_cross_trailing,
    ) = match orientation {
        LayoutOrientation::Horizontal => (
            (available.width - padding.horizontal()).max(0.0),
            (available.height - padding.vertical()).max(0.0),
            padding.left,
            padding.top,
            padding.bottom,
        ),
        LayoutOrientation::Vertical => (
            (available.height - padding.vertical()).max(0.0),
            (available.width - padding.horizontal()).max(0.0),
            padding.top,
            padding.left,
            padding.right,
        ),
    };

    let mut positions = Vec::with_capacity(children.len());
    let mut lines = Vec::new();
    let mut line_start = 0usize;
    let mut line_index = 0u32;
    let mut cursor_primary = 0.0f64;
    let mut line_extent = 0.0f64;
    let mut line_cursor = 0.0f64;

    let mut place_line = |line_start: usize,
                          line_end: usize,
                          line_extent: f64,
                          cursor_primary: f64,
                          line_index: u32| {
        let primary_offset = padding_cross_leading + cursor_primary;
        lines.push(WrapLayoutLine {
            line_index,
            start: line_start,
            count: line_end - line_start,
            primary_offset,
            primary_length: line_extent,
        });
    };

    for (index, child) in children.iter().enumerate() {
        let (margin_leading, margin_trailing, margin_cross_leading, margin_cross_trailing) =
            match orientation {
                LayoutOrientation::Horizontal => (
                    child.margin.left.max(0.0),
                    child.margin.right.max(0.0),
                    child.margin.top.max(0.0),
                    child.margin.bottom.max(0.0),
                ),
                LayoutOrientation::Vertical => (
                    child.margin.top.max(0.0),
                    child.margin.bottom.max(0.0),
                    child.margin.left.max(0.0),
                    child.margin.right.max(0.0),
                ),
            };

        let main_constraint = match orientation {
            LayoutOrientation::Horizontal => child.constraints.width,
            LayoutOrientation::Vertical => child.constraints.height,
        };
        let cross_constraint = match orientation {
            LayoutOrientation::Horizontal => child.constraints.height,
            LayoutOrientation::Vertical => child.constraints.width,
        };

        let desired_main =
            clamp_length(main_constraint.normalised().preferred + margin_leading + margin_trailing);
        let desired_cross = clamp_length(
            cross_constraint.normalised().preferred + margin_cross_leading + margin_cross_trailing,
        );

        let available_line = if available_main.is_infinite() {
            desired_main
        } else {
            available_main
        };

        let start_new_line = if index == line_start {
            false
        } else if child.line_break {
            true
        } else {
            line_cursor + desired_main + margin_leading > available_line + EPSILON
        };

        if start_new_line {
            place_line(line_start, index, line_extent, cursor_primary, line_index);
            cursor_primary += line_extent + line_spacing;
            line_index += 1;
            line_start = index;
            line_cursor = 0.0;
            line_extent = 0.0;
        }

        let offset_main = padding_leading + line_cursor + margin_leading;
        let offset_cross = align_cross_value(
            available_cross,
            desired_cross - margin_cross_leading - margin_cross_trailing,
            margin_cross_leading,
            margin_cross_trailing,
            padding_cross_leading,
            padding_cross_trailing,
            options.cross_alignment,
            LayoutAlignment::Stretch,
        );

        let (x, y, width, height) = match orientation {
            LayoutOrientation::Horizontal => (
                offset_main,
                offset_cross,
                desired_main - margin_leading - margin_trailing,
                (desired_cross - margin_cross_leading - margin_cross_trailing).max(0.0),
            ),
            LayoutOrientation::Vertical => (
                offset_cross,
                offset_main,
                (desired_cross - margin_cross_leading - margin_cross_trailing).max(0.0),
                desired_main - margin_leading - margin_trailing,
            ),
        };

        let primary_offset = match orientation {
            LayoutOrientation::Horizontal => y,
            LayoutOrientation::Vertical => x,
        };
        let primary_length = match orientation {
            LayoutOrientation::Horizontal => height,
            LayoutOrientation::Vertical => width,
        };

        let mut rect = LayoutRect {
            x,
            y,
            width: width.max(0.0),
            height: height.max(0.0),
            primary_offset,
            primary_length,
            line_index,
        };
        clamp_rect(&mut rect);
        positions.push(rect);

        line_cursor += desired_main + item_spacing;
        line_extent = line_extent.max(desired_cross - margin_cross_leading - margin_cross_trailing);
    }

    if !positions.is_empty() {
        place_line(
            line_start,
            positions.len(),
            line_extent,
            cursor_primary,
            line_index,
        );
    }

    WrapLayoutResult {
        items: positions,
        lines,
    }
}

#[derive(Clone, Copy, Debug)]
pub enum GridTrackKind {
    Fixed(f64),
    Auto,
    Star(f64),
}

#[derive(Clone, Copy, Debug)]
pub struct GridTrack {
    pub kind: GridTrackKind,
    pub min: f64,
    pub max: f64,
}

impl GridTrack {
    pub fn fixed(value: f64) -> Self {
        Self {
            kind: GridTrackKind::Fixed(value.max(0.0)),
            min: 0.0,
            max: f64::INFINITY,
        }
    }

    pub fn auto() -> Self {
        Self {
            kind: GridTrackKind::Auto,
            min: 0.0,
            max: f64::INFINITY,
        }
    }

    pub fn star(weight: f64) -> Self {
        let weight = if weight.is_nan() || !weight.is_finite() || weight <= 0.0 {
            1.0
        } else {
            weight
        };
        Self {
            kind: GridTrackKind::Star(weight),
            min: 0.0,
            max: f64::INFINITY,
        }
    }
}

#[derive(Clone, Copy, Debug)]
pub struct GridLayoutOptions {
    pub padding: LayoutThickness,
    pub column_spacing: f64,
    pub row_spacing: f64,
}

impl Default for GridLayoutOptions {
    fn default() -> Self {
        Self {
            padding: LayoutThickness::ZERO,
            column_spacing: 0.0,
            row_spacing: 0.0,
        }
    }
}

#[derive(Clone, Copy, Debug)]
pub struct GridLayoutChild {
    pub constraints: LayoutConstraints,
    pub column: u16,
    pub column_span: u16,
    pub row: u16,
    pub row_span: u16,
    pub margin: LayoutThickness,
    pub horizontal_alignment: LayoutAlignment,
    pub vertical_alignment: LayoutAlignment,
}

impl GridLayoutChild {
    pub fn new(constraints: LayoutConstraints, column: u16, row: u16) -> Self {
        Self {
            constraints,
            column,
            column_span: 1,
            row,
            row_span: 1,
            margin: LayoutThickness::ZERO,
            horizontal_alignment: LayoutAlignment::Stretch,
            vertical_alignment: LayoutAlignment::Stretch,
        }
    }
}

pub fn solve_grid_layout(
    columns: &[GridTrack],
    rows: &[GridTrack],
    children: &[GridLayoutChild],
    options: GridLayoutOptions,
    available: LayoutSize,
) -> Vec<LayoutRect> {
    if columns.is_empty() || rows.is_empty() || children.is_empty() {
        return Vec::new();
    }

    let padding = clamp_thickness(options.padding);
    let column_spacing = options.column_spacing.max(0.0);
    let row_spacing = options.row_spacing.max(0.0);

    let column_offsets = solve_grid_tracks(
        columns,
        children,
        available.width,
        column_spacing,
        padding.left,
        true,
    );
    let row_offsets = solve_grid_tracks(
        rows,
        children,
        available.height,
        row_spacing,
        padding.top,
        false,
    );

    let mut rects = Vec::with_capacity(children.len());
    for child in children {
        let column = child.column.min(columns.len().saturating_sub(1) as u16);
        let span = child
            .column_span
            .max(1)
            .min((columns.len() as u16).saturating_sub(column).max(1));
        let row = child.row.min(rows.len().saturating_sub(1) as u16);
        let row_span = child
            .row_span
            .max(1)
            .min((rows.len() as u16).saturating_sub(row).max(1));

        let (x, width) = resolve_grid_slot(
            &column_offsets,
            column as usize,
            span as usize,
            column_spacing,
            child.margin.left,
            child.margin.right,
            child.constraints.width,
            child.horizontal_alignment,
        );

        let (y, height) = resolve_grid_slot(
            &row_offsets,
            row as usize,
            row_span as usize,
            row_spacing,
            child.margin.top,
            child.margin.bottom,
            child.constraints.height,
            child.vertical_alignment,
        );

        let mut rect = LayoutRect {
            x,
            y,
            width,
            height,
            primary_offset: y,
            primary_length: height,
            line_index: row as u32,
        };
        clamp_rect(&mut rect);
        rects.push(rect);
    }

    rects
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum DockSide {
    Left,
    Top,
    Right,
    Bottom,
    Fill,
}

#[derive(Clone, Copy, Debug)]
pub struct DockLayoutChild {
    pub constraints: LayoutConstraints,
    pub margin: LayoutThickness,
    pub side: DockSide,
    pub horizontal_alignment: LayoutAlignment,
    pub vertical_alignment: LayoutAlignment,
}

impl DockLayoutChild {
    pub fn new(constraints: LayoutConstraints, side: DockSide) -> Self {
        Self {
            constraints,
            margin: LayoutThickness::ZERO,
            side,
            horizontal_alignment: LayoutAlignment::Stretch,
            vertical_alignment: LayoutAlignment::Stretch,
        }
    }
}

#[derive(Clone, Copy, Debug)]
pub struct DockLayoutOptions {
    pub padding: LayoutThickness,
    pub spacing: f64,
    pub last_child_fill: bool,
}

impl Default for DockLayoutOptions {
    fn default() -> Self {
        Self {
            padding: LayoutThickness::ZERO,
            spacing: 0.0,
            last_child_fill: true,
        }
    }
}

pub fn solve_dock_layout(
    children: &[DockLayoutChild],
    options: DockLayoutOptions,
    available: LayoutSize,
) -> Vec<LayoutRect> {
    if children.is_empty() {
        return Vec::new();
    }

    let padding = clamp_thickness(options.padding);
    let spacing = options.spacing.max(0.0);

    let mut left = padding.left;
    let mut top = padding.top;
    let mut right = (available.width - padding.right).max(left);
    let mut bottom = (available.height - padding.bottom).max(top);

    let mut results = Vec::with_capacity(children.len());
    for (index, child) in children.iter().enumerate() {
        let is_last = options.last_child_fill && index == children.len() - 1;
        let margin = clamp_thickness(child.margin);

        let mut width = child.constraints.width.normalised().preferred;
        let mut height = child.constraints.height.normalised().preferred;
        width = width.clamp(child.constraints.width.min, child.constraints.width.max);
        height = height.clamp(child.constraints.height.min, child.constraints.height.max);

        let mut x = left + margin.left;
        let mut y = top + margin.top;
        let mut available_width = (right - left - margin.horizontal()).max(0.0);
        let mut available_height = (bottom - top - margin.vertical()).max(0.0);

        match child.side {
            DockSide::Left => {
                width = width.min(available_width);
                available_width = width;
                left += width + margin.horizontal() + spacing;
            }
            DockSide::Right => {
                width = width.min(available_width);
                available_width = width;
                x = right - width - margin.right;
                right -= width + margin.horizontal() + spacing;
            }
            DockSide::Top => {
                height = height.min(available_height);
                available_height = height;
                top += height + margin.vertical() + spacing;
            }
            DockSide::Bottom => {
                height = height.min(available_height);
                available_height = height;
                y = bottom - height - margin.bottom;
                bottom -= height + margin.vertical() + spacing;
            }
            DockSide::Fill => {
                width = available_width;
                height = available_height;
            }
        }

        if is_last && child.side != DockSide::Fill {
            width = (right - left - margin.horizontal()).max(0.0);
            height = (bottom - top - margin.vertical()).max(0.0);
        }

        width = align_length(
            available_width,
            width,
            margin.left,
            margin.right,
            child.horizontal_alignment,
        );
        height = align_length(
            available_height,
            height,
            margin.top,
            margin.bottom,
            child.vertical_alignment,
        );

        x = align_position(
            x,
            available_width,
            width,
            margin.left,
            margin.right,
            child.horizontal_alignment,
        );
        y = align_position(
            y,
            available_height,
            height,
            margin.top,
            margin.bottom,
            child.vertical_alignment,
        );

        let mut rect = LayoutRect {
            x,
            y,
            width: width.max(0.0),
            height: height.max(0.0),
            primary_offset: y,
            primary_length: height.max(0.0),
            line_index: index as u32,
        };
        clamp_rect(&mut rect);
        results.push(rect);
    }

    results
}

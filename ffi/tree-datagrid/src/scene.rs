use vello::{
    Scene,
    kurbo::{Affine, Line, Point, Rect, Stroke},
    peniko::{Brush, Color, Fill},
};

use crate::color::VelloTdgColor;
use crate::types::{ColumnStrip, FrozenColumns, FrozenKind};

#[derive(Clone, Copy, Debug)]
pub struct RowVisual {
    pub width: f64,
    pub height: f64,
    pub depth: u32,
    pub indent: f64,
    pub background: VelloTdgColor,
    pub hover_background: VelloTdgColor,
    pub selection_fill: VelloTdgColor,
    pub outline: VelloTdgColor,
    pub outline_width: f32,
    pub stripe: VelloTdgColor,
    pub stripe_width: f32,
    pub is_selected: bool,
    pub is_hovered: bool,
}

impl Default for RowVisual {
    fn default() -> Self {
        Self {
            width: 0.0,
            height: 0.0,
            depth: 0,
            indent: 18.0,
            background: VelloTdgColor {
                r: 0.12,
                g: 0.12,
                b: 0.12,
                a: 1.0,
            },
            hover_background: VelloTdgColor {
                r: 0.18,
                g: 0.18,
                b: 0.18,
                a: 0.6,
            },
            selection_fill: VelloTdgColor {
                r: 0.19,
                g: 0.43,
                b: 0.78,
                a: 0.35,
            },
            outline: VelloTdgColor {
                r: 0.28,
                g: 0.54,
                b: 0.92,
                a: 1.0,
            },
            outline_width: 1.0,
            stripe: VelloTdgColor {
                r: 1.0,
                g: 1.0,
                b: 1.0,
                a: 0.05,
            },
            stripe_width: 1.0,
            is_selected: false,
            is_hovered: false,
        }
    }
}

#[derive(Clone, Copy, Debug)]
pub struct GroupHeaderVisual {
    pub width: f64,
    pub height: f64,
    pub depth: u32,
    pub indent: f64,
    pub background: VelloTdgColor,
    pub accent: VelloTdgColor,
    pub outline: VelloTdgColor,
    pub outline_width: f32,
}

impl Default for GroupHeaderVisual {
    fn default() -> Self {
        Self {
            width: 0.0,
            height: 0.0,
            depth: 0,
            indent: 20.0,
            background: VelloTdgColor {
                r: 0.16,
                g: 0.16,
                b: 0.16,
                a: 1.0,
            },
            accent: VelloTdgColor {
                r: 0.35,
                g: 0.74,
                b: 0.98,
                a: 0.75,
            },
            outline: VelloTdgColor {
                r: 0.3,
                g: 0.3,
                b: 0.3,
                a: 1.0,
            },
            outline_width: 1.0,
        }
    }
}

#[derive(Clone, Copy, Debug)]
pub struct SummaryVisual {
    pub width: f64,
    pub height: f64,
    pub highlight: VelloTdgColor,
    pub background: VelloTdgColor,
    pub outline: VelloTdgColor,
    pub outline_width: f32,
}

impl Default for SummaryVisual {
    fn default() -> Self {
        Self {
            width: 0.0,
            height: 0.0,
            highlight: VelloTdgColor {
                r: 0.45,
                g: 0.76,
                b: 0.44,
                a: 0.65,
            },
            background: VelloTdgColor {
                r: 0.1,
                g: 0.1,
                b: 0.1,
                a: 0.9,
            },
            outline: VelloTdgColor {
                r: 0.36,
                g: 0.69,
                b: 0.32,
                a: 1.0,
            },
            outline_width: 1.0,
        }
    }
}

#[derive(Clone, Copy, Debug)]
pub struct RowChromeVisual {
    pub width: f64,
    pub height: f64,
    pub grid_color: VelloTdgColor,
    pub grid_width: f32,
    pub frozen: FrozenColumns,
    pub frozen_fill: VelloTdgColor,
}

impl Default for RowChromeVisual {
    fn default() -> Self {
        Self {
            width: 0.0,
            height: 0.0,
            grid_color: VelloTdgColor {
                r: 0.25,
                g: 0.25,
                b: 0.25,
                a: 0.6,
            },
            grid_width: 1.0,
            frozen: FrozenColumns::default(),
            frozen_fill: VelloTdgColor {
                r: 0.12,
                g: 0.12,
                b: 0.12,
                a: 0.85,
            },
        }
    }
}

pub fn encode_row(scene: &mut Scene, visual: &RowVisual, columns: &[ColumnStrip]) {
    scene.reset();
    let width = visual.width.max(0.0);
    let height = visual.height.max(0.0);

    fill_rect(
        scene,
        Rect::new(0.0, 0.0, width, height),
        visual.background.to_color(),
    );

    let indent_width = visual.indent.max(0.0) * visual.depth as f64;
    if indent_width > 0.0 {
        fill_rect(
            scene,
            Rect::new(0.0, 0.0, indent_width, height),
            visual.background.lighten(0.2).to_color(),
        );
    }

    if visual.is_hovered {
        fill_rect(
            scene,
            Rect::new(0.0, 0.0, width, height),
            visual.hover_background.to_color(),
        );
    }

    if visual.is_selected {
        fill_rect(
            scene,
            Rect::new(0.0, 0.0, width, height),
            visual.selection_fill.to_color(),
        );
    }

    if visual.outline_width > 0.0 {
        stroke_rect(
            scene,
            Rect::new(0.5, 0.5, width - 0.5, height - 0.5),
            visual.outline_width as f64,
            visual.outline.to_color(),
        );
    }

    draw_column_separators(
        scene,
        columns,
        height,
        f64::from(visual.stripe_width.max(0.0)),
        visual.stripe.to_color(),
    );
}

pub fn encode_group_header(scene: &mut Scene, visual: &GroupHeaderVisual, columns: &[ColumnStrip]) {
    scene.reset();
    let width = visual.width.max(0.0);
    let height = visual.height.max(0.0);

    fill_rect(
        scene,
        Rect::new(0.0, 0.0, width, height),
        visual.background.to_color(),
    );

    let indent_width = visual.indent.max(0.0) * visual.depth as f64;
    if indent_width > 0.0 {
        fill_rect(
            scene,
            Rect::new(0.0, 0.0, indent_width, height),
            visual.accent.to_color(),
        );
    }

    stroke_line(
        scene,
        Point::new(0.0, height - 0.5),
        Point::new(width, height - 0.5),
        visual.outline_width.max(0.5) as f64,
        visual.outline.to_color(),
    );

    draw_column_separators(
        scene,
        columns,
        height,
        visual.outline_width.max(0.5) as f64,
        visual.outline.to_color(),
    );
}

pub fn encode_summary(scene: &mut Scene, visual: &SummaryVisual, columns: &[ColumnStrip]) {
    scene.reset();
    let width = visual.width.max(0.0);
    let height = visual.height.max(0.0);

    fill_rect(
        scene,
        Rect::new(0.0, 0.0, width, height),
        visual.background.to_color(),
    );

    fill_rect(
        scene,
        Rect::new(0.0, 0.0, width, 3.0_f64.min(height)),
        visual.highlight.to_color(),
    );

    if visual.outline_width > 0.0 {
        stroke_rect(
            scene,
            Rect::new(0.5, 0.5, width - 0.5, height - 0.5),
            visual.outline_width.max(0.5) as f64,
            visual.outline.to_color(),
        );
    }

    draw_column_separators(
        scene,
        columns,
        height,
        visual.outline_width.max(0.5) as f64,
        visual.outline.to_color(),
    );
}

pub fn encode_chrome(scene: &mut Scene, visual: &RowChromeVisual, columns: &[ColumnStrip]) {
    scene.reset();
    let width = visual.width.max(0.0);
    let height = visual.height.max(0.0);

    if width <= 0.0 || height <= 0.0 {
        return;
    }

    if visual.frozen.leading > 0 || visual.frozen.trailing > 0 {
        fill_frozen_columns(scene, columns, height, visual);
    }

    draw_column_separators(
        scene,
        columns,
        height,
        visual.grid_width.max(0.5) as f64,
        visual.grid_color.to_color(),
    );

    stroke_line(
        scene,
        Point::new(0.0, height - 0.5),
        Point::new(width, height - 0.5),
        visual.grid_width.max(0.5) as f64,
        visual.grid_color.to_color(),
    );
}

fn fill_rect(scene: &mut Scene, rect: Rect, color: Color) {
    scene.fill(
        Fill::NonZero,
        Affine::IDENTITY,
        &Brush::Solid(color),
        None,
        &rect,
    );
}

fn stroke_rect(scene: &mut Scene, rect: Rect, width: f64, color: Color) {
    let stroke = Stroke::new(width);
    scene.stroke(&stroke, Affine::IDENTITY, &Brush::Solid(color), None, &rect);
}

fn stroke_line(scene: &mut Scene, from: Point, to: Point, width: f64, color: Color) {
    let stroke = Stroke::new(width);
    let line = Line::new(from, to);
    scene.stroke(&stroke, Affine::IDENTITY, &Brush::Solid(color), None, &line);
}

fn draw_column_separators(
    scene: &mut Scene,
    columns: &[ColumnStrip],
    height: f64,
    width: f64,
    color: Color,
) {
    if width <= 0.0 || columns.is_empty() {
        return;
    }

    for column in columns {
        let x = column.offset + column.width;
        let line = Line::new(Point::new(x, 0.0), Point::new(x, height));
        scene.stroke(
            &Stroke::new(width),
            Affine::IDENTITY,
            &Brush::Solid(color),
            None,
            &line,
        );
    }
}

fn fill_frozen_columns(
    scene: &mut Scene,
    columns: &[ColumnStrip],
    height: f64,
    visual: &RowChromeVisual,
) {
    if columns.is_empty() {
        return;
    }

    let fill_color = visual.frozen_fill.to_color();
    let mut leading_extent: f64 = 0.0;
    let mut trailing_start: f64 = visual.width;
    let mut trailing_end: f64 = 0.0;

    for column in columns {
        match column.frozen {
            FrozenKind::Leading => {
                leading_extent = leading_extent.max(column.offset + column.width);
            }
            FrozenKind::Trailing => {
                trailing_start = trailing_start.min(column.offset);
                trailing_end = trailing_end.max(column.offset + column.width);
            }
            FrozenKind::None => {}
        }
    }

    if leading_extent > 0.0 {
        fill_rect(
            scene,
            Rect::new(0.0, 0.0, leading_extent, height),
            fill_color,
        );
    }

    if trailing_end > trailing_start && trailing_start < visual.width {
        fill_rect(
            scene,
            Rect::new(trailing_start, 0.0, trailing_end, height),
            fill_color,
        );
    }
}

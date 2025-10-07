use std::sync::{Arc, Mutex};

use hashbrown::HashMap;
use once_cell::sync::Lazy;
use skrifa::raw::{FileRef, FontRef};
use skrifa::{
    MetadataProvider,
    instance::{Location, Size},
};
use vello::Glyph;
use vello::peniko::{Blob, FontData};

const LABEL_FONT_BYTES: &[u8] =
    include_bytes!("../../../extern/vello/examples/assets/roboto/Roboto-Regular.ttf");

static LABEL_FONT: Lazy<FontData> =
    Lazy::new(|| FontData::new(Blob::new(Arc::new(LABEL_FONT_BYTES)), 0));

static DEFAULT_TEXT_SHAPER: Lazy<Mutex<TextShaper>> =
    Lazy::new(|| Mutex::new(TextShaper::new(label_font())));

#[derive(Debug, Clone)]
pub struct LabelLayout {
    pub glyphs: Vec<Glyph>,
    pub width: f32,
    pub height: f32,
    pub ascent: f32,
}

#[derive(Hash, Eq, PartialEq)]
struct TextCacheKey {
    font_size_bits: u32,
    text: String,
}

impl TextCacheKey {
    fn new(text: &str, font_size: f32) -> Self {
        Self {
            font_size_bits: font_size.to_bits(),
            text: text.to_owned(),
        }
    }
}

pub struct TextShaper {
    font: &'static FontData,
    cache: HashMap<TextCacheKey, LabelLayout>,
}

impl TextShaper {
    pub fn new(font: &'static FontData) -> Self {
        Self {
            font,
            cache: HashMap::with_capacity(64),
        }
    }

    pub fn shape(&mut self, text: &str, font_size: f32) -> Option<LabelLayout> {
        let trimmed = text.trim();
        if trimmed.is_empty() {
            return None;
        }

        let key = TextCacheKey::new(trimmed, font_size);
        if let Some(layout) = self.cache.get(&key) {
            return Some(layout.clone());
        }

        let layout = layout_text(self.font, trimmed, font_size)?;
        self.cache.insert(key, layout.clone());
        Some(layout)
    }

    pub fn clear(&mut self) {
        self.cache.clear();
    }
}

pub fn label_font() -> &'static FontData {
    &LABEL_FONT
}

pub fn layout_label(text: &str, font_size: f32) -> Option<LabelLayout> {
    let mut shaper = DEFAULT_TEXT_SHAPER
        .lock()
        .expect("text shaper mutex poisoned");
    shaper.shape(text, font_size)
}

fn layout_text(font: &'static FontData, text: &str, font_size: f32) -> Option<LabelLayout> {
    let font_ref = to_font_ref(font)?;
    let size = Size::new(font_size);
    let axes = font_ref.axes();
    let location = Location::new(axes.len());
    let metrics = font_ref.metrics(size, &location);
    let glyph_metrics = font_ref.glyph_metrics(size, &location);
    let charmap = font_ref.charmap();

    let mut pen_x = 0f32;
    let mut glyphs = Vec::with_capacity(text.len());
    for ch in text.chars() {
        if ch.is_control() && ch != ' ' {
            continue;
        }
        let glyph_id = charmap.map(ch).unwrap_or_default();
        let advance = glyph_metrics.advance_width(glyph_id).unwrap_or_default();
        glyphs.push(Glyph {
            id: glyph_id.to_u32(),
            x: pen_x,
            y: 0.0,
        });
        pen_x += advance;
    }

    if glyphs.is_empty() {
        return None;
    }

    let ascent = metrics.ascent;
    let height = ascent - metrics.descent;

    Some(LabelLayout {
        glyphs,
        width: pen_x,
        height,
        ascent,
    })
}

fn to_font_ref(font: &FontData) -> Option<FontRef<'_>> {
    let file_ref = FileRef::new(font.data.as_ref()).ok()?;
    match file_ref {
        FileRef::Font(font) => Some(font),
        FileRef::Collection(collection) => collection.get(font.index).ok(),
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn layout_label_returns_metrics_for_ascii_text() {
        let layout = layout_label("Price Δ", 14.0).expect("layout");
        assert!((layout.width - 45.04883).abs() < 1e-5);
        assert!((layout.height - 16.40625).abs() < 1e-5);
        assert!((layout.ascent - 12.988281).abs() < 1e-5);
        assert_eq!(layout.glyphs.len(), 7);
    }

    #[test]
    fn text_shaper_caches_repeated_requests() {
        let mut shaper = TextShaper::new(label_font());
        let first = shaper.shape("Volume", 12.0).expect("first layout");
        let second = shaper.shape("Volume", 12.0).expect("cached layout");
        assert_eq!(first.width, second.width);
        assert_eq!(first.height, second.height);
        assert_eq!(first.ascent, second.ascent);
    }
}

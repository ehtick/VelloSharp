#![cfg_attr(not(test), deny(clippy::all))]
#![allow(clippy::missing_safety_doc)]
#![allow(missing_docs)]
#![allow(missing_debug_implementations)]

use std::io::Cursor;
use std::ptr;
use std::slice;

use image::codecs::avif::AvifEncoder;
use image::codecs::gif::{GifEncoder, Repeat};
use image::codecs::jpeg::JpegEncoder;
use image::codecs::png::PngEncoder;
use image::codecs::webp::WebPEncoder;
use image::error::{EncodingError, ImageFormatHint};
use image::{DynamicImage, ImageEncoder, ImageError, ImageFormat, ImageReader};
use vello_ffi::{
    VelloImageAlphaMode, VelloImageHandle, VelloImageInfo, VelloRenderFormat, VelloStatus,
    shim_clear_last_error, shim_set_last_error, vello_image_create, vello_image_get_info,
    vello_image_map_pixels, vello_image_unmap_pixels,
};

#[repr(C)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum ImageCodecFormat {
    Auto = 0,
    Png = 1,
    Jpeg = 2,
    Webp = 3,
    Avif = 4,
    Gif = 5,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct ImageCodecBuffer {
    pub data: *const u8,
    pub length: usize,
}

pub struct ImageCodecBufferHandle {
    buffer: Vec<u8>,
}

#[inline]
fn success(out_status: VelloStatus) -> VelloStatus {
    if matches!(out_status, VelloStatus::Success) {
        shim_clear_last_error();
    }
    out_status
}

fn report_error<E: std::fmt::Display>(err: E, status: VelloStatus) -> VelloStatus {
    shim_set_last_error(err.to_string());
    status
}

fn decode_image_bytes(
    bytes: &[u8],
    format: Option<ImageCodecFormat>,
) -> Result<DynamicImage, ImageError> {
    match format {
        Some(ImageCodecFormat::Png) => image::load_from_memory_with_format(bytes, ImageFormat::Png),
        Some(ImageCodecFormat::Jpeg) => {
            image::load_from_memory_with_format(bytes, ImageFormat::Jpeg)
        }
        Some(ImageCodecFormat::Webp) => {
            image::load_from_memory_with_format(bytes, ImageFormat::WebP)
        }
        Some(ImageCodecFormat::Avif) => {
            image::load_from_memory_with_format(bytes, ImageFormat::Avif)
        }
        Some(ImageCodecFormat::Gif) => image::load_from_memory_with_format(bytes, ImageFormat::Gif),
        Some(ImageCodecFormat::Auto) | None => {
            let reader = ImageReader::new(Cursor::new(bytes));
            let reader = reader.with_guessed_format()?;
            reader.decode()
        }
    }
}

fn map_format_to_image(format: ImageCodecFormat) -> Option<ImageCodecFormat> {
    match format {
        ImageCodecFormat::Auto => None,
        _ => Some(format),
    }
}

fn create_image_handle_from_rgba(
    image: &DynamicImage,
) -> Result<*mut VelloImageHandle, String> {
    let rgba = image.to_rgba8();
    let width = rgba.width();
    let height = rgba.height();
    let stride = (width as usize) * 4;
    let handle = unsafe {
        vello_image_create(
            VelloRenderFormat::Rgba8,
            VelloImageAlphaMode::Straight,
            width,
            height,
            rgba.as_ptr(),
            stride,
        )
    };
    if handle.is_null() {
        Err("Failed to create Vello image handle.".into())
    } else {
        Ok(handle)
    }
}

fn clamp_quality(quality: u8) -> u8 {
    quality.clamp(1, 100)
}

fn convert_to_contiguous_rgba(
    info: &VelloImageInfo,
    pixels: &[u8],
) -> Result<Vec<u8>, &'static str> {
    let width = info.width as usize;
    let height = info.height as usize;
    let stride = info.stride;
    let row_bytes = width
        .checked_mul(4)
        .ok_or("Image dimensions overflow while computing row size.")?;
    if stride < row_bytes {
        return Err("Image stride is smaller than expected row size.");
    }

    let expected = stride
        .checked_mul(height)
        .ok_or("Image dimensions overflow while computing stride.")?;
    if pixels.len() < expected {
        return Err("Image pixel buffer is smaller than expected.");
    }

    let mut rgba = Vec::with_capacity(row_bytes * height);
    #[allow(unreachable_patterns)]
    match info.format {
        VelloRenderFormat::Rgba8 => {
            for y in 0..height {
                let start = y * stride;
                let row = &pixels[start..start + row_bytes];
                rgba.extend_from_slice(row);
            }
            Ok(rgba)
        }
        VelloRenderFormat::Bgra8 => {
            for y in 0..height {
                let start = y * stride;
                let row = &pixels[start..start + row_bytes];
                for chunk in row.chunks_exact(4) {
                    rgba.extend_from_slice(&[chunk[2], chunk[1], chunk[0], chunk[3]]);
                }
            }
            Ok(rgba)
        }
        _ => Err("Unsupported pixel format for rgba export."),
    }
}

fn encode_jpeg(rgba: &[u8], width: u32, height: u32, quality: u8) -> Result<Vec<u8>, ImageError> {
    let mut rgb = Vec::with_capacity((width as usize) * (height as usize) * 3);
    for pixel in rgba.chunks_exact(4) {
        rgb.extend_from_slice(&pixel[..3]);
    }

    let mut buffer = Vec::new();
    {
        let mut encoder = JpegEncoder::new_with_quality(&mut buffer, clamp_quality(quality));
        encoder.encode(&rgb, width, height, image::ExtendedColorType::Rgb8)?;
    }
    Ok(buffer)
}

fn encode_webp(rgba: &[u8], width: u32, height: u32) -> Result<Vec<u8>, ImageError> {
    let mut buffer = Vec::new();
    {
        let encoder = WebPEncoder::new_lossless(&mut buffer);
        encoder.encode(rgba, width, height, image::ExtendedColorType::Rgba8)?;
    }
    Ok(buffer)
}

fn encode_avif(rgba: &[u8], width: u32, height: u32, quality: u8) -> Result<Vec<u8>, ImageError> {
    let mut buffer = Vec::new();
    {
        let encoder =
            AvifEncoder::new_with_speed_quality(&mut buffer, 4, clamp_quality(quality));
        encoder.write_image(rgba, width, height, image::ExtendedColorType::Rgba8)?;
    }
    Ok(buffer)
}

fn encode_gif(rgba: Vec<u8>, width: u32, height: u32) -> Result<Vec<u8>, ImageError> {
    let mut buffer = Vec::new();
    {
        let mut encoder = GifEncoder::new(&mut buffer);
        encoder.set_repeat(Repeat::Infinite)?;
        encoder.encode(&rgba, width, height, image::ExtendedColorType::Rgba8)?;
    }
    Ok(buffer)
}

fn encode_image_to_format(
    rgba: Vec<u8>,
    width: u32,
    height: u32,
    format: ImageCodecFormat,
    quality: u8,
) -> Result<Vec<u8>, ImageError> {
    match format {
        ImageCodecFormat::Png => {
            let mut buffer = Vec::new();
            {
                let mut encoder = PngEncoder::new(&mut buffer);
                encoder.write_image(&rgba, width, height, image::ExtendedColorType::Rgba8)?;
            }
            Ok(buffer)
        }
        ImageCodecFormat::Jpeg => encode_jpeg(&rgba, width, height, quality),
        ImageCodecFormat::Webp => encode_webp(&rgba, width, height),
        ImageCodecFormat::Avif => encode_avif(&rgba, width, height, quality),
        ImageCodecFormat::Gif => encode_gif(rgba, width, height),
        ImageCodecFormat::Auto => unreachable!("Auto format should be rejected prior to encoding"),
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn image_codec_decode(
    data: *const u8,
    length: usize,
    format: ImageCodecFormat,
    out_image: *mut *mut VelloImageHandle,
) -> VelloStatus {
    if data.is_null() || length == 0 || out_image.is_null() {
        return VelloStatus::NullPointer;
    }

    let bytes = unsafe { slice::from_raw_parts(data, length) };
    match decode_image_bytes(bytes, map_format_to_image(format)) {
        Ok(image) => match create_image_handle_from_rgba(&image) {
            Ok(handle) => {
                unsafe { *out_image = handle };
                success(VelloStatus::Success)
            }
            Err(err) => report_error(err, VelloStatus::RenderError),
        },
        Err(err) => report_error(err, VelloStatus::RenderError),
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn image_codec_decode_auto(
    data: *const u8,
    length: usize,
    out_image: *mut *mut VelloImageHandle,
) -> VelloStatus {
    unsafe { image_codec_decode(data, length, ImageCodecFormat::Auto, out_image) }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn image_codec_encode(
    image: *const VelloImageHandle,
    format: ImageCodecFormat,
    quality: u8,
    out_buffer: *mut *mut ImageCodecBufferHandle,
) -> VelloStatus {
    if image.is_null() || out_buffer.is_null() {
        return VelloStatus::NullPointer;
    }

    if matches!(format, ImageCodecFormat::Auto) {
        return report_error(
            "Image format must be specified for encoding.",
            VelloStatus::InvalidArgument,
        );
    }

    let mut info = VelloImageInfo {
        width: 0,
        height: 0,
        format: VelloRenderFormat::Rgba8,
        alpha: VelloImageAlphaMode::Straight,
        stride: 0,
    };

    let status = unsafe { vello_image_get_info(image, &mut info) };
    if status != VelloStatus::Success {
        return status;
    }

    let mut pixels_ptr: *const u8 = ptr::null();
    let mut length: usize = 0;
    let status = unsafe { vello_image_map_pixels(image, &mut pixels_ptr, &mut length) };
    if status != VelloStatus::Success {
        return status;
    }

    let result = (|| {
        let pixels = unsafe { slice::from_raw_parts(pixels_ptr, length) };
        let rgba = convert_to_contiguous_rgba(&info, pixels).map_err(|msg| {
            ImageError::Encoding(EncodingError::new(ImageFormatHint::Unknown, msg))
        })?;

        encode_image_to_format(rgba, info.width, info.height, format, quality)
    })();

    unsafe { vello_image_unmap_pixels(image) };

    match result {
        Ok(data) => {
            let handle = Box::into_raw(Box::new(ImageCodecBufferHandle { buffer: data }));
            unsafe { *out_buffer = handle };
            success(VelloStatus::Success)
        }
        Err(err) => report_error(err, VelloStatus::RenderError),
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn image_codec_buffer_get_data(
    handle: *const ImageCodecBufferHandle,
    out_buffer: *mut ImageCodecBuffer,
) -> VelloStatus {
    if handle.is_null() || out_buffer.is_null() {
        return VelloStatus::NullPointer;
    }

    let handle_ref = unsafe { &*handle };
    let descriptor = ImageCodecBuffer {
        data: handle_ref.buffer.as_ptr(),
        length: handle_ref.buffer.len(),
    };
    unsafe {
        *out_buffer = descriptor;
    }
    success(VelloStatus::Success)
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn image_codec_buffer_destroy(handle: *mut ImageCodecBufferHandle) {
    if !handle.is_null() {
        unsafe {
            drop(Box::from_raw(handle));
        }
    }
}

#![cfg_attr(not(test), deny(clippy::all))]
#![allow(clippy::missing_safety_doc)]
#![allow(missing_docs)]
#![allow(missing_debug_implementations)]

use std::{
    cell::RefCell,
    collections::HashMap,
    ffi::{CStr, CString, c_char, c_void},
    num::NonZeroIsize,
    panic::{AssertUnwindSafe, catch_unwind},
    ptr::{self, NonNull},
    rc::Rc,
    sync::{Mutex, OnceLock},
    time::Duration,
};

const KEY_CODE_NAME_CAPACITY: usize = 64;

#[cfg(any(target_os = "macos", target_os = "windows", target_os = "linux"))]
use copypasta::{ClipboardContext, ClipboardProvider};

use raw_window_handle::{HasDisplayHandle, HasWindowHandle, RawDisplayHandle, RawWindowHandle};
use winit::{
    application::ApplicationHandler,
    dpi::PhysicalSize,
    event::{
        DeviceEvent, ElementState, Ime, KeyEvent, MouseButton, MouseScrollDelta, StartCause,
        TouchPhase, WindowEvent,
    },
    event_loop::{ActiveEventLoop, ControlFlow, EventLoop, EventLoopProxy},
    keyboard::{Key, KeyCode, KeyLocation, ModifiersState, NamedKey, PhysicalKey},
    window::{Window, WindowAttributes, WindowId},
};

#[derive(Clone, Copy, Debug)]
enum UserEvent {
    Wake,
}

static EVENT_LOOP_PROXY: OnceLock<Mutex<Option<EventLoopProxy<UserEvent>>>> = OnceLock::new();

thread_local! {
    static LAST_ERROR: RefCell<Option<CString>> = const { RefCell::new(None) };
}

fn clear_last_error() {
    LAST_ERROR.with(|slot| slot.borrow_mut().take());
}

fn set_last_error(msg: impl Into<String>) {
    let msg = msg.into();
    let cstr = CString::new(msg).unwrap_or_else(|_| CString::new("invalid error message").unwrap());
    LAST_ERROR.with(|slot| *slot.borrow_mut() = Some(cstr));
}

fn store_event_loop_proxy(proxy: EventLoopProxy<UserEvent>) {
    let mutex = EVENT_LOOP_PROXY.get_or_init(|| Mutex::new(None));
    *mutex.lock().unwrap() = Some(proxy);
}

fn clear_event_loop_proxy() {
    if let Some(mutex) = EVENT_LOOP_PROXY.get() {
        *mutex.lock().unwrap() = None;
    }
}

fn with_event_loop_proxy<F>(mut f: F) -> Result<(), WinitStatus>
where
    F: FnMut(&EventLoopProxy<UserEvent>) -> Result<(), WinitStatus>,
{
    let Some(mutex) = EVENT_LOOP_PROXY.get() else {
        set_last_error("Event loop proxy is not initialized");
        return Err(WinitStatus::InvalidState);
    };

    let guard = mutex.lock().unwrap();
    let Some(proxy) = guard.as_ref() else {
        set_last_error("Event loop proxy is not available");
        return Err(WinitStatus::InvalidState);
    };

    f(proxy)
}

struct ProxyRegistration;

impl ProxyRegistration {
    fn register(proxy: EventLoopProxy<UserEvent>) -> Self {
        store_event_loop_proxy(proxy);
        Self
    }
}

impl Drop for ProxyRegistration {
    fn drop(&mut self) {
        clear_event_loop_proxy();
    }
}

#[cfg(any(target_os = "macos", target_os = "windows", target_os = "linux"))]
struct ClipboardState {
    context: Option<ClipboardContext>,
}

#[cfg(any(target_os = "macos", target_os = "windows", target_os = "linux"))]
impl ClipboardState {
    fn ensure_context(&mut self) -> Result<&mut ClipboardContext, String> {
        if self.context.is_none() {
            let context = ClipboardContext::new().map_err(|err| err.to_string())?;
            self.context = Some(context);
        }

        // SAFETY: context is guaranteed to be `Some` above.
        Ok(self
            .context
            .as_mut()
            .expect("clipboard context should be initialized"))
    }
}

#[cfg(any(target_os = "macos", target_os = "windows", target_os = "linux"))]
fn clipboard_state() -> &'static Mutex<ClipboardState> {
    static CLIPBOARD: OnceLock<Mutex<ClipboardState>> = OnceLock::new();
    CLIPBOARD.get_or_init(|| Mutex::new(ClipboardState { context: None }))
}

#[cfg(any(target_os = "macos", target_os = "windows", target_os = "linux"))]
fn with_clipboard<T, F>(f: F) -> Result<T, WinitStatus>
where
    F: FnOnce(&mut ClipboardContext) -> Result<T, String>,
{
    let mutex = clipboard_state();
    let mut guard = match mutex.lock() {
        Ok(guard) => guard,
        Err(_) => {
            set_last_error("Clipboard mutex poisoned");
            return Err(WinitStatus::RuntimeError);
        }
    };

    let context = match guard.ensure_context() {
        Ok(ctx) => ctx,
        Err(err) => {
            set_last_error(format!("Clipboard unavailable: {err}"));
            return Err(WinitStatus::RuntimeError);
        }
    };

    match f(context) {
        Ok(value) => Ok(value),
        Err(err) => {
            set_last_error(format!("Clipboard error: {err}"));
            Err(WinitStatus::RuntimeError)
        }
    }
}

#[cfg(any(target_os = "macos", target_os = "windows", target_os = "linux"))]
#[unsafe(no_mangle)]
pub extern "C" fn winit_clipboard_is_available() -> bool {
    let mutex = clipboard_state();
    match mutex.lock() {
        Ok(mut guard) => guard.ensure_context().is_ok(),
        Err(_) => false,
    }
}

#[cfg(not(any(target_os = "macos", target_os = "windows", target_os = "linux")))]
#[unsafe(no_mangle)]
pub extern "C" fn winit_clipboard_is_available() -> bool {
    false
}

#[cfg(any(target_os = "macos", target_os = "windows", target_os = "linux"))]
#[unsafe(no_mangle)]
pub unsafe extern "C" fn winit_clipboard_set_text(text: *const c_char) -> WinitStatus {
    clear_last_error();

    let contents = if text.is_null() {
        String::new()
    } else {
        unsafe { CStr::from_ptr(text) }
            .to_string_lossy()
            .into_owned()
    };

    match with_clipboard(|ctx| {
        ctx.set_contents(contents.clone())
            .map_err(|err| err.to_string())
    }) {
        Ok(_) => WinitStatus::Success,
        Err(status) => status,
    }
}

#[cfg(not(any(target_os = "macos", target_os = "windows", target_os = "linux")))]
#[unsafe(no_mangle)]
pub unsafe extern "C" fn winit_clipboard_set_text(text: *const c_char) -> WinitStatus {
    let _ = text;
    set_last_error("Clipboard is not supported on this platform");
    WinitStatus::InvalidState
}

#[cfg(any(target_os = "macos", target_os = "windows", target_os = "linux"))]
#[unsafe(no_mangle)]
pub unsafe extern "C" fn winit_clipboard_get_text(out_text: *mut *mut c_char) -> WinitStatus {
    clear_last_error();

    if out_text.is_null() {
        set_last_error("out_text must not be null");
        return WinitStatus::NullPointer;
    }

    let result = with_clipboard(|ctx| ctx.get_contents().map_err(|err| err.to_string()));
    match result {
        Ok(text) => {
            let ptr = if text.is_empty() {
                ptr::null_mut()
            } else {
                match CString::new(text) {
                    Ok(cstr) => cstr.into_raw(),
                    Err(_) => {
                        set_last_error("Clipboard contents contain interior null bytes");
                        return WinitStatus::RuntimeError;
                    }
                }
            };

            unsafe {
                *out_text = ptr;
            }
            WinitStatus::Success
        }
        Err(status) => {
            unsafe {
                *out_text = ptr::null_mut();
            }
            status
        }
    }
}

#[cfg(not(any(target_os = "macos", target_os = "windows", target_os = "linux")))]
#[unsafe(no_mangle)]
pub unsafe extern "C" fn winit_clipboard_get_text(out_text: *mut *mut c_char) -> WinitStatus {
    if !out_text.is_null() {
        unsafe {
            *out_text = ptr::null_mut();
        }
    }
    set_last_error("Clipboard is not supported on this platform");
    WinitStatus::InvalidState
}

#[cfg(any(target_os = "macos", target_os = "windows", target_os = "linux"))]
#[unsafe(no_mangle)]
pub unsafe extern "C" fn winit_clipboard_free_text(text: *mut c_char) {
    if text.is_null() {
        return;
    }

    unsafe {
        let _ = CString::from_raw(text);
    }
}

#[cfg(not(any(target_os = "macos", target_os = "windows", target_os = "linux")))]
#[unsafe(no_mangle)]
pub unsafe extern "C" fn winit_clipboard_free_text(text: *mut c_char) {
    let _ = text;
}

#[unsafe(no_mangle)]
pub extern "C" fn winit_last_error_message() -> *const c_char {
    LAST_ERROR.with(|slot| match slot.borrow().as_ref() {
        Some(cstr) => cstr.as_ptr(),
        None => ptr::null(),
    })
}

#[repr(i32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum WinitStatus {
    Success = 0,
    NullPointer = 1,
    InvalidArgument = 2,
    RuntimeError = 3,
    WindowCreationFailed = 4,
    CallbackPanicked = 5,
    InvalidState = 6,
}

#[repr(i32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum WinitControlFlow {
    Poll = 0,
    Wait = 1,
    WaitUntil = 2,
    Exit = 3,
}

#[repr(i32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum WinitStartCause {
    Init = 0,
    Poll = 1,
    WaitCancelled = 2,
    ResumeTimeReached = 3,
}

#[repr(i32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum WinitEventKind {
    NewEvents = 0,
    Resumed = 1,
    Suspended = 2,
    WindowCreated = 3,
    WindowResized = 4,
    WindowScaleFactorChanged = 5,
    WindowCloseRequested = 6,
    WindowRedrawRequested = 7,
    WindowDestroyed = 8,
    AboutToWait = 9,
    MemoryWarning = 10,
    Exiting = 11,
    WindowFocused = 12,
    WindowFocusLost = 13,
    CursorMoved = 14,
    CursorEntered = 15,
    CursorLeft = 16,
    MouseInput = 17,
    MouseWheel = 18,
    KeyboardInput = 19,
    ModifiersChanged = 20,
    Touch = 21,
    TextInput = 22,
}

#[repr(i32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum WinitMouseButton {
    Left = 0,
    Right = 1,
    Middle = 2,
    Back = 3,
    Forward = 4,
    Other = 5,
}

#[repr(i32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum WinitElementState {
    Released = 0,
    Pressed = 1,
}

#[repr(i32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum WinitScrollDeltaKind {
    LineDelta = 0,
    PixelDelta = 1,
}

#[repr(i32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum WinitKeyLocation {
    Standard = 0,
    Left = 1,
    Right = 2,
    Numpad = 3,
}

#[repr(i32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum WinitTouchPhaseKind {
    Started = 0,
    Moved = 1,
    Ended = 2,
    Cancelled = 3,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct WinitWindowDescriptor {
    pub width: u32,
    pub height: u32,
    pub min_width: u32,
    pub min_height: u32,
    pub max_width: u32,
    pub max_height: u32,
    pub resizable: bool,
    pub decorations: bool,
    pub transparent: bool,
    pub visible: bool,
    pub title: *const c_char,
}

impl Default for WinitWindowDescriptor {
    fn default() -> Self {
        Self {
            width: 0,
            height: 0,
            min_width: 0,
            min_height: 0,
            max_width: 0,
            max_height: 0,
            resizable: true,
            decorations: true,
            transparent: false,
            visible: true,
            title: ptr::null(),
        }
    }
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct WinitRunOptions {
    pub create_window: bool,
    pub window: WinitWindowDescriptor,
}

impl Default for WinitRunOptions {
    fn default() -> Self {
        Self {
            create_window: true,
            window: WinitWindowDescriptor::default(),
        }
    }
}

#[repr(C)]
#[derive(Debug)]
pub struct WinitEvent {
    pub kind: WinitEventKind,
    pub window: *mut WinitWindowHandleOpaque,
    pub width: u32,
    pub height: u32,
    pub scale_factor: f64,
    pub start_cause: WinitStartCause,
    pub mouse_x: f64,
    pub mouse_y: f64,
    pub delta_x: f64,
    pub delta_y: f64,
    pub modifiers: u32,
    pub mouse_button: WinitMouseButton,
    pub mouse_button_value: u32,
    pub element_state: WinitElementState,
    pub scroll_delta_kind: WinitScrollDeltaKind,
    pub key_code: u32,
    pub key_code_name: [c_char; KEY_CODE_NAME_CAPACITY],
    pub key_location: WinitKeyLocation,
    pub repeat: bool,
    pub touch_id: u64,
    pub touch_phase: WinitTouchPhaseKind,
    pub text: *mut c_char,
}

impl Default for WinitEvent {
    fn default() -> Self {
        Self {
            kind: WinitEventKind::NewEvents,
            window: ptr::null_mut(),
            width: 0,
            height: 0,
            scale_factor: 1.0,
            start_cause: WinitStartCause::Init,
            mouse_x: 0.0,
            mouse_y: 0.0,
            delta_x: 0.0,
            delta_y: 0.0,
            modifiers: 0,
            mouse_button: WinitMouseButton::Left,
            mouse_button_value: 0,
            element_state: WinitElementState::Released,
            scroll_delta_kind: WinitScrollDeltaKind::LineDelta,
            key_code: 0,
            key_code_name: [0; KEY_CODE_NAME_CAPACITY],
            key_location: WinitKeyLocation::Standard,
            repeat: false,
            touch_id: 0,
            touch_phase: WinitTouchPhaseKind::Started,
            text: ptr::null_mut(),
        }
    }
}

impl WinitEvent {
    fn new(kind: WinitEventKind) -> Self {
        Self {
            kind,
            ..Self::default()
        }
    }
}

impl Drop for WinitEvent {
    fn drop(&mut self) {
        if !self.text.is_null() {
            unsafe {
                drop(CString::from_raw(self.text));
            }
            self.text = ptr::null_mut();
        }
    }
}

#[repr(C)]
pub struct WinitWindowHandleOpaque {
    _private: [u8; 0],
}

#[repr(C)]
pub struct WinitCallbackContextOpaque {
    _private: [u8; 0],
}

pub type WinitEventCallback = unsafe extern "C" fn(
    user_data: *mut c_void,
    context: *mut WinitCallbackContextOpaque,
    event: *const WinitEvent,
);

#[repr(i32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum VelloWindowHandleKind {
    None = 0,
    Win32 = 1,
    AppKit = 2,
    Wayland = 3,
    Xlib = 4,
    Headless = 100,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloWin32WindowHandle {
    pub hwnd: usize,
    pub hinstance: usize,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloAppKitWindowHandle {
    pub ns_view: *mut c_void,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloWaylandWindowHandle {
    pub surface: *mut c_void,
    pub display: *mut c_void,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloXlibWindowHandle {
    pub window: u64,
    pub display: *mut c_void,
    pub screen: i32,
    pub visual_id: u64,
}

#[repr(C)]
#[derive(Copy, Clone)]
pub union VelloWindowHandlePayload {
    pub win32: VelloWin32WindowHandle,
    pub appkit: VelloAppKitWindowHandle,
    pub wayland: VelloWaylandWindowHandle,
    pub xlib: VelloXlibWindowHandle,
    pub none: usize,
}

#[repr(C)]
#[derive(Copy, Clone)]
pub struct VelloWindowHandle {
    pub kind: VelloWindowHandleKind,
    pub payload: VelloWindowHandlePayload,
}

impl Default for VelloWindowHandle {
    fn default() -> Self {
        Self {
            kind: VelloWindowHandleKind::None,
            payload: VelloWindowHandlePayload { none: 0 },
        }
    }
}

#[derive(Clone)]
struct WindowConfig {
    width: Option<u32>,
    height: Option<u32>,
    min_size: Option<PhysicalSize<u32>>,
    max_size: Option<PhysicalSize<u32>>,
    resizable: bool,
    decorations: bool,
    transparent: bool,
    visible: bool,
    title: String,
}

impl WindowConfig {
    fn from_descriptor(descriptor: &WinitWindowDescriptor) -> Result<Self, WinitStatus> {
        let title = if descriptor.title.is_null() {
            "winit window".to_string()
        } else {
            match unsafe { CStr::from_ptr(descriptor.title) }.to_str() {
                Ok(value) => value.to_string(),
                Err(_) => {
                    set_last_error("Window title must be valid UTF-8");
                    return Err(WinitStatus::InvalidArgument);
                }
            }
        };

        let min_size = if descriptor.min_width == 0 || descriptor.min_height == 0 {
            None
        } else {
            Some(PhysicalSize::new(
                descriptor.min_width,
                descriptor.min_height,
            ))
        };
        let max_size = if descriptor.max_width == 0 || descriptor.max_height == 0 {
            None
        } else {
            Some(PhysicalSize::new(
                descriptor.max_width,
                descriptor.max_height,
            ))
        };

        Ok(Self {
            width: (descriptor.width != 0).then_some(descriptor.width),
            height: (descriptor.height != 0).then_some(descriptor.height),
            min_size,
            max_size,
            resizable: descriptor.resizable,
            decorations: descriptor.decorations,
            transparent: descriptor.transparent,
            visible: descriptor.visible,
            title,
        })
    }
}

#[derive(Default)]
struct SharedState {
    error: Option<String>,
    panic: bool,
}

struct CallbackContext {
    event_loop: *const ActiveEventLoop,
    application: *mut WinitApplication,
}

impl CallbackContext {
    unsafe fn event_loop(&self) -> Result<&ActiveEventLoop, WinitStatus> {
        unsafe { self.event_loop.as_ref() }.ok_or_else(|| {
            set_last_error("Event loop pointer is null");
            WinitStatus::InvalidArgument
        })
    }

    unsafe fn application(&self) -> Result<&mut WinitApplication, WinitStatus> {
        unsafe { self.application.as_mut() }.ok_or_else(|| {
            set_last_error("Application pointer is null");
            WinitStatus::InvalidArgument
        })
    }
}

struct WinitWindowHandle {
    id: u64,
    window: Window,
}

impl WinitWindowHandle {
    fn new(window: Window) -> Self {
        let id = u64::from(window.id());
        Self { id, window }
    }

    fn id(&self) -> u64 {
        self.id
    }

    fn window(&self) -> &Window {
        &self.window
    }
}

struct WinitApplication {
    callback: WinitEventCallback,
    user_data: *mut c_void,
    create_window: bool,
    window_config: WindowConfig,
    windows: HashMap<u64, NonNull<WinitWindowHandle>>,
    shared: Rc<RefCell<SharedState>>,
    modifiers: ModifiersState,
}

impl Drop for WinitApplication {
    fn drop(&mut self) {
        self.drop_window_handle();
    }
}

fn set_key_code_name(target: &mut [c_char; KEY_CODE_NAME_CAPACITY], value: &str) {
    target.fill(0);
    let bytes = value.as_bytes();
    let len = bytes.len().min(KEY_CODE_NAME_CAPACITY.saturating_sub(1));
    for (dst, src) in target.iter_mut().take(len).zip(bytes.iter()) {
        *dst = *src as c_char;
    }
}

impl WinitApplication {
    fn new(
        callback: WinitEventCallback,
        user_data: *mut c_void,
        create_window: bool,
        window_config: WindowConfig,
        shared: Rc<RefCell<SharedState>>,
    ) -> Self {
        Self {
            callback,
            user_data,
            create_window,
            window_config,
            windows: HashMap::new(),
            shared,
            modifiers: ModifiersState::empty(),
        }
    }

    fn drop_window_handle(&mut self) {
        for handle in self.windows.drain().map(|(_, handle)| handle) {
            unsafe { drop(Box::from_raw(handle.as_ptr())) };
        }
    }

    fn dispatch_event(&mut self, event_loop: &ActiveEventLoop, event: WinitEvent) {
        if self.shared.borrow().panic {
            return;
        }
        let mut context = CallbackContext {
            event_loop: event_loop as *const ActiveEventLoop,
            application: self as *mut _,
        };
        let ctx_ptr = (&mut context as *mut CallbackContext).cast::<WinitCallbackContextOpaque>();
        let result = catch_unwind(AssertUnwindSafe(|| unsafe {
            (self.callback)(self.user_data, ctx_ptr, &event as *const WinitEvent);
        }));
        if result.is_err() {
            self.shared.borrow_mut().panic = true;
            set_last_error("winit callback panicked");
            event_loop.exit();
        }
    }

    fn record_error(&self, message: impl Into<String>) {
        let message = message.into();
        let mut shared = self.shared.borrow_mut();
        if shared.error.is_none() {
            set_last_error(&message);
            shared.error = Some(message);
        }
    }

    fn fail(&mut self, event_loop: &ActiveEventLoop, message: impl Into<String>) {
        self.record_error(message);
        event_loop.exit();
    }

    fn update_modifiers_from_key_event(&mut self, key_event: &KeyEvent) -> ModifiersState {
        let mut modifiers = self.modifiers;

        let is_pressed = key_event.state == ElementState::Pressed;

        if let PhysicalKey::Code(code) = key_event.physical_key {
            match code {
                KeyCode::ShiftLeft | KeyCode::ShiftRight => {
                    modifiers.set(ModifiersState::SHIFT, is_pressed);
                }
                KeyCode::ControlLeft | KeyCode::ControlRight => {
                    modifiers.set(ModifiersState::CONTROL, is_pressed);
                }
                KeyCode::AltLeft | KeyCode::AltRight => {
                    modifiers.set(ModifiersState::ALT, is_pressed);
                }
                _ => {}
            }
        }

        if matches!(key_event.logical_key, Key::Named(NamedKey::Meta)) {
            // winit still exposes the meta modifier via the deprecated SUPER alias.
            #[allow(deprecated)]
            modifiers.set(ModifiersState::SUPER, is_pressed);
        }

        self.modifiers = modifiers;
        modifiers
    }

    fn ensure_window(
        &mut self,
        event_loop: &ActiveEventLoop,
    ) -> Result<NonNull<WinitWindowHandle>, WinitStatus> {
        if let Some((&_, handle)) = self.windows.iter().next() {
            return Ok(*handle);
        }

        let config = self.window_config.clone();
        self.create_window_from_config(event_loop, &config)
    }

    fn create_window_from_config(
        &mut self,
        event_loop: &ActiveEventLoop,
        config: &WindowConfig,
    ) -> Result<NonNull<WinitWindowHandle>, WinitStatus> {
        let mut attributes = WindowAttributes::default().with_title(config.title.clone());
        if let (Some(width), Some(height)) = (config.width, config.height) {
            attributes = attributes.with_inner_size(PhysicalSize::new(width, height));
        }
        if let Some(min_size) = config.min_size {
            attributes = attributes.with_min_inner_size(min_size);
        }
        if let Some(max_size) = config.max_size {
            attributes = attributes.with_max_inner_size(max_size);
        }
        attributes = attributes
            .with_resizable(config.resizable)
            .with_decorations(config.decorations)
            .with_transparent(config.transparent)
            .with_visible(config.visible);

        match event_loop.create_window(attributes) {
            Ok(window) => {
                let handle = Box::into_raw(Box::new(WinitWindowHandle::new(window)));
                let handle_ptr = unsafe { NonNull::new_unchecked(handle) };
                let key = unsafe { handle_ptr.as_ref().id() };
                self.windows.insert(key, handle_ptr);
                Ok(handle_ptr)
            }
            Err(err) => {
                self.fail(event_loop, format!("Failed to create window: {err}"));
                Err(WinitStatus::WindowCreationFailed)
            }
        }
    }

    fn create_window_with_descriptor(
        &mut self,
        event_loop: &ActiveEventLoop,
        descriptor: &WinitWindowDescriptor,
    ) -> Result<NonNull<WinitWindowHandle>, WinitStatus> {
        let config = WindowConfig::from_descriptor(descriptor)?;
        self.create_window_from_config(event_loop, &config)
    }

    fn handle_for_id(&mut self, window_id: WindowId) -> Option<&mut WinitWindowHandle> {
        let key = u64::from(window_id);
        let handle = self.windows.get_mut(&key)?;
        unsafe { Some(handle.as_mut()) }
    }

    fn remove_window(&mut self, window_id: WindowId) {
        let key = u64::from(window_id);
        if let Some(handle) = self.windows.remove(&key) {
            unsafe { drop(Box::from_raw(handle.as_ptr())) };
        }
    }

    fn first_window_ptr(&self) -> *mut WinitWindowHandleOpaque {
        self.windows
            .values()
            .next()
            .map(|handle| handle.as_ptr().cast::<WinitWindowHandleOpaque>())
            .unwrap_or(ptr::null_mut())
    }

    fn remove_window_by_key(&mut self, key: u64) {
        if let Some(handle) = self.windows.remove(&key) {
            unsafe { drop(Box::from_raw(handle.as_ptr())) };
        }
    }
}

impl ApplicationHandler<UserEvent> for WinitApplication {
    fn new_events(&mut self, event_loop: &ActiveEventLoop, cause: StartCause) {
        let mut event = WinitEvent::new(WinitEventKind::NewEvents);
        event.start_cause = match cause {
            StartCause::Init => WinitStartCause::Init,
            StartCause::Poll => WinitStartCause::Poll,
            StartCause::WaitCancelled { .. } => WinitStartCause::WaitCancelled,
            StartCause::ResumeTimeReached { .. } => WinitStartCause::ResumeTimeReached,
        };
        self.dispatch_event(event_loop, event);
    }

    fn resumed(&mut self, event_loop: &ActiveEventLoop) {
        let event = WinitEvent::new(WinitEventKind::Resumed);
        self.dispatch_event(event_loop, event);

        if self.create_window && self.windows.is_empty() {
            if let Ok(handle) = self.ensure_window(event_loop) {
                let window_handle = unsafe { handle.as_ref() };
                let mut created = WinitEvent::new(WinitEventKind::WindowCreated);
                created.window = handle.as_ptr().cast::<WinitWindowHandleOpaque>();
                let size = window_handle.window().inner_size();
                created.width = size.width;
                created.height = size.height;
                created.scale_factor = window_handle.window().scale_factor();
                self.dispatch_event(event_loop, created);
            }
        }
    }

    fn suspended(&mut self, event_loop: &ActiveEventLoop) {
        let event = WinitEvent::new(WinitEventKind::Suspended);
        self.dispatch_event(event_loop, event);
    }

    fn window_event(
        &mut self,
        event_loop: &ActiveEventLoop,
        window_id: WindowId,
        event: WindowEvent,
    ) {
        let Some(handle) = self.handle_for_id(window_id) else {
            return;
        };
        let handle_ptr = handle as *mut WinitWindowHandle;
        let window_ptr = handle_ptr.cast::<WinitWindowHandleOpaque>();
        match event {
            WindowEvent::Resized(size) => {
                let mut evt = WinitEvent::new(WinitEventKind::WindowResized);
                evt.window = window_ptr;
                evt.width = size.width;
                evt.height = size.height;
                evt.scale_factor = handle.window().scale_factor();
                self.dispatch_event(event_loop, evt);
            }
            WindowEvent::ScaleFactorChanged { scale_factor, .. } => {
                let mut evt = WinitEvent::new(WinitEventKind::WindowScaleFactorChanged);
                evt.window = window_ptr;
                evt.scale_factor = scale_factor;
                self.dispatch_event(event_loop, evt);
            }
            WindowEvent::Focused(true) => {
                let mut evt = WinitEvent::new(WinitEventKind::WindowFocused);
                evt.window = window_ptr;
                self.dispatch_event(event_loop, evt);
            }
            WindowEvent::Focused(false) => {
                self.modifiers = ModifiersState::empty();
                let mut evt = WinitEvent::new(WinitEventKind::WindowFocusLost);
                evt.window = window_ptr;
                self.dispatch_event(event_loop, evt);
            }
            WindowEvent::CursorMoved { position, .. } => {
                let mut evt = WinitEvent::new(WinitEventKind::CursorMoved);
                evt.window = window_ptr;
                evt.mouse_x = position.x;
                evt.mouse_y = position.y;
                evt.modifiers = self.modifiers.bits();
                self.dispatch_event(event_loop, evt);
            }
            WindowEvent::CursorEntered { .. } => {
                let mut evt = WinitEvent::new(WinitEventKind::CursorEntered);
                evt.window = window_ptr;
                self.dispatch_event(event_loop, evt);
            }
            WindowEvent::CursorLeft { .. } => {
                let mut evt = WinitEvent::new(WinitEventKind::CursorLeft);
                evt.window = window_ptr;
                self.dispatch_event(event_loop, evt);
            }
            WindowEvent::MouseInput { state, button, .. } => {
                let mut evt = WinitEvent::new(WinitEventKind::MouseInput);
                evt.window = window_ptr;
                evt.element_state = map_element_state(state);
                let (mouse_button, other_value) = map_mouse_button(button);
                evt.mouse_button = mouse_button;
                evt.mouse_button_value = other_value;
                evt.modifiers = self.modifiers.bits();
                self.dispatch_event(event_loop, evt);
            }
            WindowEvent::MouseWheel { delta, .. } => {
                let mut evt = WinitEvent::new(WinitEventKind::MouseWheel);
                evt.window = window_ptr;
                let (kind, delta_x, delta_y) = map_scroll_delta(delta);
                evt.scroll_delta_kind = kind;
                evt.delta_x = delta_x;
                evt.delta_y = delta_y;
                evt.modifiers = self.modifiers.bits();
                self.dispatch_event(event_loop, evt);
            }
            WindowEvent::KeyboardInput { event, .. } => {
                let mut evt = WinitEvent::new(WinitEventKind::KeyboardInput);
                evt.window = window_ptr;
                evt.element_state = map_element_state(event.state);
                let modifiers_state = self.update_modifiers_from_key_event(&event);
                evt.modifiers = modifiers_state.bits();
                evt.repeat = event.repeat;
                evt.key_location = map_key_location(event.location);
                match event.physical_key {
                    PhysicalKey::Code(code) => {
                        evt.key_code = code as u32;
                        let name = format!("{code:?}");
                        set_key_code_name(&mut evt.key_code_name, &name);
                    }
                    PhysicalKey::Unidentified(native) => {
                        let name = format!("Unidentified({native:?})");
                        set_key_code_name(&mut evt.key_code_name, &name);
                    }
                }
                if let Some(text) = event.text.as_ref() {
                    if !text.is_empty() {
                        if let Ok(cstr) = CString::new(text.as_str()) {
                            evt.text = cstr.into_raw();
                        }
                    }
                }
                self.dispatch_event(event_loop, evt);
            }
            WindowEvent::ModifiersChanged(modifiers) => {
                self.modifiers = modifiers.state();
                let mut evt = WinitEvent::new(WinitEventKind::ModifiersChanged);
                evt.window = window_ptr;
                evt.modifiers = self.modifiers.bits();
                self.dispatch_event(event_loop, evt);
            }
            WindowEvent::Ime(Ime::Commit(text)) => {
                if text.is_empty() {
                    return;
                }

                let mut evt = WinitEvent::new(WinitEventKind::TextInput);
                evt.window = window_ptr;
                if let Ok(cstr) = CString::new(text.as_str()) {
                    evt.text = cstr.into_raw();
                }
                evt.modifiers = self.modifiers.bits();
                self.dispatch_event(event_loop, evt);
            }
            WindowEvent::Touch(touch) => {
                let mut evt = WinitEvent::new(WinitEventKind::Touch);
                evt.window = window_ptr;
                evt.mouse_x = touch.location.x;
                evt.mouse_y = touch.location.y;
                evt.touch_id = touch.id;
                evt.touch_phase = map_touch_phase(touch.phase);
                evt.modifiers = self.modifiers.bits();
                self.dispatch_event(event_loop, evt);
            }
            WindowEvent::RedrawRequested => {
                let mut evt = WinitEvent::new(WinitEventKind::WindowRedrawRequested);
                evt.window = window_ptr;
                self.dispatch_event(event_loop, evt);
            }
            WindowEvent::CloseRequested => {
                let mut evt = WinitEvent::new(WinitEventKind::WindowCloseRequested);
                evt.window = window_ptr;
                self.dispatch_event(event_loop, evt);
            }
            WindowEvent::Destroyed => {
                let mut evt = WinitEvent::new(WinitEventKind::WindowDestroyed);
                evt.window = window_ptr;
                self.dispatch_event(event_loop, evt);
                self.remove_window(window_id);
            }
            _ => {}
        }
    }

    fn device_event(
        &mut self,
        _event_loop: &ActiveEventLoop,
        _device_id: winit::event::DeviceId,
        _event: DeviceEvent,
    ) {
        // Device events are currently ignored in the FFI layer.
    }

    fn about_to_wait(&mut self, event_loop: &ActiveEventLoop) {
        let event = WinitEvent::new(WinitEventKind::AboutToWait);
        self.dispatch_event(event_loop, event);
    }

    fn memory_warning(&mut self, event_loop: &ActiveEventLoop) {
        let event = WinitEvent::new(WinitEventKind::MemoryWarning);
        self.dispatch_event(event_loop, event);
    }

    fn exiting(&mut self, event_loop: &ActiveEventLoop) {
        let event = WinitEvent::new(WinitEventKind::Exiting);
        self.dispatch_event(event_loop, event);
    }

    fn user_event(&mut self, _event_loop: &ActiveEventLoop, _event: UserEvent) {}
}

fn map_element_state(state: ElementState) -> WinitElementState {
    match state {
        ElementState::Pressed => WinitElementState::Pressed,
        ElementState::Released => WinitElementState::Released,
    }
}

fn map_mouse_button(button: MouseButton) -> (WinitMouseButton, u32) {
    match button {
        MouseButton::Left => (WinitMouseButton::Left, 0),
        MouseButton::Right => (WinitMouseButton::Right, 0),
        MouseButton::Middle => (WinitMouseButton::Middle, 0),
        MouseButton::Back => (WinitMouseButton::Back, 0),
        MouseButton::Forward => (WinitMouseButton::Forward, 0),
        MouseButton::Other(value) => (WinitMouseButton::Other, value.into()),
    }
}

fn map_scroll_delta(delta: MouseScrollDelta) -> (WinitScrollDeltaKind, f64, f64) {
    match delta {
        MouseScrollDelta::LineDelta(x, y) => (WinitScrollDeltaKind::LineDelta, x as f64, y as f64),
        MouseScrollDelta::PixelDelta(pos) => (WinitScrollDeltaKind::PixelDelta, pos.x, pos.y),
    }
}

fn map_key_location(location: KeyLocation) -> WinitKeyLocation {
    match location {
        KeyLocation::Standard => WinitKeyLocation::Standard,
        KeyLocation::Left => WinitKeyLocation::Left,
        KeyLocation::Right => WinitKeyLocation::Right,
        KeyLocation::Numpad => WinitKeyLocation::Numpad,
    }
}

fn map_touch_phase(phase: TouchPhase) -> WinitTouchPhaseKind {
    match phase {
        TouchPhase::Started => WinitTouchPhaseKind::Started,
        TouchPhase::Moved => WinitTouchPhaseKind::Moved,
        TouchPhase::Ended => WinitTouchPhaseKind::Ended,
        TouchPhase::Cancelled => WinitTouchPhaseKind::Cancelled,
    }
}

fn with_context<F>(ctx: *mut WinitCallbackContextOpaque, mut f: F) -> Result<(), WinitStatus>
where
    F: FnMut(&mut CallbackContext) -> Result<(), WinitStatus>,
{
    let Some(context) = (unsafe { (ctx as *mut CallbackContext).as_mut() }) else {
        set_last_error("Context pointer is null");
        return Err(WinitStatus::NullPointer);
    };
    f(context)
}

fn with_window<F>(window: *mut WinitWindowHandleOpaque, mut f: F) -> Result<(), WinitStatus>
where
    F: FnMut(&mut WinitWindowHandle) -> Result<(), WinitStatus>,
{
    let Some(handle) = (unsafe { (window as *mut WinitWindowHandle).as_mut() }) else {
        set_last_error("Window handle pointer is null");
        return Err(WinitStatus::NullPointer);
    };
    f(handle)
}

fn fill_vello_window_handle(
    window: &Window,
    out_handle: &mut VelloWindowHandle,
) -> Result<(), WinitStatus> {
    let window_handle = match window.window_handle() {
        Ok(handle) => handle,
        Err(err) => {
            set_last_error(format!("Failed to query window handle: {err}"));
            return Err(WinitStatus::InvalidArgument);
        }
    };
    let display_handle = match window.display_handle() {
        Ok(handle) => handle,
        Err(err) => {
            set_last_error(format!("Failed to query display handle: {err}"));
            return Err(WinitStatus::InvalidArgument);
        }
    };

    let raw_window = window_handle.as_raw();
    let raw_display = display_handle.as_raw();

    match raw_window {
        RawWindowHandle::Win32(win32) => {
            let hwnd = win32.hwnd;
            let hinstance = win32.hinstance.map(NonZeroIsize::get).unwrap_or(0);
            out_handle.kind = VelloWindowHandleKind::Win32;
            out_handle.payload.win32 = VelloWin32WindowHandle {
                hwnd: hwnd.get() as usize,
                hinstance: hinstance as usize,
            };
            Ok(())
        }
        RawWindowHandle::AppKit(appkit) => {
            out_handle.kind = VelloWindowHandleKind::AppKit;
            out_handle.payload.appkit = VelloAppKitWindowHandle {
                ns_view: appkit.ns_view.as_ptr(),
            };
            Ok(())
        }
        RawWindowHandle::Wayland(wayland) => {
            let RawDisplayHandle::Wayland(display) = raw_display else {
                set_last_error("Wayland display handle missing");
                return Err(WinitStatus::InvalidArgument);
            };
            out_handle.kind = VelloWindowHandleKind::Wayland;
            out_handle.payload.wayland = VelloWaylandWindowHandle {
                surface: wayland.surface.as_ptr(),
                display: display.display.as_ptr(),
            };
            Ok(())
        }
        RawWindowHandle::Xlib(xlib) => {
            let RawDisplayHandle::Xlib(display) = raw_display else {
                set_last_error("Xlib display handle missing");
                return Err(WinitStatus::InvalidArgument);
            };
            out_handle.kind = VelloWindowHandleKind::Xlib;
            let display_ptr = display
                .display
                .map(|ptr| ptr.as_ptr())
                .unwrap_or(ptr::null_mut());
            out_handle.payload.xlib = VelloXlibWindowHandle {
                window: u64::from(xlib.window),
                display: display_ptr,
                screen: display.screen,
                visual_id: u64::from(xlib.visual_id),
            };
            Ok(())
        }
        _ => {
            set_last_error("Unsupported window handle kind");
            Err(WinitStatus::InvalidArgument)
        }
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn winit_context_set_control_flow(
    ctx: *mut WinitCallbackContextOpaque,
    flow: WinitControlFlow,
    wait_millis: i64,
) -> WinitStatus {
    clear_last_error();
    match with_context(ctx, |context| {
        let event_loop = unsafe { context.event_loop()? };
        let control_flow = match flow {
            WinitControlFlow::Poll => ControlFlow::Poll,
            WinitControlFlow::Wait => ControlFlow::Wait,
            WinitControlFlow::WaitUntil => {
                let millis = wait_millis.max(0) as u64;
                ControlFlow::wait_duration(Duration::from_millis(millis))
            }
            WinitControlFlow::Exit => {
                event_loop.exit();
                ControlFlow::Wait
            }
        };
        event_loop.set_control_flow(control_flow);
        Ok(())
    }) {
        Ok(()) => WinitStatus::Success,
        Err(status) => status,
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn winit_context_exit(ctx: *mut WinitCallbackContextOpaque) -> WinitStatus {
    clear_last_error();
    match with_context(ctx, |context| {
        let event_loop = unsafe { context.event_loop()? };
        event_loop.exit();
        Ok(())
    }) {
        Ok(()) => WinitStatus::Success,
        Err(status) => status,
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn winit_context_is_exiting(
    ctx: *mut WinitCallbackContextOpaque,
    out_exiting: *mut bool,
) -> WinitStatus {
    clear_last_error();
    let Some(out) = (unsafe { out_exiting.as_mut() }) else {
        set_last_error("Output pointer is null");
        return WinitStatus::NullPointer;
    };
    match with_context(ctx, |context| {
        let event_loop = unsafe { context.event_loop()? };
        *out = event_loop.exiting();
        Ok(())
    }) {
        Ok(()) => WinitStatus::Success,
        Err(status) => status,
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn winit_context_get_window(
    ctx: *mut WinitCallbackContextOpaque,
    out_window: *mut *mut WinitWindowHandleOpaque,
) -> WinitStatus {
    clear_last_error();
    let Some(out) = (unsafe { out_window.as_mut() }) else {
        set_last_error("Output pointer is null");
        return WinitStatus::NullPointer;
    };
    match with_context(ctx, |context| {
        let application = unsafe { context.application()? };
        *out = application.first_window_ptr();
        Ok(())
    }) {
        Ok(()) => WinitStatus::Success,
        Err(status) => status,
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn winit_context_create_window(
    ctx: *mut WinitCallbackContextOpaque,
    descriptor: *const WinitWindowDescriptor,
    out_window: *mut *mut WinitWindowHandleOpaque,
) -> WinitStatus {
    clear_last_error();

    let descriptor_ref = unsafe { descriptor.as_ref() };
    let Some(out) = (unsafe { out_window.as_mut() }) else {
        set_last_error("Output pointer is null");
        return WinitStatus::NullPointer;
    };

    match with_context(ctx, |context| {
        let event_loop = unsafe { context.event_loop()? };
        let application = unsafe { context.application()? };
        let handle = if let Some(descriptor) = descriptor_ref {
            application.create_window_with_descriptor(event_loop, descriptor)?
        } else {
            application.ensure_window(event_loop)?
        };
        *out = handle.as_ptr().cast::<WinitWindowHandleOpaque>();
        Ok(())
    }) {
        Ok(()) => WinitStatus::Success,
        Err(status) => status,
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn winit_context_destroy_window(
    ctx: *mut WinitCallbackContextOpaque,
    window: *mut WinitWindowHandleOpaque,
) -> WinitStatus {
    clear_last_error();
    if window.is_null() {
        set_last_error("Window pointer is null");
        return WinitStatus::NullPointer;
    }

    match with_context(ctx, |context| {
        let application = unsafe { context.application()? };
        let handle_ptr = NonNull::new(window.cast::<WinitWindowHandle>()).ok_or_else(|| {
            set_last_error("Window pointer is null");
            WinitStatus::NullPointer
        })?;
        let key = unsafe { handle_ptr.as_ref().id() };
        application.remove_window_by_key(key);
        Ok(())
    }) {
        Ok(()) => WinitStatus::Success,
        Err(status) => status,
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn winit_window_request_redraw(
    window: *mut WinitWindowHandleOpaque,
) -> WinitStatus {
    clear_last_error();
    match with_window(window, |handle| {
        handle.window().request_redraw();
        Ok(())
    }) {
        Ok(()) => WinitStatus::Success,
        Err(status) => status,
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn winit_window_pre_present_notify(
    window: *mut WinitWindowHandleOpaque,
) -> WinitStatus {
    clear_last_error();
    match with_window(window, |handle| {
        handle.window().pre_present_notify();
        Ok(())
    }) {
        Ok(()) => WinitStatus::Success,
        Err(status) => status,
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn winit_window_surface_size(
    window: *mut WinitWindowHandleOpaque,
    out_width: *mut u32,
    out_height: *mut u32,
) -> WinitStatus {
    clear_last_error();
    let Some(width) = (unsafe { out_width.as_mut() }) else {
        set_last_error("Width pointer is null");
        return WinitStatus::NullPointer;
    };
    let Some(height) = (unsafe { out_height.as_mut() }) else {
        set_last_error("Height pointer is null");
        return WinitStatus::NullPointer;
    };
    match with_window(window, |handle| {
        let size = handle.window().inner_size();
        *width = size.width;
        *height = size.height;
        Ok(())
    }) {
        Ok(()) => WinitStatus::Success,
        Err(status) => status,
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn winit_window_scale_factor(
    window: *mut WinitWindowHandleOpaque,
    out_scale: *mut f64,
) -> WinitStatus {
    clear_last_error();
    let Some(scale) = (unsafe { out_scale.as_mut() }) else {
        set_last_error("Scale pointer is null");
        return WinitStatus::NullPointer;
    };
    match with_window(window, |handle| {
        *scale = handle.window().scale_factor();
        Ok(())
    }) {
        Ok(()) => WinitStatus::Success,
        Err(status) => status,
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn winit_window_id(
    window: *mut WinitWindowHandleOpaque,
    out_id: *mut u64,
) -> WinitStatus {
    clear_last_error();
    let Some(id) = (unsafe { out_id.as_mut() }) else {
        set_last_error("Id pointer is null");
        return WinitStatus::NullPointer;
    };
    match with_window(window, |handle| {
        *id = handle.id();
        Ok(())
    }) {
        Ok(()) => WinitStatus::Success,
        Err(status) => status,
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn winit_window_set_title(
    window: *mut WinitWindowHandleOpaque,
    title: *const c_char,
) -> WinitStatus {
    clear_last_error();
    let Some(title_ptr) = (unsafe { title.as_ref() }) else {
        set_last_error("Title pointer is null");
        return WinitStatus::NullPointer;
    };
    let title_str = match unsafe { CStr::from_ptr(title_ptr) }.to_str() {
        Ok(value) => value,
        Err(_) => {
            set_last_error("Window title must be valid UTF-8");
            return WinitStatus::InvalidArgument;
        }
    };
    match with_window(window, |handle| {
        handle.window().set_title(title_str);
        Ok(())
    }) {
        Ok(()) => WinitStatus::Success,
        Err(status) => status,
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn winit_window_set_inner_size(
    window: *mut WinitWindowHandleOpaque,
    width: u32,
    height: u32,
) -> WinitStatus {
    clear_last_error();
    match with_window(window, |handle| {
        let _ = handle
            .window()
            .request_inner_size(PhysicalSize::new(width, height));
        Ok(())
    }) {
        Ok(()) => WinitStatus::Success,
        Err(status) => status,
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn winit_window_set_min_inner_size(
    window: *mut WinitWindowHandleOpaque,
    width: u32,
    height: u32,
) -> WinitStatus {
    clear_last_error();
    match with_window(window, |handle| {
        let size = if width == 0 || height == 0 {
            None
        } else {
            Some(PhysicalSize::new(width, height))
        };
        handle.window().set_min_inner_size(size);
        Ok(())
    }) {
        Ok(()) => WinitStatus::Success,
        Err(status) => status,
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn winit_window_set_max_inner_size(
    window: *mut WinitWindowHandleOpaque,
    width: u32,
    height: u32,
) -> WinitStatus {
    clear_last_error();
    match with_window(window, |handle| {
        let size = if width == 0 || height == 0 {
            None
        } else {
            Some(PhysicalSize::new(width, height))
        };
        handle.window().set_max_inner_size(size);
        Ok(())
    }) {
        Ok(()) => WinitStatus::Success,
        Err(status) => status,
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn winit_window_set_visible(
    window: *mut WinitWindowHandleOpaque,
    visible: bool,
) -> WinitStatus {
    clear_last_error();
    match with_window(window, |handle| {
        handle.window().set_visible(visible);
        Ok(())
    }) {
        Ok(()) => WinitStatus::Success,
        Err(status) => status,
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn winit_window_set_resizable(
    window: *mut WinitWindowHandleOpaque,
    resizable: bool,
) -> WinitStatus {
    clear_last_error();
    match with_window(window, |handle| {
        handle.window().set_resizable(resizable);
        Ok(())
    }) {
        Ok(()) => WinitStatus::Success,
        Err(status) => status,
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn winit_window_set_decorations(
    window: *mut WinitWindowHandleOpaque,
    decorations: bool,
) -> WinitStatus {
    clear_last_error();
    match with_window(window, |handle| {
        handle.window().set_decorations(decorations);
        Ok(())
    }) {
        Ok(()) => WinitStatus::Success,
        Err(status) => status,
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn winit_window_set_minimized(
    window: *mut WinitWindowHandleOpaque,
    minimized: bool,
) -> WinitStatus {
    clear_last_error();
    match with_window(window, |handle| {
        handle.window().set_minimized(minimized);
        Ok(())
    }) {
        Ok(()) => WinitStatus::Success,
        Err(status) => status,
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn winit_window_set_maximized(
    window: *mut WinitWindowHandleOpaque,
    maximized: bool,
) -> WinitStatus {
    clear_last_error();
    match with_window(window, |handle| {
        handle.window().set_maximized(maximized);
        Ok(())
    }) {
        Ok(()) => WinitStatus::Success,
        Err(status) => status,
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn winit_window_focus(window: *mut WinitWindowHandleOpaque) -> WinitStatus {
    clear_last_error();
    match with_window(window, |handle| {
        handle.window().focus_window();
        Ok(())
    }) {
        Ok(()) => WinitStatus::Success,
        Err(status) => status,
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn winit_window_get_vello_handle(
    window: *mut WinitWindowHandleOpaque,
    out_handle: *mut VelloWindowHandle,
) -> WinitStatus {
    clear_last_error();
    let Some(out) = (unsafe { out_handle.as_mut() }) else {
        set_last_error("Output pointer is null");
        return WinitStatus::NullPointer;
    };
    match with_window(window, |handle| {
        fill_vello_window_handle(handle.window(), out)
    }) {
        Ok(()) => WinitStatus::Success,
        Err(status) => status,
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn winit_event_loop_wake() -> WinitStatus {
    clear_last_error();
    match with_event_loop_proxy(|proxy| {
        proxy.send_event(UserEvent::Wake).map_err(|err| {
            set_last_error(format!("Failed to signal event loop: {err}"));
            WinitStatus::InvalidState
        })
    }) {
        Ok(()) => WinitStatus::Success,
        Err(status) => status,
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn winit_event_loop_run(
    options: *const WinitRunOptions,
    callback: WinitEventCallback,
    user_data: *mut c_void,
) -> WinitStatus {
    clear_last_error();
    if (callback as usize) == 0 {
        set_last_error("Callback pointer is null");
        return WinitStatus::InvalidArgument;
    }

    let opts = unsafe { options.as_ref() }.copied().unwrap_or_default();
    let window_config = match WindowConfig::from_descriptor(&opts.window) {
        Ok(config) => config,
        Err(status) => return status,
    };

    let event_loop = match EventLoop::with_user_event().build() {
        Ok(loop_) => loop_,
        Err(err) => {
            set_last_error(format!("Failed to create event loop: {err}"));
            return WinitStatus::RuntimeError;
        }
    };

    let proxy = event_loop.create_proxy();
    let _proxy_guard = ProxyRegistration::register(proxy);

    let shared = Rc::new(RefCell::new(SharedState::default()));
    let mut app = WinitApplication::new(
        callback,
        user_data,
        opts.create_window,
        window_config,
        Rc::clone(&shared),
    );

    match event_loop.run_app(&mut app) {
        Ok(()) => {
            let shared_state = shared.borrow();
            if shared_state.panic {
                set_last_error("winit callback panicked");
                WinitStatus::CallbackPanicked
            } else if let Some(message) = shared_state.error.as_ref() {
                set_last_error(message.clone());
                WinitStatus::RuntimeError
            } else {
                WinitStatus::Success
            }
        }
        Err(err) => {
            set_last_error(format!("Event loop error: {err}"));
            WinitStatus::RuntimeError
        }
    }
}

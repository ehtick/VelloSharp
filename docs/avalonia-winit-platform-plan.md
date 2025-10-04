# Avalonia Winit Platform Implementation Plan

## Background
- `VelloSharp` already exposes winit FFI bindings (`WinitEventLoop`, `WinitWindow`, etc.) used by native samples.
- Avalonia currently relies on Win32/X11/etc. specific implementations. We want a new windowing subsystem that drives Avalonia using winit for cross-platform eventing and surfaces.
- Consolonia config shows how to opt into Avalonia unstable/private APIs via MSBuild props; we need similar settings for the new platform library so it can access required interfaces.

## Goals
- Provide a reusable `VelloSharp.Avalonia.Winit` project that implements Avalonia's `IWindowingPlatform` using the existing winit FFI layer.
- Expose an `AppBuilder` extension (e.g. `UseWinit()`) to enable the subsystem.
- Translate winit window, pointer, keyboard, and resize events into Avalonia raw input events and callbacks.
- Supply the native window handles via `INativePlatformHandleSurface` so Skia/Vello render paths can interoperate.
- Add a sample Avalonia application that boots exclusively on the new Winit backend.
- Replicate necessary MSBuild props to allow consuming the platform from external solutions (private API opt-in, Avalonia version pinning, etc.).

## Non-Goals & Assumptions
- Only classic desktop-style lifetime is targeted (single UI thread). Additional lifetimes (like single-view) are out of scope.
- Initial implementation can limit functionality (e.g. no tray icons, no popups/multiple windows) but must cleanly report unsupported features.
- Keyboard mapping will use winit's physical key codes; locale-specific IME/text composition is deferred.
- Rendering is delegated to Avalonia's existing rendering subsystems; we just provide window handles and event pumps.

## Architecture Overview
1. **Platform bootstrap**
   - Create `WinitApplicationExtensions` to expose `AppBuilder.UseWinit()` similar to `UseWin32`.
   - Implement static `WinitPlatform.Initialize(WinitPlatformOptions options)` that registers services in `AvaloniaLocator`:
     - `IDispatcherImpl` → custom dispatcher tied to the winit event loop.
     - `IWindowingPlatform`, `IScreenImpl`, `IKeyboardDevice`, `ICursorFactory`, `IClipboard` stubs, `IRenderTimer`, `IPlatformSettings`, `PlatformHotkeyConfiguration`, etc.
     - Set up `Compositor` instance using existing Avalonia composition infrastructure.

2. **Dispatcher / Event loop**
   - Build `WinitDispatcher` implementing `IControlledDispatcherImpl` and `IWinitEventHandler`.
   - Own a single `WinitEventLoop` instance, a work queue for dispatcher posts, and manages exit requests.
   - Map dispatcher `Signal`/`UpdateTimer` calls to winit `ControlFlow::Poll`/`WaitUntil` using context helpers.

3. **Window implementation**
   - `WinitWindowImpl` implementing `IWindowImpl`, `INativePlatformHandleSurface`.
   - Manage lifecycle: creation during `CreateWindow`, `Show`, `Hide`, `Resize`, `SetTitle`, etc.
   - Keep references to Avalonia callbacks (`Input`, `Paint`, `Resized`, `Closed`, etc.) and forward winit events.
   - Provide `PixelSize`, `RenderScaling` using `WinitWindow.GetSurfaceSize()` and `ScaleFactor`.
   - Emulate focus/activation callbacks based on winit focus events.

4. **Event translation**
   - Convert winit mouse events to Avalonia `RawPointerEventArgs`, tracking current pointer position and button modifiers.
   - Convert scroll events into pointer wheel events with appropriate delta scaling.
   - Convert keyboard events using a lookup from winit `KeyCode` (cast to `PhysicalKey`) to Avalonia's `Key` enum; produce `RawKeyEventArgs`.
   - Translate modifier state bitmasks into `RawInputModifiers` for every dispatched event.

5. **Auxiliary services**
   - Minimal `WinitCursorFactory` that returns default cursors (with potential TODO markers).
   - Stub clipboard implementation returning not supported yet (throwing or storing in-memory).
   - `WinitPlatformSettings` deriving from `DefaultPlatformSettings` for theme hints.
   - Simple `WinitScreens` implementing `IScreenImpl` with a single primary monitor using winit-reported dimensions.

6. **Build configuration**
   - Create `Directory.WinitPlatform.props` (or similar) alongside the project to set:
     - `AvaloniaAccessUnstablePrivateApis` & related pledge flags.
     - `Nullable`/`LangVersion`/analysis settings consistent with repo style.
   - Ensure project references `Avalonia`, `Avalonia.Desktop`, and existing `VelloSharp` packages as needed.

7. **Sample application**
  - Add `samples/AvaloniaVelloWinitDemo` with minimal MVVM window that calls `.UseWinit()` in `BuildAvaloniaApp`.
   - Demonstrate `VelloSurfaceView` usage inside the sample to validate rendering on the new backend.

8. **Solution & documentation updates**
   - Add new projects to `VelloSharp.sln`.
   - Update `README.md` and `docs/ffi-api-coverage.md` with new platform description / status.
  - Provide run instructions (`dotnet run --project samples/AvaloniaVelloWinitDemo/...`).

## Implementation Steps
1. **Scaffold projects & build props**
   - Create `src/VelloSharp.Avalonia.Winit` directory with project file and props.
   - Reference required packages & projects; ensure unsafe blocks allowed.
   - Register project in solution.

2. **Dispatcher & event loop integration**
   - Implement `WinitControlFlowHelper` to bridge dispatcher timers to winit `ControlFlow` states.
   - Create `WinitDispatcher` and verify it can pump queued managed callbacks while processing winit events.

3. **Window & platform services**
   - Implement `WinitWindowingPlatform` with `CreateWindow`, `CreatePopup`, etc. (throw `NotSupportedException` where appropriate).
   - Build `WinitWindowImpl` with lifecycle management, surface provisioning, and event callbacks.
   - Add auxiliary helpers (cursor factory, clipboard stub, screens, platform settings).

4. **Event translation layer**
   - Implement mapping helpers for modifiers, mouse buttons, pointer actions, and key codes.
   - Ensure pointer capture and double buffering of pointer position/hover state.
   - Integrate with `WinitWindowImpl` event handling pipeline.

5. **Sample application**
  - Scaffold `samples/AvaloniaVelloWinitDemo` using `.UseWinit()` and `ClassicDesktopStyleApplicationLifetime`.
   - Embed `VelloSurfaceView` (or fallback `VelloView`) to showcase rendering with winit backend.

6. **Validation & docs**
   - Build solution (`dotnet build VelloSharp.sln`).
   - Run sample to confirm window opens (document manual verification steps if automated test not feasible).
   - Update README/docs with new backend instructions and status notes.

## Testing & Verification
- `dotnet build VelloSharp.sln`
- `dotnet run --project samples/AvaloniaVelloWinitDemo/AvaloniaVelloWinitDemo.csproj`
- Verify pointer + keyboard interactions produce expected output (document manual testing expectations).

## Follow-Up Work (Out of Scope)
- Multi-window and popup support.
- IME/text input integration and clipboard bridging to OS.
- Cursor customization and icon loading.
- Comprehensive unit/integration tests for event translation.

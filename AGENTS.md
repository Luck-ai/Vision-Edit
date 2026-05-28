# AGENTS.md

Shared guidance for any AI coding agent working in this repository (Claude Code, Gemini, etc.). `CLAUDE.md` and `GEMINI.md` redirect here.

## Project overview

VisionEditCV is a cross-platform .NET 8 image editor (Windows / Linux / macOS / Android) built on **Avalonia 11.3**. It loads an image, segments regions via a remote SAM3 REST API (bounding-box or text prompt), and applies per-mask effects (Color Grading, Artistic Style, Sticker Generation, Pixelation/Blur, Portrait DOF, Grayscale). Live debounced previews, undo, before/after compare, and Velopack auto-update are all wired in. The app opens on a **Library / Home** view (project list with filter/search/pin) and cross-fades into the editor.

## Common commands

```bash
# Build everything (desktop)
dotnet build src/VisionEditCV.Desktop/VisionEditCV.Desktop.csproj

# Run desktop in dev
dotnet run --project src/VisionEditCV.Desktop

# Build Android (needs JDK 17 + ANDROID_HOME/ANDROID_SDK_ROOT/JAVA_HOME set)
dotnet build src/VisionEditCV.Android/VisionEditCV.Android.csproj \
    -p:RunAOTCompilation=false -p:AndroidEnableProfiledAot=false

# Package a release (Velopack)
./publish.sh 1.6.0 linux-x64           # linux-x64 | osx-x64 | osx-arm64
./publish.ps1 -Version 1.6.0           # win-x64 (Windows host only)
```

`global.json` pins SDK **8.0.421** with `rollForward: latestFeature`. If `dotnet --version` doesn't satisfy that, point at the user-local install: `~/.dotnet/dotnet …`.

There is no test project — verify changes by running the app and exercising the affected flow.

## Architecture

Four projects in `src/`:

- **VisionEditCV.Core** — platform-agnostic. `Api/Sam3Client.cs` (REST client) and `Processing/ImageEffects.cs` (every effect, written against `Emgu.CV.Mat` + `MCvScalar`). Pins Emgu.CV 4.12 + `mini.{ubuntu-x64,windows,macos}` natives.
- **VisionEditCV.Shared** — the Avalonia UI. All `ViewModels/`, `Views/`, `Styles/`, `Controls/`, `Helpers/`, `Models/`, `Converters/`. Single set of views runs on both desktop and mobile.
- **VisionEditCV.Desktop** — desktop bootstrap (`App.axaml.cs`, `Program.cs`, `Views/MainWindow.axaml`, `DesktopUpdateService.cs`). Injects `AppShellViewModel` into `MainShellView`.
- **VisionEditCV.Android** — Android bootstrap (`MainActivity.cs`, `SplashActivity.cs`, `AndroidManifest.xml`, `Resources/`). Injects `AppShellViewModel` into `MobileShellView`.
- **VisionEditCV.WinForms** — legacy Windows-only build, kept as reference. Do not extend.

### Shell + navigation

`AppShellViewModel` owns one `LibraryViewModel` and one `MainWindowViewModel` (the editor). Its `ActiveView` flips between `"Library"` and `"Editor"`; `CurrentViewModel` returns whichever VM matches. Both `MainShellView` (desktop) and `MobileShellView` use a `TransitioningContentControl` + `CrossFade` over `CurrentViewModel`, with typed `<DataTemplate>` selectors in `UserControl.DataTemplates`.

Editor → Library nav is event-driven: `MainWindowViewModel.BackToLibraryCommand` raises `BackRequested`; `AppShellViewModel` subscribes and sets `ActiveView = "Library"`. Library → Editor: `LibraryViewModel.ProjectOpened` event fires after a successful `OpenImage`/`OpenImageFromPath`; shell flips to `"Editor"`.

Mobile bottom-nav tabs (Library / History / Settings) live on `MobileTab` and host `MobileHomeView` / `MobileHistoryView` / `MobileSettingsView`.

### Project persistence

`Helpers/ProjectStore` round-trips `Models/ProjectItem` to/from `LocalAppData/VisionEditCV/projects.json`. `LibraryViewModel` subscribes to `MainWindowViewModel.PropertyChanged(CurrentImagePath)` to auto-record any opened image, `MainWindowViewModel.ProjectExported` to flip the `IsExported` flag, and `MaskItems.CollectionChanged` to update `MaskCount`. `RebuildProjects()` reapplies the active filter + search and is the single point that writes to the visible `Projects` collection (which always starts with the "Open new image" tile).

### MVVM and editor state model

`MainWindowViewModel` is a `CommunityToolkit.Mvvm` `[ObservableProperty]` view model. The image-editing state machine is non-obvious:

- `CurrentImage` — the loaded original. Never mutated after Open.
- `ProcessedImage` — what the canvas actually shows. May be a *live preview* (from a debounced background task) or a *committed* result (from Apply).
- `_history` is a `Stack<Bitmap?>` of **post-apply** states. `ApplyEffect` pushes `ProcessedImage` itself onto the stack, so `_history.Peek()` is "the latest committed state" and the same reference is also held by `ProcessedImage`. Because the references are shared, **do not `Dispose()` bitmaps that may be in `_history`** — clear the reference and let the GC reclaim them.
- `History` (the `ObservableCollection<string>` bound to the History tab) is a parallel list of effect names and must be kept in lockstep with `_history`.
- `_previewSourceSnapshot` is a `Mat` clone of the latest committed state, captured on every effect switch and after every Apply. The live-preview pipeline (`TriggerPreviewUpdate`) sources from this snapshot so slider tweaks never compound — critical for the Sticker effect.

When you reset state (`OpenImage`, `LoadImageFromPath`, `ClearMasks`, `ResetAll`), do not call `.Clear()` on the `Masks` list — `ImageCanvas` only invalidates its cached mask `SKImage`s when the `MasksProperty` *reference* changes. Reassign: `Masks = new List<float[,]>();`.

`OpenImage(window)` shows the file picker; `OpenImageFromPath(path)` skips it (used by Library when the user clicks a saved project). Both funnel through `LoadImageFromPath`.

### Mobile sheet state

`MobileSheetTab` ("" | "Segment" | "Effects" | "Layers" | "History" | "Server") drives the editor's bottom-sheet modals. `ToggleMobileSheetCommand` toggles, `CloseMobileSheetCommand` clears.

### Rendering

`Controls/ImageCanvas.cs` is a custom Avalonia control that owns a Skia draw operation (`ICustomDrawOperation`). It caches `SKImage`s for the original, processed, and per-mask overlays, and rebuilds them only when the corresponding `StyledProperty` reference changes — keep that in mind when wiring new bindings. Zoom-around-mouse and panning state live here too.

For pixel-level work in this control, use `SKBitmap.GetPixels()` directly and keep the draw operation allocation-free.

### Effects pipeline

`VisionEditCV.Core/Processing/ImageEffects.cs` is the only place new effects should land. Every public method takes `Mat` in and returns `Mat`; conversion to Avalonia `Bitmap` happens at the UI boundary in `Shared/Helpers/ImageHelper.cs`. **Never introduce `System.Drawing` or any Windows-only namespace into Core** — it must stay platform-agnostic.

The sticker output is a BGRA `Mat`. When passing an `MCvScalar` colour into a sticker operation, set the 4th channel to 255 explicitly — the default 0 is fully transparent and any drawn contour disappears.

Emgu 4.10+ renamed `ImreadModes.Color` → `ImreadModes.ColorBgr`. We're on 4.12 desktop; use `ColorBgr`.

## XAML conventions

- Use the semantic resources in `Styles/AppStyles.axaml` (`{DynamicResource AccentBrush}`, `{DynamicResource GlassBackgroundBrush}`, etc.) — do not hardcode hex colours in views.
- Visual philosophy: high-contrast professional dark UI, hairline borders, 10–14px corner radii, glass panels for hierarchical grouping. Cyan `#22D3EE` accent. Cards lift `-2px` on hover.
- **Card buttons** (any Button wrapping a rounded inner Border): use `Classes="card-button"`. It suppresses the default Button hover/pressed background that would otherwise leak as a hard rectangle behind the rounded card, and adds the hover lift.
- **`RadioButton.IsChecked`** defaults to TwoWay. If the binding goes through a converter with a working `ConvertBack` (e.g. `EnumToBoolConverter`), the converter fires and clobbers the bound property *before* any `Command` runs. Effect-selection buttons specifically need `Mode=OneWay` so the `SelectEffectCommand` is the sole writer.
- **Avalonia 11 quirks**: `LetterSpacing` exists on `TextBlock` but NOT on `Button` / `RadioButton`. Use `Watermark` on `TextBox` (v12's `PlaceholderText` doesn't exist).
- **Compiled-binding DataContext**: when wrapping a child view in a Panel with `IsVisible="{Binding ...}"` *and* a different inline `DataContext="{Binding ...}"`, put the `IsVisible` on a nesting Panel that keeps the parent DataContext — otherwise the compiler resolves `IsVisible` against the child's DataContext and fails.

## Package version pinning (do not bump blindly)

- **Avalonia 11.3.14** across all csproj. Avalonia 12.x dropped `net8.0-android` support; bumping breaks the Android build.
- **SkiaSharp 2.88.9** — matches the native lib Avalonia 11 bundles. SkiaSharp 3.x throws `InvalidOperationException: native libSkiaSharp library (88.1) is incompatible … [119.0, 120.0)` at runtime.
- **Emgu.CV 4.12** (managed) + **`Emgu.CV.runtime.mini.{ubuntu-x64,windows,macos}` 4.12** on desktop. The non-mini variants link against FFmpeg 4 (`libavcodec.so.58`) which isn't on Ubuntu 24+; the `mini.*` variants link against FFmpeg 6 (`libavcodec.so.60`) and drop heavy video codec deps.
- **Emgu.CV.runtime.maui.mini.android 4.9.0.5494** — Android stays on 4.9 because 4.10+ requires `net9.0-android35.0`.

## Native lib (Linux)

`VisionEditCV.Desktop.csproj` has a `MirrorEmguCvNativeToLinuxRid` `AfterTargets="Build"` target that copies `libcvextern.so` from `runtimes/ubuntu-x64/native/` → `runtimes/linux-x64/native/`. Without this, the .NET 8 RID graph resolves to `linux-x64` and can't find the native lib that NuGet placed under the legacy `ubuntu-x64` RID dir.

## Auto-update

Velopack is wired in `Program.cs`. Update checks only run for installed builds (installer/AppImage/pkg) — `dotnet run` shows "running from a non-installed build" in the Settings tab and is expected.

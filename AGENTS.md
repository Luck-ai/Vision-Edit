# AGENTS.md

Shared guidance for any AI coding agent working in this repository (Claude Code, Gemini, etc.). `CLAUDE.md` and `GEMINI.md` redirect here.

## Project overview

VisionEditCV is a cross-platform .NET 8 desktop app for AI-assisted image segmentation and region-based image editing. It originally shipped as a Windows Forms app and has been fully migrated to Avalonia 12 + SkiaSharp for Windows/Linux/macOS from a single codebase.

The app lets users load images, run AI segmentation (bounding-box or text prompts via a remote SAM3 REST API), manage masks, and apply effects (Color Grading, Artistic Style, Sticker Generation, Pixelation/Blur, Portrait DOF, Grayscale). Live debounced previews, undo, before/after compare, and Velopack auto-update are all wired in.

## Common commands

```bash
# Build everything (Core + Desktop)
dotnet build src/VisionEditCV.Desktop/VisionEditCV.Desktop.csproj

# Run the desktop app
dotnet run --project src/VisionEditCV.Desktop

# Package a release (uses Velopack)
./publish.sh 1.0.0 linux-x64           # linux-x64 | osx-x64 | osx-arm64
./publish.ps1 -Version 1.0.0           # win-x64 (Windows host only)
```

The Velopack Windows installer can only be produced from a Windows host. `publish.sh` handles Linux/macOS, `publish.ps1` handles Windows.

There is no test project — verify changes by running the Desktop app and exercising the affected flow in the UI.

## Architecture

Three projects in `src/`:

- **VisionEditCV.Core** — platform-agnostic. Contains `Api/Sam3Client.cs` (REST client for the SAM3 segmentation backend) and `Processing/ImageEffects.cs` (every effect, written against `Emgu.CV.Mat` + `MCvScalar`).
- **VisionEditCV.Desktop** — the current Avalonia 12 UI. Single-window MVVM. `Views/MainWindow.axaml` + `ViewModels/MainWindowViewModel.cs` hold essentially the whole app; `Controls/ImageCanvas.cs` is a custom Skia-backed render surface.
- **VisionEditCV.WinForms** — legacy Windows-only build, kept around as a reference for parity bugs. Do not extend it.

### MVVM and state model

`MainWindowViewModel` is a `CommunityToolkit.Mvvm` `[ObservableProperty]` view model. The image-editing state machine is non-obvious and worth internalising before changing it:

- `CurrentImage` — the loaded original. Never mutated after Open.
- `ProcessedImage` — what the canvas actually shows. May be a *live preview* (from a debounced background task) or a *committed* result (from Apply).
- `_history` is a `Stack<Bitmap?>` of **post-apply** states. `ApplyEffect` pushes `ProcessedImage` itself onto the stack, so `_history.Peek()` is "the latest committed state" and the same reference is also held by `ProcessedImage`. Because the references are shared, **do not `Dispose()` bitmaps that may be in `_history`** — clear the reference and let the GC reclaim them.
- `History` (the `ObservableCollection<string>` bound to the History tab) is a parallel list of effect names and must be kept in lockstep with `_history`.
- `_previewSourceSnapshot` is a `Mat` clone of the latest committed state, captured on every effect switch and after every Apply. The live-preview pipeline (`TriggerPreviewUpdate`) sources from this snapshot so slider tweaks never compound on top of a previous tick's output — this is critical for the Sticker effect, which crops/rotates/scales.

When you reset state (`OpenImage`, `ClearMasks`, `ResetAll`), do not call `.Clear()` on the `Masks` list — the canvas only invalidates its cached mask `SKImage`s when the `MasksProperty` *reference* changes. Reassign: `Masks = new List<float[,]>();`.

### Rendering

`Controls/ImageCanvas.cs` is a custom Avalonia control that owns a Skia draw operation (`ICustomDrawOperation`). It caches `SKImage`s for the original, processed, and per-mask overlays, and rebuilds them only when the corresponding `StyledProperty` reference changes — keep that in mind when wiring new bindings. Zoom-around-mouse and panning state live here too.

For pixel-level work in this control, use `SKBitmap.GetPixels()` directly and keep the draw operation allocation-free — this is the lever that keeps drawing zero-lag.

### Effects pipeline

`VisionEditCV.Core/Processing/ImageEffects.cs` is the only place new effects should land. Every public method takes `Mat` in and returns `Mat`; conversion to Avalonia `Bitmap` happens at the UI boundary in `VisionEditCV.Desktop/Helpers/ImageHelper.cs`. **Never introduce `System.Drawing` or any Windows-only namespace into Core** — it must stay platform-agnostic.

The sticker output is a BGRA `Mat`. When passing an `MCvScalar` colour into a sticker operation, set the 4th channel to 255 explicitly — the default 0 is fully transparent and any drawn contour disappears.

## XAML conventions

- Use the semantic resources defined in `Styles/AppStyles.axaml` (`{DynamicResource AccentBrush}`, `{DynamicResource GlassBackgroundBrush}`, etc.) — do not hardcode hex colours in views. The theme is Avalonia `FluentTheme` plus the project's custom luxury visual system.
- Visual philosophy: high-contrast professional aesthetic, hairline borders, generous 8–12px corner radii, glass panels for hierarchical grouping.
- `RadioButton.IsChecked` defaults to TwoWay. If the binding goes through a converter with a working `ConvertBack` (e.g. `EnumToBoolConverter`), the converter will fire and clobber the bound property *before* any `Command` runs. Effect-selection buttons specifically need `Mode=OneWay` so the `SelectEffectCommand` is the sole writer of `SelectedEffect`.

## Auto-update

Velopack is wired in `Program.cs`. Update checks only run for installed builds (installer/AppImage/pkg) — `dotnet run` shows "running from a non-installed build" in the Server tab and is expected.

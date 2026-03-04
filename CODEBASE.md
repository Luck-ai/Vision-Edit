# VisionEditCV — Codebase Documentation

## Table of Contents
1. [Project Overview](#project-overview)
2. [Architecture](#architecture)
3. [Data Flow](#data-flow)
4. [File Reference](#file-reference)
   - [Program.cs](#programcs)
   - [MainForm.cs](#mainformcs)
   - [Api/Sam3Client.cs](#apisam3clientcs)
   - [Controls/ImageCanvas.cs](#controlsimagecanvascs)
   - [Controls/ChipLabel.cs](#controlschiplabelcs)
   - [Controls/ChromeButtonPanel.cs](#controlschromebuttonpanelcs)
   - [Controls/ColorSwatch.cs](#controlscolorswatchcs)
   - [Controls/DarkButton.cs](#controlsdarkbuttoncs)
   - [Controls/DarkComboBox.cs](#controlsdarkcomboboxcs)
   - [Controls/GraphicsExtensions.cs](#controlsgraphicsextensionscs)
   - [Controls/MaskListPanel.cs](#controlsmasklistpanelcs)
   - [Controls/RoundedTextBox.cs](#controlsroundedtextboxcs)
   - [Controls/SliderControl.cs](#controlsslidercontrolcs)
   - [Models/SegmentationResult.cs](#modelssegmentationresultcs)
   - [Processing/ImageEffects.cs](#processingimageeffectscs)
5. [Key Design Patterns](#key-design-patterns)

---

## Project Overview

**VisionEditCV** is a **.NET 8 WinForms** desktop application for AI-assisted image segmentation and editing.

| Property | Value |
|---|---|
| Framework | .NET 8, WinForms |
| Image processing | Emgu.CV 4.9.0 (OpenCV C# wrapper) |
| JSON | Newtonsoft.Json 13.0.3 |
| AI backend | SAM3 (Segment Anything Model) REST API on Lightning AI cloud |
| UI style | Borderless, fully owner-drawn dark theme |

### Core Capabilities
- Load images via file dialog or drag-and-drop
- Draw bounding boxes or enter text prompts to segment objects via the SAM3 API
- Select individual segmentation masks
- Apply 6 image effects to selected mask regions:
  - **Color Grading** — tint, brightness, contrast
  - **Artistic Style** — stylization or pencil sketch (non-photorealistic rendering)
  - **Sticker Generation** — extract subject with border, shadow, scale, rotation, composite on background
  - **Pixelation & Blur** — pixelate or Gaussian blur the masked region
  - **Portrait Effect** — blur background while keeping subject sharp, feathered edge
  - **Grayscale** — convert masked region (or background) to black-and-white
- Live debounced preview of all effects before committing
- Chain multiple effects via "Apply" — each commit becomes the new base image
- "Show Before" restores the pristine on-disk original for comparison
- Save output as PNG or JPEG

---

## Architecture

```
VisionEditCV.exe
│
├── MainForm (single form, all app logic)
│   ├── Title bar panel          — custom chrome (minimize/maximize/close), applied-effects chips
│   ├── Top toolbar              — mode toggle (BBox / Prompt), prompt text box, Segment + Server buttons
│   ├── Left effects panel       — 6 effect buttons
│   ├── Center canvas            — ImageCanvas (double-buffered, letterboxed, zoom/pan)
│   ├── Effect sub-panel         — proportional flow layout of sliders/buttons/combos, Apply/Reset
│   └── Right mask panel         — collapsible MaskListPanel with one MaskCard per mask
│
├── Api/Sam3Client               — async HTTP client wrapping the SAM3 REST API
├── Controls/                    — all owner-drawn custom WinForms controls
├── Models/SegmentationResult    — data class for API response
└── Processing/ImageEffects      — all EmguCV image effect implementations
```

### Window Chrome
The form uses `FormBorderStyle.None` for a fully custom borderless window.
- Resize is handled by overriding `WndProc` and returning the correct `HITTEST` code when the cursor is within 8px of any edge or corner.
- The title bar is draggable via `SendMessage(WM_NCLBUTTONDOWN, HTCAPTION)`.
- Minimize / Maximize / Close are handled by `ChromeButtonPanel`.

---

## Data Flow

### 1. Image Load
```
User drops file / clicks open
  → ImageCanvas.LoadImage(path)
      → sets _originalBitmap   (used as working base, replaced on each Apply)
      → sets _fileOriginalBitmap (pristine copy, NEVER replaced)
      → clears masks, boxes, resets zoom
  → MainForm._currentImagePath = path
```

### 2. Segmentation
```
User draws bounding boxes (or types prompt) → clicks Segment
  → MainForm.RunSegmentation()
      → Sam3Client.SegmentWithBBoxAsync() or SegmentWithTextAsync()
          → POST /predict-bounding-box or /predict-image-text
          → ParseResponse() → SegmentationResult { Masks, Boxes, Scores }
      → ImageCanvas.SetMasks(result)
          → stores _masks (working copy)
          → stores _originalMasks (immutable backup for Reset)
          → assigns random colors per mask
      → MaskListPanel.Populate()
      → ImageCanvas.ClearBoxes()   ← hides bounding boxes after segmentation
      → Right panel shown
```

### 3. Effect Live Preview
```
User selects an effect / moves a slider / clicks a toggle
  → MainForm.TriggerLivePreview()
      → cancels previous CancellationTokenSource
      → snapshots selected masks + all control state (CaptureEffectArgs → EffectArgs record)
      → Task.Run (background thread):
          → Delay(30ms or 200ms for heavy effects)
          → acquires SemaphoreSlim(1,1) to serialise renders
          → if heavy effect: ScaleForPreview(src, 900px)  ← downscale
          → ApplyEffectArgs(effect, image, mask, args)
              → calls ImageEffects static method
          → Invoke() back to UI thread:
              → ImageCanvas.SetProcessedBitmap(result)
              → ImageCanvas.SetDisplayMaskOverride(transformedMasks) [Sticker only]
```

### 4. Effect Apply (Commit)
```
User clicks Apply
  → MainForm.ApplyCurrentEffect()
      → cancels any in-flight preview
      → snapshots args with resolution-scale factors:
          - Artistic: ArtSigmaSScale = fullRes / 900
          - Portrait: PortraitScale  = fullRes / 900
          - PixelBlur: PbScale       = fullRes / 900
      → Task.Run: applies effect at full resolution
      → ImageCanvas.CommitProcessedAsOriginal(result)
          → replaces _originalBitmap (NOT _fileOriginalBitmap)
      → [Sticker only] ImageCanvas.ReplaceMasks(transformedMasks)
          → bakes scale+rotation into stored mask data
      → _appliedEffects list updated, chips rebuilt in title bar
      → DeactivateEffect()  ← clears active effect, clears preview
```

### 5. Show Before / Reset
```
"Show Before" button (toggle)
  → ImageCanvas.ShowOriginal(true)
      → _showingOriginal = true
      → RefreshDisplay() uses _fileOriginalBitmap

"Reset All Effects" button
  → MainForm.ResetAllEffects()
      → ImageCanvas.RestoreOriginalFromFile(_currentImagePath)
          → reloads from disk → replaces _originalBitmap
      → ImageCanvas.RestoreOriginalMasks()
          → copies _originalMasks back to _masks
      → clears _appliedEffects, rebuilds chips
```

### 6. Save
```
User clicks Save
  → MainForm.SaveImage()
      → clone of _originalBitmap (already contains all committed effects)
      → if an effect is active and masks are selected: apply it inline
      → SaveFileDialog → Bitmap.Save(path, ImageFormat.Png/Jpeg)
```

---

## File Reference

---

### Program.cs
**Path:** `VisionEditCV/Program.cs`  
**Lines:** 13

Entry point. Sets up WinForms application and runs `MainForm`.

```csharp
[STAThread]
static void Main()
{
    ApplicationConfiguration.Initialize();
    Application.EnableVisualStyles();
    Application.SetCompatibleTextRenderingDefault(false);
    Application.Run(new MainForm());
}
```

---

### MainForm.cs
**Path:** `VisionEditCV/MainForm.cs`  
**Lines:** 1728  
**Namespace:** `VisionEditCV`

The single application form. Contains all app logic. The designer-generated layout lives in `MainForm.Designer.cs`.

#### Theme Constants (static readonly Color)
| Field | Value | Purpose |
|---|---|---|
| `_BgMain` | `rgb(18,18,18)` | Darkest background (form background) |
| `_BgPanel` | `rgb(24,24,24)` | Panel backgrounds |
| `_BgButton` | `rgb(32,34,38)` | Default button background |
| `_Cyan` | `rgb(0,229,255)` | Accent colour — active states, borders |
| `_TextMain` | `rgb(220,220,220)` | Primary text |
| `_TextDim` | `rgb(140,140,160)` | Dimmed / secondary text |
| `_BorderColor` | `rgb(45,45,55)` | Form border |

#### Private Fields
| Field | Type | Purpose |
|---|---|---|
| `_client` | `Sam3Client` | API HTTP client |
| `_lastResult` | `SegmentationResult?` | Most recent segmentation response |
| `_currentImagePath` | `string?` | Path to the currently loaded image file |
| `_healthCts` | `CancellationTokenSource?` | Cancels the server health-poll loop |
| `_previewCts` | `CancellationTokenSource?` | Cancels in-flight live preview task |
| `_previewSem` | `SemaphoreSlim(1,1)` | Serialises concurrent preview renders |
| `_activeEffect` | `string` | Name of the currently active effect (`""` = none) |
| `_comparingOriginal` | `bool` | Whether "Show Before" is active |
| `_pixelateMode` | `bool` | PixelBlur: true = pixelate, false = blur |
| `_artStylizeMode` | `bool` | Artistic: true = stylize, false = pencil sketch |
| `_pbTargetBg` | `bool` | PixelBlur: true = apply to background |
| `_gsTargetBgMode` | `bool` | Grayscale: true = apply to background |
| `_cgTargetBgMode` | `bool` | Color Grading: true = apply to background |
| `_stickerBgMode` | `string` | `"Original"` / `"Solid"` / `"Image"` / `"Transparent"` |
| `_stickerCustomBg` | `Bitmap?` | User-uploaded background image for sticker |
| `_preEffectSnapshot` | `Bitmap?` | Pre-sticker-apply snapshot — allows re-applying sticker during live preview without chaining |
| `_appliedEffects` | `List<string>` | Display names of committed effects (used to build title-bar chips) |
| `_rightPanelExpanded` | `bool` | Collapsed/expanded state of the right mask panel |

#### WndProc Override
```
WndProc(ref Message m)
```
Intercepts `WM_NCHITTEST` (0x84). When the default result is `HTCLIENT`, checks if the cursor is within 8px of any edge or corner and returns the appropriate resize hit-test code (`HTLEFT`, `HTBOTTOMRIGHT`, etc.), enabling native OS borderless resize.

#### Constructor & Initialization

**`MainForm()`**  
Calls `InitializeComponent()` then `PostInit()`.

**`PostInit()`**  
Runs after designer setup:
- Sets `FormBorderStyle.None`, enables double buffering
- Populates server URL from `_client.BaseUrl`
- Calls `WireEvents()`, `WireResizeHandlers()`, `WireWindowTitleBar()`
- Paints a 1px border around the form in `_BorderColor`
- Runs initial layout passes: `TopBarResize`, `EffectSubPanelResize`
- Sets initial canvas mode to `BoundingBox`
- Fires `StartServerAsync()` on startup (auto-connect)

**`WireWindowTitleBar()`**  
- Wires `ChromeButtonPanel` events: `CloseClicked` → `Application.Exit()`, `MaximizeClicked` → toggle maximize, `MinimizeClicked` → minimize
- Wires title bar drag via P/Invoke `ReleaseCapture` + `SendMessage(WM_NCLBUTTONDOWN, HTCAPTION)`
- Wires double-click to toggle maximize

**`WireEvents()`**  
Wires all button click handlers, slider `ValueChanged`, canvas events, combo box changes.
- All slider `ValueChanged` events route through `WireSlider(sc)` → `TriggerLivePreview()`
- Effect buttons → `ActivateEffect(name)`
- `_btnApplyEffect` → `ApplyCurrentEffect()`
- `_btnResetEffect` → `ResetCurrentEffect()`
- `_btnSegment` → `RunSegmentation()`
- `_btnCompare` → toggle `_canvas.ShowOriginal(bool)`
- `_canvas.MaskSelectionChanged` → sync `_maskList` row + `TriggerLivePreview()`
- `_maskList.MaskSelectionChanged` → sync canvas mask + `TriggerLivePreview()`

**`WireResizeHandlers()`**  
- Re-runs `EffectSubPanelResize`, `TopBarResize`, `TitleBarChipsResize` on form `Resize`
- After `Shown`: deferred `BeginInvoke` for final layout pass

**`WireSlider(SliderControl sc)`**  
Wires `sc.ValueChanged` → `TriggerLivePreview()`.

---

#### Logic — Image / Canvas

**`OpenImageFile()`**  
Shows `OpenFileDialog`, calls `_canvas.LoadImage(path)`, resets all effect state, calls `OnImageLoaded()`.

**`ClearAllMasks()`**  
Clears `_maskList`, `_canvas.ClearMasks()`, hides the right panel.

**`ToggleRightPanel()`**  
Toggles `_rightPanelExpanded`. Sets panel width to `360` (full) or `26` (collapsed). Updates toggle button text.

**`RightPanelResize(object sender, EventArgs e)`**  
Positions the `_btnToggleRight` strip to always fill the left edge of the right panel at full height.

**`OnImageLoaded()`**  
Sets canvas mode to `BoundingBox`.

**`SetCanvasMode(CanvasMode mode)`**  
Updates `_canvas.Mode`, toggles active highlight on `_btnBBox` / `_btnPrompt`, shows/hides `_promptBox`.

---

#### Logic — Segmentation

**`RunSegmentation()` (async)**  
1. Validates image and input (boxes or prompt)
2. Calls `_client.SegmentWithBBoxAsync()` or `_client.SegmentWithTextAsync()`
3. On success: `_canvas.SetMasks(result)`, `_canvas.ClearBoxes()`, `_maskList.Populate()`, shows right panel
4. On failure: shows `MessageBox`

---

#### Logic — Effect Activation

**`SetArtMode(bool stylize)`**  
Sets `_artStylizeMode`, updates button highlight, shows/hides sigmaR/shade groups, triggers preview.

**`SetStickerBgMode(string mode)`**  
Sets `_stickerBgMode`, syncs dropdown, shows/hides `_stBgColorSwatch` or `_btnStickerUploadBg`, triggers preview.

**`SetCgMode(bool targetBg)`**  
Sets `_cgTargetBgMode`, updates FG/BG button highlight, triggers preview.

**`SetGsMode(bool targetBg)`**  
Sets `_gsTargetBgMode`, updates FG/BG button highlight, triggers preview.

**`DeactivateEffect()`**  
- Cancels any in-flight preview
- Clears `_activeEffect`, `_preEffectSnapshot`
- Resets all effect button highlight states to default
- Hides all effect panels, Apply/Reset buttons, effect sub-panel
- Clears the canvas processed bitmap and display mask override

**`ActivateEffect(string effect)`**  
- Guards: no image loaded → warning
- Clicking already-active effect → `DeactivateEffect()` (toggle)
- Sets `_activeEffect`, highlights the active button (dark teal + cyan border)
- Shows the corresponding parameter panel
- Shows Apply/Reset buttons and the effect sub-panel
- Runs `EffectSubPanelResize` (deferred second pass via `BeginInvoke`)
- Calls `TriggerLivePreview()`

---

#### Logic — Apply Effect

**`ResetCurrentEffect()`**  
Resets all sliders and toggles for the active effect to their defaults, triggers preview.

**`ApplyCurrentEffect()` (async void)**  
1. Cancels any in-flight preview
2. Collects selected masks
3. Snapshots args via `CaptureEffectArgs()`
4. Adjusts resolution scale factors for Artistic / Portrait / PixelBlur if image > 900px
5. `Task.Run`: applies effect at full resolution across all selected masks
6. On UI thread: saves pre-commit snapshot (Sticker), calls `_canvas.CommitProcessedAsOriginal(result)`
7. Sticker: bakes transform into stored masks via `_canvas.ReplaceMasks()`
8. Adds display name to `_appliedEffects`, rebuilds title-bar chips
9. Calls `DeactivateEffect()`

---

#### Logic — Async Debounced Preview

**Constants**
| Constant | Value | Purpose |
|---|---|---|
| `_heavyEffects` | `{"Artistic","Sticker","Portrait","PixelBlur"}` | These effects downscale to ≤900px for preview |
| `PreviewMaxDim` | `900` | Maximum preview longest side in pixels |
| `DebounceMs` | `30` | Debounce delay for light effects |
| `DebounceHeavyMs` | `200` | Debounce delay for heavy effects |

**`TriggerLivePreview()`**  
- Returns immediately if no active effect or no image
- Cancels previous `_previewCts`, creates a new one
- Collects selected masks and snapshots args on the UI thread
- Fires a `Task.Run`:
  1. Waits `DebounceMs` / `DebounceHeavyMs` (cancellable)
  2. Acquires `_previewSem` (serialises renders)
  3. For heavy effects: `ScaleForPreview(src, 900)` — downscales
  4. Sticker: adjusts `StThicknessScale` proportionally for preview resolution
  5. Calls `ApplyEffectArgs()` across selected masks
  6. Sticker: computes `TransformMaskForDisplay()` at `previewMaxDim=256` for overlay sync
  7. `Invoke()` back to UI: `SetProcessedBitmap`, `SetDisplayMaskOverride`

**`ScaleForPreview(Bitmap src, int maxDim) → Bitmap`**  
Returns a copy scaled so the longest side is ≤ `maxDim`. Returns the original if already within bounds. Uses bilinear interpolation.

---

#### Logic — Effect Parameter Snapshot

**`record EffectArgs(...)`**  
Immutable snapshot of all effect parameter values captured at the moment preview/apply is triggered. Using a `record` with `with`-expressions allows non-destructive modification for resolution scaling.

| Parameter | Source | Notes |
|---|---|---|
| `TintColor` | `_cgTintSwatch.SelectedColor` | Color grading tint colour |
| `TintStrength` | `_cgTintStrength.Value / 100f` | 0..1 |
| `Brightness` | `_cgBrightness.Value` | -100..100 |
| `Contrast` | `_cgContrast.Value / 10f` | 0.1..3.0 |
| `CgTargetBg` | `_cgTargetBgMode` | Apply CG to background instead |
| `ArtStylizeMode` | `_artStylizeMode` | true=stylize, false=pencil |
| `ArtSigmaS` | `_artSigmaS.Value` | 1..200, spatial filter extent |
| `ArtSigmaR` | `_artSigmaR.Value / 100f` | 0.01..1.0, color range |
| `ArtShade` | `_artShade.Value / 1000f` | Pencil shade factor |
| `ArtSigmaSScale` | `1f` (or fullRes/900 on Apply) | Normalises sigmaS for full-res |
| `StScale` | `_stScale.Value` | 1..20, sticker scale (÷10 = factor) |
| `StRotation` | `_stRotation.Value` | -180..180 degrees |
| `StBorderColor` | `_stBorderColor.SelectedColor` | Sticker contour colour |
| `StThickness` | `_stThickness.Value` | Contour thickness (px) |
| `StShadowBlur` | `_stShadowBlur.Value` | Drop shadow blur kernel |
| `StThicknessScale` | `1f` (or preview/full ratio) | Scales thickness for preview |
| `StickerOriginalBg` | `_stickerBgMode=="Original"` | Keep original background |
| `StickerSolidBg` | `_stickerBgMode=="Solid"` | Solid colour background |
| `StickerSolidColor` | `_stBgColorSwatch.SelectedColor` | Solid background colour |
| `StickerImageBg` | clone of `_stickerCustomBg` | Uploaded background bitmap |
| `StickerTransparentBg` | `_stickerBgMode=="Transparent"` | Transparent/BGRA output |
| `PixelateMode` | `_pixelateMode` | true=pixelate, false=blur |
| `PbTargetBg` | `_pbTargetBg` | Apply to background |
| `PbIntensity` | `_pbIntensity.Value` | 2..100 |
| `PbScale` | `1f` (or fullRes/900 on Apply) | Normalises kernel for full-res |
| `PortraitBlur` | `_ptBlurStrength.Value` | Blur kernel size |
| `PortraitFeather` | `_ptFeatherAmount.Value` | Feather kernel size |
| `PortraitScale` | `1f` (or fullRes/900 on Apply) | Normalises both for full-res |
| `GsTargetBg` | `_gsTargetBgMode` | Apply grayscale to background |

**`CaptureEffectArgs() → EffectArgs`**  
Constructs and returns an `EffectArgs` record from current UI state. Must be called on the UI thread.

---

#### Logic — Effect Application

**`ApplyEffectArgs(string effect, Bitmap image, float[,] mask, EffectArgs a) → Bitmap?`**  
Switch dispatch to the correct `ImageEffects` static method based on `effect` name.

| Effect | Calls |
|---|---|
| `"ColorGrading"` | Inverts mask if `CgTargetBg`, then `ImageEffects.ColorGrading(...)` |
| `"Artistic"` (stylize) | `ImageEffects.StylizeMasked(...)` with scaled sigmaS |
| `"Artistic"` (pencil) | `ImageEffects.PencilSketchMasked(...)` with scaled sigmaS |
| `"Sticker"` | `ApplyStickerEffect(...)` |
| `"PixelBlur"` (pixelate) | `ImageEffects.PixelateMasked(...)` with scaled pixelSize |
| `"PixelBlur"` (blur) | `ImageEffects.BlurMasked(...)` with scaled kernelSize |
| `"Portrait"` | `ImageEffects.PortraitEffect(...)` with scaled blur/feather |
| `"Grayscale"` | Inverts mask if `GsTargetBg`, then `ImageEffects.ColorGrading(..., blackAndWhite: true)` |

**`ApplyStickerEffect(Bitmap image, float[,] mask, EffectArgs a) → Bitmap?`**  
1. Calls `ImageEffects.ExtractSticker()` to get BGRA sticker with border + shadow + transform
2. If `StickerTransparentBg`: returns sticker directly
3. Otherwise builds background: original clone / solid colour / resized uploaded image
4. Calls `ImageEffects.CompositeSticker(sticker, bg)` and returns result

**`ApplyPixelBlur(Bitmap image, float[,] mask, EffectArgs a) → Bitmap?`**  
Optionally inverts mask for background targeting. Routes to `PixelateMasked` or `BlurMasked` with intensity scaled by `PbScale`.

**`InvertMask(float[,] mask) → float[,]`**  
Returns a new mask where every value is `1 - original`. Used for "target background" modes in `ColorGrading`, `Grayscale`, `PixelateMasked`, and `BlurMasked` — all effects handle bg/fg targeting by inverting the mask at the call site rather than branching inside the effect function.

---

#### Logic — Applied Effects Tracking

**`GetEffectDisplayName(string effect) → string`**  
Maps internal effect name (e.g. `"ColorGrading"`) to display label (e.g. `"Color Grading"`).

**`RebuildAppliedEffectsChips()`**  
Disposes and recreates all `ChipLabel` controls in `_appliedEffectsPanel`. Hides panel and Reset All button when list is empty. Uses `BeginInvoke` to defer layout until after the message pump so chips appear correctly.

**`ResetAllEffects()`**  
- Cancels preview
- Clears `_appliedEffects`
- Calls `_canvas.RestoreOriginalFromFile(_currentImagePath)` — reloads from disk
- Calls `_canvas.RestoreOriginalMasks()` — restores mask positions
- Rebuilds (empty) chips
- Triggers fresh preview

---

#### Logic — Save

**`SaveImage()`**  
Shows `SaveFileDialog`. Clones `_canvas.OriginalBitmap`. If an effect is active and masks are selected, applies the effect inline before saving. Saves as PNG or JPEG.

---

#### Logic — Server

**`StartServerAsync()` (async Task)**  
- Cancels any existing health poll
- Updates `_client.BaseUrl` from `_txtServerUrl`
- Calls `_client.WaitForHealthAsync()` which polls `GET /health`
- Updates button colour and text: yellow (connecting) → green (connected) → grey (disconnected)

**`SetStartServerButton(bool connecting, bool online)`**  
Updates `_btnStartServer` text and colour. Calls `TopBarResize` so button resizes to fit new text.

**`SetServerStatus(string message, Color color)`**  
Updates `_lblServerStatus` text and colour. Thread-safe via `InvokeRequired`.

**`SetLoading(bool loading, string message)`**  
Shows/hides the `_loadingOverlay` panel and `_loadingLabel`. Enables/disables `_btnSegment`. Thread-safe.

---

#### Layout Event Handlers

**`EffectSubPanelResize(object sender, EventArgs e)`**  
The most complex layout method (~180 lines). Positions all controls in the effect sub-panel proportionally:
- Apply/Reset buttons: fixed right column (96px), vertically centred
- Each flow panel (`_cgFlow`, `_artFlow`, etc.) fills remaining width
- Groups are measured by their "natural" width (combo: text width + arrow; buttons: grid)
- Surplus width is distributed evenly across non-combo groups
- Each group's content (label + slider/button/combo/swatch) is vertically centred

**`TitleBarChipsResize(object? sender, EventArgs e)`**  
Positions the applied-effects chip panel and Reset All button in the title bar between the title label (left) and chrome buttons (right).

**`TopBarResize(object sender, EventArgs e)`**  
Sizes and positions all controls in the top toolbar (mode buttons, prompt box, Segment/Connect buttons). The prompt box stretches between the two button groups.

**`TitleBarPaint(object sender, PaintEventArgs e)`**  
Draws a 1px cyan accent line at the bottom of the title bar.

**`EffectSubPanelPaint(object sender, PaintEventArgs e)`**  
Draws a 1px separator line at the top of the effect sub-panel.

**`PaintCenterBorder(object sender, PaintEventArgs e)`**  
Draws a dashed cyan rounded-rectangle border around the canvas panel (visible before an image is loaded).

---

### Api/Sam3Client.cs
**Path:** `VisionEditCV/Api/Sam3Client.cs`  
**Lines:** 263  
**Namespace:** `VisionEditCV.Api`

Async HTTP client for the SAM3 REST API.

#### Class: `Sam3Client`

**Properties**
| Property | Default | Purpose |
|---|---|---|
| `BaseUrl` | `"https://8000-dep-01..."` | SAM3 server base URL (editable in UI) |

**Static Fields**
| Field | Value | Purpose |
|---|---|---|
| `_http` | `HttpClient` timeout 10 min | Used for prediction POST calls |
| `_healthHttp` | `HttpClient` timeout 15 s | Used for health poll GET calls |
| `_debugLogPath` | Desktop/sam3_debug.txt | Debug log file path |

**Private Helpers**

`EncodeImageToBase64(string imagePath) → string`  
Reads the image file as bytes and returns a base-64 string.

`ParseResponse(string json) → SegmentationResult`  
Parses the JSON API response. Handles both `(H, W)` and `(1, H, W)` mask shapes (FastSAM can return either). Fills `SegmentationResult.Masks`, `Boxes`, `Scores`.

`NormalizedUrl() → string`  
Returns `BaseUrl` with trailing slash stripped.

`DebugLog(string text)`  
Appends text to the desktop debug log file. Silently ignores errors.

`PostAsync(string endpoint, object payload) → Task<SegmentationResult?>`  
Serialises payload to JSON, POSTs to `{BaseUrl}{endpoint}`, logs request/response to debug file. Throws `InvalidOperationException` on network error, timeout, or non-2xx status.

**Public Methods**

`WaitForHealthAsync(IProgress<string> onStatus, CancellationToken ct) → Task<bool>`  
Polls `GET /health` in a loop until the server responds 200 or `ct` is cancelled. Each attempt has its own 15-second timeout linked to `ct` so a user cancel is always fast. Returns `true` when healthy, `false` if cancelled.

`SegmentWithTextAsync(string imagePath, string prompt) → Task<SegmentationResult?>`  
POSTs `{ image: base64, prompt: string }` to `/predict-image-text`.

`SegmentWithBBoxAsync(string imagePath, float[][] boxes, bool[] labels) → Task<SegmentationResult?>`  
POSTs `{ image: base64, boxes: [[x,y,w,h],...], labels: [bool,...] }` to `/predict-bounding-box`.

---

### Controls/ImageCanvas.cs
**Path:** `VisionEditCV/Controls/ImageCanvas.cs`  
**Lines:** 840  
**Namespace:** `VisionEditCV.Controls`

The central image display and interaction control. Double-buffered, owner-drawn.

#### Enums

`CanvasMode { None, BoundingBox, Prompt }`  
Controls which interaction gestures are active.

#### Class: `BBoxEntry`
Stores one bounding box and its foreground/background label.

| Property | Type | Purpose |
|---|---|---|
| `Rect` | `RectangleF` | Box in image-space coordinates |
| `Label` | `bool` | `true` = foreground, `false` = background |

#### Class: `MaskSelectedEventArgs : EventArgs`
| Property | Type |
|---|---|
| `MaskIndex` | `int` |
| `Selected` | `bool` |

#### Class: `ImageCanvas : Control`

**Private Fields**
| Field | Purpose |
|---|---|
| `_originalBitmap` | Working base bitmap; replaced on each effect commit |
| `_fileOriginalBitmap` | Pristine on-disk copy; NEVER replaced by effects; used by ShowOriginal |
| `_processedBitmap` | Current live preview result |
| `_displayBitmap` | Composed output (processed + mask overlays) rendered to screen |
| `_showingOriginal` | When true, display uses `_fileOriginalBitmap` |
| `_masks` | Working mask list (can be transformed by Sticker apply) |
| `_originalMasks` | Immutable backup set at segmentation time; restored by ResetAllEffects |
| `_maskColors` | Random colour per mask |
| `_maskSelected` | Selection state per mask |
| `_maskScores` | Confidence score per mask |
| `_displayMasksOverride` | Optional override mask list used only for overlay rendering (Sticker live preview) |
| `_zoom` / `_panOffset` | Zoom factor and pan translation |
| `_boxes` | List of drawn bounding boxes |
| `_selectedBox` | Index of currently selected box (-1 = none) |

**Events**
| Event | Args | When fired |
|---|---|---|
| `MaskSelectionChanged` | `MaskSelectedEventArgs` | User clicks on a mask |
| `BBoxChanged` | `RectangleF` | Box drawn, moved, or resized |
| `ImageDropped` | `EventArgs` | File dropped or empty canvas clicked |

**Public Accessors**
| Property | Returns |
|---|---|
| `OriginalBitmap` | `_originalBitmap` |
| `BBoxEntries` | `IReadOnlyList<BBoxEntry>` — all boxes in image space |
| `BBoxInImageSpace` | `RectangleF` — first box (legacy single-box API) |
| `MaskSelected` | `IReadOnlyList<bool>` |
| `MaskColors` | `List<Color>` |
| `Masks` | `List<float[,]>` |
| `MaskScores` | `List<float>` |

**Constructor**  
Sets `OptimizedDoubleBuffer`, `AllPaintingInWmPaint`, `UserPaint`, `ResizeRedraw`. Registers drag-and-drop handlers.

**Public API Methods**

`LoadImage(string path)`  
Disposes all bitmaps. Loads `_originalBitmap` and `_fileOriginalBitmap` from the same path. Clears masks and boxes. Resets zoom.

`RestoreOriginalFromFile(string path)`  
Reloads `_originalBitmap` from disk without touching `_fileOriginalBitmap`, masks, boxes, or zoom. Used by "Reset All Effects".

`SetProcessedBitmap(Bitmap? bmp)`  
Replaces `_processedBitmap` and calls `RefreshDisplay()`.

`CommitProcessedAsOriginal(Bitmap committed)`  
Replaces `_originalBitmap` with a clone of `committed`. Clears `_processedBitmap`. Does NOT touch `_fileOriginalBitmap`.

`ShowOriginal(bool show)`  
Sets `_showingOriginal` and calls `RefreshDisplay()`.

`SetMasks(SegmentationResult result)`  
Clears existing masks. For each mask in the result: stores in `_masks`, stores a deep copy in `_originalMasks`, assigns random colour, initialises selection to `false`.

`ReplaceMasks(List<float[,]> newMasks)`  
Overwrites `_masks` in-place (count must match). Clears `_displayMasksOverride`. Used to bake Sticker transform on Apply.

`SetMaskSelected(int index, bool selected)`  
Sets selection state for a single mask index.

`SetDisplayMaskOverride(List<float[,]>? overrideMasks)`  
When non-null, `RefreshDisplay` uses this list instead of `_masks` for overlay rendering. Used by Sticker live preview to show the transformed mask contour.

`ClearMasks()`  
Clears all mask data, disposes `_processedBitmap` and `_displayBitmap`.

`RestoreOriginalMasks()`  
Deep-copies `_originalMasks` back to `_masks`. Clears `_displayMasksOverride`.

`ClearBoxes()`  
Clears `_boxes` and resets `_selectedBox`.

`ResetZoom()`  
Sets `_zoom = 1.0f` and `_panOffset = PointF.Empty`.

**Private Methods**

`RefreshDisplay()`  
Composes `_displayBitmap`:
- Base image: `_fileOriginalBitmap` (if `_showingOriginal`), else `_processedBitmap ?? _originalBitmap`
- If masks present: calls `ImageEffects.RenderMaskOverlays()` with `_displayMasksOverride ?? _masks`
- Otherwise: clones the base image

`OnPaint(PaintEventArgs e)`  
Clears to `PanelBg`. If no image: draws placeholder text. Otherwise letterboxes the image and draws all boxes (in `BoundingBox` mode).

`DrawPlaceholder(Graphics g)`  
Draws a dashed cyan rectangle border and centred "Drag & Drop or click to upload" hint text.

`DrawBox(Graphics g, Rectangle imgRect, BBoxEntry entry, bool selected)`  
Draws a single bounding box. Foreground boxes are cyan, background boxes are red/orange. Selected box has thicker lines, 8 resize handles, and an FG/BG label badge.

`GetLetterboxRect(int imgW, int imgH) → Rectangle`  
Computes the destination rectangle that centers and scales the image to fit the control, respecting aspect ratio, zoom, and pan offset.

`ScreenToImage(PointF screen, Rectangle imgRect) → PointF`  
Converts a screen-space point to image-space coordinates.

`ImageToScreen(RectangleF imgRect, Rectangle destRect) → RectangleF`  
Converts an image-space rectangle to screen-space.

`GetHandlePoints(RectangleF r) → PointF[]`  
Returns the 8 resize handle positions (TL, TM, TR, MR, BR, BM, BL, ML) for a screen-space rectangle.

`HitTestHandle(PointF screen, Rectangle imgRect) → int`  
Returns handle index (0–7) if the point is within 8px of any handle on the selected box, else -1.

`HitTestBox(PointF screen, Rectangle imgRect) → int`  
Returns the topmost box index whose screen rect contains the point, else -1.

`HitTestMask(PointF imgPt) → int`  
Returns the topmost mask index whose binary pixel at the image-space point is > 0.5, else -1.

`UpdateRectForHandle(RectangleF r, int handle, PointF imgPt) → RectangleF`  
Returns a new rectangle with the appropriate edges moved to `imgPt` based on which handle (0–7) is being dragged.

`ClampToImage(RectangleF rect) → RectangleF`  
Clamps a rectangle to `[0, imageWidth] x [0, imageHeight]`.

`GetCursorForHandle(int handle) → Cursor`  
Returns the appropriate resize cursor for each of the 8 handle positions.

**Mouse / Keyboard Interaction**
- **Mouse wheel**: zoom in/out (0.5x – 10x), keeping the cursor point stationary
- **Middle-click drag**: pan
- **Left-click drag on empty area** (BBox mode): draw new box
- **Left-click on handle**: resize selected box
- **Left-click on box body**: move box
- **Right-click on box**: toggle FG/BG label
- **Delete/Backspace**: delete selected box
- **Left-click on mask pixel**: toggle mask selection
- **Ctrl+0**: reset zoom/pan

---

### Controls/ChipLabel.cs
**Path:** `VisionEditCV/Controls/ChipLabel.cs`  
**Lines:** 78  
**Namespace:** `VisionEditCV.Controls`

A lightweight owner-drawn label that renders as a rounded pill chip. Used in the title bar to show applied effect names.

#### Class: `ChipLabel : Control`

| Property | Default | Purpose |
|---|---|---|
| `CornerRadius` | `10` | Pill corner radius |

**Colours (static)**
| Constant | Value |
|---|---|
| `DefaultPillBg` | `rgb(14,48,52)` |
| `DefaultPillBorder` | `rgb(0,140,160)` |
| `DefaultText` | `rgb(0,229,255)` |

`OnPaint(PaintEventArgs e)`  
Fills rounded rectangle with `DefaultPillBg`, draws 1px `DefaultPillBorder` outline, renders centred text with ellipsis trimming.

`RoundedRect(RectangleF r, int radius) → GraphicsPath`  
Private helper that builds a 4-arc rounded rectangle path.

---

### Controls/ChromeButtonPanel.cs
**Path:** `VisionEditCV/Controls/ChromeButtonPanel.cs`  
**Lines:** 182  
**Namespace:** `VisionEditCV.Controls`

A single owner-drawn control that renders the three window-chrome buttons (─ 🗖 ✕) as one unified pill shape. No child controls, no layering issues.

#### Class: `ChromeButtonPanel : Control`

**Events**
| Event | Fired when |
|---|---|
| `MinimizeClicked` | Minimize zone clicked |
| `MaximizeClicked` | Maximize zone clicked |
| `CloseClicked` | Close zone clicked |

**Private Enum: `Zone { None, Min, Max, Close }`**  
Tracks which third of the control the cursor is in.

**Hit testing**  
`HitZone(Point p) → Zone`: Divides control width by 3 to determine which button zone the point falls in.

**`OnPaint(PaintEventArgs e)`**  
1. Clears to title-bar background colour (makes control edges invisible)
2. Clips to the pill shape
3. Paints each slot background (hover = lighter, press = darker, Close hover = red)
4. Draws Unicode icons centred in each slot
5. Draws the bottom cyan accent line (continues the title bar line seamlessly)

`PaintSlot(Graphics g, RectangleF rect, Zone zone)`  
Fills a slot with its hover/press background colour.

---

### Controls/ColorSwatch.cs
**Path:** `VisionEditCV/Controls/ColorSwatch.cs`  
**Lines:** 52  
**Namespace:** `VisionEditCV`

A coloured rectangle that opens a `ColorDialog` when clicked.

#### Class: `ColorSwatch : Control`

| Property | Purpose |
|---|---|
| `SelectedColor` | The currently selected colour. Setting it invalidates the control. |

**Events**
| Event | Fired when |
|---|---|
| `ColorChanged` | User picks a new colour via the dialog |

`OnPaint`: fills the control with `SelectedColor`, draws a semi-transparent white border.  
`OnClick`: opens `ColorDialog`, updates `_color`, fires `ColorChanged`.

---

### Controls/DarkButton.cs
**Path:** `VisionEditCV/Controls/DarkButton.cs`  
**Lines:** 293  
**Namespace:** `VisionEditCV`

A `Button` subclass with hover/press colour animation, rounded corners, and optional leading icon. Fully owner-drawn.

#### Class: `DarkButton : Button`

**Properties**
| Property | Default | Purpose |
|---|---|---|
| `HoverBackColor` | `null` | Custom hover background; if null uses `DefaultHoverBg` |
| `CornerRadius` | `8` | Corner radius for standard buttons |
| `RoundedLeft` | `false` | Round only left corners (for pill chrome buttons) |
| `RoundedRight` | `false` | Round only right corners |
| `HoverCornerRadius` | `7` | Radius used when `RoundedLeft`/`RoundedRight` |
| `PillVInset` | `0` | Vertical inset to match pill shape in title bar |
| `PillCenter` | `false` | Middle chrome button — flat fill, no corner rounding |
| `Icon` | `null` | Optional emoji/Unicode icon drawn left of text |

**`OnPaint(PaintEventArgs e)`**  
- Resolves effective background: base → hover (lighten) → press (darken)
- If `RoundedLeft`/`RoundedRight`: clears to parent background first, then draws selective-round pill fill
- Otherwise: draws standard rounded rect fill + optional border
- If `Icon` set: draws icon in a 36px left column, text starts after
- Otherwise: draws centred text, honouring `TextAlign`

`RoundedRect(RectangleF, int) → GraphicsPath`  
Standard 4-arc rounded rectangle.

`RoundedRectSelective(RectangleF, int, bool roundLeft, bool roundRight) → GraphicsPath`  
Builds a path that only rounds the specified sides — used so chrome button hover highlights round only the outer pill edge.

---

### Controls/DarkComboBox.cs
**Path:** `VisionEditCV/Controls/DarkComboBox.cs`  
**Lines:** 196  
**Namespace:** `VisionEditCV`

A fully owner-drawn `ComboBox` styled to the dark theme. `DropDownList` mode only.

#### Class: `DarkComboBox : ComboBox`

| Property | Default |
|---|---|
| `CornerRadius` | `8` |

**Colours**
| Constant | Purpose |
|---|---|
| `BgNormal` | Default background |
| `BgHover` | Hover background |
| `BgOpen` | Open/dropdown background |
| `Cyan` | Accent border and selected item highlight |
| `BorderNormal` | Default border |

**`OnPaint`**: draws rounded background, border (cyan when open/hovered), arrow separator line, chevron arrow, selected item text.

**`DrawArrow(Graphics, cx, cy, open, color)`**: draws a 5-point chevron pointing up (open) or down (closed).

**`OnDrawItem`**: draws each dropdown item with dark background, cyan left accent bar when selected, and adjusted text colour.

---

### Controls/GraphicsExtensions.cs
**Path:** `VisionEditCV/Controls/GraphicsExtensions.cs`  
**Lines:** 36  
**Namespace:** `VisionEditCV`

Static helper extension methods for `Graphics`.

#### Static Class: `GraphicsExtensions`

`FillRoundedRect(this Graphics g, Brush brush, RectangleF rect, float radius)`  
Fills a rectangle with 4 rounded corners using a 4-arc path.

`DrawRoundedRect(this Graphics g, Pen pen, RectangleF rect, float radius)`  
Draws the outline of a rectangle with 4 rounded corners.

---

### Controls/MaskListPanel.cs
**Path:** `VisionEditCV/Controls/MaskListPanel.cs`  
**Lines:** 190  
**Namespace:** `VisionEditCV.Controls`

Scrollable dark panel displaying one `MaskCard` per segmentation mask. Selection state is bidirectionally synced with `ImageCanvas`.

#### Class: `MaskListPanel : Panel`

**Events**
| Event | Args | When |
|---|---|---|
| `MaskSelectionChanged` | `MaskSelectedEventArgs` | User clicks a card |

**Public API**

`Populate(List<Color> colors, List<float> scores)`  
Clears existing cards and creates one `MaskCard` per entry. Resets scroll to top.

`ClearRows()`  
Disposes and removes all `MaskCard` controls.

`SetRowSelected(int index, bool selected)`  
Silently sets a card's checked state (without firing `CheckChanged`) and invalidates it. Used when canvas selection changes.

**Layout**  
`LayoutRows()`: stacks cards top-to-bottom with 6px gap. Card width fills the panel minus scrollbar. Called on `OnResize`.

#### Private Class: `MaskCard : Control`

One row in the mask list. 44px tall, double-buffered.

| State | Background |
|---|---|
| Default | `CardBg` dark blue |
| Hover | `CardHover` lighter blue |
| Selected | `CardSelected` dark teal |

`OnPaint`: draws rounded card background, optional border (cyan when selected, faint cyan when hovered), a coloured circle swatch, and "Mask N" label.

`SetCheckedSilent(bool value)`: sets `_checked` without firing the event.

`OnClick`: toggles `_checked`, fires `CheckChanged`.

---

### Controls/RoundedTextBox.cs
**Path:** `VisionEditCV/Controls/RoundedTextBox.cs`  
**Lines:** 158  
**Namespace:** `VisionEditCV.Controls`

A single-line text input with a custom-drawn rounded border. Hosts a native `TextBox` inset inside a custom-painted wrapper.

#### Class: `RoundedTextBox : Control`

**Properties**
| Property | Default | Purpose |
|---|---|---|
| `CornerRadius` | `10` | Border corner radius |
| `BorderWidth` | `1` | Border stroke width |
| `BorderColor` | `rgb(0,229,255)` | Border colour |
| `PlaceholderText` | `""` | Forwarded to inner `TextBox.PlaceholderText` |
| `Text` | | Forwarded to inner `TextBox.Text` |
| `Font` | | Applied to both wrapper and inner TextBox |

**Behaviour**  
- The inner `TextBox` has `BorderStyle.None` — its edges are hidden behind the custom-painted border
- Focus state: `_focused` flag changes border colour to bright cyan
- `UpdateInnerBounds()`: insets the inner TextBox by `borderWidth + cornerRadius/3 + 3` px on all sides

`OnPaint`: fills rounded background, draws border (dim grey when unfocused, cyan when focused).

---

### Controls/SliderControl.cs
**Path:** `VisionEditCV/Controls/SliderControl.cs`  
**Lines:** 148  
**Namespace:** `VisionEditCV`

A fully custom-painted horizontal slider replacing the OS `TrackBar`.

#### Class: `SliderControl : Control`

**Properties**
| Property | Default | Purpose |
|---|---|---|
| `Minimum` | `0` | Minimum value |
| `Maximum` | `100` | Maximum value |
| `Value` | `50` | Current value; fires `ValueChanged` on change |

**Events**
| Event | Fired when |
|---|---|
| `ValueChanged` | Value changes programmatically or via drag |

**Layout geometry (private)**
- `TrackLeft()` / `TrackRight()`: 8px inset on each side
- `TrackY()`: vertically centred
- `ThumbX()`: maps `Value` linearly to track pixel position

**`OnPaint`**: draws 6px tall rounded track background, filled portion (TrackFill cyan), 14px circular thumb, right-aligned value label.

**Mouse interaction**: `OnMouseDown` / `OnMouseMove` call `UpdateFromMouse(int x)` which maps the cursor X position to a `Value` via ratio.

---

### Models/SegmentationResult.cs
**Path:** `VisionEditCV/Models/SegmentationResult.cs`  
**Lines:** 19  
**Namespace:** `VisionEditCV.Models`

Data class holding the parsed SAM3 API response.

#### Class: `SegmentationResult`

| Property | Type | Description |
|---|---|---|
| `Masks` | `List<float[,]>` | N masks, each a `[H, W]` float array (values 0..1) |
| `Boxes` | `List<float[]>` | N bounding boxes `[x, y, w, h]` in image-space |
| `Scores` | `List<float>` | N confidence scores (0..1) |

---

### Processing/ImageEffects.cs
**Path:** `VisionEditCV/Processing/ImageEffects.cs`  
**Lines:** 910  
**Namespace:** `VisionEditCV.Processing`

Static class containing all EmguCV implementations of image effects. All effect methods are C# ports of the Python `functions.py` equivalents.

#### Class: `ImageEffects` (static)

---

##### Bitmap / Mat Conversion Helpers

**`BitmapToMat(Bitmap bmp) → Mat`**  
Converts a `System.Drawing.Bitmap` to an EmguCV `Mat` (BGR or BGRA).
- Normalises pixel format to `Format24bppRgb` or `Format32bppArgb` if needed
- Uses `LockBits` + `Marshal.Copy` for row-by-row copy
- Returns a Mat with the same channel count as the source

**`MatToBitmap(Mat mat) → Bitmap`**  
Converts an EmguCV Mat (BGR or BGRA) to a `System.Drawing.Bitmap`.
- Creates a bitmap with matching pixel format
- Uses `LockBits` + `Marshal.Copy` for row-by-row copy

**`ResizeAndThresholdMask(float[,] mask, int targetW, int targetH, float threshold=0.5f) → byte[,]`**  
Converts a float probability mask to a binary `byte[,]` mask at target dimensions.
1. Pins the float array and copies to `byte[]` via `Marshal.Copy`
2. Wraps in a `Cv32F` Mat
3. Resizes with `Inter.Linear`
4. Thresholds at `threshold` → binary (0 or 255)
5. Converts to `Cv8U`
6. Returns as `byte[H, W]`

Mirrors Python: `mask = cv2.resize(mask, (image.shape[1], image.shape[0]))`

---

##### Effect 1: Color Grading

**`ColorGrading(Bitmap image, float[,] mask, Color tintColor, float tintStrength, int brightness, float contrast, bool blackAndWhite) → Bitmap`**

Port of `Color_Grading()` from functions.py. Applies adjustments to the masked region. To target the background, the caller passes an inverted mask (same pattern as `PixelateMasked`/`BlurMasked`).

Steps:
1. Convert to BGR 3-channel
2. Resize+threshold mask; build `maskMat`
3. Clone full image as `processed`
4. If `blackAndWhite`: convert `processed` to greyscale and back to BGR
5. `CvInvoke.ConvertScaleAbs(alpha=contrast, beta=brightness)` for brightness/contrast
6. If `tintStrength > 0`: blend with a solid tint colour using `AddWeighted`
7. Clone original as `result`; `processed.CopyTo(result, maskMat)` — overwrite only masked pixels

| Parameter | Range | Purpose |
|---|---|---|
| `tintStrength` | 0..1 | Blend weight for the tint colour |
| `brightness` | -255..255 | Additive brightness offset |
| `contrast` | 0.1..3.0 | Multiplicative contrast scale |
| `blackAndWhite` | bool | Convert to greyscale before grading |

---

##### Effect 2: Artistic Style

**`MaskBoundingRect(byte[,] binaryMask, int w, int h, int pad=8) → Rectangle`** (private)  
Scans the binary mask for the bounding rectangle of all non-zero pixels, padded by `pad` px and clamped to image bounds. Returns `Rectangle.Empty` if mask is all zero. Used to crop the ROI before expensive NPR filters.

**`StylizeMasked(Bitmap image, float[,] mask, int sigmaS, float sigmaR) → Bitmap`**  
Port of `cv2.stylization()` applied only within the mask bounding box.

Steps:
1. Convert to BGR
2. Build binary mask; compute bounding rect ROI
3. Crop to ROI: `new Mat(imgBgr, roi)`
4. `CvInvoke.Stylization(crop, styledCrop, sigmaS, sigmaR)` on the small crop only
5. Paste styled crop back into result using mask ROI as copy mask

| Parameter | Range | Purpose |
|---|---|---|
| `sigmaS` | 1..200 | Spatial filter extent (larger = more painterly) |
| `sigmaR` | 0.01..1.0 | Color range (larger = fewer distinct colours) |

**`PencilSketchMasked(Bitmap image, float[,] mask, int sigmaS, float shadeFactor) → Bitmap`**  
Port of `cv2.pencilSketch()` applied only within the mask bounding box.

Steps same as `StylizeMasked` but calls `CvInvoke.PencilSketch(crop, gray, colorSketch, sigmaS, 0.07f, shadeFactor)`. Pastes the colour sketch back.

| Parameter | Purpose |
|---|---|
| `sigmaS` | Spatial filter extent |
| `shadeFactor` | Controls darkness of pencil shading (0..0.1 typical) |

---

##### Effect 3: Sticker Generation

**`ExtractSticker(Bitmap image, float[,] mask, float threshold, int contourThickness, int shadowBlur, Color borderColor, float scaleFactor, float rotationAngle) → Bitmap`**  
Extracts the masked foreground as a BGRA bitmap with transparent background, drop shadow, contour border, and optional scale+rotation.

Steps:
1. Resize+threshold mask → `maskMono` (single-channel)
2. **Shadow**: if `shadowBlur > 0`, gaussian-blur the mask, shift it +20px right/down via warp affine, scale alpha to 50%, merge as black BGRA shadow layer
3. **Foreground copy**: convert source to BGRA, set alpha channel to `maskMono`, copy foreground pixels onto the sticker using `maskMono` as copy mask
4. **Scale + Rotate**: if transform is non-trivial, compute mask centroid via `CvInvoke.Moments()`, build rotation matrix via `GetRotationMatrix2D()`, apply `WarpAffine` with transparent border
5. **Contour border**: if `contourThickness > 0`, find contours on the alpha channel and `DrawContours` with `borderColor`

| Parameter | Purpose |
|---|---|
| `threshold` | Binary mask threshold (default 0.5) |
| `contourThickness` | Pixel width of the contour border |
| `shadowBlur` | Gaussian blur kernel for drop shadow (0 = no shadow) |
| `borderColor` | Contour border colour |
| `scaleFactor` | Scale factor around centroid |
| `rotationAngle` | Rotation degrees around centroid |

**`CompositeSticker(Bitmap stickerBgra, Bitmap background) → Bitmap`**  
Alpha-blends a BGRA sticker centred onto a background bitmap.

Steps:
1. Convert background to BGRA Mat
2. Compute centre offset for the sticker
3. Per-pixel alpha blend: `out = bg*(1-a) + sticker*a`

**`SolidColorBackground(Color color, int width, int height) → Bitmap`**  
Creates a solid-colour background bitmap. Used for the Sticker "Solid" background mode.

---

##### Effect 4: Portrait

**`PortraitEffect(Bitmap image, float[,] mask, int blurStrength=51, int featherAmount=21) → Bitmap`**  
Port of `apply_portrait_effect()`. Blurs the background while keeping the masked foreground sharp.

Steps:
1. Build float mask `[0..1]` resized to image dimensions via `Marshal.Copy` + `CvInvoke.Resize`
2. If `featherAmount > 0`: `GaussianBlur` the float mask to soften edges (feathering)
3. Gaussian blur the full image for the background layer
4. Convert image and blurred image to float32
5. Expand alpha mask to 3 channels via `CvtColor(Gray2Bgr)`
6. Blend: `result = sharp * alpha + blurred * (1 - alpha)`
7. Convert back to uint8

| Parameter | Range | Purpose |
|---|---|---|
| `blurStrength` | 3..501 (odd) | Gaussian blur kernel for background |
| `featherAmount` | 0..501 (odd) | Gaussian blur kernel for mask edge softening |

---

##### Effect 5: Pixelation & Blur

**`PixelateMasked(Bitmap image, float[,] mask, int pixelSize) → Bitmap`**  
Port of `pixelate_image()`. Applies pixelation to the masked region.

Steps:
1. Downsample image to `(w/pixelSize, h/pixelSize)` with `Inter.Linear`
2. Upsample back to `(w, h)` with `Inter.Nearest` (creates blocky pixels)
3. Copy pixelated result into original using `maskMat` as copy mask

**`BlurMasked(Bitmap image, float[,] mask, int kernelSize) → Bitmap`**  
Port of `blur_image()`. Applies Gaussian blur to the masked region.

Steps:
1. `CvInvoke.GaussianBlur(imgBgr, blurred, (k,k), sigma=10)`
2. Copy blurred result into original using `maskMat` as copy mask

Both effects ensure kernel size is odd and ≥1.

---

##### Mask Overlay Renderer

**`RenderMaskOverlays(Bitmap image, IList<float[,]> masks, IList<Color> colors, IList<bool> selected, IList<float> scores, float alpha=0.45f) → Bitmap`**  
Composites semi-transparent coloured mask overlays onto the image. Called by `ImageCanvas.RefreshDisplay()`.

Logic:
- If any mask is selected: only render selected masks (hide all others)
- Selected mask alpha: `0.45f`; unselected mask alpha: `0.45 * 0.35 ≈ 0.16f`
- For each rendered mask:
  1. `AddWeighted` coloured overlay onto the result
  2. `FindContours` + `DrawContours` — only on **unselected** masks (selected mask skips the contour to avoid covering sticker/effect borders)
  3. Draw a numbered label tag above the topmost contour: a dark rounded rectangle with a coloured circle containing the mask number, and a confidence score percentage

| Parameter | Purpose |
|---|---|
| `alpha` | Base overlay transparency (default 0.45) |

---

##### Transform Helper

**`TransformMaskForDisplay(float[,] mask, int imageW, int imageH, float scaleFactor, float rotationAngle, float threshold=0.5f, int previewMaxDim=0) → float[,]`**  
Applies the same scale+rotation transform used by `ExtractSticker` to a float mask. Used during Sticker live preview so the mask overlay contour stays in sync with the transformed subject pixels.

- If no transform (scale≈1, rotation≈0): returns a copy unchanged
- If `previewMaxDim > 0` and image is larger: works at reduced resolution, then upscales back (much faster for live preview at 256px)
- Computes centroid via `CvInvoke.Moments()` (same as `ExtractSticker`)
- Applies `GetRotationMatrix2D` + `WarpAffine`
- Returns `float[H, W]` (0.0 or 1.0)

| Parameter | Purpose |
|---|---|
| `previewMaxDim` | If > 0, processes at reduced resolution for speed. Pass 0 for full-res (Apply path). |

---

##### Internal Helper

**`BuildMaskMat(byte[,] binaryMask, int w, int h) → Mat`** (private)  
Flattens `byte[,]` to `byte[]` via `Buffer.BlockCopy` and creates a single-channel `Cv8U` Mat. Used throughout the effects to produce OpenCV-compatible mask Mats.

---

## Key Design Patterns

### 1. EffectArgs Immutable Snapshot
All effect parameters are captured in an immutable `record EffectArgs(...)` on the UI thread before any async work begins. This eliminates thread-safety issues: the background thread operates entirely on captured data, never touching UI controls.

The `with`-expression allows non-destructive modification:
```csharp
// Scale sigmaS for full-resolution apply:
args = args with { ArtSigmaSScale = (float)longestSide / PreviewMaxDim };
```

### 2. Debounced Async Preview
`TriggerLivePreview()` uses a `CancellationTokenSource` + `Task.Delay` debounce pattern:
- Every call cancels the previous CTS and creates a new one
- Light effects: 30ms debounce; heavy effects: 200ms
- A `SemaphoreSlim(1,1)` ensures only one render runs at a time — a new request waits for the previous render to finish rather than spawning parallel renders

### 3. Heavy Effect Downscaling
Artistic, Sticker, Portrait, and PixelBlur use `ScaleForPreview(src, 900)` to limit preview to ≤900px longest side. On Apply, resolution-dependent parameters (sigmaS, blurStrength, featherAmount, kernelSize, pixelSize) are multiplied by `fullResolution / 900` so the committed result visually matches the preview.

### 4. Dual Bitmap Tracking
`ImageCanvas` maintains two distinct original bitmaps:
- `_originalBitmap`: the working base, replaced on every `CommitProcessedAsOriginal()` call — enables effect chaining
- `_fileOriginalBitmap`: set once on `LoadImage()`, never replaced — enables "Show Before" to always show the pristine on-disk image regardless of how many effects have been applied

### 5. Mask Override System
For the Sticker effect, the live preview needs to show the mask contour at the transformed (scaled/rotated) position. `SetDisplayMaskOverride()` injects a separate mask list used only for overlay rendering, without touching the canonical `_masks` data. On Apply, the transform is baked into `_masks` permanently via `ReplaceMasks()`, and the override is cleared.

### 6. ROI Crop for NPR Filters
`StylizeMasked` and `PencilSketchMasked` crop to the mask bounding box (+ 8px pad) before running `Stylization`/`PencilSketch`. These NPR filters are O(W×H) and extremely slow on full images. Processing only the ROI gives a 10–50x speedup on typical subject-in-scene images.

### 7. Borderless Window Resize via WndProc
Since `FormBorderStyle.None` removes all OS-managed resize chrome, `WndProc` intercepts `WM_NCHITTEST`. When the default result is `HTCLIENT`, it checks if the cursor is within 8px of any edge/corner and substitutes the correct resize hit-test code, restoring native OS resize behaviour without any manual mouse tracking.

### 8. Proportional Effect Sub-Panel Layout
`EffectSubPanelResize` implements a full proportional layout engine: it measures each group's natural width, distributes surplus space evenly across non-combo groups, then vertically centres each group's content (label + control) within the available height. This ensures the panel looks correct at any window width.

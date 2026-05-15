# VisionEditCV

## Project Overview
VisionEditCV is a high-performance computer vision image editing application. It originally started as a Windows Forms (.NET 8) application but has been fully modernized and migrated to a native cross-platform architecture using Avalonia UI and SkiaSharp.

The application allows users to load images, perform AI-powered segmentation (via bounding boxes or text prompts calling a SAM3 backend API), manage masks, and apply various image effects (e.g., Color Grading, Pixelation, Blur, Artistic Styles, Portrait depth-of-field, and Sticker Extraction).

### Architecture
- **VisionEditCV.Core**: A platform-agnostic class library containing the core business logic, API clients (`Sam3Client`), and high-performance image processing algorithms (`ImageEffects.cs`). It utilizes `Emgu.CV` (OpenCV wrapper) using the `Mat` class instead of Windows-only `System.Drawing.Bitmap`.
- **VisionEditCV.Desktop**: The modern Avalonia UI desktop application implementing the MVVM pattern (via `CommunityToolkit.Mvvm`).
  - Uses `SKCanvas` (SkiaSharp) for hardware-accelerated, high-performance rendering of the image and mask overlays (utilizing `unsafe` memory pointers for 60fps rendering without lag).
  - Designed with a professional "Creative Suite" layout featuring a luxury glassmorphic dark theme inspired by WinUI 3 (Windows 11 Fluent Design), implementing `ExperimentalAcrylicBorder` for deep transparency and depth.

## Building and Running

Ensure you have the .NET 8 SDK installed.

**To build the solution:**
```bash
dotnet build VisionEditCV.sln
```

**To run the Desktop Application:**
```bash
dotnet run --project src/VisionEditCV.Desktop/VisionEditCV.Desktop.csproj
```

## Development Conventions
- **MVVM Pattern:** Strict adherence to Model-View-ViewModel. UI logic belongs in `MainWindowViewModel.cs` using `[ObservableProperty]` and `[RelayCommand]` attributes.
- **Cross-Platform Compatibility:** Never introduce `System.Drawing` or Windows-specific namespaces into the Core library. Use `Emgu.CV.Mat` or primitive arrays for image processing, converting to Avalonia `Bitmap` at the UI boundary (`ImageHelper.cs`).
- **Styling:** Use Avalonia's `FluentTheme` and the custom luxury visual system. Avoid hardcoding hex colors directly in the XAML; instead, use the semantic color resources defined in `src/VisionEditCV.Desktop/Styles/AppStyles.axaml` (e.g., `{DynamicResource GlassBackgroundBrush}`, `{DynamicResource AccentBrush}`).
- **Visual Philosophy:** Maintain a high-contrast, professional aesthetic with thin borders (hairline), generous corner radii (8-12px), and clear hierarchical groupings using glass panels.
- **Rendering Performance:** Modifying `ImageCanvas.cs` rendering logic should utilize `Avalonia.Rendering.SceneGraph.ICustomDrawOperation` and direct pointer manipulation via SkiaSharp (`SKBitmap.GetPixels()`) to maintain zero-lag drawing operations.

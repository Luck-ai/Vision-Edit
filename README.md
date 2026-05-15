# VisionEditCV

VisionEditCV is a cross-platform .NET 8 desktop app for AI-assisted image segmentation and region-based image editing. It runs on Windows, Linux, and macOS with a single Avalonia UI codebase.

It combines:
- SAM3 API segmentation (bounding boxes or text prompts)
- Interactive mask selection and toggle
- Adjustable per-effect parameters
- Non-destructive workflow with undo and before/after compare

## Features

- Open images from the system file picker
- Segment by:
  - **Bounding Box** — draw one or more boxes on the canvas
  - **Prompt** — describe what to segment in natural language
- Toggle individual masks on/off as effect targets
- Built-in effects:
  - Color Grading (tint, brightness, contrast)
  - Artistic Style (stylize / pencil sketch)
  - Sticker Generation (border, shadow, transform, custom background)
  - Pixelation / Blur
  - Portrait Effect (background blur with feathering)
  - Grayscale
- Undo per applied effect, full project reset, compare-with-original
- Keyboard shortcuts: `Ctrl+O` open, `Ctrl+S` save, `Ctrl+Enter` segment, `Ctrl+Z` undo, `Esc` clear masks
- Auto-update via Velopack (Windows + Linux + macOS, delta patches)

## Tech Stack

- **.NET 8** with **Avalonia 12** for the cross-platform UI
- **Semi.Avalonia** theme + **Inter** font
- **CommunityToolkit.Mvvm** for view models
- **Emgu CV** (OpenCV bindings) for image processing — automatically swaps the native runtime per OS (`ubuntu-x64`, `windows`, `macos`)
- **Velopack** for installer + auto-update
- Remote **SAM3 REST API** for segmentation

## Install (end users)

Prebuilt installers are published to [GitHub Releases](https://github.com/Luck-ai/Vision-Edit/releases).

| Platform | Download | Run |
|---|---|---|
| Windows | `VisionEditCV-win-Setup.exe` | Double-click — installs to `%LocalAppData%`, creates Start Menu + Desktop shortcuts |
| Linux | `VisionEditCV.AppImage` | `chmod +x VisionEditCV.AppImage && ./VisionEditCV.AppImage` |
| macOS | `VisionEditCV-osx.pkg` | Double-click |

Once installed the app checks for updates 3 seconds after launch and again when you click **Check for Updates** in the *Server* tab. Updates download as small delta patches and apply on the next restart.

## Build from source

### Prerequisites

- [.NET SDK 8.0+](https://dotnet.microsoft.com/download)
- A C# IDE (Rider, Visual Studio, or VS Code with the C# Dev Kit) — optional

### Run in development

```bash
git clone https://github.com/Luck-ai/Vision-Edit
cd Vision-Edit/VisionEditCV
dotnet run --project src/VisionEditCV.Desktop
```

Auto-update is disabled in dev builds (the update panel shows *"running from a non-installed build"*).

### Package a release

The repo ships two scripts that wrap `dotnet publish` + Velopack's `vpk pack`:

```bash
# Linux / macOS host:
./publish.sh 1.0.0 linux-x64         # → releases/linux-x64/VisionEditCV.AppImage
./publish.sh 1.0.0 osx-arm64         # → releases/osx-arm64/...

# Windows host (PowerShell):
./publish.ps1 -Version 1.0.0          # → releases/win-x64/VisionEditCV-win-Setup.exe
```

Each run produces an installer plus a full + delta nupkg consumed by the auto-updater.

> **Note:** Velopack's Windows installer can only be built *from* Windows; the bash script handles `linux-x64` / `osx-*` and the PowerShell script handles `win-x64`.

### Publish a new version to GitHub Releases

Get a [GitHub Personal Access Token](https://github.com/settings/tokens?type=beta) with `Contents: write` for the repo, then:

```bash
export GITHUB_TOKEN=github_pat_xxx
./publish.sh 1.0.1 linux-x64 --publish
./publish.ps1 -Version 1.0.1 -PublishGithub    # on Windows
```

The script tags the commit `v<version>` and uploads the installer + update manifest. The next launch of any installed copy will detect the new version, download the delta, and prompt the user to **Restart & Install**.

## How to Use

1. Start the app. The **Server** tab shows your SAM3 endpoint — click **Connect** to verify it's reachable.
2. **Import Image** (or `Ctrl+O`).
3. Pick a tool in the top bar:
   - **BOX** — drag one or more boxes on the canvas
   - **TEXT** — type a prompt in the composer
4. Click **Segment** (or `Ctrl+Enter`). Masks appear in the right panel.
5. Toggle which masks are active and select an **effect** in the left panel.
6. Adjust the parameter sliders in the bottom inspector. Click **Apply**.
7. Stack more effects, **Undo** with `Ctrl+Z`, or **Compare** to flash the original.
8. **Export** (or `Ctrl+S`) to save the result.

## Server Configuration

- The default SAM3 endpoint is set on first launch — change it in the **Server** tab and click **Connect**.
- A cold-starting server may take 5–6 minutes to wake; the status pill shows progress.

## Project Structure

```text
VisionEditCV/                          # repo root
├── README.md
├── VisionEditCV.sln
└── VisionEditCV/                      # main project folder
    ├── publish.sh / publish.ps1       # Velopack build + upload scripts
    └── src/
        ├── VisionEditCV.Core/         # API client, models, image effects
        │   ├── Api/Sam3Client.cs
        │   ├── Models/
        │   └── Processing/ImageEffects.cs
        ├── VisionEditCV.Desktop/      # Avalonia UI (current)
        │   ├── Views/MainWindow.axaml
        │   ├── ViewModels/MainWindowViewModel.cs
        │   ├── Styles/AppStyles.axaml
        │   ├── Controls/ImageCanvas.cs
        │   ├── Converters/
        │   └── Program.cs
        └── VisionEditCV.WinForms/     # legacy Windows-only UI (deprecated)
```

## Troubleshooting

- **App won't start** — verify .NET 8 runtime is installed (`dotnet --info`). Self-contained installers bundle the runtime so this only matters for `dotnet run` from source.
- **Segmentation hangs or fails** — check the **Server** tab status. The server may be cold-starting; retry after a minute. Verify network access.
- **"Auto-update disabled" in Server tab** — expected in dev builds (`dotnet run`). Auto-update only activates in installed copies from the installer or AppImage.
- **NETSDK1206 warning during build** — harmless. The Emgu native runtime package is still named with the legacy `ubuntu-x64` RID; everything works at runtime.

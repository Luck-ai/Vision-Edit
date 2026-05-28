# VisionEditCV

VisionEditCV is a cross-platform .NET 8 desktop + mobile app for AI-assisted image segmentation and region-based image editing. Single Avalonia 11 UI runs on Windows, Linux, macOS and Android.

It combines:
- SAM3 API segmentation (bounding boxes or text prompts)
- Interactive mask selection and toggle
- Adjustable per-effect parameters with live debounced previews
- Non-destructive workflow with undo and before/after compare
- A persistent project library (open, pin, filter, search recently-edited images)

## Features

- **Library / Home view** — opens on launch, lists projects with thumbnail gradients and live counts. Filters: All / In progress / Exported / Pinned. Search by name. Pin or remove per card. "Continue editing" rail of the four most recently-edited projects.
- **Editor** — opens any image from the system file picker; cross-fades in from the library and back via a top-bar back button. Persists every opened/saved file to `LocalAppData/VisionEditCV/projects.json`.
- **Segment** by:
  - **Bounding Box** — drag one or more boxes on the canvas
  - **Prompt** — describe what to segment in natural language
- Toggle individual masks on/off as effect targets.
- **Effects**: Color Grading (tint, brightness, contrast), Artistic Style (stylize / pencil sketch), Sticker Generation (border, shadow, transform, custom background), Pixelation / Blur, Portrait Effect (background blur with feathering), Grayscale.
- Undo per applied effect, full reset, compare-with-original.
- **Mobile** (Android) — same editor, plus a bottom float-dock nav with Library / History / Settings tabs.
- Keyboard shortcuts (desktop): `Ctrl+O` / `Ctrl+N` open, `Ctrl+S` save, `Ctrl+Enter` segment, `Ctrl+Z` undo, `Esc` clear masks.
- Auto-update via Velopack (Windows + Linux + macOS, delta patches).

## Tech Stack

- **.NET 8** with **Avalonia 11.3.14** for the cross-platform UI
- **CommunityToolkit.Mvvm** source generators for view models
- **Emgu CV 4.12** (`mini.*` desktop runtimes; `maui.mini.android` 4.9 on Android) for image processing
- **SkiaSharp 2.88.9** (matches Avalonia's bundled native)
- **Velopack** for installer + auto-update
- Remote **SAM3 REST API** for segmentation

## Install (end users)

Prebuilt installers are published to [GitHub Releases](https://github.com/Luck-ai/Vision-Edit/releases).

| Platform | Download | Run |
|---|---|---|
| Linux   | `VisionEditCV.AppImage`     | `chmod +x VisionEditCV.AppImage && ./VisionEditCV.AppImage` |
| Windows | `VisionEditCV-win-Setup.exe`| Double-click — installs to `%LocalAppData%`, creates Start Menu + Desktop shortcuts |
| macOS   | `VisionEditCV-osx.pkg`      | Double-click |
| Android | `VisionEditCV.apk`          | `adb install` or sideload |

Once installed the app checks for updates a few seconds after launch and again from the **Settings** tab. Updates download as small delta patches and apply on the next restart.

## Build from source

### Prerequisites

- .NET SDK **8.0.421+** (the repo's `global.json` pins this — `latestFeature` rollForward)
- For Android: JDK 17, `ANDROID_HOME`, `ANDROID_SDK_ROOT`, `JAVA_HOME` set

### Run in development

```bash
git clone https://github.com/Luck-ai/Vision-Edit
cd Vision-Edit
dotnet run --project src/VisionEditCV.Desktop
```

Auto-update is disabled in dev builds (the Settings tab shows *"running from a non-installed build"*).

### Package a release

```bash
# Linux / macOS host:
./publish.sh 1.6.0 linux-x64          # → releases/linux-x64/VisionEditCV.AppImage
./publish.sh 1.6.0 osx-arm64          # → releases/osx-arm64/...

# Windows host (PowerShell):
./publish.ps1 -Version 1.6.0          # → releases/win-x64/VisionEditCV-win-Setup.exe
```

Each run produces an installer plus a full + delta nupkg consumed by the auto-updater.

> Velopack's Windows installer can only be built *from* Windows; the bash script handles `linux-x64` / `osx-*` and the PowerShell script handles `win-x64`.

### Publish a new version to GitHub Releases

```bash
export GITHUB_TOKEN="$(gh auth token)"   # or a PAT with Contents: write
./publish.sh 1.6.1 linux-x64 --publish
./publish.ps1 -Version 1.6.1 -PublishGithub   # on Windows
```

The script tags the commit `v<version>` and uploads the installer + update manifest. Any installed copy auto-detects the new version, downloads the delta, and prompts to **Restart & Install**.

## How to use

1. Launch the app. The **Library** shows your recently-opened projects.
2. Click **+ NEW PROJECT** (or `Ctrl+O`/`Ctrl+N`) or the **Open new image** card to pick an image.
3. The editor cross-fades in. Pick a tool in the top bar:
   - **Box** — drag one or more boxes on the canvas
   - **Text** — type a prompt
4. Click **Segment** (or `Ctrl+Enter`). Masks appear in the right panel.
5. Toggle which masks are active and select an effect in the left panel.
6. Adjust sliders in the inspector. Click **Apply**.
7. Stack effects, **Undo** with `Ctrl+Z`, **Compare** to flash the original.
8. **Export** (or `Ctrl+S`) — sets the project's **EXPORTED** tag in the library.

## Server configuration

- The default SAM3 endpoint is set on first launch — change it in the **Settings** tab and click **Test connection**.
- A cold-starting server may take 5–6 minutes to wake; the status pill shows progress.

## Project Structure

```text
VisionEditCV/                          # repo root
├── README.md  AGENTS.md  CLAUDE.md  GEMINI.md
├── VisionEditCV.sln
├── global.json                        # pins SDK 8.0.421
├── publish.sh  publish.ps1
└── src/
    ├── VisionEditCV.Core/             # platform-agnostic
    │   ├── Api/Sam3Client.cs
    │   ├── Models/
    │   └── Processing/ImageEffects.cs
    ├── VisionEditCV.Shared/           # Avalonia views + view models
    │   ├── App.axaml(.cs)
    │   ├── Styles/AppStyles.axaml
    │   ├── ViewModels/
    │   │   ├── AppShellViewModel.cs   # owns Library + Editor; drives ActiveView/MobileTab
    │   │   ├── LibraryViewModel.cs    # projects, filters, search, pin/remove
    │   │   └── MainWindowViewModel.cs # all editor state
    │   ├── Models/ProjectItem.cs
    │   ├── Helpers/ProjectStore.cs    # JSON persistence
    │   ├── Controls/ImageCanvas.cs
    │   └── Views/
    │       ├── MainShellView.axaml    # desktop: TransitioningContentControl Library↔Editor
    │       ├── MobileShellView.axaml  # mobile: same + bottom float-dock nav
    │       ├── LibraryView.axaml
    │       ├── MainView.axaml         # editor
    │       ├── MobileView.axaml       # editor (mobile layout)
    │       └── MobileHomeView / MobileHistoryView / MobileSettingsView
    ├── VisionEditCV.Desktop/          # desktop bootstrap (Avalonia + Velopack)
    │   ├── App.axaml(.cs)             # picks shell + injects AppShellViewModel
    │   ├── Views/MainWindow.axaml
    │   └── DesktopUpdateService.cs
    ├── VisionEditCV.Android/          # Android bootstrap
    │   ├── MainActivity.cs / SplashActivity.cs
    │   ├── AndroidManifest.xml
    │   └── Resources/
    └── VisionEditCV.WinForms/         # legacy Win-only build, kept for reference
```

## Troubleshooting

- **App won't start / `cvextern` not found on Ubuntu 24+** — fixed in 1.6.0. The `mini.ubuntu-x64` Emgu runtime links against FFmpeg 6 (`libavcodec.so.60`); an MSBuild target mirrors `libcvextern.so` into `runtimes/linux-x64/native` so the .NET 8 RID lookup finds it.
- **`dotnet run` fails with "Requested SDK version: 8.0.421"** — the project pins to a user-local SDK. Either run via `~/.dotnet/dotnet run …` or put `~/.dotnet` ahead of `/usr/lib/dotnet` on your `PATH`.
- **Segmentation hangs or fails** — check the **Settings** tab status. The server may be cold-starting; retry after a minute. Verify network access.
- **"Auto-update disabled"** — expected in dev builds (`dotnet run`). Auto-update only activates in installed copies from the installer / AppImage / .apk.
- **NETSDK1206 warning during build** — harmless. Emgu's distro-specific RID (`ubuntu-x64`) needs `UseRidGraph=true`, which the csproj already sets.

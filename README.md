<div align="center">

# ✨ VisionEditCV

**AI-assisted image segmentation and region-based editing — one Avalonia codebase for Desktop & Android.**

[![Latest release](https://img.shields.io/github/v/release/Luck-ai/Vision-Edit?include_prereleases&style=for-the-badge&color=22D3EE&labelColor=0E1626)](https://github.com/Luck-ai/Vision-Edit/releases)
[![Downloads](https://img.shields.io/github/downloads/Luck-ai/Vision-Edit/total?style=for-the-badge&color=5A8DFF&labelColor=0E1626)](https://github.com/Luck-ai/Vision-Edit/releases)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&labelColor=0E1626)](https://dotnet.microsoft.com/)
[![Avalonia](https://img.shields.io/badge/Avalonia-11.3-8A2BE2?style=for-the-badge&labelColor=0E1626)](https://avaloniaui.net/)
[![Platforms](https://img.shields.io/badge/Linux%20·%20Windows%20·%20macOS%20·%20Android-Cross%20platform-34D399?style=for-the-badge&labelColor=0E1626)](#-install)

</div>

---

## 🎯 What is it

VisionEditCV is a cross-platform .NET 8 image editor built around **SAM3** (Segment Anything 3). Open any photo, segment regions with a **bounding box** or a **natural-language prompt**, toggle masks on/off as effect targets, and apply per-region effects with live debounced previews. The same Avalonia codebase ships as a desktop app (Windows / Linux / macOS) and an Android APK.

> Built for the KMITL Computer Vision course (2/2) — but generally useful as a self-hostable AI image editor.

---

## ✨ Highlights

### 🖼 Library / Home
Persistent project list that opens on launch. Filter by **All / In progress / Exported / Pinned**, search by name, pin or remove per card. The four most-recently-edited projects surface in a **Continue editing** rail. Cross-fades into the editor and back via a top-bar back button.

### ✂️ Segment
- **Box prompt** — drag one or more rectangles on the canvas
- **Text prompt** — describe the subject ("the woman in the red dress")

### 🎨 Effects
**Color Grading** · **Artistic Style** · **Sticker Generation** · **Pixelation / Blur** · **Portrait DOF** · **Grayscale**

Each effect runs against the user's chosen masks. Undo per applied effect, **before/after compare**, **mask visibility** toggle, **full reset**.

### 📱 Mobile
Same editor inside a native bottom float-dock shell with **Library / History / Settings** tabs.

### ⚙️ Plumbing
- Live debounced **preview** for every slider tweak (cancellation-token guarded)
- Persistent project store in `LocalAppData/VisionEditCV/projects.json`
- **Auto-update** via Velopack (Windows + Linux + macOS, delta patches)
- Keyboard shortcuts (desktop): `Ctrl+O`/`Ctrl+N` open · `Ctrl+S` save · `Ctrl+Enter` segment · `Ctrl+Z` undo · `Esc` clear masks

---

## 🚀 Install

Prebuilt installers live on [GitHub Releases](https://github.com/Luck-ai/Vision-Edit/releases).

| Platform | Download                       | Run                                                                   |
|----------|--------------------------------|-----------------------------------------------------------------------|
| 🐧 Linux   | `VisionEditCV.AppImage`        | `chmod +x VisionEditCV.AppImage && ./VisionEditCV.AppImage`           |
| 🪟 Windows | `VisionEditCV-win-Setup.exe`   | Double-click — installs to `%LocalAppData%` with Start Menu shortcut  |
| 🍎 macOS   | `VisionEditCV-osx.pkg`         | Double-click                                                          |
| 🤖 Android | `VisionEditCV.apk`             | `adb install` or sideload                                             |

Once installed the app checks for updates a few seconds after launch and again from the **Settings** tab. Updates download as small deltas and apply on next restart.

---

## 🛠 Build from source

### Prerequisites
- **.NET SDK 8.0.421+** (the repo's `global.json` pins this with `rollForward: latestFeature`)
- For Android: **JDK 17** plus `ANDROID_HOME`, `ANDROID_SDK_ROOT`, `JAVA_HOME` exported

### Run in development

```bash
git clone https://github.com/Luck-ai/Vision-Edit
cd Vision-Edit
dotnet run --project src/VisionEditCV.Desktop
```

> 💡 If your system `dotnet` is older than 8.0.421, the repo's `global.json` will refuse to load it. Either point at your user-local install (`~/.dotnet/dotnet …`) or put `~/.dotnet` ahead of `/usr/lib/dotnet` on `PATH`.

Auto-update is disabled in dev builds (the Settings tab shows *"running from a non-installed build"*).

---

## 📦 Publish a release

The repo ships two scripts that wrap `dotnet publish` + Velopack's `vpk pack`:

```bash
# Linux / macOS host
./publish.sh 1.6.0 linux-x64          # → releases/linux-x64/VisionEditCV.AppImage
./publish.sh 1.6.0 osx-arm64          # → releases/osx-arm64/...

# Windows host (PowerShell)
./publish.ps1 -Version 1.6.0          # → releases/win-x64/VisionEditCV-win-Setup.exe
```

Each run produces an installer plus a full + delta nupkg consumed by the auto-updater.

### Push to GitHub Releases

```bash
export GITHUB_TOKEN="$(gh auth token)"   # or a PAT with Contents: write
./publish.sh 1.6.1 linux-x64 --publish
./publish.ps1 -Version 1.6.1 -PublishGithub   # on Windows
```

The script tags the commit `v<version>` and uploads the installer + update manifest. Any installed copy auto-detects the new version, downloads the delta, and prompts to **Restart & Install**.

> 🪟 Velopack's Windows installer can only be built **from** Windows; `publish.sh` handles `linux-x64` / `osx-*` and `publish.ps1` handles `win-x64`.

---

## 🎯 How to use

1. Launch the app — the **Library** opens with your recently-edited projects.
2. Click **+ NEW PROJECT** (or press `Ctrl+O`/`Ctrl+N`) or the **Open new image** card.
3. The editor cross-fades in. Pick a tool in the top bar:
   - **Box** — drag one or more rectangles on the canvas
   - **Text** — type a prompt in the composer
4. Click **Segment** (or `Ctrl+Enter`). Masks appear in the right panel.
5. Toggle which masks are active, then select an **effect** in the left panel.
6. Adjust the parameter sliders in the inspector. Click **Apply**.
7. Stack more effects, **Undo** with `Ctrl+Z`, or **Compare** to flash the original.
8. **Export** (or `Ctrl+S`) — sets the project's **EXPORTED** tag in the library.

---

## ⚙️ Server configuration

- The default SAM3 endpoint is set on first launch — change it in the **Settings** tab and click **Test connection**.
- A cold-starting server may take **5–6 minutes** to wake; the status pill shows progress.

---

## 🗂 Project structure

```text
VisionEditCV/                          # repo root
├── README.md  AGENTS.md  CLAUDE.md  GEMINI.md
├── VisionEditCV.sln
├── global.json                        # pins SDK 8.0.421
├── publish.sh  publish.ps1
└── src/
    ├── VisionEditCV.Core/             # platform-agnostic: SAM client + effects
    │   ├── Api/Sam3Client.cs
    │   ├── Models/
    │   └── Processing/ImageEffects.cs
    │
    ├── VisionEditCV.Shared/           # Avalonia views + view models (desktop + mobile)
    │   ├── App.axaml(.cs)
    │   ├── Styles/AppStyles.axaml
    │   ├── ViewModels/
    │   │   ├── AppShellViewModel.cs   # owns Library + Editor; drives ActiveView / MobileTab
    │   │   ├── LibraryViewModel.cs    # projects, filters, search, pin/remove
    │   │   └── MainWindowViewModel.cs # all editor state
    │   ├── Models/ProjectItem.cs
    │   ├── Helpers/ProjectStore.cs    # JSON persistence
    │   ├── Controls/ImageCanvas.cs    # custom Skia canvas
    │   └── Views/
    │       ├── MainShellView.axaml    # desktop: cross-fade Library ↔ Editor
    │       ├── MobileShellView.axaml  # mobile: same + bottom float-dock nav
    │       ├── LibraryView.axaml      # desktop home
    │       ├── MainView.axaml         # desktop editor
    │       ├── MobileView.axaml       # mobile editor
    │       └── MobileHomeView · MobileHistoryView · MobileSettingsView
    │
    ├── VisionEditCV.Desktop/          # desktop bootstrap (Avalonia + Velopack)
    ├── VisionEditCV.Android/          # Android bootstrap (icons, splash, MainActivity)
    └── VisionEditCV.WinForms/         # legacy Win-only build, kept for reference
```

---

## 🆘 Troubleshooting

| Symptom                                                                 | Fix                                                                                                                                                  |
|-------------------------------------------------------------------------|------------------------------------------------------------------------------------------------------------------------------------------------------|
| `cvextern` not found on Ubuntu 24+                                      | Fixed in 1.6.0. The `mini.ubuntu-x64` Emgu runtime links against FFmpeg 6 (`libavcodec.so.60`); an MSBuild target also mirrors the `.so` into the linux-x64 RID dir. |
| `dotnet run` says *"Requested SDK version: 8.0.421"*                    | Repo pins a user-local SDK. Run via `~/.dotnet/dotnet run …` or put `~/.dotnet` ahead of `/usr/lib/dotnet` on your `PATH`.                            |
| Segmentation hangs or fails                                             | Check the **Settings** tab status. The server may be cold-starting; retry after a minute. Verify network access.                                       |
| *"Auto-update disabled"* in Settings                                    | Expected in dev builds (`dotnet run`). Auto-update only activates in installed copies (installer / AppImage / .apk).                                  |
| Build warning `NETSDK1206`                                              | Harmless. Emgu's distro-specific RID (`ubuntu-x64`) needs `UseRidGraph=true`, which the csproj already sets.                                          |

---

<div align="center">

**VisionEditCV** · [GitHub](https://github.com/Luck-ai/Vision-Edit) · [Releases](https://github.com/Luck-ai/Vision-Edit/releases) · [Issues](https://github.com/Luck-ai/Vision-Edit/issues)

Built for **KMITL Computer Vision 2/2** · MIT-spirited

</div>

# VisionEditCV

VisionEditCV is a .NET 8 Windows desktop app for AI-assisted image segmentation and region-based image editing.

It combines:
- SAM3 API segmentation (bounding boxes or text prompts)
- Interactive mask selection
- Real-time effect previews
- Non-destructive-style editing workflow with apply/reset and before/after comparison

## Features

- Load images from file dialog or drag-and-drop
- Segment by:
	- Bounding box mode
	- Text prompt mode
- Select one or more masks to target edits
- Built-in effects:
	- Color Grading (tint, brightness, contrast)
	- Artistic Style (stylize or pencil sketch)
	- Sticker Generation (outline, shadow, transform, custom background)
	- Pixelation / Blur
	- Portrait Effect (background blur with feathering)
	- Grayscale
- Live preview while adjusting sliders
- Save output as PNG or JPEG

## Tech Stack

- .NET 8 WinForms
- Emgu CV (OpenCV wrapper for C#)
- Newtonsoft.Json
- Remote SAM3 REST API

## Prerequisites

1. Windows OS
2. .NET SDK 8.0+
3. Internet access to reach the segmentation server

Check your .NET version:

```bash
dotnet --version
```

## Getting Started

1. Clone the repository.
2. Open the solution in Visual Studio, or run from terminal.

## Install from Setup Folder (Prebuilt App)

If you want to install and run the app without building source code, use the files in `Setup/`.

### Option 1: Use setup.exe (recommended)

1. Open `Setup/`.
2. Double-click `setup.exe`.
3. Follow the installation wizard prompts.
4. Launch VisionEditCV from Start Menu (or desktop shortcut if created).

### Option 2: Use ClickOnce manifest directly

1. Open `Setup/`.
2. Double-click `VisionEditCV.application`.
3. Accept the install/run prompt.

Notes:
- Windows may show a security prompt for downloaded files; choose to trust/publish only if the source is trusted.
- If required by your machine, install the .NET Desktop Runtime 8 first.

### Run with Visual Studio

1. Open `VisionEditCV.slnx`.
2. Set `VisionEditCV` as startup project (if needed).
3. Press `F5`.

### Run from Terminal

```bash
cd VisionEditCV
dotnet restore
dotnet run
```

## How to Use

1. Start the app and load an image.
2. Ensure server status is connected.
3. Choose segmentation mode:
	 - `BBox`: draw one or more boxes.
	 - `Prompt`: type a text prompt.
4. Click `Segment`.
5. Select masks in the right panel.
6. Choose an effect and adjust parameters.
7. Click `Apply` to commit, or `Reset` to discard current effect adjustments.
8. Use `Show Before` to compare with the original image.
9. Save the final result.

## Server Configuration

- The app uses a default SAM3 server URL on startup.
- If the server is cold-starting, initial connection can take around 5-6 minutes, a successful connection will show "Connected" status.

## Project Structure

```text
VisionEditCV/
	Api/
		Sam3Client.cs          # SAM3 HTTP client
	Controls/                # Custom WinForms controls
	Models/
		SegmentationResult.cs
	Processing/
		ImageEffects.cs        # Effect implementations
	MainForm.cs              # Main UI and app workflow
	Program.cs               # Application entry point
```

## Troubleshooting

- App does not start:
	- Verify .NET 8 SDK is installed.
- Segmentation fails or times out:
	- Check internet connection.
	- Verify server is connected by clicking the status button.
	- Retry after waiting for server warm-up.
- No masks returned:
	- Try a different prompt or adjust bounding boxes.


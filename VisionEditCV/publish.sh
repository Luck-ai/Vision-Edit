#!/usr/bin/env bash
# Build and pack VisionEditCV.Desktop with Velopack for a given RID.
# Usage: ./publish.sh <version> [rid]
#   version  e.g. 1.0.0
#   rid      linux-x64 | win-x64 | osx-x64 | osx-arm64  (default: linux-x64)
set -euo pipefail

VERSION="${1:?version required, e.g. ./publish.sh 1.0.0 linux-x64}"
RID="${2:-linux-x64}"

PROJECT="src/VisionEditCV.Desktop/VisionEditCV.Desktop.csproj"
PUBLISH_DIR="publish/$RID"
RELEASE_DIR="releases/$RID"
PACK_ID="VisionEditCV"

case "$RID" in
    win-x64)        MAIN_EXE="VisionEditCV.Desktop.exe"; CHANNEL="win" ;;
    linux-x64)      MAIN_EXE="VisionEditCV.Desktop";     CHANNEL="linux" ;;
    osx-x64|osx-arm64) MAIN_EXE="VisionEditCV.Desktop";  CHANNEL="osx" ;;
    *) echo "Unsupported RID: $RID" >&2; exit 1 ;;
esac

if ! command -v vpk >/dev/null 2>&1; then
    echo "Installing vpk global tool..."
    dotnet tool install -g vpk
    export PATH="$PATH:$HOME/.dotnet/tools"
fi

echo "==> Cleaning previous publish output for $RID"
rm -rf "$PUBLISH_DIR"

echo "==> dotnet publish ($RID, self-contained)"
dotnet publish "$PROJECT" \
    -c Release \
    -r "$RID" \
    --self-contained true \
    -o "$PUBLISH_DIR" \
    /p:PublishSingleFile=false \
    /p:DebugType=embedded

echo "==> vpk pack"
mkdir -p "$RELEASE_DIR"
vpk pack \
    --packId "$PACK_ID" \
    --packVersion "$VERSION" \
    --packDir "$PUBLISH_DIR" \
    --mainExe "$MAIN_EXE" \
    --outputDir "$RELEASE_DIR" \
    --channel "$CHANNEL"

echo
echo "Done. Release artifacts written to: $RELEASE_DIR"
echo "  - Setup installer  (Windows: Setup.exe / Linux: .AppImage)"
echo "  - Release nupkg    (delta + full)"
echo "  - RELEASES file    (manifest used by the auto-updater)"

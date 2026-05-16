#!/usr/bin/env bash
# Build and pack VisionEditCV.Desktop with Velopack for a given RID.
# Usage: ./publish.sh <version> [rid] [--publish]
#   version    e.g. 1.0.0
#   rid        linux-x64 | win-x64 | osx-x64 | osx-arm64  (default: linux-x64)
#   --publish  optional: also upload to the GitHub release (requires gh CLI auth
#              or $GITHUB_TOKEN). Creates/updates tag v<version>.
set -euo pipefail

VERSION="${1:?version required, e.g. ./publish.sh 1.0.0 linux-x64}"
RID="${2:-linux-x64}"
PUBLISH_TO_GITHUB="no"
for arg in "$@"; do
    if [ "$arg" = "--publish" ]; then PUBLISH_TO_GITHUB="yes"; fi
done

PROJECT="src/VisionEditCV.Desktop/VisionEditCV.Desktop.csproj"
PUBLISH_DIR="publish/$RID"
RELEASE_DIR="releases/$RID"
PACK_ID="VisionEditCV"
REPO_URL="https://github.com/Luck-ai/Vision-Edit"

case "$RID" in
    win-x64)        MAIN_EXE="VisionEditCV.Desktop.exe"; CHANNEL="win" ;;
    linux-x64)      MAIN_EXE="VisionEditCV.Desktop";     CHANNEL="linux" ;;
    osx-x64|osx-arm64) MAIN_EXE="VisionEditCV.Desktop";  CHANNEL="osx" ;;
    *) echo "Unsupported RID: $RID" >&2; exit 1 ;;
esac

# Ensure the global tools dir is on PATH before checking — in a fresh shell
# it usually isn't, even though vpk was installed by a previous run.
export PATH="$PATH:$HOME/.dotnet/tools"
if ! command -v vpk >/dev/null 2>&1; then
    echo "Installing vpk global tool..."
    dotnet tool install -g vpk
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

if [ "$PUBLISH_TO_GITHUB" = "yes" ]; then
    : "${GITHUB_TOKEN:?GITHUB_TOKEN must be set to publish to GitHub Releases}"
    echo "==> Uploading $RID release to GitHub ($REPO_URL, tag v$VERSION)"
    vpk upload github \
        --repoUrl "$REPO_URL" \
        --outputDir "$RELEASE_DIR" \
        --tag "v$VERSION" \
        --channel "$CHANNEL" \
        --merge \
        --publish \
        --token "$GITHUB_TOKEN"
fi

echo
echo "Done. Release artifacts written to: $RELEASE_DIR"
echo "  - Setup installer  (Windows: Setup.exe / Linux: .AppImage)"
echo "  - Release nupkg    (delta + full)"
echo "  - RELEASES file    (manifest used by the auto-updater)"
if [ "$PUBLISH_TO_GITHUB" != "yes" ]; then
    echo
    echo "Re-run with --publish (and GITHUB_TOKEN set) to push to GitHub Releases."
fi

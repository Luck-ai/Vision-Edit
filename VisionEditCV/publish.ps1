#!/usr/bin/env pwsh
# Build and pack VisionEditCV.Desktop with Velopack.
# Defaults are tuned for Windows (Setup.exe with Start Menu + Desktop shortcuts).
# Usage:
#   ./publish.ps1 -Version 1.0.0                    # win-x64 (default)
#   ./publish.ps1 -Version 1.0.0 -Rid linux-x64     # AppImage
#   ./publish.ps1 -Version 1.0.0 -Rid osx-arm64     # macOS pkg
#   ./publish.ps1 -Version 1.0.0 -PublishGithub     # also upload to GitHub Releases
#                                                   # (requires $env:GITHUB_TOKEN)
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$Version,
    [ValidateSet('win-x64','linux-x64','osx-x64','osx-arm64')]
    [string]$Rid = 'win-x64',
    [switch]$PublishGithub
)

$ErrorActionPreference = 'Stop'

$project    = 'src/VisionEditCV.Desktop/VisionEditCV.Desktop.csproj'
$publishDir = "publish/$Rid"
$releaseDir = "releases/$Rid"
$packId     = 'VisionEditCV'
$packTitle  = 'VisionEditCV'
$packAuthor = 'VisionEditCV'
$iconPath   = 'src/VisionEditCV.Desktop/Assets/avalonia-logo.ico'
$repoUrl    = 'https://github.com/Luck-ai/Vision-Edit'

$mainExe, $channel = switch ($Rid) {
    'win-x64'   { 'VisionEditCV.Desktop.exe', 'win' }
    'linux-x64' { 'VisionEditCV.Desktop',     'linux' }
    default     { 'VisionEditCV.Desktop',     'osx' }
}

# Ensure the global tools dir is on PATH before checking — in a fresh shell
# it usually isn't, even though vpk may already be installed.
$toolsDir = if ($IsWindows -or $null -eq $IsWindows) { "$env:USERPROFILE\.dotnet\tools" } else { "$HOME/.dotnet/tools" }
$sep = [System.IO.Path]::PathSeparator
if (-not ($env:PATH -split [regex]::Escape($sep) | Where-Object { $_ -eq $toolsDir })) {
    $env:PATH = "$env:PATH$sep$toolsDir"
}
if (-not (Get-Command vpk -ErrorAction SilentlyContinue)) {
    Write-Host 'Installing vpk global tool...'
    dotnet tool install -g vpk
    if ($LASTEXITCODE -ne 0) { throw "dotnet tool install -g vpk failed" }
}

Write-Host "==> Cleaning previous publish output for $Rid"
if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }

Write-Host "==> dotnet publish ($Rid, self-contained)"
dotnet publish $project `
    -c Release `
    -r $Rid `
    --self-contained true `
    -o $publishDir `
    /p:PublishSingleFile=false `
    /p:DebugType=embedded
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

Write-Host '==> vpk pack'
New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null

$vpkArgs = @(
    'pack',
    '--packId',      $packId,
    '--packVersion', $Version,
    '--packTitle',   $packTitle,
    '--packAuthors', $packAuthor,
    '--packDir',     $publishDir,
    '--mainExe',     $mainExe,
    '--outputDir',   $releaseDir,
    '--channel',     $channel
)

# Windows-specific installer polish: icon + Start Menu / Desktop shortcuts.
if ($Rid -eq 'win-x64' -and (Test-Path $iconPath)) {
    $vpkArgs += @('--icon', $iconPath, '--shortcuts', 'StartMenu,Desktop')
}

vpk @vpkArgs
if ($LASTEXITCODE -ne 0) { throw "vpk pack failed" }

if ($PublishGithub) {
    if (-not $env:GITHUB_TOKEN) {
        throw "GITHUB_TOKEN env var must be set to upload to GitHub Releases"
    }
    Write-Host "==> Uploading $Rid release to GitHub ($repoUrl, tag v$Version)"
    vpk upload github `
        --repoUrl   $repoUrl `
        --outputDir $releaseDir `
        --tag       "v$Version" `
        --channel   $channel `
        --merge `
        --publish `
        --token     $env:GITHUB_TOKEN
    if ($LASTEXITCODE -ne 0) { throw "vpk upload github failed" }
}

Write-Host ''
Write-Host "Done. Release artifacts written to: $releaseDir"
if ($Rid -eq 'win-x64') {
    Write-Host '  - VisionEditCV-win-Setup.exe   (installer)'
    Write-Host '  - VisionEditCV-*-win-full.nupkg (release package)'
    Write-Host '  - RELEASES-win                  (update manifest)'
}
if (-not $PublishGithub) {
    Write-Host ''
    Write-Host 'Re-run with -PublishGithub (and $env:GITHUB_TOKEN set) to push to GitHub Releases.'
}

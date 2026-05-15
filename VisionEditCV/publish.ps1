#!/usr/bin/env pwsh
# Build and pack VisionEditCV.Desktop with Velopack.
# Defaults are tuned for Windows (Setup.exe with Start Menu + Desktop shortcuts).
# Usage:
#   ./publish.ps1 -Version 1.0.0                    # win-x64 (default)
#   ./publish.ps1 -Version 1.0.0 -Rid linux-x64     # AppImage
#   ./publish.ps1 -Version 1.0.0 -Rid osx-arm64     # macOS pkg
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$Version,
    [ValidateSet('win-x64','linux-x64','osx-x64','osx-arm64')]
    [string]$Rid = 'win-x64'
)

$ErrorActionPreference = 'Stop'

$project    = 'src/VisionEditCV.Desktop/VisionEditCV.Desktop.csproj'
$publishDir = "publish/$Rid"
$releaseDir = "releases/$Rid"
$packId     = 'VisionEditCV'
$packTitle  = 'VisionEditCV'
$packAuthor = 'VisionEditCV'
$iconPath   = 'src/VisionEditCV.Desktop/Assets/avalonia-logo.ico'

$mainExe, $channel = switch ($Rid) {
    'win-x64'   { 'VisionEditCV.Desktop.exe', 'win' }
    'linux-x64' { 'VisionEditCV.Desktop',     'linux' }
    default     { 'VisionEditCV.Desktop',     'osx' }
}

if (-not (Get-Command vpk -ErrorAction SilentlyContinue)) {
    Write-Host 'Installing vpk global tool...'
    dotnet tool install -g vpk
    $toolsDir = if ($IsWindows -or $null -eq $IsWindows) { "$env:USERPROFILE\.dotnet\tools" } else { "$HOME/.dotnet/tools" }
    $sep = [System.IO.Path]::PathSeparator
    $env:PATH = "$env:PATH$sep$toolsDir"
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

Write-Host ''
Write-Host "Done. Release artifacts written to: $releaseDir"
if ($Rid -eq 'win-x64') {
    Write-Host '  - VisionEditCV-win-Setup.exe   (installer)'
    Write-Host '  - VisionEditCV-*-win-full.nupkg (release package)'
    Write-Host '  - RELEASES-win                  (update manifest)'
}

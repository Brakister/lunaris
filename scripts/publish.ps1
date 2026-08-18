# Publishes Lunaris as a self-contained single-folder build and, when Inno Setup
# is available, builds the Windows installer.
# Also downloads bundled tools (yt-dlp, ffmpeg) so users don't need to download them at runtime.

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'Lunaris\Lunaris.csproj'
$outDir = Join-Path $root 'artifacts\publish'
$installerOut = Join-Path $root 'artifacts'
$toolsDir = Join-Path $outDir 'tools'

Write-Host '==> Publishing Lunaris (Release, self-contained, win-x64) ...'
dotnet publish $project -c Release -r win-x64 --self-contained true -o $outDir
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }

$exe = Join-Path $outDir 'Lunaris.exe'
if (-not (Test-Path $exe)) { throw "Expected output '$exe' not found." }

Write-Host "==> Published: $exe"

# --- Bundle yt-dlp and ffmpeg ---
Write-Host '==> Downloading bundled tools (yt-dlp, ffmpeg) ...'
New-Item -ItemType Directory -Path $toolsDir -Force | Out-Null

$ProgressPreference = 'SilentlyContinue'

# yt-dlp
$ytdlpPath = Join-Path $toolsDir 'yt-dlp.exe'
if (-not (Test-Path $ytdlpPath)) {
    Write-Host '    Downloading yt-dlp.exe ...'
    Invoke-WebRequest -Uri 'https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe' -OutFile $ytdlpPath -UseBasicParsing
    Write-Host "    yt-dlp.exe -> $ytdlpPath"
} else {
    Write-Host '    yt-dlp.exe already present, skipping.'
}

# ffmpeg
$ffmpegPath = Join-Path $toolsDir 'ffmpeg.exe'
if (-not (Test-Path $ffmpegPath)) {
    Write-Host '    Downloading ffmpeg ...'
    $ffmpegZip = Join-Path $toolsDir 'ffmpeg.zip'
    Invoke-WebRequest -Uri 'https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip' -OutFile $ffmpegZip -UseBasicParsing

    Write-Host '    Extracting ffmpeg.exe ...'
    $tempExtract = Join-Path $toolsDir '_ffmpeg_extract'
    Expand-Archive -Path $ffmpegZip -DestinationPath $tempExtract -Force

    $ffmpegExe = Get-ChildItem -Path $tempExtract -Recurse -Filter 'ffmpeg.exe' | Select-Object -First 1
    if ($ffmpegExe) {
        Copy-Item $ffmpegExe.FullName $ffmpegPath -Force
        Write-Host "    ffmpeg.exe -> $ffmpegPath"
    } else {
        throw 'ffmpeg.exe not found in the downloaded archive.'
    }

    Remove-Item -Path $tempExtract -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -Path $ffmpegZip -Force -ErrorAction SilentlyContinue
} else {
    Write-Host '    ffmpeg.exe already present, skipping.'
}

$ProgressPreference = 'Continue'
Write-Host '==> Tools bundled successfully.'

$iscc = Get-Command iscc -ErrorAction SilentlyContinue
if (-not $iscc) {
    foreach ($path in @(
        'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
        'C:\Program Files\Inno Setup 6\ISCC.exe',
        'C:\Program Files (x86)\Inno Setup 5\ISCC.exe')) {
        if (Test-Path $path) { $iscc = $path; break }
    }
}
if ($iscc) {
    $iss = Join-Path $root 'installer\Lunaris.iss'
    Write-Host '==> Building installer with Inno Setup ...'
    & $iscc $iss
    if ($LASTEXITCODE -ne 0) { throw 'Installer build failed.' }
    Write-Host "==> Installer written to: $installerOut"
}
else {
    Write-Warning 'Inno Setup (ISCC.exe) not found - skipping installer. Install it from https://jrsoftware.org/isinfo.php'
}

Write-Host 'Done.'

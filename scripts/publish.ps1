# Publishes Lunaris as a self-contained single-folder build and, when Inno Setup
# is available, builds the Windows installer.

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'Lunaris\Lunaris.csproj'
$outDir = Join-Path $root 'artifacts\publish'
$installerOut = Join-Path $root 'artifacts'

Write-Host '==> Publishing Lunaris (Release, self-contained, win-x64) ...'
dotnet publish $project -c Release -r win-x64 --self-contained true -o $outDir
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }

$exe = Join-Path $outDir 'Lunaris.exe'
if (-not (Test-Path $exe)) { throw "Expected output '$exe' not found." }

Write-Host "==> Published: $exe"

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

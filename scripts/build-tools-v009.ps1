# Builds the bundled toolchain exe (tools_v009.exe) from the sources in this repo.
#
# The app ships a tools_v009.exe so a user never has to install Python. The exe is a build artifact, not source: a clone
# builds it with this script, and without it the app falls back to the Python toolbelt in tools/ (see Toolchain.Choose).
#
#   .\scripts\build-tools-v009.ps1            # build, output to tools_v009\dist\tools_v009\
#   .\scripts\build-tools-v009.ps1 -Clean     # remove the previous dist first
#
# Needs PyInstaller (pip install pyinstaller). --onedir, not --onefile: onefile has to unpack itself into %TEMP% at every
# start, and on this machine Defender blocks that for a freshly built exe ("Could not create temporary directory!").

[CmdletBinding()]
param(
    [switch]$Clean
)

$ErrorActionPreference = 'Stop'

$app = Split-Path $PSScriptRoot -Parent
$source = Join-Path $app 'tools_v009'
$entry = Join-Path $source 'tools_v009.py'
$hashes = Join-Path $source 'tools_v009\hashes.txt'
$dist = Join-Path $source 'dist'

if (-not (Test-Path $entry)) { throw "No entry point at $entry" }
if (-not (Test-Path $hashes)) { throw "No hash table at $hashes" }

& python -c "import PyInstaller" 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) { throw "PyInstaller is not installed. Run: python -m pip install pyinstaller" }

if ($Clean -and (Test-Path $dist)) { Remove-Item $dist -Recurse -Force }

Write-Host "building tools_v009.exe from $source"
& python -m PyInstaller --onedir --name tools_v009 --noconfirm `
    --distpath $dist `
    --workpath (Join-Path $env:TEMP 'pyi-tools-v009-work') `
    --specpath (Join-Path $env:TEMP 'pyi-tools-v009-spec') `
    --add-data "$hashes;." `
    $entry

if ($LASTEXITCODE -ne 0) { throw "PyInstaller failed with exit $LASTEXITCODE" }

$exe = Join-Path $dist 'tools_v009\tools_v009.exe'
if (-not (Test-Path $exe)) { throw "Build finished but $exe is missing" }

$size = [math]::Round(((Get-ChildItem (Join-Path $dist 'tools_v009') -Recurse -File | Measure-Object -Property Length -Sum).Sum) / 1MB, 1)
Write-Host ""
Write-Host "built: $exe ($size MB)"
Write-Host "the app copies it to tools\v009 beside ModdingGUI.exe on the next build (see ModdingGUI.csproj)"

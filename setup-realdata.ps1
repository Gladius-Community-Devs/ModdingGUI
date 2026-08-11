<#
.SYNOPSIS
    Regenerates the REALDATA_* project fixture from the vanilla ISO using the app's
    own unpack pipeline (the same commands BatchContentBuilder.GenerateUnpackContent
    emits), and verifies the result is byte-identical to clean-bec-unpacked.

.DESCRIPTION
    The 16 RealGameDataTests run against a REALDATA_* project folder plus vanilla.iso
    in the build output. This script rebuilds that fixture "from scratch" so the tests
    are grounded in what the app actually produces from the vanilla ISO, not just a
    certified copy. It also hardlinks vanilla.iso into the build output.

    Pipeline (identical to the app's Unpacking tab):
      1. ngciso-tool.py  -unpack <iso> <project>_ISO <project>_FileList.txt
      2. bec-tool-all.py --platform GC -unpack <iso>/gladius.bec <project>_BEC\
      3. Gladius_Units_IDX_Unpack.py <project>_BEC\Data\

    Then the <project>_BEC is copied into the build output as
    bin/<Config>/net8.0-windows/win-x64/REALDATA_<name>/<name>_BEC/ and hash-verified
    against clean-bec-unpacked (0 files missing / 0 extra / 0 content diffs).

.PARAMETER Iso
    Path to the vanilla ISO. Default: <repo root>\vanilla_gamecube.iso.

.PARAMETER ProjectName
    Internal project name used for the staging project and the REALDATA folder name.
    The build-output folder becomes REALDATA_<ProjectName>. Default: vanilla.

.PARAMETER Configuration
    Which build output to install into (Debug or Release). Default: Debug.
    If a Release REALDATA_<name> directory junction already points at the Debug
    fixture, it is left alone (it reflects the Debug rebuild automatically).

.PARAMETER CleanBec
    Reference tree to verify against. Default: <repo root>\clean-bec-unpacked.
    Use -CleanBec '' or -SkipVerify to skip the byte-identical check.

.PARAMETER SkipVerify
    Skip the clean-bec-unpacked byte-identical verification.

.PARAMETER KeepStaging
    Keep the temporary staging project (for inspection) instead of deleting it.

.EXAMPLE
    .\setup-realdata.ps1                          # Debug, default paths
    .\setup-realdata.ps1 -Configuration Release
    .\setup-realdata.ps1 -Iso "D:\roms\gladius.iso" -ProjectName nightly
#>
[CmdletBinding()]
param(
    [string]$Iso = (Join-Path $PSScriptRoot "..\vanilla_gamecube.iso"),
    [string]$ProjectName = "vanilla",
    [string]$Configuration = "Debug",
    [string]$CleanBec = (Join-Path $PSScriptRoot "..\clean-bec-unpacked"),
    [switch]$SkipVerify,
    [switch]$KeepStaging
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path $PSScriptRoot -Parent
$BinRoot  = Join-Path $PSScriptRoot "bin\$Configuration\net8.0-windows\win-x64"

# Normalize to real paths (removes "..\" segments so Substring math in the
# verification step is correct).
$Iso      = [System.IO.Path]::GetFullPath($Iso)
$CleanBec = [System.IO.Path]::GetFullPath($CleanBec)
$BinRoot  = [System.IO.Path]::GetFullPath($BinRoot)

function Write-Step([string]$t) { Write-Host "" ; Write-Host ("== " + $t) -ForegroundColor Cyan }

# ---------------------------------------------------------------------------
# Resolve the toolchain: prefer the shipped copy in the build output (what the
# app actually runs), fall back to the source tools/ folder.
# ---------------------------------------------------------------------------
$tools = Join-Path $BinRoot "tools"
if (-not (Test-Path -LiteralPath (Join-Path $tools "bec-tool-all.py"))) { $tools = Join-Path $RepoRoot "tools" }
if (-not (Test-Path -LiteralPath (Join-Path $tools "bec-tool-all.py"))) {
    throw "tools/ not found. Build the app first (dotnet build ModdingGUI.sln) or fix the tools path."
}

if (-not (Test-Path -LiteralPath $Iso)) { throw "ISO not found: $Iso" }
if (-not $SkipVerify -and -not (Test-Path -LiteralPath $CleanBec)) { throw "clean-bec-unpacked not found: $CleanBec (use -SkipVerify to skip)" }
if (-not (Test-Path -LiteralPath $BinRoot)) {
    Write-Host "Build output missing ($BinRoot). Building solution first..." -ForegroundColor Yellow
    Push-Location $PSScriptRoot
    try { dotnet build "ModdingGUI.sln" -c $Configuration --nologo | Out-Host; if ($LASTEXITCODE -ne 0) { throw "build failed" } }
    finally { Pop-Location }
}

# ---------------------------------------------------------------------------
# Staging
# ---------------------------------------------------------------------------
$stageRoot = Join-Path $env:TEMP ("GladiusRealData-" + [guid]::NewGuid().ToString("N"))
$stageProj = Join-Path $stageRoot "project"
$becFolder = Join-Path $stageProj "$ProjectName`_BEC\"
$isoFolder = Join-Path $stageProj "$ProjectName`_ISO"
New-Item -ItemType Directory -Force -Path $stageProj | Out-Null
Write-Host "Staging: $stageProj" -ForegroundColor DarkGray

try {
    # -------------------------------------------------------------------------
    # Step 1: ISO filesystem dump (ngciso)
    # -------------------------------------------------------------------------
    Write-Step "Step 1/3: ngciso unpack (ISO filesystem)"
    & python (Join-Path $tools "ngciso-tool.py") -unpack $Iso $isoFolder "$ProjectName`_FileList.txt" 2>&1 | ForEach-Object { Write-Host $_ }
    if ($LASTEXITCODE -ne 0) { throw "ngciso unpack failed (exit $LASTEXITCODE)" }

    $becInput = Join-Path $isoFolder "gladius.bec"
    if (-not (Test-Path -LiteralPath $becInput)) { throw "gladius.bec not found after ngciso unpack: $becInput" }

    # -------------------------------------------------------------------------
    # Step 2: BEC breakdown (bec-tool-all). The output folder must end with a
    # trailing separator or the tool concatenates it onto every filename.
    # -------------------------------------------------------------------------
    Write-Step "Step 2/3: bec-tool-all unpack (BEC breakdown)"
    & python (Join-Path $tools "bec-tool-all.py") --platform GC -unpack $becInput $becFolder 2>&1 | ForEach-Object { Write-Host $_ }
    if ($LASTEXITCODE -ne 0) { throw "bec-tool-all unpack failed (exit $LASTEXITCODE)" }

    # -------------------------------------------------------------------------
    # Step 3: units IDX unpack (also requires a trailing separator)
    # -------------------------------------------------------------------------
    Write-Step "Step 3/3: units IDX unpack"
    $dataFolder = Join-Path $becFolder "Data"
    & python (Join-Path $tools "Gladius_Units_IDX_Unpack.py") "$dataFolder\" 2>&1 | ForEach-Object { Write-Host $_ }
    if ($LASTEXITCODE -ne 0) { throw "units IDX unpack failed (exit $LASTEXITCODE)" }

    $becFileCount = @(Get-ChildItem -LiteralPath $becFolder -Recurse -File).Count
    Write-Step "Unpack complete: $becFileCount files under $becFolder"

    # -------------------------------------------------------------------------
    # Verify byte-identical against clean-bec-unpacked
    # -------------------------------------------------------------------------
    if (-not $SkipVerify) {
        Write-Step "Verifying against $CleanBec"
        function Get-RelHash($basePath) {
            Get-ChildItem -LiteralPath $basePath -Recurse -File | ForEach-Object {
                $rel = $_.FullName.Substring($basePath.Length).TrimStart('\').Replace('/','\')
                "$rel|$($_.Length)|$((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash)"
            }
        }
        $ref = @{}; Get-RelHash $CleanBec | ForEach-Object { $p = $_ -split '\|'; $ref[$p[0]] = $_ }
        $new = @{}; Get-RelHash $becFolder | ForEach-Object { $p = $_ -split '\|'; $new[$p[0]] = $_ }
        $onlyRef  = @($ref.Keys  | Where-Object { -not $new.ContainsKey($_) })
        $onlyNew  = @($new.Keys  | Where-Object { -not $ref.ContainsKey($_) })
        $diffCont = @($ref.Keys  | Where-Object { $new.ContainsKey($_) -and $ref[$_] -ne $new[$_] })
        if ($onlyRef.Count -or $onlyNew.Count -or $diffCont.Count) {
            "  ref-only:  $($onlyRef.Count)";  $onlyRef  | Select-Object -First 10 | ForEach-Object { Write-Host "    - $_" -ForegroundColor Red }
            "  new-only:  $($onlyNew.Count)";  $onlyNew  | Select-Object -First 10 | ForEach-Object { Write-Host "    + $_" -ForegroundColor Red }
            "  content:   $($diffCont.Count)"; $diffCont | Select-Object -First 10 | ForEach-Object { Write-Host "    ~ $_" -ForegroundColor Red }
            throw "Fresh pipeline output does NOT match $CleanBec"
        }
        Write-Host "Byte-identical: $($ref.Count) files, 0 missing, 0 extra, 0 diffs." -ForegroundColor Green
    }

    # -------------------------------------------------------------------------
    # Install into the build output as a REALDATA_<name> project
    # -------------------------------------------------------------------------
    Write-Step "Installing into $BinRoot"
    $destRoot   = Join-Path $BinRoot ("REALDATA_" + $ProjectName)
    $destBec    = Join-Path $destRoot ($ProjectName + "_BEC")
    $destIsoDir = Join-Path $destRoot ($ProjectName + "_ISO")

    if ((Get-Item -LiteralPath $destRoot -ErrorAction SilentlyContinue).LinkType) {
        Write-Host "Removing existing junction at $destRoot" -ForegroundColor Yellow
        Remove-Item -LiteralPath $destRoot -Recurse -Force
    }
    if (Test-Path -LiteralPath $destRoot) { Remove-Item -LiteralPath $destRoot -Recurse -Force }

    New-Item -ItemType Directory -Force -Path $destRoot | Out-Null
    Copy-Item -LiteralPath $becFolder -Destination $destBec -Recurse
    New-Item -ItemType Directory -Force -Path $destIsoDir | Out-Null
    Write-Host "REALDATA project: $destRoot" -ForegroundColor Green

    # vanilla.iso hardlink for the fixture discovery (GameDataFixture.FindGameDataRoot)
    $isoLink = Join-Path $BinRoot "vanilla.iso"
    if (-not (Test-Path -LiteralPath $isoLink)) {
        New-Item -ItemType HardLink -Path $isoLink -Target $Iso | Out-Null
        Write-Host "vanilla.iso hardlink: $isoLink" -ForegroundColor Green
    } else {
        Write-Host "vanilla.iso already present: $isoLink" -ForegroundColor DarkGray
    }

    Write-Step "Done. Run:"
    Write-Host "  .\run-tests.ps1 -Filter RealGameDataTests" -ForegroundColor Cyan
}
finally {
    if (-not $KeepStaging -and (Test-Path -LiteralPath $stageRoot)) {
        Remove-Item -LiteralPath $stageRoot -Recurse -Force
    }
}

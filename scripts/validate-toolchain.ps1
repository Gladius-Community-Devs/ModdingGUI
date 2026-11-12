# Validates a toolchain end to end: vanilla ISO -> unpack -> (no edits) -> pack, then checks the invariants.
#
#   .\scripts\validate-toolchain.ps1 -Iso <vanilla.iso> [-Tool python|exe] [-Exe <tools_v009.exe>] [-Python <python>]
#
# Checks, in order of how much they prove:
#   1. the repacked gladius.bec is BYTE-IDENTICAL to the one taken out of the ISO (this is the strong claim)
#   2. the rebuilt ISO's MD5 equals the vanilla ISO's (byte-for-byte round trip of the whole disc)
#   3. the six generated data/units/*.txt files are NOT in the packed archive
#   4. the unpacked tree matches a reference unpack when a reference is supplied
#
# Everything happens in a temp folder; the input ISO is only read.

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Iso,
    [ValidateSet('python', 'exe')][string]$Tool = 'python',
    [string]$Exe,
    [string]$Python = 'python',
    [string]$ToolsDir,                      # folder holding tools_v009.py (python mode)
    [string]$ReferenceBec,                  # optional: a known-good unpacked BEC tree
    [switch]$Keep,                          # leave the work folder for inspection
    [string]$WorkRoot = $env:TEMP           # parent for the scratch folder; the tool must be able to write here
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $Iso)) { throw "No ISO at $Iso" }
$Iso = (Resolve-Path $Iso).Path

$work = Join-Path $WorkRoot ("validate-" + [Guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Path $work | Out-Null
Write-Host "work: $work"

function Invoke-Tool {
    param([string[]]$Arguments)

    if ($Tool -eq 'exe') {
        & $Exe @Arguments
    }
    else {
        & $Python (Join-Path $ToolsDir 'tools_v009.py') @Arguments
    }

    if ($LASTEXITCODE -ne 0) { throw "tool failed (exit $LASTEXITCODE): $($Arguments -join ' ')" }
}

function Md5([string]$Path) { (Get-FileHash $Path -Algorithm MD5).Hash.ToLower() }

$results = [ordered]@{}
$failures = New-Object System.Collections.Generic.List[string]

try {
    $isoDir = Join-Path $work 'iso'
    $becDir = Join-Path $work 'bec'
    New-Item -ItemType Directory -Path $isoDir, $becDir | Out-Null

    # --- unpack -------------------------------------------------------------
    Write-Host "`n[1/6] iso unpack"
    $filelist = Join-Path $work 'iso-filelist.txt'
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    Invoke-Tool @('iso', 'unpack', $Iso, "$isoDir/", $filelist)
    Write-Host ("      {0:0.0}s" -f $sw.Elapsed.TotalSeconds)

    $originalBec = Join-Path $isoDir 'gladius.bec'
    if (-not (Test-Path $originalBec)) { throw "iso unpack produced no gladius.bec" }
    $originalBecMd5 = Md5 $originalBec

    Write-Host "[2/6] bec unpack"
    $sw.Restart()
    Invoke-Tool @('bec', 'unpack', $originalBec, "$becDir/", '--platform', 'GC')
    Write-Host ("      {0:0.0}s" -f $sw.Elapsed.TotalSeconds)

    Write-Host "[3/6] idx unpack (generates data/units/*.txt)"
    Invoke-Tool @('idx', 'unpack', "$(Join-Path $becDir 'data')/")

    $unitsTxt = Get-ChildItem (Join-Path $becDir 'data\units') -Filter '*.txt' -ErrorAction SilentlyContinue
    $results['units txt generated'] = "$($unitsTxt.Count) files"
    if ($unitsTxt.Count -lt 6) { $failures.Add("expected 6 generated unit tables, found $($unitsTxt.Count)") }

    if ($ReferenceBec) {
        Write-Host "[4/6] compare unpacked tree with reference (content hashes)"
        $reference = (Resolve-Path $ReferenceBec).Path
        $refFiles = Get-ChildItem $reference -Recurse -File | Where-Object { $_.FullName -notmatch '\\\.mod-history\\' }
        $newFiles = Get-ChildItem $becDir -Recurse -File

        # Compare CONTENT, not paths: since v008.3 unresolved files get descriptive names, so an unpacked tree can have
        # identical bytes under different names than a v007 unpack. Path differences are reported, content differences fail.
        $refByName = @{}
        foreach ($file in $refFiles) { $refByName[$file.FullName.Substring($reference.Length + 1).Replace('\', '/')] = (Md5 $file.FullName) }
        $newByName = @{}
        foreach ($file in $newFiles) { $newByName[$file.FullName.Substring($becDir.Length + 1).Replace('\', '/')] = (Md5 $file.FullName) }

        $refHashes = $refByName.Values | Sort-Object
        $newHashes = $newByName.Values | Sort-Object
        $refOnly = Compare-Object $refHashes $newHashes -PassThru | Where-Object { $_.SideIndicator -eq '<=' }
        $newOnly = Compare-Object $refHashes $newHashes -PassThru | Where-Object { $_.SideIndicator -eq '=>' }

        $renamed = @($newByName.Keys | Where-Object { -not $refByName.ContainsKey($_) })
        $contentsDiffer = ($refOnly.Count -gt 0 -or $newOnly.Count -gt 0)

        $results['files in reference'] = $refByName.Count
        $results['files in this unpack'] = $newByName.Count
        $results['paths only in reference'] = $renamed.Count
        $results['content hashes only in reference'] = $refOnly.Count
        $results['content hashes only in this unpack'] = $newOnly.Count

        if ($contentsDiffer) {
            $failures.Add("$($refOnly.Count) file contents missing and $($newOnly.Count) unexpected vs reference")
        }
        elseif ($renamed.Count -gt 0) {
            Write-Host "      note: $($renamed.Count) paths differ but every content hash matches (descriptive renames)"
        }
    }

    Write-Host "[5/6] idx repack + bec pack (no edits made)"
    $sw.Restart()
    Invoke-Tool @('idx', 'repack', "$(Join-Path $becDir 'data')/")
    $repackedBec = Join-Path $work 'gladius-repacked.bec'
    Invoke-Tool @('bec', 'pack', "$becDir/", $repackedBec, (Join-Path $becDir 'filelist.txt'), '--platform', 'GC')
    Write-Host ("      {0:0.0}s" -f $sw.Elapsed.TotalSeconds)

    $repackedMd5 = Md5 $repackedBec
    $results['gladius.bec from ISO'] = "$originalBecMd5  ($([math]::Round((Get-Item $originalBec).Length / 1MB, 0)) MB)"
    $results['gladius.bec repacked'] = "$repackedMd5  ($([math]::Round((Get-Item $repackedBec).Length / 1MB, 0)) MB)"
    $results['bec byte-identical'] = ($originalBecMd5 -eq $repackedMd5)
    if ($originalBecMd5 -ne $repackedMd5) { $failures.Add('repacked gladius.bec is NOT byte-identical to the original') }

    # --- units txt must not be in the packed archive ---------------------------------
    Write-Host "[6/6] iso pack"
    Copy-Item $repackedBec (Join-Path $isoDir 'gladius.bec') -Force
    $outIso = Join-Path $work 'rebuilt.iso'
    $fst = Join-Path $isoDir 'fst.bin'
    $sw.Restart()
    Invoke-Tool @('iso', 'pack', "$isoDir/", $fst, $filelist, $outIso)
    Write-Host ("      {0:0.0}s" -f $sw.Elapsed.TotalSeconds)

    $isoMd5 = Md5 $outIso
    $vanillaMd5 = Md5 $Iso
    $results['vanilla ISO md5'] = $vanillaMd5
    $results['rebuilt ISO md5'] = $isoMd5
    $results['ISO byte-identical'] = ($isoMd5 -eq $vanillaMd5)
    if ($isoMd5 -ne $vanillaMd5) { $failures.Add('rebuilt ISO is NOT byte-identical to the vanilla ISO') }

    # The pack writes its own manifest; use it to prove the generated tables stayed out.
    $manifest = Get-ChildItem $work -Recurse -File -Filter 'repack-filelist.txt' | Select-Object -First 1
    $becEntries = @()
    if ($manifest) { $becEntries = Get-Content $manifest.FullName | Select-Object -Skip 1 | ForEach-Object { ($_ -split ',')[0] } }
    else {
        # Fall back to the file list the unpack wrote: entry names are what the archive stores.
        $becEntries = Get-Content (Join-Path $becDir 'filelist.txt') | Select-Object -Skip 1 | ForEach-Object { ($_ -split ',')[0] }
    }

    $leaked = $becEntries | Where-Object { $_ -match '^data/units/.*\.txt$' }
    $results['units/*.txt in packed archive'] = $leaked.Count
    if ($leaked.Count -gt 0) { $failures.Add("$($leaked.Count) generated unit tables ended up in the archive: $($leaked -join ', ')") }
}
catch {
    $failures.Add("validation aborted: $($_.Exception.Message)")
}
finally {
    Write-Host "`n=== results ==="
    foreach ($key in $results.Keys) { "{0,-32} {1}" -f $key, $results[$key] }

    if ($failures.Count -gt 0) {
        Write-Host "`n=== FAILURES ===" -ForegroundColor Red
        foreach ($failure in $failures) { Write-Host " - $failure" -ForegroundColor Red }
    }
    else {
        Write-Host "`nALL CHECKS PASSED" -ForegroundColor Green
    }

    if ($Keep) { Write-Host "`nkept: $work" } else { Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue }
}

if ($failures.Count -gt 0) { exit 1 }
exit 0

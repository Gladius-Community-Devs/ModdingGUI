# Measures the pack chain's steps, separately from the GUI.
#
# The point of running this outside the app is attribution: the same pack timed through the app and timed here differ by
# whatever the log plumbing costs, and a cold run here is the baseline the app has to live with.
#
# Usage:
#   .\scripts\bench-pack.ps1 -Project "<...>\demo_hero"            # one measured run
#   .\scripts\bench-pack.ps1 -Project "..." -Runs 3               # three runs: cold-ish, then warm
#   .\scripts\bench-pack.ps1 -Project "..." -ThroughApp           # drive the app instead (needs it running with --ui-server)
#
# For a genuinely cold number, reboot (or drop the standby list) first: the second run in a session is always served from
# the OS cache, which is why the app showed 97.9 s and then 13.8 s for identical inputs.

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Project,
    [int]$Runs = 1,
    [switch]$ThroughApp,
    [int]$Port = 4572
)

$ErrorActionPreference = 'Stop'

$bec = Get-ChildItem $Project -Directory -Filter '*_BEC' | Select-Object -First 1
if (-not $bec) { throw "No *_BEC folder in $Project" }

$iso = Get-ChildItem $Project -Directory -Filter '*_ISO' | Select-Object -First 1
if (-not $iso) { throw "No *_ISO folder in $Project" }

$tools = Join-Path (Split-Path $PSScriptRoot -Parent) 'bin\Debug\net8.0-windows\win-x64\tools'
$becTool = Join-Path $tools 'bec-tool-all.py'
$fileList = Join-Path $bec.FullName 'filelist.txt'
if (-not (Test-Path $becTool)) { throw "No bec-tool-all.py at $becTool" }
if (-not (Test-Path $fileList)) { throw "No filelist.txt in $($bec.FullName)" }

$fileCount = (Get-ChildItem $bec.FullName -Recurse -File | Measure-Object).Count
$sourceMb = [math]::Round(((Get-ChildItem $bec.FullName -Recurse -File | Measure-Object -Property Length -Sum).Sum) / 1MB, 0)
$existingBec = Join-Path $iso.FullName 'gladius.bec'
$becMb = if (Test-Path $existingBec) { [math]::Round((Get-Item $existingBec).Length / 1MB, 0) } else { 0 }

Write-Host "project   : $Project"
Write-Host "bec source: $fileCount files, $sourceMb MB"
Write-Host "bec built : $becMb MB"
Write-Host ""

for ($run = 1; $run -le $Runs; $run++) {
    $watch = [System.Diagnostics.Stopwatch]::StartNew()

    if ($ThroughApp) {
        # The app renders each line into a RichTextBox; this is the path that used to dominate.
        $job = & (Join-Path $PSScriptRoot 'ui-drive.ps1') '{"cmd":"click","name":"btnPack"}' -Port $Port -Quiet | ConvertFrom-Json
        $id = $job.result.job.id
        while ($true) {
            Start-Sleep -Seconds 3
            $jobs = & (Join-Path $PSScriptRoot 'ui-drive.ps1') '{"cmd":"jobs"}' -Port $Port -Quiet | ConvertFrom-Json
            if ($jobs.result.active.Count -eq 0) {
                $done = $jobs.result.recent | Where-Object { $_.id -eq $id } | Select-Object -First 1
                foreach ($step in $done.steps) { Write-Host ("  {0,-32} {1,7:0.0}s" -f $step.name, $step.seconds) }
                break
            }
        }
    }
    else {
        # Output to a file, never through the GUI: this is the tool's own cost.
        $log = Join-Path $env:TEMP "bench-pack-$run.log"
        & python $becTool -pack $bec.FullName (Join-Path $iso.FullName 'gladius_bench.bec') $fileList --platform GC *> $log
    }

    $watch.Stop()
    Write-Host ("run {0}: {1:0.0}s{2}" -f $run, $watch.Elapsed.TotalSeconds, $(if ($ThroughApp) { ' (through the app)' } else { ' (tool only)' }))
}

$bench = Join-Path $iso.FullName 'gladius_bench.bec'
if (Test-Path $bench) { Remove-Item $bench -Force }

Write-Host ""
Write-Host "Notes: a cold run is dominated by opening and reading $fileCount files plus writing the archive; a warm run is"
Write-Host "several times faster because the OS has the tree cached. The structural fix is an incremental repack (reuse the"
Write-Host "unchanged entries from the existing .bec and only read what changed), which would make even a cold run seconds."

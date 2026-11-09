<#
.SYNOPSIS
    Restore the Medium integrity label on this workspace and its build output.

.DESCRIPTION
    DSH's Windows sandbox labels every writable workspace directory with a Low mandatory integrity label
    (S-1-16-4096, no-write-up, inherited by children). Windows derives a process's integrity from the image file it was
    started from, so a binary built inside a labelled workspace runs at Low integrity even when the user launches it
    normally - and a Low process may not drive the medium-integrity shell, which is what made "Open containing folder"
    fail with Access denied while the same call succeeded everywhere else.

    The sandbox keeps workspace grants on purpose (its cross-session reuse cache), so the label returns whenever a
    session provisions this folder again in a confining mode. Run this after such a session, then rebuild.

    details: docs/WINDOWS_GOTCHAS.md

.PARAMETER Path
    Root to relabel. Defaults to the parent of this script (the workspace).

.PARAMETER WhatIf
    Report the current labels and the planned command without changing anything.
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    # The script sits in the git repo (ModdingGUI/); the labelled workspace is its parent.
    [string] $Path = (Split-Path -Parent (Split-Path -Parent $PSCommandPath))
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $Path).Path

function Get-Label([string] $target) {
    $line = icacls $target 2>$null | Select-String -Pattern 'Mandatory Label\\(\w+) Mandatory Level' | Select-Object -First 1
    if (-not $line) { return 'none' }
    if ($line.ToString() -match 'Low') { return 'Low' }
    if ($line.ToString() -match 'Medium') { return 'Medium' }
    if ($line.ToString() -match 'High') { return 'High' }
    return 'other'
}

$exe = Join-Path $root 'ModdingGUI\bin\Debug\net8.0-windows\win-x64\ModdingGUI.exe'
Write-Host "workspace : $root"
Write-Host "  label   : $(Get-Label $root)"
if (Test-Path -LiteralPath $exe) {
    Write-Host "  app     : $(Get-Label $exe)  ($exe)"
}

if ($PSCmdlet.ShouldProcess($root, 'set integrity level to Medium (OI)(CI), recursively')) {
    $output = icacls $root /setintegritylevel '(OI)(CI)Medium' /T /C 2>&1
    $failed = @($output | Select-String -Pattern 'Failed processing' )
    Write-Host "  label   : $(Get-Label $root) after"

    if ($failed.Count -gt 0) {
        Write-Warning "$($failed.Count) item(s) could not be relabelled (usually files in use)."
        $failed | Select-Object -First 10 | ForEach-Object { Write-Warning $_.ToString().Trim() }
    }

    if (Test-Path -LiteralPath $exe) {
        Write-Host "  app     : $(Get-Label $exe) after"
        if ((Get-Label $exe) -ne 'Medium') {
            Write-Warning 'The executable is still not Medium: rebuild it, or copy the build output to an unlabelled folder.'
        }
    }
}

# Restart the app under the UI driver, on a chosen monitor.
#
# QA needs the same three steps after every build: stop only the instance a test started, rebuild, relaunch with
# --ui-server, and put the window somewhere it will not cover whatever the user is doing. Stopping by PID (not by
# name) keeps a user's own window alive.
#
#   .\restart-app.ps1 -Port 4572 -Monitor '\\.\DISPLAY2'

[CmdletBinding()]
param(
    [int]$Port = 4572,
    [string]$Monitor,
    [string]$Configuration = 'Debug',
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Set-Location $root

# Only instances this script (or the driver) started are recorded, by pid.
$stateFile = Join-Path $env:TEMP 'gladius-qa-app.pid'
if (Test-Path $stateFile) {
    $oldPid = Get-Content $stateFile
    $old = Get-Process -Id $oldPid -ErrorAction SilentlyContinue
    if ($old) { $old | Stop-Process -Force; Start-Sleep -Milliseconds 700 }
    Remove-Item $stateFile -ErrorAction SilentlyContinue
}

if (-not $NoBuild) {
    $build = & dotnet build ModdingGUI.sln -c $Configuration 2>&1
    $built = $build | Select-String -Pattern 'Build succeeded|Build FAILED'
    Write-Output ($built | ForEach-Object { $_.Line.Trim() })
    if ($build | Select-String -Pattern ' error ') { throw "build failed" }
}

$exe = Join-Path $root "bin\$Configuration\net8.0-windows\win-x64\ModdingGUI.exe"
$app = Start-Process -FilePath $exe -ArgumentList '--ui-server', $Port -PassThru
$app.Id | Set-Content $stateFile
Write-Output "started pid=$($app.Id) exe=$exe"

$deadline = (Get-Date).AddSeconds(30)
while ((Get-Date) -lt $deadline) {
    try {
        $client = New-Object System.Net.Sockets.TcpClient
        $client.Connect('127.0.0.1', $Port)
        $client.Dispose()
        break
    }
    catch { Start-Sleep -Milliseconds 300 }
}

Start-Sleep -Milliseconds 600

if ($Monitor) {
    $body = @{ cmd = 'window'; monitor = $Monitor } | ConvertTo-Json -Compress
    & (Join-Path $root 'scripts\ui-drive.ps1') $body -Port $Port -Quiet
}

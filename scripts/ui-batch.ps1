# Batch UI driver client: one connection, many commands.
#
# ui-drive.ps1 opens a connection per invocation, which costs a process start (~0.5s) plus a socket
# each time - fine for one command, painful for a QA sweep of a hundred. This one connects once and
# streams a file of JSON commands, writing one response line per command so results can be correlated
# by position.
#
#   .\ui-batch.ps1 -In cmds.jsonl -Out responses.jsonl -Port 4572
#
# Every line of -In is sent as-is; blank lines and lines starting with '#' are skipped. The response
# file gets one JSON line per sent command, in order.

[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$In,
    [Parameter(Mandatory)][string]$Out,
    [int]$Port = 4572,
    [int]$TimeoutMs = 0
)

$ErrorActionPreference = 'Stop'

$commands = @(Get-Content $In | Where-Object { $_.Trim().Length -gt 0 -and -not $_.Trim().StartsWith('#') })
if ($commands.Count -eq 0) { throw "no commands in $In" }

$client = New-Object System.Net.Sockets.TcpClient
$client.Connect('127.0.0.1', $Port)
$stream = $client.GetStream()
$writer = New-Object System.IO.StreamWriter($stream)
$writer.AutoFlush = $true
$reader = New-Object System.IO.StreamReader($stream)

$responses = New-Object System.Collections.Generic.List[string]
$timings = New-Object System.Collections.Generic.List[object]
$failures = 0

try {
    foreach ($line in $commands) {
        $watch = [Diagnostics.Stopwatch]::StartNew()
        $writer.WriteLine($line)

        $task = $reader.ReadLineAsync()
        if ($TimeoutMs -gt 0) {
            if (-not $task.Wait($TimeoutMs)) { throw "timed out after ${TimeoutMs}ms on: $line" }
        }
        else {
            $task.Wait()
        }

        $response = $task.Result
        if ($null -eq $response) { throw "the app closed the connection on: $line" }

        $responses.Add($response)
        if ($response -notmatch '^\{"ok":true') { $failures++ }
        $timings.Add([pscustomobject]@{ Index = $responses.Count; Ms = [math]::Round($watch.Elapsed.TotalMilliseconds); Command = $line; Ok = ($response -match '^\{"ok":true') })
    }
}
finally {
    $writer.Dispose()
    $reader.Dispose()
    $stream.Dispose()
    $client.Dispose()
    $responses | Set-Content -Path $Out -Encoding UTF8
    $timings | Export-Csv -NoTypeInformation -Encoding UTF8 ($Out -replace '\.jsonl$', '.timing.csv')
}

Write-Output "sent=$($commands.Count) failed=$failures out=$Out"

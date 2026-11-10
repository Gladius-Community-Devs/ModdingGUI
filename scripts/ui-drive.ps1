# Send UI commands to a running ModdingGUI instance started with --ui-server.
#
#   .\ui-drive.ps1 '{"cmd":"ping"}'
#   .\ui-drive.ps1 '{"cmd":"snapshot"}' | ConvertFrom-Json
#   .\ui-drive.ps1 -File .\my-scenario.txt          # one command per line, blank lines and # comments ignored
#   .\ui-drive.ps1 -File .\my-scenario.txt -Json    # raw JSON responses, one per line
#
# The app must already be running with the driver attached:
#   ModdingGUI.exe --ui-server 4571
#
# Protocol: one JSON request per line, one JSON response per line. Everything runs on the UI thread of the app,
# through the real controls, so an automated run exercises the same code path a user does.

[CmdletBinding()]
param(
    [Parameter(Position = 0)][string]$Command,
    [string]$File,
    [int]$Port = 4571,
    # 0 means "wait for the response, however long the work takes" — the driver answers when the job finishes, so
    # there is nothing to poll and no deadline to guess. Pass a positive value only when you want a deadline.
    [int]$TimeoutMs = 0,
    [switch]$Json,
    [switch]$Quiet
)

$ErrorActionPreference = 'Stop'

function Send-UiCommand {
    param([System.IO.StreamWriter]$Writer, [System.IO.StreamReader]$Reader, [string]$Line)

    $Writer.WriteLine($Line)
    $Writer.Flush()

    $task = $Reader.ReadLineAsync()
    if ($TimeoutMs -gt 0) {
        if (-not $task.Wait($TimeoutMs)) {
            throw "timed out after ${TimeoutMs}ms waiting for a response to: $Line"
        }
    }
    else {
        $task.Wait()   # parameterless Wait() returns void; the response is the only thing that unblocks it
    }

    if ($null -eq $task.Result) {
        throw 'the app closed the connection (did it exit?) while waiting for a response to: ' + $Line
    }

    return $task.Result
}

if (-not $File -and -not $Command) {
    throw 'Pass a JSON command, or -File <path>. See the header of this script.'
}

$lines = if ($File) {
    Get-Content -LiteralPath $File | Where-Object { $_.Trim() -ne '' -and -not $_.Trim().StartsWith('#') } | ForEach-Object { $_.Trim() }
} else {
    @($Command.Trim())
}

$client = New-Object System.Net.Sockets.TcpClient
try {
    $client.Connect('127.0.0.1', $Port)
    $stream = $client.GetStream()
    $writer = New-Object System.IO.StreamWriter($stream, (New-Object System.Text.UTF8Encoding($false)))
    $writer.NewLine = "`n"
    $reader = New-Object System.IO.StreamReader($stream, [System.Text.Encoding]::UTF8)

    $responses = @()
    foreach ($line in $lines) {
        # wait is client-side so the app's UI thread stays free to paint while we sleep.
        if ($line -match '"cmd"\s*:\s*"wait"') {
            $ms = if ($line -match '"ms"\s*:\s*(\d+)') { [int]$Matches[1] } else { 250 }
            if (-not $Quiet) { Write-Host "> wait ${ms}ms" -ForegroundColor DarkGray }
            Start-Sleep -Milliseconds $ms
            $responses += '{"ok":true,"result":{"waited":' + $ms + '}}'
            continue
        }

        if (-not $Quiet) { Write-Host "> $line" -ForegroundColor DarkGray }
        $response = Send-UiCommand -Writer $writer -Reader $reader -Line $line
        $responses += $response

        if (-not $Quiet) {
            if ($Json) {
                Write-Output $response
            } else {
                try {
                    $parsed = $response | ConvertFrom-Json
                    if ($parsed.ok) {
                        $pretty = $parsed.result | ConvertTo-Json -Depth 12 -Compress
                        if ($pretty.Length -gt 400) { $pretty = $pretty.Substring(0, 400) + '…' }
                        Write-Host "< ok $pretty" -ForegroundColor Green
                    } else {
                        Write-Host "< FAIL $($parsed.error)" -ForegroundColor Red
                    }
                } catch {
                    Write-Host "< $response" -ForegroundColor Yellow
                }
            }
        }

        # Stop early when the app said it is closing, otherwise the next write would block on a dead socket.
        if ($line -match '"quit"') { break }
    }

    if ($Quiet) { $responses | ForEach-Object { Write-Output $_ } }
}
finally {
    $client.Dispose()
}

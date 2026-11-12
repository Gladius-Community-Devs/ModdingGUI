<#
.SYNOPSIS
    Raw-wire smoke test for the MCP server: spawn the exe, speak JSON-RPC on its stdin, read frames
    from its stdout, and assert the protocol behaviour without any client library.

.DESCRIPTION
    This is the test that catches the failures a client library hides:

      * stdout pollution — every line the process writes to stdout must be one JSON-RPC frame. A stray
        Console.WriteLine, a library banner or a --help echo would show up here as a non-JSON line.
      * framing regressions — one frame per line, terminated by a newline, no Content-Length headers.
      * lifecycle — initialize, notifications/initialized, ping, tools/list, tools/call and
        notifications/cancelled all behave, and the process exits cleanly when stdin closes.

    Responses may arrive in any order (legal JSON-RPC), so a frame is matched by id, never by position.

    It stages a small project from the read-only vanilla corpus into a temp folder, because a smoke test
    must never write to clean-bec-unpacked.

.PARAMETER Exe
    Path to ModdingGUI.Mcp.exe. Defaults to the Debug build in this repo.

.PARAMETER Project
    An existing project to run against. Defaults to a temp folder staged from -Corpus.

.PARAMETER Corpus
    The read-only vanilla unpack. Defaults to ..\..\clean-bec-unpacked beside the repo.

.PARAMETER KeepProject
    Leave the staged project in place (it holds the .mod-history backup this run created).

.EXAMPLE
    .\scripts\mcp-smoke.ps1

.EXAMPLE
    .\scripts\mcp-smoke.ps1 -Exe .\ModdingGUI.Mcp\bin\Release\net8.0\ModdingGUI.Mcp.exe -KeepProject
#>
[CmdletBinding()]
param(
    [string]$Exe,
    [string]$Project,
    [string]$Corpus,
    [int]$TimeoutMs = 30000,
    [switch]$KeepProject
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (-not $Exe) { $Exe = Join-Path $repoRoot 'ModdingGUI.Mcp\bin\Debug\net8.0\ModdingGUI.Mcp.exe' }
if (-not $Corpus) { $Corpus = (Resolve-Path (Join-Path $repoRoot '..\clean-bec-unpacked')).Path }

$failures = 0
function Pass([string]$what) { Write-Host "  PASS  $what" -ForegroundColor Green }
function Fail([string]$what, [string]$detail) {
    $script:failures++
    Write-Host "  FAIL  $what" -ForegroundColor Red
    if ($detail) { Write-Host "        $detail" -ForegroundColor DarkGray }
}
function Kill-Tree($process) {
    try { & taskkill /PID $process.Id /T /F *> $null } catch { }
    try { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue } catch { }
}

if (-not (Test-Path $Exe)) {
    Write-Host "MCP exe not found: $Exe" -ForegroundColor Red
    Write-Host "Build it first: dotnet build ModdingGUI.Mcp" -ForegroundColor DarkGray
    exit 2
}

# ---- stage a project -------------------------------------------------------
$staged = $false
if (-not $Project) {
    $Project = Join-Path ([System.IO.Path]::GetTempPath()) "gladius-mcp-smoke-$PID"
    if (Test-Path $Project) { Remove-Item -Recurse -Force $Project }

    $relativeFiles = @(
        'data/config/items.tok'
        'data/config/skills.tok'
        'data/config/classdefs.tok'
        'data/config/lookuptext_eng.txt'
        'data/config/world.tok'
        'data/units/gladiators.txt'
        'data/units/statsets.txt'
        'data/units/skillsets.txt'
        'data/units/itemsets.txt'
        'data/units/tints.txt'
        'data/units/schools.txt'
        'data/school/ursulanordagh.tok'
        'data/encounters/expanse/expansedunes/expansedunesencounter01.enc'
        'data/levels/expanse/expansedunes/expansedunesnomove.grd'
        'data/towns/leagues/altahrunruins_leagues.tok'
    )

    foreach ($relative in $relativeFiles) {
        $source = Join-Path $Corpus ($relative -replace '/', '\')
        if (-not (Test-Path $source)) { throw "corpus file missing: $source" }
        $destination = Join-Path $Project ($relative -replace '/', '\')
        $folder = Split-Path -Parent $destination
        if (-not (Test-Path $folder)) { New-Item -ItemType Directory -Path $folder -Force | Out-Null }
        Copy-Item $source $destination
    }
    $staged = $true
}

Write-Host "MCP wire smoke test" -ForegroundColor Cyan
Write-Host "  exe:     $Exe"
Write-Host "  project: $Project"

# ---- start the server ------------------------------------------------------
$startInfo = [System.Diagnostics.ProcessStartInfo]::new()
$startInfo.FileName = $Exe
$startInfo.ArgumentList.Add('--project')
$startInfo.ArgumentList.Add($Project)
$startInfo.WorkingDirectory = Split-Path -Parent $Exe
$startInfo.UseShellExecute = $false
$startInfo.RedirectStandardInput = $true
$startInfo.RedirectStandardOutput = $true
$startInfo.RedirectStandardError = $true
$startInfo.CreateNoWindow = $true

$server = [System.Diagnostics.Process]::Start($startInfo)

# Start draining stderr immediately: a full stderr pipe would deadlock the server mid-session.
$stderrTask = $server.StandardError.ReadToEndAsync()

$frames = [System.Collections.Generic.List[object]]::new()
$nonJsonLines = [System.Collections.Generic.List[string]]::new()

# One pending read at a time: starting a second ReadLineAsync while one is in flight throws
# "the stream is currently in use", so the pending task is kept across calls that time out.
$pendingRead = $null

function Read-Frame([int]$timeoutMs) {
    if ($null -eq $script:pendingRead) {
        $script:pendingRead = $server.StandardOutput.ReadLineAsync()
    }

    if (-not $script:pendingRead.Wait($timeoutMs)) { return $null }

    $line = $script:pendingRead.Result
    $script:pendingRead = $null
    if ($null -eq $line) { return $null }

    if ($line.Trim().Length -eq 0) { return [pscustomobject]@{ Blank = $true } }

    try {
        $frame = $line | ConvertFrom-Json -Depth 100
    }
    catch {
        $script:nonJsonLines.Add($line)
        return [pscustomobject]@{ NonJson = $true; Raw = $line }
    }

    if (-not ($frame.PSObject.Properties.Name -contains 'jsonrpc')) {
        $script:nonJsonLines.Add($line)
        return [pscustomobject]@{ NonJson = $true; Raw = $line }
    }

    $script:frames.Add($frame)
    return $frame
}

function Find-Frame([scriptblock]$predicate) {
    foreach ($frame in $script:frames) { if (& $predicate $frame) { return $frame } }
    return $null
}

function Wait-Response([int]$id, [int]$timeoutMs = $TimeoutMs) {
    $deadline = [datetime]::UtcNow.AddMilliseconds($timeoutMs)
    while ($true) {
        $existing = Find-Frame { param($f) $f.PSObject.Properties.Name -contains 'id' -and $f.id -eq $id -and -not ($f.PSObject.Properties.Name -contains 'method') }
        if ($existing) { return $existing }

        $remaining = [int]([math]::Max(1, ($deadline - [datetime]::UtcNow).TotalMilliseconds))
        if ($remaining -le 1) { return $null }

        Read-Frame ([math]::Min($remaining, 5000)) | Out-Null
        if ($server.HasExited -and $null -eq $script:pendingRead) { return $null }
        if ([datetime]::UtcNow -gt $deadline) { return $null }
    }
}

function Send([hashtable]$message) {
    $json = $message | ConvertTo-Json -Depth 20 -Compress
    $server.StandardInput.WriteLine($json)
    $server.StandardInput.Flush()
}

function Send-Raw([string]$line) {
    $server.StandardInput.WriteLine($line)
    $server.StandardInput.Flush()
}

function Get-TextPayload($response) {
    if (-not $response) { return $null }
    if (-not ($response.PSObject.Properties.Name -contains 'result')) { return $null }
    $content = $response.result.content
    if (-not $content -or $content.Count -eq 0) { return $null }
    $text = $content[0].text
    if ($null -eq $text) { return $null }
    return ($text | ConvertFrom-Json -Depth 100)
}

function Test-IsError($response) {
    if (-not $response) { return $false }
    # A JSON-RPC protocol error (unknown tool, bad params) has no result at all.
    if (-not ($response.PSObject.Properties.Name -contains 'result')) { return $true }
    if ($null -eq $response.result) { return $true }
    return (($response.result.PSObject.Properties.Name -contains 'isError') -and $response.result.isError -eq $true)
}

try {
    # ---- 1. initialize -----------------------------------------------------
    Send @{
        jsonrpc = '2.0'; id = 1; method = 'initialize'
        params  = @{
            protocolVersion = '2025-06-18'
            capabilities    = @{}
            clientInfo      = @{ name = 'mcp-smoke'; version = '1.0.0' }
        }
    }

    $init = Wait-Response 1
    if (-not $init) {
        Fail 'initialize answers' 'no response with id 1'
    }
    elseif ($init.PSObject.Properties.Name -contains 'error') {
        Fail 'initialize answers' ($init.error | ConvertTo-Json -Compress)
    }
    else {
        Pass "initialize answers (server '$($init.result.serverInfo.name)', protocol $($init.result.protocolVersion))"
        if ($init.result.capabilities.tools) { Pass 'capabilities.tools advertised' }
        else { Fail 'capabilities.tools advertised' ($init.result.capabilities | ConvertTo-Json -Compress) }
        if ($init.result.instructions) { Pass 'server instructions present' }
        else { Fail 'server instructions present' 'result.instructions missing' }
    }

    # ---- 2. notifications/initialized (no response expected) ----------------
    Send @{ jsonrpc = '2.0'; method = 'notifications/initialized' }

    # ---- 3. ping -----------------------------------------------------------
    Send @{ jsonrpc = '2.0'; id = 2; method = 'ping' }
    $ping = Wait-Response 2
    if ($ping -and ($ping.PSObject.Properties.Name -contains 'result')) { Pass 'ping answers with an empty result' }
    else { Fail 'ping answers with an empty result' ($ping | ConvertTo-Json -Compress -Depth 5) }

    # ---- 4. tools/list -----------------------------------------------------
    Send @{ jsonrpc = '2.0'; id = 3; method = 'tools/list'; params = @{} }
    $list = Wait-Response 3
    if (-not $list -or -not ($list.PSObject.Properties.Name -contains 'result')) {
        Fail 'tools/list answers' ($list | ConvertTo-Json -Compress -Depth 5)
    }
    else {
        $tools = @($list.result.tools)
        Pass "tools/list returns $($tools.Count) tools"

        $names = @($tools | ForEach-Object { $_.name })
        foreach ($required in @('logs_tail', 'logs_clear', 'items_list', 'items_get', 'items_patch',
                               'encounters_get', 'skills_update', 'project_pack', 'project_unpack')) {
            if ($names -contains $required) { Pass "tool $required is registered" }
            else { Fail "tool $required is registered" 'not in tools/list' }
        }

        # Every tool needs a description and an object input schema: that is the documentation contract.
        $missingDescription = @($tools | Where-Object { -not $_.description -or $_.description.Trim().Length -lt 20 })
        if ($missingDescription.Count -eq 0) { Pass 'every tool carries a usable description' }
        else { Fail 'every tool carries a usable description' (($missingDescription | ForEach-Object { $_.name }) -join ', ') }

        $badSchema = @($tools | Where-Object { $_.inputSchema.type -ne 'object' -or -not (@($_.inputSchema.PSObject.Properties.Name) -contains 'properties') })
        if ($badSchema.Count -eq 0) { Pass 'every tool has an object input schema' }
        else { Fail 'every tool has an object input schema' (($badSchema | ForEach-Object { $_.name }) -join ', ') }

        $noStrict = @($tools | Where-Object {
            $_.name -match '^(items_add|items_patch|skills_add|encounters_set_field|schools_set_unit_field)$' -and
            -not (@($_.inputSchema.properties.PSObject.Properties.Name) -contains 'strict')
        })
        if ($noStrict.Count -eq 0) { Pass 'write tools expose strict' }
        else { Fail 'write tools expose strict' (($noStrict | ForEach-Object { $_.name }) -join ', ') }
    }

    # ---- 5. a read call ----------------------------------------------------
    Send @{ jsonrpc = '2.0'; id = 4; method = 'tools/call'; params = @{ name = 'items_list'; arguments = @{} } }
    $readCall = Wait-Response 4
    $payload = Get-TextPayload $readCall
    $ids = @()
    if ($null -ne $payload) { $ids = @($payload) }
    if ($ids.Count -gt 0) { Pass "items_list returns $($ids.Count) ids" }
    else { Fail 'items_list returns ids' ($readCall | ConvertTo-Json -Compress -Depth 5) }

    # ---- 6. a write call, then verify it landed and was backed up -----------
    $itemId = $null
    if ($ids -and $ids.Count -gt 0) { $itemId = $ids[0] }

    if ($itemId) {
        $newCost = 4242
        Send @{
            jsonrpc = '2.0'; id = 5; method = 'tools/call'
            params  = @{
                name      = 'items_patch'
                arguments = @{ name = $itemId; patch = @{ Cost = $newCost }; strict = $false }
            }
        }

        $writeCall = Wait-Response 5
        $written = Get-TextPayload $writeCall
        if ($written -and $written.result.cost -eq $newCost) {
            Pass "items_patch wrote Cost=$newCost to '$itemId' (strict=false)"
        }
        else {
            Fail "items_patch wrote Cost=$newCost to '$itemId'" ($writeCall | ConvertTo-Json -Compress -Depth 8)
        }

        $itemsFile = Join-Path $Project 'data\config\items.tok'
        $edited = Get-Content $itemsFile -Raw -Encoding Latin1
        if ($edited -match [regex]::Escape([string]$newCost)) { Pass 'the edit is on disk' }
        else { Fail 'the edit is on disk' "items.tok does not contain $newCost" }

        $history = Join-Path $Project '.mod-history\data\config\items.tok'
        $backups = @()
        if (Test-Path $history) { $backups = @(Get-ChildItem $history -Filter *.bak) }
        if ($backups.Count -ge 1) { Pass "a .mod-history backup exists ($($backups[0].Name))" }
        else { Fail 'a .mod-history backup exists' "no .bak under $history" }
    }
    else {
        Fail 'write call' 'no item id to patch'
    }

    # ---- 7. strict mode: the same invalid payload writes, then is refused ---
    # set_row with an implausible stat value is a `stat-out-of-range` warning in statsets.txt. strict=false must
    # write it (with a backup), strict=true must refuse it and restore the pre-call bytes.
    $statsetsFile = Join-Path $Project 'data\units\statsets.txt'
    $invalidRow = @{ level = 1; con = 9999; pow = 1; acc = 1; def = 1; iniMov = 1 }

    $before = (Get-FileHash $statsetsFile -Algorithm SHA256).Hash
    Send @{
        jsonrpc = '2.0'; id = 6; method = 'tools/call'
        params  = @{ name = 'statsets_set_row'; arguments = @{ index = 0; row = $invalidRow; strict = $false } }
    }

    $advisory = Wait-Response 6
    $advisoryPayload = Get-TextPayload $advisory
    if (-not (Test-IsError $advisory)) { Pass 'strict=false writes an invalid row (advisory)' }
    else { Fail 'strict=false writes an invalid row (advisory)' ($advisory | ConvertTo-Json -Compress -Depth 8) }

    if ($advisoryPayload -and @($advisoryPayload.warnings).Count -gt 0) {
        Pass "the advisory write reports its finding ($(@($advisoryPayload.warnings)[0].code))"
    }
    else { Fail 'the advisory write reports its finding' 'warnings[] was empty' }

    $afterAdvisory = (Get-FileHash $statsetsFile -Algorithm SHA256).Hash
    if ($before -ne $afterAdvisory) { Pass 'the advisory write changed the file' }
    else { Fail 'the advisory write changed the file' 'hash unchanged' }

    Send @{
        jsonrpc = '2.0'; id = 7; method = 'tools/call'
        params  = @{
            name      = 'statsets_set_row'
            # A different level, so this write genuinely changes the file and genuinely introduces a new warning.
            arguments = @{ index = 0; row = @{ level = 2; con = 9999; pow = 1; acc = 1; def = 1; iniMov = 1 }; strict = $true }
        }
    }

    $strictCall = Wait-Response 7
    if (Test-IsError $strictCall) { Pass 'strict=true refuses an invalid row' }
    else { Fail 'strict=true refuses an invalid row' ($strictCall | ConvertTo-Json -Compress -Depth 8) }

    if ($strictCall -and (@($strictCall.result.content)[0].text -match 'refused by strict mode')) {
        Pass 'the refusal explains itself'
    }
    else { Fail 'the refusal explains itself' ($strictCall | ConvertTo-Json -Compress -Depth 8) }

    $afterStrict = (Get-FileHash $statsetsFile -Algorithm SHA256).Hash
    if ($afterAdvisory -eq $afterStrict) { Pass 'the refused write left the file byte-identical' }
    else { Fail 'the refused write left the file byte-identical' "hash $afterAdvisory -> $afterStrict" }

    # ---- 8. notifications/cancelled is accepted and silent -----------------
    Send @{ jsonrpc = '2.0'; method = 'notifications/cancelled'; params = @{ requestId = 999; reason = 'smoke' } }

    # A bogus line must not kill the session either; the transport owns stdout, we only check liveness.
    Send @{ jsonrpc = '2.0'; id = 8; method = 'ping' }
    $alive = Wait-Response 8
    if ($alive -and ($alive.PSObject.Properties.Name -contains 'result')) { Pass 'the session survives notifications/cancelled' }
    else { Fail 'the session survives notifications/cancelled' ($alive | ConvertTo-Json -Compress -Depth 5) }

    # ---- 9. a job tool answers with a BuildResult -------------------------
    # An ISO that does not exist makes the unpack job fail immediately, which is enough to prove the job path:
    # a Started/Finished pair, a report with steps, and an exit code.
    $progressFrames = [System.Collections.Generic.List[object]]::new()
    Send @{
        jsonrpc = '2.0'; id = 9; method = 'tools/call'
        params  = @{
            name      = 'project_unpack'
            arguments = @{ iso = (Join-Path $Project 'no-such.iso'); projectName = 'smoke-unpack' }
        }
    }

    $jobCall = Wait-Response 9 -timeoutMs 60000
    $jobReport = Get-TextPayload $jobCall
    if ($jobReport -and $jobReport.kind -eq 'unpack' -and $jobReport.steps.Count -ge 1) {
        Pass "project_unpack returns a job report (state $($jobReport.state), exit $($jobReport.exitCode))"
    }
    else { Fail 'project_unpack returns a job report' ($jobCall | ConvertTo-Json -Compress -Depth 8) }

    if ($jobReport -and $jobReport.succeeded -eq $false) { Pass 'a failed job reports succeeded=false' }
    else { Fail 'a failed job reports succeeded=false' ($jobReport | ConvertTo-Json -Compress -Depth 5) }

    # ---- 10. stdout carried nothing but frames -----------------------------
    if ($nonJsonLines.Count -eq 0) { Pass 'stdout carried only JSON-RPC frames' }
    else {
        Fail 'stdout carried only JSON-RPC frames' ("non-JSON lines: " + (($nonJsonLines | Select-Object -First 3) -join ' | '))
    }
}
finally {
    # Closing stdin is how a client ends the session; the server must exit 0 on its own.
    try { $server.StandardInput.Close() } catch { }

    if (-not $server.WaitForExit(15000)) {
        Kill-Tree $server
    }

    $stderrText = ''
    try { $stderrText = $stderrTask.GetAwaiter().GetResult() } catch { }

    if ($server.HasExited) {
        if ($server.ExitCode -eq 0) { Pass 'the server exits 0 when stdin closes' }
        else { Fail 'the server exits 0 when stdin closes' "exit code $($server.ExitCode)" }
    }
    else {
        Fail 'the server exits 0 when stdin closes' 'still running after 15s'
    }

    if ($stderrText -match 'Unhandled exception|System\.NullReferenceException') {
        Fail 'no unhandled exception on stderr' (($stderrText -split "`n" | Select-Object -First 5) -join ' | ')
    }

    if ($stderrText.Trim()) {
        Write-Host ''
        Write-Host 'server stderr (last 20 lines)' -ForegroundColor DarkGray
        ($stderrText -split "`r?`n" | Select-Object -Last 20) | ForEach-Object { Write-Host "  $_" -ForegroundColor DarkGray }
    }

    if ($staged -and -not $KeepProject) {
        try { Remove-Item -Recurse -Force $Project } catch { }
    }
    elseif ($staged) {
        Write-Host "project kept: $Project" -ForegroundColor DarkGray
    }
}

Write-Host ''
if ($failures -eq 0) {
    Write-Host 'MCP wire smoke test: PASS' -ForegroundColor Green
    exit 0
}

Write-Host "MCP wire smoke test: $failures failure(s)" -ForegroundColor Red
exit 1

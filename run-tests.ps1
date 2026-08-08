<#
.SYNOPSIS
    Builds and runs the Gladius Modding Tools test suite with colored output.

.PARAMETER NoBuild
    Skip the build step and run the last compiled test DLL directly.

.PARAMETER ProjectFolder
    Name (or full path) of the REALDATA_ project folder to use for integration
    tests. Overrides auto-discovery. Examples:
        -ProjectFolder REALDATA_v2
        -ProjectFolder "C:\gladius\REALDATA_nightly"

.PARAMETER Filter
    xUnit filter expression. Examples:
        -Filter "PathHelperTests"
        -Filter "RealGameDataTests"
        -Filter "FullyQualifiedName~PackValidator"
        -Filter "FullyQualifiedName~Integration"

.PARAMETER Configuration
    Build configuration. Defaults to Debug.

.PARAMETER ListTests
    Print all discovered test names without running them, then exit.

.EXAMPLE
    .\run-tests.ps1
    .\run-tests.ps1 -NoBuild
    .\run-tests.ps1 -ProjectFolder REALDATA_v2
    .\run-tests.ps1 -Filter "RealGameDataTests"
    .\run-tests.ps1 -Configuration Release
    .\run-tests.ps1 -ListTests
#>
param(
    [switch]$NoBuild,
    [string]$ProjectFolder = "",
    [string]$Filter        = "",
    [string]$Configuration = "Debug",
    [switch]$ListTests
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# --- paths -------------------------------------------------------------------
$Root        = $PSScriptRoot
$TestProject = Join-Path $Root "ModdingGUI.Tests\ModdingGUI.Tests.csproj"
$ResultsDir  = Join-Path $Root "TestResults"
$TrxFile     = Join-Path $ResultsDir "TestResults.trx"

# --- helpers -----------------------------------------------------------------
function Write-Sep  { Write-Host ("=" * 62) -ForegroundColor DarkGray }
function Write-Head([string]$t) {
    Write-Host ""
    Write-Sep
    Write-Host "  $t" -ForegroundColor Cyan
    Write-Sep
}
function Write-Row([string]$label, [string]$value, [string]$color = "White") {
    Write-Host ("  {0,-26} {1}" -f $label, $value) -ForegroundColor $color
}

# --- banner ------------------------------------------------------------------
Write-Host ""
Write-Host "  Gladius Modding Tools -- Test Runner" -ForegroundColor Cyan
Write-Host "  Config        : $Configuration" -ForegroundColor DarkGray
Write-Host "  Project folder: $(if ($ProjectFolder) { $ProjectFolder } else { '(auto: first REALDATA_*)' })" -ForegroundColor DarkGray
Write-Host "  Filter        : $(if ($Filter) { $Filter } else { '(all)' })" -ForegroundColor DarkGray

# Forward ProjectFolder to the fixture via environment variable.
# Scoped to this process so it does not leak into the parent shell.
if ($ProjectFolder) {
    $env:REALDATA_PROJECT = $ProjectFolder
} else {
    $env:REALDATA_PROJECT = ""
}

# --- build -------------------------------------------------------------------
if (-not $NoBuild) {
    Write-Head "Building"
    $buildOut = dotnet build $TestProject --configuration $Configuration --nologo 2>&1
    if ($LASTEXITCODE -ne 0) {
        $buildOut | ForEach-Object { Write-Host $_ -ForegroundColor Red }
        Write-Host ""
        Write-Host "  [FAIL] Build failed." -ForegroundColor Red
        exit 1
    }
    Write-Host "  [OK]   Build succeeded." -ForegroundColor Green
}

# --- list only ---------------------------------------------------------------
if ($ListTests) {
    Write-Head "Discovered tests"
    $listArgs = @("test", $TestProject, "--no-build", "--configuration", $Configuration, "--list-tests")
    if ($Filter) { $listArgs += "--filter"; $listArgs += $Filter }
    & dotnet @listArgs | Select-String "^\s{4}" | ForEach-Object {
        Write-Host "  $_" -ForegroundColor DarkCyan
    }
    exit 0
}

# --- run tests ---------------------------------------------------------------
Write-Head "Running tests"

$null = New-Item -ItemType Directory -Force -Path $ResultsDir

$testArgs = @(
    "test", $TestProject,
    "--no-build",
    "--configuration", $Configuration,
    "--logger", "console;verbosity=normal",
    "--logger", "trx;LogFileName=$TrxFile"
)
if ($Filter) { $testArgs += "--filter"; $testArgs += $Filter }

$rawOutput = & dotnet @testArgs 2>&1
$exitCode  = $LASTEXITCODE

foreach ($line in $rawOutput) {
    $str = "$line"
    if      ($str -match "^\s*Passed ")  { Write-Host $str -ForegroundColor Green  }
    elseif  ($str -match "^\s*Failed ")  { Write-Host $str -ForegroundColor Red    }
    elseif  ($str -match "^\s*Skipped ") { Write-Host $str -ForegroundColor Yellow }
    elseif  ($str -match "error")        { Write-Host $str -ForegroundColor Red    }
    else                                 { Write-Host $str -ForegroundColor Gray   }
}

# --- parse TRX ---------------------------------------------------------------
$passed = 0; $failed = 0; $skipped = 0; $total = 0

if (Test-Path $TrxFile) {
    [xml]$trx = Get-Content $TrxFile -Encoding UTF8
    $ns = @{ t = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010" }

    $counters = ($trx | Select-Xml "//t:ResultSummary/t:Counters" -Namespace $ns |
                  Select-Object -First 1).Node
    if ($counters) {
        $passed  = [int]$counters.passed
        $failed  = [int]$counters.failed
        $total   = [int]$counters.total
        # xUnit 2.x records skipped facts in 'total' but not in 'notExecuted';
        # derive the count from what's unaccounted for.
        $skipped = $total - $passed - $failed
    }

    if ($failed -gt 0) {
        Write-Head "Failed tests"
        $trx | Select-Xml "//t:UnitTestResult[@outcome='Failed']" -Namespace $ns |
            ForEach-Object {
                $name = $_.Node.testName
                $msg  = ($_.Node.Output.ErrorInfo.Message -replace "\s+", " ").Trim()
                Write-Host "  [FAIL] $name" -ForegroundColor Red
                if ($msg) { Write-Host "         $msg" -ForegroundColor DarkRed }
            }
    }

    if ($skipped -gt 0) {
        Write-Head "Skipped tests"
        $trx | Select-Xml "//t:UnitTestResult[@outcome='NotExecuted']" -Namespace $ns |
            ForEach-Object {
                Write-Host "  [SKIP] $($_.Node.testName)" -ForegroundColor Yellow
            }
    }
}

# --- summary -----------------------------------------------------------------
Write-Head "Summary"
Write-Row "Total"        "$total"
Write-Row "Passed"       "$passed"  $(if ($passed  -gt 0) { "Green"  } else { "White" })
Write-Row "Failed"       "$failed"  $(if ($failed  -gt 0) { "Red"    } else { "White" })
Write-Row "Skipped"      "$skipped" $(if ($skipped -gt 0) { "Yellow" } else { "White" })

$resolvedTrx = if (Test-Path $TrxFile) { (Resolve-Path $TrxFile).Path } else { "N/A" }
Write-Row "Results file" $resolvedTrx DarkGray

Write-Host ""
if ($exitCode -eq 0) {
    Write-Host "  [OK]   All tests passed." -ForegroundColor Green
} else {
    Write-Host "  [FAIL] $failed test(s) failed." -ForegroundColor Red
}
Write-Host ""

exit $exitCode

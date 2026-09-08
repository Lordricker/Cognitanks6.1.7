# Colorized wrapper around mlagents-learn so NavPolicy/TurretPolicy lines are easy to scan at a
# glance in a long scrolling console instead of reading every line.
#
# Usage:
#   .\MLTraining\run_training.ps1
#   .\MLTraining\run_training.ps1 -RunId solo-tank-06 -Force
#   .\MLTraining\run_training.ps1 -ConfigPath MLTraining\configs\solo_tank.yaml -RunId my-run
#
# Runs via `cmd /c ... 2>&1` rather than PowerShell's own `2>&1` on a native exe - in PowerShell
# 5.1, redirecting a native command's stderr directly wraps each line in a NativeCommandError
# object instead of plain text, which breaks simple string matching. Routing the redirection
# through cmd.exe keeps the merged stdout+stderr stream as plain text.
param(
    [string]$ConfigPath = "MLTraining\configs\solo_tank.yaml",
    [string]$RunId = "solo-tank",
    [switch]$Force,
    # Curriculum training (BottomUpAgentPlan.md Section 5, 2026-09-01/02): load a prior run's
    # per-behavior-name checkpoint(s) as this run's starting weights, e.g. carrying a Turret-only
    # checkpoint into a run that also adds NavPolicy - matching behavior names resume from there,
    # any behavior with no match in the source run (a newly-added one) just initializes fresh.
    [string]$InitializeFrom = "",
    # Catches anything PowerShell couldn't bind to a named parameter above - specifically so a
    # typo'd "--force" (double-dash, valid for mlagents-learn itself since it's a Python/argparse
    # CLI, but NOT valid PowerShell switch syntax, which only binds "-Force") doesn't silently
    # vanish instead of erroring or applying. Confirmed 2026-09-02: "--force" bound to nothing,
    # $Force stayed $false, and mlagents-learn correctly refused to touch the existing run
    # directory - not a crash, just a wasted attempt that looked like it should have worked.
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$ExtraArgs
)

if ($ExtraArgs -contains "--force") {
    $Force = $true
}
$unrecognized = $ExtraArgs | Where-Object { $_ -ne "--force" }
if ($unrecognized) {
    Write-Host "Ignoring unrecognized argument(s): $($unrecognized -join ', ') - PowerShell params need a single dash (-Force), not double (--force)." -ForegroundColor Yellow
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $repoRoot "MLTraining\venv\Scripts\mlagents-learn.exe"

if (-not (Test-Path $exe)) {
    Write-Host "mlagents-learn.exe not found at $exe - is the venv set up?" -ForegroundColor Red
    exit 1
}

$logDir = Join-Path $repoRoot "MLTraining\logs"
if (-not (Test-Path $logDir)) {
    New-Item -ItemType Directory -Path $logDir | Out-Null
}
$logFile = Join-Path $logDir "$RunId-$(Get-Date -Format 'yyyyMMdd-HHmmss').log"
Write-Host "Logging full output to $logFile" -ForegroundColor DarkGray

$forceFlag = if ($Force) { "--force" } else { "" }
$initializeFromFlag = if ($InitializeFrom) { "--initialize-from=$InitializeFrom" } else { "" }
$cmdLine = "`"$exe`" `"$ConfigPath`" --run-id=$RunId $forceFlag $initializeFromFlag 2>&1"

cmd /c $cmdLine | ForEach-Object {
    $line = $_
    Add-Content -Path $logFile -Value $line
    if ($line -match "NavPolicy") {
        Write-Host $line -ForegroundColor Cyan
    }
    elseif ($line -match "TurretPolicy") {
        Write-Host $line -ForegroundColor Green
    }
    elseif ($line -match "\[WARNING\]") {
        Write-Host $line -ForegroundColor Yellow
    }
    elseif ($line -match "\[ERROR\]|Traceback|Exception") {
        Write-Host $line -ForegroundColor Red
    }
    else {
        Write-Host $line
    }
}

Write-Host "Training stopped. Full log saved at $logFile" -ForegroundColor DarkGray

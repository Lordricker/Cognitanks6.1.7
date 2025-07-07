# PowerShell script to remove Debug.Log statements from TankMan.cs
# This script removes Debug.Log, Debug.LogWarning, and Debug.LogError lines

param(
    [string]$FilePath = "Assets\AiEditor\AIScripts\TankMan.cs"
)

# Get the full path
$fullPath = Join-Path $PSScriptRoot $FilePath

if (-not (Test-Path $fullPath)) {
    Write-Host "File not found: $fullPath" -ForegroundColor Red
    exit 1
}

Write-Host "Processing file: $fullPath" -ForegroundColor Green

# Read all lines from the file
$lines = Get-Content $fullPath

# Initialize variables for tracking
$outputLines = @()
$removedCount = 0

# Process each line
for ($i = 0; $i -lt $lines.Count; $i++) {
    $line = $lines[$i]
    $trimmedLine = $line.Trim()
    
    # Check if line contains Debug.Log, Debug.LogWarning, or Debug.LogError
    if ($line -match 'Debug\.(Log|LogWarning|LogError|LogException)\s*\(') {
        # This is a debug statement line
        $removedCount++
        Write-Host "Removing line $($i + 1): $($trimmedLine.Substring(0, [Math]::Min(60, $trimmedLine.Length)))..." -ForegroundColor Yellow
        
        # Skip this line (don't add to output)
        continue
    }
    
    # Keep the line
    $outputLines += $line
}

# Write the modified content back to the file
$outputLines | Set-Content $fullPath -Encoding UTF8

Write-Host "`nProcessing complete!" -ForegroundColor Green
Write-Host "Removed $removedCount debug log statements" -ForegroundColor Cyan
Write-Host "File updated: $fullPath" -ForegroundColor Green

# Show a summary of what was removed
if ($removedCount -gt 0) {
    Write-Host "`nSummary of changes:" -ForegroundColor Magenta
    Write-Host "- Removed $removedCount debug log lines" -ForegroundColor White
    Write-Host "- File has been cleaned of all Debug.Log statements" -ForegroundColor White
} else {
    Write-Host "No debug log statements found to remove." -ForegroundColor Yellow
}

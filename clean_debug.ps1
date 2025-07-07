# Simple PowerShell script to clean Debug.Log statements
$filePath = "Assets\AiEditor\AIScripts\TankMan.cs"
$content = Get-Content $filePath
$cleanContent = @()

foreach ($line in $content) {
    if ($line -like "*Debug.Log*" -or $line -like "*Debug.LogWarning*" -or $line -like "*Debug.LogError*") {
        Write-Host "Removing: $($line.Trim())"
    } else {
        $cleanContent += $line
    }
}

$cleanContent | Out-File $filePath -Encoding UTF8
Write-Host "Clean complete! Removed $(($content.Count - $cleanContent.Count)) debug lines."

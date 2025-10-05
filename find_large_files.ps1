param (
    [int]$ThresholdMB = 50
)

Write-Host "Scanning the repository for large files (over $ThresholdMB MB)..." -ForegroundColor Cyan

# Get all files with progress bar
$files = Get-ChildItem -Recurse -File | ForEach-Object -Begin {
    $i = 0
    $total = (Get-ChildItem -Recurse -File | Measure-Object).Count
} -Process {
    $i++
    Write-Progress -Activity "Scanning files..." -Status "$i / $total" -PercentComplete (($i / $total) * 100)
    $_
}

# Filter large files
$largeFiles = $files | Where-Object { $_.Length -gt ($ThresholdMB * 1MB) } |
Sort-Object Length -Descending

if (-not $largeFiles -or $largeFiles.Count -eq 0) {
    Write-Host "No large files found above $ThresholdMB MB." -ForegroundColor Green
    exit
}

# Display results
$totalSizeMB = [math]::Round(($largeFiles | Measure-Object -Property Length -Sum).Sum / 1MB, 2)
Write-Host "Found $($largeFiles.Count) large files. Total size: $totalSizeMB MB" -ForegroundColor Yellow
Write-Host ""

for ($i = 0; $i -lt $largeFiles.Count; $i++) {
    $file = $largeFiles[$i]
    $sizeMB = [math]::Round($file.Length / 1MB, 2)
    Write-Host "[$i] $($file.FullName) ($sizeMB MB)"
}

Write-Host ""
$selection = Read-Host "Enter the numbers of the files you want to process (comma separated, e.g. 0,2,5) or 'all' for batch mode"

if ($selection -eq "all") {
    Write-Host "`nBatch mode selected. Choose one action for ALL files:" -ForegroundColor Cyan
    Write-Host "  1 = Add to .gitignore and remove from cache"
    Write-Host "  2 = Track with Git LFS"
    Write-Host "  3 = Skip (do nothing)"
    $batchAction = Read-Host "Enter option (1/2/3)"

    foreach ($file in $largeFiles) {
        $relativePath = $file.FullName.Replace((Get-Location).Path + "\", "").Replace("\", "/")
        switch ($batchAction) {
            "1" {
                Add-Content ".gitignore" "`n$relativePath"
                git rm --cached "$relativePath" 2>$null
                Write-Host "Added $relativePath to .gitignore and removed from cache." -ForegroundColor Green
            }
            "2" {
                git lfs track "$relativePath"
                Write-Host "Added $relativePath to Git LFS tracking." -ForegroundColor Green
            }
            "3" {
                Write-Host "Skipped $relativePath."
            }
            default {
                Write-Host "Invalid option, skipping $relativePath..."
            }
        }
    }
}
else {
    $indices = $selection -split "," | ForEach-Object { $_.Trim() } | Where-Object { $_ -match '^\d+$' }

    foreach ($index in $indices) {
        if ($index -ge 0 -and $index -lt $largeFiles.Count) {
            $file = $largeFiles[$index]
            $relativePath = $file.FullName.Replace((Get-Location).Path + "\", "").Replace("\", "/")

            Write-Host ""
            Write-Host "Selected file: $relativePath"
            Write-Host "Choose action: " -ForegroundColor Cyan
            Write-Host "  1 = Add to .gitignore and remove from cache"
            Write-Host "  2 = Track with Git LFS"
            Write-Host "  3 = Skip (do nothing)"
            $action = Read-Host "Enter option (1/2/3)"

            switch ($action) {
                "1" {
                    Add-Content ".gitignore" "`n$relativePath"
                    git rm --cached "$relativePath" 2>$null
                    Write-Host "Added $relativePath to .gitignore and removed from cache." -ForegroundColor Green
                }
                "2" {
                    git lfs track "$relativePath"
                    Write-Host "Added $relativePath to Git LFS tracking." -ForegroundColor Green
                }
                "3" {
                    Write-Host "Skipped $relativePath."
                }
                default {
                    Write-Host "Invalid option, skipping..."
                }
            }
        }
        else {
            Write-Host "Invalid index: $index"
        }
    }
}

Write-Host "`nDone!"

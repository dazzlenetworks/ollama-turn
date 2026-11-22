# Ollama Turn — clean build artifacts

# Created 7/21/2026



# Welcome
Write-Output "This script removes build artifacts (bin, obj, dist) from the project."
Write-Output "Your source and the www UI are left untouched. A config.json beside a built or published exe goes with its folder."



# Variables
$projectRoot = Split-Path -Parent $PSScriptRoot
$targets     = @("bin", "obj", "dist")



# Clean
foreach ($target in $targets) {
    $path = Join-Path $projectRoot $target
    Write-Output "`nRemoving $path"
    if (Test-Path $path) {
        Remove-Item -Recurse -Force $path
        Write-Output "  Removed."
    } else {
        Write-Output "  Not present, skipping."
    }
}



# Summary
Write-Output "`nSummary: bin, obj, and dist have been cleared (where they existed)."
Write-Output "The next build will recreate them from source."



# Confirmation
Write-Output "`nOperation completed successfully"

Read-Host -Prompt "`nPress any key to continue."

Write-Output "`nExiting in 1.5 seconds...`n"

Start-Sleep -s 1.5

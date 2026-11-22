# Ollama Turn — clean build artifacts

# Created 7/21/2026


param(
    # Also remove dist\, the published build. Left alone by default: it is an install
    # someone may launch, with their own config.json beside it.
    [switch] $IncludeDist
)



# Welcome
Write-Output "This script removes build artifacts (bin, obj) from the project, and dist with -IncludeDist."
Write-Output "Your source and the www UI are left untouched. A config.json beside a built exe goes with its folder."



# Variables
$projectRoot = Split-Path -Parent $PSScriptRoot
$targets     = @("bin", "obj")
if ($IncludeDist) { $targets += "dist" }



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

if ($IncludeDist) {
    Write-Output "`ndist was removed, including the published build and any config.json beside it."
} else {
    Write-Output "`nKept dist: it is the published build, and may be an install with its own config.json. Pass -IncludeDist to remove it."
}



# Summary
Write-Output "`nSummary: $($targets -join ', ') cleared (where they existed)."
Write-Output "The next build will recreate them from source."



# Confirmation
Write-Output "`nOperation completed successfully"

Read-Host -Prompt "`nPress any key to continue."

Write-Output "`nExiting in 1.5 seconds...`n"

Start-Sleep -s 1.5

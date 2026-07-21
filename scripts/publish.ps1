# Ollama Turn — publish a self-contained Windows release

# Created 7/21/2026



# Welcome
Write-Output "This script publishes Ollama Turn as a self-contained, single-file Windows executable."
Write-Output "The result runs on a machine without the .NET SDK installed."
Write-Output "(The WebView2 Runtime is still required, but it ships with modern Windows 10/11 and Edge.)"



# Variables
$projectRoot = Split-Path -Parent $PSScriptRoot
$runtime     = "win-x64"
$config      = "Release"
$outputDir   = Join-Path $projectRoot "dist"



# Publish
Write-Output "`nPublishing $config / $runtime to:"
Write-Output "  $outputDir"

Push-Location $projectRoot
dotnet publish -c $config -r $runtime --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $outputDir
$exitCode = $LASTEXITCODE
Pop-Location

if ($exitCode -ne 0) {
    Write-Output "`nPublish failed (exit code $exitCode)."
    Read-Host -Prompt "`nPress any key to continue."
    exit $exitCode
}



# Summary
Write-Output "`nSummary:"
Write-Output "  A self-contained OllamaTurn.exe now lives in: $outputDir"
Write-Output "  config.json and the www UI are copied alongside it, ready to zip and distribute."



# Confirmation
Write-Output "`nOperation completed successfully"

Read-Host -Prompt "`nPress any key to continue."

Write-Output "`nExiting in 1.5 seconds...`n"

Start-Sleep -s 1.5

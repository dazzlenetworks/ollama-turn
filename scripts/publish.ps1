# Ollama Turn — publish a self-contained Windows release

# Created 7/21/2026


param(
    # Skip the press-any-key tails so the script can run unattended.
    [switch] $NoPause
)



# Welcome
Write-Output "This script publishes Ollama Turn as self-contained, single-file Windows executables:"
Write-Output "  OllamaTurn.exe     the app window"
Write-Output "  OllamaTurnCLI.exe  the command-line front end"
Write-Output "The results run on a machine without the .NET SDK installed."
Write-Output "(The WebView2 Runtime is still required, but it ships with modern Windows 10/11 and Edge.)"



# Variables
$projectRoot = Split-Path -Parent $PSScriptRoot
$runtime     = "win-x64"
$config      = "Release"
$outputDir   = Join-Path $projectRoot "dist"



# Publish the app
Write-Output "`nPublishing the app ($config / $runtime) to:"
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
    if (-not $NoPause) { Read-Host -Prompt "`nPress any key to continue." }
    exit $exitCode
}



# Publish the CLI into the same folder
# Both front ends land beside one config.json, which is what the CLI reads when
# it is run from a distributed copy. The app writes it; no publish does.
Write-Output "`nPublishing the CLI to the same folder..."

Push-Location $projectRoot
# Trimmed: the CLI touches no reflection-based JSON, so this is safe and takes
# it from ~68 MB to ~14 MB. (The app itself can't be trimmed - WPF doesn't
# support it.)
dotnet publish "cli\OllamaTurnCLI.csproj" -c $config -r $runtime --self-contained true `
    -p:PublishSingleFile=true `
    -p:PublishTrimmed=true `
    -o $outputDir
$exitCode = $LASTEXITCODE
Pop-Location

if ($exitCode -ne 0) {
    Write-Output "`nCLI publish failed (exit code $exitCode)."
    if (-not $NoPause) { Read-Host -Prompt "`nPress any key to continue." }
    exit $exitCode
}



# Report the release config
# Neither publish writes a config.json, so a configured one in this folder is
# never overwritten. Without one, both exes use the built-in default and the app
# writes the file on first save. config.example.json shows its shape.
$distConfig = Join-Path $outputDir "config.json"
Write-Output "`nChecking the release config..."

if (Test-Path $distConfig) {
    Write-Output "  Kept the existing $distConfig"
} else {
    Write-Output "  None yet. The app writes one on first save; until then both use http://localhost:11434/."
}



# Summary
Write-Output "`nSummary:"
Write-Output "  Self-contained OllamaTurn.exe and OllamaTurnCLI.exe now live in: $outputDir"
Write-Output "  The www UI is copied alongside them, ready to zip and distribute."



# Confirmation
Write-Output "`nOperation completed successfully"

if (-not $NoPause) {
    Read-Host -Prompt "`nPress any key to continue."

    Write-Output "`nExiting in 1.5 seconds...`n"

    Start-Sleep -s 1.5
}

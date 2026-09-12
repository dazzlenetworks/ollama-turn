# Ollama Turn — test harness

# Created 7/28/2026

# Codifies the checks that were previously run by hand after every change:
# CLI behaviour, config discovery, the settings-writing paths, and the app
# smoke test. Exit code 0 means everything that could run, passed.
#
# Tests never touch a real config. Each one works in its own temporary folder,
# so running this cannot change which model you have selected.


param(
    # Path to the CLI under test. Defaults to the published exe if there is one,
    # otherwise the Debug build.
    [string] $Cli,

    # Skip the tests that need a reachable Ollama server.
    [switch] $SkipServer,

    # Skip the app smoke test (which opens and closes a window).
    [switch] $SkipApp,

    # Skip the press-any-key tail so the script can run unattended.
    [switch] $NoPause
)



# Welcome
Write-Output "This script runs the Ollama Turn test suite: CLI behaviour, config discovery,"
Write-Output "settings writes, and the app smoke test."



# Variables
$projectRoot = Split-Path -Parent $PSScriptRoot
$sandbox     = Join-Path ([IO.Path]::GetTempPath()) ("ollama-turn-tests-" + [Guid]::NewGuid().ToString("N").Substring(0, 8))
$defaultUrl  = "http://localhost:11434"
$results     = New-Object System.Collections.ArrayList
$serverOk    = $false
$serverUrl   = $null
$firstModel  = $null

$published  = Join-Path $projectRoot "dist\OllamaTurnCLI.exe"
$debugBuild = Join-Path $projectRoot "cli\bin\Debug\net9.0\OllamaTurnCLI.exe"

if (-not $Cli) {
    $Cli = if (Test-Path $published) { $published } else { $debugBuild }
}



# Helpers
# Run the CLI as a real process so stdout, stderr and the exit code can be
# asserted separately, and so each case can be handed its own config file.
function Invoke-Cli {
    param(
        [string[]] $Arguments = @(),
        [string]   $StdIn,
        [string]   $ConfigPath,
        [string]   $WorkingExe
    )

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName               = if ($WorkingExe) { $WorkingExe } else { $Cli }
    $psi.UseShellExecute        = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError  = $true
    $psi.RedirectStandardInput  = $true

    # Windows PowerShell runs on .NET Framework, which has no ArgumentList, so
    # the arguments are quoted into one string by hand.
    $psi.Arguments = (($Arguments | ForEach-Object { '"' + ($_ -replace '"', '\"') + '"' }) -join ' ')

    # An empty value must still override the caller's environment, so that a
    # test never accidentally picks up a real config.
    $psi.EnvironmentVariables["OLLAMA_TURN_CONFIG"] = $ConfigPath

    $p = [Diagnostics.Process]::Start($psi)

    # Written as UTF-8 bytes to the base stream rather than through Write(),
    # which encodes using the console's codepage. Under an OEM codepage a
    # character with no mapping - a byte order mark, for one - becomes a
    # question mark, so the pipe carried something other than what the case
    # meant to send and the CLI was tested against the wrong bytes. UTF-8 is
    # also what the CLI now reads, so the harness and the contract agree.
    if ($StdIn) {
        $bytes = [Text.Encoding]::UTF8.GetBytes($StdIn)
        $p.StandardInput.BaseStream.Write($bytes, 0, $bytes.Length)
        $p.StandardInput.BaseStream.Flush()
    }
    $p.StandardInput.Close()
    $out = $p.StandardOutput.ReadToEnd()
    $err = $p.StandardError.ReadToEnd()
    $p.WaitForExit()

    return [pscustomobject]@{
        Out  = $out.Trim()
        Err  = $err.Trim()
        Code = $p.ExitCode
    }
}

function New-ConfigFile {
    param([string] $Name, [string] $Content)
    $dir = Join-Path $sandbox $Name
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    $path = Join-Path $dir "config.json"
    [IO.File]::WriteAllText($path, $Content)
    return $path
}

# A copy of the CLI in an empty folder. Needed whenever a test asserts what
# happens with no config: the real exe has a real config.json beside it, which
# is rank 2 of discovery and would answer instead of the built-in default.
function New-IsolatedCli {
    param([string] $Name, [string] $ConfigContent)

    $dir = Join-Path $sandbox $Name
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    $base = [IO.Path]::GetFileNameWithoutExtension($Cli)
    Copy-Item (Join-Path (Split-Path -Parent $Cli) "$base.*") $dir
    Remove-Item (Join-Path $dir "config.json") -ErrorAction SilentlyContinue
    if ($PSBoundParameters.ContainsKey("ConfigContent")) {
        [IO.File]::WriteAllText((Join-Path $dir "config.json"), $ConfigContent)
    }
    return (Join-Path $dir "$base.exe")
}

function Assert-True {
    param([bool] $Condition, [string] $Message)
    if (-not $Condition) { throw $Message }
}

function Assert-Equal {
    param($Expected, $Actual, [string] $What)
    if ($Expected -ne $Actual) { throw "$What : expected '$Expected', got '$Actual'" }
}

function Assert-Match {
    param([string] $Pattern, [string] $Actual, [string] $What)
    if ($Actual -notmatch $Pattern) { throw "$What : '$Actual' does not match /$Pattern/" }
}

function Test-Case {
    # Body second so a case can be written as: Test-Case "name" { ... }
    param([string] $Name, [scriptblock] $Body, [string] $Needs = "offline")

    if ($Needs -eq "server" -and -not $serverOk) {
        [void]$results.Add([pscustomobject]@{ Name = $Name; Status = "SKIP"; Detail = "no server" })
        Write-Output "  SKIP  $Name"
        return
    }

    try {
        & $Body
        [void]$results.Add([pscustomobject]@{ Name = $Name; Status = "PASS"; Detail = "" })
        Write-Output "  PASS  $Name"
    } catch {
        [void]$results.Add([pscustomobject]@{ Name = $Name; Status = "FAIL"; Detail = $_.Exception.Message })
        Write-Output "  FAIL  $Name"
        Write-Output "        $($_.Exception.Message)"
    }
}



# Preflight
Write-Output "`nPreflight"

if (-not (Test-Path -LiteralPath $Cli)) {
    Write-Output "  The CLI is not built: $Cli"
    Write-Output "  Build it with: dotnet build cli\OllamaTurnCLI.csproj -c Debug"
    if (-not $NoPause) { Read-Host -Prompt "`nPress any key to continue." }
    exit 1
}

# Say which build answered, not just where it lives. $Cli prefers the published
# exe while smoke.ps1 always launches the Debug window, so one run can cover a
# window built a minute ago and a CLI built days ago and report a single summary
# over both. A session that fixes the CLI, runs this, and watches the fix not
# appear has otherwise nothing to go on.
if ($Cli -eq $published) {
    Write-Output "  Testing: $Cli"
    Write-Output "           the published build - re-run scripts\publish.ps1 after changing"
    Write-Output "           the CLI, or this suite tests the previous one"
}
elseif ($Cli -eq $debugBuild) {
    Write-Output "  Testing: $Cli (the Debug build)"
}
else {
    Write-Output "  Testing: $Cli (given with -Cli)"
}

if (-not $SkipApp) {
    Write-Output "  App:     the Debug build, launched by smoke.ps1"
}

New-Item -ItemType Directory -Path $sandbox -Force | Out-Null
Write-Output "  Sandbox: $sandbox"

# Is there a server to talk to? The real config is read only to find its URL,
# and is never written by this script.
if (-not $SkipServer) {
    $realConfig = Join-Path (Split-Path -Parent $Cli) "config.json"
    if (Test-Path $realConfig) {
        try { $serverUrl = (Get-Content $realConfig -Raw | ConvertFrom-Json).url } catch { }
    }
    if (-not $serverUrl) { $serverUrl = $defaultUrl }

    $probeConfig = New-ConfigFile -Name "probe" -Content ('{ "url": "' + $serverUrl + '" }')
    $probe = Invoke-Cli -Arguments @("--list-models") -ConfigPath $probeConfig
    if ($probe.Code -eq 0 -and $probe.Out) {
        $serverOk = $true
        $models = @(($probe.Out -split "`r?`n") | ForEach-Object { $_.Trim() } | Where-Object { $_ })

        # Turn tests need a model that can actually generate. Embedding models
        # answer /api/tags but reject /api/generate, and one may well be first
        # in the list.
        $firstModel = @($models | Where-Object { $_ -notmatch "embed" })[0]
        if (-not $firstModel) { $firstModel = $models[0] }

        Write-Output "  Server:  reachable, $($models.Count) models"
        Write-Output "  Using:   $firstModel"
    } else {
        Write-Output "  Server:  not reachable, server tests will be skipped"
    }
} else {
    Write-Output "  Server:  skipped by request"
}



# CLI: basics
Write-Output "`nCLI basics"

Test-Case "--help prints usage and exits 0" {
    $r = Invoke-Cli -Arguments @("--help")
    Assert-Equal 0 $r.Code "exit code"
    Assert-Match "OllamaTurnCLI" $r.Out "help text"
    Assert-Match "--set-model" $r.Out "help lists options"
}

Test-Case "no prompt fails locally with a clear message" {
    $cfg = New-ConfigFile -Name "noprompt" -Content '{ "url": "http://127.0.0.1:9/" }'
    $r = Invoke-Cli -ConfigPath $cfg
    Assert-Equal 1 $r.Code "exit code"
    Assert-Match "no prompt" $r.Err "stderr"
    Assert-Equal "" $r.Out "stdout stays empty"
}

Test-Case "unknown option is rejected, not treated as a prompt" {
    $r = Invoke-Cli -Arguments @("--nope", "hello")
    Assert-Equal 1 $r.Code "exit code"
    Assert-Match "unknown option" $r.Err "stderr"
}

Test-Case "--timeout rejects a non-numeric value" {
    $r = Invoke-Cli -Arguments @("--timeout", "soon", "hi")
    Assert-Equal 1 $r.Code "exit code"
    Assert-Match "positive number" $r.Err "stderr"
}

Test-Case "errors go to stderr, never stdout" {
    $r = Invoke-Cli -Arguments @("--nope")
    Assert-Equal "" $r.Out "stdout"
    Assert-True ($r.Err.Length -gt 0) "stderr should carry the message"
}



# CLI: config discovery
Write-Output "`nConfig discovery"

Test-Case "OLLAMA_TURN_CONFIG is used when set" {
    $cfg = New-ConfigFile -Name "pinned" -Content '{ "url": "http://pinned.example:1234/", "model": "pinned-model" }'
    $r = Invoke-Cli -Arguments @("--show-config") -ConfigPath $cfg
    Assert-Equal 0 $r.Code "exit code"
    Assert-Match "http://pinned.example:1234" $r.Out "url"
    Assert-Match "pinned-model" $r.Out "model"
}

Test-Case "config.json beside the exe is used when nothing is pinned" {
    $exe = New-IsolatedCli -Name "sibling" `
        -ConfigContent '{ "url": "http://sibling.example:4321/", "model": "sibling-model" }'
    $r = Invoke-Cli -Arguments @("--show-config") -ConfigPath "" -WorkingExe $exe
    Assert-Equal 0 $r.Code "exit code"
    Assert-Match "http://sibling.example:4321" $r.Out "url"
    Assert-Match "sibling-model" $r.Out "model"
}

Test-Case "the pinned config outranks one beside the exe" {
    $exe = New-IsolatedCli -Name "both" `
        -ConfigContent '{ "url": "http://sibling.example:4321/", "model": "sibling-model" }'
    $cfg = New-ConfigFile -Name "wins" -Content '{ "url": "http://pinned.example:1234/", "model": "pinned-model" }'
    $r = Invoke-Cli -Arguments @("--show-config") -ConfigPath $cfg -WorkingExe $exe
    Assert-Match "pinned-model" $r.Out "pinned config should win"
}

Test-Case "a corrupt config falls through to the default" {
    $exe = New-IsolatedCli -Name "corrupt-exe"
    $cfg = New-ConfigFile -Name "corrupt" -Content '{ this is not json'
    $r = Invoke-Cli -Arguments @("--show-config") -ConfigPath $cfg -WorkingExe $exe
    Assert-Equal 0 $r.Code "exit code"
    Assert-Match "localhost:11434" $r.Out "falls back to the default url"
}

Test-Case "a config with no url is treated as absent" {
    $exe = New-IsolatedCli -Name "nourl-exe"
    $cfg = New-ConfigFile -Name "nourl" -Content '{ "model": "orphan" }'
    $r = Invoke-Cli -Arguments @("--show-config") -ConfigPath $cfg -WorkingExe $exe
    Assert-Equal 0 $r.Code "exit code"
    Assert-Match "localhost:11434" $r.Out "falls back to the default url"
    Assert-True ($r.Out -notmatch "orphan") "a url-less file contributes nothing"
}

Test-Case "a missing config file falls through to the default" {
    $exe = New-IsolatedCli -Name "missing-exe"
    $r = Invoke-Cli -Arguments @("--show-config") -ConfigPath (Join-Path $sandbox "nope\config.json") -WorkingExe $exe
    Assert-Equal 0 $r.Code "exit code"
    Assert-Match "localhost:11434" $r.Out "default url"
}



# CLI: turns against a live server
Write-Output "`nTurns"

Test-Case "--list-models returns at least one model" -Needs "server" {
    $cfg = New-ConfigFile -Name "list" -Content ('{ "url": "' + $serverUrl + '" }')
    $r = Invoke-Cli -Arguments @("--list-models") -ConfigPath $cfg
    Assert-Equal 0 $r.Code "exit code"
    Assert-True ($r.Out.Length -gt 0) "should list something"
}

Test-Case "a prompt argument returns a response on stdout" -Needs "server" {
    $cfg = New-ConfigFile -Name "turn" -Content ('{ "url": "' + $serverUrl + '", "model": "' + $firstModel + '" }')
    $r = Invoke-Cli -Arguments @("Reply with exactly the word: pong") -ConfigPath $cfg
    Assert-Equal 0 $r.Code "exit code"
    Assert-True ($r.Out.Length -gt 0) "response should not be empty"
}

Test-Case "a prompt on stdin works the same way" -Needs "server" {
    $cfg = New-ConfigFile -Name "stdin" -Content ('{ "url": "' + $serverUrl + '", "model": "' + $firstModel + '" }')
    $r = Invoke-Cli -StdIn "Reply with exactly the word: pong" -ConfigPath $cfg
    Assert-Equal 0 $r.Code "exit code"
    Assert-True ($r.Out.Length -gt 0) "response should not be empty"
}

Test-Case "--json emits the raw response object" -Needs "server" {
    $cfg = New-ConfigFile -Name "json" -Content ('{ "url": "' + $serverUrl + '", "model": "' + $firstModel + '" }')
    $r = Invoke-Cli -Arguments @("--json", "say hi") -ConfigPath $cfg
    Assert-Equal 0 $r.Code "exit code"
    $obj = $r.Out | ConvertFrom-Json
    Assert-Equal $firstModel $obj.model "model in the response"
    Assert-True ($null -ne $obj.response) "response field present"
}

Test-Case "--model overrides the configured model for one turn" -Needs "server" {
    $cfg = New-ConfigFile -Name "override" -Content ('{ "url": "' + $serverUrl + '", "model": "not-the-one" }')
    $r = Invoke-Cli -Arguments @("--model", $firstModel, "--json", "say hi") -ConfigPath $cfg
    Assert-Equal 0 $r.Code "exit code"
    Assert-Equal $firstModel ($r.Out | ConvertFrom-Json).model "model actually used"
    Assert-Match "not-the-one" (Get-Content $cfg -Raw) "the override must not be written to the config"
}

Test-Case "no configured model, several installed, is an error not a guess" -Needs "server" {
    $cfg = New-ConfigFile -Name "nomodel" -Content ('{ "url": "' + $serverUrl + '" }')
    $r = Invoke-Cli -Arguments @("hi") -ConfigPath $cfg
    Assert-Equal 1 $r.Code "exit code"
    Assert-Match "no model selected" $r.Err "stderr"
    Assert-Match $firstModel $r.Err "the available models are listed"
}



# CLI: writing settings
Write-Output "`nSettings writes"

Test-Case "--set-model saves a model that exists" -Needs "server" {
    $cfg = New-ConfigFile -Name "setmodel" -Content ('{ "url": "' + $serverUrl + '", "model": "old-model" }')
    $r = Invoke-Cli -Arguments @("--set-model", $firstModel) -ConfigPath $cfg
    Assert-Equal 0 $r.Code "exit code"
    Assert-Equal $firstModel ((Get-Content $cfg -Raw | ConvertFrom-Json).model) "model on disk"
}

Test-Case "--set-model refuses an unknown model and writes nothing" -Needs "server" {
    $cfg = New-ConfigFile -Name "setbogus" -Content ('{ "url": "' + $serverUrl + '", "model": "keep-me" }')
    $r = Invoke-Cli -Arguments @("--set-model", "definitely-not-installed:99b") -ConfigPath $cfg
    Assert-Equal 1 $r.Code "exit code"
    Assert-Match "no model named" $r.Err "stderr"
    Assert-Equal "keep-me" ((Get-Content $cfg -Raw | ConvertFrom-Json).model) "config must be untouched"
}

Test-Case "--set-url saves even when that server does not answer, and warns" {
    $cfg = New-ConfigFile -Name "seturl" -Content '{ "url": "http://old.example:1111/", "model": "keep-me" }'
    $r = Invoke-Cli -Arguments @("--set-url", "http://127.0.0.1:9/") -ConfigPath $cfg
    Assert-Equal 0 $r.Code "exit code"
    Assert-Match "127.0.0.1:9" ((Get-Content $cfg -Raw | ConvertFrom-Json).url) "url on disk"
    Assert-Match "did not answer" $r.Err "should warn on stderr"
    Assert-Equal "keep-me" ((Get-Content $cfg -Raw | ConvertFrom-Json).model) "model must survive a url change"
}

Test-Case "--set-model writes to the config beside the exe" -Needs "server" {
    $exe = New-IsolatedCli -Name "created" -ConfigContent ('{ "url": "' + $serverUrl + '" }')
    $cfg = Join-Path (Split-Path -Parent $exe) "config.json"

    $r = Invoke-Cli -Arguments @("--set-model", $firstModel) -ConfigPath "" -WorkingExe $exe
    Assert-Equal 0 $r.Code "exit code"
    Assert-Equal $firstModel ((Get-Content $cfg -Raw | ConvertFrom-Json).model) "model written beside the exe"
}

Test-Case "a BOM-prefixed pipe is not mistaken for a prompt" {
    $exe = New-IsolatedCli -Name "bom" -ConfigContent '{ "url": "http://127.0.0.1:9/" }'
    $r = Invoke-Cli -ConfigPath "" -WorkingExe $exe -StdIn ([string][char]0xFEFF)
    Assert-Equal 1 $r.Code "exit code"
    Assert-Match "no prompt" $r.Err "a byte order mark alone is not a prompt"
}



# The app
Write-Output "`nApp"

if ($SkipApp) {
    [void]$results.Add([pscustomobject]@{ Name = "app smoke test"; Status = "SKIP"; Detail = "skipped by request" })
    Write-Output "  SKIP  app smoke test"
} else {
    Test-Case "the app opens, reports its UI ready, and closes" {
        & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "smoke.ps1") -NoPause | Out-Null
        Assert-Equal 0 $LASTEXITCODE "smoke.ps1 exit code"
    }
}



# Cleanup
Remove-Item $sandbox -Recurse -Force -ErrorAction SilentlyContinue



# Summary
$passed  = @($results | Where-Object { $_.Status -eq "PASS" }).Count
$failed  = @($results | Where-Object { $_.Status -eq "FAIL" }).Count
$skipped = @($results | Where-Object { $_.Status -eq "SKIP" }).Count

Write-Output "`nSummary:"
Write-Output "  $passed passed, $failed failed, $skipped skipped"

if ($failed -gt 0) {
    Write-Output "`n  Failures:"
    foreach ($r in $results | Where-Object { $_.Status -eq "FAIL" }) {
        Write-Output "    $($r.Name)"
        Write-Output "      $($r.Detail)"
    }
}



# Confirmation
if ($failed -eq 0) {
    Write-Output "`nOperation completed successfully"
} else {
    Write-Output "`nTests failed"
}

if (-not $NoPause) {
    Read-Host -Prompt "`nPress any key to continue."

    Write-Output "`nExiting in 1.5 seconds...`n"

    Start-Sleep -s 1.5
}

exit ([int]($failed -gt 0))

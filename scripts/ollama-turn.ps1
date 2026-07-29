# Ollama Turn CLI — run one turn against the server and model the GUI is using

# Created 7/28/2026

# This is a machine-facing CLI, not an interactive helper: it deliberately skips
# the house welcome/summary/pause chatter so that stdout is nothing but the
# model's answer, ready to be piped or captured by an agent.


[CmdletBinding()]
param(
    # The message. Positional; anything left over is joined with spaces.
    # Omit it to read the prompt from stdin instead.
    [Parameter(Position = 0, ValueFromRemainingArguments = $true)]
    [string[]] $Prompt,

    # Overrides — normally you want none of these.
    [string] $Model,
    [string] $Url,
    [string] $System,
    [int]    $TimeoutSec = 600,

    # Alternate modes.
    [switch] $Json,
    [switch] $ListModels,
    [switch] $ShowConfig
)


# Variables
$ErrorActionPreference = 'Stop'
$defaultUrl = 'http://localhost:11434/'
$projectRoot = Split-Path -Parent $PSScriptRoot

# Emit UTF-8 so non-ASCII answers survive being captured.
try { [Console]::OutputEncoding = New-Object System.Text.UTF8Encoding $false } catch { }


function Write-Fail {
    param([string] $Message)
    [Console]::Error.WriteLine("ollama-turn: $Message")
    exit 1
}


# Config discovery
# The GUI publishes its live config (URL + selected model) to %APPDATA%, so a
# caller needs to know nothing about where the app is installed. The remaining
# candidates cover a checkout that has been built but never launched.
function Get-TurnConfig {
    $candidates = @(
        $env:OLLAMA_TURN_CONFIG
        (Join-Path $env:APPDATA 'OllamaTurn\config.json')
        (Join-Path $projectRoot 'dist\config.json')
        (Join-Path $projectRoot 'bin\Debug\net9.0-windows\config.json')
        (Join-Path $projectRoot 'config.json')
    ) | Where-Object { $_ -and (Test-Path -LiteralPath $_ -PathType Leaf) }

    foreach ($path in $candidates) {
        try {
            $cfg = Get-Content -LiteralPath $path -Raw -Encoding UTF8 | ConvertFrom-Json
        } catch {
            continue
        }
        if ($cfg -and $cfg.url) {
            return [pscustomobject]@{
                Url    = ([string]$cfg.url).Trim().TrimEnd('/')
                Model  = if ($cfg.PSObject.Properties['model']) { [string]$cfg.model } else { '' }
                Source = $path
            }
        }
    }

    return [pscustomobject]@{
        Url    = $defaultUrl.TrimEnd('/')
        Model  = ''
        Source = '(default)'
    }
}


function Invoke-Ollama {
    param([string] $Path, [hashtable] $Body)

    $uri = "$($script:baseUrl)$Path"
    try {
        if ($null -eq $Body) {
            return Invoke-RestMethod -Uri $uri -Method Get -TimeoutSec $TimeoutSec
        }
        $bytes = [Text.Encoding]::UTF8.GetBytes(($Body | ConvertTo-Json -Depth 5 -Compress))
        return Invoke-RestMethod -Uri $uri -Method Post -TimeoutSec $TimeoutSec `
            -ContentType 'application/json; charset=utf-8' -Body $bytes
    } catch {
        Write-Fail "$($_.Exception.Message) ($uri)"
    }
}


# Resolve configuration
$config = Get-TurnConfig
$script:baseUrl = if ($Url) { $Url.Trim().TrimEnd('/') } else { $config.Url }

if ($ShowConfig) {
    [pscustomobject]@{
        url    = $script:baseUrl
        model  = if ($Model) { $Model } else { $config.Model }
        source = $config.Source
    } | ConvertTo-Json
    exit 0
}

if ($ListModels) {
    $tags = Invoke-Ollama -Path '/api/tags' -Body $null
    foreach ($m in $tags.models) { [Console]::Out.WriteLine($m.name) }
    exit 0
}


# Resolve the prompt
# Done before the model, so a missing prompt fails locally and instantly rather
# than after a round trip to the server.
$text = if ($Prompt) { ($Prompt -join ' ') } elseif ([Console]::IsInputRedirected) { [Console]::In.ReadToEnd() } else { '' }
if (-not $text.Trim()) {
    Write-Fail 'no prompt. Pass it as an argument or pipe it on stdin.'
}


# Resolve the model
$useModel = if ($Model) { $Model.Trim() } else { $config.Model }

if (-not $useModel) {
    # Nothing chosen in the GUI yet — a single installed model is unambiguous,
    # anything else needs a decision the CLI shouldn't make silently.
    $tags = Invoke-Ollama -Path '/api/tags' -Body $null
    $names = @($tags.models | ForEach-Object { $_.name })
    if ($names.Count -eq 1) {
        $useModel = $names[0]
    } elseif ($names.Count -eq 0) {
        Write-Fail "no models installed on $($script:baseUrl)."
    } else {
        Write-Fail ("no model selected. Pick one in the Ollama Turn app, or pass " +
                    "-Model. Available: " + ($names -join ', '))
    }
}


# Run the turn
$body = @{ model = $useModel; prompt = $text; stream = $false }
if ($System) { $body.system = $System }

$result = Invoke-Ollama -Path '/api/generate' -Body $body

if ($Json) {
    [Console]::Out.WriteLine(($result | ConvertTo-Json -Depth 5))
} else {
    [Console]::Out.WriteLine(([string]$result.response).Trim())
}

exit 0

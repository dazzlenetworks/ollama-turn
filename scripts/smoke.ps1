# Ollama Turn — smoke test: launch the app, verify it opened, close it

# Created 7/28/2026



param(
    # Seconds to wait for the window and then for the UI to report ready.
    [int] $TimeoutSec = 30,

    # How long to leave the window up once the UI has rendered, so the run is
    # visible rather than a flash on screen.
    [int] $HoldMs = 1200,

    # Skip the press-any-key tail so the script can run unattended.
    [switch] $NoPause
)



# Welcome
Write-Output "This script launches Ollama Turn, verifies the window actually opened, then closes it."
Write-Output "It is the smoke-test gate for a unit of work: exit code 0 means the app runs."



# Variables
$projectRoot = Split-Path -Parent $PSScriptRoot
$exe         = Join-Path $projectRoot "bin\Debug\net9.0-windows\OllamaTurn.exe"
$expectTitle = "Ollama Turn"
$closeWait   = 10
$exitCode    = 0
$proc        = $null



# Launch
Write-Output "`nLaunching:"
Write-Output "  $exe"

if (-not (Test-Path -LiteralPath $exe)) {
    Write-Output "`nThe exe is not there. Build it first with scripts\build.bat."
    $exitCode = 1
} else {
    $proc = Start-Process -FilePath $exe -PassThru
    Write-Output "  Started as PID $($proc.Id)."
}



# Verify the window opened
if ($exitCode -eq 0) {
    Write-Output "`nWaiting up to $TimeoutSec seconds for the main window..."

    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    $opened   = $false

    while ((Get-Date) -lt $deadline) {
        $proc.Refresh()
        if ($proc.HasExited) { break }
        if ($proc.MainWindowHandle -ne [IntPtr]::Zero) { $opened = $true; break }
        Start-Sleep -Milliseconds 250
    }

    if ($proc.HasExited) {
        Write-Output "  The process exited on its own (exit code $($proc.ExitCode)) - the app did not stay up."
        $exitCode = 1
    } elseif (-not $opened) {
        Write-Output "  No main window appeared within $TimeoutSec seconds."
        $exitCode = 1
    } else {
        Write-Output "  Window is open: `"$($proc.MainWindowTitle)`""
        if ($proc.MainWindowTitle -ne $expectTitle) {
            Write-Output "  Expected the title `"$expectTitle`" - continuing, but this is worth a look."
        }

        # The window handle exists well before WebView2 paints anything, so the
        # real check is the page's own ready signal: it posts 'ready' over the
        # RPC bridge once the cards are painted, and the host drops a marker
        # file for this PID. That proves the UI rendered and the bridge works.
        $marker = Join-Path $env:TEMP "OllamaTurn\ready-$($proc.Id).json"
        Write-Output "`nWaiting for the UI to report ready..."

        $deadline = (Get-Date).AddSeconds($TimeoutSec)
        $ready    = $false

        while ((Get-Date) -lt $deadline) {
            $proc.Refresh()
            if ($proc.HasExited) { break }
            if (Test-Path -LiteralPath $marker) { $ready = $true; break }
            Start-Sleep -Milliseconds 100
        }

        if (-not $ready) {
            if ($proc.HasExited) {
                Write-Output "  The process exited before the UI reported ready."
            } else {
                Write-Output "  The UI never reported ready within $TimeoutSec seconds."
                Write-Output "  (Expected marker: $marker)"
            }
            $exitCode = 1
        } else {
            Write-Output "  UI is rendered and the JS/C# bridge answered."
            Write-Output "  Holding it open for $HoldMs ms..."
            Start-Sleep -Milliseconds $HoldMs
            $proc.Refresh()
            if ($proc.HasExited) {
                Write-Output "  The app closed itself while we watched - treating that as a failure."
                $exitCode = 1
            }
        }
    }
}



# Close
if ($proc -and -not $proc.HasExited) {
    Write-Output "`nClosing the app..."
    [void]$proc.CloseMainWindow()

    if ($proc.WaitForExit($closeWait * 1000)) {
        Write-Output "  Closed cleanly."
    } else {
        Write-Output "  It ignored the close request; forcing it down."
        Stop-Process -Id $proc.Id -Force
        $exitCode = 1
    }
}



# Summary
Write-Output "`nSummary:"
if ($exitCode -eq 0) {
    Write-Output "  Ollama Turn launched, showed its window, and closed cleanly."
} else {
    Write-Output "  The smoke test failed - see the output above."
}



# Confirmation
if ($exitCode -eq 0) {
    Write-Output "`nOperation completed successfully"
} else {
    Write-Output "`nSmoke test failed"
}

if (-not $NoPause) {
    Read-Host -Prompt "`nPress any key to continue."

    Write-Output "`nExiting in 1.5 seconds...`n"

    Start-Sleep -s 1.5
}

exit $exitCode

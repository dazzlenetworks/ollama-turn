---
created: 7/21/2026 11:05
---

# Helper Scripts and Release Publishing

## Purpose

Document the `scripts/` folder: what each script does, the conventions they
follow, and how the self-contained release is produced. This consolidates the
project's build/run/distribute entry points in one place.

## Layout

All helper scripts live under `scripts/`, resolving the project root as the
parent of the scripts folder so they work regardless of the current directory:

```text
scripts/start.bat     Launch the app (builds first if the exe is missing)
scripts/build.bat     Build the Debug configuration
scripts/test.ps1      The test suite (CLI, config discovery, app smoke)
scripts/smoke.ps1     Launch the app, verify the window opened, close it
scripts/publish.ps1   Publish a self-contained single-file release into dist/
scripts/clean.ps1     Remove build artifacts (bin, obj, dist)
```

`scripts/start.bat` replaces the former root-level `start.bat`.

## Unit-of-Work Gates

The house development workflow runs every unit of work through change → build →
test → smoke test → spec → commit. This project's concrete commands are:

```text
Build       scripts\build.bat   (or: dotnet build -c Debug)
            plus: dotnet build cli\OllamaTurnCLI.csproj -c Debug
Test        powershell -NoProfile -ExecutionPolicy Bypass -File scripts\test.ps1 -NoPause
Smoke test  included in test.ps1 (or run scripts\smoke.ps1 -NoPause alone)
```

## The Test Suite

`scripts/test.ps1` codifies the checks that were previously run by hand after
every change. It exercises the **built artifacts** as a caller would — starting
the real exe, feeding it arguments and stdin, and asserting on stdout, stderr and
the exit code separately — rather than testing code in isolation. For a tool
whose contract *is* "stdout is the answer, stderr is the diagnostics, the exit
code is the verdict", that is the thing worth testing.

Coverage: CLI basics and error paths, config discovery (all ranks, plus corrupt
and url-less files), live turns, the settings-writing paths, and the app smoke
test as the final case.

### Rules It Follows

- **Never touch a real config.** Every case gets its own file in a temporary
  sandbox, wiped at the end. A test run cannot change which model you have
  selected. The only real file read is the one beside the CLI, and only to find
  a server URL to probe.
- **Isolated exe copies for fallback cases.** A test asserting what happens with
  no config must run a copy of the exe in an empty folder — otherwise discovery
  rank 2 finds the *real* `config.json` beside the shipped exe and answers with
  it. Getting this wrong made three tests fail for the wrong reason.
- **Skip, don't fail, when there is no server.** Server-dependent cases report
  SKIP so the suite stays useful offline.
- **Pick a model that can generate.** Embedding models answer `/api/tags` but
  reject `/api/generate`, and one may be first in the list; the preflight prefers
  a name without "embed" in it.

`-SkipServer` and `-SkipApp` narrow a run; `-NoPause` keeps it unattended;
`-Cli <path>` points it at a specific build (it defaults to the published exe,
falling back to the Debug build).

### What the First Run Found

A real bug, immediately: with stdin redirected, .NET can emit a UTF-8 byte order
mark, and `string.IsNullOrWhiteSpace` does not consider `U+FEFF` whitespace. An
empty pipe therefore read as a prompt of one invisible character, and a piped
file with a BOM — ordinary on Windows — had one prepended to the prompt. The CLI
now strips a leading BOM from stdin, and a test pins the behaviour.

This is the argument for the harness in miniature: the case had been run by hand
several times without ever redirecting stdin the way a real caller does.

## The Smoke Test

`scripts/smoke.ps1` is the gate that answers "does the app actually run?" It:

1. Launches `bin\Debug\net9.0-windows\OllamaTurn.exe` with `-PassThru`.
2. Polls the process for a non-zero `MainWindowHandle`, up to `-TimeoutSec`
   (default 30), and reports the window title it found.
3. Waits for the **UI ready signal** described below — the real gate.
4. Holds the window open for `-HoldMs` (default 1200) so the run is visible
   rather than a flash on screen. The hold starts from the ready signal, so a
   short one is enough — it is dwell time on a rendered UI, not a guess at how
   long startup takes.
5. Closes it with `CloseMainWindow()`, falling back to a forced stop.

It exits `0` only if the UI reported ready and the app was still alive at the end
of the hold. No window, no ready signal, an early exit, or a close that had to be
forced all exit `1`.

### The UI Ready Signal

A `MainWindowHandle` appears almost immediately — well before WebView2 has
painted anything — so waiting on the window alone closes the app before the
controls render. The signal instead comes from the page itself:

- After `init()` reads the config, `index.html` waits two animation frames (so
  the cards are painted, not merely parsed) and posts the `ready` action.
- The host writes `%TEMP%\OllamaTurn\ready-<pid>.json` and deletes it in
  `OnClosed`. The file is per-process, so concurrent or stale runs can't be
  mistaken for each other.
- The smoke test polls for the marker matching the PID it launched.

This makes the gate meaningfully stronger than "a window exists": it proves the
page rendered *and* that the JS↔C# bridge is answering. Model loading is
deliberately kicked off after the signal, so an unreachable Ollama server can't
stall the smoke test.

`-NoPause` skips the press-any-key tail so an unattended caller doesn't hang;
the build gate is deliberately *not* folded in, so a smoke failure can't be
confused with a compile failure.

### Encoding Constraint

The `.ps1` files carry no BOM, so Windows PowerShell 5.1 decodes them as CP1252.
An em dash inside a **string literal** decodes to a smart quote, which PowerShell
treats as a string delimiter — it swallows the rest of the file and the script
dies with a bogus "Missing closing '}'". Em dashes are therefore fine in comments
(where mis-decoding is harmless) but must stay out of executable strings.

## Conventions

The scripts loosely follow the house Utilities style guides:

- **Batch:** `::` comment headers with `:: Created M/D/YYYY`, `@echo off` +
  `setlocal`, section comments, and an `exitCode` pattern with a success/fail
  message and pause.
- **PowerShell:** a `# Created` header, a welcome message, a `# Variables`
  block, section headings with `Write-Output` (using `` `n `` newlines), a
  **Summary**, and the standard confirmation tail (`Operation completed
  successfully` → press-any-key → "Exiting in 1.5 seconds").

### Wrapper Exception

`start.bat` is a thin wrapper that hands control to the GUI app. Per the batch
style guide's own note, a wrapper should not duplicate success chatter or a
pause; `start.bat` therefore only pauses when it fails to build, and otherwise
launches and exits quietly.

## Release Publishing

`publish.ps1` publishes **both** front ends into `dist/`. The app:

```text
dotnet publish -c Release -r win-x64 --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -o dist
```

then the CLI into the same folder, additionally trimmed:

```text
dotnet publish cli\OllamaTurnCLI.csproj -c Release -r win-x64 --self-contained true \
    -p:PublishSingleFile=true \
    -p:PublishTrimmed=true \
    -o dist
```

Result:
- `dist/OllamaTurn.exe` (~129 MB) and `dist/OllamaTurnCLI.exe` (~14 MB), both
  self-contained, running on a machine without the .NET SDK. The WebView2
  Runtime is still required for the app, but it ships with modern Windows 10/11
  and Edge.
- `IncludeNativeLibrariesForSelfExtract` ensures the WebView2 native loader is
  extracted at runtime; this was smoke-tested by launching the published exe.
- Trimming is applied only to the CLI. WPF does not support it, and the CLI is
  only trim-safe because `shared/` avoids reflection-based JSON — see
  [[2026-0728-1620 cli path for agents]].
- `config.json` and the `www/` UI are copied alongside them, so the folder is
  ready to zip and distribute (e.g. as a GitHub Release asset). Both exes read
  that one `config.json`, so the release folder must always contain one.
- Publishing a second project into the same folder can remove the `config.json`
  the first one placed there, so the script re-seeds it from the project default
  afterwards **only when it is missing** — never overwriting a configured file.

`publish.ps1` takes `-NoPause`, like `smoke.ps1`, so it can run unattended.

`dist/` is gitignored.

## Related

- [[2026-0721-1033 build run and distribution]]
- [[2026-0721-1028 project overview and intent]]

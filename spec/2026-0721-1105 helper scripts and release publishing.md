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
scripts/publish.ps1   Publish a self-contained single-file release into dist/
scripts/clean.ps1     Remove build artifacts (bin, obj, dist)
```

`scripts/start.bat` replaces the former root-level `start.bat`.

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

`publish.ps1` runs:

```text
dotnet publish -c Release -r win-x64 --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -o dist
```

Result:
- A single, self-contained `dist/OllamaTurn.exe` (~130 MB) that runs on a
  machine without the .NET SDK. The WebView2 Runtime is still required, but it
  ships with modern Windows 10/11 and Edge.
- `IncludeNativeLibrariesForSelfExtract` ensures the WebView2 native loader is
  extracted at runtime; this was smoke-tested by launching the published exe.
- `config.json` and the `www/` UI are copied alongside the exe, so the folder is
  ready to zip and distribute (e.g. as a GitHub Release asset).

`dist/` is gitignored.

## Related

- [[2026-0721-1033 build run and distribution]]
- [[2026-0721-1028 project overview and intent]]

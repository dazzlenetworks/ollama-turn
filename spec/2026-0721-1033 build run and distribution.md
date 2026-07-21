---
created: 7/21/2026 10:33
---

# Build, Run, and Distribution

## Purpose

Record how the app is built, launched, and packaged for public distribution,
including the icon and repository hygiene decisions.

## Project Shape

- WPF app targeting `net9.0-windows` (`UseWPF`), a `WinExe`.
- Single NuGet dependency: `Microsoft.Web.WebView2`.
- The `www/` folder and the template `config.json` are copied next to the exe on
  build via `PreserveNewest`.

## Running It

- **`start.bat`** is the double-click launcher: it builds on first run if the exe
  is missing, then launches it. Otherwise `dotnet run -c Debug` works too.
- The build must not run while the app is open — the running process locks the
  exe. Close the app before rebuilding.

## Application Icon

The icon is **original**, generated programmatically with `System.Drawing`: a
rounded square with an accent gradient, a white chevron (the "turn"), and a dot
(the response). It is rendered at multiple sizes (16–256px) as PNG frames and
assembled into a proper multi-size `app.ico`, wired in via `<ApplicationIcon>`
so it appears in the title bar, taskbar, and the exe in Explorer.

## Repository Hygiene

- `.gitignore` excludes `bin/`, `obj/`, the WebView2 user-data folder, and IDE
  and OS cruft.
- `.gitattributes` normalizes line endings (LF in the repo; CRLF for `.bat`) and
  marks `.ico`/`.png` as binary.
- Commit identity uses a GitHub **noreply** email so no private address enters
  history.
- Licensed **MIT**, © Dazzle Networks (https://dazzlenetworks.com).

## Distribution

- The repository is **public** on GitHub.
- Before the first commit, tracked files were scanned for secrets, internal
  hostnames, and personal data; the internal default URL was genericized to
  `localhost` (see [[2026-0721-1031 configuration storage model]]).

## Requirements

- WebView2 Runtime (standard on modern Windows 10/11 and Edge).
- .NET 9 SDK to build.

## Related

- [[2026-0721-1028 project overview and intent]]
- [[2026-0721-1031 configuration storage model]]

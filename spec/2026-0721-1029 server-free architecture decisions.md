---
created: 7/21/2026 10:29
---

# Server-Free Architecture Decisions

## Purpose

Record the options that were weighed for delivering a browser-grade UI with no
server to deploy, and why the WebView2 native app was chosen. This is the core
architectural "gold" of the project.

## Constraints That Shaped the Decision

1. No standing web server (no IIS, no Coolify).
2. Initially assumed: no Python and no Node available on the machine.
3. Later discovered: the full **.NET 9 SDK**, the Windows Desktop runtime, and
   the **WebView2 Runtime** are all present. This unlocked the best option.
4. The user wanted it to feel like a real app, with a pretty, modern UI.

## Options Considered

- **Static HTML + `localStorage`.** Simplest, but no real config file on disk,
  and browser-to-Ollama calls hit CORS.
- **Static HTML + `config.js`.** A `<script>`-loaded config file sidesteps the
  `file://` fetch/CORS block for *reading* config, but can't write back and
  still needs `OLLAMA_ORIGINS` for the Ollama calls.
- **`.hta` (HTML Application).** Can read/write a real `config.json` and avoids
  CORS, but renders in the legacy IE/Trident engine — functional, not pretty.
- **PowerShell `HttpListener` localhost app.** No install; serves the page and
  proxies Ollama. Good, but opens in a browser tab rather than feeling like an
  app, and is less pleasant to maintain than compiled code.
- **ASP.NET Core (Kestrel) localhost app.** Clean proxy code, real `config.json`,
  no CORS — but still a browser tab plus a localhost listener.
- **WebView2 native window app.** A WPF host around a WebView2 control: a native
  window rendering a modern Chromium page.

## Decision

**WebView2 native window app.** It uniquely satisfies every constraint at once:
- Native window, modern Chromium rendering (pretty).
- The C# host reads/writes a real `config.json` and makes the Ollama calls, so
  there is **no CORS** to configure.
- Ships as a double-click `.exe`; nothing to deploy.

## Trade-offs Accepted

- Slightly more moving parts than a static file: a NuGet dependency
  (`Microsoft.Web.WebView2`) and a small JS↔C# message bridge.
- Requires the WebView2 Runtime (already standard on modern Windows 10/11).

## Related

- [[2026-0721-1030 host proxy and cors avoidance]]
- [[2026-0721-1031 configuration storage model]]

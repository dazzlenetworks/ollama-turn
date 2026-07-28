---
created: 7/21/2026 10:30
---

# Host Proxy and CORS Avoidance

## Purpose

Explain the mechanism that lets the app talk to Ollama without any CORS setup,
and the JS↔C# bridge that makes it work. This is the design detail that keeps
the app friction-free for users.

## The CORS Problem It Solves

If the web page itself called `http://<host>:11434` via `fetch`, the browser
would treat it as a cross-origin request and Ollama would reject it unless the
server had `OLLAMA_ORIGINS` configured. Requiring users to change server-side
environment variables would defeat the "just works" goal.

## The Mechanism

The **C# host**, not the page, makes every Ollama HTTP call. The page never
issues a cross-origin request, so CORS simply does not apply.

- The UI is served from a stable virtual origin via
  `SetVirtualHostNameToFolderMapping("ollamaturn.app", <www folder>)`.
- The page and the host communicate over a small JSON-RPC-style protocol built
  on `WebView2` messaging.

## The RPC Bridge

- **Page → host:** `window.chrome.webview.postMessage({ id, action, payload })`.
- **Host → page:** `CoreWebView2.PostWebMessageAsJson({ id, ok, data, error })`.
- The page keeps a map of pending promises keyed by `id` and resolves/rejects
  them when the matching reply arrives.

### Actions

- `getConfig` / `saveConfig` — read/write the URL in `config.json`.
- `listModels` — host calls `GET /api/tags` and returns model names.
- `generate` — host calls `POST /api/generate` (`stream: false`) and returns the
  response text plus a couple of stats (duration, eval count).
- `closeApp` — host closes the window (used by "Copy & Close").
- `ready` — the page reports that its UI has painted; the host drops a
  per-process marker file that the smoke test watches for. See
  [[2026-0721-1105 helper scripts and release publishing]].

## Threading and Reliability

- Ollama calls use a single shared `HttpClient` with a generous timeout (10 min)
  to tolerate slow local models.
- Replies are posted on the UI thread via the dispatcher.
- Every action is wrapped so failures return `{ ok: false, error }` rather than
  crashing; the page surfaces the message.

## Related

- [[2026-0721-1029 server-free architecture decisions]]
- [[2026-0721-1032 user interface and interactions]]

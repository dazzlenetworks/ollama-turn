---
created: 7/21/2026 10:31
---

# Configuration Storage Model

## Purpose

Document how and where the Ollama server URL is stored, and the reasoning behind
having both a tracked template and a runtime copy. This resolves the original
"how does a simple page save settings to a file?" question.

## The Original Question

The user wanted the Ollama URL to live in a file next to the app, editable and
persistent, without a server. A plain `file://` page cannot write files, and
reading a sibling `config.json` via `fetch` is CORS-blocked. Moving to a WebView2
host solved this: the C# side has normal filesystem access.

## Storage Design

- The settings live in **`config.json`** as `{ "url": "...", "model": "..." }`.
- The file the app actually reads and writes at runtime is the copy **next to the
  exe** (`AppContext.BaseDirectory`), i.e. `bin\...\config.json`.
- The in-app textbox + **Save** button writes to that real file; the file can
  also be edited directly in a text editor.
- On first run, if the file is missing, the host creates it from the default.
- Corrupt or unreadable config falls back to the default rather than crashing.

## Tracked Template vs. Runtime Copy

- The repository tracks a **template** `config.json` at the project root with a
  neutral default of `http://localhost:11434/`.
- The build copies it to the output directory (`PreserveNewest`) so a fresh
  checkout runs sensibly, without clobbering a user's edited runtime copy.
- **Do not edit the tracked template casually.** `PreserveNewest` protects the
  runtime copy only while the template stays older than it. Touching the
  template makes it newer, and the next build overwrites the user's real
  settings with the committed `localhost` default. This was learned the hard
  way: adding a cosmetic `"model": ""` key to the template silently reset a
  working server URL. New keys need no template entry — a missing key already
  falls back to its default.
- The runtime copy under `bin/` is **gitignored**, so a user's real server URL
  never ends up in version control.

## Why `localhost` Is the Committed Default

The project is public. The committed default was genericized to
`http://localhost:11434/` — the canonical Ollama default — so no internal
hostname is published, and anyone cloning gets a sensible starting point. The
real server URL lives only in the local, gitignored runtime copy.

## Model Selection Persistence

`config.json` is the **single source of truth** for the selected model. The page
calls a `saveModel` action whenever the dropdown changes, and again once the
model list has loaded; on load the model named in the file is re-selected if it
is still installed.

The selection previously lived in the page's `localStorage`. That was abandoned,
not merely demoted: WebView2's user-data folder is not somewhere another program
can read, so the choice was invisible to the CLI (see
[[2026-0728-1620 cli path for agents]]). Keeping both would have meant two
sources of truth, with the stale one able to win — a hand-edited `config.json`
could be silently overridden by a leftover `localStorage` value.

The one-time cost of dropping it: on the first launch after the change, the
dropdown falls back to the first installed model instead of the last one used,
and immediately writes that choice to `config.json`.

## The Published Mirror

Every config write also publishes `{ url, model, source }` to
**`%APPDATA%\OllamaTurn\config.json`**, and the app republishes it on launch.
This is a one-directional publication for outside callers — the app never reads
it back, and a failure to write it leaves the app working normally. `source`
records which install produced it, so it is clear whose settings these are when
both a `dist\` copy and a `bin\` copy exist.

The runtime `config.json` next to the exe remains the app's own source of truth;
the mirror exists because outside callers cannot be expected to know where that
exe lives.

## Related

- [[2026-0721-1030 host proxy and cors avoidance]]
- [[2026-0721-1033 build run and distribution]]

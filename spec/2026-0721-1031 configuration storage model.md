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

- The URL is stored in **`config.json`** as `{ "url": "..." }`.
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
- The runtime copy under `bin/` is **gitignored**, so a user's real server URL
  never ends up in version control.

## Why `localhost` Is the Committed Default

The project is public. The committed default was genericized to
`http://localhost:11434/` — the canonical Ollama default — so no internal
hostname is published, and anyone cloning gets a sensible starting point. The
real server URL lives only in the local, gitignored runtime copy.

## Model Selection Persistence

The last-used model is remembered in the page's `localStorage` (persisted by
WebView2's user-data folder) and re-selected on load if still installed. See
[[2026-0721-1032 user interface and interactions]].

## Related

- [[2026-0721-1030 host proxy and cors avoidance]]
- [[2026-0721-1033 build run and distribution]]

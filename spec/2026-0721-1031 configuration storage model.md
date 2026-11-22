---
created: 7/21/2026 10:31
---

# Configuration Storage Model

## Purpose

Document how and where the Ollama server URL is stored, and why the repository
ships an example of the file rather than a copy of it. This resolves the original
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
- A missing file reads as the built-in default; the host writes the file the
  first time it saves a setting — the Save button, or the model being recorded
  once the list has loaded.
- Corrupt or unreadable config falls back to the default rather than crashing.

## An Example, Not a Template

- The repository tracks **`config.example.json`** at the project root, holding
  the neutral default `http://localhost:11434/`. It documents the file's shape
  and nothing copies it anywhere.
- **No build or publish writes a `config.json`.** It used to: the repository
  tracked a template `config.json` that the build copied beside the exe with
  `PreserveNewest`. That protected the runtime copy only while the template
  stayed older than it — adding a cosmetic `"model": ""` key to the template once
  silently reset a working server URL — and a publish follows the same rule, so
  once the template was the newer file, publishing into a configured release
  folder overwrote the real one too. The code's built-in default made the copy unnecessary,
  so it was removed rather than guarded.
- **Removing the copy is not enough on its own.** A build made while the rule
  existed lists `config.json` among its outputs, and MSBuild's incremental clean
  deletes a listed output that the current build no longer produces — so the first
  build after the rule went would delete a configured runtime copy. The project
  therefore declares `$(OutDir)config.json` as one of its own outputs
  (`FileWrites`, in a target that runs before `_CleanGetCurrentAndPriorFileWrites`)
  without writing it. Hooked any later — on `IncrementalClean` itself — it does
  not work, because the list has already been taken. `dotnet clean` still deletes
  the runtime copy, as it did before.
- New keys need no entry in the example to work — a missing key already falls
  back to its default — though the example should show any a user may set.
- `config.json` at the repository root is **gitignored**, and the runtime copies
  under `bin/` and `dist/` are ignored with their folders, so a real server URL
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

## One File, No Copies

`config.json` next to the executables is the **only** place settings live. The
app writes it, `OllamaTurnCLI.exe` reads it from its own folder, and a person can
edit it in Notepad. Discovery for both front ends is:

1. `%OLLAMA_TURN_CONFIG%`, when a caller wants to pin one file
2. `config.json` beside the executable
3. the built-in `http://localhost:11434/`

### The App Follows External Changes

Because the file is shared — the CLI's `--set-model`, a text editor, another
machine's file sync — a running window would otherwise sit on settings that are
no longer true. The host watches `config.json` with a `FileSystemWatcher` and
pushes a `configChanged` event to the page, which updates the URL box and the
model dropdown and flashes "Updated from config.json".

Three details make this behave:

- **Settling.** A save is rarely one event: editors write, truncate and rename,
  and a sync can land a file in pieces. Events restart a 400 ms timer and the
  file is read once, after the noise stops.
- **No echo.** The host records what the page already knows on every read and
  write, and only raises the event when the file differs. Without that, the
  app's own saves would come straight back as "the config changed".
- **No write-back.** The page assigns `.value` directly, which does not fire a
  `change` event, and sets its remembered model first — so following a change
  cannot bounce a save at the host and start a loop.

A half-written file simply reads as unusable and is skipped; the next settled
event picks up the finished version.

### The Mirror That Was Removed

An earlier version of this feature copied `{ url, model, source }` to
`%APPDATA%\OllamaTurn\config.json` on every write, so a caller that didn't know
where the app was installed could still find the settings. It was removed.

The reasoning: `OllamaTurnCLI.exe` ships **in the same folder as the app**, so it
already knows where the config is — `AppContext.BaseDirectory`. The mirror only
earned its keep in a split that development created (app running from `bin\`,
CLI invoked from `dist\`), not in any real install.

What it cost was worse than what it bought. A copy of data can go stale, and it
did: with the project folder synced between two machines, the synced
`config.json` said one model while the un-synced per-machine mirror said another,
and the mirror outranked the file. The CLI confidently used the wrong model.

A copy of the settings can disagree with the settings. One file cannot.

The trade accepted: copying `OllamaTurnCLI.exe` on its own to somewhere outside
the app folder leaves it with nothing to read, and it falls back to the default.
The answer is to put the app's folder on `PATH` rather than move the exe out of
it, or to set `OLLAMA_TURN_CONFIG`. If "the exe works anywhere" is ever wanted,
the honest form is a *pointer* file naming the install directory — never a second
copy of the values.

## Related

- [[2026-0721-1030 host proxy and cors avoidance]]
- [[2026-0721-1033 build run and distribution]]

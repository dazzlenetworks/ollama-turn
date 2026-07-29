---
created: 7/28/2026 16:20
---

# CLI Path for Agents

## Purpose

Record the command-line entry point that lets an agent (or any script) run local
inference through this project's configuration without knowing anything about
it. This is the first capability in Ollama Turn aimed at a non-human caller.

## The Goal

An agent should be able to run:

```text
scripts\ollama-turn.cmd "summarize this in one line: ..."
```

and get back model output on stdout — no server URL, no model name, no flags, no
knowledge that a GUI exists. Whatever the GUI is pointed at is what the agent
gets.

## The Obstacle: The Selection Was Invisible

The server URL already lived in a real file (`config.json`, see
[[2026-0721-1031 configuration storage model]]), but the **selected model** was
only ever kept in the page's `localStorage`, inside WebView2's user-data folder.
Nothing outside the app could read it. Two changes fixed that:

1. **The model is persisted to `config.json`.** The page calls a new `saveModel`
   action whenever the dropdown changes, and once more after the model list
   loads, so the file always names the model actually in use. `localStorage` was
   dropped outright rather than kept as a fallback, leaving one source of truth.
2. **The config is published to a fixed path.** Every write also mirrors
   `{ url, model, source }` to `%APPDATA%\OllamaTurn\config.json`, and the app
   republishes it on launch. `source` records which install wrote it.

The mirror is what makes "zero knowledge" possible: the caller does not need to
know whether the app is running from `bin\Debug\...`, from `dist\`, or from
wherever a release was unzipped. The app never reads the mirror — it is a
publication, one-directional, and the app is unaffected if it can't be written.

## The CLI

```text
scripts\ollama-turn.cmd    Shim: forwards to the .ps1 with the right flags
scripts\ollama-turn.ps1    The CLI itself
```

The `.cmd` exists so any shell can call the tool without knowing about
`-ExecutionPolicy Bypass`.

### Behavior

- The prompt is positional (remaining arguments are joined), or read from stdin
  when piped. Empty prompt is an error, not an empty turn.
- stdout carries **only** the response text. Diagnostics go to stderr, and the
  exit code is `0` on success, `1` on any failure.
- `-Json` emits the raw Ollama response object instead, for callers that want
  the timing and token counts.
- `-ListModels`, `-ShowConfig` for inspection; `-Model`, `-Url`, `-System`,
  `-TimeoutSec` (default 600) for overrides.

### Config Resolution

First hit wins:

1. `$env:OLLAMA_TURN_CONFIG`
2. `%APPDATA%\OllamaTurn\config.json` — the mirror the GUI publishes
3. `dist\config.json`, then `bin\Debug\net9.0-windows\config.json`
4. the tracked template at the project root
5. failing all that, `http://localhost:11434/`

Steps 3 and 4 cover a checkout that has been built but never launched.

### When No Model Is Configured

If nothing has been selected yet, the CLI asks the server for its models. Exactly
one installed model is unambiguous, so it uses it. Zero or several is a decision
the CLI should not make silently: it fails with the available names listed.

## Why Not a Console Mode on the exe

`OllamaTurn.exe` is a `WinExe` with no console attached, so a `--prompt` mode
would need `AttachConsole` and would behave differently piped than interactively.
A script also keeps working when the GUI is closed and needs no rebuild to
change. The CLI talks to Ollama directly rather than driving the app, so the
running GUI is irrelevant to it.

## House Style Exception

The other PowerShell helpers open with a welcome banner and end with a
press-any-key tail. `ollama-turn.ps1` deliberately does neither: its stdout is
consumed by another program. This is the same wrapper exception already granted
to `start.bat` in [[2026-0721-1105 helper scripts and release publishing]].

Note the encoding constraint recorded in that document: em dashes are safe in
comments but must stay out of string literals in `.ps1` files.

## Out of Scope

- Multi-turn conversation state (the app is single-turn by design, see
  [[2026-0721-1028 project overview and intent]]).
- Streaming output — the CLI uses `stream: false` like the GUI.
- Writing config from the CLI; it reads what the GUI publishes.

## Related

- [[2026-0721-1031 configuration storage model]]
- [[2026-0721-1030 host proxy and cors avoidance]]
- [[2026-0721-1105 helper scripts and release publishing]]

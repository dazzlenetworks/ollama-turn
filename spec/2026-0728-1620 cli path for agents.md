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
OllamaTurnCLI "summarize this in one line: ..."
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

## Shape: A Second Executable

`OllamaTurn.exe` is a `WinExe` — Windows subsystem, no console attached — so it
cannot be its own CLI. Printing to a terminal from it would need `AttachConsole`
and would still behave differently piped than interactively. This is the same
reason VS Code ships `code.cmd` next to `Code.exe`.

The project therefore builds a **second, console-subsystem executable**:

```text
cli\OllamaTurnCLI.csproj   Console project, AssemblyName OllamaTurnCLI
cli\Program.cs             Argument parsing and the output contract
shared\TurnConfig.cs       Config discovery, writing, publishing
shared\OllamaApi.cs        GET /api/tags and POST /api/generate
```

`shared\` is compiled into **both** projects (the WPF project picks it up by
default globbing; the CLI project includes it explicitly, and `cli\**` is
excluded from the WPF project so its `Main` doesn't collide). The window and the
CLI are two front ends over one core, which is the point: a PowerShell
reimplementation was rejected precisely because it would drift.

An earlier iteration of this feature *was* a `.ps1` plus a `.cmd` shim. It
worked, but a headline feature resolving to a script depends on PowerShell,
can't ship in `dist\`, and duplicates logic the app already had.

### Behavior

- The prompt is positional (remaining arguments are joined), or read from stdin
  when piped. Empty prompt is an error, not an empty turn.
- stdout carries **only** the response text. Diagnostics go to stderr, and the
  exit code is `0` on success, `1` on any failure. No banner, no colour.
- `--json` emits the raw Ollama response object instead, for callers that want
  the timing and token counts.
- `--list-models`, `--show-config` for inspection; `--model`, `--url`,
  `--system`, `--timeout` (default 600s) for overrides; `--help` for usage.
- An unrecognised `-switch` is an error rather than prompt text — far more
  likely a typo than a one-word question.

### Distribution

`publish.ps1` publishes both projects into `dist\`, so a release folder holds
`OllamaTurn.exe`, `OllamaTurnCLI.exe`, one `config.json`, and `www\`. The CLI is
published **trimmed** (~14 MB rather than ~68 MB); this is only safe because
`shared\` uses `Utf8JsonWriter` and `JsonDocument` rather than reflection-based
serialization, and that constraint should be kept. The app itself cannot be
trimmed — WPF doesn't support it.

### Config Resolution

First usable candidate wins:

1. `%OLLAMA_TURN_CONFIG%`, for a caller that wants to pin one explicitly
2. `%APPDATA%\OllamaTurn\config.json` — the mirror, i.e. the live settings of
   whichever install ran most recently
3. `config.json` sitting beside the CLI exe — a distributed `dist\` folder
4. failing all that, `http://localhost:11434/`

The mirror deliberately outranks the sibling file: the promise is "the model the
app has selected", and the mirror is the one that tracks the running app. The
sibling covers a release folder whose app has never been launched.

### When No Model Is Configured

If nothing has been selected yet, the CLI asks the server for its models. Exactly
one installed model is unambiguous, so it uses it. Zero or several is a decision
the CLI should not make silently: it fails with the available names listed.

## Independence From the Running App

The CLI talks to Ollama directly rather than driving the window, so the app does
not need to be open. It reads published settings; it never writes them.

## Out of Scope

- Multi-turn conversation state (the app is single-turn by design, see
  [[2026-0721-1028 project overview and intent]]).
- Streaming output — the CLI uses `stream: false` like the GUI.
- Writing config from the CLI; it reads what the GUI publishes.

## Related

- [[2026-0721-1031 configuration storage model]]
- [[2026-0721-1030 host proxy and cors avoidance]]
- [[2026-0721-1105 helper scripts and release publishing]]

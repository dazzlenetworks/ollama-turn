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

**The model is persisted to `config.json`.** The page calls a `saveModel` action
whenever the dropdown changes, and once more after the model list loads, so the
file always names the model actually in use. `localStorage` was dropped outright
rather than kept as a fallback, leaving one source of truth.

What makes "zero knowledge" work is that the CLI ships in the same folder as the
app, so `config.json` is simply the file beside its own exe. An intermediate
design published a copy of the settings to `%APPDATA%` so a caller need not know
the install location; that copy went stale and was removed. See
[[2026-0721-1031 configuration storage model]].

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

### Reading and Writing Settings

`--model` and `--url` override a single turn. `--set-model` and `--set-url`
change the configured values and exit, so an agent can select a model without
anyone opening the app. They write back to the file discovery found, or create
one beside the exe if there wasn't one.

The two are validated differently, on purpose:

- **`--set-model` is checked against `/api/tags` and refuses a name the server
  doesn't have**, writing nothing. A bad model name would otherwise sit in the
  config and break every later run, long after the typo.
- **`--set-url` is written first and only warned about** if that server doesn't
  answer. A server may legitimately be configured before it is up, and refusing
  to save would leave no way to point at a host that is temporarily down.

A running app follows the change: it watches `config.json` and updates its URL box
and model dropdown live, so `--set-model` takes effect in an open window without
a restart. See [[2026-0721-1031 configuration storage model]].

### Distribution

`publish.ps1` publishes both projects into `dist\`, so a release folder holds
`OllamaTurn.exe`, `OllamaTurnCLI.exe`, one `config.json`, and `www\`. The CLI is
published **trimmed** (~14 MB rather than ~68 MB); this is only safe because
`shared\` uses `Utf8JsonWriter` and `JsonDocument` rather than reflection-based
serialization, and that constraint should be kept. The app itself cannot be
trimmed — WPF doesn't support it.

### Config Resolution

First usable candidate wins:

1. `%OLLAMA_TURN_CONFIG%`, for a caller that wants to pin one file explicitly
2. `config.json` beside the CLI exe — the normal case, since both executables
   ship in one folder
3. failing that, `http://localhost:11434/`

An unreadable or URL-less file is treated as absent, so a corrupt config falls
through to the next candidate rather than failing the run.

### When No Model Is Configured

If nothing has been selected yet, the CLI asks the server for its models. Exactly
one installed model is unambiguous, so it uses it. Zero or several is a decision
the CLI should not make silently: it fails with the available names listed.

## Independence From the Running App

The CLI talks to Ollama directly rather than driving the window, so the app does
not need to be open. It reads published settings; it never writes them.

## One Config Per Install, Deliberately

The dev build (`bin\Debug\...`), the published `dist\` folder, and anything
unzipped from a GitHub release each keep their own `config.json`. That is
correct rather than a limitation: they are separate installations, and a machine
may reasonably want the dev copy pointed somewhere other than the release copy.

It does mean the CLI in one folder does not follow the app in another. Since the
CLI can now both query and set the model, an agent working against a given
install has everything it needs without the GUI at all.

## Out of Scope

- Multi-turn conversation state (the app is single-turn by design, see
  [[2026-0721-1028 project overview and intent]]).
- Streaming output — the CLI uses `stream: false` like the GUI.
- Pulling or deleting models; `--set-model` only chooses among installed ones.

## Related

- [[2026-0721-1031 configuration storage model]]
- [[2026-0721-1030 host proxy and cors avoidance]]
- [[2026-0721-1105 helper scripts and release publishing]]

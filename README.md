# Ollama Turn

A tiny Windows desktop app for doing a **single request → single response** turn against a remote Ollama server. Native window, modern Chromium (WebView2) UI, no separate web server, no CORS setup.

It also **exports turns to Markdown** in a clean, frontmatter'd format — and can **append** successive turns to the same file, so a deliberately single-turn app doubles as a multi-turn transcript builder.

And it ships a second executable, **`OllamaTurnCLI.exe`**, that gives scripts and coding agents local inference with zero configuration of their own: `OllamaTurnCLI "your prompt"` uses whatever server and model the app is set to. [Jump to the CLI](#cli-zero-config-inference-for-agents-and-scripts).

![Ollama Turn — the app window showing the server, prompt, and response cards](docs/screenshot.png)

## Run it

Double-click **`scripts\start.bat`** (it builds on first run, then just launches the exe).

Or from a terminal:

```
dotnet run -c Debug
```

## CLI: zero-config inference for agents and scripts

`OllamaTurnCLI.exe` runs a turn from the command line against **whatever server
and model the app is currently set to** — nothing to configure, no flags to
remember:

```
OllamaTurnCLI "why is the sky blue?"
```

```
Because shorter (blue) wavelengths scatter more in the atmosphere...
```

That is the whole interface. stdout carries the response text and nothing else,
so it pipes and captures cleanly; anything that goes wrong goes to stderr with
exit code `1`. The prompt can arrive on stdin instead:

```
type notes.md | OllamaTurnCLI
```

**Why it needs no configuration:** the app publishes its live settings — the
server URL *and* the model selected in the dropdown — to
`%APPDATA%\OllamaTurn\config.json` every time either changes. The CLI reads that,
so a caller needs to know nothing about where the app is installed or which model
you're on. Switch models in the dropdown and the next CLI call follows you. The
app doesn't have to be running.

If no model has been selected yet, one installed model is used automatically;
otherwise the CLI lists what's available and stops rather than guessing.

Overrides, when you do want them:

```
-m, --model <name>    Use a specific model instead of the published one
    --url <url>       Point at a different Ollama server
-s, --system <text>   Prepend a system prompt
-t, --timeout <sec>   Request timeout (default 600)
    --json            Emit the raw Ollama response (timings, token counts)
    --list-models     List the models installed on the server
    --show-config     Show the resolved URL, model, and where they came from
-h, --help            Show usage
```

It's a real console executable, published beside `OllamaTurn.exe` in `dist\` and
self-contained, so a release folder gives you both front ends with nothing to
install. Put that folder on your PATH and `OllamaTurnCLI` works from anywhere.

## Scripts

Helper scripts live in [`scripts/`](scripts):

```
scripts\start.bat      Launch the app (builds first if needed)
scripts\build.bat      Build the Debug configuration
scripts\smoke.ps1      Launch the app, verify the window opened, close it
scripts\publish.ps1    Publish both self-contained exes into dist\
scripts\clean.ps1      Remove build artifacts (bin, obj, dist)
```

`smoke.ps1` is the "does it actually run?" gate — it exits `0` only if the window
opened and the app was still up afterwards. Run it unattended with:

```
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\smoke.ps1 -NoPause
```

## How it works

- The app is a small WPF host around a **WebView2** control. The UI lives in [`www/index.html`](www/index.html).
- The **C# host** (not the browser page) makes the HTTP calls to Ollama, so there is **no CORS** to configure — you do **not** need `OLLAMA_ORIGINS`.
- The window and the CLI are two front ends over one core: config discovery and the Ollama calls live in [`shared/`](shared) and are compiled into both, so they can't drift apart.
- The Ollama URL and the selected model are stored in **`config.json`** sitting next to the exe. The textbox + **Save** button writes to that real file on disk, and picking a model in the dropdown updates it too.
  - Default: `http://localhost:11434/` (point it at your own Ollama host, local or on the network).
  - You can edit `config.json` directly in Notepad, or change it in the app.
  - A copy is published to `%APPDATA%\OllamaTurn\config.json` for the CLI to read; the app never reads it back.

## Using it

1. Confirm/enter the Ollama server URL, click **Save** (or **Refresh models**).
2. Pick a model from the dropdown (loaded from `GET /api/tags`).
3. Type a message and click **Send** (or just press **Enter**; **Shift+Enter**
   gives you a new line).
   - Uses `POST /api/generate` with `stream: false`.

## Exporting to Markdown

Click **Export** (bottom-right of the response) to save the turn as a `.md` file:

- The current model generates a short Title Case name; the file is proposed as
  `YYYY-MMdd-HHmm Ollama Turn export - <Generated Title>.md` via a Save As dialog.
- **Pick a new file** → a fresh document is written.
- **Pick an existing file** → choose **Append**, **Overwrite**, or **Cancel**.
  *Append* adds the new turn under the existing frontmatter and updates the
  `updated:` timestamp — run a turn, append, repeat to build a multi-turn record.

Output format (YAML frontmatter + alternating prompt/response sections):

```markdown
---
created: 7/21/2026, 10:25:04 AM
updated: 7/21/2026, 10:25:15 AM
model: "Ollama — llama3:latest"
---

## User prompt
*7/21/2026, 10:25:04 AM*

Why is the sky blue?

## Ollama response
*7/21/2026, 10:25:15 AM*

Because shorter (blue) wavelengths scatter more in the atmosphere…
```

## Files

```
scripts/             Launch, build, smoke test, publish, and clean helpers
OllamaTurn.csproj    WPF + WebView2 project (the app window)
App.xaml(.cs)        App entry
MainWindow.xaml(.cs) WebView2 host + config I/O + Ollama proxy + Markdown export
www/index.html       The UI (HTML/CSS/JS, self-contained)
shared/              Config discovery + Ollama calls, compiled into both exes
cli/                 OllamaTurnCLI project: the command-line front end
config.json          { "url": "http://localhost:11434/" } — the app adds "model"
spec/                Design notes: the build story, the export format, the CLI
```

## Notes

- The `config.json` that matters at runtime is the copy next to the exe
  (`bin\Debug\net9.0-windows\config.json`). It's created from the project default
  on first run and preserved across rebuilds.
  - That preservation only holds while the tracked template stays untouched —
    editing it makes it newer, and the next build overwrites your real settings
    with the committed default. New keys don't need a template entry.
- Requires the **WebView2 Runtime** (ships with modern Windows 10/11 and Edge) and the **.NET 9 SDK**.

## License

MIT © [Dazzle Networks](https://dazzlenetworks.com) — see [LICENSE](LICENSE).

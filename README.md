# Ollama Turn

A tiny Windows desktop app for doing a **single request → single response** turn against a remote Ollama server. Native window, modern Chromium (WebView2) UI, no separate web server, no CORS setup.

It also **exports turns to Markdown** in a clean, frontmatter'd format — and can **append** successive turns to the same file, so a deliberately single-turn app doubles as a multi-turn transcript builder.

![Ollama Turn — the app window showing the server, prompt, and response cards](docs/screenshot.png)

## Run it

Double-click **`scripts\start.bat`** (it builds on first run, then just launches the exe).

Or from a terminal:

```
dotnet run -c Debug
```

## Scripts

Helper scripts live in [`scripts/`](scripts):

```
scripts\start.bat    Launch the app (builds first if needed)
scripts\build.bat    Build the Debug configuration
scripts\smoke.ps1    Launch the app, verify the window opened, close it
scripts\publish.ps1  Publish a self-contained single-file release into dist\
scripts\clean.ps1    Remove build artifacts (bin, obj, dist)
```

`smoke.ps1` is the "does it actually run?" gate — it exits `0` only if the window
opened and the app was still up afterwards. Run it unattended with:

```
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\smoke.ps1 -NoPause
```

## How it works

- The app is a small WPF host around a **WebView2** control. The UI lives in [`www/index.html`](www/index.html).
- The **C# host** (not the browser page) makes the HTTP calls to Ollama, so there is **no CORS** to configure — you do **not** need `OLLAMA_ORIGINS`.
- The Ollama URL is stored in **`config.json`** sitting next to the exe. The textbox + **Save** button writes to that real file on disk.
  - Default: `http://localhost:11434/` (point it at your own Ollama host, local or on the network).
  - You can edit `config.json` directly in Notepad, or change it in the app.

## Using it

1. Confirm/enter the Ollama server URL, click **Save** (or **Refresh models**).
2. Pick a model from the dropdown (loaded from `GET /api/tags`).
3. Type a message and click **Send** (or press **Ctrl+Enter**).
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
scripts/             Launch, build, publish, and clean helpers
OllamaTurn.csproj    WPF + WebView2 project
App.xaml(.cs)        App entry
MainWindow.xaml(.cs) WebView2 host + config I/O + Ollama proxy + Markdown export
www/index.html       The UI (HTML/CSS/JS, self-contained)
config.json          { "url": "http://localhost:11434/" }
spec/                Design notes: the build story and the export format
```

## Notes

- The `config.json` that matters at runtime is the copy next to the exe
  (`bin\Debug\net9.0-windows\config.json`). It's created from the project default
  on first run and preserved across rebuilds.
- Requires the **WebView2 Runtime** (ships with modern Windows 10/11 and Edge) and the **.NET 9 SDK**.

## License

MIT © [Dazzle Networks](https://dazzlenetworks.com) — see [LICENSE](LICENSE).

# Ollama Turn

A tiny Windows desktop app for doing a **single request → single response** turn against a remote Ollama server. Native window, modern Chromium (WebView2) UI, no separate web server, no CORS setup.

## Run it

Double-click **`start.bat`** (it builds on first run, then just launches the exe).

Or from a terminal:

```
dotnet run -c Debug
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

## Files

```
start.bat            Double-click launcher
OllamaTurn.csproj    WPF + WebView2 project
App.xaml(.cs)        App entry
MainWindow.xaml(.cs) WebView2 host + config I/O + Ollama proxy
www/index.html       The UI (HTML/CSS/JS, self-contained)
config.json          { "url": "http://localhost:11434/" }
```

## Notes

- The `config.json` that matters at runtime is the copy next to the exe
  (`bin\Debug\net9.0-windows\config.json`). It's created from the project default
  on first run and preserved across rebuilds.
- Requires the **WebView2 Runtime** (ships with modern Windows 10/11 and Edge) and the **.NET 9 SDK**.

## License

MIT © [Dazzle Networks](https://dazzlenetworks.com) — see [LICENSE](LICENSE).

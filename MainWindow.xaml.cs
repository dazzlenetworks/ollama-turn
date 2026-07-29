using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using OllamaTurn.Core;

namespace OllamaTurn
{
    public partial class MainWindow : Window
    {
        // Note Chat timestamps look like "7/21/2026, 10:25:04 AM" (en-US).
        private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");

        private sealed class Turn
        {
            public string BaseUrl;
            public string Model;
            public string Prompt;
            public string Response;
            public DateTime Created;
            public DateTime Updated;
        }

        // The most recent completed turn, cached so Export can reuse it.
        private Turn _lastTurn;
        private string _lastExportDir;

        // The config this app owns: the copy next to its own exe. Reading,
        // writing, and publishing it to the CLI's fixed path all live in
        // TurnConfig, shared with OllamaTurnCLI.
        private string ConfigPath => Path.Combine(AppContext.BaseDirectory, "config.json");

        // Dropped on disk once the page has loaded and talked to the host, so an
        // outside observer (the smoke test) can tell "window exists" apart from
        // "the UI is actually up". Named per-process so runs can't confuse each
        // other, and removed when the window closes.
        private static string ReadyMarkerPath => Path.Combine(
            Path.GetTempPath(), "OllamaTurn",
            "ready-" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture) + ".json");

        public MainWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await webView.EnsureCoreWebView2Async();

                var core = webView.CoreWebView2;

                // Serve the local www folder from a stable virtual origin.
                var wwwPath = Path.Combine(AppContext.BaseDirectory, "www");
                core.SetVirtualHostNameToFolderMapping(
                    "ollamaturn.app", wwwPath, CoreWebView2HostResourceAccessKind.Allow);

                // Lock the app down a bit — no devtools/context menu in normal use.
#if DEBUG
                core.Settings.AreDevToolsEnabled = true;
                core.Settings.AreDefaultContextMenusEnabled = true;
#else
                core.Settings.AreDevToolsEnabled = false;
                core.Settings.AreDefaultContextMenusEnabled = false;
#endif
                core.Settings.IsStatusBarEnabled = false;

                core.WebMessageReceived += OnWebMessage;

                core.Navigate("https://ollamaturn.app/index.html");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Failed to start the WebView2 host:\n\n" + ex.Message,
                    "Ollama Turn", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void OnWebMessage(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            long id = 0;
            try
            {
                using var doc = JsonDocument.Parse(e.WebMessageAsJson);
                var root = doc.RootElement;
                id = root.GetProperty("id").GetInt64();
                var action = root.GetProperty("action").GetString();
                var payload = root.TryGetProperty("payload", out var p) ? p : default;

                object data = action switch
                {
                    "getConfig" => GetConfig(),
                    "saveConfig" => SaveConfig(payload),
                    "saveModel" => SaveModel(payload),
                    "listModels" => await ListModelsAsync(payload),
                    "generate" => await GenerateAsync(payload),
                    "export" => await ExportAsync(),
                    "ready" => MarkReady(),
                    "closeApp" => CloseApp(),
                    _ => throw new InvalidOperationException("Unknown action: " + action)
                };

                Reply(id, true, data, null);
            }
            catch (Exception ex)
            {
                Reply(id, false, null, ex.Message);
            }
        }

        private void Reply(long id, bool ok, object data, string error)
        {
            var envelope = new { id, ok, data, error };
            var json = JsonSerializer.Serialize(envelope);
            // Ensure we post on the UI thread.
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => webView.CoreWebView2.PostWebMessageAsJson(json));
            }
            else
            {
                webView.CoreWebView2.PostWebMessageAsJson(json);
            }
        }

        // ---- Handlers -------------------------------------------------------

        // The page calls this once its UI is rendered and the bridge is working.
        private object MarkReady()
        {
            try
            {
                var path = ReadyMarkerPath;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var json = JsonSerializer.Serialize(new
                {
                    pid = Environment.ProcessId,
                    ready = Ts(DateTime.Now)
                });
                File.WriteAllText(path, json + Environment.NewLine);
            }
            catch
            {
                // A missing marker only costs an outside observer its signal.
            }
            return new { ready = true };
        }

        protected override void OnClosed(EventArgs e)
        {
            try { File.Delete(ReadyMarkerPath); } catch { }
            base.OnClosed(e);
        }

        private object CloseApp()
        {
            // Let the RPC reply flush, then close on the UI thread.
            Dispatcher.BeginInvoke(new Action(() => Close()));
            return new { closing = true };
        }

        private object GetConfig()
        {
            var (url, model) = ReadConfig();
            try
            {
                if (File.Exists(ConfigPath))
                {
                    // Republish on launch so the CLI follows the install that
                    // was most recently used.
                    PublishMirror(url, model);
                }
                else
                {
                    // First run — write the default so the file exists on disk.
                    WriteConfig(url, model);
                }
            }
            catch
            {
                // Config housekeeping must never stop the app from starting.
            }
            return new { url, model };
        }

        private object SaveConfig(JsonElement payload)
        {
            var url = payload.GetProperty("url").GetString();
            if (string.IsNullOrWhiteSpace(url))
                throw new InvalidOperationException("URL cannot be empty.");
            WriteConfig(url.Trim(), ReadConfig().model);
            return new { url = url.Trim() };
        }

        // The model picked in the dropdown is written through to config.json so
        // the CLI can run turns against it without being told which one.
        private object SaveModel(JsonElement payload)
        {
            var model = payload.GetProperty("model").GetString();
            if (string.IsNullOrWhiteSpace(model))
                throw new InvalidOperationException("Model cannot be empty.");
            WriteConfig(ReadConfig().url, model.Trim());
            return new { model = model.Trim() };
        }

        private (string url, string model) ReadConfig()
        {
            var cfg = TurnConfig.FromFile(ConfigPath);
            return cfg == null
                ? (TurnConfig.DefaultUrl, "")
                : (cfg.Url, cfg.Model);
        }

        private void WriteConfig(string url, string model)
        {
            TurnConfig.Write(ConfigPath, url, model, AppContext.BaseDirectory);
        }

        private static void PublishMirror(string url, string model)
        {
            TurnConfig.Publish(url, model, AppContext.BaseDirectory);
        }

        private static string BaseFrom(JsonElement payload)
        {
            return OllamaApi.NormalizeBase(payload.GetProperty("url").GetString());
        }

        private async Task<object> ListModelsAsync(JsonElement payload)
        {
            var names = await OllamaApi.ListModelsAsync(BaseFrom(payload));
            return new { models = names };
        }

        private async Task<object> GenerateAsync(JsonElement payload)
        {
            var baseUrl = BaseFrom(payload);
            var model = payload.GetProperty("model").GetString();
            var prompt = payload.GetProperty("prompt").GetString();
            if (string.IsNullOrWhiteSpace(model))
                throw new InvalidOperationException("No model selected.");

            var created = DateTime.Now;
            var turn = await OllamaApi.GenerateAsync(baseUrl, model, prompt);
            var updated = DateTime.Now;
            var response = turn.Response;

            // Cache the turn so Export can reuse it with accurate timestamps.
            _lastTurn = new Turn
            {
                BaseUrl = baseUrl,
                Model = model,
                Prompt = prompt,
                Response = response,
                Created = created,
                Updated = updated
            };

            return new { response, totalNs = turn.TotalNs, evalCount = turn.EvalCount };
        }

        // ---- Export ---------------------------------------------------------

        private async Task<object> ExportAsync()
        {
            var t = _lastTurn;
            if (t == null)
                throw new InvalidOperationException("Nothing to export yet — run a turn first.");

            var title = await MakeTitleAsync(t);
            var defaultName = $"{t.Created:yyyy-MMdd-HHmm} Ollama Turn export - {SanitizeFileName(title)}.md";

            // Show the Save As dialog on the UI thread.
            string chosen = null;
            await Dispatcher.InvokeAsync(() =>
            {
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Export turn to Markdown",
                    FileName = defaultName,
                    DefaultExt = ".md",
                    Filter = "Markdown (*.md)|*.md|All files (*.*)|*.*",
                    AddExtension = true,
                    OverwritePrompt = false // we handle append-vs-overwrite ourselves
                };
                if (!string.IsNullOrEmpty(_lastExportDir) && Directory.Exists(_lastExportDir))
                    dlg.InitialDirectory = _lastExportDir;

                if (dlg.ShowDialog(this) == true) chosen = dlg.FileName;
            });

            if (chosen == null) return new { canceled = true };

            bool appended = false;
            if (File.Exists(chosen))
            {
                // Existing file: append (multi-turn) / overwrite / cancel.
                var choice = MessageBoxResult.Cancel;
                await Dispatcher.InvokeAsync(() =>
                {
                    choice = MessageBox.Show(this,
                        $"\"{Path.GetFileName(chosen)}\" already exists.\n\n" +
                        "Yes  —  Append this turn to it\n" +
                        "No   —  Overwrite it\n" +
                        "Cancel  —  Do nothing",
                        "Export", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                });

                if (choice == MessageBoxResult.Cancel) return new { canceled = true };

                if (choice == MessageBoxResult.Yes)
                {
                    var existing = File.ReadAllText(chosen);
                    File.WriteAllText(chosen, AppendTurn(existing, t), Utf8NoBom);
                    appended = true;
                }
                else // No = overwrite
                {
                    File.WriteAllText(chosen, BuildDocument(t), Utf8NoBom);
                }
            }
            else
            {
                File.WriteAllText(chosen, BuildDocument(t), Utf8NoBom);
            }

            _lastExportDir = Path.GetDirectoryName(chosen);
            return new { path = chosen, appended };
        }

        private async Task<string> MakeTitleAsync(Turn t)
        {
            try
            {
                var prompt =
                    "Write a short, descriptive title for the following exchange. " +
                    "Use 3 to 7 words in Title Case. No quotes, no punctuation, no trailing period. " +
                    "Reply with only the title.\n\n" +
                    "User: " + t.Prompt + "\n\nAssistant: " + t.Response;

                var titled = await OllamaApi.GenerateAsync(t.BaseUrl, t.Model, prompt);
                var raw = titled.Response;
                var title = Regex.Replace((raw ?? "").Trim().Trim('"', '\'', '.'), @"\s+", " ").Trim();
                if (title.Length > 70) title = title.Substring(0, 70).Trim();
                return string.IsNullOrWhiteSpace(title) ? FallbackTitle(t) : title;
            }
            catch
            {
                return FallbackTitle(t);
            }
        }

        private static string FallbackTitle(Turn t)
        {
            var words = (t.Prompt ?? "").Split(
                new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            var take = string.Join(" ", words.Take(6));
            return string.IsNullOrWhiteSpace(take) ? "Untitled Turn" : take;
        }

        private static string SanitizeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c.ToString(), "");
            name = name.Trim().Trim('.');
            return string.IsNullOrWhiteSpace(name) ? "Untitled Turn" : name;
        }

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        private static string Ts(DateTime d) => d.ToString("M/d/yyyy, h:mm:ss tt", EnUs);

        private static string BuildSections(Turn t)
        {
            var sb = new StringBuilder();
            sb.Append("## User prompt\n");
            sb.Append('*').Append(Ts(t.Created)).Append("*\n\n");
            sb.Append((t.Prompt ?? "").Trim()).Append("\n\n");
            sb.Append("## Ollama response\n");
            sb.Append('*').Append(Ts(t.Updated)).Append("*\n\n");
            sb.Append((t.Response ?? "").Trim()).Append('\n');
            return sb.ToString();
        }

        private static string BuildDocument(Turn t)
        {
            var sb = new StringBuilder();
            sb.Append("---\n");
            sb.Append("created: ").Append(Ts(t.Created)).Append('\n');
            sb.Append("updated: ").Append(Ts(t.Updated)).Append('\n');
            sb.Append("model: \"Ollama — ").Append(t.Model).Append("\"\n");
            sb.Append("---\n\n");
            sb.Append(BuildSections(t));
            return sb.ToString();
        }

        private static string AppendTurn(string existing, Turn t)
        {
            existing = BumpUpdated(existing, Ts(t.Updated));
            return existing.TrimEnd() + "\n\n" + BuildSections(t);
        }

        // Update (or insert) the frontmatter `updated:` value; leave `created:` alone.
        private static string BumpUpdated(string text, string updatedTs)
        {
            if (text == null || !text.StartsWith("---")) return text;
            int close = text.IndexOf("\n---", 3, StringComparison.Ordinal);
            if (close < 0) return text;

            var frontmatter = text.Substring(0, close);
            var rest = text.Substring(close);
            var line = "updated: " + updatedTs;

            frontmatter = Regex.IsMatch(frontmatter, @"(?m)^updated:.*$")
                ? Regex.Replace(frontmatter, @"(?m)^updated:.*$", line)
                : frontmatter.TrimEnd('\n') + "\n" + line;

            return frontmatter + rest;
        }
    }
}

using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace OllamaTurn
{
    public partial class MainWindow : Window
    {
        private static readonly HttpClient Http = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(10)
        };

        private const string DefaultUrl = "http://localhost:11434/";

        private static readonly JsonSerializerOptions JsonOpts = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        private string ConfigPath => Path.Combine(AppContext.BaseDirectory, "config.json");

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
                    "listModels" => await ListModelsAsync(payload),
                    "generate" => await GenerateAsync(payload),
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

        private object CloseApp()
        {
            // Let the RPC reply flush, then close on the UI thread.
            Dispatcher.BeginInvoke(new Action(() => Close()));
            return new { closing = true };
        }

        private object GetConfig()
        {
            string url = DefaultUrl;
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var txt = File.ReadAllText(ConfigPath);
                    using var doc = JsonDocument.Parse(txt);
                    if (doc.RootElement.TryGetProperty("url", out var u) &&
                        u.ValueKind == JsonValueKind.String)
                    {
                        var val = u.GetString();
                        if (!string.IsNullOrWhiteSpace(val)) url = val;
                    }
                }
                else
                {
                    // First run — write the default so the file exists on disk.
                    WriteConfig(url);
                }
            }
            catch
            {
                // Corrupt/unreadable file — fall back to default, don't crash.
            }
            return new { url };
        }

        private object SaveConfig(JsonElement payload)
        {
            var url = payload.GetProperty("url").GetString();
            if (string.IsNullOrWhiteSpace(url))
                throw new InvalidOperationException("URL cannot be empty.");
            url = url.Trim();
            WriteConfig(url);
            return new { url };
        }

        private void WriteConfig(string url)
        {
            var json = JsonSerializer.Serialize(
                new { url }, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json + Environment.NewLine);
        }

        private static string BaseFrom(JsonElement payload)
        {
            var url = payload.GetProperty("url").GetString();
            if (string.IsNullOrWhiteSpace(url))
                throw new InvalidOperationException("Ollama URL is not set.");
            return url.Trim().TrimEnd('/');
        }

        private async Task<object> ListModelsAsync(JsonElement payload)
        {
            var baseUrl = BaseFrom(payload);
            var resp = await Http.GetAsync(baseUrl + "/api/tags");
            var body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"Ollama returned {(int)resp.StatusCode}: {body}");

            using var doc = JsonDocument.Parse(body);
            var names = new System.Collections.Generic.List<string>();
            if (doc.RootElement.TryGetProperty("models", out var models) &&
                models.ValueKind == JsonValueKind.Array)
            {
                foreach (var m in models.EnumerateArray())
                {
                    if (m.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String)
                        names.Add(n.GetString());
                }
            }
            return new { models = names };
        }

        private async Task<object> GenerateAsync(JsonElement payload)
        {
            var baseUrl = BaseFrom(payload);
            var model = payload.GetProperty("model").GetString();
            var prompt = payload.GetProperty("prompt").GetString();
            if (string.IsNullOrWhiteSpace(model))
                throw new InvalidOperationException("No model selected.");

            var reqBody = JsonSerializer.Serialize(new
            {
                model,
                prompt,
                stream = false
            });

            using var content = new StringContent(reqBody, Encoding.UTF8, "application/json");
            var resp = await Http.PostAsync(baseUrl + "/api/generate", content);
            var body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"Ollama returned {(int)resp.StatusCode}: {body}");

            using var doc = JsonDocument.Parse(body);
            var response = doc.RootElement.TryGetProperty("response", out var r)
                ? r.GetString() : "";

            // Pull a couple of useful stats if present.
            long? totalNs = doc.RootElement.TryGetProperty("total_duration", out var td) &&
                            td.ValueKind == JsonValueKind.Number
                ? td.GetInt64() : (long?)null;
            int? evalCount = doc.RootElement.TryGetProperty("eval_count", out var ec) &&
                             ec.ValueKind == JsonValueKind.Number
                ? ec.GetInt32() : (int?)null;

            return new { response, totalNs, evalCount };
        }
    }
}

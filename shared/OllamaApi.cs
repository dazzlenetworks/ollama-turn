using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace OllamaTurn.Core
{
    // The two Ollama endpoints this project is built on, in one place so the
    // window and the CLI cannot drift apart.
    //
    // Timeouts are per-request via CancellationToken rather than on the shared
    // HttpClient, so callers can pick their own patience.
    public static class OllamaApi
    {
        private static readonly HttpClient Http = new HttpClient
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

        public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(10);

        public static string NormalizeBase(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new InvalidOperationException("Ollama URL is not set.");
            return url.Trim().TrimEnd('/');
        }

        public sealed class Turn
        {
            public string Response;
            public long? TotalNs;
            public int? EvalCount;

            // The untouched response body, for callers that want everything.
            public string RawJson;
        }

        public static async Task<List<string>> ListModelsAsync(
            string baseUrl, TimeSpan? timeout = null)
        {
            var body = await SendAsync(HttpMethod.Get, baseUrl, "/api/tags", null, timeout);

            var names = new List<string>();
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("models", out var models) &&
                models.ValueKind == JsonValueKind.Array)
            {
                foreach (var m in models.EnumerateArray())
                {
                    if (m.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String)
                        names.Add(n.GetString());
                }
            }
            return names;
        }

        public static async Task<Turn> GenerateAsync(
            string baseUrl, string model, string prompt,
            string system = null, TimeSpan? timeout = null)
        {
            if (string.IsNullOrWhiteSpace(model))
                throw new InvalidOperationException("No model selected.");

            var request = BuildGenerateBody(model, prompt, system);
            var body = await SendAsync(HttpMethod.Post, baseUrl, "/api/generate", request, timeout);

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            return new Turn
            {
                Response = root.TryGetProperty("response", out var r) ? r.GetString() : "",
                TotalNs = root.TryGetProperty("total_duration", out var td) &&
                          td.ValueKind == JsonValueKind.Number ? td.GetInt64() : (long?)null,
                EvalCount = root.TryGetProperty("eval_count", out var ec) &&
                            ec.ValueKind == JsonValueKind.Number ? ec.GetInt32() : (int?)null,
                RawJson = body
            };
        }

        private static async Task<string> SendAsync(
            HttpMethod method, string baseUrl, string path, string jsonBody, TimeSpan? timeout)
        {
            var uri = NormalizeBase(baseUrl) + path;

            using var cts = new CancellationTokenSource(timeout ?? DefaultTimeout);
            using var request = new HttpRequestMessage(method, uri);
            if (jsonBody != null)
                request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            HttpResponseMessage resp;
            try
            {
                resp = await Http.SendAsync(request, cts.Token);
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                throw new TimeoutException("Ollama did not answer within the timeout.");
            }

            using (resp)
            {
                var body = await resp.Content.ReadAsStringAsync();
                if (!resp.IsSuccessStatusCode)
                    throw new InvalidOperationException(
                        $"Ollama returned {(int)resp.StatusCode}: {body}");
                return body;
            }
        }

        // Hand-written so the CLI stays trim-safe (no reflection-based JSON).
        private static string BuildGenerateBody(string model, string prompt, string system)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WriteString("model", model);
                writer.WriteString("prompt", prompt ?? "");
                if (!string.IsNullOrWhiteSpace(system)) writer.WriteString("system", system);
                writer.WriteBoolean("stream", false);
                writer.WriteEndObject();
            }
            return Encoding.UTF8.GetString(stream.ToArray());
        }
    }
}

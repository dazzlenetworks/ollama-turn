using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace OllamaTurn.Core
{
    // The settings both front ends work from: which server, which model.
    //
    // There is exactly one config.json, and it sits next to the executables.
    // The window writes it, the CLI reads it, and it can be edited by hand.
    // Nothing is copied anywhere else: a second copy could only ever go stale
    // and start disagreeing with this one.
    //
    // Everything here uses Utf8JsonWriter and JsonDocument rather than
    // reflection-based serialization, so the CLI can be published trimmed.
    public sealed class TurnConfig
    {
        public const string DefaultUrl = "http://localhost:11434/";

        public string Url { get; private set; } = DefaultUrl;
        public string Model { get; private set; } = "";

        // Where these values came from, for -ShowConfig and for diagnosing
        // "why is it using that model?".
        public string Source { get; private set; } = "(default)";

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        // Set this to point either front end at a specific config file.
        public const string PathVariable = "OLLAMA_TURN_CONFIG";

        // Reads a config file. Returns null when it is missing, unreadable, or
        // has no usable URL, so callers can simply try the next candidate.
        public static TurnConfig FromFile(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;

                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                var url = StringOr(doc.RootElement, "url", null);
                if (string.IsNullOrWhiteSpace(url)) return null;

                return new TurnConfig
                {
                    Url = url,
                    Model = StringOr(doc.RootElement, "model", ""),
                    Source = path
                };
            }
            catch
            {
                // Corrupt or locked file: treat it as absent rather than fatal.
                return null;
            }
        }

        // First usable candidate wins:
        //   1. OLLAMA_TURN_CONFIG, for a caller that wants to pin one file
        //   2. config.json beside the executable — the normal case, since the
        //      app and the CLI ship in the same folder
        //   3. the built-in default
        public static TurnConfig Discover(string exeDirectory)
        {
            var candidates = new[]
            {
                Environment.GetEnvironmentVariable(PathVariable),
                string.IsNullOrEmpty(exeDirectory) ? null : Path.Combine(exeDirectory, "config.json")
            };

            foreach (var candidate in candidates)
            {
                var found = FromFile(candidate);
                if (found != null) return found;
            }

            return new TurnConfig();
        }

        public static void Write(string path, string url, string model)
        {
            File.WriteAllText(path, Serialize(url, model), Utf8NoBom);
        }

        private static string Serialize(string url, string model)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();
                writer.WriteString("url", url ?? "");
                writer.WriteString("model", model ?? "");
                writer.WriteEndObject();
            }
            return Utf8NoBom.GetString(stream.ToArray()) + Environment.NewLine;
        }

        private static string StringOr(JsonElement root, string name, string fallback)
        {
            if (root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String)
            {
                var val = v.GetString();
                if (!string.IsNullOrWhiteSpace(val)) return val;
            }
            return fallback;
        }
    }
}

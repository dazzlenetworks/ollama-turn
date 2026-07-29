using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace OllamaTurn.Core
{
    // The settings both front ends work from: which server, which model.
    //
    // The window owns the config file next to its exe. The CLI only reads, and
    // finds its settings through Discover() so a caller needs to know nothing
    // about where anything is installed.
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

        // The fixed per-user path the app publishes to. This is what makes the
        // CLI zero-config: one location, regardless of install.
        public static string MirrorPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OllamaTurn", "config.json");

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
        //   1. OLLAMA_TURN_CONFIG, for callers that want to pin one explicitly
        //   2. the published mirror — the live settings of whichever app ran last
        //   3. a config.json sitting beside the caller (a dist\ install)
        //   4. the built-in default
        public static TurnConfig Discover(string exeDirectory)
        {
            var candidates = new[]
            {
                Environment.GetEnvironmentVariable("OLLAMA_TURN_CONFIG"),
                MirrorPath,
                string.IsNullOrEmpty(exeDirectory) ? null : Path.Combine(exeDirectory, "config.json")
            };

            foreach (var candidate in candidates)
            {
                var found = FromFile(candidate);
                if (found != null) return found;
            }

            return new TurnConfig();
        }

        // Writes the app's own config file, then republishes the mirror so
        // outside callers see the change immediately.
        public static void Write(string path, string url, string model, string source)
        {
            File.WriteAllText(path, Serialize(url, model, null), Utf8NoBom);
            Publish(url, model, source);
        }

        // The mirror is a one-directional publication: the app never reads it
        // back, and failing to write it leaves the app working normally.
        public static void Publish(string url, string model, string source)
        {
            try
            {
                var path = MirrorPath;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, Serialize(url, model, source), Utf8NoBom);
            }
            catch
            {
                // Only costs an outside caller its shortcut.
            }
        }

        private static string Serialize(string url, string model, string source)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();
                writer.WriteString("url", url ?? "");
                writer.WriteString("model", model ?? "");
                if (source != null) writer.WriteString("source", source);
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

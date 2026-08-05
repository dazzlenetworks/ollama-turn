using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using OllamaTurn.Core;

namespace OllamaTurn.Cli
{
    // OllamaTurnCLI.exe — one turn against the server and model the app is set
    // to, for agents and scripts.
    //
    // The contract with a calling program: stdout is the answer and nothing
    // else, diagnostics go to stderr, and the exit code is 0 or 1. No banner,
    // no prompts, no colour.
    internal static class Program
    {
        private const string Usage = @"OllamaTurnCLI - run one Ollama turn from the command line.

  OllamaTurnCLI ""why is the sky blue?""
  type notes.md | OllamaTurnCLI

Uses the server and model that Ollama Turn is set to, so there is nothing to
configure. Options:

  -m, --model <name>    Use this model for this turn only
      --url <url>       Use this server for this turn only
  -s, --system <text>   Prepend a system prompt
  -t, --timeout <sec>   Give up after this long (default 600)
      --json            Print the raw Ollama response object
      --list-models     List the models installed on the server
      --show-config     Print the resolved server, model, and their source
      --set-model <n>   Save this model as the configured one, then exit
      --set-url <url>   Save this server as the configured one, then exit
  -h, --help            Show this help

Exit code is 0 on success, 1 on failure. Errors go to stderr.";

        private static int Main(string[] args)
        {
            try
            {
                // Answers can contain anything; don't let the console mangle it.
                Console.OutputEncoding = new UTF8Encoding(false);
            }
            catch
            {
                // Some redirected consoles refuse this; the run is still fine.
            }

            try
            {
                return RunAsync(args).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                return Fail(ex.Message);
            }
        }

        private static async Task<int> RunAsync(string[] args)
        {
            var opts = Options.Parse(args, out var parseError);
            if (parseError != null) return Fail(parseError);

            if (opts.Help)
            {
                Console.Out.WriteLine(Usage);
                return 0;
            }

            var config = TurnConfig.Discover(AppContext.BaseDirectory);
            var baseUrl = opts.Url ?? config.Url;
            var timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);

            if (opts.SetUrl != null || opts.SetModel != null)
                return await SaveSettingsAsync(opts, config, timeout);

            if (opts.ShowConfig)
            {
                ShowConfig(OllamaApi.NormalizeBase(baseUrl), opts.Model ?? config.Model, config.Source);
                return 0;
            }

            if (opts.ListModels)
            {
                foreach (var name in await OllamaApi.ListModelsAsync(baseUrl, timeout))
                    Console.Out.WriteLine(name);
                return 0;
            }

            // Resolved before the model, so a missing prompt fails locally and
            // instantly instead of after a round trip to the server.
            var prompt = ResolvePrompt(opts.PromptWords);
            if (string.IsNullOrWhiteSpace(prompt))
                return Fail("no prompt. Pass it as an argument or pipe it on stdin.");

            var model = opts.Model ?? config.Model;
            if (string.IsNullOrWhiteSpace(model))
            {
                model = await SoleInstalledModelAsync(baseUrl, timeout);
                if (model == null) return 1;
            }

            var turn = await OllamaApi.GenerateAsync(baseUrl, model, prompt, opts.System, timeout);

            Console.Out.WriteLine(opts.Json
                ? (turn.RawJson ?? "").Trim()
                : (turn.Response ?? "").Trim());
            return 0;
        }

        // --set-url / --set-model: change the configured settings, so an agent
        // can pick a model without anyone opening the app.
        //
        // A URL may legitimately be set before that server is up, so it is
        // written and merely warned about if unreachable. A model, by contrast,
        // is only meaningful if the server actually has it, so it is validated
        // first and nothing is written when it isn't there - a typo would
        // otherwise sit in the config and break every later run.
        private static async Task<int> SaveSettingsAsync(
            Options opts, TurnConfig config, TimeSpan timeout)
        {
            var url = opts.SetUrl ?? config.Url;
            var model = config.Model;

            if (opts.SetModel != null)
            {
                var installed = await OllamaApi.ListModelsAsync(url, timeout);
                if (!installed.Contains(opts.SetModel))
                    return Fail("no model named " + opts.SetModel + " on " +
                                OllamaApi.NormalizeBase(url) + ". Available: " +
                                string.Join(", ", installed));
                model = opts.SetModel;
            }

            // Write back to the file we read, or create one beside this exe.
            var target = File.Exists(config.Source)
                ? config.Source
                : Path.Combine(AppContext.BaseDirectory, "config.json");

            TurnConfig.Write(target, url, model);
            ShowConfig(OllamaApi.NormalizeBase(url), model, target);

            if (opts.SetUrl != null && opts.SetModel == null)
            {
                try
                {
                    await OllamaApi.ListModelsAsync(url, timeout);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(
                        "OllamaTurnCLI: saved, but that server did not answer: " + ex.Message);
                }
            }

            return 0;
        }

        private static void ShowConfig(string url, string model, string source)
        {
            Console.Out.WriteLine("url:    " + url);
            Console.Out.WriteLine("model:  " + model);
            Console.Out.WriteLine("source: " + source);
        }

        // Nothing picked in the app yet: one installed model is unambiguous,
        // anything else is a decision the CLI should not make silently.
        private static async Task<string> SoleInstalledModelAsync(string baseUrl, TimeSpan timeout)
        {
            var names = await OllamaApi.ListModelsAsync(baseUrl, timeout);
            if (names.Count == 1) return names[0];

            if (names.Count == 0)
            {
                Fail("no models installed on " + OllamaApi.NormalizeBase(baseUrl) + ".");
                return null;
            }

            Fail("no model selected. Pick one in Ollama Turn, or pass --model. Available: " +
                 string.Join(", ", names));
            return null;
        }

        private static string ResolvePrompt(List<string> words)
        {
            if (words.Count > 0) return string.Join(" ", words);
            if (!Console.IsInputRedirected) return "";

            // A piped file often starts with a UTF-8 byte order mark, and
            // .NET's own redirected stdin can emit one on its own. It is not
            // whitespace as far as string.IsNullOrWhiteSpace is concerned, so
            // without this an empty pipe reads as a prompt of one invisible
            // character - and a real piped file gets one prepended.
            return Console.In.ReadToEnd().TrimStart('\uFEFF');
        }

        private static int Fail(string message)
        {
            Console.Error.WriteLine("OllamaTurnCLI: " + message);
            return 1;
        }

        private sealed class Options
        {
            public List<string> PromptWords = new List<string>();
            public string Model;
            public string Url;
            public string SetModel;
            public string SetUrl;
            public string System;
            public int TimeoutSeconds = 600;
            public bool Json;
            public bool ListModels;
            public bool ShowConfig;
            public bool Help;

            public static Options Parse(string[] args, out string error)
            {
                var o = new Options();
                error = null;

                for (var i = 0; i < args.Length; i++)
                {
                    var a = args[i];
                    switch (a)
                    {
                        case "-h":
                        case "--help":
                        case "-?":
                            o.Help = true;
                            break;
                        case "--json":
                            o.Json = true;
                            break;
                        case "--list-models":
                            o.ListModels = true;
                            break;
                        case "--show-config":
                            o.ShowConfig = true;
                            break;
                        case "-m":
                        case "--model":
                            if (!Next(args, ref i, out o.Model)) { error = "--model needs a value."; return o; }
                            break;
                        case "--url":
                            if (!Next(args, ref i, out o.Url)) { error = "--url needs a value."; return o; }
                            break;
                        case "--set-model":
                            if (!Next(args, ref i, out o.SetModel)) { error = "--set-model needs a value."; return o; }
                            break;
                        case "--set-url":
                            if (!Next(args, ref i, out o.SetUrl)) { error = "--set-url needs a value."; return o; }
                            break;
                        case "-s":
                        case "--system":
                            if (!Next(args, ref i, out o.System)) { error = "--system needs a value."; return o; }
                            break;
                        case "-t":
                        case "--timeout":
                            if (!Next(args, ref i, out var secs)) { error = "--timeout needs a value."; return o; }
                            if (!int.TryParse(secs, out var parsed) || parsed <= 0)
                            {
                                error = "--timeout needs a positive number of seconds.";
                                return o;
                            }
                            o.TimeoutSeconds = parsed;
                            break;
                        default:
                            // Everything else is the prompt. An unknown switch is
                            // more likely a typo than a one-word question.
                            if (a.StartsWith("-") && a.Length > 1)
                            {
                                error = "unknown option " + a + ". Try --help.";
                                return o;
                            }
                            o.PromptWords.Add(a);
                            break;
                    }
                }

                return o;
            }

            private static bool Next(string[] args, ref int i, out string value)
            {
                if (i + 1 >= args.Length) { value = null; return false; }
                value = args[++i];
                return true;
            }
        }
    }
}

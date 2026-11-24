using AnimationEditor.Editing;

namespace AnimationEditor.Cli;

/// <summary>
/// The aecli commands. <see cref="RunAsync"/> returns the process exit code: 0 success, 1 the command ran and
/// found problems or failed (check errors, a failed edit, an unreadable file), 2 the command line or ops file is
/// malformed. Results go to <c>stdout</c> (text, or one JSON document with <c>--json</c>); an unreadable file goes to
/// <c>stderr</c> in text mode. Does no process-wide setup: that happens once in <c>Program.Main</c>.
/// </summary>
public static class CliApp
{
    private const string Usage = """
        usage: aecli <command> [options]
          describe <file> [--chain|--sequence|--track <glob>] [--summary] [--json]  chains, frames and shapes of an .achx/.achj; sequences, tracks and blocks of a .seqj
          check <file> [--json]                  validate; exit 1 on errors
          new <file> [--json]                    create an empty .achx/.achj/.seqj
          convert <file> --to achj|achx [--json] write the other format next to it; never overwrites, keeps the source
          edit <file> --ops <ops.json> [--json]  apply an ordered operations file; writes nothing unless all succeed
          edit <file> --op "<op> <address> key=value ..." [--op ...] [--json]   same, inline (see --schema); --ops - reads the file from stdin
          render <file> --out <dir> [options]    write PNGs of a sequence (--sequence <name> --at 0,2.2 [--pass <n>]) or a chain (--chain <name> --frame 0 | --at 0.3)
              --scale <1-16>  --background transparent|black|white|gray|#RRGGBB  --bounds auto|x,y,w,h  --grid  --shapes  --sheet  [--json]
          at <file.seqj> --time 0,4.2 [--sequence <name>] [--track <glob>] [--pass <n>] [--shapes-only] [--json]  per visible track at each time: active chain, frame, sampled offset and color, flips, the frame's events and its shapes (position, size)
          at <file.achx|.achj> --time 0,0.3 [--chain <name>] [--shapes-only] [--json]  the same for one chain (its loop, ping-pong and reverse apply). An event fires when its frame is entered; a time inside the frame lists it but does not mean it fires then
          --schema [--json]                      list the operations and their fields
          --version                              build version, source commit and build time (UTC)
        exit codes: 0 ok, 1 problems found or edit failed, 2 usage error
        """;

    public static async Task<int> RunAsync(string[] args, TextWriter stdout, TextWriter stderr, TextReader? stdin = null)
    {
        var cli = new Invocation(stdout, stderr, stdin ?? TextReader.Null) { Json = args.Contains("--json") };
        if (args.Length == 0) return cli.Usage("no command given.", showUsage: true);
        if (args[0] is "-h" or "--help") args = ["help", .. args.Skip(1)];

        var parsed = Arguments.Parse(args);
        if (parsed.Error is not null) return cli.Usage(parsed.Error);
        var command = parsed.Command!;
        cli.Json = parsed.Has("--json");

        if (command.Name == "help")
        {
            stdout.WriteLine(Usage);
            return 0;
        }
        if (command.Name == "--schema") return cli.Schema();
        if (command.Name == "--version")
        {
            stdout.WriteLine(BuildInfo.Describe());
            return 0;
        }

        if (parsed.File is not { } file) return cli.Usage($"{command.Name} needs a file.");
        if (command.WrongKind(file, parsed.Values.Keys) is { } wrongKind) return cli.Usage(wrongKind);

        switch (command.Name)
        {
            case "describe": return cli.Describe(file, parsed.Values);
            case "check": return cli.Check(file);
            case "new": return cli.New(file);
            case "edit":
                var opsFile = parsed.Value("--ops");
                if (opsFile is null && parsed.InlineOps.Count == 0) return cli.Usage("edit needs --ops <ops.json|-> or --op \"<op> <address> key=value ...\".");
                if (opsFile is not null && parsed.InlineOps.Count > 0) return cli.Usage("edit takes --ops or --op, not both.");
                return await cli.EditAsync(file, opsFile, parsed.InlineOps);
            case "convert":
                if (parsed.Value("--to") is not ("achj" or "achx")) return cli.Usage("convert needs --to achj or --to achx.");
                return cli.Convert(file, parsed.Value("--to")!);
            case "render": return cli.Render(file, parsed.Values);
            case "at": return cli.At(file, parsed.Values);
            default: throw new InvalidOperationException($"'{command.Name}' is in CliCommands but has no handler.");
        }
    }

    // Values holds every option given except --op, which repeats (InlineOps); a flag's value is "".
    private sealed record Arguments(CliCommand? Command, string? File, IReadOnlyDictionary<string, string> Values, IReadOnlyList<string> InlineOps, string? Error)
    {
        public bool Has(string option) => Values.ContainsKey(option);

        public string? Value(string option) => Values.GetValueOrDefault(option);

        public static Arguments Parse(string[] args)
        {
            string? name = null, file = null;
            var values = new Dictionary<string, string>();
            var inline = new List<string>();
            var given = new List<(string Option, string Value)>();
            for (int i = 0; i < args.Length; i++)
            {
                var a = args[i];
                if (!a.StartsWith("--", StringComparison.Ordinal) || (name is null && CliCommands.Find(a) is not null))
                {
                    if (name is null) name = a;
                    else if (file is null && CliCommands.Find(name) is not { TakesFile: false }) file = a;
                    else return Bad(Refused() ?? $"unexpected argument '{a}'.");
                    continue;
                }

                var option = CliCommands.FindOption(a);
                bool needsValue = option?.Needs is not null, missing = needsValue && i + 1 >= args.Length;
                string value = needsValue && !missing ? args[++i] : "";
                given.Add((a, value));
                if (option is null) return Bad(Refused() ?? $"unknown option '{a}'.");
                if (missing) return Bad(Refused() ?? option.Needs!);
                if (a == "--op") inline.Add(value);
                else values[a] = value;
            }

            if (name is null) return Bad("no command given.");
            if (Refused() is { } refused) return Bad(refused);
            return new Arguments(CliCommands.Find(name), file, values, inline, null);

            // The first error the command itself explains: there is no such command, an option it does not take, one of its
            // options taken as the value of another, or an option given twice.
            string? Refused()
            {
                if (name is null) return null;
                if (CliCommands.Find(name) is not { } command) return $"unknown command '{name}'.";
                foreach (var (option, _) in given)
                    if (command.Find(option) is null) return command.Refusal(option);
                foreach (var (option, value) in given)
                    if (command.Find(value) is not null) return $"{command.Find(option)!.Needs} {value} is an option, not its value.";
                var repeated = given.Where(g => g.Option != "--op").GroupBy(g => g.Option).FirstOrDefault(g => g.Count() > 1);
                return repeated is null ? null : $"{repeated.Key} is given twice.";
            }

            static Arguments Bad(string error) => new(null, null, new Dictionary<string, string>(), [], error);
        }
    }

    private sealed class Invocation(TextWriter stdout, TextWriter stderr, TextReader stdin)
    {
        public bool Json { get; set; }

        public int Usage(string message, bool showUsage = false)
        {
            if (Json)
            {
                stdout.WriteLine(EditJson.Render(w => EditJson.WriteResult(
                    w, new EditResult([], [message], [], [EditErrorCode.UsageError]))));
                return 2;
            }

            stderr.WriteLine($"aecli: {message}");
            stderr.WriteLine(showUsage ? CliApp.Usage : "Run 'aecli help' for usage.");
            return 2;
        }

        public int Schema()
        {
            stdout.WriteLine(Json ? EditJson.Render(EditJson.WriteSchema) : EditJson.SchemaText());
            return 0;
        }

        public int Describe(string file, IReadOnlyDictionary<string, string> values)
        {
            string? chain = values.GetValueOrDefault("--chain"), sequence = values.GetValueOrDefault("--sequence"), track = values.GetValueOrDefault("--track");
            bool summary = values.ContainsKey("--summary");

            if (SequenceFileReader.IsSequenceFile(file))
            {
                var seq = SequenceFileReader.Read(file);
                if (seq.Document is null) return Failed(seq.Error!);

                IReadOnlyCollection<string>? sequences = null;
                if (sequence is not null)
                {
                    var expanded = SequenceSelectors.ExpandSequences(seq.Document, sequence);
                    if (!expanded.Ok)
                        return Failed(expanded.Error! + (expanded.Code == EditErrorCode.NoMatch ? NameHints.For(sequence, seq.Document.Sequences.Select(s => s.Name), "sequences") : ""));
                    sequences = expanded.Targets!.ToList();
                }
                var filter = new SequenceDescriber.Filter(sequences, track, summary);
                if (SequenceDescriber.CheckTrackFilter(seq.Document, filter) is { } trackError) return Failed(trackError);

                stdout.Write(Json
                    ? EditJson.Render(w => SequenceDescriber.WriteJson(w, seq.Document, file, filter)) + Environment.NewLine
                    : SequenceDescriber.ToText(seq.Document, file, filter));
                return 0;
            }

            var read = AnimationFileReader.Read(file);
            if (read.Document is null) return Failed(read.Error!);

            IReadOnlyCollection<string>? only = null;
            if (chain is not null)
            {
                var expanded = EditSelectors.ExpandChains(read.Document, chain);
                if (!expanded.Ok)
                    return Failed(expanded.Error! + (expanded.Code == EditErrorCode.NoMatch ? NameHints.For(chain, read.Document.AnimationChains.Select(c => c.Name), "chains") : ""));
                only = expanded.Targets!.ToList();
            }

            stdout.Write(Json
                ? EditJson.Render(w => AnimationDescriber.WriteJson(w, read.Document, file, read.OwnerTiles, only, summary)) + Environment.NewLine
                : AnimationDescriber.ToText(read.Document, file, read.OwnerTiles, only, summary));
            return 0;
        }

        public int Check(string file)
        {
            CheckReport report;
            if (SequenceFileReader.IsSequenceFile(file))
            {
                var seq = SequenceFileReader.Read(file);
                if (seq.Document is null) return Failed(seq.Error!);
                report = SequenceCheck.Run(seq.Document, file);
            }
            else
            {
                var read = AnimationFileReader.Read(file);
                if (read.Document is null) return Failed(read.Error!);
                report = CheckReport.Run(read.Document, file);
                if (read.Issues is { Count: > 0 })
                    report = new CheckReport([.. report.Issues, .. read.Issues]);
            }
            if (Json)
            {
                stdout.WriteLine(EditJson.Render(w => EditJson.WriteCheck(w, report)));
            }
            else
            {
                foreach (var issue in report.Issues)
                    stdout.WriteLine($"{(issue.Severity == CheckSeverity.Error ? "error" : "warning")}[{issue.Code}]: {issue.Message}");
                stdout.WriteLine(report.Ok
                    ? $"ok: {Path.GetFileName(file)} has no errors ({report.Warnings.Count()} warning(s))."
                    : $"{report.Errors.Count()} error(s), {report.Warnings.Count()} warning(s) in {Path.GetFileName(file)}.");
            }
            return report.Ok ? 0 : 1;
        }

        public int New(string file)
        {
            var result = SequenceFileReader.IsSequenceFile(file)
                ? NewFileEdit.CreateSequenceFile(file)
                : NewFileEdit.CreateAnimationFile(file);
            return Report(result, null, $"Created {file}" + (result.Sequence is null ? "" : $" with sequence '{result.Sequence}'"));
        }

        public int Render(string file, IReadOnlyDictionary<string, string> values)
        {
            var outcome = RenderCommand.Run(file, values);
            if (outcome.UsageError is not null) return Usage(outcome.UsageError);
            if (Json)
            {
                stdout.WriteLine(EditJson.Render(w =>
                {
                    w.WriteStartObject();
                    w.WriteBoolean("success", outcome.Success);
                    w.WriteStartArray("files");
                    foreach (var f in outcome.Files) w.WriteStringValue(f);
                    w.WriteEndArray();
                    w.WriteStartArray("errors");
                    foreach (var (code, message) in outcome.Errors)
                    {
                        w.WriteStartObject();
                        w.WriteString("code", code);
                        w.WriteString("message", message);
                        w.WriteEndObject();
                    }
                    w.WriteEndArray();
                    w.WriteEndObject();
                }));
            }
            else
            {
                foreach (var f in outcome.Files) stdout.WriteLine(f);
                foreach (var (_, message) in outcome.Errors) stderr.WriteLine($"aecli: {message}");
            }
            return outcome.Success ? 0 : 1;
        }

        public int At(string file, IReadOnlyDictionary<string, string> values)
        {
            bool isSequence = SequenceFileReader.IsSequenceFile(file);
            if (!isSequence && Path.GetExtension(file).ToLowerInvariant() is not (".achx" or ".achj"))
                return Usage("at needs a .seqj, .achx or .achj file.");
            if (!values.TryGetValue("--time", out var timeText)) return Usage("at needs --time <seconds>[,<seconds>...].");
            var times = new List<double>();
            foreach (var part in timeText.Split(',', StringSplitOptions.TrimEntries))
            {
                if (!double.TryParse(part, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var t) || t < 0 || !double.IsFinite(t))
                    return Usage("--time must be a comma list of times in seconds, 0 or more (0,2.15,4.2).");
                times.Add(t);
            }
            values.TryGetValue("--sequence", out var sequence);
            values.TryGetValue("--track", out var track);
            values.TryGetValue("--chain", out var chain);
            bool shapesOnly = values.ContainsKey("--shapes-only");
            if (!CliCommands.TryPass(values, out int pass, out var error)) return Usage(error!);

            bool usage;
            var report = isSequence
                ? AtReport.BuildSequence(file, sequence, times, pass, track, shapesOnly, out error, out usage)
                : AtReport.BuildChain(file, chain, times, shapesOnly, out error, out usage);
            if (report is null) return usage ? Usage(error!) : Failed(error!);
            stdout.Write(Json ? EditJson.Render(w => AtReport.WriteJson(w, report)) + Environment.NewLine : AtReport.ToText(report));
            return 0;
        }

        public int Convert(string file, string to)
        {
            var result = FileConvertEdit.Convert(file, to);
            return Report(result, null, $"Created {result.ChangedItems.FirstOrDefault()}");
        }

        public async Task<int> EditAsync(string file, string? opsFile, IReadOnlyList<string> inlineOps)
        {
            bool sequence = SequenceFileReader.IsSequenceFile(file);
            var specs = sequence ? SequenceOps.Specs : EditOps.Specs;

            OpsParse parsed;
            if (inlineOps.Count > 0)
                parsed = InlineOps.Parse(inlineOps, specs);
            else
            {
                string text;
                try { text = opsFile == "-" ? stdin.ReadToEnd() : File.ReadAllText(opsFile!); }
                catch (Exception ex) { return Usage($"cannot read the ops {(opsFile == "-" ? "from stdin" : $"file {opsFile}")}: {ex.Message}"); }
                parsed = EditOps.Parse(text, specs);
            }
            if (parsed.Ops is null) return Usage(parsed.Error!);

            if (sequence)
                return EditSequence(file, parsed.Ops);

            var open = await EditSession.OpenAsync(file);
            if (open.Session is null) return Report(open.Result, null, null);

            var run = await EditOps.ApplyAsync(open.Session, parsed.Ops);
            var applied = run.Result.ChangedItems.Count == 0
                ? $"No changes; {Path.GetFileName(file)} left as it was."
                : $"Applied {parsed.Ops.Count} op(s) and saved {Path.GetFileName(file)}.";
            return Report(run.Result, run.ErrorOps, applied);
        }

        private int EditSequence(string file, IReadOnlyList<EditOp> ops)
        {
            var open = SequenceSession.Open(file);
            if (open.Session is null) return Report(open.Result, null, null);

            var run = SequenceOps.Apply(open.Session, ops);
            var warnings = open.Result.Warnings.Concat(run.Result.Warnings).ToList();
            var result = run.Result with { Warnings = warnings };
            var applied = result.ChangedItems.Count == 0
                ? $"No changes; {Path.GetFileName(file)} left as it was."
                : $"Applied {ops.Count} op(s) and saved {Path.GetFileName(file)}.";
            return Report(result, run.ErrorOps, applied);
        }

        private int Report(EditResult result, IReadOnlyList<int?>? errorOps, string? successText)
        {
            if (Json)
            {
                stdout.WriteLine(EditJson.Render(w => EditJson.WriteResult(w, result, errorOps)));
            }
            else
            {
                foreach (var error in result.Errors) stdout.WriteLine($"error: {error}");
                foreach (var warning in result.Warnings) stdout.WriteLine($"warning: {warning}");
                if (result.Success && successText is not null) stdout.WriteLine(successText);
            }
            return result.Success ? 0 : 1;
        }

        private int Failed(string message)
        {
            if (Json)
                stdout.WriteLine(EditJson.Render(w => EditJson.WriteResult(w, new EditResult([], [message], []))));
            else
                stderr.WriteLine($"aecli: {message}");
            return 1;
        }
    }
}

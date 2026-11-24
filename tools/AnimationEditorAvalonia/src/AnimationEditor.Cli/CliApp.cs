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
          describe <file> [--json]               chains, frames and shapes of an .achx/.achj; sequences, tracks and blocks of a .seqj
          check <file> [--json]                  validate; exit 1 on errors
          new <file> [--json]                    create an empty .achx/.achj/.seqj
          convert <file> --to achj|achx [--json] write the other format next to it; never overwrites, keeps the source
          edit <file> --ops <ops.json> [--json]  apply an ordered operations file; writes nothing unless all succeed
          render <file> --out <dir> [options]    write PNGs of a sequence (--sequence <name> --at 0,2.2) or a chain (--chain <name> --frame 0 | --at 0.3)
              --scale <1-16>  --background transparent|black|white|gray|#RRGGBB  --bounds auto|x,y,w,h  --grid  --shapes  --sheet  [--json]
          --schema [--json]                      list the operations and their fields
        exit codes: 0 ok, 1 problems found or edit failed, 2 usage error
        """;

    public static async Task<int> RunAsync(string[] args, TextWriter stdout, TextWriter stderr)
    {
        var cli = new Invocation(stdout, stderr) { Json = args.Contains("--json") };
        if (args.Length == 0) return cli.Usage("no command given.", showUsage: true);
        if (args[0] is "-h" or "--help" or "help")
        {
            stdout.WriteLine(Usage);
            return 0;
        }

        var parsed = Arguments.Parse(args);
        if (parsed.Error is not null) return cli.Usage(parsed.Error);
        cli.Json = parsed.Json;

        if (parsed.Schema) return cli.Schema();

        switch (parsed.Command)
        {
            case "describe": return parsed.File is null ? cli.Usage("describe needs a file.") : cli.Describe(parsed.File);
            case "check": return parsed.File is null ? cli.Usage("check needs a file.") : cli.Check(parsed.File);
            case "new": return parsed.File is null ? cli.Usage("new needs a file.") : cli.New(parsed.File);
            case "edit":
                if (parsed.File is null) return cli.Usage("edit needs a file.");
                if (parsed.OpsFile is null) return cli.Usage("edit needs --ops <ops.json>.");
                return await cli.EditAsync(parsed.File, parsed.OpsFile);
            case "convert":
                if (parsed.File is null) return cli.Usage("convert needs a file.");
                if (parsed.To is not ("achj" or "achx")) return cli.Usage("convert needs --to achj or --to achx.");
                return cli.Convert(parsed.File, parsed.To);
            case "render":
                if (parsed.File is null) return cli.Usage("render needs a file.");
                return cli.Render(parsed.File, parsed.Values, parsed.Grid);
            default:
                return cli.Usage($"unknown command '{parsed.Command}'.");
        }
    }

    private sealed record Arguments(string? Command, string? File, string? OpsFile, string? To, bool Json, bool Schema, string? Error,
        IReadOnlyDictionary<string, string>? Values = null, bool Grid = false)
    {
        public static Arguments Parse(string[] args)
        {
            string? command = null, file = null, ops = null, to = null;
            bool json = false, schema = false, grid = false;
            var values = new Dictionary<string, string>();
            for (int i = 0; i < args.Length; i++)
            {
                var a = args[i];
                switch (a)
                {
                    case "--json": json = true; break;
                    case "--schema": schema = true; break;
                    case "--grid": grid = true; break;
                    case "--shapes" or "--sheet": values[a] = ""; break;
                    case "--out" or "--sequence" or "--chain" or "--at" or "--frame" or "--scale" or "--background" or "--bounds":
                        if (i + 1 >= args.Length) return Bad($"{a} needs a value.");
                        values[a] = args[++i];
                        break;
                    case "--ops":
                        if (i + 1 >= args.Length) return Bad("--ops needs a file.");
                        ops = args[++i];
                        break;
                    case "--to":
                        if (i + 1 >= args.Length) return Bad("--to needs achj or achx.");
                        to = args[++i];
                        break;
                    default:
                        if (a.StartsWith("--")) return Bad($"unknown option '{a}'.");
                        if (command is null) command = a;
                        else if (file is null) file = a;
                        else return Bad($"unexpected argument '{a}'.");
                        break;
                }
            }
            return new Arguments(command, file, ops, to, json, schema, null, values, grid);

            static Arguments Bad(string error) => new(null, null, null, null, false, false, error);
        }
    }

    private sealed class Invocation(TextWriter stdout, TextWriter stderr)
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

        public int Describe(string file)
        {
            if (SequenceFileReader.IsSequenceFile(file))
            {
                var seq = SequenceFileReader.Read(file);
                if (seq.Document is null) return Failed(seq.Error!);
                stdout.Write(Json
                    ? EditJson.Render(w => SequenceDescriber.WriteJson(w, seq.Document, file)) + Environment.NewLine
                    : SequenceDescriber.ToText(seq.Document, file));
                return 0;
            }

            var read = AnimationFileReader.Read(file);
            if (read.Document is null) return Failed(read.Error!);

            stdout.Write(Json
                ? EditJson.Render(w => AnimationDescriber.WriteJson(w, read.Document, file, read.OwnerTiles)) + Environment.NewLine
                : AnimationDescriber.ToText(read.Document, file, read.OwnerTiles));
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
            return Report(result, null, $"Created {file}");
        }

        public int Render(string file, IReadOnlyDictionary<string, string>? values, bool grid)
        {
            var outcome = RenderCommand.Run(file, values ?? new Dictionary<string, string>(), grid);
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

        public int Convert(string file, string to)
        {
            var result = FileConvertEdit.Convert(file, to);
            return Report(result, null, $"Created {result.ChangedItems.FirstOrDefault()}");
        }

        public async Task<int> EditAsync(string file, string opsFile)
        {
            string text;
            try { text = File.ReadAllText(opsFile); }
            catch (Exception ex) { return Usage($"cannot read the ops file {opsFile}: {ex.Message}"); }

            if (SequenceFileReader.IsSequenceFile(file))
                return EditSequence(file, text);

            var parsed = EditOps.Parse(text);
            if (parsed.Ops is null) return Usage(parsed.Error!);

            var open = await EditSession.OpenAsync(file);
            if (open.Session is null) return Report(open.Result, null, null);

            var run = await EditOps.ApplyAsync(open.Session, parsed.Ops);
            var applied = run.Result.ChangedItems.Count == 0
                ? $"No changes; {Path.GetFileName(file)} left as it was."
                : $"Applied {parsed.Ops.Count} op(s) and saved {Path.GetFileName(file)}.";
            return Report(run.Result, run.ErrorOps, applied);
        }

        private int EditSequence(string file, string opsText)
        {
            var parsed = SequenceOps.Parse(opsText);
            if (parsed.Ops is null) return Usage(parsed.Error!);

            var open = SequenceSession.Open(file);
            if (open.Session is null) return Report(open.Result, null, null);

            var run = SequenceOps.Apply(open.Session, parsed.Ops);
            var warnings = open.Result.Warnings.Concat(run.Result.Warnings).ToList();
            var result = run.Result with { Warnings = warnings };
            var applied = result.ChangedItems.Count == 0
                ? $"No changes; {Path.GetFileName(file)} left as it was."
                : $"Applied {parsed.Ops.Count} op(s) and saved {Path.GetFileName(file)}.";
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

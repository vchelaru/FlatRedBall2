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
          describe <file> [--json]               chains, frames (by index) and shapes of an .achx/.achj
          check <file> [--json]                  validate; exit 1 on errors
          new <file> [--json]                    create an empty .achx/.achj
          convert <file> --to achj|achx [--json] write the other format next to it; never overwrites, keeps the source
          edit <file> --ops <ops.json> [--json]  apply an ordered operations file; writes nothing unless all succeed
          --schema [--json]                      list the operations and their fields
        exit codes: 0 ok, 1 problems found or edit failed, 2 usage error
        """;

    public static async Task<int> RunAsync(string[] args, TextWriter stdout, TextWriter stderr)
    {
        var cli = new Invocation(stdout, stderr);
        if (args.Length == 0) return cli.Usage("no command given.");
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
            default:
                return cli.Usage($"unknown command '{parsed.Command}'.");
        }
    }

    private sealed record Arguments(string? Command, string? File, string? OpsFile, string? To, bool Json, bool Schema, string? Error)
    {
        public static Arguments Parse(string[] args)
        {
            string? command = null, file = null, ops = null, to = null;
            bool json = false, schema = false;
            for (int i = 0; i < args.Length; i++)
            {
                var a = args[i];
                switch (a)
                {
                    case "--json": json = true; break;
                    case "--schema": schema = true; break;
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
            return new Arguments(command, file, ops, to, json, schema, null);

            static Arguments Bad(string error) => new(null, null, null, null, false, false, error);
        }
    }

    private sealed class Invocation(TextWriter stdout, TextWriter stderr)
    {
        public bool Json { get; set; }

        public int Usage(string message)
        {
            stderr.WriteLine($"aecli: {message}");
            stderr.WriteLine(CliApp.Usage);
            return 2;
        }

        public int Schema()
        {
            stdout.WriteLine(Json ? EditJson.Render(EditJson.WriteSchema) : EditJson.SchemaText());
            return 0;
        }

        public int Describe(string file)
        {
            var read = AnimationFileReader.Read(file);
            if (read.Document is null) return Failed(read.Error!);

            stdout.Write(Json
                ? EditJson.Render(w => AnimationDescriber.WriteJson(w, read.Document, file)) + Environment.NewLine
                : AnimationDescriber.ToText(read.Document, file));
            return 0;
        }

        public int Check(string file)
        {
            var read = AnimationFileReader.Read(file);
            if (read.Document is null) return Failed(read.Error!);

            var report = CheckReport.Run(read.Document, file);
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
            var result = NewFileEdit.CreateAnimationFile(file);
            return Report(result, null, $"Created {file}");
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

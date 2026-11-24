using System.Text.Json;
using System.Text.RegularExpressions;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Cli.Tests;

/// <summary>An option the command does not take is exit 2 and lists the ones it does; nothing is silently ignored.</summary>
public class OptionValidationTests : IDisposable
{
    // What each command takes. A new option is added here too, so every other command is checked to refuse it.
    private static readonly Dictionary<string, string[]> Takes = new()
    {
        ["describe"] = ["--chain", "--sequence", "--track", "--summary", "--json"],
        ["check"] = ["--json"],
        ["new"] = ["--json"],
        ["convert"] = ["--to", "--json"],
        ["edit"] = ["--ops", "--op", "--json"],
        ["render"] = ["--out", "--sequence", "--chain", "--at", "--frame", "--pass", "--scale", "--background", "--bounds", "--grid", "--shapes", "--sheet", "--json"],
        ["at"] = ["--time", "--sequence", "--track", "--chain", "--pass", "--shapes-only", "--json"],
        ["--schema"] = ["--json"],
        ["--version"] = [],
        ["help"] = [],
    };

    private static readonly string[] Flags = ["--json", "--summary", "--grid", "--shapes", "--sheet", "--shapes-only", "--schema", "--version", "--bogus"];

    private readonly CliWorkspace _w = new("hero.achx", "hero.achj", "sheet.png");

    public void Dispose() => _w.Dispose();

    private string WriteSequence()
    {
        var save = new SequenceListSave { Sources = { "hero.achj" } };
        save.Sequences.Add(new SequenceSave
        {
            Name = "Run",
            Tracks = { new SequenceTrackSave { Name = "Body", Chains = { new SequenceChainReferenceSave { Source = "hero.achj", Chain = "Walk" } } } },
        });
        var path = _w.PathOf("hero.seqj");
        save.Save(path);
        return path;
    }

    // A command line that works, so the added option is the only thing wrong with it.
    private string[] Working(string command) => command switch
    {
        "--schema" or "--version" or "help" => [command],
        "new" => ["new", _w.PathOf("fresh.achx")],
        "convert" => ["convert", _w.PathOf("only.achx"), "--to", "achj"],
        "edit" => ["edit", _w.PathOf("hero.achx"), "--op", "set-chain-loop Walk value=false"],
        "render" => ["render", _w.PathOf("hero.achx"), "--chain", "Walk", "--out", _w.PathOf("out")],
        "at" => ["at", _w.PathOf("hero.achx"), "--chain", "Walk", "--time", "0"],
        _ => [command, _w.PathOf("hero.achx")],
    };

    public static TheoryData<string> Commands() => [.. Takes.Keys];

    [Theory]
    [MemberData(nameof(Commands))]
    public async Task OptionTheCommandDoesNotTake_IsUsageErrorListingTheOnesItDoes(string command)
    {
        File.Copy(_w.PathOf("hero.achx"), _w.PathOf("only.achx"));
        var before = Directory.GetFiles(_w.Dir).Order().ToArray();
        var bytes = _w.Bytes("hero.achx");
        var refused = Takes.Values.SelectMany(o => o).Concat(["--schema", "--version", "--bogus"]).Distinct().Except([.. Takes[command], command]).ToList();

        foreach (var option in refused)
        {
            string[] args = Flags.Contains(option) ? [.. Working(command), option] : [.. Working(command), option, "1"];

            var r = await _w.RunAsync(args);

            r.Code.ShouldBe(2, string.Join(" ", args) + "\n" + r.Out + r.Err);
            // --json counts even where it is refused, so that error is JSON.
            var message = option == "--json" ? r.Out : r.Err;
            message.ShouldContain($"{command} does not take {option}");
            var listed = Regex.Matches(message.Split("it takes")[1], "--[a-z-]+").Select(m => m.Value);
            listed.ShouldBe(Takes[command], ignoreOrder: true);
        }
        Directory.GetFiles(_w.Dir).Order().ShouldBe(before);
        Directory.Exists(_w.PathOf("out")).ShouldBeFalse();
        _w.Bytes("hero.achx").ShouldBe(bytes);
    }

    [Fact]
    public async Task Help_ListsEveryOptionOfEveryCommand()
    {
        var help = (await _w.RunAsync("help")).Out;

        foreach (var command in Takes.Keys.Where(c => c != "help"))
        {
            // A command's lines: each line starting with it, plus the deeper-indented lines that continue it.
            var block = string.Join("\n", Regex.Matches(help.ReplaceLineEndings("\n"), $@"^  {command} .*(\n      .*)*", RegexOptions.Multiline).Select(m => m.Value));
            foreach (var option in Takes[command])
                block.ShouldContain(option.TrimStart('-'), Case.Sensitive, $"{command} {option}");
        }
    }

    [Theory]
    [InlineData("check")]
    [InlineData("convert")]
    [InlineData("--schema")]
    public async Task RefusedOption_WithJson_IsJson_WhicheverSideOfItJsonIs(string command)
    {
        foreach (var args in new string[][] { [.. Working(command), "--grid", "--json"], [.. Working(command), "--json", "--grid"] })
        {
            var r = await _w.RunAsync(args);

            r.Code.ShouldBe(2);
            r.Err.ShouldBeEmpty();
            using var d = JsonDocument.Parse(r.Out);
            var e = d.RootElement.GetProperty("errors")[0];
            e.GetProperty("code").GetString().ShouldBe("UsageError");
            e.GetProperty("message").GetString()!.ShouldContain("does not take --grid");
        }
    }

    [Fact]
    public async Task RefusedOption_BeforeTheCommand_OrAsTheLastArgument_IsStillNamed()
    {
        var first = await _w.RunAsync("--track", "Body", "check", _w.PathOf("hero.achx"));
        var last = await _w.RunAsync("check", _w.PathOf("hero.achx"), "--chain");
        var noCommand = await _w.RunAsync("--bogus", "check", _w.PathOf("hero.achx"));

        first.Err.ShouldContain("check does not take --track");
        last.Err.ShouldContain("check does not take --chain");
        noCommand.Err.ShouldContain("unknown option '--bogus'");
        new[] { first.Code, last.Code, noCommand.Code }.ShouldAllBe(c => c == 2);
    }

    [Fact]
    public async Task OptionsBeforeTheCommand_Work()
    {
        var r = await _w.RunAsync("--json", "--chain", "Walk", "describe", _w.PathOf("hero.achx"));
        var schema = await _w.RunAsync("--json", "--schema");

        r.Code.ShouldBe(0, r.Err);
        using var doc = JsonDocument.Parse(r.Out);
        doc.RootElement.GetProperty("chains").GetArrayLength().ShouldBe(1);
        schema.Code.ShouldBe(0);
        JsonDocument.Parse(schema.Out).Dispose();
    }

    [Theory]
    [InlineData("render", "--pass", "--pass needs a value")]
    [InlineData("edit", "--ops", "--ops needs a file")]
    [InlineData("edit", "--op", "--op needs an operation")]
    [InlineData("convert", "--to", "--to needs achj or achx")]
    [InlineData("at", "--time", "--time needs a value")]
    public async Task OptionWithoutItsValue_SaysWhatItNeeds(string command, string option, string expected)
    {
        var r = await _w.RunAsync(command, _w.PathOf("hero.achx"), option);

        r.Code.ShouldBe(2);
        r.Err.ShouldContain(expected);
    }

    [Theory]
    [InlineData("describe", "hero.seqj", "--chain", "--chain applies to .achx/.achj files; a .seqj takes --sequence and --track.")]
    [InlineData("describe", "hero.achx", "--sequence", "--sequence and --track apply to .seqj files; an .achx/.achj takes --chain.")]
    [InlineData("describe", "hero.achx", "--track", "--sequence and --track apply to .seqj files; an .achx/.achj takes --chain.")]
    [InlineData("at", "hero.seqj", "--chain", "--chain applies to .achx/.achj files; a .seqj takes --sequence and --track.")]
    [InlineData("at", "hero.achx", "--sequence", "--sequence and --track apply to .seqj files; an .achx/.achj takes --chain.")]
    [InlineData("at", "hero.achx", "--track", "--sequence and --track apply to .seqj files; an .achx/.achj takes --chain.")]
    [InlineData("at", "hero.achx", "--pass", "--pass is for .seqj sequences.")]
    [InlineData("render", "hero.seqj", "--chain", "--chain is for .achx/.achj; use --sequence with a .seqj.")]
    [InlineData("render", "hero.seqj", "--frame", "--frame is for chains; use --at with a sequence.")]
    [InlineData("render", "hero.achx", "--sequence", "--sequence is for .seqj; use --chain with an .achx/.achj.")]
    [InlineData("render", "hero.achx", "--pass", "--pass is for .seqj sequences.")]
    public async Task OptionOfTheOtherFileKind_KeepsItsOwnMessage(string command, string file, string option, string message)
    {
        WriteSequence();
        string[] rest = command switch { "at" => ["--time", "0"], "render" => ["--out", _w.PathOf("out")], _ => [] };

        var r = await _w.RunAsync([command, _w.PathOf(file), option, "0", .. rest]);

        r.Code.ShouldBe(2);
        r.Err.ShouldContain(message);
    }
}

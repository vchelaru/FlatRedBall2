using System.Globalization;
using AnimationEditor.Editing;

namespace AnimationEditor.Cli;

/// <summary>One option of a command.</summary>
/// <param name="Needs">The error when its value is missing; null for a flag, which takes none.</param>
/// <param name="ForSequence">True when only a .seqj takes it, false when a .seqj does not; null when any file does.</param>
/// <param name="WrongKind">The error when the file is of the other kind.</param>
internal sealed record CliOption(string Name, string? Needs = null, bool? ForSequence = null, string? WrongKind = null)
{
    public override string ToString() => ForSequence switch { true => $"{Name} (.seqj only)", false => $"{Name} (not .seqj)", null => Name };
}

/// <summary>A command and every option it takes. Parsing and the error text both read this, so an option not listed here is refused.</summary>
internal sealed record CliCommand(string Name, bool TakesFile, params CliOption[] Options)
{
    public string Refusal(string option) =>
        $"{Name} does not take {option}; it takes {(Options.Length == 0 ? "no options" : string.Join(", ", Options.AsEnumerable()))}.";

    /// <summary>The error when <paramref name="file"/> is of the kind one of the <paramref name="given"/> options is not for, or null.</summary>
    public string? WrongKind(string file, IEnumerable<string> given)
    {
        bool isSequence = SequenceFileReader.IsSequenceFile(file);
        return Options.FirstOrDefault(o => o.ForSequence == !isSequence && given.Contains(o.Name))?.WrongKind;
    }
}

/// <summary>The commands of aecli with the options each one takes.</summary>
internal static class CliCommands
{
    private const string ChainIsNotForSequences = "--chain applies to .achx/.achj files; a .seqj takes --sequence and --track.";
    private const string SequenceAndTrackAreNotForChains = "--sequence and --track apply to .seqj files; an .achx/.achj takes --chain.";

    private static CliOption Flag(string name) => new(name);

    private static CliOption Value(string name, bool? forSequence = null, string? wrongKind = null) => new(name, $"{name} needs a value.", forSequence, wrongKind);

    private static readonly CliOption Json = Flag("--json");
    private static readonly CliOption Chain = Value("--chain", false, ChainIsNotForSequences);
    private static readonly CliOption Sequence = Value("--sequence", true, SequenceAndTrackAreNotForChains);
    private static readonly CliOption Track = Value("--track", true, SequenceAndTrackAreNotForChains);
    private static readonly CliOption Pass = Value("--pass", true, "--pass is for .seqj sequences.");

    /// <summary>What <c>aecli help</c> takes; it is not in <see cref="Find"/> because it runs before parsing.</summary>
    public static readonly CliCommand Help = new("help", TakesFile: false);

    private static readonly CliCommand[] All =
    [
        new("describe", true, Chain, Sequence, Track, Flag("--summary"), Json),
        new("check", true, Json),
        new("new", true, Json),
        new("convert", true, new CliOption("--to", "--to needs achj or achx."), Json),
        new("edit", true, new CliOption("--ops", "--ops needs a file."),
            new CliOption("--op", "--op needs an operation, e.g. --op \"set-frame-offset Walk/0 x=4\"."), Json),
        new("render", true, Value("--out"),
            Value("--sequence", true, "--sequence is for .seqj; use --chain with an .achx/.achj."),
            Value("--chain", false, "--chain is for .achx/.achj; use --sequence with a .seqj."),
            Value("--at"), Value("--frame", false, RenderPlanner.FrameIsForChains), Pass, Value("--scale"), Value("--background"), Value("--bounds"),
            Flag("--grid"), Flag("--shapes"), Flag("--sheet"), Json),
        new("at", true, Value("--time"), Sequence, Track, Chain, Pass, Flag("--shapes-only"), Json),
        new("--schema", false, Json),
        new("--version", false),
    ];

    private static readonly Dictionary<string, CliCommand> ByName = All.ToDictionary(c => c.Name);

    // Whether an option takes a value is the same in every command, so a command line can be split before its command is known.
    private static readonly Dictionary<string, CliOption> OptionsByName = All.SelectMany(c => c.Options).DistinctBy(o => o.Name).ToDictionary(o => o.Name);

    public static CliCommand? Find(string name) => ByName.GetValueOrDefault(name);

    /// <summary>The option of that name in any command, or null.</summary>
    public static CliOption? FindOption(string name) => OptionsByName.GetValueOrDefault(name);

    /// <summary>The <c>--pass</c> value (0 when not given), or false with the usage error.</summary>
    public static bool TryPass(IReadOnlyDictionary<string, string> values, out int pass, out string? error)
    {
        pass = 0;
        error = null;
        if (!values.TryGetValue("--pass", out var text) || int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out pass)) return true;
        error = "--pass must be a whole number, 0 or more (the number of loop wraps so far).";
        return false;
    }
}

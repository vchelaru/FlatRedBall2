namespace AnimationEditor.Core.Diagnostics;

/// <summary>
/// Command line for the opt-in memory probe (issue #949): <c>--memory-probe --probe-file
/// &lt;achx&gt; [--probe-cycles N] [--probe-out &lt;path&gt;]</c>. Absent <c>--memory-probe</c>
/// the app launches normally.
/// </summary>
public sealed class MemoryProbeOptions
{
    public const int DefaultCycles = 5;

    public required string? FilePath { get; init; }
    public int Cycles { get; init; } = DefaultCycles;
    public string? OutputPath { get; init; }

    /// <summary>
    /// Reloads the same file without closing the project in between, reproducing the
    /// tab-accumulating path a user actually takes when reopening files (#949).
    /// </summary>
    public bool KeepOpen { get; init; }

    /// <summary>
    /// Optional second file; cycles alternate between it and <see cref="FilePath"/> so the run
    /// exercises two distinct textures rather than re-focusing one already-open tab.
    /// </summary>
    public string? SecondFilePath { get; init; }

    public static bool TryParse(IReadOnlyList<string> args, out MemoryProbeOptions? options)
    {
        options = null;
        if (!args.Contains("--memory-probe"))
            return false;

        options = new MemoryProbeOptions
        {
            FilePath = ValueAfter(args, "--probe-file"),
            Cycles = int.TryParse(ValueAfter(args, "--probe-cycles"), out int cycles) && cycles > 0
                ? cycles
                : DefaultCycles,
            OutputPath = ValueAfter(args, "--probe-out"),
            KeepOpen = args.Contains("--probe-keep-open"),
            SecondFilePath = ValueAfter(args, "--probe-file2"),
        };
        return true;
    }

    private static string? ValueAfter(IReadOnlyList<string> args, string flag)
    {
        int index = -1;
        for (int i = 0; i < args.Count; i++)
        {
            if (args[i] == flag) { index = i; break; }
        }

        return index >= 0 && index + 1 < args.Count ? args[index + 1] : null;
    }
}

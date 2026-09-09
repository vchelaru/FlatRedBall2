using AnimationEditor.Core.Diagnostics;
using Xunit;

namespace AnimationEditor.Core.Tests;

// Issue #949: the probe is opt-in via command line so a normal launch is untouched.
public class MemoryProbeOptionsTests
{
    [Fact]
    public void TryParse_MissingProbeFlag_ReturnsFalse()
    {
        bool parsed = MemoryProbeOptions.TryParse(
            new[] { @"C:\projects\MyAnim.achx" }, out var options);

        Assert.False(parsed);
        Assert.Null(options);
    }

    [Fact]
    public void TryParse_ProbeFlagWithAllValues_PopulatesOptions()
    {
        string[] args =
        [
            "--memory-probe",
            "--probe-file", @"C:\projects\MyAnim.achx",
            "--probe-cycles", "6",
            "--probe-out", @"C:\out\probe.ndjson",
        ];

        bool parsed = MemoryProbeOptions.TryParse(args, out var options);

        Assert.True(parsed);
        Assert.Equal(@"C:\projects\MyAnim.achx", options!.FilePath);
        Assert.Equal(6, options.Cycles);
        Assert.Equal(@"C:\out\probe.ndjson", options.OutputPath);
    }

    [Fact]
    public void TryParse_ProbeFlagWithKeepOpen_SkipsTheCloseStep()
    {
        string[] args = ["--memory-probe", "--probe-file", @"C:\projects\MyAnim.achx", "--probe-keep-open"];

        MemoryProbeOptions.TryParse(args, out var options);

        Assert.True(options!.KeepOpen);
    }

    [Fact]
    public void TryParse_ProbeFlagWithoutCycles_DefaultsToFiveCycles()
    {
        string[] args = ["--memory-probe", "--probe-file", @"C:\projects\MyAnim.achx"];

        bool parsed = MemoryProbeOptions.TryParse(args, out var options);

        Assert.True(parsed);
        Assert.Equal(5, options!.Cycles);
    }
}

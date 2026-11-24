using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace AnimationEditor.Cli.Tests;

public class VersionTests
{
    [Fact]
    public async Task Version_PrintsVersionCommitAndUtcBuildTimeOnOneLine()
    {
        using var w = new CliWorkspace();

        var r = await w.RunAsync("--version");

        r.Code.ShouldBe(0);
        Regex.IsMatch(r.Out.TrimEnd(), @"^aecli \S+\+[0-9a-f]{7,40} built \d{4}-\d\d-\d\d \d\d:\d\d:\d\dZ$").ShouldBeTrue(r.Out);
    }
}

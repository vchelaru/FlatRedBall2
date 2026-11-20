using System.Text.Json;
using AnimationEditor.Editing;
using Shouldly;
using Xunit;

namespace AnimationEditor.Cli.Tests;

public class CheckCommandTests : IDisposable
{
    private readonly CliWorkspace _w = new("hero.achx", "hero.achj", "broken.achx", "sheet.png");

    public void Dispose() => _w.Dispose();

    [Theory]
    [InlineData("hero.achx")]
    [InlineData("hero.achj")]
    public async Task CleanFile_ExitsZero(string file)
    {
        var r = await _w.RunAsync("check", _w.PathOf(file));

        r.Code.ShouldBe(0);
    }

    [Fact]
    public async Task BrokenFile_ExitsOneAndNamesEachProblem()
    {
        var r = await _w.RunAsync("check", _w.PathOf("broken.achx"));

        r.Code.ShouldBe(1);
        r.Out.ShouldContain("missing.png");
        r.Out.ShouldContain("Idle");
        r.Out.ShouldContain("unit", Case.Insensitive);
    }

    [Fact]
    public async Task UndefinedUnit_IsAWarningNotAnError()
    {
        var path = _w.PathOf("hero.achx");
        File.WriteAllText(path, File.ReadAllText(path).Replace("<TimeMeasurementUnit>Second", "<TimeMeasurementUnit>Undefined"));

        var r = await _w.RunAsync("check", path);

        r.Code.ShouldBe(0);
        r.Out.ShouldContain("unit", Case.Insensitive);
    }

    [Fact]
    public async Task Json_ListsIssuesWithSeverity()
    {
        var r = await _w.RunAsync("check", _w.PathOf("broken.achx"), "--json");

        r.Code.ShouldBe(1);
        using var doc = JsonDocument.Parse(r.Out);
        doc.RootElement.GetProperty("ok").GetBoolean().ShouldBeFalse();
        var issues = doc.RootElement.GetProperty("issues").EnumerateArray().ToList();
        issues.Select(i => i.GetProperty("severity").GetString()).ShouldContain("error");
        issues.Select(i => i.GetProperty("code").GetString()).ShouldContain("missing-texture");
        issues.Select(i => i.GetProperty("code").GetString()).ShouldContain("duplicate-chain-name");
    }

    [Fact]
    public async Task UnreadableFile_ExitsOne()
    {
        var path = _w.Write("garbage.achx", "not xml at all");

        var r = await _w.RunAsync("check", path);

        r.Code.ShouldBe(1);
        r.Err.ShouldContain("garbage.achx");
    }

    [Fact]
    public void NewErrors_ReportsOnlyErrorsAbsentBefore()
    {
        var before = new CheckReport([
            new CheckIssue(CheckSeverity.Error, "missing-texture", "Texture a.png not found."),
            new CheckIssue(CheckSeverity.Warning, "unit", "unit undefined")]);
        var after = new CheckReport([
            new CheckIssue(CheckSeverity.Error, "missing-texture", "Texture a.png not found."),
            new CheckIssue(CheckSeverity.Error, "duplicate-chain-name", "Two chains are named Run."),
            new CheckIssue(CheckSeverity.Warning, "other", "w")]);

        var added = CheckReport.NewErrors(before, after);

        added.Select(i => i.Code).ShouldBe(["duplicate-chain-name"]);
    }
}

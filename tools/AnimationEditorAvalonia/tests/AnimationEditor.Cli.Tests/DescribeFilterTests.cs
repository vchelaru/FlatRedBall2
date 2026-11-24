using System.Text.Json;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Cli.Tests;

public class DescribeFilterTests : IDisposable
{
    private readonly CliWorkspace _w = new("hero.achx", "hero.achj", "sheet.png");

    public void Dispose() => _w.Dispose();

    private string WriteSeq()
    {
        var save = new SequenceListSave();
        save.Sources.Add("hero.achj");
        foreach (var name in new[] { "Run", "Agro" })
        {
            var seq = new SequenceSave { Name = name };
            foreach (var track in new[] { "Body", "EyeLeft", "EyeRight" })
                seq.Tracks.Add(new SequenceTrackSave
                {
                    Name = track,
                    Chains = { new SequenceChainReferenceSave { Source = "hero.achj", Chain = "Walk", Start = 0, Length = 0.8 } },
                });
            save.Sequences.Add(seq);
        }
        var path = _w.PathOf("hero.seqj");
        save.Save(path);
        return path;
    }

    [Fact]
    public async Task Chain_Glob_ShowsOnlyMatchingChains_TextAndJson()
    {
        var text = await _w.RunAsync("describe", _w.PathOf("hero.achx"), "--chain", "Wa*");
        text.Code.ShouldBe(0, text.Err);
        text.Out.ShouldContain("Walk");
        text.Out.ShouldNotContain("Idle");

        var json = await _w.RunAsync("describe", _w.PathOf("hero.achx"), "--chain", "Wa*", "--json");
        using var doc = JsonDocument.Parse(json.Out);
        doc.RootElement.GetProperty("chains").EnumerateArray().Select(c => c.GetProperty("name").GetString()).ShouldBe(["Walk"]);
    }

    [Fact]
    public async Task Chain_NoMatch_FailsWithDidYouMean()
    {
        var r = await _w.RunAsync("describe", _w.PathOf("hero.achx"), "--chain", "Wlak");

        r.Code.ShouldBe(1);
        r.Err.ShouldContain("No chain matches 'Wlak'");
        r.Err.ShouldContain("Did you mean 'Walk'?");
    }

    [Fact]
    public async Task Summary_Chains_OneLineEach_TextAndJson()
    {
        var text = await _w.RunAsync("describe", _w.PathOf("hero.achx"), "--summary");
        text.Code.ShouldBe(0, text.Err);
        var lines = text.Out.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        lines.Length.ShouldBe(2);
        lines[0].ShouldStartWith("Walk  3 frames  length ");

        var json = await _w.RunAsync("describe", _w.PathOf("hero.achx"), "--summary", "--json");
        using var doc = JsonDocument.Parse(json.Out);
        var walk = doc.RootElement.GetProperty("chains")[0];
        walk.GetProperty("frames").GetInt32().ShouldBe(3);
        walk.TryGetProperty("length", out _).ShouldBeTrue();
        walk.TryGetProperty("frames", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Sequence_And_Track_FilterTheSeqjDescription()
    {
        var r = await _w.RunAsync("describe", WriteSeq(), "--sequence", "Agro", "--track", "Eye*");

        r.Code.ShouldBe(0, r.Err);
        r.Out.ShouldContain("Agro/EyeLeft/0");
        r.Out.ShouldContain("Agro/EyeRight/0");
        r.Out.ShouldNotContain("Agro/Body");
        r.Out.ShouldNotContain("Run");
    }

    [Fact]
    public async Task Summary_Tracks_OneLinePerTrack_TextAndJson()
    {
        var file = WriteSeq();

        var text = await _w.RunAsync("describe", file, "--sequence", "Run", "--summary");
        text.Out.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ShouldBe(
            ["Run/Body  1 block  length 0.8s", "Run/EyeLeft  1 block  length 0.8s", "Run/EyeRight  1 block  length 0.8s"]);

        var json = await _w.RunAsync("describe", file, "--track", "Body", "--summary", "--json");
        using var doc = JsonDocument.Parse(json.Out);
        var tracks = doc.RootElement.GetProperty("tracks");
        tracks.GetArrayLength().ShouldBe(2);
        tracks[0].GetProperty("sequence").GetString().ShouldBe("Run");
        tracks[0].GetProperty("blocks").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task Sequence_And_Track_NoMatch_FailWithHints()
    {
        var file = WriteSeq();

        var seq = await _w.RunAsync("describe", file, "--sequence", "Agr");
        seq.Code.ShouldBe(1);
        seq.Err.ShouldContain("Did you mean 'Agro'?");

        var track = await _w.RunAsync("describe", file, "--track", "Bdy");
        track.Code.ShouldBe(1);
        track.Err.ShouldContain("Did you mean 'Body'?");
    }

    [Fact]
    public async Task FilterForTheOtherFileKind_IsUsageError()
    {
        (await _w.RunAsync("describe", _w.PathOf("hero.achx"), "--track", "Body")).Code.ShouldBe(2);
        (await _w.RunAsync("describe", WriteSeq(), "--chain", "Walk")).Code.ShouldBe(2);
    }
}

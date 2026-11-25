using System.Text.Json;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Cli.Tests;

/// <summary>#2137: at, render, describe and check skip zero-length frames, and an animation of no length never makes them loop forever.</summary>
public class ZeroLengthTests : IDisposable
{
    private const int TimeLimit = 60000;

    private readonly CliWorkspace _w = new("sheet.png");

    public void Dispose() => _w.Dispose();

    private static string Chain(string name, bool pingPong, params string[] lengths) =>
        $"<AnimationChain><Name>{name}</Name><PingPong>{(pingPong ? "true" : "false")}</PingPong>" + string.Concat(lengths.Select((length, i) =>
            $"<Frame><TextureName>sheet.png</TextureName><FrameLength>{length}</FrameLength><LeftCoordinate>{i * 16}</LeftCoordinate>"
            + $"<RightCoordinate>{i * 16 + 16}</RightCoordinate><TopCoordinate>0</TopCoordinate><BottomCoordinate>32</BottomCoordinate></Frame>")) + "</AnimationChain>";

    private string WriteChains() => _w.Write("zero.achx",
        "<AnimationChainArraySave><FileRelativeTextures>true</FileRelativeTextures><TimeMeasurementUnit>Second</TimeMeasurementUnit><CoordinateType>Pixel</CoordinateType>"
        + Chain("Mix", false, "0.2", "0", "0.2") + Chain("None", false, "0", "0") + Chain("NonePingPong", true, "0", "0")
        + Chain("Tiny", true, "0", "0.0000001", "0") + Chain("Bad", false, "-1", "0.2", "-1") + "</AnimationChainArraySave>");

    private string WriteSequences(string chain)
    {
        WriteChains();
        var save = new SequenceListSave { Sources = { "zero.achx" } };
        save.Sequences.Add(new SequenceSave { Name = "Leaf", Tracks = { new SequenceTrackSave { Name = "B", Continuous = true, Chains = { new SequenceChainReferenceSave { Source = "zero.achx", Chain = chain } } } } });
        save.Sequences.Add(new SequenceSave { Name = "Middle", Tracks = { new SequenceTrackSave { Name = "C", Continuous = true, References = [new SequenceReferenceSave { Sequence = "Leaf", Length = 2 }] } } });
        save.Sequences.Add(new SequenceSave { Name = "Top", Tracks = { new SequenceTrackSave { Name = "D", References = [new SequenceReferenceSave { Sequence = "Middle" }] } } });
        var path = _w.PathOf("zero.seqj");
        save.Save(path);
        return path;
    }

    [Theory(Timeout = TimeLimit)]
    [InlineData("Mix", "0,0.1,0.3", "0 0 2")]
    [InlineData("None", "0,0.1,86400", "1 1 1")]
    [InlineData("NonePingPong", "0,86400", "0 0")]
    [InlineData("Bad", "0,0.1,0.3", "1 1 1")]
    public async Task At_Chain_ShowsTheFramesTheGameShows(string chain, string times, string frames)
    {
        var r = await _w.RunAsync("at", WriteChains(), "--chain", chain, "--time", times, "--json");

        r.Code.ShouldBe(0, r.Err);
        using var doc = JsonDocument.Parse(r.Out);
        string.Join(' ', doc.RootElement.GetProperty("times").EnumerateArray().Select(t => t.GetProperty("sprites")[0].GetProperty("frame").GetInt32())).ShouldBe(frames);
    }

    [Theory(Timeout = TimeLimit)]
    [InlineData("None")]
    [InlineData("NonePingPong")]
    [InlineData("Tiny")]
    public async Task Render_Chain_OfNoLengthOrNextToNone_WritesAPicture(string chain)
    {
        var r = await _w.RunAsync("render", WriteChains(), "--chain", chain, "--at", "0,0.5", "--out", _w.PathOf("out"));

        r.Code.ShouldBe(0, r.Err);
        Directory.GetFiles(_w.PathOf("out"), "*.png").Length.ShouldBe(2);
    }

    [Theory(Timeout = TimeLimit)]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Describe_Chain_LengthIsWhatPlays(bool json)
    {
        var r = await _w.RunAsync(["describe", WriteChains(), .. json ? new[] { "--json" } : []]);

        r.Code.ShouldBe(0, r.Err);
        if (!json) return;
        using var doc = JsonDocument.Parse(r.Out);
        doc.RootElement.GetProperty("chains").EnumerateArray().Select(c => c.GetProperty("length").GetDouble()).ShouldBe([0.4, 0, 0, 0, 0.2]);
    }

    [Theory(Timeout = TimeLimit)]
    [InlineData("None")]
    [InlineData("NonePingPong")]
    [InlineData("Tiny")]
    public async Task Sequence_OfNoLengthOrNextToNone_EveryCommandReturns(string chain)
    {
        string path = WriteSequences(chain);
        foreach (string sequence in new[] { "Leaf", "Middle", "Top" })
        {
            (await _w.RunAsync("at", path, "--sequence", sequence, "--time", "0,0.5,86400", "--pass", "3")).Code.ShouldBe(0);
            await _w.RunAsync("render", path, "--sequence", sequence, "--at", "0", "--out", _w.PathOf("out"));
        }
        (await _w.RunAsync("describe", path, "--json")).Code.ShouldBe(0);
        (await _w.RunAsync("check", path, "--json")).Code.ShouldBe(0);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-0.1")]
    [InlineData("NaN")]
    [InlineData("1e39")]
    public async Task Check_LengthTheEditorSkips_Warns(string length)
    {
        var path = _w.Write("bad.achx",
            "<AnimationChainArraySave><FileRelativeTextures>true</FileRelativeTextures><TimeMeasurementUnit>Second</TimeMeasurementUnit><CoordinateType>Pixel</CoordinateType>"
            + Chain("Bad", false, "0.2", length) + "</AnimationChainArraySave>");

        var r = await _w.RunAsync("check", path, "--json");

        using var doc = JsonDocument.Parse(r.Out);
        doc.RootElement.GetProperty("issues").EnumerateArray().Select(i => i.GetProperty("code").GetString()).ShouldContain("zero-length-frame");
    }

    // Null: the block has no length of its own, so it plays nothing.
    [Theory(Timeout = TimeLimit)]
    [InlineData("None", null, 0)]
    [InlineData("None", 2.0, 2)]
    [InlineData("NonePingPong", 2.0, 2)]
    public async Task Sequence_BlockWithItsOwnLength_HoldsTheFrame(string chain, double? length, double end)
    {
        WriteChains();
        var save = new SequenceListSave { Sources = { "zero.achx" } };
        save.Sequences.Add(new SequenceSave { Name = "S", Tracks = { new SequenceTrackSave { Name = "B", Chains = { new SequenceChainReferenceSave { Source = "zero.achx", Chain = chain, Length = length } } } } });
        var path = _w.PathOf("held.seqj");
        save.Save(path);

        var at = await _w.RunAsync("at", path, "--time", "0,1.5");
        var render = await _w.RunAsync("render", path, "--at", "1.5", "--out", _w.PathOf("out"));
        var describe = await _w.RunAsync("describe", path, "--json");
        var check = await _w.RunAsync("check", path);

        at.Out.Contains($"{chain} frame {(chain == "None" ? 1 : 0)}").ShouldBe(end > 0);
        render.Code.ShouldBe(end > 0 ? 0 : 1);
        using var doc = JsonDocument.Parse(describe.Out);
        doc.RootElement.GetProperty("sequences")[0].GetProperty("length").GetDouble().ShouldBe(end);
        check.Code.ShouldBe(0, check.Out);
    }

    [Fact]
    public async Task Check_HeldBlockCutByTheNext_Warns()
    {
        WriteChains();
        var save = new SequenceListSave { Sources = { "zero.achx" } };
        save.Sequences.Add(new SequenceSave { Name = "S", Tracks = { new SequenceTrackSave { Name = "B", Chains = { new SequenceChainReferenceSave { Source = "zero.achx", Chain = "None", Length = 2 }, new SequenceChainReferenceSave { Source = "zero.achx", Chain = "Mix", Start = 1 } } } } });
        var path = _w.PathOf("cut.seqj");
        save.Save(path);

        (await _w.RunAsync("check", path)).Out.ShouldContain("cut short: it plays 1 of 2 seconds");
    }
}

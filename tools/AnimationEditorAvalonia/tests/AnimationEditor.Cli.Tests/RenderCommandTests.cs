using System.Buffers.Binary;
using System.Text.Json;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Cli.Tests;

public class RenderCommandTests : IDisposable
{
    private readonly CliWorkspace _w = new("hero.achj", "sheet.png");

    public void Dispose() => _w.Dispose();

    // A 48x32 frame of the same sheet, so a sequence that shows it is wider than one that shows a hero frame (16x32).
    private const string WideChain = """
        {"fileRelativeTextures":true,"timeMeasurementUnit":"Second","coordinateType":"Pixel","animationChains":[
          {"name":"Big","frames":[{"textureName":"sheet.png","frameLength":0.5,
            "leftCoordinate":0,"rightCoordinate":48,"topCoordinate":0,"bottomCoordinate":32}]}]}
        """;

    // Walk (16x32) plays 0 to 0.4 on one track and is hidden after; Big (48x32) shows from 1 on the other.
    private string WriteSequence()
    {
        _w.Write("wide.achj", WideChain);
        var save = new SequenceListSave();
        save.Sources.Add("hero.achj");
        save.Sources.Add("wide.achj");
        var seq = new SequenceSave { Name = "Run" };
        seq.Tracks.Add(new SequenceTrackSave { Name = "Body", Chains = { new SequenceChainReferenceSave { Source = "hero.achj", Chain = "Walk", Start = 0 } } });
        seq.Tracks.Add(new SequenceTrackSave { Name = "Wide", Chains = { new SequenceChainReferenceSave { Source = "wide.achj", Chain = "Big", Start = 1 } } });
        save.Sequences.Add(seq);
        var path = _w.PathOf("hero.seqj");
        save.Save(path);
        return path;
    }

    private (int Width, int Height) PngSize(string path)
    {
        var bytes = File.ReadAllBytes(path);
        bytes.AsSpan(0, 8).ToArray().ShouldBe(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        return (BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16)), BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20)));
    }

    [Fact]
    public async Task Chain_Frame_WritesPngAtScale()
    {
        var r = await _w.RunAsync("render", _w.PathOf("hero.achj"), "--chain", "Walk", "--frame", "0", "--scale", "3", "--out", _w.PathOf("out"));

        r.Code.ShouldBe(0, r.Err);
        var file = _w.PathOf("out/Walk_frame0.png");
        r.Out.Trim().ShouldBe(Path.GetFullPath(file));
        PngSize(file).ShouldBe((16 * 3, 32 * 3));
    }

    [Fact]
    public async Task Sequence_AutoBounds_AreIdenticalAcrossTimes_AndFilesAreNamedByTime()
    {
        var r = await _w.RunAsync("render", WriteSequence(), "--sequence", "Run", "--at", "0,1.2", "--out", _w.PathOf("out"), "--json");

        r.Code.ShouldBe(0, r.Err);
        using var doc = JsonDocument.Parse(r.Out);
        doc.RootElement.GetProperty("success").GetBoolean().ShouldBeTrue();
        doc.RootElement.GetProperty("files").EnumerateArray().Select(f => Path.GetFileName(f.GetString())).ShouldBe(["Run_0.png", "Run_1.2.png"]);
        // Time 0 shows only the 16 px frame, but the canvas is fitted to the 48 px frame of the later time.
        PngSize(_w.PathOf("out/Run_0.png")).ShouldBe((48 * 4, 32 * 4));
        PngSize(_w.PathOf("out/Run_1.2.png")).ShouldBe((48 * 4, 32 * 4));
    }

    [Fact]
    public async Task Sequence_PassArgument_NamesFilesByPass_AndIsRejectedForChains()
    {
        var r = await _w.RunAsync("render", WriteSequence(), "--sequence", "Run", "--at", "0", "--pass", "2", "--out", _w.PathOf("out"), "--json");

        r.Code.ShouldBe(0, r.Err);
        File.Exists(_w.PathOf("out/Run_0_pass2.png")).ShouldBeTrue();
        (await _w.RunAsync("render", _w.PathOf("hero.achj"), "--chain", "Walk", "--pass", "1", "--out", _w.PathOf("out2"))).Code.ShouldNotBe(0);
    }

    [Fact]
    public async Task ExplicitBounds_SetTheCanvasSize()
    {
        var r = await _w.RunAsync("render", _w.PathOf("hero.achj"), "--chain", "Walk", "--bounds", "-5,-10,10,20", "--scale", "2", "--out", _w.PathOf("out"));

        r.Code.ShouldBe(0, r.Err);
        PngSize(_w.PathOf("out/Walk_frame0.png")).ShouldBe((20, 40));
    }

    [Fact]
    public async Task SequenceInAnotherCase_IsUsedOnlyWhenOneSequenceMatches()
    {
        var file = WriteSequence();
        var single = await _w.RunAsync("render", file, "--sequence", "RUN", "--out", _w.PathOf("out"));
        var save = SequenceListSave.FromFile(file);
        save.Sequences.Add(new SequenceSave { Name = "run" });
        save.Save(file);
        var ambiguous = await _w.RunAsync("render", file, "--sequence", "RUN", "--out", _w.PathOf("out"));

        single.Code.ShouldBe(0, single.Err);
        ambiguous.Code.ShouldBe(1);
        ambiguous.Err.ShouldContain("No sequence named 'RUN'");
    }

    [Fact]
    public async Task UnknownSequence_FailsWithDidYouMean_InTextAndJson()
    {
        var file = WriteSequence();

        var text = await _w.RunAsync("render", file, "--sequence", "Rn", "--out", _w.PathOf("out"));
        var json = await _w.RunAsync("render", file, "--sequence", "Rn", "--out", _w.PathOf("out"), "--json");

        text.Code.ShouldBe(1);
        text.Err.ShouldContain("Did you mean 'Run'?");
        json.Code.ShouldBe(1);
        using var doc = JsonDocument.Parse(json.Out);
        doc.RootElement.GetProperty("success").GetBoolean().ShouldBeFalse();
        doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString().ShouldBe("NotFound");
        Directory.Exists(_w.PathOf("out")).ShouldBeFalse();
    }

    [Fact]
    public async Task MissingOut_IsAUsageError()
    {
        var r = await _w.RunAsync("render", _w.PathOf("hero.achj"), "--chain", "Walk");

        r.Code.ShouldBe(2);
        r.Err.ShouldContain("--out");
    }

    [Fact]
    public async Task Shapes_AreDrawnOverTheFrame()
    {
        var plain = await _w.RunAsync("render", _w.PathOf("hero.achj"), "--chain", "Walk", "--frame", "2", "--scale", "2", "--out", _w.PathOf("plain"));
        var shaped = await _w.RunAsync("render", _w.PathOf("hero.achj"), "--chain", "Walk", "--frame", "2", "--scale", "2", "--shapes", "--out", _w.PathOf("shaped"));

        plain.Code.ShouldBe(0, plain.Err);
        shaped.Code.ShouldBe(0, shaped.Err);
        _w.Bytes("shaped/Walk_frame2.png").ShouldNotBe(_w.Bytes("plain/Walk_frame2.png"));
    }

    [Fact]
    public async Task Sheet_WritesOnePngWithATilePerTime()
    {
        var r = await _w.RunAsync("render", _w.PathOf("hero.achj"), "--chain", "Walk", "--frame", "0,1", "--scale", "1", "--sheet", "--out", _w.PathOf("out"));

        r.Code.ShouldBe(0, r.Err);
        r.Out.Trim().ShouldBe(Path.GetFullPath(_w.PathOf("out/Walk_sheet.png")));
        Directory.GetFiles(_w.PathOf("out")).Length.ShouldBe(1);
        // Two 16x32 tiles side by side: 8 px gaps, a 20 px caption strip under each.
        PngSize(_w.PathOf("out/Walk_sheet.png")).ShouldBe((8 + 2 * (16 + 8), 8 + 32 + 20 + 8));
    }

    [Fact]
    public async Task Sheet_TooLarge_FailsAndSuggestsLowerScale()
    {
        var frames = string.Join(",", Enumerable.Repeat("0", 16));
        var r = await _w.RunAsync("render", _w.PathOf("hero.achj"), "--chain", "Walk", "--frame", frames, "--bounds", "0,0,100,100", "--scale", "16", "--sheet", "--out", _w.PathOf("out"));

        r.Code.ShouldBe(1);
        r.Err.ShouldContain("contact sheet");
        r.Err.ShouldContain("--scale");
        Directory.Exists(_w.PathOf("out")).ShouldBeFalse();
    }
}

using System.Text.Json;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Cli.Tests;

public class AtCommandTests : IDisposable
{
    private readonly CliWorkspace _w = new("hero.achj", "sheet.png");

    public void Dispose() => _w.Dispose();

    // Walk (frames 0.1, 0.1, 0.2; frame 2 has rect Hitbox at 1,2 scale 4x6) on track "Body" from 0; Idle on track "Fx" from 1.
    private string WriteSequence()
    {
        var save = new SequenceListSave();
        save.Sources.Add("hero.achj");
        var seq = new SequenceSave { Name = "Run" };
        seq.Tracks.Add(new SequenceTrackSave { Name = "Body", Chains = { new SequenceChainReferenceSave { Source = "hero.achj", Chain = "Walk", Start = 0 } } });
        seq.Tracks.Add(new SequenceTrackSave { Name = "Fx", Chains = { new SequenceChainReferenceSave { Source = "hero.achj", Chain = "Idle", Start = 1 } } });
        save.Sequences.Add(seq);
        var path = _w.PathOf("hero.seqj");
        save.Save(path);
        return path;
    }

    [Fact]
    public async Task Text_ListsChainFrameOffsetAndShapesPerTime_AndHiddenTracksAreLeftOut()
    {
        var r = await _w.RunAsync("at", WriteSequence(), "--sequence", "Run", "--time", "0,0.25");

        r.Code.ShouldBe(0, r.Err);
        r.Out.ShouldContain("Run at 0s");
        r.Out.ShouldContain("track 0 Body: Walk frame 0 offset 0,0");
        r.Out.ShouldContain("Run at 0.25s");
        r.Out.ShouldContain("track 0 Body: Walk frame 2 offset 0,0");
        r.Out.ShouldContain("rect Hitbox at 1,2 scale 4x6");
        r.Out.ShouldNotContain("Fx");
    }

    [Fact]
    public async Task Json_HasTheSameFacts()
    {
        var r = await _w.RunAsync("at", WriteSequence(), "--time", "0.25", "--json");

        r.Code.ShouldBe(0, r.Err);
        using var doc = JsonDocument.Parse(r.Out);
        var at = doc.RootElement.GetProperty("times")[0];
        at.GetProperty("time").GetDouble().ShouldBe(0.25);
        var sprite = at.GetProperty("sprites")[0];
        sprite.GetProperty("track").GetInt32().ShouldBe(0);
        sprite.GetProperty("chain").GetString().ShouldBe("Walk");
        sprite.GetProperty("frame").GetInt32().ShouldBe(2);
        var shape = sprite.GetProperty("shapes")[0];
        shape.GetProperty("type").GetString().ShouldBe("rect");
        shape.GetProperty("name").GetString().ShouldBe("Hitbox");
        shape.GetProperty("x").GetDouble().ShouldBe(1);
        shape.GetProperty("scaleY").GetDouble().ShouldBe(6);
    }

    [Fact]
    public async Task TrackGlob_KeepsMatchingTracksOnly_AndNoMatchFailsWithDidYouMean()
    {
        var file = WriteSequence();

        var some = await _w.RunAsync("at", file, "--time", "1.2", "--track", "Fx*");
        var none = await _w.RunAsync("at", file, "--time", "1.2", "--track", "Bdy");

        some.Code.ShouldBe(0, some.Err);
        some.Out.ShouldContain("track 1 Fx");
        some.Out.ShouldNotContain("Body");
        none.Code.ShouldBe(1);
        none.Err.ShouldContain("No track matches 'Bdy'");
        none.Err.ShouldContain("Did you mean 'Body'?");
    }

    [Fact]
    public async Task ShapesOnly_DropsTracksWhoseActiveFrameHasNoShapes_InTextAndJson()
    {
        var file = WriteSequence();

        var early = await _w.RunAsync("at", file, "--time", "0", "--shapes-only");
        var late = await _w.RunAsync("at", file, "--time", "0.25", "--shapes-only", "--json");

        early.Code.ShouldBe(0, early.Err);
        early.Out.ShouldNotContain("track 0");
        late.Code.ShouldBe(0, late.Err);
        using var doc = JsonDocument.Parse(late.Out);
        doc.RootElement.GetProperty("times")[0].GetProperty("sprites").GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task Schema_DescribesAt()
    {
        var r = await _w.RunAsync("--schema");

        r.Out.ShouldContain("at <file.seqj> --time");
    }

    [Fact]
    public async Task UnknownSequence_FailsWithDidYouMean_AndBadTimeIsUsage()
    {
        var file = WriteSequence();

        var unknown = await _w.RunAsync("at", file, "--sequence", "Rn", "--time", "0");
        var bad = await _w.RunAsync("at", file, "--time", "x");
        var missing = await _w.RunAsync("at", file);

        unknown.Code.ShouldBe(1);
        unknown.Err.ShouldContain("Did you mean 'Run'?");
        bad.Code.ShouldBe(2);
        missing.Code.ShouldBe(2);
        missing.Err.ShouldContain("--time");
    }

    // Walk at 0.1 s per frame: frame 0 glides red 0 to 200 and has two events, frame 1 is flipped, frame 2 has a rect.
    private string WriteChains(string name, Action<AnimationChainSave>? adjust = null)
    {
        var chain = new AnimationChainSave { Name = "Walk" };
        for (int i = 0; i < 3; i++)
            chain.Frames.Add(new AnimationFrameSave { TextureName = "sheet.png", FrameLength = 0.1f, RightCoordinate = 16, BottomCoordinate = 32 });
        chain.Frames[0].Red = 0;
        chain.Frames[0].ColorOperation = FlatRedBall2.Animation.ColorOperation.Add;
        chain.Frames[0].ColorInterpolation = "Linear";
        chain.Frames[1].Red = 200;
        chain.Frames[0].Events.Add(new FlatRedBall2.Animation.AnimationFrameEvent { Name = "Step", Data = "left" });
        chain.Frames[0].Events.Add(new FlatRedBall2.Animation.AnimationFrameEvent { Name = "Dust" });
        chain.Frames[1].FlipHorizontal = true;
        chain.Frames[1].FlipDiagonal = true;
        (chain.Frames[2].ShapesSave = new ShapesSave()).Add(new AARectSave { Name = "Hitbox", X = 1, Y = 2, ScaleX = 4, ScaleY = 6 });
        adjust?.Invoke(chain);
        var list = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel, TimeMeasurementUnit = TimeMeasurementUnit.Second };
        list.AnimationChains.Add(chain);
        list.AnimationChains.Add(new AnimationChainSave { Name = "Empty" });
        var path = _w.PathOf(name);
        if (name.EndsWith(".achj")) list.SaveJson(path); else list.Save(path);
        return path;
    }

    [Theory]
    [InlineData("fx.achx")]
    [InlineData("fx.achj")]
    public async Task Chain_Text_HasSampledColorFlipsEventsAndShapes(string name)
    {
        var r = await _w.RunAsync("at", WriteChains(name), "--chain", "Walk", "--time", "0.05,0.15,0.25");

        r.Code.ShouldBe(0, r.Err);
        r.Out.ReplaceLineEndings("\n").ShouldBe("""
            Walk at 0.05s
              Walk frame 0 offset 0,0
                color red=100 operation=Add
                event Step  data left
                event Dust
            Walk at 0.15s
              Walk frame 1 offset 0,0  flip H D
                color red=200 operation=Add
            Walk at 0.25s
              Walk frame 2 offset 0,0
                color red=200 operation=Add
                rect Hitbox at 1,2 scale 4x6

            """.ReplaceLineEndings("\n"));
    }

    [Fact]
    public async Task Chain_Json_HasSampledColorFlipsEventsAndShapes_AndNoTrack()
    {
        var r = await _w.RunAsync("at", WriteChains("fx.achx"), "--chain", "walk", "--time", "0.05,0.15", "--json");

        r.Code.ShouldBe(0, r.Err);
        using var doc = JsonDocument.Parse(r.Out);
        doc.RootElement.GetProperty("chain").GetString().ShouldBe("Walk");
        doc.RootElement.TryGetProperty("sequence", out _).ShouldBeFalse();
        var first = doc.RootElement.GetProperty("times")[0].GetProperty("sprites")[0];
        first.TryGetProperty("track", out _).ShouldBeFalse();
        first.GetProperty("frame").GetInt32().ShouldBe(0);
        first.GetProperty("color").GetProperty("red").GetInt32().ShouldBe(100);
        first.GetProperty("color").GetProperty("operation").GetString().ShouldBe("Add");
        first.GetProperty("flipHorizontal").GetBoolean().ShouldBeFalse();
        var events = first.GetProperty("events");
        events.GetArrayLength().ShouldBe(2);
        events[0].GetProperty("name").GetString().ShouldBe("Step");
        events[0].GetProperty("data").GetString().ShouldBe("left");
        events[1].TryGetProperty("data", out _).ShouldBeFalse();
        var second = doc.RootElement.GetProperty("times")[1].GetProperty("sprites")[0];
        second.GetProperty("flipHorizontal").GetBoolean().ShouldBeTrue();
        second.GetProperty("flipVertical").GetBoolean().ShouldBeFalse();
        second.GetProperty("flipDiagonal").GetBoolean().ShouldBeTrue();
        second.GetProperty("events").GetArrayLength().ShouldBe(0);
    }

    [Theory]
    [InlineData(true, false, false, "0.45", 1)]  // wraps to 0.15
    [InlineData(false, false, false, "0.45", 2)] // holds the end
    [InlineData(true, true, false, "0.35", 2)]   // 0.05 into the way back; a plain loop shows frame 0
    [InlineData(true, false, true, "0.05", 2)]   // plays the last frame first
    public async Task Chain_FollowsLoopPingPongAndReverse(bool loop, bool pingPong, bool reverse, string time, int frame)
    {
        var file = WriteChains("fx.achx", c => { c.Loop = loop; c.PingPong = pingPong; c.Reverse = reverse; });

        var r = await _w.RunAsync("at", file, "--chain", "Walk", "--time", time);

        r.Code.ShouldBe(0, r.Err);
        r.Out.ShouldContain($"Walk frame {frame} ");
    }

    [Fact]
    public async Task Chain_ShapesOnly_LeavesOutFramesWithoutShapes()
    {
        var r = await _w.RunAsync("at", WriteChains("fx.achx"), "--chain", "Walk", "--time", "0,0.25", "--shapes-only");

        r.Code.ShouldBe(0, r.Err);
        r.Out.ShouldNotContain("frame 0");
        r.Out.ShouldContain("frame 2");
    }

    [Fact]
    public async Task Sequence_ReportsSampledColorFlipsAndEvents_InTextAndJson()
    {
        WriteChains("fx.achj");
        var save = new SequenceListSave { Sources = { "fx.achj" } };
        save.Sequences.Add(new SequenceSave
        {
            Name = "Run",
            Tracks = { new SequenceTrackSave { Name = "Body", Chains = { new SequenceChainReferenceSave { Source = "fx.achj", Chain = "Walk", Start = 0 } } } },
        });
        var file = _w.PathOf("run.seqj");
        save.Save(file);

        var text = await _w.RunAsync("at", file, "--time", "0.05,0.15");
        var json = await _w.RunAsync("at", file, "--time", "0.05", "--json");

        text.Code.ShouldBe(0, text.Err);
        text.Out.ShouldContain("color red=100 operation=Add");
        text.Out.ShouldContain("event Step  data left");
        text.Out.ShouldContain("track 0 Body: Walk frame 1 offset 0,0  flip H D");
        using var doc = JsonDocument.Parse(json.Out);
        doc.RootElement.GetProperty("sequence").GetString().ShouldBe("Run");
        var sprite = doc.RootElement.GetProperty("times")[0].GetProperty("sprites")[0];
        sprite.GetProperty("color").GetProperty("red").GetInt32().ShouldBe(100);
        sprite.GetProperty("flipVertical").GetBoolean().ShouldBeFalse();
        sprite.GetProperty("events")[0].GetProperty("name").GetString().ShouldBe("Step");
    }

    [Fact]
    public async Task Chain_BadNames_AndOptionsOfTheOtherFileKind_AreRefused()
    {
        var chains = WriteChains("fx.achx");
        var sequence = WriteSequence();

        var unknown = await _w.RunAsync("at", chains, "--chain", "Wlk", "--time", "0");
        var empty = await _w.RunAsync("at", chains, "--chain", "Empty", "--time", "0");
        var unnamed = await _w.RunAsync("at", chains, "--time", "0");
        var absent = await _w.RunAsync("at", _w.PathOf("none.achx"), "--time", "0");
        var tileset = await _w.RunAsync("at", _w.PathOf("a.tsx"), "--time", "0");
        var track = await _w.RunAsync("at", chains, "--chain", "Walk", "--track", "Body", "--time", "0");
        var seqOnChain = await _w.RunAsync("at", chains, "--sequence", "Run", "--time", "0");
        var chainOnSeq = await _w.RunAsync("at", sequence, "--chain", "Walk", "--time", "0");

        unknown.Code.ShouldBe(1);
        unknown.Err.ShouldContain("Did you mean 'Walk'?");
        empty.Code.ShouldBe(1);
        empty.Err.ShouldContain("Chain 'Empty' has no frames.");
        unnamed.Code.ShouldBe(2);
        unnamed.Err.ShouldContain("--chain is needed");
        absent.Code.ShouldBe(1);
        absent.Err.ShouldContain("File not found");
        tileset.Code.ShouldBe(2);
        tileset.Err.ShouldContain("at needs a .seqj, .achx or .achj file.");
        track.Code.ShouldBe(2);
        track.Err.ShouldContain("--sequence and --track apply to .seqj files");
        seqOnChain.Code.ShouldBe(2);
        chainOnSeq.Code.ShouldBe(2);
        chainOnSeq.Err.ShouldContain("--chain applies to .achx/.achj files");
    }

    [Fact]
    public async Task Help_SaysChainsAreAccepted_AndWhenAnEventFires()
    {
        var schema = await _w.RunAsync("--schema");
        var usage = await _w.RunAsync("help");

        foreach (var text in new[] { schema.Out, usage.Out })
        {
            text.ShouldContain("at <file.achx|.achj> --time");
            text.ShouldContain("fires when its frame is entered");
        }
    }
}

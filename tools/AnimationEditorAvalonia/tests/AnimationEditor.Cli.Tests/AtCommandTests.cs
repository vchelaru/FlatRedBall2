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
}

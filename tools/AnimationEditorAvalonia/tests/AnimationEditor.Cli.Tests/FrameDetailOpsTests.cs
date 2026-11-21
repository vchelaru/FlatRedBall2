using System.Text.Json;
using AnimationEditor.Editing;
using FlatRedBall2.Animation;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Cli.Tests;

public class FrameDetailOpsTests : IDisposable
{
    private readonly CliWorkspace _w = new("hero.achx", "hero.achj", "sheet.png");

    public void Dispose() => _w.Dispose();

    private async Task<CliOutput> Edit(string file, string opsJson, params string[] extra) =>
        await _w.RunAsync(["edit", _w.PathOf(file), "--ops", _w.Write("ops.json", opsJson), .. extra]);

    private AnimationChainListSave Doc(string file) => AnimationFileReader.Read(_w.PathOf(file)).Document!;

    private static AnimationFrameSave Frame(AnimationChainListSave doc, string chain, int i) =>
        doc.AnimationChains.Single(c => c.Name == chain).Frames[i];

    [Theory]
    [InlineData("hero.achx")]
    [InlineData("hero.achj")]
    public async Task EventOps_ApplyInOrder_AndSave(string file)
    {
        var r = await Edit(file, """
            {"ops":[
              {"op":"add-frame-event","frame":"Walk/1","name":"Step","data":"left"},
              {"op":"add-frame-event","frame":"Walk/1","name":"Dust"},
              {"op":"set-frame-event","event":"Walk/1/event/1","name":"Smoke","data":"big"},
              {"op":"set-frame-event","event":"Walk/1/event/0","data":""},
              {"op":"add-frame-event","frame":"Walk/0","name":"Gone"},
              {"op":"delete-frame-event","event":"Walk/0/event/0"}
            ]}
            """);

        r.Code.ShouldBe(0, r.Out + r.Err);
        var doc = Doc(file);
        Frame(doc, "Walk", 1).Events.Select(e => (e.Name, e.Data)).ShouldBe([("Step", (string?)null), ("Smoke", "big")]);
        Frame(doc, "Walk", 0).Events.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("hero.achx")]
    [InlineData("hero.achj")]
    public async Task KeyframeAndColorOps_ApplyInOrder_AndSave(string file)
    {
        var r = await Edit(file, """
            {"ops":[
              {"op":"set-frame-keyframe","frame":"Walk/1","group":"offset","value":false},
              {"op":"set-frame-keyframe","frame":"Walk/2","group":"color","value":true},
              {"op":"set-frame-keyframe-curve","frame":"Walk/2","group":"color","curve":"ease-in-out"},
              {"op":"set-frame-keyframe-curve","frame":"Walk/2","group":"offset","curve":"bounce"},
              {"op":"set-frame-color","frame":"Walk/2","red":10,"green":20,"blue":30,"alpha":40,"operation":"add"},
              {"op":"clear-frame-color","frame":"Walk/2","green":true,"alpha":true,"operation":true}
            ]}
            """);

        r.Code.ShouldBe(0, r.Out + r.Err);
        var doc = Doc(file);
        Frame(doc, "Walk", 1).SkipOffsetKeyframe.ShouldBeTrue();
        var f2 = Frame(doc, "Walk", 2);
        (f2.ColorInterpolation, f2.ColorEasing).ShouldBe(("Quadratic", "InOut"));
        (f2.OffsetInterpolation, f2.OffsetEasing).ShouldBe(("Bounce", "Out"));
        (f2.Red, f2.Green, f2.Blue, f2.Alpha, f2.ColorOperation).ShouldBe((10, (int?)null, 30, (int?)null, (ColorOperation?)null));
    }

    [Theory]
    [InlineData("hero.achx")]
    [InlineData("hero.achj")]
    public async Task ShapeKeyframeAndReorderOps_ApplyInOrder_AndSave(string file)
    {
        var r = await Edit(file, """
            {"ops":[
              {"op":"duplicate-frame","frame":"Walk/2"},
              {"op":"set-shape-keyframe","shape":"Walk/3/rect/Hitbox","group":"size","value":false},
              {"op":"set-shape-keyframe-curve","shape":"Walk/3/rect/Hitbox","group":"offset","curve":"linear"},
              {"op":"add-shape","frame":"Walk/3","type":"rect","name":"Second"},
              {"op":"reorder-shape","shape":"Walk/3/rect/Second","index":0}
            ]}
            """);

        r.Code.ShouldBe(0, r.Out + r.Err);
        var shapes = Frame(Doc(file), "Walk", 3).ShapesSave!;
        shapes.AARectSaves.Select(s => s.Name).ShouldBe(["Second", "Hitbox"]);
        var hitbox = shapes.AARectSaves.Single(s => s.Name == "Hitbox");
        hitbox.SkipSizeKeyframe.ShouldBeTrue();
        (hitbox.OffsetInterpolation, hitbox.OffsetEasing).ShouldBe(("Linear", "Out"));
    }

    [Fact]
    public async Task FailingOp_WritesNothing_AndNamesTheOp()
    {
        var before = _w.Bytes("hero.achx");

        var r = await Edit("hero.achx", """
            {"ops":[
              {"op":"add-frame-event","frame":"Walk/1","name":"Step"},
              {"op":"set-frame-keyframe","frame":"Walk/0","group":"offset","value":false}
            ]}
            """);

        r.Code.ShouldBe(1, r.Out + r.Err);
        (r.Out + r.Err).ShouldContain("set-frame-keyframe");
        _w.Bytes("hero.achx").ShouldBe(before);
    }

    [Fact]
    public async Task LockedChain_FailsWithLockedCode_InJson()
    {
        var r = await Edit("hero.achx", """
            {"ops":[
              {"op":"set-chain-locked","chain":"Walk","value":true},
              {"op":"add-frame-event","frame":"Walk/1","name":"Step"}
            ]}
            """, "--json");

        r.Code.ShouldBe(1, r.Out + r.Err);
        using var doc = JsonDocument.Parse(r.Out);
        doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString().ShouldBe("Locked");
    }

    [Theory]
    [InlineData("""{"ops":[{"op":"delete-frame-event","event":"Walk/1/event/0"}]}""", "event")]
    [InlineData("""{"ops":[{"op":"delete-frame-event","event":"Walk/1"}]}""", "event address")]
    [InlineData("""{"ops":[{"op":"delete-frame-event","event":"Walk/1/event/x"}]}""", "index")]
    [InlineData("""{"ops":[{"op":"delete-frame-event","event":"Nope/1/event/0"}]}""", "Nope")]
    [InlineData("""{"ops":[{"op":"set-frame-event","event":"Walk/1/event/0"}]}""", "name")]
    [InlineData("""{"ops":[{"op":"reorder-shape","shape":"Walk/2/rect/Hitbox","index":4}]}""", "range")]
    [InlineData("""{"ops":[{"op":"set-shape-keyframe","shape":"Walk/2/circle/Foot","group":"points","value":false}]}""", "points")]
    [InlineData("""{"ops":[{"op":"clear-frame-color","frame":"Walk/1"}]}""", "clear")]
    public async Task BadAddressOrValues_FailAtRuntime_WriteNothing(string opsJson, string messagePart)
    {
        var before = _w.Bytes("hero.achx");

        var r = await Edit("hero.achx", opsJson);

        r.Code.ShouldBe(1, r.Out + r.Err);
        (r.Out + r.Err).ToLowerInvariant().ShouldContain(messagePart.ToLowerInvariant());
        _w.Bytes("hero.achx").ShouldBe(before);
    }

    [Theory]
    [InlineData("""{"ops":[{"op":"set-frame-keyframe","frame":"Walk/1","group":"size","value":true}]}""", "group")]
    [InlineData("""{"ops":[{"op":"set-shape-keyframe","shape":"Walk/2/rect/Hitbox","group":"color","value":true}]}""", "group")]
    [InlineData("""{"ops":[{"op":"set-frame-keyframe-curve","frame":"Walk/1","group":"offset","curve":"wobble"}]}""", "curve")]
    [InlineData("""{"ops":[{"op":"set-frame-keyframe","frame":"Walk/1","group":"offset"}]}""", "value")]
    [InlineData("""{"ops":[{"op":"add-frame-event","frame":"Walk/1"}]}""", "name")]
    [InlineData("""{"ops":[{"op":"clear-frame-color","frame":"Walk/1","red":"yes"}]}""", "red")]
    public async Task MalformedOps_AreRefusedBeforeAnythingIsApplied(string opsJson, string messagePart)
    {
        var before = _w.Bytes("hero.achx");

        var r = await Edit("hero.achx", opsJson);

        r.Code.ShouldBe(2, r.Out + r.Err);
        (r.Out + r.Err).ToLowerInvariant().ShouldContain(messagePart.ToLowerInvariant());
        _w.Bytes("hero.achx").ShouldBe(before);
    }

    [Theory]
    [InlineData("hero.achx")]
    [InlineData("hero.achj")]
    public async Task NoChangeBatch_LeavesFileByteIdentical(string file)
    {
        var before = _w.Bytes(file);

        var r = await Edit(file, """
            {"ops":[
              {"op":"set-frame-keyframe","frame":"Walk/0","group":"offset","value":true},
              {"op":"set-frame-keyframe","frame":"Walk/1","group":"color","value":true},
              {"op":"set-frame-keyframe-curve","frame":"Walk/1","group":"offset","curve":"snap"},
              {"op":"set-shape-keyframe","shape":"Walk/2/rect/Hitbox","group":"size","value":true},
              {"op":"reorder-shape","shape":"Walk/2/rect/Hitbox","index":0},
              {"op":"clear-frame-color","frame":"Walk/1","red":true,"alpha":true,"operation":true}
            ]}
            """);

        r.Code.ShouldBe(0, r.Out + r.Err);
        _w.Bytes(file).ShouldBe(before);
    }

    [Fact]
    public async Task Describe_ShowsEventsAndKeyframeDataWithAddresses_Text()
    {
        await Edit("hero.achx", """
            {"ops":[
              {"op":"add-frame-event","frame":"Walk/1","name":"Step","data":"left"},
              {"op":"set-frame-keyframe","frame":"Walk/1","group":"offset","value":false},
              {"op":"set-frame-keyframe-curve","frame":"Walk/2","group":"color","curve":"bounce"},
              {"op":"set-frame-color","frame":"Walk/2","red":9},
              {"op":"set-shape-keyframe-curve","shape":"Walk/2/rect/Hitbox","group":"size","curve":"overshoot"}
            ]}
            """);

        var r = await _w.RunAsync("describe", _w.PathOf("hero.achx"));

        r.Code.ShouldBe(0);
        r.Out.ShouldContain("Walk/1/event/0");
        r.Out.ShouldContain("Step");
        r.Out.ShouldContain("left");
        r.Out.ShouldContain("offset=skip");
        r.Out.ShouldContain("color-curve=Bounce");
        r.Out.ShouldContain("red=9");
        r.Out.ShouldContain("size-curve=Back/Out");
    }

    [Fact]
    public async Task Describe_ShowsEventsAndKeyframeDataWithAddresses_Json()
    {
        await Edit("hero.achx", """
            {"ops":[
              {"op":"add-frame-event","frame":"Walk/1","name":"Step","data":"left"},
              {"op":"set-frame-keyframe","frame":"Walk/1","group":"offset","value":false},
              {"op":"set-frame-keyframe-curve","frame":"Walk/2","group":"color","curve":"bounce"},
              {"op":"set-frame-color","frame":"Walk/2","red":9,"operation":"add"},
              {"op":"set-shape-keyframe-curve","shape":"Walk/2/rect/Hitbox","group":"size","curve":"overshoot"}
            ]}
            """);

        var r = await _w.RunAsync("describe", _w.PathOf("hero.achx"), "--json");

        using var doc = JsonDocument.Parse(r.Out);
        var frames = doc.RootElement.GetProperty("chains")[0].GetProperty("frames");
        var ev = frames[1].GetProperty("events")[0];
        ev.GetProperty("address").GetString().ShouldBe("Walk/1/event/0");
        ev.GetProperty("name").GetString().ShouldBe("Step");
        ev.GetProperty("data").GetString().ShouldBe("left");
        var k1 = frames[1].GetProperty("keyframes").GetProperty("offset");
        k1.GetProperty("keyframe").GetBoolean().ShouldBeFalse();
        var k2 = frames[2].GetProperty("keyframes").GetProperty("color");
        k2.GetProperty("keyframe").GetBoolean().ShouldBeTrue();
        k2.GetProperty("interpolation").GetString().ShouldBe("Bounce");
        k2.GetProperty("easing").GetString().ShouldBe("Out");
        var color = frames[2].GetProperty("color");
        color.GetProperty("red").GetInt32().ShouldBe(9);
        color.GetProperty("operation").GetString().ShouldBe("Add");
        var shape = frames[2].GetProperty("shapes").EnumerateArray().First(s => s.GetProperty("name").GetString() == "Hitbox");
        var size = shape.GetProperty("keyframes").GetProperty("size");
        size.GetProperty("interpolation").GetString().ShouldBe("Back");
        shape.GetProperty("keyframes").TryGetProperty("points", out _).ShouldBeFalse();
    }
}

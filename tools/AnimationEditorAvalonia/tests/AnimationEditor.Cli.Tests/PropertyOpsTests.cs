using System.Text.Json;
using AnimationEditor.Editing;
using FlatRedBall2.Animation;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Cli.Tests;

public class PropertyOpsTests : IDisposable
{
    private readonly CliWorkspace _w = new("hero.achx", "hero.achj", "sheet.png", "other.png");

    public void Dispose() => _w.Dispose();

    private async Task<CliOutput> Edit(string file, string opsJson, params string[] extra) =>
        await _w.RunAsync(["edit", _w.PathOf(file), "--ops", _w.Write("ops.json", opsJson), .. extra]);

    private AnimationChainListSave Doc(string file) => AnimationFileReader.Read(_w.PathOf(file)).Document!;

    [Theory]
    [InlineData("hero.achx")]
    [InlineData("hero.achj")]
    public async Task ChainFlagOps_ApplyAndSave(string file)
    {
        var r = await Edit(file, """
            {"ops":[
              {"op":"set-chain-loop","chain":"Walk","value":false},
              {"op":"set-chain-pingpong","chain":"Walk","value":true},
              {"op":"set-chain-reverse","chain":"Idle","value":true},
              {"op":"set-chain-locked","chain":"Idle","value":true}
            ]}
            """);

        r.Code.ShouldBe(0, r.Out + r.Err);
        var doc = Doc(file);
        var walk = doc.AnimationChains.Single(c => c.Name == "Walk");
        var idle = doc.AnimationChains.Single(c => c.Name == "Idle");
        (walk.Loop, walk.PingPong, walk.Reverse).ShouldBe((false, true, false));
        (idle.Reverse, idle.IsLocked).ShouldBe((true, true));
    }

    [Theory]
    [InlineData("hero.achx")]
    [InlineData("hero.achj")]
    public async Task FrameOps_ApplyInOrderAndSave(string file)
    {
        var r = await Edit(file, """
            {"ops":[
              {"op":"set-frame-length","frame":"Walk/1","length":0.25},
              {"op":"set-frame-region","frame":"Walk/0","region":"8,4,16,16"},
              {"op":"set-frame-offset","frame":"Walk/2","x":3,"y":-2},
              {"op":"set-frame-color","frame":"Walk/2","red":255,"alpha":128,"operation":"add"},
              {"op":"set-frame-flip","frame":"Walk/0","flipH":true,"flipDiagonal":true}
            ]}
            """);

        r.Code.ShouldBe(0, r.Out + r.Err);
        var frames = Doc(file).AnimationChains.Single(c => c.Name == "Walk").Frames;
        frames[1].FrameLength.ShouldBe(0.25f, 0.0001f);
        (frames[0].LeftCoordinate, frames[0].RightCoordinate, frames[0].TopCoordinate, frames[0].BottomCoordinate).ShouldBe((8f, 24f, 4f, 20f));
        (frames[2].RelativeX, frames[2].RelativeY).ShouldBe((3f, -2f));
        (frames[2].Red, frames[2].Alpha, frames[2].ColorOperation).ShouldBe((255, 128, ColorOperation.Add));
        (frames[0].FlipHorizontal, frames[0].FlipVertical, frames[0].FlipDiagonal).ShouldBe((true, false, true));
    }

    [Fact]
    public async Task TwoOpsTouchingSameFrame_ListEachAddressOnce()
    {
        var r = await Edit("hero.achx", """
            {"ops":[
              {"op":"set-frame-flip","frame":"Walk/0","flipH":true},
              {"op":"set-frame-offset","frame":"Walk/2","x":3,"y":-2},
              {"op":"set-frame-offset","frame":"Walk/0","x":1,"y":1}
            ]}
            """, "--json");

        r.Code.ShouldBe(0, r.Out + r.Err);
        using var doc = JsonDocument.Parse(r.Out);
        var changed = doc.RootElement.GetProperty("changed").EnumerateArray().Select(e => e.GetString()).ToList();
        changed.ShouldBe(changed.Distinct().ToList());
        changed.Count(c => c!.EndsWith("Walk/0")).ShouldBe(1);
    }

    [Theory]
    [InlineData("hero.achx")]
    [InlineData("hero.achj")]
    public async Task SetUnit_ConvertsLengthsAndKeepsPlaybackTime(string file)
    {
        var before = Doc(file);
        var secondsBefore = before.AnimationChains.SelectMany(c => c.Frames).Select(f => (double)f.FrameLength).ToArray();

        var r = await Edit(file, """{"ops":[{"op":"set-unit","unit":"millisecond"}]}""");

        r.Code.ShouldBe(0, r.Out + r.Err);
        var after = Doc(file);
        after.TimeMeasurementUnit.ShouldBe(TimeMeasurementUnit.Millisecond);
        var ms = after.AnimationChains.SelectMany(c => c.Frames).Select(f => (double)f.FrameLength).ToArray();
        for (int i = 0; i < ms.Length; i++) (ms[i] / 1000.0).ShouldBe(secondsBefore[i], 0.0005);

        var back = await Edit(file, """{"ops":[{"op":"set-unit","unit":"second"}]}""");
        back.Code.ShouldBe(0, back.Out + back.Err);
        Doc(file).TimeMeasurementUnit.ShouldBe(TimeMeasurementUnit.Second);
    }

    [Fact]
    public async Task OpsApplyInOrder_LaterOpsSeeTheEarlierUnit()
    {
        var r = await Edit("hero.achx", """
            {"ops":[
              {"op":"set-unit","unit":"millisecond"},
              {"op":"set-frame-length","frame":"Walk/0","length":250}
            ]}
            """);

        r.Code.ShouldBe(0, r.Out + r.Err);
        Doc("hero.achx").AnimationChains.Single(c => c.Name == "Walk").Frames[0].FrameLength.ShouldBe(250f);
    }

    [Theory]
    [InlineData("""{"op":"set-chain-loop","chain":"Nope","value":false}""", "Nope")]
    [InlineData("""{"op":"set-frame-length","frame":"Walk/0","length":0}""", "length")]
    [InlineData("""{"op":"set-frame-length","frame":"Walk/0","length":1e30}""", "0.001 to 60 seconds")]
    [InlineData("""{"op":"set-frame-length","frame":"Walk/9","length":0.1}""", "range")]
    [InlineData("""{"op":"set-frame-region","frame":"Walk/0","region":"40,0,16,4"}""", "48x32")]
    [InlineData("""{"op":"set-frame-region","frame":"Walk/0","region":"1,2,3"}""", "region")]
    [InlineData("""{"op":"set-frame-offset","frame":"Walk/0"}""", "offset")]
    [InlineData("""{"op":"set-frame-color","frame":"Walk/0","alpha":300}""", "0 to 255")]
    [InlineData("""{"op":"set-frame-color","frame":"Walk/0"}""", "color")]
    [InlineData("""{"op":"set-frame-flip","frame":"Walk/0"}""", "flip")]
    public async Task FailingOp_WritesNothing_AndNamesTheProblem(string op, string messagePart)
    {
        var before = _w.Bytes("hero.achx");

        var r = await Edit("hero.achx", $$"""{"ops":[{"op":"set-chain-loop","chain":"Walk","value":false},{{op}}]}""", "--json");

        r.Code.ShouldBe(1, r.Out + r.Err);
        r.Out.ToLowerInvariant().ShouldContain(messagePart.ToLowerInvariant());
        using var doc = JsonDocument.Parse(r.Out);
        doc.RootElement.GetProperty("errors")[0].GetProperty("op").GetInt32().ShouldBe(1);
        _w.Bytes("hero.achx").ShouldBe(before);
    }

    [Fact]
    public async Task LockedChain_FrameOpFailsWithLockedCode()
    {
        var before = _w.Bytes("hero.achx");

        var r = await Edit("hero.achx", """
            {"ops":[
              {"op":"set-chain-locked","chain":"Walk","value":true},
              {"op":"set-frame-length","frame":"Walk/0","length":0.5}
            ]}
            """, "--json");

        r.Code.ShouldBe(1, r.Out + r.Err);
        r.Out.ShouldContain("Locked");
        _w.Bytes("hero.achx").ShouldBe(before);
    }

    [Theory]
    [InlineData("""{"ops":[{"op":"set-chain-loop","chain":"Walk","value":"yes"}]}""", "value")]
    [InlineData("""{"ops":[{"op":"set-chain-loop","chain":"Walk"}]}""", "value")]
    [InlineData("""{"ops":[{"op":"set-frame-flip","frame":"Walk/0","flipH":1}]}""", "flipH")]
    [InlineData("""{"ops":[{"op":"set-unit","unit":"minutes"}]}""", "unit")]
    [InlineData("""{"ops":[{"op":"set-frame-color","frame":"Walk/0","operation":"screen"}]}""", "operation")]
    [InlineData("""{"ops":[{"op":"set-frame-length","frame":"Walk/0","length":"long"}]}""", "length")]
    public async Task MalformedPropertyOps_AreRefusedBeforeAnythingIsApplied(string opsJson, string messagePart)
    {
        var before = _w.Bytes("hero.achx");

        var r = await Edit("hero.achx", opsJson);

        r.Code.ShouldBe(2);
        r.Err.ShouldContain(messagePart);
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
              {"op":"set-chain-loop","chain":"Walk","value":true},
              {"op":"set-chain-pingpong","chain":"Walk","value":false},
              {"op":"set-frame-length","frame":"Walk/0","length":0.1},
              {"op":"set-frame-flip","frame":"Walk/0","flipH":false},
              {"op":"set-unit","unit":"second"}
            ]}
            """);

        r.Code.ShouldBe(0, r.Out + r.Err);
        _w.Bytes(file).ShouldBe(before);
    }

    [Fact]
    public async Task Schema_ListsEveryPropertyOpWithFlagsAndEnums()
    {
        var r = await _w.RunAsync("--schema", "--json");

        r.Code.ShouldBe(0);
        using var doc = JsonDocument.Parse(r.Out);
        var ops = doc.RootElement.GetProperty("ops").EnumerateArray().ToDictionary(o => o.GetProperty("op").GetString()!);
        foreach (var name in new[]
        {
            "set-chain-loop", "set-chain-pingpong", "set-chain-reverse", "set-chain-locked", "set-frame-length",
            "set-frame-region", "set-frame-offset", "set-frame-color", "set-frame-flip", "set-unit",
        })
            ops.ContainsKey(name).ShouldBeTrue(name);
        var loop = ops["set-chain-loop"].GetProperty("fields").EnumerateArray().ToDictionary(f => f.GetProperty("name").GetString()!);
        loop["value"].GetProperty("type").GetString().ShouldBe("boolean");
        loop["value"].GetProperty("required").GetBoolean().ShouldBeTrue();
        var unit = ops["set-unit"].GetProperty("fields").EnumerateArray().Single();
        unit.GetProperty("values").EnumerateArray().Select(v => v.GetString()).ShouldBe(["second", "millisecond"]);
        var color = ops["set-frame-color"].GetProperty("fields").EnumerateArray().ToDictionary(f => f.GetProperty("name").GetString()!);
        color["operation"].GetProperty("values").EnumerateArray().Select(v => v.GetString()).ShouldBe(["multiply", "add"]);
    }
}

using System.Text.Json;
using AnimationEditor.Editing;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Cli.Tests;

public class ShapeOpsTests : IDisposable
{
    private readonly CliWorkspace _w = new("hero.achx", "hero.achj", "sheet.png");

    public void Dispose() => _w.Dispose();

    private async Task<CliOutput> Edit(string file, string opsJson, params string[] extra) =>
        await _w.RunAsync(["edit", _w.PathOf(file), "--ops", _w.Write("ops.json", opsJson), .. extra]);

    private AnimationChainListSave Doc(string file) => AnimationFileReader.Read(_w.PathOf(file)).Document!;

    private static ShapesSave Shapes(AnimationChainListSave doc, string chain, int frame) =>
        doc.AnimationChains.Single(c => c.Name == chain).Frames[frame].ShapesSave!;

    [Theory]
    [InlineData("hero.achx")]
    [InlineData("hero.achj")]
    public async Task ShapeOps_ApplyInOrder_AndSave(string file)
    {
        var r = await Edit(file, """
            {"ops":[
              {"op":"add-shape","frame":"Walk/0","type":"rect","name":"Box"},
              {"op":"set-rect","shape":"Walk/0/rect/Box","x":2,"y":3,"scaleX":5,"scaleY":7},
              {"op":"add-shape","frame":"Walk/0","type":"circle","name":"Ball"},
              {"op":"set-circle","shape":"Walk/0/circle/Ball","radius":4},
              {"op":"add-shape","frame":"Walk/0","type":"polygon","name":"Zone"},
              {"op":"set-polygon","shape":"Walk/0/polygon/Zone","x":1,"points":[[0,0],[6,0],[6,6]]},
              {"op":"set-shape-common","shape":"Walk/2/circle/Foot","x":4,"y":5},
              {"op":"duplicate-shape","shape":"Walk/2/rect/Hitbox"},
              {"op":"delete-shape","shape":"Walk/2/circle/Foot"}
            ]}
            """);

        r.Code.ShouldBe(0, r.Out + r.Err);
        var doc = Doc(file);
        var f0 = Shapes(doc, "Walk", 0);
        var box = f0.AARectSaves.Single();
        (box.Name, box.X, box.Y, box.ScaleX, box.ScaleY).ShouldBe(("Box", 2f, 3f, 5f, 7f));
        f0.CircleSaves.Single().Radius.ShouldBe(4f);
        var zone = f0.PolygonSaves.Single();
        zone.X.ShouldBe(1f);
        zone.Points.Count.ShouldBe(3);
        var f2 = Shapes(doc, "Walk", 2);
        f2.CircleSaves.ShouldBeEmpty();
        f2.AARectSaves.Count().ShouldBe(2);
    }

    [Fact]
    public async Task LaterOpsAddressTheNewName()
    {
        var r = await Edit("hero.achx", """
            {"ops":[
              {"op":"add-shape","frame":"Walk/0","type":"rect","name":"Box"},
              {"op":"rename-shape","shape":"Walk/0/rect/Box","to":"Crate"},
              {"op":"set-rect","shape":"Walk/0/rect/Crate","scaleX":9}
            ]}
            """);

        r.Code.ShouldBe(0, r.Out + r.Err);
        Shapes(Doc("hero.achx"), "Walk", 0).AARectSaves.Single().ScaleX.ShouldBe(9f);
    }

    [Theory]
    [InlineData("""{"op":"add-shape","frame":"Walk/2","type":"rect","name":"Hitbox"}""", "already exists")]
    [InlineData("""{"op":"add-shape","frame":"Walk/9","type":"rect","name":"X"}""", "range")]
    [InlineData("""{"op":"delete-shape","shape":"Walk/2/rect/Nope"}""", "Nope")]
    [InlineData("""{"op":"set-rect","shape":"Walk/2/circle/Foot","x":1}""", "rect")]
    [InlineData("""{"op":"set-rect","shape":"Walk/2/rect/Hitbox"}""", "rect")]
    [InlineData("""{"op":"set-rect","shape":"Walk/2/rect/Hitbox","scaleX":0}""", "scale")]
    [InlineData("""{"op":"set-circle","shape":"Walk/2/circle/Foot","radius":-1}""", "radius")]
    [InlineData("""{"op":"set-polygon","shape":"Walk/2/rect/Hitbox","x":1}""", "polygon")]
    [InlineData("""{"op":"duplicate-shape","shape":"Walk/2/rect"}""", "shape address")]
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
    public async Task PolygonWithTwoPoints_Fails()
    {
        var before = _w.Bytes("hero.achx");

        var r = await Edit("hero.achx", """
            {"ops":[
              {"op":"add-shape","frame":"Walk/0","type":"polygon","name":"Zone"},
              {"op":"set-polygon","shape":"Walk/0/polygon/Zone","points":[[0,0],[1,1]]}
            ]}
            """, "--json");

        r.Code.ShouldBe(1, r.Out + r.Err);
        r.Out.ShouldContain("3 points");
        _w.Bytes("hero.achx").ShouldBe(before);
    }

    [Fact]
    public async Task LockedChain_ShapeOpFailsWithLockedCode()
    {
        var before = _w.Bytes("hero.achx");

        var r = await Edit("hero.achx", """
            {"ops":[
              {"op":"set-chain-locked","chain":"Walk","value":true},
              {"op":"set-shape-common","shape":"Walk/2/rect/Hitbox","x":5}
            ]}
            """, "--json");

        r.Code.ShouldBe(1, r.Out + r.Err);
        r.Out.ShouldContain("Locked");
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
              {"op":"set-rect","shape":"Walk/2/rect/Hitbox","x":1,"y":2,"scaleX":4,"scaleY":6},
              {"op":"set-circle","shape":"Walk/2/circle/Foot","radius":3},
              {"op":"set-shape-common","shape":"Walk/2/circle/Foot","x":0,"y":-8}
            ]}
            """);

        r.Code.ShouldBe(0, r.Out + r.Err);
        _w.Bytes(file).ShouldBe(before);
    }

    [Theory]
    [InlineData("""{"ops":[{"op":"set-polygon","shape":"Walk/0/polygon/Z","points":"1,2"}]}""", "points")]
    [InlineData("""{"ops":[{"op":"set-polygon","shape":"Walk/0/polygon/Z","points":[[0,0],[1]]}]}""", "points")]
    [InlineData("""{"ops":[{"op":"set-polygon","shape":"Walk/0/polygon/Z","points":[[0,"a"],[1,1],[2,2]]}]}""", "points")]
    [InlineData("""{"ops":[{"op":"set-rect","shape":"Walk/2/rect/Hitbox","scaleX":"big"}]}""", "scaleX")]
    [InlineData("""{"ops":[{"op":"add-shape","frame":"Walk/0","type":"cube","name":"X"}]}""", "type")]
    public async Task MalformedShapeOps_AreRefusedBeforeAnythingIsApplied(string opsJson, string messagePart)
    {
        var before = _w.Bytes("hero.achx");

        var r = await Edit("hero.achx", opsJson);

        r.Code.ShouldBe(2, r.Out + r.Err);
        (r.Out + r.Err).ToLowerInvariant().ShouldContain(messagePart.ToLowerInvariant());
        _w.Bytes("hero.achx").ShouldBe(before);
    }

    [Fact]
    public async Task Schema_ListsShapeOps()
    {
        var r = await _w.RunAsync("--schema", "--json");

        r.Code.ShouldBe(0);
        using var doc = JsonDocument.Parse(r.Out);
        var ops = doc.RootElement.GetProperty("ops").EnumerateArray().ToDictionary(o => o.GetProperty("op").GetString()!);
        foreach (var name in new[] { "add-shape", "delete-shape", "duplicate-shape", "set-rect", "set-circle", "set-polygon", "set-shape-common" })
            ops.ContainsKey(name).ShouldBeTrue(name);
        var add = ops["add-shape"].GetProperty("fields").EnumerateArray().ToDictionary(f => f.GetProperty("name").GetString()!);
        add["type"].GetProperty("values").EnumerateArray().Select(v => v.GetString()).ShouldBe(["rect", "circle", "polygon"]);
        var poly = ops["set-polygon"].GetProperty("fields").EnumerateArray().ToDictionary(f => f.GetProperty("name").GetString()!);
        poly["points"].GetProperty("type").GetString().ShouldBe("points");
    }

    [Fact]
    public async Task Describe_ShowsShapeAddressesAndFullProperties()
    {
        await Edit("hero.achx", """
            {"ops":[
              {"op":"add-shape","frame":"Walk/0","type":"polygon","name":"Zone"},
              {"op":"set-polygon","shape":"Walk/0/polygon/Zone","points":[[0,0],[6,0],[6,6]]}
            ]}
            """);

        var text = await _w.RunAsync("describe", _w.PathOf("hero.achx"));
        text.Out.ShouldContain("Walk/2/rect/Hitbox");
        text.Out.ShouldContain("Walk/0/polygon/Zone");
        text.Out.ShouldContain("0,0 6,0 6,6");

        var json = await _w.RunAsync("describe", _w.PathOf("hero.achx"), "--json");
        using var doc = JsonDocument.Parse(json.Out);
        var shapes = doc.RootElement.GetProperty("chains")[0].GetProperty("frames")[2].GetProperty("shapes");
        shapes[0].GetProperty("address").GetString().ShouldBe("Walk/2/rect/Hitbox");
        var zone = doc.RootElement.GetProperty("chains")[0].GetProperty("frames")[0].GetProperty("shapes")[0];
        zone.GetProperty("address").GetString().ShouldBe("Walk/0/polygon/Zone");
        zone.GetProperty("points").GetArrayLength().ShouldBe(3);
        zone.GetProperty("points")[1][0].GetSingle().ShouldBe(6f);
    }
}

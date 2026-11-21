using System.Text.Json;
using AnimationEditor.Editing;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Cli.Tests;

public class SelectorExpansionTests
{
    private readonly AnimationChainListSave _doc = Build();

    private static AnimationChainListSave Build()
    {
        var doc = new AnimationChainListSave();
        foreach (var (name, count) in new[] { ("Walk", 8), ("WalkFast", 3), ("Idle", 2), ("Empty", 0) })
        {
            var chain = new AnimationChainSave { Name = name };
            for (int i = 0; i < count; i++) chain.Frames.Add(new AnimationFrameSave());
            doc.AnimationChains.Add(chain);
        }
        var other = new AnimationFrameSave { ShapesSave = new ShapesSave() };
        other.ShapesSave.Add(new AARectSave { Name = "Hitbox" });
        doc.AnimationChains[2].Frames[0] = other;
        var shaped = new AnimationFrameSave { ShapesSave = new ShapesSave() };
        shaped.ShapesSave.Add(new AARectSave { Name = "Hitbox" });
        shaped.ShapesSave.Add(new CircleSave { Name = "Hitbox" });
        doc.AnimationChains[2].Frames[1] = shaped;
        return doc;
    }

    [Theory]
    [InlineData("Walk/*", "Walk/0,Walk/1,Walk/2,Walk/3,Walk/4,Walk/5,Walk/6,Walk/7")]
    [InlineData("Walk/2-5", "Walk/2,Walk/3,Walk/4,Walk/5")]
    [InlineData("Walk/0,3,7", "Walk/0,Walk/3,Walk/7")]
    [InlineData("Walk/7,0", "Walk/7,Walk/0")]
    [InlineData("Walk/0,2-3,6", "Walk/0,Walk/2,Walk/3,Walk/6")]
    [InlineData("Walk/1,1,0-1", "Walk/1,Walk/0")]
    [InlineData("Walk/4-4", "Walk/4")]
    [InlineData("Walk*/0-2", "Walk/0,Walk/1,Walk/2,WalkFast/0,WalkFast/1,WalkFast/2")]
    [InlineData("Idle/*", "Idle/0,Idle/1")]
    public void Frames_Expand(string address, string expected)
    {
        var r = EditSelectors.ExpandFrames(_doc, address);

        r.Error.ShouldBeNull();
        string.Join(",", r.Targets!).ShouldBe(expected);
    }

    [Theory]
    [InlineData("Walk*", "Walk,WalkFast")]
    [InlineData("*", "Walk,WalkFast,Idle,Empty")]
    [InlineData("W?lk", "Walk")]
    [InlineData("*Fast", "WalkFast")]
    [InlineData("Idle", "Idle")]
    public void Chains_Expand(string pattern, string expected)
    {
        var r = EditSelectors.ExpandChains(_doc, pattern);

        r.Error.ShouldBeNull();
        string.Join(",", r.Targets!).ShouldBe(expected);
    }

    [Theory]
    [InlineData("Nope*")]
    [InlineData("Z*")]
    public void Chains_MatchingNothing_IsNoMatchError(string pattern)
    {
        var r = EditSelectors.ExpandChains(_doc, pattern);

        r.Ok.ShouldBeFalse();
        r.Code.ShouldBe(EditErrorCode.NoMatch);
        r.Error!.ShouldContain(pattern);
    }

    [Theory]
    [InlineData("Empty/*")]
    [InlineData("Nope*/*")]
    public void Frames_MatchingNothing_IsNoMatchError(string address)
    {
        var r = EditSelectors.ExpandFrames(_doc, address);

        r.Ok.ShouldBeFalse();
        r.Code.ShouldBe(EditErrorCode.NoMatch);
    }

    [Theory]
    [InlineData("Walk/8")]
    [InlineData("Walk/0,9")]
    [InlineData("Walk/5-8")]
    [InlineData("Walk*/0-3")]
    public void Frames_OutOfRange_IsIndexError(string address)
    {
        var r = EditSelectors.ExpandFrames(_doc, address);

        r.Ok.ShouldBeFalse();
        r.Code.ShouldBe(EditErrorCode.IndexOutOfRange);
    }

    [Theory]
    [InlineData("Walk/3-1")]
    [InlineData("Walk/a")]
    [InlineData("Walk/1,,2")]
    [InlineData("Walk/-1")]
    [InlineData("Walk/1-")]
    [InlineData("Walk/")]
    [InlineData("Walk")]
    [InlineData("Walk/1/2")]
    public void Frames_BadSyntax_IsError(string address)
    {
        var r = EditSelectors.ExpandFrames(_doc, address);

        r.Ok.ShouldBeFalse();
        r.Error.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void Shapes_ExpandAcrossFrames()
    {
        var r = EditSelectors.ExpandShapes(_doc, "Idle/*/rect/Hitbox");

        r.Error.ShouldBeNull();
        string.Join(",", r.Targets!).ShouldBe("Idle/0/rect/Hitbox,Idle/1/rect/Hitbox");
    }

    [Fact]
    public void Shapes_SkipFramesWithoutTheShape()
    {
        var r = EditSelectors.ExpandShapes(_doc, "Idle/0-1/circle/Hitbox");

        string.Join(",", r.Targets!).ShouldBe("Idle/1/circle/Hitbox");
    }

    [Fact]
    public void Shapes_NameWildcard_MatchesEveryShapeOfThatType()
    {
        var r = EditSelectors.ExpandShapes(_doc, "Idle/0-1/circle/*");

        string.Join(",", r.Targets!).ShouldBe("Idle/1/circle/Hitbox");
    }

    [Fact]
    public void Shapes_MatchingNothing_IsNoMatchError()
    {
        var r = EditSelectors.ExpandShapes(_doc, "Idle/*/polygon/Hitbox");

        r.Ok.ShouldBeFalse();
        r.Code.ShouldBe(EditErrorCode.NoMatch);
    }
}

public class BulkOpsTests : IDisposable
{
    private readonly CliWorkspace _w = new("hero.achx", "hero.achj", "sheet.png", "other.png");

    public void Dispose() => _w.Dispose();

    private async Task<CliOutput> Edit(string file, string opsJson, params string[] extra) =>
        await _w.RunAsync(["edit", _w.PathOf(file), "--ops", _w.Write("ops.json", opsJson), .. extra]);

    private AnimationChainListSave Doc(string file) => AnimationFileReader.Read(_w.PathOf(file)).Document!;

    private const string Grid6 = """{"op":"add-frames-grid","chain":"Idle","texture":"sheet.png","cellWidth":16,"cellHeight":16,"columns":3,"rows":2}""";

    [Theory]
    [InlineData("hero.achx")]
    [InlineData("hero.achj")]
    public async Task SetFrameLength_Wildcard_AppliesToEveryFrame(string file)
    {
        var r = await Edit(file, """{"ops":[{"op":"set-frame-length","frame":"Walk/*","length":0.3}]}""");

        r.Code.ShouldBe(0, r.Out + r.Err);
        Doc(file).AnimationChains.Single(c => c.Name == "Walk").Frames.ShouldAllBe(f => Math.Abs(f.FrameLength - 0.3f) < 1e-5);
    }

    [Fact]
    public async Task SetFrameLength_ListAndRange_TouchOnlyThoseFrames()
    {
        var r = await Edit("hero.achx", """
            {"ops":[{"op":"set-frame-length","frame":"Walk/0,2","length":0.4},{"op":"set-frame-offset","frame":"Walk/1-2","x":5}]}
            """);

        r.Code.ShouldBe(0, r.Out + r.Err);
        var walk = Doc("hero.achx").AnimationChains.Single(c => c.Name == "Walk").Frames;
        walk.Select(f => f.FrameLength).ToArray().ShouldBe([0.4f, 0.1f, 0.4f]);
        walk.Select(f => f.RelativeX).ToArray().ShouldBe([0f, 5f, 5f]);
    }

    [Fact]
    public async Task ChainGlob_SetsFlagOnEveryMatch()
    {
        var r = await Edit("hero.achx", """{"ops":[{"op":"set-chain-loop","chain":"*","value":false}]}""");

        r.Code.ShouldBe(0, r.Out + r.Err);
        Doc("hero.achx").AnimationChains.ShouldAllBe(c => !c.Loop);
    }

    [Fact]
    public async Task DeleteFrame_Range_RemovesTheRightFrames()
    {
        var r = await Edit("hero.achx", "{\"ops\":[" + Grid6 + ",{\"op\":\"delete-frame\",\"frame\":\"Idle/1-3\"}]}");

        r.Code.ShouldBe(0, r.Out + r.Err);
        var idle = Doc("hero.achx").AnimationChains.Single(c => c.Name == "Idle").Frames;
        // Frame 0 is the original, grid cells 0..5 sit at indexes 1..6; removing 1-3 leaves the original and cells 3,4,5.
        idle.Count.ShouldBe(4);
        idle.Skip(1).Select(f => f.LeftCoordinate).ToArray().ShouldBe([0f, 16f, 32f]);
        idle.Skip(1).Select(f => f.TopCoordinate).ToArray().ShouldBe([16f, 16f, 16f]);
    }

    [Fact]
    public async Task DuplicateFrame_Range_DuplicatesEachOnce()
    {
        var r = await Edit("hero.achx", """{"ops":[{"op":"duplicate-frame","frame":"Walk/0-1"}]}""");

        r.Code.ShouldBe(0, r.Out + r.Err);
        Doc("hero.achx").AnimationChains.Single(c => c.Name == "Walk").Frames.Count.ShouldBe(5);
    }

    [Fact]
    public async Task ShapeOps_FrameSelector_AddSetDelete()
    {
        var r = await Edit("hero.achx", """
            {"ops":[
              {"op":"add-shape","frame":"Walk/0-1","type":"rect","name":"Box"},
              {"op":"set-rect","shape":"Walk/*/rect/Box","scaleX":9},
              {"op":"set-shape-common","shape":"Walk/0-1/rect/Box","x":3}
            ]}
            """);

        r.Code.ShouldBe(0, r.Out + r.Err);
        var walk = Doc("hero.achx").AnimationChains.Single(c => c.Name == "Walk").Frames;
        foreach (var f in walk.Take(2))
        {
            var box = f.ShapesSave!.Shapes.OfType<AARectSave>().Single(s => s.Name == "Box");
            box.ScaleX.ShouldBe(9f);
            box.X.ShouldBe(3f);
        }
        walk[2].ShapesSave!.Shapes.OfType<AARectSave>().Any(s => s.Name == "Box").ShouldBeFalse();

        var d = await Edit("hero.achx", """{"ops":[{"op":"delete-shape","shape":"Walk/*/rect/Box"}]}""");
        d.Code.ShouldBe(0, d.Out + d.Err);
        Doc("hero.achx").AnimationChains.Single(c => c.Name == "Walk").Frames
            .Any(f => f.ShapesSave?.Shapes.OfType<AARectSave>().Any(s => s.Name == "Box") == true).ShouldBeFalse();
    }

    [Fact]
    public async Task FrameEvents_Range_AddsToEach()
    {
        var r = await Edit("hero.achx", """{"ops":[{"op":"add-frame-event","frame":"Walk/1-2","name":"Step"}]}""");

        r.Code.ShouldBe(0, r.Out + r.Err);
        var walk = Doc("hero.achx").AnimationChains.Single(c => c.Name == "Walk").Frames;
        walk.Select(f => f.Events.Count).ToArray().ShouldBe([0, 1, 1]);
    }

    [Fact]
    public async Task SetFrameTexture_Wildcard_RepointsEveryFrame()
    {
        var r = await Edit("hero.achx", """{"ops":[{"op":"set-frame-texture","frame":"Walk/*","texture":"other.png"}]}""");

        r.Code.ShouldBe(0, r.Out + r.Err);
        Doc("hero.achx").AnimationChains.Single(c => c.Name == "Walk").Frames.ShouldAllBe(f => f.TextureName == "other.png");
    }

    [Fact]
    public async Task OpsApplyInOrder_LaterSelectorSeesEarlierChanges()
    {
        var r = await Edit("hero.achx", "{\"ops\":[" + Grid6 + ",{\"op\":\"set-frame-length\",\"frame\":\"Idle/*\",\"length\":0.05}]}");

        r.Code.ShouldBe(0, r.Out + r.Err);
        var idle = Doc("hero.achx").AnimationChains.Single(c => c.Name == "Idle").Frames;
        idle.Count.ShouldBe(7);
        idle.ShouldAllBe(f => Math.Abs(f.FrameLength - 0.05f) < 1e-5);
    }

    [Fact]
    public async Task SelectorMatchingNothing_FailsWithNoMatch_AndWritesNothing()
    {
        var before = _w.Bytes("hero.achx");

        var r = await Edit("hero.achx", """
            {"ops":[{"op":"set-frame-length","frame":"Walk/0","length":0.9},{"op":"set-chain-loop","chain":"Nope*","value":false}]}
            """, "--json");

        r.Code.ShouldBe(1);
        using var doc = JsonDocument.Parse(r.Out);
        doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString().ShouldBe("NoMatch");
        doc.RootElement.GetProperty("errors")[0].GetProperty("op").GetInt32().ShouldBe(1);
        _w.Bytes("hero.achx").ShouldBe(before);
    }

    [Fact]
    public async Task OutOfRangeSelector_FailsAndWritesNothing()
    {
        var before = _w.Bytes("hero.achx");

        var r = await Edit("hero.achx", """{"ops":[{"op":"set-frame-length","frame":"Walk/0-3","length":0.9}]}""");

        r.Code.ShouldBe(1);
        r.Out.ShouldContain("out of range");
        _w.Bytes("hero.achx").ShouldBe(before);
    }

    [Fact]
    public async Task FailureOnOneTarget_WritesNothing_AndNamesTheTarget()
    {
        var before = _w.Bytes("hero.achx");

        // Walk is locked mid-batch: the first op already changed Idle in memory, nothing may reach disk.
        var r = await Edit("hero.achx", """
            {"ops":[{"op":"set-frame-length","frame":"Idle/0","length":0.9},{"op":"set-chain-locked","chain":"Walk","value":true},{"op":"set-frame-length","frame":"*/0","length":0.5}]}
            """);

        r.Code.ShouldBe(1);
        r.Out.ShouldContain("Walk/0");
        _w.Bytes("hero.achx").ShouldBe(before);
    }

    [Theory]
    [InlineData("hero.achx")]
    [InlineData("hero.achj")]
    public async Task NoChangeBatch_LeavesFileByteIdentical(string file)
    {
        var before = _w.Bytes(file);

        var r = await Edit(file, """
            {"ops":[{"op":"set-frame-length","frame":"Walk/0-1","length":0.1},{"op":"set-chain-loop","chain":"*","value":true}]}
            """);

        r.Code.ShouldBe(0, r.Out + r.Err);
        _w.Bytes(file).ShouldBe(before);
    }

    [Fact]
    public async Task ReorderFrame_RejectsSelectors()
    {
        var r = await Edit("hero.achx", """{"ops":[{"op":"reorder-frame","frame":"Walk/0-1","index":2}]}""");

        r.Code.ShouldBe(1);
    }
}

public class AddFramesGridTests : IDisposable
{
    private readonly CliWorkspace _w = new("hero.achx", "hero.achj", "sheet.png", "other.png");

    public void Dispose() => _w.Dispose();

    private async Task<CliOutput> Edit(string file, string opsJson, params string[] extra) =>
        await _w.RunAsync(["edit", _w.PathOf(file), "--ops", _w.Write("ops.json", opsJson), .. extra]);

    private AnimationChainListSave Doc(string file) => AnimationFileReader.Read(_w.PathOf(file)).Document!;

    [Theory]
    [InlineData("hero.achx")]
    [InlineData("hero.achj")]
    public async Task AddsEveryCellRowMajor(string file)
    {
        var r = await Edit(file, """
            {"ops":[{"op":"add-chain","name":"Sheet"},{"op":"add-frames-grid","chain":"Sheet","texture":"sheet.png","cellWidth":16,"cellHeight":16,"columns":3,"rows":2,"frameLength":0.2}]}
            """);

        r.Code.ShouldBe(0, r.Out + r.Err);
        var frames = Doc(file).AnimationChains.Single(c => c.Name == "Sheet").Frames;
        frames.Count.ShouldBe(6);
        frames.Select(f => (f.LeftCoordinate, f.TopCoordinate, f.RightCoordinate, f.BottomCoordinate)).ToArray().ShouldBe(
        [
            (0f, 0f, 16f, 16f), (16f, 0f, 32f, 16f), (32f, 0f, 48f, 16f),
            (0f, 16f, 16f, 32f), (16f, 16f, 32f, 32f), (32f, 16f, 48f, 32f),
        ]);
        frames.ShouldAllBe(f => f.TextureName == "sheet.png" && Math.Abs(f.FrameLength - 0.2f) < 1e-5);
    }

    [Fact]
    public async Task StartAndCount_PickASubRange()
    {
        var r = await Edit("hero.achx", """
            {"ops":[{"op":"add-chain","name":"Sheet"},{"op":"add-frames-grid","chain":"Sheet","texture":"sheet.png","cellWidth":16,"cellHeight":16,"columns":3,"rows":2,"start":2,"count":3}]}
            """);

        r.Code.ShouldBe(0, r.Out + r.Err);
        var frames = Doc("hero.achx").AnimationChains.Single(c => c.Name == "Sheet").Frames;
        frames.Select(f => (f.LeftCoordinate, f.TopCoordinate)).ToArray().ShouldBe([(32f, 0f), (0f, 16f), (16f, 16f)]);
    }

    [Theory]
    [InlineData("\"cellWidth\":16,\"cellHeight\":16,\"columns\":4,\"rows\":2")]
    [InlineData("\"cellWidth\":16,\"cellHeight\":16,\"columns\":3,\"rows\":3")]
    [InlineData("\"cellWidth\":0,\"cellHeight\":16,\"columns\":3,\"rows\":2")]
    [InlineData("\"cellWidth\":16,\"cellHeight\":16,\"columns\":3,\"rows\":2,\"start\":6")]
    [InlineData("\"cellWidth\":16,\"cellHeight\":16,\"columns\":3,\"rows\":2,\"start\":4,\"count\":3")]
    [InlineData("\"cellWidth\":16,\"cellHeight\":16,\"columns\":3,\"rows\":2,\"count\":0")]
    public async Task BadGrid_FailsAndWritesNothing(string grid)
    {
        var before = _w.Bytes("hero.achx");

        var r = await Edit("hero.achx", "{\"ops\":[{\"op\":\"add-frames-grid\",\"chain\":\"Idle\",\"texture\":\"sheet.png\"," + grid + "}]}");

        r.Code.ShouldBe(1);
        _w.Bytes("hero.achx").ShouldBe(before);
    }

    [Fact]
    public async Task MissingTexture_Fails()
    {
        var r = await Edit("hero.achx", """
            {"ops":[{"op":"add-frames-grid","chain":"Idle","texture":"nope.png","cellWidth":16,"cellHeight":16,"columns":1,"rows":1}]}
            """);

        r.Code.ShouldBe(1);
    }

    [Fact]
    public async Task LockedChain_Fails()
    {
        var r = await Edit("hero.achx", """
            {"ops":[{"op":"set-chain-locked","chain":"Idle","value":true},{"op":"add-frames-grid","chain":"Idle","texture":"sheet.png","cellWidth":16,"cellHeight":16,"columns":1,"rows":1}]}
            """);

        r.Code.ShouldBe(1);
        r.Out.ShouldContain("Locked");
    }
}

public class SchemaSelectorDocsTests
{
    [Fact]
    public async Task SchemaText_DocumentsSelectorSyntaxAndGridOp()
    {
        using var w = new CliWorkspace();

        var r = await w.RunAsync("--schema");

        r.Out.ShouldContain("add-frames-grid");
        r.Out.ShouldContain("Walk/2-5");
        r.Out.ShouldContain("Walk/0,3,7");
        r.Out.ShouldContain("Walk*");
    }

    [Fact]
    public async Task SchemaJson_HasSelectorsSection()
    {
        using var w = new CliWorkspace();

        var r = await w.RunAsync("--schema", "--json");

        using var doc = JsonDocument.Parse(r.Out);
        doc.RootElement.GetProperty("selectors").GetString()!.ShouldContain("Walk/2-5");
    }
}

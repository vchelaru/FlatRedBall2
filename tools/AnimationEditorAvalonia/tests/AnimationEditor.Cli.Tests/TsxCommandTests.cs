using System.Text.Json;
using Shouldly;
using Xunit;

namespace AnimationEditor.Cli.Tests;

public class TsxCommandTests : IDisposable
{
    private readonly CliWorkspace _w = new("tiles.tsx", "collection.tsx", "hero.achx", "sheet.png", "other.png");

    public void Dispose() => _w.Dispose();

    private async Task<CliOutput> Edit(string file, string opsJson, params string[] extra) =>
        await _w.RunAsync(["edit", _w.PathOf(file), "--ops", _w.Write("ops.json", opsJson), .. extra]);

    private async Task<JsonElement> Describe(string file)
    {
        var r = await _w.RunAsync("describe", _w.PathOf(file), "--json");
        r.Code.ShouldBe(0, r.Out + r.Err);
        return JsonDocument.Parse(r.Out).RootElement.Clone();
    }

    private string Raw(string file) => File.ReadAllText(_w.PathOf(file));

    private static string ErrorCode(CliOutput r) =>
        JsonDocument.Parse(r.Out).RootElement.GetProperty("errors")[0].GetProperty("code").GetString()!;

    // ---- describe / check ----

    [Fact]
    public async Task Describe_Tsx_ListsChainsInPixelsWithOwnerTiles()
    {
        var root = await Describe("tiles.tsx");

        root.GetProperty("coordinateType").GetString().ShouldBe("Pixel");
        var chains = root.GetProperty("chains");
        chains[0].GetProperty("name").GetString().ShouldBe("Walk");
        chains[0].GetProperty("ownerTile").GetInt32().ShouldBe(0);
        chains[1].GetProperty("name").GetString().ShouldBe("ID:5");
        chains[1].GetProperty("ownerTile").GetInt32().ShouldBe(5);
        var frames = chains[0].GetProperty("frames");
        frames.GetArrayLength().ShouldBe(3);
        frames[1].GetProperty("texture").GetString().ShouldBe("sheet.png");
        frames[1].GetProperty("length").GetDouble().ShouldBe(0.1, 0.0001);
        frames[1].GetProperty("region").GetProperty("x").GetDouble().ShouldBe(16);
        frames[1].GetProperty("region").GetProperty("width").GetDouble().ShouldBe(16);
        frames[2].GetProperty("length").GetDouble().ShouldBe(0.2, 0.0001);
    }

    [Fact]
    public async Task Describe_Tsx_TextShowsOwnerTile()
    {
        var r = await _w.RunAsync("describe", _w.PathOf("tiles.tsx"));

        r.Code.ShouldBe(0, r.Err);
        r.Out.ShouldContain("Walk");
        r.Out.ShouldContain("owner tile 0");
    }

    [Fact]
    public async Task Check_Tsx_IsCleanWhenTheImageIsThere()
    {
        var r = await _w.RunAsync("check", _w.PathOf("tiles.tsx"));

        r.Code.ShouldBe(0, r.Out + r.Err);
    }

    [Fact]
    public async Task Check_Tsx_MissingImage_IsAnError()
    {
        File.Delete(_w.PathOf("sheet.png"));

        var r = await _w.RunAsync("check", _w.PathOf("tiles.tsx"));

        r.Code.ShouldBe(1);
        r.Out.ShouldContain("missing-texture");
    }

    [Theory]
    [InlineData("describe")]
    [InlineData("check")]
    public async Task UnsupportedTsx_SaysNoUpFrontWithTheCheckersMessage(string command)
    {
        var r = await _w.RunAsync(command, _w.PathOf("collection.tsx"));

        r.Code.ShouldBe(1);
        r.Err.ShouldContain("image collection");
    }

    [Fact]
    public async Task Describe_ReadOnly_NeverTouchesTheFile()
    {
        var before = _w.Bytes("tiles.tsx");

        await _w.RunAsync("describe", _w.PathOf("tiles.tsx"));
        await _w.RunAsync("check", _w.PathOf("tiles.tsx"));

        _w.Bytes("tiles.tsx").ShouldBe(before);
    }

    // ---- edit: supported ops keep foreign content ----

    [Fact]
    public async Task Edit_RenameChainAndFrameOps_SaveAndKeepForeignContent()
    {
        var r = await Edit("tiles.tsx", """
            {"ops":[
              {"op":"rename-chain","chain":"Walk","to":"Run"},
              {"op":"set-frame-length","frame":"Run/0","length":0.25},
              {"op":"delete-frame","frame":"Run/2"},
              {"op":"add-frame","chain":"Run","texture":"sheet.png","region":"0,16,16,16","length":0.3}
            ]}
            """);

        r.Code.ShouldBe(0, r.Out + r.Err);
        var chain = (await Describe("tiles.tsx")).GetProperty("chains")[0];
        chain.GetProperty("name").GetString().ShouldBe("Run");
        var frames = chain.GetProperty("frames");
        frames.GetArrayLength().ShouldBe(3);
        frames[0].GetProperty("length").GetDouble().ShouldBe(0.25, 0.0001);
        frames[2].GetProperty("region").GetProperty("y").GetDouble().ShouldBe(16);

        var raw = Raw("tiles.tsx");
        raw.ShouldContain("name=\"Name\" value=\"Run\"");
        raw.ShouldContain("type=\"class\" propertytype=\"Stats\"");
        raw.ShouldContain("name=\"biome\" propertytype=\"Biome\" value=\"Forest\"");
        raw.ShouldContain("<wangset name=\"CollisionSet\" type=\"mixed\"");
        raw.ShouldContain("<wangtile tileid=\"3\"");
        raw.ShouldContain("<objectgroup");
        raw.ShouldContain("<transformations hflip=\"1\" vflip=\"0\" rotate=\"1\" preferuntransformed=\"0\"");
        raw.ShouldContain("<tile id=\"3\" type=\"SolidCollision\"");
        raw.ShouldContain("<tile id=\"4\" type=\"Water\"");
        raw.ShouldContain("duration=\"250\"");
    }

    [Fact]
    public async Task Edit_NoOp_LeavesTheFileByteIdentical()
    {
        var before = _w.Bytes("tiles.tsx");

        var r = await Edit("tiles.tsx", """{"ops":[{"op":"set-frame-length","frame":"Walk/0","length":0.1}]}""");

        r.Code.ShouldBe(0, r.Out + r.Err);
        _w.Bytes("tiles.tsx").ShouldBe(before);
    }

    [Fact]
    public async Task Edit_AddChainReorderAndDuplicate_Work()
    {
        var r = await Edit("tiles.tsx", """
            {"ops":[
              {"op":"add-chain","name":"Jump"},
              {"op":"add-frame","chain":"Jump","texture":"sheet.png","region":"16,16,16,16"},
              {"op":"reorder-chain","chain":"Jump","index":0}
            ]}
            """);

        r.Code.ShouldBe(0, r.Out + r.Err);
        var chains = (await Describe("tiles.tsx")).GetProperty("chains");
        chains.EnumerateArray().Select(c => c.GetProperty("name").GetString()).ShouldContain("Jump");
    }

    [Fact]
    public async Task Edit_SetFrameRegion_OnTheTilesSheet_Works()
    {
        var r = await Edit("tiles.tsx", """{"ops":[{"op":"set-frame-region","frame":"Walk/0","region":"32,16,16,16"}]}""");

        r.Code.ShouldBe(0, r.Out + r.Err);
        var left = (await Describe("tiles.tsx")).GetProperty("chains")[0].GetProperty("frames")[0].GetProperty("region").GetProperty("x");
        left.GetDouble().ShouldBe(32);
    }

    [Fact]
    public async Task Edit_AddFrameWithoutRegion_IsRefusedBecauseATileIsNotTheWholeSheet()
    {
        var before = _w.Bytes("tiles.tsx");

        var r = await Edit("tiles.tsx", """{"ops":[{"op":"add-frame","chain":"Walk","texture":"sheet.png"}]}""", "--json");

        r.Code.ShouldBe(1);
        ErrorCode(r).ShouldBe("NotSupportedInTsx");
        _w.Bytes("tiles.tsx").ShouldBe(before);
    }

    [Fact]
    public async Task Edit_AddFrameOfAnotherTexture_IsRefusedBecauseATilesetHasOneImage()
    {
        var before = _w.Bytes("tiles.tsx");

        var r = await Edit("tiles.tsx", """{"ops":[{"op":"add-frame","chain":"Walk","texture":"other.png","region":"0,0,16,16"}]}""", "--json");

        r.Code.ShouldBe(1);
        ErrorCode(r).ShouldBe("NotSupportedInTsx");
        _w.Bytes("tiles.tsx").ShouldBe(before);
    }

    // ---- edit: refused ops ----

    [Theory]
    [InlineData("""{"op":"set-chain-loop","chain":"Walk","value":false}""")]
    [InlineData("""{"op":"set-chain-pingpong","chain":"Walk","value":true}""")]
    [InlineData("""{"op":"set-chain-locked","chain":"Walk","value":true}""")]
    [InlineData("""{"op":"set-frame-flip","frame":"Walk/0","flipH":true}""")]
    [InlineData("""{"op":"set-frame-offset","frame":"Walk/0","x":1}""")]
    [InlineData("""{"op":"set-frame-color","frame":"Walk/0","red":10}""")]
    [InlineData("""{"op":"add-shape","frame":"Walk/0","type":"rect"}""")]
    [InlineData("""{"op":"add-frame-event","frame":"Walk/0","name":"Hit"}""")]
    [InlineData("""{"op":"set-frame-keyframe","frame":"Walk/1","group":"offset","value":false}""")]
    [InlineData("""{"op":"set-frame-texture","frame":"Walk/0","texture":"other.png"}""")]
    [InlineData("""{"op":"set-unit","unit":"millisecond"}""")]
    public async Task Edit_OpsATsxCannotStore_AreRefusedWithTheTiledReason(string op)
    {
        var before = _w.Bytes("tiles.tsx");

        var r = await Edit("tiles.tsx", "{\"ops\":[" + op + "]}", "--json");

        r.Code.ShouldBe(1, r.Out);
        ErrorCode(r).ShouldBe("NotSupportedInTsx");
        r.Out.ShouldContain("Tiled");
        _w.Bytes("tiles.tsx").ShouldBe(before);
    }

    [Fact]
    public async Task Edit_RefusedOpLate_WritesNothing()
    {
        var before = _w.Bytes("tiles.tsx");

        var r = await Edit("tiles.tsx", """
            {"ops":[
              {"op":"rename-chain","chain":"Walk","to":"Run"},
              {"op":"set-frame-flip","frame":"Run/0","flipH":true}
            ]}
            """);

        r.Code.ShouldBe(1);
        _w.Bytes("tiles.tsx").ShouldBe(before);
    }

    [Fact]
    public async Task Edit_DuplicatedChain_SharesTheOwnerTile_AndIsRefusedUntilItGetsItsOwn()
    {
        var before = _w.Bytes("tiles.tsx");

        var r = await Edit("tiles.tsx", """{"ops":[{"op":"duplicate-chain","chain":"Walk"}]}""", "--json");

        r.Code.ShouldBe(1);
        ErrorCode(r).ShouldBe("DuplicateOwnerTile");
        _w.Bytes("tiles.tsx").ShouldBe(before);
    }

    [Fact]
    public async Task Edit_DuplicatedChain_WithItsOwnOwnerTile_Saves()
    {
        var r = await Edit("tiles.tsx", """
            {"ops":[
              {"op":"duplicate-chain","chain":"Walk","name":"Walk2"},
              {"op":"set-chain-owner-tile","chain":"Walk2","tile":1}
            ]}
            """);

        r.Code.ShouldBe(0, r.Out + r.Err);
        var chains = (await Describe("tiles.tsx")).GetProperty("chains");
        chains.EnumerateArray().Single(c => c.GetProperty("name").GetString() == "Walk2").GetProperty("ownerTile").GetInt32().ShouldBe(1);
    }

    // ---- set-chain-owner-tile ----

    [Fact]
    public async Task SetChainOwnerTile_MovesTheAnimationToAnotherTile()
    {
        var r = await Edit("tiles.tsx", """{"ops":[{"op":"set-chain-owner-tile","chain":"Walk","tile":2}]}""");

        r.Code.ShouldBe(0, r.Out + r.Err);
        (await Describe("tiles.tsx")).GetProperty("chains")[0].GetProperty("ownerTile").GetInt32().ShouldBe(2);
        Raw("tiles.tsx").ShouldContain("<wangset name=\"CollisionSet\"");
    }

    [Fact]
    public async Task SetChainOwnerTile_Clear_ReturnsToTheFirstFramesTile()
    {
        await Edit("tiles.tsx", """{"ops":[{"op":"set-chain-owner-tile","chain":"Walk","tile":2}]}""");

        var r = await Edit("tiles.tsx", """{"ops":[{"op":"set-chain-owner-tile","chain":"Walk","clear":true}]}""");

        r.Code.ShouldBe(0, r.Out + r.Err);
        (await Describe("tiles.tsx")).GetProperty("chains")[0].GetProperty("ownerTile").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task SetChainOwnerTile_NeedsExactlyOneOfTileAndClear()
    {
        var neither = await Edit("tiles.tsx", """{"ops":[{"op":"set-chain-owner-tile","chain":"Walk"}]}""", "--json");
        var both = await Edit("tiles.tsx", """{"ops":[{"op":"set-chain-owner-tile","chain":"Walk","tile":1,"clear":true}]}""", "--json");

        neither.Code.ShouldBe(1);
        both.Code.ShouldBe(1);
    }

    [Fact]
    public async Task SetChainOwnerTile_TileOfAnotherChain_FailsAndWritesNothing()
    {
        var before = _w.Bytes("tiles.tsx");

        var r = await Edit("tiles.tsx", """{"ops":[{"op":"set-chain-owner-tile","chain":"Walk","tile":5}]}""", "--json");

        r.Code.ShouldBe(1);
        ErrorCode(r).ShouldBe("InvalidValue");
        _w.Bytes("tiles.tsx").ShouldBe(before);
    }

    [Fact]
    public async Task SetChainOwnerTile_OnAnAchx_IsRefused()
    {
        var r = await Edit("hero.achx", """{"ops":[{"op":"set-chain-owner-tile","chain":"Walk","tile":1}]}""", "--json");

        r.Code.ShouldBe(1);
        ErrorCode(r).ShouldBe("WrongFileKind");
    }

    // ---- associate-tileset ----

    [Fact]
    public async Task AssociateTileset_WritesTheCompanionFile_AndTheSaveReportsTheSync()
    {
        var r = await Edit("hero.achx", """{"ops":[{"op":"associate-tileset","path":"tiles.tsx"}]}""", "--json");

        r.Code.ShouldBe(0, r.Out + r.Err);
        Directory.GetFiles(_w.Dir, "*.tiledsync").Length.ShouldBe(1);
        JsonDocument.Parse(r.Out).RootElement.GetProperty("changed").EnumerateArray()
            .Select(e => e.GetString()).ShouldContain(s => s!.EndsWith("tiles.tsx"));
    }

    [Fact]
    public async Task AssociateTileset_MissingTsx_FailsAndWritesNothing()
    {
        var before = _w.Bytes("hero.achx");

        var r = await Edit("hero.achx", """{"ops":[{"op":"associate-tileset","path":"nope.tsx"}]}""", "--json");

        r.Code.ShouldBe(1);
        ErrorCode(r).ShouldBe("NotFound");
        Directory.GetFiles(_w.Dir, "*.tiledsync").ShouldBeEmpty();
        _w.Bytes("hero.achx").ShouldBe(before);
    }

    [Fact]
    public async Task AssociateTileset_LaterOpFails_WritesNothing()
    {
        var r = await Edit("hero.achx", """
            {"ops":[
              {"op":"associate-tileset","path":"tiles.tsx"},
              {"op":"rename-chain","chain":"Nope","to":"X"}
            ]}
            """);

        r.Code.ShouldBe(1);
        Directory.GetFiles(_w.Dir, "*.tiledsync").ShouldBeEmpty();
    }

    [Fact]
    public async Task AssociateTileset_ThenDescribingThatTsx_IsRefusedUpFront()
    {
        await Edit("hero.achx", """{"ops":[{"op":"associate-tileset","path":"tiles.tsx"}]}""");

        var r = await _w.RunAsync("describe", _w.PathOf("tiles.tsx"));

        r.Code.ShouldBe(1);
        r.Err.ShouldContain("associated");
    }

    [Fact]
    public async Task AssociateTileset_InATsx_IsRefused()
    {
        var r = await Edit("tiles.tsx", """{"ops":[{"op":"associate-tileset","path":"tiles.tsx"}]}""", "--json");

        r.Code.ShouldBe(1);
        ErrorCode(r).ShouldBe("WrongFileKind");
    }

    // ---- other commands ----

    [Fact]
    public async Task New_Tsx_IsRefused()
    {
        var r = await _w.RunAsync("new", _w.PathOf("fresh.tsx"));

        r.Code.ShouldBe(1);
        File.Exists(_w.PathOf("fresh.tsx")).ShouldBeFalse();
    }

    [Fact]
    public async Task Convert_Tsx_IsRefused()
    {
        var r = await _w.RunAsync("convert", _w.PathOf("tiles.tsx"), "--to", "achj");

        r.Code.ShouldBe(1);
    }

    [Fact]
    public async Task Schema_ListsTheTsxOps()
    {
        var r = await _w.RunAsync("--schema", "--json");

        using var doc = JsonDocument.Parse(r.Out);
        var ops = doc.RootElement.GetProperty("ops").EnumerateArray().Select(o => o.GetProperty("op").GetString()).ToList();
        ops.ShouldContain("associate-tileset");
        ops.ShouldContain("set-chain-owner-tile");
        doc.RootElement.GetProperty("tsx").GetString()!.ShouldContain("rename-chain");
    }
}

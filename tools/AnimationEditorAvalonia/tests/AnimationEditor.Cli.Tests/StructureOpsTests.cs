using System.Text.Json;
using Shouldly;
using Xunit;

namespace AnimationEditor.Cli.Tests;

public class StructureOpsTests : IDisposable
{
    private readonly CliWorkspace _w = new("hero.achx", "hero.achj", "sheet.png", "other.png");

    public void Dispose() => _w.Dispose();

    private async Task<CliOutput> Edit(string file, string opsJson, params string[] extra) =>
        await _w.RunAsync(["edit", _w.PathOf(file), "--ops", _w.Write("ops.json", opsJson), .. extra]);

    private async Task<JsonElement> Chains(string file)
    {
        var r = await _w.RunAsync("describe", _w.PathOf(file), "--json");
        r.Code.ShouldBe(0);
        return JsonDocument.Parse(r.Out).RootElement.GetProperty("chains").Clone();
    }

    private static string[] Names(JsonElement chains) => chains.EnumerateArray().Select(c => c.GetProperty("name").GetString()!).ToArray();
    private static string[] Textures(JsonElement chain) => chain.GetProperty("frames").EnumerateArray().Select(f => f.GetProperty("texture").GetString()!).ToArray();

    [Theory]
    [InlineData("hero.achx")]
    [InlineData("hero.achj")]
    public async Task ChainOps_ApplyInOrder_AndSave(string file)
    {
        var r = await Edit(file, """
            {"ops":[
              {"op":"add-chain","name":"Jump"},
              {"op":"duplicate-chain","chain":"Walk","name":"Sprint"},
              {"op":"reorder-chain","chain":"Jump","index":0},
              {"op":"delete-chain","chain":"Idle"}
            ]}
            """);

        r.Code.ShouldBe(0, r.Out + r.Err);
        Names(await Chains(file)).ShouldBe(["Jump", "Walk", "Sprint"]);
    }

    [Theory]
    [InlineData("hero.achx")]
    [InlineData("hero.achj")]
    public async Task FrameOps_ApplyInOrder_AndSave(string file)
    {
        var r = await Edit(file, """
            {"ops":[
              {"op":"duplicate-frame","frame":"Walk/0"},
              {"op":"delete-frame","frame":"Walk/3"},
              {"op":"reorder-frame","frame":"Walk/0","index":2},
              {"op":"add-frame","chain":"Idle","texture":"other.png"}
            ]}
            """);

        r.Code.ShouldBe(0, r.Out + r.Err);
        var chains = await Chains(file);
        chains[0].GetProperty("frames").GetArrayLength().ShouldBe(3);
        Textures(chains[1]).ShouldBe(["sheet.png", "other.png"]);
    }

    [Fact]
    public async Task AddFrame_WithRegionAndLength_StoresPixelRegion()
    {
        var r = await Edit("hero.achx", """{"ops":[{"op":"add-frame","chain":"Idle","texture":"sheet.png","region":"16,0,16,32","length":0.3}]}""");

        r.Code.ShouldBe(0, r.Out + r.Err);
        var frame = (await Chains("hero.achx"))[1].GetProperty("frames")[1];
        frame.GetProperty("length").GetSingle().ShouldBe(0.3f, 0.0001f);
        var region = frame.GetProperty("region");
        region.GetProperty("x").GetSingle().ShouldBe(16f, 0.01f);
        region.GetProperty("width").GetSingle().ShouldBe(16f, 0.01f);
        region.GetProperty("height").GetSingle().ShouldBe(32f, 0.01f);
    }

    [Fact]
    public async Task AddFrame_WithoutLength_UsesTheFilesDefault()
    {
        var r = await Edit("hero.achx", """{"ops":[{"op":"add-frame","chain":"Idle","texture":"sheet.png"}]}""");

        r.Code.ShouldBe(0, r.Out + r.Err);
        var frame = (await Chains("hero.achx"))[1].GetProperty("frames")[1];
        frame.GetProperty("length").GetSingle().ShouldBe(0.1f, 0.0001f);
        frame.GetProperty("region").GetProperty("width").GetSingle().ShouldBe(48f, 0.01f);
    }

    [Fact]
    public async Task AddFrame_OutsideFolder_CopyChoiceCopiesNextToTheFile()
    {
        var outside = _w.Dir + "_outside";
        Directory.CreateDirectory(outside);
        try
        {
            var far = Path.Combine(outside, "far.png");
            File.Copy(_w.PathOf("other.png"), far);

            var r = await Edit("hero.achx", $$"""{"ops":[{"op":"add-frame","chain":"Idle","texture":{{JsonSerializer.Serialize(far)}},"copy":"copy"}]}""");

            r.Code.ShouldBe(0, r.Out + r.Err);
            File.Exists(_w.PathOf("far.png")).ShouldBeTrue();
        }
        finally { Directory.Delete(outside, recursive: true); }
    }

    [Theory]
    [InlineData("""{"op":"add-chain","name":"Walk"}""", "already exists")]
    [InlineData("""{"op":"add-chain","name":""}""", "empty")]
    [InlineData("""{"op":"delete-chain","chain":"Nope"}""", "Nope")]
    [InlineData("""{"op":"duplicate-chain","chain":"Walk","name":"Idle"}""", "already exists")]
    [InlineData("""{"op":"reorder-chain","chain":"Walk","index":5}""", "range")]
    [InlineData("""{"op":"add-frame","chain":"Idle","texture":"gone.png"}""", "gone.png")]
    [InlineData("""{"op":"add-frame","chain":"Idle","texture":"sheet.png","region":"40,0,16,4"}""", "region")]
    [InlineData("""{"op":"add-frame","chain":"Idle","texture":"sheet.png","region":"1,2,3"}""", "region")]
    [InlineData("""{"op":"add-frame","chain":"Idle","texture":"sheet.png","length":0}""", "length")]
    [InlineData("""{"op":"add-frame","chain":"Idle","texture":"sheet.png","length":60.1}""", "A frame length of 60.1 for Idle/1 is out of range: valid lengths are 0.001 to 60 seconds.")]
    [InlineData("""{"op":"add-frame","chain":"Idle","texture":"sheet.png","length":0.0001}""", "for Idle/1 is out of range: valid lengths are 0.001 to 60 seconds.")]
    [InlineData("""{"op":"add-frames-grid","chain":"Idle","texture":"sheet.png","cellWidth":16,"cellHeight":16,"columns":1,"rows":1,"frameLength":61}""", "A frame length of 61 for Idle/1 is out of range: valid lengths are 0.001 to 60 seconds.")]
    [InlineData("""{"op":"delete-frame","frame":"Walk/9"}""", "range")]
    [InlineData("""{"op":"duplicate-frame","frame":"Nope/0"}""", "Nope")]
    [InlineData("""{"op":"reorder-frame","frame":"Walk/0","index":3}""", "range")]
    public async Task FailingOp_WritesNothing_AndNamesTheProblem(string op, string messagePart)
    {
        var before = _w.Bytes("hero.achx");

        var r = await Edit("hero.achx", $$"""{"ops":[{"op":"add-chain","name":"Fresh"},{{op}}]}""", "--json");

        r.Code.ShouldBe(1, r.Out + r.Err);
        r.Out.ToLowerInvariant().ShouldContain(messagePart.ToLowerInvariant());
        using var doc = JsonDocument.Parse(r.Out);
        doc.RootElement.GetProperty("errors")[0].GetProperty("op").GetInt32().ShouldBe(1);
        _w.Bytes("hero.achx").ShouldBe(before);
    }

    [Theory]
    [InlineData("""{"ops":[{"op":"reorder-chain","chain":"Walk","index":"zero"}]}""", "index")]
    [InlineData("""{"ops":[{"op":"reorder-chain","chain":"Walk","index":1.5}]}""", "index")]
    [InlineData("""{"ops":[{"op":"add-frame","chain":"Idle","texture":"sheet.png","length":"long"}]}""", "length")]
    [InlineData("""{"ops":[{"op":"add-frame","chain":"Idle"}]}""", "texture")]
    public async Task MalformedStructureOps_AreRefusedBeforeAnythingIsApplied(string opsJson, string messagePart)
    {
        var before = _w.Bytes("hero.achx");

        var r = await Edit("hero.achx", opsJson);

        r.Code.ShouldBe(2);
        (r.Out + r.Err).ShouldContain(messagePart);
        _w.Bytes("hero.achx").ShouldBe(before);
    }

    [Theory]
    [InlineData("hero.achx")]
    [InlineData("hero.achj")]
    public async Task AddThenDeleteInOneBatch_LeavesTheFileByteIdentical(string file)
    {
        var before = _w.Bytes(file);

        var r = await Edit(file, """{"ops":[{"op":"add-chain","name":"Temp"},{"op":"delete-chain","chain":"Temp"}]}""");

        r.Code.ShouldBe(0, r.Out + r.Err);
        // The batch saves (it did change things in between); only the platform line ending may differ.
        System.Text.Encoding.UTF8.GetString(_w.Bytes(file)).Replace("\r\n", "\n")
            .ShouldBe(System.Text.Encoding.UTF8.GetString(before).Replace("\r\n", "\n"));
    }

    [Theory]
    [InlineData("hero.achx")]
    [InlineData("hero.achj")]
    public async Task ReorderToCurrentPosition_LeavesTheFileByteIdentical(string file)
    {
        var before = _w.Bytes(file);

        var r = await Edit(file, """{"ops":[{"op":"reorder-chain","chain":"Walk","index":0},{"op":"reorder-frame","frame":"Walk/1","index":1}]}""");

        r.Code.ShouldBe(0, r.Out + r.Err);
        _w.Bytes(file).ShouldBe(before);
    }

    [Fact]
    public async Task Schema_ListsTheStructureOps()
    {
        var r = await _w.RunAsync("--schema", "--json");

        r.Code.ShouldBe(0);
        using var doc = JsonDocument.Parse(r.Out);
        var ops = doc.RootElement.GetProperty("ops").EnumerateArray().ToDictionary(o => o.GetProperty("op").GetString()!);
        foreach (var name in new[] { "add-chain", "delete-chain", "duplicate-chain", "reorder-chain", "add-frame", "delete-frame", "duplicate-frame", "reorder-frame" })
            ops.ContainsKey(name).ShouldBeTrue(name);
        var reorder = ops["reorder-chain"].GetProperty("fields").EnumerateArray().ToDictionary(f => f.GetProperty("name").GetString()!);
        reorder["index"].GetProperty("type").GetString().ShouldBe("integer");
        var add = ops["add-frame"].GetProperty("fields").EnumerateArray().ToDictionary(f => f.GetProperty("name").GetString()!);
        add["length"].GetProperty("type").GetString().ShouldBe("number");
        add["length"].GetProperty("required").GetBoolean().ShouldBeFalse();
        add["region"].GetProperty("required").GetBoolean().ShouldBeFalse();
        add["texture"].GetProperty("required").GetBoolean().ShouldBeTrue();
    }

    [Theory]
    [InlineData("""{"op":"delete-frame","frame":"Walk/0"}""")]
    [InlineData("""{"op":"add-shape","frame":"Walk/0","type":"rect"}""")]
    [InlineData("""{"op":"add-frame-event","frame":"Walk/0","name":"Step"}""")]
    [InlineData("""{"op":"set-frame-offset","frame":"Walk/0","x":1}""")]
    public async Task LockedChain_EveryOpFamilyNamesTheUnlockOp(string op)
    {
        var r = await Edit("hero.achx", $$"""{"ops":[{"op":"set-chain-locked","chain":"Walk","value":true},{{op}}]}""");

        r.Code.ShouldBe(1);
        (r.Out + r.Err).ShouldContain("Chain 'Walk' is locked; unlock it first (set-chain-locked).");
    }
}

using System.Text.Json;
using Shouldly;
using Xunit;

namespace AnimationEditor.Cli.Tests;

public class EditCommandTests : IDisposable
{
    private readonly CliWorkspace _w = new("hero.achx", "hero.achj", "broken.achx", "sheet.png", "other.png");
    private readonly string _outside;

    public EditCommandTests()
    {
        _outside = _w.Dir + "_outside";
        Directory.CreateDirectory(_outside);
        File.Copy(_w.PathOf("other.png"), Path.Combine(_outside, "far.png"));
    }

    public void Dispose()
    {
        _w.Dispose();
        if (Directory.Exists(_outside)) Directory.Delete(_outside, recursive: true);
    }

    private string Ops(string json) => _w.Write("ops.json", json);

    private async Task<CliOutput> Edit(string file, string opsJson, params string[] extra) =>
        await _w.RunAsync(["edit", _w.PathOf(file), "--ops", Ops(opsJson), .. extra]);

    private async Task<JsonDocument> Describe(string file)
    {
        var r = await _w.RunAsync("describe", _w.PathOf(file), "--json");
        r.Code.ShouldBe(0);
        return JsonDocument.Parse(r.Out);
    }

    [Theory]
    [InlineData("hero.achx")]
    [InlineData("hero.achj")]
    public async Task RenameChain_AppliesAndSaves(string file)
    {
        var r = await Edit(file, """{"ops":[{"op":"rename-chain","chain":"Walk","to":"Run"}]}""");

        r.Code.ShouldBe(0, r.Out + r.Err);
        using var d = await Describe(file);
        d.RootElement.GetProperty("chains")[0].GetProperty("name").GetString().ShouldBe("Run");
    }

    [Fact]
    public async Task Ops_ApplyInOrder_LaterOpsUseEarlierNames()
    {
        var r = await Edit("hero.achx", """
            {"ops":[
              {"op":"rename-chain","chain":"Walk","to":"Run"},
              {"op":"rename-shape","shape":"Run/2/rect/Hitbox","to":"Box"}
            ]}
            """);

        r.Code.ShouldBe(0, r.Out + r.Err);
        using var d = await Describe("hero.achx");
        var shapes = d.RootElement.GetProperty("chains")[0].GetProperty("frames")[2].GetProperty("shapes");
        shapes[0].GetProperty("name").GetString().ShouldBe("Box");
    }

    [Fact]
    public async Task FailingOp_WritesNothing_AndReportsWhichOp()
    {
        var before = _w.Bytes("hero.achx");

        var r = await Edit("hero.achx", """
            {"ops":[
              {"op":"rename-chain","chain":"Walk","to":"Run"},
              {"op":"rename-chain","chain":"Nope","to":"X"}
            ]}
            """, "--json");

        r.Code.ShouldBe(1);
        _w.Bytes("hero.achx").ShouldBe(before);
        using var doc = JsonDocument.Parse(r.Out);
        doc.RootElement.GetProperty("success").GetBoolean().ShouldBeFalse();
        var err = doc.RootElement.GetProperty("errors")[0];
        err.GetProperty("op").GetInt32().ShouldBe(1);
        err.GetProperty("message").GetString()!.ShouldContain("Nope");
    }

    [Fact]
    public async Task OpsAddressedWithAStaleName_Fail()
    {
        var before = _w.Bytes("hero.achx");

        var r = await Edit("hero.achx", """
            {"ops":[
              {"op":"rename-chain","chain":"Walk","to":"Run"},
              {"op":"rename-shape","shape":"Walk/2/rect/Hitbox","to":"Box"}
            ]}
            """);

        r.Code.ShouldBe(1);
        _w.Bytes("hero.achx").ShouldBe(before);
    }

    [Fact]
    public async Task RenameToExistingName_FailsAndWritesNothing()
    {
        var before = _w.Bytes("hero.achx");

        var r = await Edit("hero.achx", """{"ops":[{"op":"rename-chain","chain":"Idle","to":"Walk"}]}""");

        r.Code.ShouldBe(1);
        r.Out.ShouldContain("already exists");
        _w.Bytes("hero.achx").ShouldBe(before);
    }

    [Fact]
    public async Task SetFrameTexture_RelativeFile_StoresRelativeName()
    {
        File.Copy(_w.PathOf("other.png"), _w.PathOf("alt.png"));

        var r = await Edit("hero.achx", """{"ops":[{"op":"set-frame-texture","frame":"Idle/0","texture":"alt.png"}]}""");

        r.Code.ShouldBe(0, r.Out + r.Err);
        using var d = await Describe("hero.achx");
        d.RootElement.GetProperty("chains")[1].GetProperty("frames")[0].GetProperty("texture").GetString().ShouldBe("alt.png");
    }

    [Fact]
    public async Task SetFrameTexture_MissingFile_FailsAndWritesNothing()
    {
        var before = _w.Bytes("hero.achx");

        var r = await Edit("hero.achx", """{"ops":[{"op":"set-frame-texture","frame":"Idle/0","texture":"gone.png"}]}""");

        r.Code.ShouldBe(1);
        r.Out.ShouldContain("gone.png");
        _w.Bytes("hero.achx").ShouldBe(before);
    }

    [Fact]
    public async Task SetFrameTexture_NotAnImage_FailsAndWritesNothing()
    {
        _w.Write("fake.png", "this is text");
        var before = _w.Bytes("hero.achx");

        var r = await Edit("hero.achx", """{"ops":[{"op":"set-frame-texture","frame":"Idle/0","texture":"fake.png"}]}""");

        r.Code.ShouldBe(1);
        _w.Bytes("hero.achx").ShouldBe(before);
    }

    [Fact]
    public async Task SetFrameTexture_OutsideFolder_CopyChoiceCopiesNextToTheFile()
    {
        var far = Path.Combine(_outside, "far.png");

        var r = await Edit("hero.achx", $$"""{"ops":[{"op":"set-frame-texture","frame":"Idle/0","texture":{{JsonSerializer.Serialize(far)}},"copy":"copy"}]}""");

        r.Code.ShouldBe(0, r.Out + r.Err);
        File.Exists(_w.PathOf("far.png")).ShouldBeTrue();
        using var d = await Describe("hero.achx");
        d.RootElement.GetProperty("chains")[1].GetProperty("frames")[0].GetProperty("texture").GetString().ShouldBe("far.png");
    }

    [Fact]
    public async Task SetFrameTexture_OnALockedChain_FailsBeforeCopyingTheFile()
    {
        var far = Path.Combine(_outside, "far.png");

        var r = await Edit("hero.achx", $$"""{"ops":[{"op":"set-chain-locked","chain":"Idle","value":true},{"op":"set-frame-texture","frame":"Idle/0","texture":{{JsonSerializer.Serialize(far)}},"copy":"copy"}]}""", "--json");

        r.Code.ShouldBe(1, r.Out + r.Err);
        r.Out.ShouldContain("Locked");
        File.Exists(_w.PathOf("far.png")).ShouldBeFalse();
    }

    [Fact]
    public async Task SetFrameTexture_OutsideFolder_KeepReferencesInPlaceWithoutCopying()
    {
        var far = Path.Combine(_outside, "far.png");

        var r = await Edit("hero.achx", $$"""{"ops":[{"op":"set-frame-texture","frame":"Idle/0","texture":{{JsonSerializer.Serialize(far)}}}]}""");

        r.Code.ShouldBe(0, r.Out + r.Err);
        File.Exists(_w.PathOf("far.png")).ShouldBeFalse();
        using var d = await Describe("hero.achx");
        d.RootElement.GetProperty("chains")[1].GetProperty("frames")[0].GetProperty("texture").GetString()!.ShouldEndWith("far.png");
    }

    [Theory]
    [InlineData("hero.achx")]
    [InlineData("hero.achj")]
    public async Task NoOpEdits_LeaveTheFileByteIdentical(string file)
    {
        var before = _w.Bytes(file);

        (await Edit(file, """{"ops":[]}""")).Code.ShouldBe(0);
        _w.Bytes(file).ShouldBe(before);

        (await Edit(file, """{"ops":[{"op":"rename-chain","chain":"Walk","to":"Walk"}]}""")).Code.ShouldBe(0);
        _w.Bytes(file).ShouldBe(before);
    }

    [Fact]
    public async Task PreexistingErrors_DoNotBlockAnEdit()
    {
        var path = _w.PathOf("hero.achx");
        File.WriteAllText(path, File.ReadAllText(path).Replace("<Name>Foot</Name>", "<Name>Hitbox</Name>"));

        var r = await Edit("hero.achx", """{"ops":[{"op":"rename-chain","chain":"Walk","to":"Run"}]}""");

        r.Code.ShouldBe(0, r.Out + r.Err);
        r.Out.ShouldContain("already");
        using var d = await Describe("hero.achx");
        d.RootElement.GetProperty("chains")[0].GetProperty("name").GetString().ShouldBe("Run");
    }

    // "The whole texture" needs the texture's size, and only a PNG header gives one here. Region 0,0,1,1 would be one pixel.
    [Theory]
    [InlineData("add-frame Idle texture=pic.jpg")]
    [InlineData("set-frame-texture New/0 texture=pic.jpg")]
    public async Task WholeTexture_OfAnImageWhoseSizeCannotBeRead_IsRefused_AndNothingIsWritten(string op)
    {
        File.WriteAllBytes(_w.PathOf("pic.jpg"), [0xFF, 0xD8, 0xFF, 0xE0, 0, 16, 0x4A, 0x46]);
        var hero = File.ReadAllText(_w.PathOf("hero.achx"));
        File.WriteAllText(_w.PathOf("hero.achx"), hero.Replace("</AnimationChainArraySave>",
            "<AnimationChain><Name>New</Name><Frame><TextureName></TextureName><FrameLength>0.1</FrameLength><LeftCoordinate>0</LeftCoordinate>" +
            "<RightCoordinate>1</RightCoordinate><TopCoordinate>0</TopCoordinate><BottomCoordinate>1</BottomCoordinate></Frame></AnimationChain></AnimationChainArraySave>"));
        var before = _w.Bytes("hero.achx");

        var r = await _w.RunAsync("edit", _w.PathOf("hero.achx"), "--op", op);

        r.Code.ShouldBe(1, r.Out + r.Err);
        r.Out.ShouldContain("pic.jpg");
        r.Out.ShouldContain("size");
        _w.Bytes("hero.achx").ShouldBe(before);
    }

    [Fact]
    public async Task SetFrameTexture_ToAnImageWhoseSizeCannotBeRead_KeepsASetRegion()
    {
        File.WriteAllBytes(_w.PathOf("pic.jpg"), [0xFF, 0xD8, 0xFF, 0xE0, 0, 16, 0x4A, 0x46]);

        var r = await _w.RunAsync("edit", _w.PathOf("hero.achx"), "--op", "set-frame-texture Walk/1 texture=pic.jpg");

        r.Code.ShouldBe(0, r.Out + r.Err);
        var region = (await Describe("hero.achx")).RootElement.GetProperty("chains")[0].GetProperty("frames")[1].GetProperty("region");
        region.GetProperty("x").GetDouble().ShouldBe(16);
        region.GetProperty("width").GetDouble().ShouldBe(16);
    }

    // A Pixel file saves as it is stored, so a missing texture no longer blocks it (#1952).
    [Fact]
    public async Task MissingTexture_DoesNotBlockSavingAPixelFile_AndKeepsItsRegion()
    {
        var r = await Edit("broken.achx", """{"ops":[{"op":"rename-chain","chain":"Walk","to":"Run"}]}""");

        r.Code.ShouldBe(0, r.Out + r.Err);
        var frame = (await Describe("broken.achx")).RootElement.GetProperty("chains")[0].GetProperty("frames")[1];
        frame.GetProperty("texture").GetString().ShouldBe("missing.png");
        frame.GetProperty("region").GetProperty("x").GetDouble().ShouldBe(16);
        frame.GetProperty("region").GetProperty("width").GetDouble().ShouldBe(16);
        frame.GetProperty("region").GetProperty("height").GetDouble().ShouldBe(32);
    }

    // #1934's repro: relinking frames whose PNG was missing used to multiply their pixels by the texture size.
    [Theory]
    [InlineData("hero.achx")]
    [InlineData("hero.achj")]
    public async Task SetFrameTexture_OnFramesWhosePngIsMissing_KeepsTheirRegions(string file)
    {
        Directory.CreateDirectory(_w.PathOf("art"));
        File.Move(_w.PathOf("sheet.png"), _w.PathOf("art/sheet.png"));
        var before = (await Describe(file)).RootElement.GetProperty("chains")[0].GetProperty("frames")[1].GetProperty("region").GetRawText();

        var r = await _w.RunAsync("edit", _w.PathOf(file), "--op", "set-frame-texture */* texture=art/sheet.png");

        r.Code.ShouldBe(0, r.Out + r.Err);
        var frame = (await Describe(file)).RootElement.GetProperty("chains")[0].GetProperty("frames")[1];
        frame.GetProperty("texture").GetString().ShouldBe("art/sheet.png");
        frame.GetProperty("region").GetRawText().ShouldBe(before);
    }

    [Theory]
    [InlineData("""{"ops":[{"op":"explode","chain":"Walk"}]}""", "explode")]
    [InlineData("""{"ops":[{"op":"rename-chain","chain":"Walk"}]}""", "to")]
    [InlineData("""{"ops":[{"op":"rename-chain","chain":"Walk","to":"X","bogus":1}]}""", "bogus")]
    [InlineData("""{"ops":[{"op":"rename-chain","chain":"Walk","to":5}]}""", "to")]
    [InlineData("""{"nope":[]}""", "ops")]
    [InlineData("not json", "JSON")]
    public async Task MalformedOps_AreRefusedBeforeAnythingIsApplied(string opsJson, string messagePart)
    {
        var before = _w.Bytes("hero.achx");

        var r = await Edit("hero.achx", opsJson);

        r.Code.ShouldBe(2);
        (r.Out + r.Err).ShouldContain(messagePart);
        _w.Bytes("hero.achx").ShouldBe(before);
    }

    [Fact]
    public async Task MissingOpsOption_IsUsageError()
    {
        var r = await _w.RunAsync("edit", _w.PathOf("hero.achx"));

        r.Code.ShouldBe(2);
    }

    [Fact]
    public async Task UvFile_IsRefusedWithoutChangingIt()
    {
        var path = _w.PathOf("hero.achx");
        File.WriteAllText(path, File.ReadAllText(path).Replace("<CoordinateType>Pixel", "<CoordinateType>UV"));
        var before = _w.Bytes("hero.achx");

        var r = await Edit("hero.achx", """{"ops":[{"op":"rename-chain","chain":"Walk","to":"Run"}]}""");

        r.Code.ShouldBe(1);
        (r.Out + r.Err).ShouldContain("UV");
        _w.Bytes("hero.achx").ShouldBe(before);
    }

    [Fact]
    public async Task Json_ReportsChangedItemsOnSuccess()
    {
        var r = await Edit("hero.achx", """{"ops":[{"op":"rename-chain","chain":"Walk","to":"Run"}]}""", "--json");

        r.Code.ShouldBe(0);
        using var doc = JsonDocument.Parse(r.Out);
        doc.RootElement.GetProperty("success").GetBoolean().ShouldBeTrue();
        doc.RootElement.GetProperty("changed").EnumerateArray().Select(c => c.GetString()).ShouldContain("Run");
    }
}

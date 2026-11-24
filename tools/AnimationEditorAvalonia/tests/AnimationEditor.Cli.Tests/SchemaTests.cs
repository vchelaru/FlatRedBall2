using System.Text.Json;
using AnimationEditor.Editing;
using Shouldly;
using Xunit;

namespace AnimationEditor.Cli.Tests;

public class SchemaTests
{
    [Fact]
    public async Task Text_ListsEveryOpWithItsFields()
    {
        using var w = new CliWorkspace();

        var r = await w.RunAsync("--schema");

        r.Code.ShouldBe(0);
        foreach (var token in new[] { "rename-chain", "rename-shape", "set-frame-texture", "chain", "shape", "frame", "texture", "copy", "to" })
            r.Out.ShouldContain(token);
    }

    [Fact]
    public async Task Text_StartsWithAValidOneLineOpsFileExample()
    {
        using var w = new CliWorkspace();

        var r = await w.RunAsync("--schema");

        var first = r.Out.Split('\n')[0].TrimEnd('\r');
        first.ShouldStartWith("Example ops file: ");
        var example = first["Example ops file: ".Length..];
        EditOps.Parse(example).Error.ShouldBeNull();
    }

    [Fact]
    public void BareArray_ErrorShowsTheRequiredShape()
    {
        var error = EditOps.Parse("[{\"op\":\"rename-chain\"}]").Error;

        error.ShouldNotBeNull();
        error.ShouldContain(EditOps.ExampleFile);
    }

    [Fact]
    public async Task Json_DescribesFieldTypesAndRequiredness()
    {
        using var w = new CliWorkspace();

        var r = await w.RunAsync("--schema", "--json");

        r.Code.ShouldBe(0);
        using var doc = JsonDocument.Parse(r.Out);
        var ops = doc.RootElement.GetProperty("ops").EnumerateArray().ToDictionary(o => o.GetProperty("op").GetString()!);
        ops.Keys.ShouldBe(["add-frames-grid", "associate-tileset", "set-chain-owner-tile", "add-frame-event", "duplicate-frame-event", "delete-frame-event", "set-frame-event", "set-frame-keyframe", "set-frame-keyframe-curve", "set-shape-keyframe", "set-shape-keyframe-curve", "reorder-shape", "clear-frame-color", "add-chain", "add-frame", "add-shape", "delete-chain", "delete-frame", "delete-shape", "duplicate-chain", "duplicate-frame", "duplicate-shape", "import-chain", "rename-chain", "rename-shape", "reorder-chain", "reorder-frame", "set-chain-length", "invert-frame-order", "justify-offsets-bottom", "match-frame-size", "set-chain-locked", "set-chain-loop", "set-chain-pingpong", "set-chain-reverse", "set-circle", "set-frame-color", "set-frame-flip", "set-frame-length", "set-frame-offset", "set-frame-region", "set-frame-texture", "set-polygon", "set-rect", "set-shape-common", "set-unit"], ignoreOrder: true);
        var rename = ops["rename-chain"].GetProperty("fields").EnumerateArray().ToDictionary(f => f.GetProperty("name").GetString()!);
        rename["chain"].GetProperty("type").GetString().ShouldBe("string");
        rename["chain"].GetProperty("required").GetBoolean().ShouldBeTrue();
        rename["to"].GetProperty("required").GetBoolean().ShouldBeTrue();
        var tex = ops["set-frame-texture"].GetProperty("fields").EnumerateArray().ToDictionary(f => f.GetProperty("name").GetString()!);
        tex["copy"].GetProperty("required").GetBoolean().ShouldBeFalse();
        tex["copy"].GetProperty("values").EnumerateArray().Select(v => v.GetString()).ShouldBe(["keep", "copy", "overwrite"]);
    }

    [Fact]
    public async Task UnknownCommand_IsUsageError()
    {
        using var w = new CliWorkspace();

        var r = await w.RunAsync("frobnicate");

        r.Code.ShouldBe(2);
    }
}

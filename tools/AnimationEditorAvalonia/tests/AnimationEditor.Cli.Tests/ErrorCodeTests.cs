using System.Text.Json;
using Shouldly;
using Xunit;

namespace AnimationEditor.Cli.Tests;

/// <summary>Every failure an agent can tell apart carries its own `--json` error code, never Unspecified.</summary>
public class ErrorCodeTests : IDisposable
{
    private readonly CliWorkspace _w = new("hero.achx", "broken.achx", "sheet.png", "collection.tsx");

    public void Dispose() => _w.Dispose();

    private async Task<string> CodeOf(string file, string opJson)
    {
        var ops = _w.Write("ops.json", $$"""{"ops":[{{opJson}}]}""");
        var r = await _w.RunAsync("edit", _w.PathOf(file), "--ops", ops, "--json");
        r.Code.ShouldBe(1, r.Out + r.Err);
        using var d = JsonDocument.Parse(r.Out);
        return d.RootElement.GetProperty("errors")[0].GetProperty("code").GetString()!;
    }

    [Theory]
    [InlineData("""{"op":"rename-chain","chain":"Idel","to":"X"}""", "NotFound")]
    [InlineData("""{"op":"delete-chain","chain":"Idel"}""", "NotFound")]
    [InlineData("""{"op":"rename-shape","shape":"Walk/2/rect/Hitbx","to":"X"}""", "NotFound")]
    [InlineData("""{"op":"delete-frame","frame":"Walk/99"}""", "IndexOutOfRange")]
    [InlineData("""{"op":"set-frame-length","frame":"Walk/99","length":0.1}""", "IndexOutOfRange")]
    [InlineData("""{"op":"reorder-chain","chain":"Walk","index":99}""", "IndexOutOfRange")]
    [InlineData("""{"op":"reorder-frame","frame":"Walk/0","index":99}""", "IndexOutOfRange")]
    [InlineData("""{"op":"set-frame-length","frame":"Walk/0","length":999}""", "InvalidValue")]
    [InlineData("""{"op":"set-frame-color","frame":"Walk/0","red":999}""", "InvalidValue")]
    [InlineData("""{"op":"delete-frame","frame":"Walk"}""", "InvalidAddress")]
    [InlineData("""{"op":"delete-frame","frame":"Walk/x"}""", "InvalidAddress")]
    [InlineData("""{"op":"rename-chain","chain":"Walk/1","to":"X"}""", "InvalidAddress")]
    [InlineData("""{"op":"rename-shape","shape":"Walk/2/blob/Hitbox","to":"X"}""", "InvalidAddress")]
    [InlineData("""{"op":"rename-shape","shape":"Walk/2/rect","to":"X"}""", "InvalidAddress")]
    [InlineData("""{"op":"set-rect","shape":"Walk/2/circle/Foot","x":1}""", "WrongShapeType")]
    [InlineData("""{"op":"set-frame-texture","frame":"Walk/0","texture":"nope.png"}""", "TextureNotFound")]
    [InlineData("""{"op":"set-frame-region","frame":"Walk/0","region":"0,0,9999,9999"}""", "InvalidValue")]
    [InlineData("""{"op":"set-frame-region","frame":"Walk/0","region":"abc"}""", "InvalidValue")]
    
    public async Task Failure_HasTypedCode(string op, string? expected)
    {
        var code = await CodeOf("hero.achx", op);

        if (expected is null) code.ShouldNotBe("Unspecified");
        else code.ShouldBe(expected);
    }

    [Fact]
    public async Task MissingFile_IsNotFound() => (await FileCode("nope.achx")).ShouldBe("NotFound");

    [Fact]
    public async Task WrongExtension_IsUnsupportedFile() => (await FileCode("sheet.png")).ShouldBe("UnsupportedFile");

    [Fact]
    public async Task UnreadableFile_IsIoFailed()
    {
        _w.Write("garbage.achx", "not xml");
        (await FileCode("garbage.achx")).ShouldBe("IoFailed");
    }

    private async Task<string> FileCode(string file)
    {
        var ops = _w.Write("ops.json", """{"ops":[{"op":"add-chain","name":"Z"}]}""");
        var r = await _w.RunAsync("edit", _w.PathOf(file), "--ops", ops, "--json");
        r.Code.ShouldBe(1, r.Out + r.Err);
        using var d = JsonDocument.Parse(r.Out);
        return d.RootElement.GetProperty("errors")[0].GetProperty("code").GetString()!;
    }

    [Fact]
    public async Task SchemaListsEveryErrorCode()
    {
        var r = await _w.RunAsync("--schema");
        r.Code.ShouldBe(0);
        foreach (var name in Enum.GetNames<AnimationEditor.Editing.EditErrorCode>())
            r.Out.ShouldContain(name);
    }
}

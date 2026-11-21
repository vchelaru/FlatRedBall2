using System.Text.Json;
using Shouldly;
using Xunit;

namespace AnimationEditor.Cli.Tests;

/// <summary>Exit-2 usage errors are JSON under --json, one hint line in text, and not-found names get suggestions.</summary>
public class UsageErrorTests : IDisposable
{
    private readonly CliWorkspace _w = new("hero.achx", "sheet.png");

    public void Dispose() => _w.Dispose();

    private Task<CliOutput> Edit(string opsJson, params string[] extra) =>
        _w.RunAsync(["edit", _w.PathOf("hero.achx"), "--ops", _w.Write("ops.json", opsJson), .. extra]);

    [Theory]
    [InlineData("{not json")]
    [InlineData("""{"ops":[{"op":"rename-chan","chain":"Walk","to":"X"}]}""")]
    [InlineData("""{"ops":[{"op":"rename-chain","chain":"Walk"}]}""")]
    [InlineData("""{"ops":[{"op":"rename-chain","chain":"Walk","to":"X","bogus":1}]}""")]
    public async Task OpsFileProblem_WithJson_IsSuccessFalseDocument(string ops)
    {
        var r = await Edit(ops, "--json");

        r.Code.ShouldBe(2);
        r.Err.ShouldBeEmpty();
        using var d = JsonDocument.Parse(r.Out);
        d.RootElement.GetProperty("success").GetBoolean().ShouldBeFalse();
        var e = d.RootElement.GetProperty("errors")[0];
        e.GetProperty("code").GetString().ShouldBe("UsageError");
        e.GetProperty("message").GetString().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task UnknownOption_WithJson_IsJson()
    {
        var r = await _w.RunAsync("describe", "--bogus", "--json");

        r.Code.ShouldBe(2);
        using var d = JsonDocument.Parse(r.Out);
        d.RootElement.GetProperty("errors")[0].GetProperty("code").GetString().ShouldBe("UsageError");
    }

    [Fact]
    public async Task UsageError_InText_IsMessagePlusOneHintLine()
    {
        var r = await Edit("""{"ops":[{"op":"rename-chan","chain":"Walk","to":"X"}]}""");

        r.Code.ShouldBe(2);
        r.Err.ShouldNotContain("usage: aecli");
        r.Err.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length.ShouldBeLessThanOrEqualTo(2);
    }

    [Fact]
    public async Task UnknownOp_SuggestsNearestOp()
    {
        var r = await Edit("""{"ops":[{"op":"rename-chan","chain":"Walk","to":"X"}]}""");

        r.Err.ShouldContain("Did you mean 'rename-chain'");
    }

    private async Task<string> Message(string op)
    {
        var r = await Edit($$"""{"ops":[{{op}}]}""", "--json");
        r.Code.ShouldBe(1, r.Out + r.Err);
        using var d = JsonDocument.Parse(r.Out);
        return d.RootElement.GetProperty("errors")[0].GetProperty("message").GetString()!;
    }

    [Fact]
    public async Task TypoChain_SuggestsNearest() =>
        (await Message("""{"op":"rename-chain","chain":"Idel","to":"X"}""")).ShouldContain("Did you mean 'Idle'");

    [Fact]
    public async Task UnrelatedChain_ListsValidNames() =>
        (await Message("""{"op":"rename-chain","chain":"Zzzzzzzz","to":"X"}""")).ShouldContain("Valid chains: Walk, Idle");

    [Fact]
    public async Task TypoShape_SuggestsNearest() =>
        (await Message("""{"op":"rename-shape","shape":"Walk/2/rect/Hitbx","to":"X"}""")).ShouldContain("Did you mean 'Hitbox'");
}

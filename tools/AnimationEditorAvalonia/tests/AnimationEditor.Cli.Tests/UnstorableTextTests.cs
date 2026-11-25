using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Cli.Tests;

/// <summary>Text that XML cannot store is refused by every op that stores text, in an .achj too (#2046).</summary>
public class UnstorableTextTests : InlineBatchTests
{
    private static string Bad => "Bad" + (char)1;

    [Theory]
    [InlineData("rename-chain Walk to=BAD")]
    [InlineData("add-chain BAD")]
    [InlineData("duplicate-chain Walk name=BAD")]
    [InlineData("add-shape Walk/0 type=rect name=BAD")]
    [InlineData("add-frame-event Walk/0 name=BAD")]
    [InlineData("add-frame-event Walk/0 name=Step data=BAD")]
    public async Task Op_WithTextXmlCannotStore_IsRefusedNamingTheCharacter(string op)
    {
        (await Fails("hero.achx", "InvalidValue", op.Replace("BAD", Bad))).ShouldContain("U+0001");
        (await Fails("hero.achj", "InvalidValue", op.Replace("BAD", Bad))).ShouldContain("U+0001");
    }

    [Theory]
    [InlineData("rename-shape Walk/0/rect/New to=BAD")]
    [InlineData("set-frame-event Walk/0/event/0 name=BAD")]
    [InlineData("set-frame-event Walk/0/event/0 data=BAD")]
    public async Task OpOnAnAddedItem_WithTextXmlCannotStore_IsRefusedNamingTheCharacter(string op) =>
        (await Fails("hero.achj", "InvalidValue",
            "add-shape Walk/0 type=rect name=New", "add-frame-event Walk/0 name=Step", op.Replace("BAD", Bad))).ShouldContain("U+0001");

    [Fact]
    public async Task RenameChain_AwayFromANameXmlCannotStore_Works()
    {
        WriteBadAchj();

        await Ok("bad.achj", $"rename-chain {Bad} to=Good");

        AnimationChainListSave.FromFile(PathOf("bad.achj")).AnimationChains.Single().Name.ShouldBe("Good");
    }

    [Fact]
    public async Task OpsFile_EscapingHalfASurrogatePair_IsAUsageError()
    {
        using var w = new CliWorkspace("hero.achx");
        string ops = w.Write("ops.json", """{"ops":[{"op":"rename-chain","chain":"Walk","to":"BAD"}]}""".Replace("BAD", (char)92 + "ud800"));

        var r = await w.RunAsync("edit", w.PathOf("hero.achx"), "--ops", ops, "--json");

        r.Code.ShouldBe(2);
        r.Out.ShouldContain("op 0: ");
        r.Out.ShouldContain("surrogate");
    }

    private void WriteBadAchj()
    {
        var file = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        file.AnimationChains.Add(new AnimationChainSave { Name = Bad });
        file.SaveJson(PathOf("bad.achj"));
    }
}

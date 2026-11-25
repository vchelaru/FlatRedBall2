using FlatRedBall2.Animation;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationChain.Common.Tests;

public class StorableTextTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("Walk \t\r\n \u00E9 \uE000 \uFFFD", null)]
    [InlineData("astral \uD83D\uDE00", null)]
    [InlineData("astral noncharacter, which XML allows \uD83F\uDFFE", null)]
    [InlineData("\0", "U+0000")]
    [InlineData("a\u001Fb", "U+001F")]
    [InlineData("a\uFFFE", "U+FFFE")]
    [InlineData("a\uFFFF", "U+FFFF")]
    [InlineData("pair\uD83D\uDE00then\u0001", "U+0001")]
    public void Problem_NamesTheFirstCharacterXmlCannotStore(string? text, string? character)
    {
        string? problem = StorableText.Problem(text);

        if (character is null) problem.ShouldBeNull();
        else problem.ShouldNotBeNull().ShouldContain($"contains the character {character}, which");
    }

    // Built from numbers: xunit's test names and attributes do not carry half a surrogate pair.
    [Theory]
    [InlineData(new[] { 0xD83D }, "U+D83D")]
    [InlineData(new[] { 0xD83D, 'x' }, "U+D83D")]
    [InlineData(new[] { 0xDE00 }, "U+DE00")]
    [InlineData(new[] { 0xDE00, 0xD83D }, "U+DE00")]
    public void Problem_HalfASurrogatePair_IsNamed(int[] units, string character)
    {
        string text = "Walk" + new string(units.Select(unit => (char)unit).ToArray());

        StorableText.Problem(text).ShouldNotBeNull().ShouldContain($"contains the character {character}, which");
    }

    private static AnimationChainListSave File(string field, string text)
    {
        var frame = new AnimationFrameSave { TextureName = field == "texture" ? text : "hero.png", ShapesSave = new ShapesSave() };
        frame.ShapesSave.Add(new AARectSave { Name = field == "rect" ? text : "Hit" });
        frame.ShapesSave.Add(new CircleSave { Name = field == "circle" ? text : "Ball" });
        frame.ShapesSave.Add(new PolygonSave { Name = field == "polygon" ? text : "Hull" });
        frame.Events.Add(new AnimationFrameEvent { Name = field == "event" ? text : "Step", Data = field == "data" ? text : null });
        var chain = new AnimationChainSave { Name = field == "chain" ? text : "Walk" };
        chain.Frames.Add(frame);
        return new AnimationChainListSave { AnimationChains = [chain] };
    }

    [Theory]
    [InlineData("chain")]
    [InlineData("texture")]
    [InlineData("rect")]
    [InlineData("circle")]
    [InlineData("polygon")]
    [InlineData("event")]
    [InlineData("data")]
    public void Save_TextXmlCannotStore_ThrowsNamingTheAnimationAndTheCharacter(string field)
    {
        var save = File(field, "Bad\u0001Text");

        var error = Should.Throw<Exception>(() => save.Save(new MemoryStream()));

        error.Message.ShouldStartWith(field == "chain" ? "\"Bad" : "Animation \"Walk\": \"Bad");
        error.Message.ShouldEndWith("\"Bad\uFFFDText\" contains the character U+0001, which XML cannot store.");
    }

    [Theory]
    [InlineData("Bad\u0001")]
    [InlineData("Bad\uFFFF")]
    public void SaveJson_TextXmlCannotStore_RoundTrips(string name)
    {
        AnimationChainListSave.FromJsonString(File("chain", name).ToJsonString()).AnimationChains[0].Name.ShouldBe(name);
    }
}

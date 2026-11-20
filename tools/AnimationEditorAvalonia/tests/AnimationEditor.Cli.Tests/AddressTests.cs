using AnimationEditor.Editing;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Cli.Tests;

public class AddressTests
{
    private readonly AnimationChainListSave _doc = Build();

    private static AnimationChainListSave Build()
    {
        var doc = new AnimationChainListSave();
        var walk = new AnimationChainSave { Name = "Walk" };
        walk.Frames.Add(new AnimationFrameSave());
        var f1 = new AnimationFrameSave { ShapesSave = new ShapesSave() };
        f1.ShapesSave.Add(new AARectSave { Name = "Hitbox" });
        f1.ShapesSave.Add(new CircleSave { Name = "Hitbox" });
        f1.ShapesSave.Add(new PolygonSave { Name = "Poly" });
        walk.Frames.Add(f1);
        doc.AnimationChains.Add(walk);
        doc.AnimationChains.Add(new AnimationChainSave { Name = "Dup" });
        doc.AnimationChains.Add(new AnimationChainSave { Name = "Dup" });
        doc.AnimationChains.Add(new AnimationChainSave { Name = "a/b" });
        return doc;
    }

    [Fact]
    public void Chain_ResolvesByName()
    {
        var r = EditAddress.ResolveChain(_doc, "Walk");

        r.Value.ShouldBeSameAs(_doc.AnimationChains[0]);
    }

    [Theory]
    [InlineData("Nope", "Nope")]
    [InlineData("Dup", "more than one")]
    [InlineData("", "empty")]
    [InlineData("a/b", "'/'")]
    public void Chain_Errors(string address, string messagePart)
    {
        var r = EditAddress.ResolveChain(_doc, address);

        r.Value.ShouldBeNull();
        r.Error.ShouldNotBeNull().ShouldContain(messagePart);
    }

    [Fact]
    public void Frame_ResolvesByZeroBasedIndex()
    {
        var r = EditAddress.ResolveFrame(_doc, "Walk/1");

        r.Value.ShouldBeSameAs(_doc.AnimationChains[0].Frames[1]);
    }

    [Theory]
    [InlineData("Walk/2", "out of range")]
    [InlineData("Walk/-1", "index")]
    [InlineData("Walk/x", "index")]
    [InlineData("Walk", "Chain/index")]
    [InlineData("Walk/1/rect/Hitbox", "Chain/index")]
    [InlineData("Nope/0", "Nope")]
    public void Frame_Errors(string address, string messagePart)
    {
        var r = EditAddress.ResolveFrame(_doc, address);

        r.Value.ShouldBeNull();
        r.Error.ShouldNotBeNull().ShouldContain(messagePart);
    }

    [Fact]
    public void Shape_ResolvesByTypeAndName_EvenWhenAnotherTypeSharesTheName()
    {
        var frame = _doc.AnimationChains[0].Frames[1];

        EditAddress.ResolveShape(_doc, "Walk/1/rect/Hitbox").Value.ShouldBeOfType<AARectSave>();
        EditAddress.ResolveShape(_doc, "Walk/1/circle/Hitbox").Value.ShouldBeOfType<CircleSave>();
        EditAddress.ResolveShape(_doc, "Walk/1/polygon/Poly").Value.ShouldBeSameAs(frame.ShapesSave!.PolygonSaves.Single());
    }

    [Theory]
    [InlineData("Walk/1/square/Hitbox", "rect|circle|polygon")]
    [InlineData("Walk/1/rect/Nope", "Nope")]
    [InlineData("Walk/0/rect/Hitbox", "Hitbox")]
    [InlineData("Walk/1/rect", "Chain/index/type/name")]
    [InlineData("Walk/9/rect/Hitbox", "out of range")]
    public void Shape_Errors(string address, string messagePart)
    {
        var r = EditAddress.ResolveShape(_doc, address);

        r.Value.ShouldBeNull();
        r.Error.ShouldNotBeNull().ShouldContain(messagePart);
    }
}

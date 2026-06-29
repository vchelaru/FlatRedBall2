using AnimationEditor.Core.IO;
using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class ChainPasteLogicTests
{
    static AnimationChainListSave Acls(params string[] names)
    {
        var acls = new AnimationChainListSave();
        foreach (var name in names)
            acls.AnimationChains.Add(new AnimationChainSave { Name = name });
        return acls;
    }

    static AnimationChainSave Chain(string name) => new AnimationChainSave { Name = name };

    [Fact]
    public void InsertPastedChains_InsertsDirectlyBelowSource()
    {
        var acls = Acls("Walk", "Run", "Jump");

        ChainPasteLogic.InsertPastedChains(acls, new[] { Chain("Walk") });

        Assert.Equal(new[] { "Walk", "Walk2", "Run", "Jump" },
            acls.AnimationChains.Select(c => c.Name).ToArray());
    }

    [Fact]
    public void InsertPastedChains_MultipleSources_InsertsBelowLowestSource()
    {
        var acls = Acls("Walk", "Run", "Jump");

        // Copied "Walk" and "Run" together; the block lands below "Run" (the lowest source).
        ChainPasteLogic.InsertPastedChains(acls, new[] { Chain("Walk"), Chain("Run") });

        Assert.Equal(new[] { "Walk", "Run", "Walk2", "Run2", "Jump" },
            acls.AnimationChains.Select(c => c.Name).ToArray());
    }

    [Fact]
    public void InsertPastedChains_NonAdjacentSources_InsertsBelowLastSource()
    {
        var acls = Acls("Walk", "Run", "Jump");

        ChainPasteLogic.InsertPastedChains(acls, new[] { Chain("Walk"), Chain("Jump") });

        Assert.Equal(new[] { "Walk", "Run", "Jump", "Walk2", "Jump2" },
            acls.AnimationChains.Select(c => c.Name).ToArray());
    }

    [Fact]
    public void InsertPastedChains_NoMatchingSource_AppendsAtEnd()
    {
        var acls = Acls("Walk", "Run");

        // Pasting into a project that never had the source chain.
        ChainPasteLogic.InsertPastedChains(acls, new[] { Chain("Idle") });

        Assert.Equal(new[] { "Walk", "Run", "Idle" },
            acls.AnimationChains.Select(c => c.Name).ToArray());
    }

    [Fact]
    public void InsertPastedChains_WithSelectionAnchor_InsertsBelowSelectedBlock()
    {
        var acls = Acls("Anim1", "Anim2", "Anim1Copy", "Anim2Copy");

        ChainPasteLogic.InsertPastedChains(
            acls,
            new[] { Chain("Anim1"), Chain("Anim2") },
            anchorChains: new[] { acls.AnimationChains[2], acls.AnimationChains[3] });

        Assert.Equal(6, acls.AnimationChains.Count);
        Assert.Equal(new[] { "Anim1", "Anim2", "Anim1Copy", "Anim2Copy" },
            acls.AnimationChains.Take(4).Select(c => c.Name));
        Assert.Equal(new[] { "Anim3", "Anim4" },
            acls.AnimationChains.Skip(4).Select(c => c.Name));
    }

    [Fact]
    public void InsertPastedChains_EmptyInput_LeavesListUnchanged()
    {
        var acls = Acls("Walk", "Run");

        ChainPasteLogic.InsertPastedChains(acls, System.Array.Empty<AnimationChainSave>());

        Assert.Equal(new[] { "Walk", "Run" },
            acls.AnimationChains.Select(c => c.Name).ToArray());
    }
}

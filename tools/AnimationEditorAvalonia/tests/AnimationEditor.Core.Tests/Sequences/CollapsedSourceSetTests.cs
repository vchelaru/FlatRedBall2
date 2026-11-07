using AnimationEditor.Core.Sequences;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

public class CollapsedSourceSetTests
{
    [Fact]
    public void Toggle_CollapsesThenExpands_AndRaisesChangedEachTime()
    {
        var set = new CollapsedSourceSet();
        int changes = 0;
        set.Changed += () => changes++;

        set.Toggle("chars/hero.achx");
        bool afterFirst = set.IsCollapsed("chars/hero.achx");
        set.Toggle("chars/hero.achx");

        afterFirst.ShouldBeTrue();
        set.IsCollapsed("chars/hero.achx").ShouldBeFalse();
        changes.ShouldBe(2);
    }

    [Fact]
    public void IsCollapsed_IgnoresCase_LikeTheDocumentsSourceLookup()
    {
        var set = new CollapsedSourceSet();

        set.Toggle("Chars/Hero.achx");

        set.IsCollapsed("chars/hero.achx").ShouldBeTrue();
    }

    [Fact]
    public void Prune_DropsSourcesNoLongerListed_KeepsTheRest_AndRaisesChangedOnlyWhenItDropped()
    {
        var set = new CollapsedSourceSet();
        set.Toggle("a.achx");
        set.Toggle("b.achx");
        int changes = 0;
        set.Changed += () => changes++;

        set.Prune(new[] { "a.achx", "c.achx" });
        set.Prune(new[] { "a.achx", "c.achx" });

        set.Keys.ShouldBe(new[] { "a.achx" });
        changes.ShouldBe(1);
    }

    [Fact]
    public void Load_ReplacesTheKeys_WithoutRaisingChanged()
    {
        var set = new CollapsedSourceSet();
        set.Toggle("old.achx");
        int changes = 0;
        set.Changed += () => changes++;

        set.Load(new[] { "a.achx", "b.achx" });
        set.Load(null);
        set.Load(new[] { "c.achx" });

        set.Keys.ShouldBe(new[] { "c.achx" });
        changes.ShouldBe(0);
    }
}

using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.IO;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

public class SequenceLibraryTests
{
    [Fact]
    public void Build_OneGroupPerSource_ChipsPerChain_MissingSourceFlaggedWithNoChips()
    {
        using var dir = new TestHelpers.TempDir();
        var list = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        list.AnimationChains.Add(new AnimationChainSave { Name = "Walk" });
        list.AnimationChains.Add(new AnimationChainSave { Name = "Run" });
        list.SaveJson(Path.Combine(dir.Path, "hero.achj"));
        string path = Path.Combine(dir.Path, "boss.seqj");
        new SequenceListSave { Sources = { "hero.achj", "gone.achx" } }.Save(path);

        var groups = SequenceLibrary.Build(SequenceDocument.Load(path));

        groups.Select(g => (g.Source, g.IsMissing)).ShouldBe(new[] { ("hero.achj", false), ("gone.achx", true) });
        groups[0].Chips.Select(c => c.Chain.Name).ShouldBe(new[] { "Walk", "Run" });
        groups[0].Chips.ShouldAllBe(c => c.Source == "hero.achj");
        groups[1].Chips.ShouldBeEmpty();
    }
}

using AnimationEditor.Core.Sequences;
using Shouldly;
using System.Linq;
using Xunit;
using Cmd = AnimationEditor.Core.Sequences.SequenceListMenuCommand;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>Which commands the SEQUENCES list's right-click menu offers, by what was hit.</summary>
public class SequenceListMenuLayoutTests
{
    [Fact]
    public void OnASequence_OffersCopyQualifiedName_ThenRenameAndDelete_ThenAddSequence()
    {
        SequenceListMenuLayout.For(onSequence: true).Select(group => group.ToArray()).ShouldBe(new[]
        {
            new[] { Cmd.CopyQualifiedName },
            new[] { Cmd.Rename, Cmd.Delete },
            new[] { Cmd.AddSequence },
        });
    }

    [Fact]
    public void OnBlankSpace_OffersOnlyAddSequence()
    {
        SequenceListMenuLayout.For(onSequence: false).Select(group => group.ToArray()).ShouldBe(new[]
        {
            new[] { Cmd.AddSequence },
        });
    }
}

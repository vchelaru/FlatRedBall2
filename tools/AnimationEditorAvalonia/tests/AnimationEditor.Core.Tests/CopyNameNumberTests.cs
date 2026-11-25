using AnimationEditor.Core.IO;
using Shouldly;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>A copy of an animation whose name ends in digits that do not fit an int.</summary>
public class CopyNameNumberTests
{
    [Theory]
    [InlineData("Shot2147483647")]
    [InlineData("Capture20260101123456")]
    [InlineData("Level٣")]
    [InlineData("Walk")]
    [InlineData("9")]
    [InlineData("")]
    public void Duplicate_AndPaste_GiveACopyAFreeName(string name)
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var source = TestHelpers.MakeChain(ctx.Acls, name);

        ctx.AppCommands.DuplicateChains([source]);
        ctx.AppCommands.PasteChains([AnimationCloneHelper.CloneChain(source)]);

        ctx.Acls.AnimationChains.Select(c => c.Name).Distinct().Count().ShouldBe(3);
    }
}

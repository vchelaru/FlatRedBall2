using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.Utilities;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>Copy Qualified Name on the animation right-click menu (issue #1378).</summary>
[Collection("SequentialSingletons")]
public class QualifiedNameTests
{
    private static TreeMenuActions Actions(Action<string>? copyText) => new(
        Copy: () => { }, Cut: () => { }, Paste: () => { }, Duplicate: () => { }, Delete: () => { },
        Rename: () => { }, AddAnimation: () => { }, DuplicateChainFlip: (_, _) => { },
        CopyText: copyText);

    [Fact]
    public void Format_WindowsBackslashPath_UsesForwardSlashes()
    {
        QualifiedName.Format(@"C:\my projects\Player.achj", "Walk")
            .ShouldBe("Walk in C:/my projects/Player.achj");
    }

    [Fact]
    public void TreeMenu_ChainNode_CopyQualifiedNameCopiesNameInFile()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 1);
        ctx.ProjectManager.FileName = @"C:\game\Player.achj";
        string? copied = null;

        var items = TreeMenuPlanBuilder.Build(
            chain, ctx.AppCommands, ctx.SelectedState, ctx.ObjectFinder, ctx.ProjectManager, Actions(t => copied = t));
        items.Single(i => i.Header == "Copy Qualified Name").OnClick!();

        copied.ShouldBe("Walk in C:/game/Player.achj");
    }

    [Fact]
    public void TreeMenu_ChainNode_UnsavedProject_OmitsCopyQualifiedName()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 1);
        ctx.ProjectManager.FileName = null;

        var items = TreeMenuPlanBuilder.Build(
            chain, ctx.AppCommands, ctx.SelectedState, ctx.ObjectFinder, ctx.ProjectManager, Actions(_ => { }));

        items.ShouldNotContain(i => i.Header == "Copy Qualified Name");
    }
}

using AnimationEditor.Core;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Saving a document that is not the current one: a cross-document cut removes chains from a
/// background tab's model, and that model must reach its file or the chain comes back from disk.
/// </summary>
public class AppCommandsSaveDocumentTests
{
    [Fact]
    public void SaveDocument_WritesThatDocument_AndLeavesTheCurrentOneAlone()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        TestHelpers.MakeChain(ctx.Acls, "Current");
        var other = new AnimationChainListSave { CoordinateType = TextureCoordinateType.UV };
        TestHelpers.MakeChain(other, "Other");
        string dir = Path.Combine(Path.GetTempPath(), "AnimationEditorCoreTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "other.achx");

        ctx.AppCommands.SaveDocument(other, path, TextureCoordinateType.UV);

        AnimationChainListSave.FromFile(path).AnimationChains.Single().Name.ShouldBe("Other");
        ctx.Acls.AnimationChains.Single().Name.ShouldBe("Current");
        ctx.ProjectManager.AnimationChainListSave.ShouldBeSameAs(ctx.Acls);
    }
}

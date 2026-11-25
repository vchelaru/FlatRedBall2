using Xunit;
using AnimationEditor.Core;
using AnimationEditor.Core.IO;
using Shouldly;

namespace AnimationEditor.Editing.Tests;

/// <summary>Copying a texture next to the .achx when a file there is in use by another program.</summary>
public class TextureCopyLockedTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("ae-locked").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public async Task ResolveForProject_PickedFileIsGone_ReportsInsteadOfThrowing()
    {
        string project = Directory.CreateDirectory(Path.Combine(_root, "project")).FullName;
        File.WriteAllBytes(Path.Combine(project, "sheet.png"), [1, 2, 3]);
        ProjectManager projectManager = new ProjectManager { FileName = Path.Combine(project, "hero.achx") };
        string gone = Path.Combine(_root, "sheet.png");

        FrameTextureEdit.ResolveForProject(projectManager, gone, TextureCopyChoice.Copy)
            .Result.HasError(EditErrorCode.TextureCopyFailed).ShouldBeTrue();
        (await FrameTextureEdit.ResolveForProjectAsync(projectManager, gone, _ => Task.FromResult(TextureCopyChoice.Copy)))
            .Result.HasError(EditErrorCode.TextureCopyFailed).ShouldBeTrue();
    }

    [Fact]
    public async Task ResolveForProjectAsync_SameNamedFileInUse_ReportsInsteadOfThrowing()
    {
        string project = Directory.CreateDirectory(Path.Combine(_root, "project")).FullName;
        string picked = Path.Combine(_root, "sheet.png");
        File.WriteAllBytes(picked, [1, 2, 3]);
        string existing = Path.Combine(project, "sheet.png");
        File.WriteAllBytes(existing, [1, 2, 3]);
        ProjectManager projectManager = new ProjectManager { FileName = Path.Combine(project, "hero.achx") };
        using FileStream held = new FileStream(existing, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        // The PNG drop handler is async void, so an exception here goes unhandled.
        TextureResolveResult? result = null;
        await Should.NotThrowAsync(async () => result = await FrameTextureEdit.ResolveForProjectAsync(
            projectManager, picked, _ => Task.FromResult(TextureCopyChoice.KeepInPlace)));

        result.ShouldNotBeNull().Result.HasError(EditErrorCode.TextureCopyFailed).ShouldBeTrue();
    }
}

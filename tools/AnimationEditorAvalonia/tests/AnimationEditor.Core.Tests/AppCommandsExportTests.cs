using AnimationEditor.Core.Export;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.Core.Tests;

[Collection("SequentialSingletons")]
public class AppCommandsExportTests : IDisposable
{
    private readonly TestHelpers.TempDir _dir;

    public AppCommandsExportTests() => _dir = new TestHelpers.TempDir();

    public void Dispose() => _dir.Dispose();

    [Fact]
    public async Task ExportAsync_PixiJs_WhenDialogCancelled_DoesNotWriteFile()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        ctx.AppCommands.FileDialogService = new StubFileDialogService(null);
        ctx.ProjectManager.AnimationChainListSave = ctx.Acls;

        await ctx.AppCommands.ExportAsync(ExportFormat.PixiJs);

        Assert.Empty(Directory.GetFiles(_dir.Path, "*.json"));
    }

    [Fact]
    public async Task ExportAsync_PixiJs_WhenExportDirDiffers_CopiesReferencedTexture()
    {
        var sourceDir = Path.Combine(_dir.Path, "src");
        var exportDir = Path.Combine(_dir.Path, "out");
        Directory.CreateDirectory(sourceDir);
        Directory.CreateDirectory(exportDir);
        File.WriteAllBytes(Path.Combine(sourceDir, "hero.png"), new byte[] { 1, 2, 3 });

        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        // Pixel coords so the export needs no on-disk PNG to resolve sizes.
        acls.CoordinateType = TextureCoordinateType.Pixel;
        acls.AnimationChains.Add(new AnimationChainSave
        {
            Name = "Walk",
            Frames = { new AnimationFrameSave { TextureName = "hero.png", RightCoordinate = 32f, BottomCoordinate = 32f } },
        });
        ctx.ProjectManager.AnimationChainListSave = acls;
        ctx.ProjectManager.FileName = Path.Combine(sourceDir, "hero.achx");
        ctx.AppCommands.FileDialogService = new StubFileDialogService(Path.Combine(exportDir, "sheet.json"));

        await ctx.AppCommands.ExportAsync(ExportFormat.PixiJs);

        Assert.True(File.Exists(Path.Combine(exportDir, "hero.png")));
    }

    [Fact]
    public async Task ExportAsync_PixiJs_WhenPathReturned_WritesParseableJsonWithAnimation()
    {
        var target = Path.Combine(_dir.Path, "sheet.json");
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        // Pixel coords so the export needs no on-disk PNG to resolve sizes.
        acls.CoordinateType = TextureCoordinateType.Pixel;
        acls.AnimationChains.Add(new AnimationChainSave
        {
            Name = "Walk",
            Frames = { new AnimationFrameSave { TextureName = "hero.png", RightCoordinate = 32f, BottomCoordinate = 32f } },
        });
        ctx.ProjectManager.AnimationChainListSave = acls;
        ctx.AppCommands.FileDialogService = new StubFileDialogService(target);

        await ctx.AppCommands.ExportAsync(ExportFormat.PixiJs);

        Assert.True(File.Exists(target));
        var sheet = JsonSerializer.Deserialize<PixiJsSpriteSheet>(File.ReadAllText(target))!;
        Assert.Equal(new System.Collections.Generic.List<string> { "Walk_0" }, sheet.Animations["Walk"]);
    }
    [Fact]
    public async Task ExportAsync_Godot_WritesTresCopiesTextureAndRaisesExportCompleted()
    {
        var sourceDir = Path.Combine(_dir.Path, "src");
        var exportDir = Path.Combine(_dir.Path, "godot");
        Directory.CreateDirectory(sourceDir);
        Directory.CreateDirectory(exportDir);
        File.WriteAllBytes(Path.Combine(sourceDir, "hero.png"), new byte[] { 1, 2, 3 });
        var target = Path.Combine(exportDir, "hero.tres");

        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        acls.CoordinateType = TextureCoordinateType.Pixel;
        acls.AnimationChains.Add(new AnimationChainSave
        {
            Name = "Walk",
            Frames = { new AnimationFrameSave { TextureName = "hero.png", RightCoordinate = 32f, BottomCoordinate = 32f, FrameLength = 0.1f } },
        });
        ctx.ProjectManager.AnimationChainListSave = acls;
        ctx.ProjectManager.FileName = Path.Combine(sourceDir, "hero.achx");
        ctx.AppCommands.FileDialogService = new StubFileDialogService(target);
        string? completedPath = null;
        ctx.AppCommands.ExportCompleted += (path, _) => completedPath = path;

        await ctx.AppCommands.ExportAsync(ExportFormat.Godot);

        File.ReadAllText(target).ShouldStartWith("[gd_resource type=\"SpriteFrames\" format=3]");
        File.Exists(Path.Combine(exportDir, "hero.png")).ShouldBeTrue();
        completedPath.ShouldBe(target);
    }
}

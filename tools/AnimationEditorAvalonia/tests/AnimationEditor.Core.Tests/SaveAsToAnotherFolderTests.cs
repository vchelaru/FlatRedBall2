using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Paths;
using AnimationEditor.DocumentMatrix;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>#2031: the frames of a document saved to another folder still name the same images.</summary>
public class SaveAsToAnotherFolderTests : IDisposable
{
    private readonly TestHelpers.TempDir _dir = new();
    private readonly TestServices _ctx = new();
    private readonly List<string> _messages = new();

    public SaveAsToAnotherFolderTests()
    {
        _ctx.AppCommands.SaveFailed += _messages.Add;
        _ctx.AppCommands.SaveAsRefused += _messages.Add;
    }

    public void Dispose()
    {
        _ctx.IoManager.DeleteRecoveryFile();
        _dir.Dispose();
    }

    [Theory]
    [InlineData("sheet.png", "../front/sheet.png")]
    [InlineData(@"art\sheet.png", "../front/art/sheet.png")]
    [InlineData("../moved/art/sheet.png", "art/sheet.png")]
    [InlineData("../shared/sheet.png", "../shared/sheet.png")]
    [InlineData(@"C:\game\front\sheet.png", @"C:\game\front\sheet.png")]
    [InlineData("//server/share/sheet.png", "//server/share/sheet.png")]
    [InlineData(@"\\server\share\sheet.png", @"\\server\share\sheet.png")]
    [InlineData("https://example.com/sheet.png", "https://example.com/sheet.png")]
    [InlineData("", "")]
    public void Rebase_AnotherFolder_NamesTheSameFile(string name, string expected) =>
        TexturePathHelper.Rebase(name, new FilePath(@"C:\game\front\"), new FilePath(@"C:\game\moved\")).ShouldBe(expected);

    [Fact]
    public void Rebase_AnotherDrive_IsAbsolute() =>
        TexturePathHelper.Rebase("art/sheet.png", new FilePath(@"C:\front\"), new FilePath(@"D:\moved\")).ShouldBe("C:/front/art/sheet.png");

    [Fact]
    public void Rebase_SameFolder_IsUnchanged() =>
        TexturePathHelper.Rebase(@"art\sheet.png", new FilePath(@"C:\front\"), new FilePath("c:/front/")).ShouldBe(@"art\sheet.png");

    [Theory]
    [InlineData(".achx", TextureCoordinateType.Pixel)]
    [InlineData(".achx", TextureCoordinateType.UV)]
    [InlineData(".achj", TextureCoordinateType.Pixel)]
    public async Task SaveAs_AnotherFolder_MixedNames_ResolveToTheSameFilesNowAndAfterAReload(string extension, TextureCoordinateType unit)
    {
        string front = Folder("front"), moved = Folder("front", "deep", "moved");
        string outside = Path.Combine(Folder("outside"), "sheet.png");
        Documents.WriteTextures(Path.Combine(front, "art"));
        string path = Path.Combine(front, "hero" + extension);
        Write(path, unit, "sheet.png", @"art\sheet.png", "./other.png", "../outside/sheet.png", outside, "");
        string[] files = [Path.Combine(front, "sheet.png"), Path.Combine(front, "art", "sheet.png"), Path.Combine(front, "other.png"), outside, outside];
        _ctx.ProjectManager.LoadAnimationChain(new FilePath(path));
        var document = _ctx.ProjectManager.AnimationChainListSave!;
        document.ProjectFile = "../Game.gluj";
        string target = Path.Combine(moved, "hero" + extension);

        await SaveAsAsync(target);
        Names(document).ShouldAllBe(name => !Path.IsPathRooted(name));
        Resolved(document, moved).ShouldBe(files.Select(file => new FilePath(file).FullPath));
        document.ProjectFile.ShouldBe("../../../Game.gluj");
        var saved = AnimationChainListSave.FromFile(target);
        Names(saved).ShouldBe(Names(document));
        saved.ProjectFile.ShouldBe("../../../Game.gluj");

        // The next save does not move them again.
        byte[] bytes = File.ReadAllBytes(target);
        _ctx.AppCommands.SaveCurrentAnimationChainList().ShouldBeTrue();
        File.ReadAllBytes(target).ShouldBe(bytes);

        _ctx.AppCommands.ReloadAchxFromDisk(target);
        Resolved(_ctx.ProjectManager.AnimationChainListSave!, moved).ShouldBe(files.Select(file => new FilePath(file).FullPath));
        _messages.ShouldBeEmpty();
    }

    [Fact]
    public async Task SaveAs_AnotherFolder_ClearsUndoAndRedo_WhichNameTexturesFromTheOldFolder()
    {
        string front = Folder("front"), moved = Folder("moved");
        string path = Path.Combine(front, "hero.achx");
        Write(path, TextureCoordinateType.Pixel, "sheet.png", "other.png");
        _ctx.ProjectManager.LoadAnimationChain(new FilePath(path));
        var chain = _ctx.ProjectManager.AnimationChainListSave!.AnimationChains[0];
        _ctx.AppCommands.DeleteFrames([chain.Frames[1]]);
        _ctx.AppCommands.AddFrameFromPixelBounds(chain, "other.png", 0, 0, 16, 16);
        _ctx.UndoManager.Undo();
        string target = Path.Combine(moved, "hero.achx");

        await SaveAsAsync(target);

        _ctx.UndoManager.CanUndo.ShouldBeFalse();
        _ctx.UndoManager.CanRedo.ShouldBeFalse();
        _ctx.UndoManager.SaveState.ShouldBe(SaveState.AutoSaveOn);

        // An edit made there is undone and redone there.
        _ctx.AppCommands.DeleteFrames([chain.Frames[0]]);
        _ctx.UndoManager.Undo();
        Resolved(AnimationChainListSave.FromFile(target), moved).ShouldBe([new FilePath(Path.Combine(front, "sheet.png")).FullPath]);
    }

    [Fact]
    public async Task SaveAs_SameFolder_WritesTheSameBytesAndKeepsUndo()
    {
        string path = Path.Combine(Folder("front"), "hero.achx");
        Write(path, TextureCoordinateType.Pixel, "sheet.png", "../outside/sheet.png");
        _ctx.ProjectManager.LoadAnimationChain(new FilePath(path));
        var chain = _ctx.ProjectManager.AnimationChainListSave!.AnimationChains[0];
        _ctx.AppCommands.DeleteFrames([chain.Frames[1]]);
        string copy = Path.Combine(Folder("front"), "copy.achx");

        await SaveAsAsync(copy);
        File.ReadAllBytes(copy).ShouldBe(File.ReadAllBytes(path));

        _ctx.UndoManager.Undo();
        Names(AnimationChainListSave.FromFile(copy)).ShouldBe(["sheet.png", "../outside/sheet.png"]);
    }

    [Fact]
    public async Task SaveAs_Untitled_KeepsUndo_WhoseTexturesResolveInTheNewFolder()
    {
        string sheet = Path.Combine(Folder("front"), "sheet.png"), moved = Folder("moved");
        var document = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        document.AnimationChains.Add(Chain(sheet, sheet, "other.png"));
        _ctx.ProjectManager.ResetToBlankDocument();
        _ctx.ProjectManager.AnimationChainListSave = document;
        _ctx.AppCommands.DeleteFrames([document.AnimationChains[0].Frames[1]]);
        string target = Path.Combine(moved, "hero.achj");

        await SaveAsAsync(target);
        _ctx.UndoManager.Undo();

        // A relative name had no folder to be relative to.
        string[] same = [new FilePath(sheet).FullPath, new FilePath(sheet).FullPath, new FilePath(Path.Combine(moved, "other.png")).FullPath];
        Resolved(document, moved).ShouldBe(same);
        Names(AnimationChainListSave.FromFile(target)).ShouldBe(["../front/sheet.png", "../front/sheet.png", "other.png"]);
        _ctx.UndoManager.Redo();
        _ctx.UndoManager.Undo();
        Resolved(document, moved).ShouldBe(same);
    }

    [Fact]
    public async Task SaveAs_AnotherFolder_ThatFails_LeavesEveryRelativePathAndUndo()
    {
        string path = Path.Combine(Folder("front"), "hero.achx");
        Write(path, TextureCoordinateType.Pixel, "sheet.png", "other.png");
        _ctx.ProjectManager.LoadAnimationChain(new FilePath(path));
        var document = _ctx.ProjectManager.AnimationChainListSave!;
        document.ProjectFile = "../Game.gluj";
        _ctx.AppCommands.DeleteFrames([document.AnimationChains[0].Frames[1]]);
        string target = Path.Combine(Folder("front", "moved"), "hero.achx");
        Directory.CreateDirectory(target);

        await SaveAsAsync(target);

        _messages.Count.ShouldBe(1);
        Names(document).ShouldBe(["sheet.png"]);
        document.ProjectFile.ShouldBe("../Game.gluj");
        _ctx.UndoManager.CanUndo.ShouldBeTrue();
    }

    private string Folder(params string[] names)
    {
        string folder = Path.Combine([_dir.Path, .. names]);
        Documents.WriteTextures(folder);
        return folder;
    }

    private Task SaveAsAsync(string path)
    {
        _ctx.AppCommands.FileDialogService = new StubFileDialogService(path);
        return _ctx.AppCommands.SaveCurrentAnimationChainListAsync();
    }

    private static void Write(string path, TextureCoordinateType unit, params string[] textures)
    {
        var list = new AnimationChainListSave { CoordinateType = unit };
        list.AnimationChains.Add(Chain(textures));
        if (unit == TextureCoordinateType.UV)
            foreach (var frame in list.AnimationChains[0].Frames)
                frame.RightCoordinate = frame.BottomCoordinate = 0.25f;
        if (path.EndsWith(".achj")) list.SaveJson(path);
        else list.Save(path);
    }

    private static AnimationChainSave Chain(params string[] textures)
    {
        var chain = new AnimationChainSave { Name = "Walk" };
        foreach (string texture in textures)
            chain.Frames.Add(new AnimationFrameSave { TextureName = texture, FrameLength = 0.1f, RightCoordinate = 16, BottomCoordinate = 16 });
        return chain;
    }

    private static List<string> Names(AnimationChainListSave document) =>
        document.AnimationChains.SelectMany(chain => chain.Frames).Select(frame => frame.TextureName).ToList();

    private static IEnumerable<string> Resolved(AnimationChainListSave document, string folder) =>
        Names(document).Where(name => name.Length > 0).Select(name => new FilePath(Path.Combine(folder, name)).FullPath);
}

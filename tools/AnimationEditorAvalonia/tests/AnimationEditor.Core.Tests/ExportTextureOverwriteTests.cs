using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Export;
using AnimationEditor.Core.Models;
using AnimationEditor.DocumentMatrix;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Core.Tests;

[Collection("SequentialSingletons")]
public class ExportTextureOverwriteTests : IDisposable
{
    private readonly TestHelpers.TempDir _dir = new();
    private readonly TestServices _ctx = new();

    public void Dispose() => _dir.Dispose();

    private string Folder(string name) => Directory.CreateDirectory(Path.Combine(_dir.Path, name)).FullName;

    private void OpenInFolder(string folder, string texture)
    {
        _ctx.Acls.AnimationChains.Add(new AnimationChainSave
        {
            Name = "Walk",
            Frames = { new AnimationFrameSave { TextureName = texture, RightCoordinate = 16f, BottomCoordinate = 16f, FrameLength = 0.1f } },
        });
        _ctx.ProjectManager.FileName = Path.Combine(folder, "hero.achx");
    }

    [Theory]
    [InlineData("hero.png", "out/hero.png", false)]
    [InlineData("../art/hero.png", "art2/hero.png", false)]
    [InlineData("hero.png", "out/hero.png", true)]
    public async Task ExportAsync_AnotherTextureOfTheNameIsThere_AsksBeforeReplacingIt(string texture, string there, bool overwrite)
    {
        string source = Folder("game/anims");
        string art = Path.GetFullPath(Path.Combine(source, texture));
        Directory.CreateDirectory(Path.GetDirectoryName(art)!);
        File.WriteAllBytes(art, [1, 2, 3]);
        string export = there.StartsWith("out") ? Folder("out") : Folder("sub/out");
        string other = there.StartsWith("out") ? Path.Combine(export, "hero.png") : Path.Combine(Folder("sub/art"), "hero.png");
        File.WriteAllBytes(other, [9, 9]);
        OpenInFolder(source, texture);
        string? asked = null;
        _ctx.AppCommands.ConfirmAsync = (message, _) => { asked = message; return Task.FromResult(overwrite); };
        _ctx.AppCommands.FileDialogService = new StubFileDialogService(Path.Combine(export, "sheet.json"));

        await _ctx.AppCommands.ExportAsync(ExportFormat.PixiJs);

        File.ReadAllBytes(other).ShouldBe(overwrite ? [1, 2, 3] : new byte[] { 9, 9 });
        asked.ShouldNotBeNull().ShouldContain(other);
        File.Exists(Path.Combine(export, "sheet.json")).ShouldBeTrue();
    }

    [Fact]
    public async Task ExportAsync_TheSameImageIsThere_DoesNotAsk()
    {
        string source = Folder("anims");
        string export = Folder("out");
        File.WriteAllBytes(Path.Combine(source, "hero.png"), [1, 2, 3]);
        File.WriteAllBytes(Path.Combine(export, "hero.png"), [1, 2, 3]);
        OpenInFolder(source, "hero.png");
        _ctx.AppCommands.ConfirmAsync = (message, _) => throw new InvalidOperationException(message);
        _ctx.AppCommands.FileDialogService = new StubFileDialogService(Path.Combine(export, "sheet.json"));

        await _ctx.AppCommands.ExportAsync(ExportFormat.PixiJs);

        File.Exists(Path.Combine(export, "sheet.json")).ShouldBeTrue();
    }

    [Fact]
    public async Task ExportAsync_ATextureCannotBeCopied_ThrowsAndWritesNoExport()
    {
        string source = Folder("anims");
        string export = Folder("out");
        File.WriteAllBytes(Path.Combine(source, "hero.png"), [1, 2, 3]);
        Directory.CreateDirectory(Path.Combine(export, "hero.png")); // a folder of the name, so the copy fails on every OS
        OpenInFolder(source, "hero.png");
        _ctx.AppCommands.FileDialogService = new StubFileDialogService(Path.Combine(export, "sheet.json"));

        await Should.ThrowAsync<Exception>(() => _ctx.AppCommands.ExportAsync(ExportFormat.PixiJs));

        File.Exists(Path.Combine(export, "sheet.json")).ShouldBeFalse();
    }
}

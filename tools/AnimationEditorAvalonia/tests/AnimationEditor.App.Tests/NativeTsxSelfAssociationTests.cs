using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using AnimationEditor.Core;
using AnimationEditor.Core.IO;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.AnimationEditorCommon;
using System;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Fresh-eyes pass #14 (issue #1147) -- stress-tests the native-tsx/achx-push coexistence guard's
/// "direction 2" check (<c>MainWindow.WireAppCommands</c>'s <c>IsTsxPathOpenAsNativeProject</c>
/// delegate) against the degenerate case the Core-layer tests stub around: a tsx tab associating
/// ITSELF via "Associate Tiled Tileset," using the real delegate wired from live
/// <see cref="MainWindow"/>/<c>ProjectManager</c> state rather than a hand-supplied stub. Without
/// this check, a self-referential ".tiledsync" would get written next to the tsx's own file,
/// permanently locking it out of ever being reopened natively again (every future
/// <c>LoadTsxProject</c> call would see its own association and refuse to open).
/// </summary>
public class NativeTsxSelfAssociationTests
{
    private const string TsxFixtureXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <tileset version="1.10" tiledversion="1.12.2" name="Heroes" tilewidth="16" tileheight="16" tilecount="16" columns="4">
         <image source="Heroes.png" width="64" height="64"/>
         <tile id="0">
          <animation>
           <frame tileid="0" duration="200"/>
           <frame tileid="1" duration="200"/>
          </animation>
         </tile>
        </tileset>
        """;

    private static (MainWindow Window, TestServices Ctx) CreateWindow()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.FileName               = null;
        ctx.SelectedState.SelectedChain           = null;
        ctx.SelectedState.SelectedFrame           = null;
        ctx.SelectedState.SelectedNodes           = new List<object>();
        ctx.AppCommands.ConfirmAsync              = (_, _) => Task.FromResult(true);
        ctx.AppCommands.FileDialogService         = NullFileDialogService.Instance;

        var window = ctx.CreateMainWindow();
        window.Show();
        return (window, ctx);
    }

    [AvaloniaFact]
    public void AddAssociatedTiledTileset_TargetIsTheCurrentlyOpenTsxItself_ThrowsInsteadOfWritingSelfReferentialTiledSync()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var (window, ctx) = CreateWindow();
        try
        {
            var path = Path.Combine(dir, "Heroes.tsx");
            File.WriteAllText(path, TsxFixtureXml);

            typeof(MainWindow)
                .GetMethod("LoadAnimationFileAsync", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(window, [path, false]);
            Dispatcher.UIThread.RunJobs();
            Assert.True(ctx.ProjectManager.IsNativeTsxProject);

            var ex = Assert.Throws<InvalidOperationException>(
                () => ctx.AppCommands.AddAssociatedTiledTileset(path));

            Assert.Contains(path, ex.Message);
            // No .tiledsync companion file must have been written -- a self-referential
            // association would permanently block this exact file from ever being reopened
            // natively again (every future LoadTsxProject call would see its own association).
            Assert.Empty(ctx.IoManager.GetAssociatedTiledTilesetPaths(path));
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }
}

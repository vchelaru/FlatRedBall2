using AnimationEditor.Core;
using AnimationEditor.Core.IO;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using DotTiled;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Headless coverage for the Inspector's owner-tile field and frame tile-id readout (issue #1182),
/// driven through the real <see cref="MainWindow"/> pipeline (<c>LoadAnimationFileAsync</c>, same
/// as <c>LoadTsxProjectTests</c>) so this proves real UI wiring, not just the underlying
/// <c>ProjectManager</c>/<c>AppCommands</c> logic already covered in Core.Tests.
/// </summary>
public class ChainTsxOwnerTileTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    private string _tsxPath = "";

    public ChainTsxOwnerTileTests() => Directory.CreateDirectory(_dir);
    public void Dispose() => Directory.Delete(_dir, true);

    // 4 columns, 16x16 tiles, 8 rows (tilecount=32, image 64x128). "RiseUp" is owned by tile 9,
    // but frame 0 is tile 8 (the "owner not first frame" pattern -- issue #1182's real repro).
    // "Idle" is a second chain owned by tile 20. Tile 16 is blank/unused.
    private const string TsxFixtureXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <tileset version="1.10" tiledversion="1.12.2" name="Heroes" tilewidth="16" tileheight="16" tilecount="32" columns="4">
         <image source="Heroes.png" width="64" height="128"/>
         <tile id="9">
          <properties>
           <property name="Name" value="RiseUp"/>
          </properties>
          <animation>
           <frame tileid="8" duration="300"/>
           <frame tileid="12" duration="300"/>
          </animation>
         </tile>
         <tile id="20">
          <properties>
           <property name="Name" value="Idle"/>
          </properties>
          <animation>
           <frame tileid="20" duration="200"/>
          </animation>
         </tile>
        </tileset>
        """;

    private (MainWindow Window, TestServices Ctx) OpenTsx()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.FileName = null;
        ctx.AppCommands.FileDialogService = NullFileDialogService.Instance;

        var window = ctx.CreateMainWindow();
        window.Show();

        _tsxPath = Path.Combine(_dir, "Heroes.tsx");
        File.WriteAllText(_tsxPath, TsxFixtureXml);
        typeof(MainWindow)
            .GetMethod("LoadAnimationFileAsync", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(window, [_tsxPath, false]);
        Dispatcher.UIThread.RunJobs();

        return (window, ctx);
    }

    private static Tileset Disk(string path) => DotTiled.Serialization.Loader.Default().LoadTileset(path);

    [AvaloniaFact]
    public void SelectingTsxChain_ShowsOwnerTileSection_WithCurrentOwnerFromFile()
    {
        var (window, ctx) = OpenTsx();
        try
        {
            var chain = ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Single(c => c.Name == "RiseUp");
            ctx.SelectedState.SelectedChain = chain;
            Dispatcher.UIThread.RunJobs();

            var section = window.FindControl<Border>("PropChainTsxOwnerSection")!;
            var input = window.FindControl<NumericUpDown>("PropChainTsxOwnerInput")!;

            Assert.True(section.IsVisible);
            Assert.Equal(9m, input.Value);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void SelectingAchxChain_HidesOwnerTileSection()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.FileName = null;
        ctx.AppCommands.DoOnUiThread = a => a();
        ctx.AppCommands.FileDialogService = NullFileDialogService.Instance;
        var chain = new AnimationChainSave { Name = "Walk" };
        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.AnimationChainListSave.AnimationChains.Add(chain);

        try
        {
            ctx.SelectedState.SelectedChain = chain;
            Dispatcher.UIThread.RunJobs();

            var section = window.FindControl<Border>("PropChainTsxOwnerSection")!;
            Assert.False(section.IsVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void EditingOwnerTileInput_ValidUnclaimedTile_MovesTheAnimationOnDisk()
    {
        var (window, ctx) = OpenTsx();
        try
        {
            var chain = ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Single(c => c.Name == "RiseUp");
            ctx.SelectedState.SelectedChain = chain;
            Dispatcher.UIThread.RunJobs();

            var input = window.FindControl<NumericUpDown>("PropChainTsxOwnerInput")!;
            input.Value = 16m;
            Dispatcher.UIThread.RunJobs();

            var error = window.FindControl<TextBlock>("PropChainTsxOwnerError")!;
            Assert.False(error.IsVisible);

            var onDisk = Disk(_tsxPath);
            var newOwner = onDisk.Tiles.Single(t => t.ID == 16);
            Assert.Equal([((uint)8, 300), ((uint)12, 300)], newOwner.Animation.Select(f => (f.TileID, f.Duration)));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void EditingOwnerTileInput_TileAlreadyOwnedByAnotherChain_ShowsInlineError()
    {
        var (window, ctx) = OpenTsx();
        try
        {
            var chain = ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Single(c => c.Name == "RiseUp");
            ctx.SelectedState.SelectedChain = chain;
            Dispatcher.UIThread.RunJobs();

            var input = window.FindControl<NumericUpDown>("PropChainTsxOwnerInput")!;
            input.Value = 20m; // "Idle"'s own owner tile
            Dispatcher.UIThread.RunJobs();

            var error = window.FindControl<TextBlock>("PropChainTsxOwnerError")!;
            Assert.True(error.IsVisible);
            Assert.Contains("Idle", error.Text);

            // Rejected: the owner tile on disk must still be 9, untouched.
            var onDisk = Disk(_tsxPath);
            Assert.Equal([((uint)8, 300), ((uint)12, 300)], onDisk.Tiles.Single(t => t.ID == 9).Animation.Select(f => (f.TileID, f.Duration)));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ClickingSyncButton_SetsOwnerToFirstFrameTileId_AndMovesTheAnimation()
    {
        var (window, ctx) = OpenTsx();
        try
        {
            var chain = ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Single(c => c.Name == "RiseUp");
            ctx.SelectedState.SelectedChain = chain;
            Dispatcher.UIThread.RunJobs();

            var syncButton = window.FindControl<Button>("PropChainTsxOwnerSyncButton")!;
            syncButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            var input = window.FindControl<NumericUpDown>("PropChainTsxOwnerInput")!;
            Assert.Equal(8m, input.Value); // frame 0's own tile id

            var onDisk = Disk(_tsxPath);
            Assert.Equal([((uint)8, 300), ((uint)12, 300)], onDisk.Tiles.Single(t => t.ID == 8).Animation.Select(f => (f.TileID, f.Duration)));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void SelectingFrame_ShowsItsOwnTileId_DistinctFromTheChainsOwnerTile()
    {
        var (window, ctx) = OpenTsx();
        try
        {
            var chain = ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Single(c => c.Name == "RiseUp");
            ctx.SelectedState.SelectedChain = chain;
            ctx.SelectedState.SelectedFrame = chain.Frames[0];
            Dispatcher.UIThread.RunJobs();

            var tileText = window.FindControl<TextBlock>("PropFrameTsxTileText")!;
            Assert.True(tileText.IsVisible);
            Assert.Contains("8", tileText.Text); // frame 0 is tile 8, distinct from the chain's owner tile (9)
        }
        finally { window.Close(); }
    }
}

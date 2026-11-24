using AnimationEditor.Core.Utilities;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Editing.Tests;

public class TsxEditTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "AeTsxEditTests", Guid.NewGuid().ToString("N"));

    public TsxEditTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private string Copy(string fixture)
    {
        var dest = Path.Combine(_dir, fixture);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", fixture), dest, overwrite: true);
        return dest;
    }

    private async Task<EditSession> OpenTsx()
    {
        Copy("sheet.png");
        var open = await EditSession.OpenAsync(Copy("tiles.tsx"));
        open.Result.Success.ShouldBeTrue(string.Join("; ", open.Result.Errors));
        return open.Session!;
    }

    [Fact]
    public async Task OpenAsync_Tsx_IsANativeProjectWithItsAnimatedTilesAsChains()
    {
        var s = await OpenTsx();

        s.IsNativeTsx.ShouldBeTrue();
        s.Document.AnimationChains.Select(c => c.Name).ShouldBe(["Walk", "ID:5"]);
    }

    [Fact]
    public async Task OpenAsync_UnsupportedTsx_IsRefusedWithTheCheckersMessage()
    {
        Copy("sheet.png");
        var open = await EditSession.OpenAsync(Copy("collection.tsx"));

        open.Session.ShouldBeNull();
        open.Result.HasError(EditErrorCode.UnsupportedTsx).ShouldBeTrue();
        open.Result.Errors[0].ShouldContain("image collection");
    }

    [Fact]
    public async Task SetChainOwnerTile_MovesTheOwnerInOneUndoStep()
    {
        var s = await OpenTsx();
        var walk = s.Document.AnimationChains[0];

        var result = TsxEdit.SetChainOwnerTile(s.ProjectManager, s.Commands, walk, 2);

        result.Success.ShouldBeTrue(string.Join("; ", result.Errors));
        result.ChangedItems.ShouldBe(["Walk"]);
        s.ProjectManager.GetTsxOwnerTileId(walk).ShouldBe(2u);
        s.Undo.Undo();
        s.ProjectManager.GetTsxOwnerTileId(walk).ShouldBe(0u);
        s.Undo.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public async Task SetChainOwnerTile_AlreadyThere_IsANoOp()
    {
        var s = await OpenTsx();
        var walk = s.Document.AnimationChains[0];

        var result = TsxEdit.SetChainOwnerTile(s.ProjectManager, s.Commands, walk, 0);

        result.Success.ShouldBeTrue();
        result.ChangedItems.ShouldBeEmpty();
        s.Undo.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public async Task SetChainOwnerTile_TakenByAnotherChain_FailsWithoutChange()
    {
        var s = await OpenTsx();
        var walk = s.Document.AnimationChains[0];

        var result = TsxEdit.SetChainOwnerTile(s.ProjectManager, s.Commands, walk, 5);

        result.HasError(EditErrorCode.InvalidValue).ShouldBeTrue();
        result.Errors[0].ShouldContain("already the owner");
        s.ProjectManager.GetTsxOwnerTileId(walk).ShouldBe(0u);
        s.Undo.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public async Task SetChainOwnerTile_PastTheTileset_Fails()
    {
        var s = await OpenTsx();

        var result = TsxEdit.SetChainOwnerTile(s.ProjectManager, s.Commands, s.Document.AnimationChains[0], 6);

        result.HasError(EditErrorCode.InvalidValue).ShouldBeTrue();
    }

    [Fact]
    public async Task ClearChainOwnerTile_ReturnsTheOwnerToTheFirstFramesTile()
    {
        var s = await OpenTsx();
        var walk = s.Document.AnimationChains[0];
        TsxEdit.SetChainOwnerTile(s.ProjectManager, s.Commands, walk, 2).Success.ShouldBeTrue();

        var result = TsxEdit.ClearChainOwnerTile(s.ProjectManager, s.Commands, walk);

        result.Success.ShouldBeTrue(string.Join("; ", result.Errors));
        s.ProjectManager.GetTsxOwnerTileId(walk).ShouldBe(0u);
    }

    [Fact]
    public void SetChainOwnerTile_OutsideATsxProject_IsRefused()
    {
        using var g = new EditingTestGraph();
        var chain = new AnimationChainSave { Name = "Walk" };
        g.ProjectManager.AnimationChainListSave = new() { AnimationChains = { chain } };

        var result = TsxEdit.SetChainOwnerTile(g.ProjectManager, g.Commands, chain, 1);

        result.HasError(EditErrorCode.WrongFileKind).ShouldBeTrue();
    }

    [Fact]
    public async Task Save_TsxWarnings_ArriveInTheResult()
    {
        var s = await OpenTsx();
        var walk = s.Document.AnimationChains[0];
        // A rect off the tile grid cannot be written back as a tile animation.
        s.Commands.SetFramePixelRegion([walk.Frames[0]], (NumericEdit)1f, (NumericEdit)1f, (NumericEdit)15f, (NumericEdit)15f);

        var save = s.Save();

        save.Success.ShouldBeTrue(string.Join("; ", save.Errors));
        save.Warnings.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task AssociateTileset_WritesTheCompanionFile()
    {
        Copy("sheet.png");
        Copy("tiles.tsx");
        var s = (await EditSession.OpenAsync(Copy("hero.achx"))).Session!;

        var result = TsxEdit.AssociateTileset(s.Commands, s.ProjectManager, Path.Combine(_dir, "tiles.tsx"));

        result.Success.ShouldBeTrue(string.Join("; ", result.Errors));
        Directory.GetFiles(_dir, "*.tiledsync").Length.ShouldBe(1);
    }

    [Fact]
    public async Task AssociateTileset_MissingFile_FailsAndWritesNothing()
    {
        Copy("sheet.png");
        var s = (await EditSession.OpenAsync(Copy("hero.achx"))).Session!;

        var result = TsxEdit.AssociateTileset(s.Commands, s.ProjectManager, Path.Combine(_dir, "nope.tsx"));

        result.HasError(EditErrorCode.NotFound).ShouldBeTrue();
        Directory.GetFiles(_dir, "*.tiledsync").ShouldBeEmpty();
    }

    [Fact]
    public async Task AssociateTileset_InATsxProject_IsRefused()
    {
        var s = await OpenTsx();

        var result = TsxEdit.AssociateTileset(s.Commands, s.ProjectManager, Path.Combine(_dir, "tiles.tsx"));

        result.HasError(EditErrorCode.WrongFileKind).ShouldBeTrue();
    }

    [Fact]
    public async Task AssociateTileset_ThenOpeningThatTsxNatively_IsRefusedUpFront()
    {
        Copy("sheet.png");
        Copy("tiles.tsx");
        var s = (await EditSession.OpenAsync(Copy("hero.achx"))).Session!;
        TsxEdit.AssociateTileset(s.Commands, s.ProjectManager, Path.Combine(_dir, "tiles.tsx")).Success.ShouldBeTrue();

        var open = await EditSession.OpenAsync(Path.Combine(_dir, "tiles.tsx"));

        open.Session.ShouldBeNull();
        open.Result.Errors[0].ShouldContain("associated");
    }
}

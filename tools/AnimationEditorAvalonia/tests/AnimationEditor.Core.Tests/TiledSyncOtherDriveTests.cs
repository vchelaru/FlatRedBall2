using AnimationEditor.Core.Data;
using AnimationEditor.Core.Tiled;
using AnimationEditor.DocumentMatrix;
using Shouldly;
using System;
using System.IO;
using System.Text.Json;
using Xunit;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Core.Tests;

/// <summary>#2060: a tileset that has no path relative to the document is stored with its full path.</summary>
[Collection("SequentialSingletons")]
public class TiledSyncOtherDriveTests : IDisposable
{
    private readonly TestHelpers.TempDir _dir = new();
    private readonly TestServices _ctx = new();

    public void Dispose() => _dir.Dispose();

    [Fact]
    public void AddAssociatedTiledTileset_OnAnotherDrive_IsFoundAgain()
    {
        if (!OperatingSystem.IsWindows()) return;
        string achx = Documents.Write(_dir.Path, DocType.Achx, "source");

        _ctx.IoManager.AddAssociatedTiledTilesetPath(achx, "Q:/maps/Heroes.tsx");
        _ctx.IoManager.AddAssociatedTiledTilesetPath(achx, "Q:/maps/Heroes.tsx");

        _ctx.IoManager.GetAssociatedTiledTilesetPaths(achx).ShouldBe(new[] { "Q:/maps/Heroes.tsx" });
    }

    [Fact]
    public void AddAssociatedTiledTileset_OnANetworkShare_IsFoundAgain()
    {
        if (!OperatingSystem.IsWindows()) return;
        string achx = Documents.Write(_dir.Path, DocType.Achx, "source");

        _ctx.IoManager.AddAssociatedTiledTilesetPath(achx, "//server/maps/Heroes.tsx");
        _ctx.IoManager.AddAssociatedTiledTilesetPath(achx, "//server/maps/Heroes.tsx");

        _ctx.IoManager.GetAssociatedTiledTilesetPaths(achx).ShouldBe(new[] { "//server/maps/Heroes.tsx" });
    }

    [Fact]
    public void FindAssociationsTargeting_StoredWithItsFullPath_IsFound()
    {
        string achx = Documents.Write(_dir.Path, DocType.Achx, "source");
        string tsx = Documents.Write(_dir.Path, DocType.Tsx, "Heroes");
        File.WriteAllText(Path.ChangeExtension(achx, ".tiledsync"),
            JsonSerializer.Serialize(new AETiledSyncSave { TiledTilesetPaths = { tsx } }));

        TiledSyncAssociationScanner.FindAssociationsTargeting(tsx).ShouldHaveSingleItem().ShouldBe(new FilePath(achx).FullPath);
    }
}

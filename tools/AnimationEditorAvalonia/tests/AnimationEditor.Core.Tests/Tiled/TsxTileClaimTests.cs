using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using DotTiled;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Core.Tests.Tiled;

/// <summary>
/// Issue #2038: no edit the editor accepts may leave a native <c>.tsx</c> in a state where every save throws.
/// Two chains wanting one tile is always settled by the save, with a warning naming the one that was not saved.
/// </summary>
[Collection("SequentialSingletons")]
public class TsxTileClaimTests : IDisposable
{
    private const int Columns = 4;
    private const int TileCount = 16;

    private readonly TestHelpers.TempDir _dir = new();

    public void Dispose() => _dir.Dispose();

    private string WriteTsx(string tiles)
    {
        var path = Path.Combine(_dir.Path, "Heroes.tsx");
        File.WriteAllText(path, $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <tileset version="1.10" tiledversion="1.12.2" name="Heroes" tilewidth="16" tileheight="16" tilecount="{TileCount}" columns="{Columns}">
             <image source="Heroes.png" width="64" height="64"/>
            {tiles}
            </tileset>
            """);
        return path;
    }

    private static string Tile(int id, string properties = "") =>
        $"<tile id=\"{id}\">{properties}<animation><frame tileid=\"{id}\" duration=\"200\"/></animation></tile>";

    private static ProjectManager Load(string path)
    {
        var pm = new ProjectManager();
        pm.LoadTsxProject(new FilePath(path));
        return pm;
    }

    private static AnimationChainSave Chain(ProjectManager pm, string name) =>
        pm.AnimationChainListSave!.AnimationChains.Single(c => c.Name == name);

    private static AnimationChainSave NewChain(ProjectManager pm, string name, int tile)
    {
        var chain = new AnimationChainSave { Name = name };
        chain.Frames.Add(new AnimationFrameSave { TextureName = "Heroes.png", FrameLength = 0.2f });
        Place(chain, tile);
        pm.AnimationChainListSave!.AnimationChains.Add(chain);
        return chain;
    }

    private static void Place(AnimationChainSave chain, int tile, int columns = 1)
    {
        foreach (var frame in chain.Frames)
        {
            frame.LeftCoordinate = tile % Columns * 16;
            frame.TopCoordinate = tile / Columns * 16;
            frame.RightCoordinate = frame.LeftCoordinate + (columns * 16);
            frame.BottomCoordinate = frame.TopCoordinate + 16;
        }
    }

    private static Tileset Disk(string path) => DotTiled.Serialization.Loader.Default().LoadTileset(path);

    private static uint[] AnimatedTiles(string path) =>
        Disk(path).Tiles.Where(t => t.Animation.Count > 0).Select(t => t.ID).Order().ToArray();

    private static string? NameOn(string path, uint tile) =>
        Disk(path).Tiles.Single(t => t.ID == tile).Properties.OfType<StringProperty>().FirstOrDefault(p => p.Name == "Name")?.Value;

    [Fact]
    public void TrySetTsxOwnerTileId_AnotherChainsSatellite_IsRefused()
    {
        var path = WriteTsx(Tile(0) + Tile(5));
        var pm = Load(path);
        Place(Chain(pm, "ID:5"), 5, columns: 2);
        pm.SaveTsxProject();

        var error = pm.TrySetTsxOwnerTileId(Chain(pm, "ID:0"), 6);

        Assert.Empty(pm.SaveTsxProject());
        Assert.Equal("Tile 6 is already used by \"ID:5\".", error);
        Assert.Equal([0u, 5u, 6u], AnimatedTiles(path));
    }

    [Fact]
    public void TrySetTsxOwnerTileId_OwnSatelliteWouldLandOnAnotherChain_IsRefused()
    {
        var path = WriteTsx(Tile(0) + Tile(5));
        var pm = Load(path);
        Place(Chain(pm, "ID:0"), 0, columns: 2);
        pm.SaveTsxProject();

        var error = pm.TrySetTsxOwnerTileId(Chain(pm, "ID:0"), 4);

        Assert.Equal("Tile 5 is already used by \"ID:5\".", error);
        Assert.Equal((uint)0, pm.GetTsxOwnerTileId(Chain(pm, "ID:0")));
    }

    [Fact]
    public void SaveTsxProject_ChainPutBackOnATileAnotherChainTook_IsReportedAndTheTileKeepsItsNewChain()
    {
        var path = WriteTsx(Tile(0));
        var pm = Load(path);
        var a = Chain(pm, "ID:0");
        pm.AnimationChainListSave!.AnimationChains.Remove(a);
        NewChain(pm, "B", 0);
        pm.SaveTsxProject();

        pm.AnimationChainListSave.AnimationChains.Insert(0, a);
        var warning = Assert.Single(pm.SaveTsxProject());

        Assert.Contains("\"ID:0\": Tiled tile 0 already carries \"B\"", warning);
        Assert.Contains(a, pm.TsxChainsNotFullySaved);
        Assert.Equal("B", NameOn(path, 0));
        // The warning's advice works: moved to free cells, the chain is saved there, still without a name of its own.
        Place(a, 9);
        Assert.Empty(pm.SaveTsxProject());
        Assert.Empty(pm.SaveTsxProject());
        Assert.Equal([0u, 9u], AnimatedTiles(path));
        Assert.Null(NameOn(path, 9));
    }

    [Fact]
    public void SaveTsxProject_NewChainOnTheTileOfAChainThatCannotBeMapped_IsReported()
    {
        var path = WriteTsx(Tile(0));
        var pm = Load(path);
        Chain(pm, "ID:0").Frames[0].LeftCoordinate = 3;
        NewChain(pm, "B", 0);

        var warnings = pm.SaveTsxProject();

        Assert.Contains(warnings, w => w.Contains("\"B\": Tiled tile 0 already carries \"ID:0\""));
        Assert.Null(NameOn(path, 0));
        Assert.Equal([0u], AnimatedTiles(path));
    }

    [Fact]
    public void SaveTsxProject_OwnerTileSetOnAChainThatCannotBeMapped_KeepsItsAnimationOnDisk()
    {
        var path = WriteTsx(Tile(0));
        var pm = Load(path);
        var a = Chain(pm, "ID:0");
        a.Frames[0].LeftCoordinate = 3;
        Assert.Null(pm.TrySetTsxOwnerTileId(a, 7));
        pm.SaveTsxProject();

        Assert.Equal([0u], AnimatedTiles(path));
        // The owner tile the user set applies as soon as the chain can be mapped again.
        Place(a, 0);
        Assert.Empty(pm.SaveTsxProject());
        Assert.Equal([7u], AnimatedTiles(path));
    }

    [Fact]
    public void SaveTsxProject_ChainGrowsBackOntoATileAnotherChainTook_IsReportedAndKeepsItsOwnTile()
    {
        var path = WriteTsx(Tile(5));
        var pm = Load(path);
        var a = Chain(pm, "ID:5");
        Place(a, 5, columns: 2);
        pm.SaveTsxProject();
        Place(a, 5);
        NewChain(pm, "B", 6);
        pm.SaveTsxProject();

        Place(a, 5, columns: 2);
        var warning = Assert.Single(pm.SaveTsxProject());

        Assert.Contains("\"ID:5\": Tiled tile 6 already carries \"B\"", warning);
        Assert.Equal([5u, 6u], AnimatedTiles(path));
        Assert.Equal("B", NameOn(path, 6));
    }

    [Theory]
    [InlineData("<properties><property name=\"achjSourceFile\" value=\"Other.achx\"/></properties><animation><frame tileid=\"9\" duration=\"200\"/></animation>", 9)]
    [InlineData("<properties><property name=\"Name\" value=\"Mine\"/></properties>", 9)]
    [InlineData("<properties><property name=\"ParentId\" type=\"int\" value=\"2\"/></properties>", 9)]
    [InlineData("<properties><property name=\"ParentId\" type=\"int\" value=\"2\"/></properties>", 8)] // its satellite would land on 9
    public void TrySetTsxOwnerTileId_TileTheSaveRefuses_IsRefusedWithTheSameText(string tile9, int ownerTile)
    {
        var path = WriteTsx(Tile(0) + $"<tile id=\"9\">{tile9}</tile>");
        var pm = Load(path);
        var a = Chain(pm, "ID:0");
        Place(a, 0, columns: 2);
        pm.SaveTsxProject();

        var error = pm.TrySetTsxOwnerTileId(a, (uint)ownerTile);

        Assert.Equal((uint)0, pm.GetTsxOwnerTileId(a));
        Assert.Equal("ID:0", a.Name);
        // The save says the same about a chain whose own cells are that tile.
        pm.AnimationChainListSave!.AnimationChains.Remove(a);
        Place(NewChain(pm, $"ID:{ownerTile}", ownerTile), ownerTile, columns: 2);
        Assert.Equal(Assert.Throws<InvalidOperationException>(() => pm.SaveTsxProject()).Message, error);
    }

    [Fact]
    public void SaveTsxProject_TileAChainShrankOff_IsFreeWhileThatChainCannotBeMapped()
    {
        var path = WriteTsx(Tile(5));
        var pm = Load(path);
        var a = Chain(pm, "ID:5");
        Place(a, 5, columns: 2);
        pm.SaveTsxProject();
        Place(a, 5);
        pm.SaveTsxProject();
        a.Frames[0].LeftCoordinate += 3;
        NewChain(pm, "B", 6);

        Assert.DoesNotContain(pm.SaveTsxProject(), w => w.Contains("\"B\""));
        Assert.Equal("B", NameOn(path, 6));
    }

    [Fact]
    public void SaveTsxProject_ChainThatLostOnlyASatellite_KeepsItsOwnerTile()
    {
        var path = WriteTsx(Tile(0) + Tile(1));
        var pm = Load(path);
        var a = Chain(pm, "ID:0");
        Place(a, 8, columns: 2); // its owner tile stays 0, so its satellite belongs on 1
        Assert.Single(pm.SaveTsxProject());

        Place(a, 8);
        Assert.Empty(pm.SaveTsxProject());

        Assert.Equal([0u, 1u], AnimatedTiles(path));
    }

    [Fact]
    public void SaveTsxProject_OwnerTileSetByTheUserIsWantedByAnEarlierNewChain_TheOwnerTileWins()
    {
        var path = WriteTsx(Tile(0));
        var pm = Load(path);
        var a = Chain(pm, "ID:0");
        Assert.Null(pm.TrySetTsxOwnerTileId(a, 7));
        var b = NewChain(pm, "B", 7);
        pm.AnimationChainListSave!.AnimationChains.Remove(b);
        pm.AnimationChainListSave.AnimationChains.Insert(0, b);

        var warning = Assert.Single(pm.SaveTsxProject());

        Assert.Contains("\"B\": Tiled tile 7 already carries \"ID:7\"", warning);
    }

    [Fact]
    public void SaveTsxProject_ChainThatYieldsKeepsItsTilesInTheFile_EvenFromAChainAlreadySettled()
    {
        var path = WriteTsx(Tile(5));
        var pm = Load(path);
        var a = Chain(pm, "ID:5");
        Place(a, 5, columns: 2);
        pm.SaveTsxProject();
        // "A" would give up tile 6 to be 1x2 (tiles 5 and 9), but loses 9 to "D" and so stays 2x1 in the file.
        Place(a, 5);
        a.Frames[0].BottomCoordinate += 16;
        NewChain(pm, "C", 6);
        NewChain(pm, "D", 9);
        pm.AnimationChainListSave!.AnimationChains.Remove(a);
        pm.AnimationChainListSave.AnimationChains.Add(a);

        var warnings = pm.SaveTsxProject();

        Assert.Equal(2, warnings.Count);
        Assert.Contains(warnings, w => w.Contains("\"ID:5\": Tiled tile 9 already carries \"D\""));
        Assert.Contains(warnings, w => w.Contains("\"C\": Tiled tile 6 already carries \"ID:5\""));
        Assert.Equal([5u, 6u, 9u], AnimatedTiles(path));
    }

    [Fact]
    public void SaveTsxProject_NewChainThatYieldsElsewhere_DoesNotHoldItsOtherTiles()
    {
        var path = WriteTsx(Tile(5));
        var pm = Load(path);
        Place(NewChain(pm, "Wide", 4), 4, columns: 2);
        NewChain(pm, "B", 4);
        // Last in the list, so tile 4 comes up before tile 5.
        pm.AnimationChainListSave!.AnimationChains.Reverse();
        pm.AnimationChainListSave.AnimationChains.Reverse(0, 2);

        var warning = Assert.Single(pm.SaveTsxProject());

        Assert.Contains("\"Wide\": Tiled tile 5 already carries \"ID:5\"", warning);
        Assert.Equal("B", NameOn(path, 4));
    }

    [Fact]
    public void SaveTsxProject_LoadedGroupThatCannotBeMapped_KeepsItsSatelliteInTheFile()
    {
        var path = WriteTsx(Tile(5) + Tile(6, "<properties><property name=\"ParentId\" type=\"int\" value=\"5\"/></properties>"));
        var pm = Load(path);
        Chain(pm, "ID:5").Frames[0].LeftCoordinate = 3;

        Assert.Single(pm.SaveTsxProject());

        Assert.Equal([5u, 6u], AnimatedTiles(path));
    }

    [Fact]
    public void RestoreTsxState_ChainThatCannotBeMapped_KeepsItsTileInTheFile()
    {
        var path = WriteTsx(Tile(0));
        var pm = Load(path);
        var list = pm.AnimationChainListSave!;
        var state = pm.CaptureTsxState();
        pm.ResetToBlankDocument();
        pm.RestoreTsxState(state);
        pm.AnimationChainListSave = list;
        Chain(pm, "ID:0").Frames[0].LeftCoordinate = 3;

        Assert.Single(pm.SaveTsxProject(path));

        Assert.Equal([0u], AnimatedTiles(path));
    }

    [Fact]
    public void SaveTsxProject_FrameAddedWhileTheChainCannotBeMapped_ClearingAndPuttingBackKeepsTheOwnerTile()
    {
        var path = WriteTsx(Tile(0));
        var pm = Load(path);
        var a = Chain(pm, "ID:0");
        Place(a, 1);  // the owner tile stays 0
        var added = new AnimationFrameSave { TextureName = "Other.png", FrameLength = 0.2f, LeftCoordinate = 32, RightCoordinate = 48, BottomCoordinate = 16 };
        a.Frames.Add(added);
        Assert.Single(pm.SaveTsxProject());
        var frames = a.Frames.ToList();
        a.Frames.Clear();
        pm.SaveTsxProject();

        a.Frames.AddRange(frames);
        added.TextureName = "Heroes.png";
        pm.SaveTsxProject();

        Assert.Equal([0u], AnimatedTiles(path));
    }

    // Seeded, so a failure names a sequence that replays.
    [Fact]
    public void RandomEdits_EveryAcceptedStateIsSaved_AndEveryChainIsOnDiskOrReported()
    {
        for (var seed = 0; seed < 60; seed++)
            RunRandomEdits(seed, steps: 8);
    }

    private void RunRandomEdits(int seed, int steps)
    {
        var random = new Random(seed);
        var path = WriteTsx(Tile(0) + Tile(5) + Tile(10) );
        var ctx = new TestServices();
        var pm = ctx.ProjectManager;
        pm.LoadTsxProject(new FilePath(path));
        IReadOnlyList<string> warnings = [];
        ctx.AppCommands.TsxSaveCompletedWithWarnings += (_, w) => warnings = w;
        string? saveError = null;
        ctx.AppCommands.SaveFailed += e => saveError = e;
        var log = new StringBuilder($"seed {seed}");
        var setAside = new List<AnimationChainSave>();

        int Origin() => random.Next(Columns) * 16;
        int Size() => random.Next(8) == 0 ? 24 : random.Next(1, 3) * 16;
        void AddFrameTo(AnimationChainSave target)
        {
            var (left, top) = (Origin(), Origin());
            ctx.AppCommands.AddFrameFromPixelBounds(target, "Heroes.png", left, top, left + 16, top + 16);
        }

        for (var step = 0; step < steps; step++)
        {
            var chains = pm.AnimationChainListSave!.AnimationChains;
            var chain = chains.Count > 0 ? chains[random.Next(chains.Count)] : null;
            var withFrames = chains.Where(c => c.Frames.Count > 0).ToList();
            var framed = withFrames.Count > 0 ? withFrames[random.Next(withFrames.Count)] : null;
            var action = random.Next(17);
            log.Append($"\n{step}: action {action}, chain {chain?.Name}, chain with frames {framed?.Name}");
            switch (action)
            {
                case 0 or 1:
                    if (ctx.AppCommands.AddAnimationChainWithName($"N{step}") is { } added)
                        AddFrameTo(added);
                    break;
                case 2 when chain != null:
                    AddFrameTo(chain);
                    break;
                case 3 or 4 when framed != null:
                    ctx.AppCommands.SetFramePixelRegion(framed.Frames.ToList(), Origin(), Origin(), null, null);
                    break;
                case 5 or 6 when framed != null:
                    ctx.AppCommands.SetFramePixelRegion([framed.Frames[0]], null, null, Size(), Size());
                    break;
                case 7 when chain != null:
                    ctx.AppCommands.DeleteAnimationChains([chain]);
                    break;
                case 8 when framed != null:
                    ctx.AppCommands.DeleteFrames(framed.Frames.ToList());
                    break;
                case 9 when chain != null:
                    ctx.AppCommands.RenameChain(chain, $"R{step}");
                    break;
                case 10 or 11 when chain != null:
                    var tile = (uint)random.Next(TileCount);
                    log.Append($", owner {tile} {ctx.AppCommands.SetChainTsxOwnerTileId(chain, tile)}");
                    break;
                case 12:
                    ctx.UndoManager.Undo();
                    break;
                case 13:
                    ctx.UndoManager.Redo();
                    break;
                case 14 when framed != null:
                    ctx.AppCommands.DuplicateChains([framed]);
                    break;
                case 15 when chain != null:
                    chains.Remove(chain);
                    setAside.Add(chain);
                    break;
                case 16 when setAside.Count > 0:
                    chains.Insert(random.Next(chains.Count + 1), setAside[0]);
                    setAside.RemoveAt(0);
                    break;
            }

            // Every command above saves by itself; these two edit the model.
            if (action is 15 or 16)
                ctx.AppCommands.SaveCurrentAnimationChainList();
            Assert.True(saveError is null, $"{saveError}\n{log}");
        }

        warnings = [];
        ctx.AppCommands.SaveCurrentAnimationChainList();
        var onDisk = Disk(path).Tiles
            .Where(t => t.Animation.Count > 0 && t.Properties.All(p => p.Name != "ParentId"))
            .Select(t => (Name: t.Properties.OfType<StringProperty>().FirstOrDefault(p => p.Name == "Name")?.Value ?? $"ID:{t.ID}", t.Animation.Count))
            .ToHashSet();
        foreach (var expected in pm.AnimationChainListSave!.AnimationChains.Where(c => c.Frames.Count > 0))
        {
            var nameOnDisk = pm.IsChainNameAuto(expected) ? $"ID:{pm.GetTsxOwnerTileId(expected)}" : expected.Name;
            Assert.True(
                warnings.Any(w => w.Contains($"\"{expected.Name}\"")) || onDisk.Contains((nameOnDisk, expected.Frames.Count)),
                $"\"{expected.Name}\" is neither on disk nor reported.\n{log}");
        }
    }
}

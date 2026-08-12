using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using FlatRedBall2.Collision;
using FlatRedBall2.Glue;
using FlatRedBall2.Glue.Model;
using FlatRedBall2.Tiled;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Glue;

// Covers building a Glue project's tile content: the .tmx itself, and the TileShapeCollection
// objects whose settings live in a property bag rather than in instructions.
[Collection(GraphicsDeviceCollection.Name)]
public class GlueTiledTests
{
    private readonly GraphicsDeviceFixture _graphics;

    public GlueTiledTests(GraphicsDeviceFixture graphics) => _graphics = graphics;

    private static ScreenSave LoadFixtureScreen(string project, string fileName) =>
        JsonSerializer.Deserialize(
            File.ReadAllText(Path.Combine(
                AppContext.BaseDirectory, "Glue", "Fixtures", project, "Screens", fileName)),
            GlueJsonContext.Default.ScreenSave)!;

    private GlueScreen? BuiltLevel1()
    {
        if (!_graphics.IsAvailable)
            return null;

        var screen = new GlueScreen
        {
            Save = LoadFixtureScreen("DoorsDemo", "Level1.glsj"),
            Content = new GlueContentSource(
                _graphics.ContentLoader!, Path.Combine("Glue", "Fixtures", "DoorsDemo", "Content"),
                _graphics.GraphicsDevice),
        };

        screen.BuildObjects();
        return screen;
    }

    [Fact]
    public void BagDefaults_AbsentKeys_UseGluesOwnDefaultsNotZero()
    {
        // Glue falls back to the editor view-model's [DefaultValue] rather than to default(T). A
        // tile collection with a grid size of 0 produces no geometry at all.
        var save = new NamedObjectSave();

        GlueTileDefaults.CollisionTileSize(save).ShouldBe(16f);
        GlueTileDefaults.CollisionFillWidth(save).ShouldBe(32);
        GlueTileDefaults.CollisionFillHeight(save).ShouldBe(1);
    }

    [Fact]
    public void CreationOptions_TheTwoEnums_DoNotShareOrdinals()
    {
        // Both are read from similarly named keys and their numbering disagrees, so one shared
        // decoder would silently misread one of them.
        ((int)CollisionCreationOptions.FromProperties).ShouldBe(3);
        ((int)CollisionCreationOptions.FromType).ShouldBe(4);
        ((int)TileNodeNetworkCreationOptions.FromProperties).ShouldBe(2);
        ((int)TileNodeNetworkCreationOptions.FromType).ShouldBe(3);
    }

    /// <summary>
    /// A node-network save shaped like the ones Glue writes, pointed at Level1's real map.
    /// </summary>
    /// <remarks>
    /// No vendored fixture declares a TileNodeNetwork — FRB1's only one is in its test project,
    /// which writes short-form SourceClassType and would need hand-editing to vendor. The map, the
    /// tile types, and every builder path under test are real; only the declaration is synthetic.
    /// </remarks>
    private static NamedObjectSave NodeNetworkSave(
        string instanceName = "NodeNetwork",
        int creationOptions = (int)TileNodeNetworkCreationOptions.FromType,
        string? tileType = "SolidCollision",
        bool eliminateCutCorners = false,
        int directionalType = 0)
    {
        var save = new NamedObjectSave
        {
            InstanceName = instanceName,
            SourceClassType = "FlatRedBall.AI.Pathfinding.TileNodeNetwork",
            SourceType = SourceType.FlatRedBallType,
        };

        save.Properties.Add(new PropertySave
        {
            Name = "TileNodeNetworkCreationOptions",
            Value = JsonDocument.Parse(creationOptions.ToString()).RootElement.Clone(),
        });
        save.Properties.Add(new PropertySave
        {
            Name = "SourceTmxName",
            Value = JsonDocument.Parse("\"Map\"").RootElement.Clone(),
        });
        save.Properties.Add(new PropertySave
        {
            Name = "DirectionalType",
            Value = JsonDocument.Parse(directionalType.ToString()).RootElement.Clone(),
        });
        save.Properties.Add(new PropertySave
        {
            Name = "EliminateCutCorners",
            Value = JsonDocument.Parse(eliminateCutCorners ? "true" : "false").RootElement.Clone(),
        });

        if (tileType is not null)
        {
            save.Properties.Add(new PropertySave
            {
                Name = "NodeNetworkTileTypeName",
                Value = JsonDocument.Parse($"\"{tileType}\"").RootElement.Clone(),
            });
        }

        return save;
    }

    private GlueScreen? Level1With(NamedObjectSave extra)
    {
        if (!_graphics.IsAvailable)
            return null;

        var save = LoadFixtureScreen("DoorsDemo", "Level1.glsj");
        save.NamedObjects.Add(extra);

        var screen = new GlueScreen
        {
            Save = save,
            Content = new GlueContentSource(
                _graphics.ContentLoader!, Path.Combine("Glue", "Fixtures", "DoorsDemo", "Content"),
                _graphics.GraphicsDevice),
        };

        screen.BuildObjects();
        return screen;
    }

    [Fact]
    public void BuildObjects_ATileNodeNetworkFromType_HasNodesWhereThatTypesTilesAre()
    {
        var screen = Level1With(NodeNetworkSave());
        if (screen is null)
            return;

        var network = screen.Objects["NodeNetwork"].ShouldBeOfType<FlatRedBall2.AI.TileNodeNetwork>();
        var solid = (TileShapes)screen.Objects["SolidCollision"];
        var map = (TileMap)screen.Objects["Map"];

        // The same query drives both, so a node exists exactly where the collision tile does.
        int columns = (int)(map.Width / map.TileWidth);
        int rows = (int)(map.Height / map.TileHeight);
        int matched = 0;

        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                bool hasTile = solid.GetTileAtCell(column, row) is not null;
                (network.NodeAt(column, row) is not null).ShouldBe(hasTile);
                if (hasTile)
                    matched++;
            }
        }

        matched.ShouldBeGreaterThan(0, "the fixture's map has solid tiles, so some nodes must exist");
    }

    [Fact]
    public void BuildObjects_ATileNodeNetworkNamingNoTileType_WarnsRatherThanBuilding()
    {
        var screen = Level1With(NodeNetworkSave(tileType: null));
        if (screen is null)
            return;

        screen.Objects.ShouldNotContainKey("NodeNetwork");
        screen.BuildDiagnostics.ShouldContain(d => d.Message.Contains("names none"));
    }

    // FillCompletely, BorderOutline and FromLayer are decoded and reported rather than silently
    // ignored -- a network that quietly has no nodes looks like a pathfinding bug, not a gap.
    [Fact]
    public void BuildObjects_AnUnsupportedNodeNetworkOption_SaysSoRatherThanBuildingNothing()
    {
        var screen = Level1With(NodeNetworkSave(
            creationOptions: (int)TileNodeNetworkCreationOptions.FromLayer));
        if (screen is null)
            return;

        screen.Objects.ShouldNotContainKey("NodeNetwork");
        screen.BuildDiagnostics.ShouldContain(
            d => d.Message.Contains("FromLayer") && d.Message.Contains("does not support"));
    }

    // Glue spawns an entity for every tile whose type names one. No vendored map paints a tile typed
    // after an entity -- DoorsDemo places its doors as NamedObjects -- so the rule is exercised by
    // renaming a real entity to match a tile type the map really does use. The tiles, the lookup and
    // the spawn path are all real; only the pairing is arranged.
    [Fact]
    public void CreateEntitiesFromTiles_ATileTypedAfterAnEntity_SpawnsOnePerTile()
    {
        if (!_graphics.IsAvailable)
            return;

        var project = GlueProject.Load(
            Path.Combine(AppContext.BaseDirectory, "Glue", "Fixtures", "DoorsDemo", "DoorsDemo.gluj"),
            new GlueContentSource(
                _graphics.ContentLoader!, Path.Combine("Glue", "Fixtures", "DoorsDemo", "Content"),
                _graphics.GraphicsDevice));

        var door = project.FindEntity(@"Entities\Door")!;
        door.Name = @"Entities\SolidCollision";

        var engine = new FlatRedBallService();
        engine.GlueProject = project;
        engine.Start<GlueScreen>(s =>
        {
            s.Save = project.FindScreen(@"Screens\Level1");
            s.Project = project;
        });

        var screen = (GlueScreen)engine.CurrentScreen;
        var solid = (TileShapes)screen.Objects["SolidCollision"];
        var spawned = project.InstancesOf(@"Entities\SolidCollision");

        spawned.ShouldNotBeEmpty();

        // Every spawn sits on a tile of that type, at the tile's own centre.
        foreach (var instance in spawned)
            solid.GetTileAtWorld(instance.X, instance.Y).ShouldNotBeNull();
    }

    [Fact]
    public void BuildObjects_ATileNodeNetwork_IsNotReportedAsAnUnmappedType()
    {
        // Before this phase a TileNodeNetwork counted as a type "a later phase owns".
        GlueTileBuilder.IsNodeNetwork(NodeNetworkSave()).ShouldBeTrue();
    }

    [Fact]
    public void BuildObjects_Level1_LoadsItsTileMap()
    {
        var screen = BuiltLevel1();
        if (screen is null)
            return;

        var map = screen.Objects["Map"].ShouldBeOfType<TileMap>();

        map.Width.ShouldBeGreaterThan(0f);
        map.Height.ShouldBeGreaterThan(0f);
    }

    // Tile objects skip the construct-and-configure pass, and skipped its instruction step with it,
    // so every authored value on a map or a collection — Visible above all — was dropped.
    [Fact]
    public void BuildObjects_ACollectionAuthoredVisible_IsVisible()
    {
        if (!_graphics.IsAvailable)
            return;

        var save = LoadFixtureScreen("DoorsDemo", "Level1.glsj");
        save.NamedObjects.Single(o => o.InstanceName == "SolidCollision").InstructionSaves.Add(
            new InstructionSave
            {
                Member = "Visible",
                Type = "bool",
                Value = JsonDocument.Parse("true").RootElement,
            });

        var screen = new GlueScreen
        {
            Save = save,
            Content = new GlueContentSource(
                _graphics.ContentLoader!, Path.Combine("Glue", "Fixtures", "DoorsDemo", "Content"),
                _graphics.GraphicsDevice),
        };

        screen.BuildObjects();

        ((TileShapes)screen.Objects["SolidCollision"]).IsVisible.ShouldBeTrue();
    }

    // TileShapes is not an IRenderable — it has its own Screen.Add overload, exactly like TileMap.
    // The register step matched IRenderable and TileMap only, so an authored collection was built
    // and collided but could never be seen, whatever its Visible setting.
    [Fact]
    public void BuildObjects_ABuiltCollection_ReachesTheRenderList()
    {
        var screen = BuiltLevel1();
        if (screen is null)
            return;

        var solid = (TileShapes)screen.Objects["SolidCollision"];

        screen.RenderList.ShouldContain(solid.AllTiles.First());
    }

    [Fact]
    public void BuildObjects_Level1_BuildsCollisionFromTheAuthoredTileType()
    {
        // Both collections are CollisionCreationOptions.FromType keyed on a tile class, sourced from
        // the map named by SourceTmxName.
        var screen = BuiltLevel1();
        if (screen is null)
            return;

        var map = (TileMap)screen.Objects["Map"];
        var solid = screen.Objects["SolidCollision"].ShouldBeOfType<TileShapes>();

        solid.Name.ShouldBe("SolidCollision");

        // TileShapes exposes no count, so scan the map's cell range for real geometry.
        int columns = (int)(map.Width / map.TileWidth) + 1;
        int rows = (int)(map.Height / map.TileHeight) + 1;
        int tiles = 0;

        for (int col = -columns; col <= columns && tiles == 0; col++)
        {
            for (int row = -rows; row <= rows; row++)
            {
                if (solid.GetTileAtCell(col, row) is not null)
                {
                    tiles++;
                    break;
                }
            }
        }

        tiles.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void BuildObjects_Level1_ReportsNoErrorsAndDropsTheTileWarnings()
    {
        var screen = BuiltLevel1();
        if (screen is null)
            return;

        screen.BuildDiagnostics.ShouldNotContain(d => d.Severity == GlueDiagnosticSeverity.Error);
        screen.BuildDiagnostics.ShouldNotContain(d => d.Message.Contains("LayeredTileMap"));
        screen.BuildDiagnostics.ShouldNotContain(d => d.Message.Contains("TileShapeCollection"));
    }

    // FRB1 adds a screen's referenced files to managers with no object involved
    // (ReferencedFileSaveCodeGenerator.GetIfShouldAddToManagers): loaded at runtime, not
    // load-on-reference, AddToManagers set, and shared-static allowed because the owner is a screen.
    // Adding a tile map to a screen in Glue produces exactly this and no NamedObject, so a map that
    // draws in FRB1 drew nothing here.
    private GlueScreen ScreenReferencingLevel1Map(params NamedObjectSave[] objects)
    {
        var save = new ScreenSave { Name = @"Screens\ReferenceOnly" };
        save.ReferencedFiles.Add(new ReferencedFileSave
        {
            Name = "Screens/Level1/Level1Map.tmx",
            RuntimeType = "FlatRedBall.TileGraphics.LayeredTileMap",
        });
        save.NamedObjects.AddRange(objects);

        return new GlueScreen
        {
            Save = save,
            Content = new GlueContentSource(
                _graphics.ContentLoader!, Path.Combine("Glue", "Fixtures", "DoorsDemo", "Content"),
                _graphics.GraphicsDevice),
        };
    }

    [Fact]
    public void BuildObjects_AScreensReferencedTileMapWithNoObject_IsStillAddedToTheScreen()
    {
        if (!_graphics.IsAvailable)
            return;

        var screen = ScreenReferencingLevel1Map();

        screen.BuildObjects();

        screen.RenderList.OfType<TileMapLayerRenderable>().ShouldNotBeEmpty();
    }

    // The same map reached by both paths is one map: LoadTileMap caches by path, so the object and
    // the referenced file resolve to the same instance, and adding it twice would draw every layer
    // twice.
    [Fact]
    public void BuildObjects_AReferencedTileMapAlsoBuiltByAnObject_IsAddedOnce()
    {
        if (!_graphics.IsAvailable)
            return;

        var reference = ScreenReferencingLevel1Map();
        reference.BuildObjects();
        int layersFromTheFileAlone = reference.RenderList.OfType<TileMapLayerRenderable>().Count();

        var both = ScreenReferencingLevel1Map(new NamedObjectSave
        {
            InstanceName = "Map",
            SourceClassType = "FlatRedBall.TileGraphics.LayeredTileMap",
            SourceFile = "Screens/Level1/Level1Map.tmx",
            SourceName = "Entire File (LayeredTileMap)",
        });

        both.BuildObjects();

        both.RenderList.OfType<TileMapLayerRenderable>().Count().ShouldBe(layersFromTheFileAlone);
    }

    // Adding a tile map to a screen in Glue writes SourceType File plus the .tmx and "Entire File
    // (LayeredTileMap)", and leaves SourceClassType empty. Recognising a map by its class type alone
    // skipped the object, and anything keyed to its instance name — a TileShapeCollection's
    // SourceTmxName — then had nothing to read from.
    private static NamedObjectSave FileSourcedMap(string instanceName) => new()
    {
        InstanceName = instanceName,
        SourceType = SourceType.File,
        SourceFile = "Screens/Level1/Level1Map.tmx",
        SourceName = "Entire File (LayeredTileMap)",
    };

    [Fact]
    public void BuildObjects_AMapObjectWithNoSourceClassType_IsStillBuiltUnderItsInstanceName()
    {
        if (!_graphics.IsAvailable)
            return;

        var screen = ScreenReferencingLevel1Map(FileSourcedMap("Map"));

        screen.BuildObjects();

        screen.Objects.ShouldContainKey("Map");
        screen.Objects["Map"].ShouldBeOfType<TileMap>();
    }

    [Fact]
    public void BuildObjects_ACollectionReadingAFileSourcedMap_BuildsItsCollision()
    {
        if (!_graphics.IsAvailable)
            return;

        var collection = new NamedObjectSave
        {
            InstanceName = "TileShapeCollectionInstance",
            SourceClassType = "FlatRedBall.TileCollisions.TileShapeCollection",
            SourceType = SourceType.FlatRedBallType,
            SourceFile = "TileShapeCollection",
        };
        collection.Properties.Add(new PropertySave
        {
            Name = "SourceTmxName",
            Value = JsonDocument.Parse("\"Map\"").RootElement,
        });
        collection.Properties.Add(new PropertySave
        {
            Name = "CollisionTileTypeName",
            Value = JsonDocument.Parse("\"SolidCollision\"").RootElement,
        });
        // 4 is CollisionCreationOptions.FromType — collision from tiles of a named type.
        collection.Properties.Add(new PropertySave
        {
            Name = "CollisionCreationOptions",
            Value = JsonDocument.Parse("4").RootElement,
        });

        var screen = ScreenReferencingLevel1Map(FileSourcedMap("Map"), collection);

        screen.BuildObjects();

        screen.BuildDiagnostics.ShouldNotContain(d => d.Message.Contains("not a loaded tile map"));
        screen.Objects.ShouldContainKey("TileShapeCollectionInstance");
    }

    [Fact]
    public void BuildObjects_CollectionWhoseSourceMapIsMissing_WarnsWithoutThrowing()
    {
        if (!_graphics.IsAvailable)
            return;

        var save = LoadFixtureScreen("DoorsDemo", "Level1.glsj");
        save.NamedObjects.Single(o => o.InstanceName == "SolidCollision")
            .Properties.Single(p => p.Name == "SourceTmxName").Value =
            JsonDocument.Parse("\"NoSuchMap\"").RootElement;

        var screen = new GlueScreen
        {
            Save = save,
            Content = new GlueContentSource(
                _graphics.ContentLoader!, Path.Combine("Glue", "Fixtures", "DoorsDemo", "Content"),
                _graphics.GraphicsDevice),
        };

        Should.NotThrow(() => screen.BuildObjects());

        screen.BuildDiagnostics.ShouldContain(d =>
            d.Severity == GlueDiagnosticSeverity.Warning && d.Message.Contains("NoSuchMap"));
    }
}

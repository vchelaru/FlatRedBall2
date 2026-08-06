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
                _graphics.ContentLoader!, Path.Combine("Glue", "Fixtures", "DoorsDemo"),
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
                _graphics.ContentLoader!, Path.Combine("Glue", "Fixtures", "DoorsDemo"),
                _graphics.GraphicsDevice),
        };

        Should.NotThrow(() => screen.BuildObjects());

        screen.BuildDiagnostics.ShouldContain(d =>
            d.Severity == GlueDiagnosticSeverity.Warning && d.Message.Contains("NoSuchMap"));
    }
}

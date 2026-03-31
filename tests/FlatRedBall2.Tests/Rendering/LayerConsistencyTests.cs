using System;
using Microsoft.Xna.Framework.Graphics;
using FlatRedBall2.Collision;
using FlatRedBall2.Rendering;
using FlatRedBall2.Rendering.Batches;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Rendering;

public class LayerConsistencyTests
{
    private class TestScreen : Screen { }

    private class StubRenderable : IRenderable
    {
        public float Z { get; set; }
        public Layer? Layer { get; set; }
        public IRenderBatch Batch { get; set; } = WorldSpaceBatch.Instance;
        public string? Name { get; set; }
        public void Draw(SpriteBatch spriteBatch, Camera camera) { }
    }

    // --- IRenderable.Layer is nullable ---

    [Fact]
    public void IRenderable_Layer_DefaultsToNull()
    {
        var rect = new AxisAlignedRectangle();

        rect.Layer.ShouldBeNull();
    }

    // --- Screen.Layer property ---

    [Fact]
    public void Screen_Layer_DefaultsToNull()
    {
        var screen = new TestScreen();

        screen.Layer.ShouldBeNull();
    }

    [Fact]
    public void Screen_Layer_PropagatesLayerToExistingRenderables()
    {
        var screen = new TestScreen();
        var layer = new Layer("Game");
        screen.Layers.Add(layer);
        var rect = new AxisAlignedRectangle();
        screen.Add(rect);

        screen.Layer = layer;

        rect.Layer.ShouldBe(layer);
    }

    [Fact]
    public void Screen_Layer_InheritedByNewAdds()
    {
        var screen = new TestScreen();
        var layer = new Layer("Game");
        screen.Layers.Add(layer);
        screen.Layer = layer;

        var rect = new AxisAlignedRectangle();
        screen.Add(rect);

        rect.Layer.ShouldBe(layer);
    }

    [Fact]
    public void Screen_Add_ExplicitLayerOverridesScreenDefault()
    {
        var screen = new TestScreen();
        var gameLayer = new Layer("Game");
        var hudLayer = new Layer("HUD");
        screen.Layers.Add(gameLayer);
        screen.Layers.Add(hudLayer);
        screen.Layer = gameLayer;

        var rect = new AxisAlignedRectangle();
        screen.Add(rect, layer: hudLayer);

        rect.Layer.ShouldBe(hudLayer);
    }

    // --- Entity.Layer property ---

    [Fact]
    public void Entity_Layer_DefaultsToNull()
    {
        var entity = new Entity();

        entity.Layer.ShouldBeNull();
    }

    [Fact]
    public void Entity_Layer_PropagatesLayerToShapeChildren()
    {
        var engine = new FlatRedBallService();
        var screen = new TestScreen();
        screen.Engine = engine;
        var entity = new Entity();
        screen.Register(entity);
        var rect = new AxisAlignedRectangle();
        entity.Add(rect);
        var layer = new Layer("HUD");
        screen.Layers.Add(layer);

        entity.Layer = layer;

        rect.Layer.ShouldBe(layer);
    }

    [Fact]
    public void Entity_Layer_InheritedByNewAdds()
    {
        var engine = new FlatRedBallService();
        var screen = new TestScreen();
        screen.Engine = engine;
        var entity = new Entity();
        screen.Register(entity);
        var layer = new Layer("HUD");
        screen.Layers.Add(layer);
        entity.Layer = layer;

        var rect = new AxisAlignedRectangle();
        entity.Add(rect);

        rect.Layer.ShouldBe(layer);
    }

    [Fact]
    public void Entity_Add_ExplicitLayerOverridesEntityDefault()
    {
        var engine = new FlatRedBallService();
        var screen = new TestScreen();
        screen.Engine = engine;
        var entity = new Entity();
        screen.Register(entity);
        var gameLayer = new Layer("Game");
        var uiLayer = new Layer("UI");
        screen.Layers.Add(gameLayer);
        screen.Layers.Add(uiLayer);
        entity.Layer = gameLayer;

        var rect = new AxisAlignedRectangle();
        entity.Add(rect, layer: uiLayer);

        rect.Layer.ShouldBe(uiLayer);
    }

    [Fact]
    public void Entity_Layer_PropagatesRecursivelyToChildEntities()
    {
        var engine = new FlatRedBallService();
        var screen = new TestScreen();
        screen.Engine = engine;
        var parent = new Entity();
        screen.Register(parent);
        var child = new Entity();
        var rect = new AxisAlignedRectangle();
        child.Add(rect);
        parent.Add(child);
        var layer = new Layer("HUD");
        screen.Layers.Add(layer);

        parent.Layer = layer;

        child.Layer.ShouldBe(layer);
        rect.Layer.ShouldBe(layer);
    }

    // --- Factory inherits screen layer ---

    [Fact]
    public void Factory_Create_InheritsScreenLayer()
    {
        var engine = new FlatRedBallService();
        var screen = new TestScreen();
        screen.Engine = engine;
        var layer = new Layer("Game");
        screen.Layers.Add(layer);
        screen.Layer = layer;
        var factory = new Factory<TestEntity>(screen);

        var entity = factory.Create();

        entity.Layer.ShouldBe(layer);
    }

    [Fact]
    public void Factory_Create_NoScreenLayer_EntityLayerIsNull()
    {
        var engine = new FlatRedBallService();
        var screen = new TestScreen();
        screen.Engine = engine;
        var factory = new Factory<TestEntity>(screen);

        var entity = factory.Create();

        entity.Layer.ShouldBeNull();
    }

    // --- TileShapeCollection.Layer ---

    [Fact]
    public void TileShapeCollection_Layer_PropagatesLayerToExistingTiles()
    {
        var screen = new TestScreen();
        var tiles = new TileShapeCollection { GridSize = 16 };
        screen.Add(tiles);
        tiles.AddTileAtCell(0, 0);
        var layer = new Layer("Game");
        screen.Layers.Add(layer);

        tiles.Layer = layer;

        // The tile should now be on the layer
        foreach (var renderable in tiles.AllTiles)
            renderable.Layer.ShouldBe(layer);
    }

    [Fact]
    public void TileShapeCollection_Layer_InheritedByNewTiles()
    {
        var screen = new TestScreen();
        var layer = new Layer("Game");
        screen.Layers.Add(layer);
        var tiles = new TileShapeCollection { GridSize = 16 };
        screen.Add(tiles);
        tiles.Layer = layer;

        tiles.AddTileAtCell(0, 0);

        foreach (var renderable in tiles.AllTiles)
            renderable.Layer.ShouldBe(layer);
    }

    [Fact]
    public void Screen_Add_TileShapeCollection_ExplicitLayer()
    {
        var screen = new TestScreen();
        var layer = new Layer("Game");
        screen.Layers.Add(layer);

        var tiles = new TileShapeCollection { GridSize = 16 };
        screen.Add(tiles, layer: layer);
        tiles.AddTileAtCell(0, 0);

        tiles.Layer.ShouldBe(layer);
        foreach (var renderable in tiles.AllTiles)
            renderable.Layer.ShouldBe(layer);
    }

    [Fact]
    public void Screen_Add_TileShapeCollection_InheritsScreenLayer()
    {
        var screen = new TestScreen();
        var layer = new Layer("Game");
        screen.Layers.Add(layer);
        screen.Layer = layer;

        var tiles = new TileShapeCollection { GridSize = 16 };
        screen.Add(tiles);
        tiles.AddTileAtCell(0, 0);

        tiles.Layer.ShouldBe(layer);
        foreach (var renderable in tiles.AllTiles)
            renderable.Layer.ShouldBe(layer);
    }

    // --- Screen.Layer propagates to entities ---

    [Fact]
    public void Screen_Layer_PropagatesLayerToExistingEntities()
    {
        var engine = new FlatRedBallService();
        var screen = new TestScreen();
        screen.Engine = engine;
        var entity = new Entity();
        var rect = new AxisAlignedRectangle();
        entity.Add(rect);
        screen.Register(entity);
        var layer = new Layer("Game");
        screen.Layers.Add(layer);

        screen.Layer = layer;

        entity.Layer.ShouldBe(layer);
        rect.Layer.ShouldBe(layer);
    }

    [Fact]
    public void AllRenderableTypes_Layer_DefaultsToNull()
    {
        new AxisAlignedRectangle().Layer.ShouldBeNull();
        new Circle().Layer.ShouldBeNull();
        new Polygon().Layer.ShouldBeNull();
        new Line().Layer.ShouldBeNull();
        new Sprite().Layer.ShouldBeNull();
    }

    private class TestEntity : Entity { }
}

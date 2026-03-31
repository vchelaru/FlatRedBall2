using System;
using Microsoft.Xna.Framework.Graphics;
using FlatRedBall2.Collision;
using FlatRedBall2.Rendering;
using FlatRedBall2.Rendering.Batches;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Rendering;

public class RenderListTests
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

    private class StubAttachable : IRenderable, IAttachable
    {
        public float Z { get; set; }
        public Layer? Layer { get; set; }
        public IRenderBatch Batch { get; set; } = WorldSpaceBatch.Instance;
        public string? Name { get; set; }
        public void Draw(SpriteBatch spriteBatch, Camera camera) { }

        public Entity? Parent { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float AbsoluteX => Parent != null ? Parent.AbsoluteX + X : X;
        public float AbsoluteY => Parent != null ? Parent.AbsoluteY + Y : Y;
        public float AbsoluteZ => Parent != null ? Parent.AbsoluteZ + Z : Z;
        public void Destroy() { }
    }

    [Fact]
    public void RenderList_ItemsRemainAfterUpdate()
    {
        var screen = new TestScreen();
        var layer = new Layer("Test");
        screen.Layers.Add(layer);

        var highZ = new StubRenderable { Z = 10f, Layer = layer };
        var lowZ = new StubRenderable { Z = 1f, Layer = layer };

        screen.Add(highZ);
        screen.Add(lowZ);

        var frame = new FrameTime(TimeSpan.FromSeconds(1f / 60f), TimeSpan.Zero, TimeSpan.Zero);
        screen.Update(frame);

        screen.RenderList.Count.ShouldBe(2);
    }

    [Fact]
    public void RenderList_LayerAndZOrdering_ContainsBothItems()
    {
        var screen = new TestScreen();
        var layer1 = new Layer("Background");
        var layer2 = new Layer("Foreground");
        screen.Layers.Add(layer1);
        screen.Layers.Add(layer2);

        var fgItem = new StubRenderable { Z = 0f, Layer = layer2, Name = "fg" };
        var bgItem = new StubRenderable { Z = 0f, Layer = layer1, Name = "bg" };

        screen.Add(fgItem);
        screen.Add(bgItem);

        var frame = new FrameTime(TimeSpan.FromSeconds(1f / 60f), TimeSpan.Zero, TimeSpan.Zero);
        screen.Update(frame);

        screen.RenderList.Count.ShouldBe(2);
        screen.RenderList.ShouldContain(fgItem);
        screen.RenderList.ShouldContain(bgItem);
    }

    [Fact]
    public void SortRenderList_ZSecondaryParentY_DifferentZ_SortsByZ()
    {
        var screen = new TestScreen();
        screen.SortMode = FlatRedBall2.Rendering.SortMode.ZSecondaryParentY;
        var layer = new Layer("Test");
        screen.Layers.Add(layer);

        // highZ has a lower parent Y, which would win under Y-sort — but Z should take priority
        var highZ = new StubAttachable { Z = 10f, Y = -100f, Layer = layer, Name = "highZ" };
        var lowZ  = new StubAttachable { Z =  1f, Y =  100f, Layer = layer, Name = "lowZ"  };

        screen.Add(highZ);
        screen.Add(lowZ);
        screen.SortRenderList();

        screen.RenderList[0].Name.ShouldBe("lowZ");
        screen.RenderList[1].Name.ShouldBe("highZ");
    }

    [Fact]
    public void SortRenderList_ZSecondaryParentY_SameZ_LowerParentYDrawnLast()
    {
        var screen = new TestScreen();
        screen.SortMode = FlatRedBall2.Rendering.SortMode.ZSecondaryParentY;
        var layer = new Layer("Test");
        screen.Layers.Add(layer);

        // Both at Z=0. Entity at Y=100 is higher on screen (behind); Y=-50 is lower (in front).
        var high = new StubAttachable { Z = 0f, Y =  100f, Layer = layer, Name = "high" };
        var low  = new StubAttachable { Z = 0f, Y = -50f,  Layer = layer, Name = "low"  };

        screen.Add(high);
        screen.Add(low);
        screen.SortRenderList();

        screen.RenderList[0].Name.ShouldBe("high");
        screen.RenderList[1].Name.ShouldBe("low");
    }

    [Fact]
    public void Layer_UpdatesLayerOnRenderableChildren()
    {
        var screen = new TestScreen();
        var layer1 = new Layer("Background");
        var layer2 = new Layer("Foreground");
        screen.Layers.Add(layer1);
        screen.Layers.Add(layer2);

        var entity = new Entity();
        var rect = new AxisAlignedRectangle { Layer = layer1 };
        entity.Add(rect);

        entity.Layer = layer2;

        rect.Layer.ShouldBe(layer2);
    }

    [Fact]
    public void Layer_RecursivelyUpdatesChildEntityRenderables()
    {
        var screen = new TestScreen();
        var layer1 = new Layer("Background");
        var layer2 = new Layer("Foreground");
        screen.Layers.Add(layer1);
        screen.Layers.Add(layer2);

        var parent = new Entity();
        var child = new Entity();
        var rect = new AxisAlignedRectangle { Layer = layer1 };
        child.Add(rect);
        parent.Add(child);

        parent.Layer = layer2;

        rect.Layer.ShouldBe(layer2);
    }

    [Fact]
    public void RenderList_StableSort_PreservesInsertionOrderForEqualZ()
    {
        var screen = new TestScreen();
        var layer = new Layer("Test");
        screen.Layers.Add(layer);

        var first = new StubRenderable { Z = 0f, Layer = layer, Name = "first" };
        var second = new StubRenderable { Z = 0f, Layer = layer, Name = "second" };
        var third = new StubRenderable { Z = 0f, Layer = layer, Name = "third" };

        screen.Add(first);
        screen.Add(second);
        screen.Add(third);

        screen.RenderList[0].Name.ShouldBe("first");
        screen.RenderList[1].Name.ShouldBe("second");
        screen.RenderList[2].Name.ShouldBe("third");
    }

    [Fact]
    public void SortRenderList_LayeredItems_DrawOnTopOfUnlayered()
    {
        var screen = new TestScreen();
        var layer = new Layer("HUD");
        screen.Layers.Add(layer);

        var unlayered = new StubRenderable { Z = 0f, Name = "unlayered" };
        var layered = new StubRenderable { Z = 0f, Layer = layer, Name = "layered" };

        // Add layered first — it should still sort on top regardless of insertion order
        screen.Add(layered);
        screen.Add(unlayered);
        screen.SortRenderList();

        screen.RenderList[0].Name.ShouldBe("unlayered");
        screen.RenderList[1].Name.ShouldBe("layered");
    }

    [Fact]
    public void SortRenderList_UnlayeredItems_DrawBehindAllLayers()
    {
        var screen = new TestScreen();
        var bg = new Layer("Background");
        var fg = new Layer("Foreground");
        screen.Layers.Add(bg);
        screen.Layers.Add(fg);

        var unlayered = new StubRenderable { Z = 0f, Name = "unlayered" };
        var bgItem = new StubRenderable { Z = 0f, Layer = bg, Name = "bg" };
        var fgItem = new StubRenderable { Z = 0f, Layer = fg, Name = "fg" };

        // Add in reverse order to prove sort wins over insertion order
        screen.Add(fgItem);
        screen.Add(unlayered);
        screen.Add(bgItem);
        screen.SortRenderList();

        screen.RenderList[0].Name.ShouldBe("unlayered");
        screen.RenderList[1].Name.ShouldBe("bg");
        screen.RenderList[2].Name.ShouldBe("fg");
    }
}

using System;
using Microsoft.Xna.Framework.Graphics;
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
        public Layer Layer { get; set; } = null!;
        public IRenderBatch Batch { get; set; } = WorldSpaceBatch.Instance;
        public string? Name { get; set; }
        public void Draw(SpriteBatch spriteBatch, Camera camera) { }
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
}

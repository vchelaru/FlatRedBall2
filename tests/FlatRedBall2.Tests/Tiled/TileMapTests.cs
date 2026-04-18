using System.Collections.Generic;
using FlatRedBall2.Tiled;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Tiled;

public class TileMapTests
{
    private static TileMap CreateTestMap(
        float width, float height, int tileWidth, int tileHeight,
        List<string> layerNames, float x = 0f, float y = 0f)
    {
        var layers = new List<TileMapLayer>();
        foreach (var name in layerNames)
            layers.Add(new TileMapLayer(name));

        return new TileMap(width, height, tileWidth, tileHeight, layers, x, y);
    }

    [Fact]
    public void Bounds_AtOrigin_CenterIsHalfWidthAndNegativeHalfHeight()
    {
        var map = CreateTestMap(320f, 240f, 16, 16, ["GameplayLayer"]);

        var bounds = map.Bounds;

        bounds.CenterX.ShouldBe(160f);
        bounds.CenterY.ShouldBe(-120f);
        bounds.Width.ShouldBe(320f);
        bounds.Height.ShouldBe(240f);
    }

    [Fact]
    public void Bounds_Centered_CenterIsAtOrigin()
    {
        var map = CreateTestMap(320f, 240f, 16, 16, ["GameplayLayer"]);
        map.CenterOn(0f, 0f);

        var bounds = map.Bounds;

        bounds.CenterX.ShouldBe(0f);
        bounds.CenterY.ShouldBe(0f);
    }

    [Fact]
    public void Bounds_Offset_ReflectsPosition()
    {
        var map = CreateTestMap(320f, 240f, 16, 16, ["GameplayLayer"], x: 100f, y: 200f);

        var bounds = map.Bounds;

        bounds.CenterX.ShouldBe(260f);
        bounds.CenterY.ShouldBe(80f);
    }

    [Fact]
    public void CenterOn_Origin_SetsCorrectXY()
    {
        var map = CreateTestMap(320f, 240f, 16, 16, ["GameplayLayer"]);

        map.CenterOn(0f, 0f);

        map.X.ShouldBe(-160f);
        map.Y.ShouldBe(120f);
    }

    [Fact]
    public void CenterOn_OffCenter_SetsCorrectXY()
    {
        var map = CreateTestMap(320f, 240f, 16, 16, ["GameplayLayer"]);

        map.CenterOn(50f, 30f);

        map.X.ShouldBe(-110f);
        map.Y.ShouldBe(150f);
    }

    [Fact]
    public void DefaultZ_WithGameplayLayer_GameplayLayerIsZero()
    {
        var map = CreateTestMap(320f, 240f, 16, 16,
            ["Background", "GameplayLayer", "Foreground"]);

        map.GetLayer("Background").Z.ShouldBe(-1f);
        map.GetLayer("GameplayLayer").Z.ShouldBe(0f);
        map.GetLayer("Foreground").Z.ShouldBe(1f);
    }

    [Fact]
    public void DefaultZ_WithoutGameplayLayer_FirstLayerIsZero()
    {
        var map = CreateTestMap(320f, 240f, 16, 16,
            ["Background", "Details", "Foreground"]);

        map.GetLayer("Background").Z.ShouldBe(0f);
        map.GetLayer("Details").Z.ShouldBe(1f);
        map.GetLayer("Foreground").Z.ShouldBe(2f);
    }

    [Fact]
    public void DefaultZ_GameplayLayerFirst_AllPositive()
    {
        var map = CreateTestMap(320f, 240f, 16, 16,
            ["GameplayLayer", "Foreground", "Overlay"]);

        map.GetLayer("GameplayLayer").Z.ShouldBe(0f);
        map.GetLayer("Foreground").Z.ShouldBe(1f);
        map.GetLayer("Overlay").Z.ShouldBe(2f);
    }

    [Fact]
    public void GetLayer_CaseInsensitive()
    {
        var map = CreateTestMap(320f, 240f, 16, 16, ["GameplayLayer"]);

        map.GetLayer("gameplaylayer").Name.ShouldBe("GameplayLayer");
        map.GetLayer("GAMEPLAYLAYER").Name.ShouldBe("GameplayLayer");
    }

    [Fact]
    public void TryGetLayer_Exists_ReturnsTrue()
    {
        var map = CreateTestMap(320f, 240f, 16, 16, ["Background"]);

        map.TryGetLayer("Background", out var layer).ShouldBeTrue();
        layer.Name.ShouldBe("Background");
    }

    [Fact]
    public void TryGetLayer_NotExists_ReturnsFalse()
    {
        var map = CreateTestMap(320f, 240f, 16, 16, ["Background"]);

        map.TryGetLayer("NonExistent", out _).ShouldBeFalse();
    }

    [Fact]
    public void Dimensions_ReflectConstructorValues()
    {
        var map = CreateTestMap(320f, 240f, 16, 16, ["GameplayLayer"]);

        map.Width.ShouldBe(320f);
        map.Height.ShouldBe(240f);
        map.TileWidth.ShouldBe(16);
        map.TileHeight.ShouldBe(16);
    }

    [Fact]
    public void Layers_ReturnsAllInOrder()
    {
        var map = CreateTestMap(320f, 240f, 16, 16,
            ["Background", "GameplayLayer", "Foreground"]);

        map.Layers.Count.ShouldBe(3);
        map.Layers[0].Name.ShouldBe("Background");
        map.Layers[1].Name.ShouldBe("GameplayLayer");
        map.Layers[2].Name.ShouldBe("Foreground");
    }
}

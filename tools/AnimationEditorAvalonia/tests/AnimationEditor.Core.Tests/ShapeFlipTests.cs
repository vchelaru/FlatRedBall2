using System.Linq;
using AnimationEditor.Core.CommandsAndState;
using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.Core.Tests;

// ── ShapeFlip ──────────────────────────────────────────────────────────────────
// Pure helper — negates a shape's offsets about the entity origin to match a frame flip.

public class ShapeFlipTests
{
    [Fact]
    public void Mirror_CircleFlipVertical_NegatesYOnly()
    {
        var c = new CircleSave { X = -6, Y = 3 };

        ShapeFlip.Mirror(c, flipHorizontal: false, flipVertical: true);

        Assert.Equal(-6, c.X);
        Assert.Equal(-3, c.Y);
    }

    [Fact]
    public void Mirror_PolygonFlipHorizontal_NegatesOriginAndPointXs()
    {
        var p = new PolygonSave { X = 4, Y = 2 };
        p.Points.Add(new Vector2Save { X = 1, Y = 5 });
        p.Points.Add(new Vector2Save { X = -3, Y = 7 });

        ShapeFlip.Mirror(p, flipHorizontal: true, flipVertical: false);

        Assert.Equal(-4, p.X);
        Assert.Equal(2, p.Y);
        Assert.Equal(-1, p.Points[0].X);
        Assert.Equal(5, p.Points[0].Y);
        Assert.Equal(3, p.Points[1].X);
    }

    [Fact]
    public void Mirror_RectFlipHorizontal_NegatesXOnly()
    {
        var r = new AARectSave { X = 10, Y = 4 };

        ShapeFlip.Mirror(r, flipHorizontal: true, flipVertical: false);

        Assert.Equal(-10, r.X);
        Assert.Equal(4, r.Y);
    }

    [Fact]
    public void Mirror_RectFlipHorizontalTwice_RestoresExactly()
    {
        var r = new AARectSave { X = 10, Y = 4 };

        ShapeFlip.Mirror(r, flipHorizontal: true, flipVertical: false);
        ShapeFlip.Mirror(r, flipHorizontal: true, flipVertical: false);

        Assert.Equal(10, r.X);   // negation is its own inverse — no drift
        Assert.Equal(4, r.Y);
    }

    [Fact]
    public void Transpose_CircleHVAgree_SwapsOffsetPlain()
    {
        var c = new CircleSave { X = -6, Y = 3, Radius = 7 };

        ShapeFlip.Transpose(c, flipHorizontal: false, flipVertical: false);

        Assert.Equal(3, c.X);
        Assert.Equal(-6, c.Y);
        Assert.Equal(7, c.Radius); // radius is orientation-independent
    }

    [Fact]
    public void Transpose_CircleHVDisagree_SwapsOffsetNegated()
    {
        var c = new CircleSave { X = -6, Y = 3, Radius = 7 };

        ShapeFlip.Transpose(c, flipHorizontal: true, flipVertical: false);

        Assert.Equal(-3, c.X);
        Assert.Equal(6, c.Y);
    }

    [Fact]
    public void Transpose_PolygonOriginAndPointsHVAgree_SwapsEachPlain()
    {
        var p = new PolygonSave { X = 4, Y = 2 };
        p.Points.Add(new Vector2Save { X = 1, Y = 5 });

        ShapeFlip.Transpose(p, flipHorizontal: true, flipVertical: true);

        Assert.Equal(2, p.X);
        Assert.Equal(4, p.Y);
        Assert.Equal(5, p.Points[0].X);
        Assert.Equal(1, p.Points[0].Y);
    }

    [Fact]
    public void Transpose_RectOffsetAndScaleHVAgree_SwapsOffsetAndScalePlain()
    {
        var r = new AARectSave { X = 10, Y = 4, ScaleX = 3, ScaleY = 5 };

        ShapeFlip.Transpose(r, flipHorizontal: false, flipVertical: false);

        Assert.Equal(4, r.X);
        Assert.Equal(10, r.Y);
        Assert.Equal(5, r.ScaleX);
        Assert.Equal(3, r.ScaleY);
    }

    [Fact]
    public void Transpose_RectOffsetHVDisagree_SwapsOffsetNegated()
    {
        var r = new AARectSave { X = 10, Y = 4, ScaleX = 3, ScaleY = 5 };

        ShapeFlip.Transpose(r, flipHorizontal: false, flipVertical: true);

        Assert.Equal(-4, r.X);
        Assert.Equal(-10, r.Y);
        Assert.Equal(5, r.ScaleX);   // scale swap has no sign ambiguity
        Assert.Equal(3, r.ScaleY);
    }

    [Fact]
    public void Transpose_RectTwiceSameHV_RestoresExactly()
    {
        var r = new AARectSave { X = 10, Y = 4, ScaleX = 3, ScaleY = 5 };

        ShapeFlip.Transpose(r, flipHorizontal: true, flipVertical: false);
        ShapeFlip.Transpose(r, flipHorizontal: true, flipVertical: false);

        Assert.Equal(10, r.X); // same delta applied twice is its own inverse — no drift
        Assert.Equal(4, r.Y);
        Assert.Equal(3, r.ScaleX);
        Assert.Equal(5, r.ScaleY);
    }
}

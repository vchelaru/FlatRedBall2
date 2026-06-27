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
}

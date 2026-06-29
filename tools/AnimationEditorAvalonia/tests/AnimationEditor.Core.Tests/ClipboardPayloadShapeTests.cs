using AnimationEditor.Core.IO;
using FlatRedBall2.Animation;
using FlatRedBall2.Animation.Content;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Repro + behavior pinning for issue #431 follow-up: copying a frame (or chain)
/// that contains a per-frame shape silently fails. The clipboard serializer must
/// round-trip every frame field and every shape type (incl. cross-type ordering),
/// while the single-shape payloads keep working.
/// </summary>
public class ClipboardPayloadShapeTests
{
    private static AnimationFrameSave RoundTripFrame(AnimationFrameSave frame)
    {
        var xml = ClipboardPayload.Serialize(new List<AnimationFrameSave> { frame });
        Assert.True(ClipboardPayload.TryDeserialize(xml, out _, out var frames, out _, out _),
            "frame payload did not round-trip");
        return Assert.Single(frames!);
    }

    // ── Shape coverage: each shape type, inside a frame ────────────────────

    [Fact]
    public void FrameWithRectangle_RoundTrips()
    {
        var frame = new AnimationFrameSave { TextureName = "lava.png" };
        frame.ShapesSave = new ShapesSave();
        frame.ShapesSave.Shapes.Add(new AARectSave { Name = "BulletOrigin", X = 1f, Y = 2f, ScaleX = 3f, ScaleY = 4f });

        var rt = RoundTripFrame(frame).ShapesSave!.AARectSaves.Single();

        Assert.Equal("BulletOrigin", rt.Name);
        Assert.Equal(1f, rt.X);
        Assert.Equal(2f, rt.Y);
        Assert.Equal(3f, rt.ScaleX);
        Assert.Equal(4f, rt.ScaleY);
    }

    [Fact]
    public void FrameWithCircle_RoundTrips()
    {
        var frame = new AnimationFrameSave { TextureName = "lava.png" };
        frame.ShapesSave = new ShapesSave();
        frame.ShapesSave.Shapes.Add(new CircleSave { Name = "Hit", X = 5f, Y = 6f, Radius = 7f });

        var rt = RoundTripFrame(frame).ShapesSave!.CircleSaves.Single();

        Assert.Equal("Hit", rt.Name);
        Assert.Equal(5f, rt.X);
        Assert.Equal(6f, rt.Y);
        Assert.Equal(7f, rt.Radius);
    }

    [Fact]
    public void FrameWithPolygon_RoundTripsIncludingPoints()
    {
        var frame = new AnimationFrameSave { TextureName = "lava.png" };
        var poly = new PolygonSave { Name = "Blade", X = 1f, Y = 2f };
        poly.Points.Add(new Vector2Save { X = 0f, Y = 0f });
        poly.Points.Add(new Vector2Save { X = 10f, Y = 0f });
        poly.Points.Add(new Vector2Save { X = 10f, Y = 8f });
        frame.ShapesSave = new ShapesSave();
        frame.ShapesSave.Shapes.Add(poly);

        var rt = RoundTripFrame(frame).ShapesSave!.PolygonSaves.Single();

        Assert.Equal("Blade", rt.Name);
        Assert.Equal(1f, rt.X);
        Assert.Equal(2f, rt.Y);
        Assert.Equal(3, rt.Points.Count);
        Assert.Equal(10f, rt.Points[2].X);
        Assert.Equal(8f, rt.Points[2].Y);
    }

    [Fact]
    public void FrameWithMixedShapes_GroupsByTypePreservingWithinTypeOrder()
    {
        var frame = new AnimationFrameSave { TextureName = "lava.png" };
        frame.ShapesSave = new ShapesSave();
        frame.ShapesSave.Shapes.Add(new AARectSave { Name = "A" });
        frame.ShapesSave.Shapes.Add(new CircleSave { Name = "B" });
        frame.ShapesSave.Shapes.Add(new AARectSave { Name = "C" });

        var rt = RoundTripFrame(frame).ShapesSave!.Shapes;

        // The .achx format uses FRB1's typed shape lists (rects, then polygons, then circles), so
        // cross-type insertion order is not preserved — shapes regroup by type (A, C rects; B
        // circle). Within a type, order is kept. Shapes are matched by Name at runtime, so this is
        // purely a serialization-layout property.
        Assert.Collection(rt,
            s => Assert.Equal("A", Assert.IsType<AARectSave>(s).Name),
            s => Assert.Equal("C", Assert.IsType<AARectSave>(s).Name),
            s => Assert.Equal("B", Assert.IsType<CircleSave>(s).Name));
    }

    // ── Field coverage: a shaped frame must not drop any frame field ───────

    [Fact]
    public void FrameWithShape_PreservesAllFrameFields()
    {
        var frame = new AnimationFrameSave
        {
            TextureName     = "sheet.png",
            FrameLength     = 0.25f,
            LeftCoordinate  = 0.1f,
            RightCoordinate = 0.2f,
            TopCoordinate   = 0.3f,
            BottomCoordinate= 0.4f,
            FlipHorizontal  = true,
            FlipVertical    = true,
            RelativeX       = 12f,
            RelativeY       = -8f,
            Red             = 10,
            Green           = 20,
            Blue            = 30,
            ColorOperation  = ColorOperation.Add,
        };
        frame.ShapesSave = new ShapesSave();
        frame.ShapesSave.Shapes.Add(new AARectSave { Name = "BulletOrigin" });

        var rt = RoundTripFrame(frame);

        Assert.Equal("sheet.png", rt.TextureName);
        Assert.Equal(0.25f, rt.FrameLength);
        Assert.Equal(0.1f, rt.LeftCoordinate);
        Assert.Equal(0.2f, rt.RightCoordinate);
        Assert.Equal(0.3f, rt.TopCoordinate);
        Assert.Equal(0.4f, rt.BottomCoordinate);
        Assert.True(rt.FlipHorizontal);
        Assert.True(rt.FlipVertical);
        Assert.Equal(12f, rt.RelativeX);
        Assert.Equal(-8f, rt.RelativeY);
        Assert.Equal(10, rt.Red);
        Assert.Equal(20, rt.Green);
        Assert.Equal(30, rt.Blue);
        Assert.Equal(ColorOperation.Add, rt.ColorOperation);
        Assert.Equal("BulletOrigin", rt.ShapesSave!.AARectSaves.Single().Name);
    }

    [Fact]
    public void FrameWithNoShapes_RoundTrips()
    {
        var frame = new AnimationFrameSave { TextureName = "lava.png", FrameLength = 0.1f };

        var rt = RoundTripFrame(frame);

        Assert.Equal("lava.png", rt.TextureName);
        Assert.Equal(0, rt.ShapesSave?.Shapes.Count ?? 0);
    }

    // ── Chain copy: chains carry frames carry shapes ───────────────────────

    [Fact]
    public void ChainWithShapedFrame_RoundTrips()
    {
        var chain = new AnimationChainSave { Name = "IdleDown" };
        var frame = new AnimationFrameSave { TextureName = "lava.png", FrameLength = 0.1f };
        frame.ShapesSave = new ShapesSave();
        frame.ShapesSave.Shapes.Add(new AARectSave { Name = "BulletOrigin" });
        chain.Frames.Add(frame);

        var xml = ClipboardPayload.Serialize(new List<AnimationChainSave> { chain });
        Assert.True(ClipboardPayload.TryDeserialize(xml, out var chains, out _, out _, out _));

        var rtChain = Assert.Single(chains!);
        Assert.Equal("IdleDown", rtChain.Name);
        var rtFrame = Assert.Single(rtChain.Frames);
        Assert.Equal("BulletOrigin", rtFrame.ShapesSave!.AARectSaves.Single().Name);
    }

    // ── Regression: single-shape payloads must keep working ────────────────

    [Fact]
    public void SingleRectangle_RoundTrips()
    {
        var rect = new AARectSave { Name = "BulletOrigin", X = 1f, Y = 2f, ScaleX = 3f, ScaleY = 4f };

        var xml = ClipboardPayload.Serialize(rect);
        Assert.True(ClipboardPayload.TryDeserialize(xml, out _, out _, out var rects, out _));

        var rt = Assert.Single(rects!);
        Assert.Equal("BulletOrigin", rt!.Name);
        Assert.Equal(3f, rt.ScaleX);
    }

    [Fact]
    public void SingleCircle_RoundTrips()
    {
        var circle = new CircleSave { Name = "Hit", X = 5f, Y = 6f, Radius = 7f };

        var xml = ClipboardPayload.Serialize(circle);
        Assert.True(ClipboardPayload.TryDeserialize(xml, out _, out _, out _, out var circles));

        var rt = Assert.Single(circles!);
        Assert.Equal("Hit", rt!.Name);
        Assert.Equal(7f, rt.Radius);
    }
}

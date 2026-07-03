using AnimationEditor.Core.ViewModels;
using FlatRedBall2.Animation;
using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class TimelineStripSignatureTests
{
    private static AnimationFrameSave Frame(float length, string texture = "sheet.png") =>
        new()
        {
            TextureName = texture,
            FrameLength = length,
            RightCoordinate = 1f,
            BottomCoordinate = 1f,
        };

    [Fact]
    public void From_NullChain_EqualsAnotherNullChain()
    {
        Assert.Equal(TimelineStripSignature.From(null), TimelineStripSignature.From(null));
    }

    [Fact]
    public void From_SameChainUnchangedFrames_AreEqual()
    {
        // A scrub crosses a frame boundary without mutating the chain — the signature
        // recomputed from the same chain must compare equal so the rebuild is skipped.
        var chain = new AnimationChainSave { Name = "Run" };
        chain.Frames.Add(Frame(0.1f));
        chain.Frames.Add(Frame(0.2f));

        Assert.Equal(TimelineStripSignature.From(chain), TimelineStripSignature.From(chain));
    }

    [Fact]
    public void From_DifferentChainReferenceSameContent_AreNotEqual()
    {
        // Selecting a different chain object is a structural change even when the frame
        // data is identical, so the strip must rebuild.
        var a = new AnimationChainSave { Name = "A" };
        a.Frames.Add(Frame(0.1f));
        var b = new AnimationChainSave { Name = "B" };
        b.Frames.Add(Frame(0.1f));

        Assert.NotEqual(TimelineStripSignature.From(a), TimelineStripSignature.From(b));
    }

    [Fact]
    public void From_FrameLengthChanged_AreNotEqual()
    {
        // FrameLength drives cell width, so a duration edit must trigger a rebuild.
        var chain = new AnimationChainSave { Name = "Run" };
        var frame = Frame(0.1f);
        chain.Frames.Add(frame);
        var before = TimelineStripSignature.From(chain);

        frame.FrameLength = 0.5f;
        var after = TimelineStripSignature.From(chain);

        Assert.NotEqual(before, after);
    }

    [Fact]
    public void From_FrameCountChanged_AreNotEqual()
    {
        var chain = new AnimationChainSave { Name = "Run" };
        chain.Frames.Add(Frame(0.1f));
        var before = TimelineStripSignature.From(chain);

        chain.Frames.Add(Frame(0.1f));
        var after = TimelineStripSignature.From(chain);

        Assert.NotEqual(before, after);
    }

    [Fact]
    public void From_FrameReordered_AreNotEqual()
    {
        var chain = new AnimationChainSave { Name = "Run" };
        chain.Frames.Add(Frame(0.1f, "a.png"));
        chain.Frames.Add(Frame(0.2f, "b.png"));
        var before = TimelineStripSignature.From(chain);

        (chain.Frames[0], chain.Frames[1]) = (chain.Frames[1], chain.Frames[0]);
        var after = TimelineStripSignature.From(chain);

        Assert.NotEqual(before, after);
    }

    [Fact]
    public void From_FrameColorChanged_AreNotEqual()
    {
        // A color edit changes the tinted thumbnail even though width is unchanged, so the strip
        // must rebuild — otherwise the cell would show the old color.
        var chain = new AnimationChainSave { Name = "Run" };
        var frame = Frame(0.1f);
        chain.Frames.Add(frame);
        var before = TimelineStripSignature.From(chain);

        frame.ColorOperation = ColorOperation.Multiply;
        frame.Red = 255; frame.Green = 0; frame.Blue = 0;
        var after = TimelineStripSignature.From(chain);

        Assert.NotEqual(before, after);
    }

    [Fact]
    public void From_EarlierFrameColorChanged_ChangesDownstreamSignature()
    {
        // Sticky color: editing frame 0's color must change frame 1's effective color too, so the
        // whole-strip signature differs and RefreshTimelineStrip rebuilds the downstream cell.
        var chain = new AnimationChainSave { Name = "Run" };
        var frame0 = Frame(0.1f);
        chain.Frames.Add(frame0);
        chain.Frames.Add(Frame(0.1f));   // frame 1 sets no color → inherits frame 0's
        var before = TimelineStripSignature.From(chain);

        frame0.ColorOperation = ColorOperation.Multiply;
        frame0.Red = 128;
        var after = TimelineStripSignature.From(chain);

        Assert.NotEqual(before, after);
    }

    [Fact]
    public void From_FrameFlipToggled_AreNotEqual()
    {
        // A flip changes the thumbnail content even though width is unchanged, so the
        // strip (which holds the thumbnails) must rebuild.
        var chain = new AnimationChainSave { Name = "Run" };
        var frame = Frame(0.1f);
        chain.Frames.Add(frame);
        var before = TimelineStripSignature.From(chain);

        frame.FlipHorizontal = true;
        var after = TimelineStripSignature.From(chain);

        Assert.NotEqual(before, after);
    }
}

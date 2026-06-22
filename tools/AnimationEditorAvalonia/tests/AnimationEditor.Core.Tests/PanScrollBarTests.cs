using AnimationEditor.Core.Rendering;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Tests for <see cref="PanScrollBar"/> — the pure mapping between a center-relative
/// pan axis and a scrollbar's (Minimum, Maximum, Value, ViewportSize). No UI.
/// Used to drive the Preview panel's scrollbars (#415).
/// </summary>
public class PanScrollBarTests
{
    // Empty content: pan band collapses to ±viewport/2, so the value range does too.
    [Fact]
    public void FromPan_EmptyContent_ValueRangeIsViewportHalfBand()
    {
        var r = PanScrollBar.FromPan(0f, 380f, 0f, 0f);
        Assert.Equal(-190f, r.Minimum, 4);
        Assert.Equal(190f, r.Maximum, 4);
        Assert.Equal(380f, r.ViewportSize, 4);
        Assert.Equal(0f, r.Value, 4);
    }

    // Scroll axis runs opposite pan: a positive pan maps to a negative scroll Value.
    [Fact]
    public void FromPan_InvertsPanIntoScrollValue()
    {
        var r = PanScrollBar.FromPan(50f, 380f, 0f, 0f);
        Assert.Equal(-50f, r.Value, 4);
    }

    // Larger on-screen content extent (as produced by zooming in) widens the range.
    [Fact]
    public void FromPan_LargerContentExtent_WidensValueRange()
    {
        var small = PanScrollBar.FromPan(0f, 380f, -10f, 10f);
        var large = PanScrollBar.FromPan(0f, 380f, -100f, 100f);
        Assert.True(large.Maximum - large.Minimum > small.Maximum - small.Minimum);
    }

    // Out-of-band pan is clamped into the value range so the thumb can't leave the track.
    [Fact]
    public void FromPan_PanBeyondBand_ValueClampedToRange()
    {
        var r = PanScrollBar.FromPan(9999f, 380f, 0f, 0f);
        Assert.Equal(r.Minimum, r.Value, 4); // pan clamps to +190 → Value = -190 = Minimum
    }

    // pan → Value → PanFromValue must recover the (in-band) pan exactly.
    [Fact]
    public void FromPan_RoundTripsThroughPanFromValue()
    {
        const float pan = 73f;
        var r = PanScrollBar.FromPan(pan, 380f, -20f, 20f);
        Assert.Equal(pan, PanScrollBar.PanFromValue(r.Value), 4);
    }
}

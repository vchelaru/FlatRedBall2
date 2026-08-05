using AnimationEditor.App.Controls;
using AnimationEditor.Core.Rendering;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using System;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// The shared pan↔scrollbar binding (#415/#422/#604, extracted in #695): one
/// <see cref="PanScrollBinder"/> now drives the wireframe, PNG viewer, and Preview bars that
/// MainWindow used to wire three times over. These cover the part each of the three copies had to
/// get right independently — the echo-loop suppression that kept a bar-driven pan from writing
/// straight back into the bar — plus the scroll-value convention both
/// <see cref="IPanScrollTarget"/> implementations now share.
/// </summary>
public class PanScrollBinderTests
{
    /// <summary>
    /// Stands in for a viewport: records the pan it was handed and can raise
    /// <see cref="ViewChanged"/> on demand (or re-entrantly from a pan setter, the way a real
    /// control does).
    /// </summary>
    private sealed class FakeTarget : IPanScrollTarget
    {
        public ScrollBarRange Horizontal = new(-100f, 100f, 0f, 50f);
        public ScrollBarRange Vertical   = new(-80f, 80f, 0f, 40f);

        public float? PanX;
        public float? PanY;
        public int SetPanCalls;

        /// <summary>When true, setting a pan re-raises ViewChanged, as a real control's
        /// <c>RaiseViewChanged</c> does — the feedback loop the binder must swallow.</summary>
        public bool EchoViewChangedOnPan;

        public event Action? ViewChanged;

        public (ScrollBarRange Horizontal, ScrollBarRange Vertical) GetScrollBarRanges() =>
            (Horizontal, Vertical);

        public void SetPanX(float scrollValue)
        {
            PanX = scrollValue;
            SetPanCalls++;
            if (EchoViewChangedOnPan) Raise();
        }

        public void SetPanY(float scrollValue)
        {
            PanY = scrollValue;
            SetPanCalls++;
            if (EchoViewChangedOnPan) Raise();
        }

        public void Raise() => ViewChanged?.Invoke();
    }

    private static (FakeTarget Target, ScrollBar H, ScrollBar V) Attach()
    {
        var target = new FakeTarget();
        var h = new ScrollBar { Orientation = Orientation.Horizontal, Minimum = -1000, Maximum = 1000 };
        var v = new ScrollBar { Orientation = Orientation.Vertical,   Minimum = -1000, Maximum = 1000 };
        PanScrollBinder.Attach(target, h, v);
        return (target, h, v);
    }

    [AvaloniaFact]
    public void BarValueChange_DrivesTargetPanOnThatAxisOnly()
    {
        var (target, h, v) = Attach();

        h.Value = 42;

        Assert.Equal(42f, target.PanX);
        Assert.Null(target.PanY);

        v.Value = -17;

        Assert.Equal(-17f, target.PanY);
    }

    [AvaloniaFact]
    public void ViewChanged_PushesRangeAndThumbIntoBothBars()
    {
        var (target, h, v) = Attach();
        target.Horizontal = new ScrollBarRange(-200f, 200f, 25f, 60f);
        target.Vertical   = new ScrollBarRange(-150f, 150f, -30f, 45f);

        target.Raise();

        Assert.Equal(-200d, h.Minimum);
        Assert.Equal(200d, h.Maximum);
        Assert.Equal(60d, h.ViewportSize);
        Assert.Equal(25d, h.Value);   // Coerced to Minimum/Maximum unless those are applied first.
        Assert.Equal(-30d, v.Value);
    }

    [AvaloniaFact]
    public void ViewChanged_RaisedFromTheBarDrivenPan_DoesNotEchoBackIntoTheBar()
    {
        var (target, h, _) = Attach();
        target.EchoViewChangedOnPan = true;
        // A range whose Value disagrees with the bar: without suppression the echo would write it
        // back mid-drag and the thumb would fight the pointer.
        target.Horizontal = new ScrollBarRange(-200f, 200f, 999f, 60f);

        h.Value = 42;

        Assert.Equal(1, target.SetPanCalls);
        Assert.Equal(42d, h.Value);
    }

    // The scroll-end persistence hook isn't covered here: ScrollBar.Scroll is a plain
    // EventHandler<ScrollEventArgs>, not a routed event, so a headless test can't raise it without
    // a real pointer drag on a templated bar. The EndScroll filter and the null-callback guard are
    // a verbatim move of MainWindow's OnPreviewScrollEnded.

    /// <summary>
    /// Both implementations take a raw scrollbar value and invert it themselves, so a value handed
    /// to <see cref="IPanScrollTarget.SetPanX"/> comes back unchanged from
    /// <see cref="IPanScrollTarget.GetScrollBarRanges"/>. Before #695 the Preview panel took an
    /// already-inverted pan while the texture viewports took the raw value, and the host had to
    /// remember which was which.
    /// </summary>
    [AvaloniaFact]
    public void PreviewControl_ScrollValueRoundTripsThroughPan()
    {
        var ctrl = TestHelpers.BuildServices().CreatePreviewControl();
        ctrl.Measure(new Size(400, 300));
        ctrl.Arrange(new Rect(0, 0, 400, 300));

        ctrl.SetPanX(30f);
        ctrl.SetPanY(-20f);

        var (h, v) = ctrl.GetScrollBarRanges();
        Assert.Equal(30f, h.Value, 3);
        Assert.Equal(-20f, v.Value, 3);
    }
}

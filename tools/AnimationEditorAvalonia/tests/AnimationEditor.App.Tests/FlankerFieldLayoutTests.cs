using AnimationEditor.App.Controls;
using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Flanker fields (a value <c>TextBox</c>/<c>AutoCompleteBox</c> flanked by −/+ buttons inside one
/// bordered pill) must keep the value field confined to the center column (#501). Fluent's base
/// TextBox style forces a <c>MinWidth</c>/<c>MinHeight</c> (~64×32). When the pill's fixed width
/// makes the center slot narrower than that minimum, Avalonia arranges the oversized textbox
/// centered in the slot, overflowing horizontally over the −/+ buttons — invisible until focus turns
/// the textbox background/border opaque, at which point the focus highlight bleeds over the buttons.
/// Each field sets MinWidth/MinHeight 0 so it shrinks to its slot. These tests assert the value field
/// never horizontally overlaps either flanker button.
/// </summary>
public class FlankerFieldLayoutTests
{
    [AvaloniaTheory]
    [InlineData("GridSizeInput", "GridSizeMinusBtn", "GridSizePlusBtn")]
    [InlineData("SpeedInput", "SpeedDownBtn", "SpeedUpBtn")]
    public void FlankerField_StaysBetweenButtons(string fieldName, string minusName, string plusName)
    {
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Width = 1400;
        window.Height = 900;
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            var field = window.FindControl<Control>(fieldName)
                ?? throw new InvalidOperationException($"{fieldName} not found");
            var minus = window.FindControl<Button>(minusName)
                ?? throw new InvalidOperationException($"{minusName} not found");
            var plus = window.FindControl<Button>(plusName)
                ?? throw new InvalidOperationException($"{plusName} not found");

            AssertFieldBetweenButtons(fieldName, field, minus, plus);
        }
        finally { window.Close(); }
    }

    // The shared ZoomControl (wireframe + preview toolbars) has the same flanker layout, but its
    // −/+ buttons and percent field live in the control's own namescope, so they're resolved by
    // descending each ZoomControl's subtree rather than by window-level name lookup. PngZoom is
    // excluded: its pane is collapsed by default so it isn't laid out.
    [AvaloniaTheory]
    [InlineData("WireframeZoom")]
    [InlineData("PreviewZoom")]
    public void ZoomControl_FieldStaysBetweenButtons(string zoomName)
    {
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Width = 1400;
        window.Height = 900;
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            var zoom = window.FindControl<ZoomControl>(zoomName)
                ?? throw new InvalidOperationException($"{zoomName} not found");

            var buttons = zoom.GetVisualDescendants().OfType<Button>().ToList();
            var minus = buttons.First(b => b.Name == "MinusBtn");
            var plus = buttons.First(b => b.Name == "PlusBtn");
            var field = zoom.GetVisualDescendants().OfType<AutoCompleteBox>().First();

            AssertFieldBetweenButtons(zoomName, field, minus, plus);
        }
        finally { window.Close(); }
    }

    private static void AssertFieldBetweenButtons(string fieldName, Control field, Button minus, Button plus)
    {
        // The value field is either a TextBox directly (SpeedInput, GridSizeInput) or the inner
        // TextBox of an AutoCompleteBox (the ZoomControl percent field). The inner TextBox is what
        // carries the focus background/border, so measure its edges — not the outer control's.
        var textBox = field as TextBox
            ?? field.GetVisualDescendants().OfType<TextBox>().FirstOrDefault()
            ?? throw new InvalidOperationException($"{fieldName} has no TextBox");

        // Project everything into the shared DockPanel space so the edges are comparable.
        var dock = (Control?)minus.Parent
            ?? throw new InvalidOperationException("flanker button has no parent");
        double fieldLeft = textBox.TranslatePoint(new Point(0, 0), dock)!.Value.X;
        double fieldRight = textBox.TranslatePoint(new Point(textBox.Bounds.Width, 0), dock)!.Value.X;

        const double eps = 0.5;
        Assert.True(fieldLeft >= minus.Bounds.Right - eps,
            $"{fieldName}: field left {fieldLeft} overlaps − button (right edge {minus.Bounds.Right})");
        Assert.True(fieldRight <= plus.Bounds.Left + eps,
            $"{fieldName}: field right {fieldRight} overlaps + button (left edge {plus.Bounds.Left})");
    }
}

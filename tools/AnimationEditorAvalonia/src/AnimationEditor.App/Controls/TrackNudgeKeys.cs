using AnimationEditor.Core.Sequences;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System;

namespace AnimationEditor.App.Controls;

/// <summary>
/// Arrow keys nudge the selected tracks while a control (the sequence preview, the timeline) has the focus: one unit, ten
/// with Shift, Up being +Y. Holding a key repeats the nudge as one undo run; the key coming up, or the focus leaving, ends
/// the run so the next press is its own undo entry. Ctrl, Alt and Cmd chords are left to their own shortcuts, and a key typed
/// into a text box (a track rename) is never taken.
/// </summary>
internal static class TrackNudgeKeys
{
    /// <summary>Wires <paramref name="control"/>: <paramref name="nudge"/> gets the step and returns whether it moved anything; <paramref name="endRun"/> closes the undo run.</summary>
    public static void Attach(Control control, Func<float, float, bool> nudge, Action endRun)
    {
        control.AddHandler(InputElement.KeyDownEvent, (_, e) =>
        {
            if (e.Handled || e.Source is TextBox || !TryDirection(e.Key, out var direction)) return;
            if ((e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) != 0) return;
            var (dx, dy) = TrackPlacement.NudgeStep(direction, e.KeyModifiers.HasFlag(KeyModifiers.Shift));
            if (nudge(dx, dy)) e.Handled = true;
        }, RoutingStrategies.Bubble);
        control.AddHandler(InputElement.KeyUpEvent, (_, e) =>
        {
            if (TryDirection(e.Key, out NudgeDirection _)) endRun();
        }, RoutingStrategies.Bubble, handledEventsToo: true);
        control.LostFocus += (_, _) => endRun();
    }

    private static bool TryDirection(Key key, out NudgeDirection direction)
    {
        direction = key switch
        {
            Key.Left => NudgeDirection.Left,
            Key.Right => NudgeDirection.Right,
            Key.Up => NudgeDirection.Up,
            _ => NudgeDirection.Down,
        };
        return key is Key.Left or Key.Right or Key.Up or Key.Down;
    }
}

using System;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace AnimationEditor.App.Controls;

/// <summary>
/// Where one run of live edits in an Inspector, and so one undo entry, ends: Enter in any of its fields, or the focus
/// leaving one. Listens on the Inspector itself, so a field needs no wiring of its own.
/// </summary>
internal static class InspectorUndoSteps
{
    public static void Wire(InputElement inspector, Action seal)
    {
        // Both bubble here after the field has committed. A field marks the Enter it commits on as handled.
        inspector.AddHandler(InputElement.LostFocusEvent, (_, _) => seal(), RoutingStrategies.Bubble, handledEventsToo: true);
        inspector.AddHandler(InputElement.KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Enter) seal();
        }, RoutingStrategies.Bubble, handledEventsToo: true);
    }
}

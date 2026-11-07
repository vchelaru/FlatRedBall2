using Avalonia.Media;
using System;

namespace AnimationEditor.App.Controls;

public sealed partial class SequenceTimelineControl
{
    // A pushed opacity of 1 still wraps everything drawn under it in a state change on the render thread, and the timeline
    // pushed one per block and per track header on every frame. Only a dimmed (hidden) track needs it.
    private static OptionalOpacity PushOpacityUnlessOpaque(DrawingContext context, double opacity) =>
        new(opacity < 1 ? context.PushOpacity(opacity) : null);

    private readonly struct OptionalOpacity(DrawingContext.PushedState? state) : IDisposable
    {
        public void Dispose() => state?.Dispose();
    }
}

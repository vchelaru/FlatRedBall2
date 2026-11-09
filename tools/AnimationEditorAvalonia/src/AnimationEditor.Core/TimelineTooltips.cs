using System;

namespace AnimationEditor.Core;

/// <summary>Shared timing for the timeline's quick tooltips.</summary>
public static class TimelineTooltips
{
    /// <summary>How long the pointer rests before a quick tooltip appears.</summary>
    public static readonly TimeSpan ShowDelay = TimeSpan.FromMilliseconds(200);
}

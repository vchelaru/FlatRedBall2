using System;

namespace AnimationEditor.Core;

/// <summary>How long the pointer rests before a hover tooltip shows. Every quick tooltip in the editor uses the same delay.</summary>
public static class HoverDelays
{
    /// <summary>The delay before a short, label-like tooltip (a timeline block's full text) appears.</summary>
    public static readonly TimeSpan QuickTooltip = TimeSpan.FromMilliseconds(200);
}

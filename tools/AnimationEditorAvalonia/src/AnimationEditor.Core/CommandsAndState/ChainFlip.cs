using System;

namespace AnimationEditor.Core.CommandsAndState;

/// <summary>
/// Which mirror axes a duplicated chain is flipped along. Combine with <c>|</c>; the axes apply
/// in Horizontal, Vertical, Diagonal order, the same as pressing each flip button in turn.
/// </summary>
[Flags]
public enum ChainFlip
{
    None = 0,
    Horizontal = 1,
    Vertical = 2,
    Diagonal = 4,
}

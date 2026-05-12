using System;

namespace AnimationEditor.Core.Rendering;

/// <summary>
/// Pure, SkiaSharp-free hit-tester for frame-rectangle drag handles.
/// All coordinates are in screen space (already transformed).
/// </summary>
public static class DragHandleHitTester
{
    /// <summary>
    /// Returns the <see cref="HandleKind"/> under the cursor at (<paramref name="ptX"/>, <paramref name="ptY"/>)
    /// for a rectangle whose screen-space edges are supplied.
    /// </summary>
    /// <param name="ptX">Mouse X in screen space.</param>
    /// <param name="ptY">Mouse Y in screen space.</param>
    /// <param name="left">Left edge of the rectangle in screen space.</param>
    /// <param name="top">Top edge of the rectangle in screen space.</param>
    /// <param name="right">Right edge of the rectangle in screen space.</param>
    /// <param name="bottom">Bottom edge of the rectangle in screen space.</param>
    /// <param name="hitRadius">Half-size of the hit area around each handle point.</param>
    /// <param name="handleOffset">
    /// Distance each handle center is pushed outward from the frame edge.
    /// Pass the same value used when drawing the handles so hit-testing aligns with rendering.
    /// </param>
    public static HandleKind GetHandleAt(
        float ptX, float ptY,
        float left, float top, float right, float bottom,
        float hitRadius = 7f,
        float handleOffset = 0f)
    {
        float cx = (left + right)  / 2f;
        float cy = (top  + bottom) / 2f;

        (float X, float Y, HandleKind Kind)[] handles =
        {
            (left  - handleOffset, top    - handleOffset, HandleKind.TopLeft),
            (cx,                   top    - handleOffset, HandleKind.TopCenter),
            (right + handleOffset, top    - handleOffset, HandleKind.TopRight),
            (left  - handleOffset, cy,                   HandleKind.MidLeft),
            (right + handleOffset, cy,                   HandleKind.MidRight),
            (left  - handleOffset, bottom + handleOffset, HandleKind.BotLeft),
            (cx,                   bottom + handleOffset, HandleKind.BotCenter),
            (right + handleOffset, bottom + handleOffset, HandleKind.BotRight),
        };

        foreach (var (hx, hy, kind) in handles)
            if (MathF.Abs(ptX - hx) <= hitRadius && MathF.Abs(ptY - hy) <= hitRadius)
                return kind;

        // Inside the rectangle body → Move
        if (ptX >= left && ptX <= right && ptY >= top && ptY <= bottom)
            return HandleKind.Move;

        return HandleKind.None;
    }
}

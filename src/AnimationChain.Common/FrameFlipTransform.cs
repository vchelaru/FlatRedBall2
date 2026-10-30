namespace FlatRedBall2.AnimationEditorCommon;

/// <summary>
/// Turns a frame's flip flags into the rotation offset and mirror axes a <c>SpriteBatch.Draw</c>
/// call needs. Shared by the engine's <c>Sprite</c> and the standalone <c>DrawAnimation</c> extension
/// so the two cannot disagree.
/// </summary>
/// <remarks>
/// <c>SpriteEffects</c> has no transpose value, and a diagonal flip is a reflection that rotation
/// alone cannot produce. A +/-90 degree rotation composed with a single-axis mirror is also a
/// reflection, and any 2D reflection can be written that way, so a diagonal flip becomes a quarter
/// turn plus (at most) a vertical mirror. Width, height, origin and scale stay untouched: the
/// quarter turn about the origin is what swaps the on-screen footprint.
/// </remarks>
internal static class FrameFlipTransform
{
    /// <summary>
    /// <paramref name="flipsY"/> is true when the destination batch's transform already mirrors Y
    /// (the engine's world-space camera), in which case a base vertical mirror cancels it; false
    /// for plain Y-down screen space.
    /// </summary>
    internal static (float RotationOffsetRadians, bool FlipHorizontally, bool FlipVertically) Compute(
        bool flipHorizontal, bool flipVertical, bool flipDiagonal, bool flipsY)
    {
        bool effectiveFlipVertical = flipsY ^ flipVertical;

        if (flipDiagonal)
        {
            float rotation = effectiveFlipVertical ? -MathF.PI / 2f : MathF.PI / 2f;
            bool flipV = flipHorizontal == effectiveFlipVertical;
            return (rotation, false, flipV);
        }

        return (0f, flipHorizontal, effectiveFlipVertical);
    }
}

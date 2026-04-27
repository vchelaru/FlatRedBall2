namespace FlatRedBall2.Rendering;

/// <summary>
/// A rectangle expressed as fractions of a host rectangle (0..1 on each axis), used by
/// <see cref="Camera.NormalizedViewport"/> to position cameras inside the window.
/// <para>
/// <c>(0, 0, 1, 1)</c> is the full host rect; <c>(0, 0, 0.5f, 1f)</c> is the left half;
/// <c>(0.5f, 0, 0.5f, 1f)</c> is the right half. Using fractions instead of pixel rects
/// keeps split-screen layouts correct across window resizes.
/// </para>
/// </summary>
public readonly record struct NormalizedRectangle(float X, float Y, float Width, float Height)
{
    /// <summary>The full host rect: <c>(0, 0, 1, 1)</c>. Default value of <see cref="Camera.NormalizedViewport"/>.</summary>
    public static readonly NormalizedRectangle FullViewport = new(0f, 0f, 1f, 1f);
}

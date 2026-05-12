namespace FlatRedBall2.Rendering;

/// <summary>
/// Determines how renderables within a single <see cref="Layer"/> are ordered before drawing.
/// Layer always wins over sort mode — a lower-indexed layer draws behind a higher-indexed
/// layer regardless of which mode is selected.
/// </summary>
public enum SortMode
{
    /// <summary>
    /// Sort by layer, then by Z ascending. This is the default.
    /// </summary>
    Z,

    /// <summary>
    /// Sort by layer, then by Z ascending. When two renderables share the same Z,
    /// the one whose parent entity has a higher world-space Y is drawn first (behind).
    /// This produces correct top-down draw order: entities lower on screen appear in front.
    /// </summary>
    ZSecondaryParentY,
}

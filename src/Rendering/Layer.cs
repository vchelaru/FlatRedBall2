namespace FlatRedBall2.Rendering;

/// <summary>
/// A named bucket that controls coarse draw order and coordinate space for renderables.
/// Renderables on a lower-indexed layer always draw behind those on a higher-indexed
/// layer, regardless of <see cref="IRenderable.Z"/>; within a single layer, Z and the
/// screen's <see cref="SortMode"/> determine ordering.
/// <para>
/// Create layers via <c>Screen.AddLayer(name)</c> and assign them by passing the layer
/// to <c>Screen.Add(renderable, layer)</c> or <c>Entity.Add(child, layer)</c>, or by
/// setting <see cref="IRenderable.Layer"/> directly.
/// </para>
/// </summary>
public class Layer
{
    /// <summary>Creates a new layer with the given diagnostic name.</summary>
    public Layer(string name) => Name = name;

    /// <summary>Diagnostic name shown in tooling and <see cref="ToString"/>.</summary>
    public string Name { get; }

    /// <summary>
    /// When <c>true</c> and passed to <c>Camera.Add</c>/<c>Screen.Add</c>, the Gum visual is
    /// parented to the owning camera's <c>Camera.ScreenSpaceRoot</c> instead of <c>Camera.UiRoot</c>:
    /// it stays confined to that camera's own viewport (correct in split-screen, unlike
    /// <c>Screen.AddOverlay</c>, which is screen-wide) but is immune to that camera's <c>Zoom</c>
    /// (unlike plain <c>Camera.Add</c>, which is intentionally zoom-coupled for cinematic effects).
    /// The window-vs-design-resolution scale still applies, so authored pixel positions still track
    /// window size — only <c>Zoom</c> is excluded.
    /// <para>
    /// Only Gum renderables added via <c>Camera.Add</c>/<c>Screen.Add</c> consult this flag today —
    /// Sprites, shapes, and tilemaps choose their <see cref="IRenderable.Batch"/> directly per
    /// instance and do not. Defaults to <c>false</c> (normal zoom-coupled HUD).
    /// </para>
    /// </summary>
    public bool IsScreenSpace { get; init; }

    /// <inheritdoc/>
    public override string ToString() => Name;
}

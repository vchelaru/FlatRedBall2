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
    /// When <c>true</c>, renderables on this layer are drawn in screen-space pixels with
    /// the camera transform bypassed — useful for HUD and UI that should not pan or zoom
    /// with the world. Defaults to <c>false</c> (world-space).
    /// </summary>
    public bool IsScreenSpace { get; init; }

    /// <inheritdoc/>
    public override string ToString() => Name;
}

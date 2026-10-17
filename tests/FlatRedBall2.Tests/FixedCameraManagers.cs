namespace FlatRedBall2.Tests;

/// <summary>
/// Stub Gum renderer exposing one real <see cref="RenderingLibrary.Camera"/> — the shared object
/// Gum's cursor hit-testing reads — so tests can run Gum hit-testing without a GraphicsDevice.
/// </summary>
internal sealed class FixedCameraRenderer : RenderingLibrary.Graphics.IRenderer
{
    public RenderingLibrary.Camera Camera { get; } = new RenderingLibrary.Camera();
    public System.Collections.ObjectModel.ReadOnlyCollection<RenderingLibrary.Graphics.Layer> Layers =>
        new(new System.Collections.Generic.List<RenderingLibrary.Graphics.Layer>());
    public RenderingLibrary.Graphics.Layer MainLayer => null!;
    public void RenderLayer(RenderingLibrary.ISystemManagers managers, RenderingLibrary.Graphics.Layer layer, bool prerender = true) { }
}

/// <summary>Stub <see cref="RenderingLibrary.ISystemManagers"/> over a <see cref="FixedCameraRenderer"/>.</summary>
internal sealed class FixedCameraManagers : RenderingLibrary.ISystemManagers
{
    public bool EnableTouchEvents { get; set; }
    public RenderingLibrary.Graphics.IRenderer Renderer { get; } = new FixedCameraRenderer();
    public void InvalidateSurface() { }

    /// <summary>Gum's shared cursor-hit-test camera.</summary>
    public RenderingLibrary.Camera GumCamera => ((FixedCameraRenderer)Renderer).Camera;
}

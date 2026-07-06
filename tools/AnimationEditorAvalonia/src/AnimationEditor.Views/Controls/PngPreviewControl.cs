namespace AnimationEditor.App.Controls;

/// <summary>
/// A read-only PNG viewer tab (issue #604). Inherits the full pan/zoom camera, canvas-palette
/// background, grid, scrollbar ranges, middle-mouse / Alt-left panning, and wheel zoom-at-cursor
/// from <see cref="TextureViewport"/> — it holds no animation-editor state, so it needs no body.
/// <para>
/// The grid is off by default (same as the wireframe). Enabling it is a one-liner via
/// <see cref="TextureViewport.SetGrid"/>; no toggle UI exists for the PNG tab yet.
/// </para>
/// </summary>
public sealed class PngPreviewControl : TextureViewport
{
}

using SkiaSharp;
using AvaloniaColor = Avalonia.Media.Color;

namespace AnimationEditor.App.Theming;

/// <summary>
/// Semantic colors for editor overlays and drag/drop indicators, named by what they mean
/// rather than which control draws them (#1241). Pick an existing role before adding a color;
/// a new role must be distinct from the others at a glance.
/// <para>
/// Theme-neutral by design: these read on both the dark and light canvas. Background and
/// chrome colors that must flip with the theme live in <see cref="CanvasPalette"/> (Skia) and
/// <c>ThemeTokens.axaml</c> (Avalonia).
/// </para>
/// </summary>
internal static class EditorColors
{
    private static readonly SKColor FrameBlue = new(80, 160, 255);
    private static readonly SKColor Gold = new(255, 220, 0);

    // ── Frame regions (wireframe) ────────────────────────────────────────────

    /// <summary>Opaque; fills composite through a layer at their tier's alpha (#817).</summary>
    public static readonly SKColor FrameFill = FrameBlue;
    public static readonly SKColor SelectedFrameStroke = FrameBlue.WithAlpha(230);
    public static readonly SKColor FrameStroke = FrameBlue.WithAlpha(120);
    /// <summary>Background of the "Frame N" hover tag.</summary>
    public static readonly SKColor FrameLabelBackground = FrameBlue.WithAlpha(235);
    public static readonly SKColor FrameLabelText = SKColors.White;

    /// <summary>A frame whose tree row is hovered (#1216).</summary>
    public static readonly SKColor TreeHover = new(190, 130, 255, 170);

    /// <summary>
    /// The frame a Ctrl+click would create (#1241). Drawn as a white dash over a dark
    /// underlay so it reads on any texture and any canvas background.
    /// </summary>
    public static readonly SKColor PendingAdd = SKColors.White.WithAlpha(230);
    public static readonly SKColor PendingAddUnderlay = SKColors.Black.WithAlpha(160);

    /// <summary>Frames on the clipboard from a Cut, not yet pasted.</summary>
    public static readonly SKColor PendingCut = new(224, 112, 48, 220);

    /// <summary>Per-tile cells inside a selected frame on a spaced grid (#1165).</summary>
    public static readonly SKColor GridCell = new(90, 220, 160, 230);

    /// <summary>The magic wand's hover region.</summary>
    public static readonly SKColor WandPreview = Gold.WithAlpha(180);

    /// <summary>Entity origin crosshair on the wireframe.</summary>
    public static readonly SKColor Origin = Gold.WithAlpha(230);

    /// <summary>Origin axes across the preview panel.</summary>
    public static readonly SKColor PreviewOriginAxes = new(100, 200, 100, 160);

    /// <summary>Opaque; the preview applies the frame's own alpha on top.</summary>
    public static readonly SKColor FrameBoundingBox = SKColors.White;

    // ── Handles ─────────────────────────────────────────────────────────────

    public static readonly SKColor HandleFill = SKColors.White;
    public static readonly SKColor HandleStroke = SKColors.DodgerBlue;

    // ── Collision shapes (preview) ───────────────────────────────────────────

    public static readonly SKColor Shape = new(0, 230, 80, 200);
    public static readonly SKColor SelectedShape = Gold.WithAlpha(230);
    /// <summary>Dashed bounding square around a selected circle.</summary>
    public static readonly SKColor SelectedShapeBounds = Gold.WithAlpha(120);
    /// <summary>A shape the runtime cannot collide with correctly (self-intersecting polygon).</summary>
    public static readonly SKColor InvalidShape = new(255, 60, 60, 230);
    public static readonly SKColor PolygonVertex = Gold;
    public static readonly SKColor PolygonVertexEdge = new(40, 40, 40);
    public static readonly SKColor PolygonMidpoint = Gold.WithAlpha(200);

    // ── Drag and drop (tree and tab strip) ───────────────────────────────────

    /// <summary>Insertion line and target box for tree and tab-strip drops.</summary>
    public static readonly AvaloniaColor DropIndicator = AvaloniaColor.FromRgb(0x4a, 0x90, 0xd9);
    /// <summary>Faint fill inside the drop target box.</summary>
    public static readonly AvaloniaColor DropTargetFill = AvaloniaColor.FromArgb(0x33, 0x4a, 0x90, 0xd9);
    public static readonly AvaloniaColor DragLabelBackground = AvaloniaColor.FromRgb(0x3a, 0x41, 0x50);
    public static readonly AvaloniaColor DragLabelText = AvaloniaColor.FromRgb(0xd4, 0xd8, 0xde);
}

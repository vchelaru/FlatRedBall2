using AnimationEditor.Core.IO;
using FlatRedBall2.AnimationEditorCommon;
using SkiaSharp;
using System.Collections.Generic;

namespace AnimationEditor.App.Controls;

/// <summary>
/// One chain's worth of matched frame rects for the PNG usage overlay (issue #953) — everything
/// <see cref="PngPreviewControl.SetUsageRegions"/> needs to draw one chain's boxes in its own color
/// and, on a click, report which chain/file to navigate to. Each <see cref="UsageFrameRegion.Rect"/>
/// is texture-space pixels, already normalized (Left&lt;Right, Top&lt;Bottom) so drawing/hit-testing
/// never has to re-check axis order.
/// </summary>
public sealed record UsageChainRegions(
    AchxFileEntry Entry,
    AnimationChainSave Chain,
    SKColor Color,
    IReadOnlyList<UsageFrameRegion> Rects);

/// <summary>
/// One matched frame's texture-space rect within a <see cref="UsageChainRegions"/> group.
/// <see cref="FrameIndex"/> is the frame's 1-based position within <c>Chain.Frames</c> — used for
/// the hover tag's <c>"file (N)"</c> label, same numbering as the wireframe editor's "Frame N".
/// </summary>
public readonly record struct UsageFrameRegion(int FrameIndex, SKRect Rect);

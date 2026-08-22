using AnimationEditor.Core.IO;
using FlatRedBall2.AnimationEditorCommon;
using SkiaSharp;
using System.Collections.Generic;

namespace AnimationEditor.App.Controls;

/// <summary>
/// One chain's worth of matched frame rects for the PNG usage overlay (issue #953) — everything
/// <see cref="PngPreviewControl.SetUsageRegions"/> needs to draw one chain's boxes in its own color
/// and, on a click, report which chain/file to navigate to. <see cref="Rects"/> are texture-space
/// pixels, already normalized (Left&lt;Right, Top&lt;Bottom) so drawing/hit-testing never has to
/// re-check axis order.
/// </summary>
public sealed record UsageChainRegions(
    AchxFileEntry Entry,
    AnimationChainSave Chain,
    SKColor Color,
    IReadOnlyList<SKRect> Rects);

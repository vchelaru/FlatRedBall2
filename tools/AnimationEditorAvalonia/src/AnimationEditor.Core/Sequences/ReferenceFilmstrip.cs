using AnimationEditor.Core.Keyframes;
using AnimationEditor.Core.Rendering;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.Sequences;

/// <summary>
/// One cell of a collapsed reference clip's filmstrip. <see cref="X"/> and <see cref="Width"/> are pixels from the clip's left
/// edge (<see cref="Width"/> is cut at the clip's end for the last cell); <see cref="FullWidth"/> is the cell's width before
/// that cut; <see cref="Time"/> is the clip-local second the cell's picture is sampled at.
/// </summary>
public readonly record struct ReferenceCell(int Cycle, double X, double Width, double FullWidth, double Time);

/// <summary>One sprite of a composite cell: everything that decides how it is drawn, so equal layers draw equal pixels.</summary>
public readonly record struct PoseLayer(
    string? Texture, float Left, float Right, float Top, float Bottom, bool FlipH, bool FlipV, bool FlipD,
    ResolvedFrameColor Color, float OffsetX, float OffsetY);

/// <summary>
/// What a sequence shows at an instant, as a cache key: its sprites back to front. Two instants with equal poses draw the
/// same picture, so a cell is rendered once however many instants and cycles show it.
/// </summary>
public sealed class ScenePose : IEquatable<ScenePose>
{
    private readonly int _hash;

    public ScenePose(PoseLayer[] layers)
    {
        Layers = layers;
        var hash = new HashCode();
        foreach (var layer in layers) hash.Add(layer);
        _hash = hash.ToHashCode();
    }

    /// <summary>The sprites, back to front.</summary>
    public IReadOnlyList<PoseLayer> Layers { get; }

    public bool Equals(ScenePose? other) => other is not null && _hash == other._hash && Layers.SequenceEqual(other.Layers);

    public override bool Equals(object? obj) => Equals(obj as ScenePose);

    public override int GetHashCode() => _hash;
}

/// <summary>Where a collapsed reference clip draws its composite filmstrip, and what each cell shows. Pure, so the control only paints.</summary>
public static class ReferenceFilmstrip
{
    /// <summary>The cell width a cycle is divided toward, in pixels: the film row's height, so a cell is about square.</summary>
    public const double CellPixels = TimelineLayout.FilmHeight;

    /// <summary>The most cells one cycle is divided into, however far the view is zoomed in; wider zoom makes wider cells.</summary>
    public const int MaxCellsPerCycle = 96;

    /// <summary>
    /// The cells of a clip of <paramref name="loops"/> cycles of a target <paramref name="targetLength"/> seconds long, at
    /// <paramref name="pixelsPerSecond"/>. Every cycle is cut into the same whole number of equal cells (about
    /// <see cref="CellPixels"/> wide), so each cycle repeats the same pictures and its boundary falls on a cell edge; a last
    /// partial cycle is cut at the clip's end. Empty when the target has no length.
    /// </summary>
    public static IReadOnlyList<ReferenceCell> Cells(double targetLength, float loops, double pixelsPerSecond)
    {
        if (targetLength <= 0 || loops <= 0 || pixelsPerSecond <= 0) return Array.Empty<ReferenceCell>();
        double cyclePixels = targetLength * pixelsPerSecond;
        int perCycle = Math.Clamp((int)Math.Round(cyclePixels / CellPixels), 1, MaxCellsPerCycle);
        double cellSeconds = targetLength / perCycle;
        double clipSeconds = SequenceTimeline.FullLength(loops, targetLength);
        var cells = new List<ReferenceCell>();
        for (int cycle = 0; cycle * targetLength < clipSeconds - 1e-9; cycle++)
        {
            for (int i = 0; i < perCycle; i++)
            {
                double start = cycle * targetLength + i * cellSeconds;
                if (start >= clipSeconds - 1e-9) break;
                double visible = Math.Min(cellSeconds, clipSeconds - start);
                cells.Add(new ReferenceCell(cycle, start * pixelsPerSecond, visible * pixelsPerSecond, cellSeconds * pixelsPerSecond,
                    start + visible / 2));
            }
        }
        return cells;
    }

    /// <summary>
    /// The pose of <paramref name="target"/> at clip-local second <paramref name="clipTime"/> of <paramref name="reference"/>,
    /// exactly what the sequence preview draws there (visible tracks layered, nested references included); null when nothing
    /// shows. <paramref name="file"/> is the file's sequences, which nested references resolve against.
    /// </summary>
    public static ScenePose? PoseAt(SequenceReferenceSave reference, SequenceSave target, double clipTime, ISequenceChainLookup lookup,
        IReadOnlyList<SequenceSave> file)
    {
        double length = SequenceTimeline.Length(target, chain => lookup.FindChain(chain) is { } found ? ChainClock.TotalLength(found) : 0);
        if (SequenceTimeline.ReferenceTime(reference, target, length, reference.Start + clipTime) is not { } inner) return null;
        var sprites = SequenceScene.At(target, 0, inner, lookup, file);
        if (sprites.Count == 0) return null;
        var layers = new PoseLayer[sprites.Count];
        for (int i = 0; i < layers.Length; i++)
        {
            var sprite = sprites[i];
            var frame = sprite.Frame;
            var sample = sprite.Sample();
            layers[i] = new PoseLayer(sprite.TexturePath, frame.LeftCoordinate, frame.RightCoordinate, frame.TopCoordinate, frame.BottomCoordinate,
                frame.FlipHorizontal, frame.FlipVertical, frame.FlipDiagonal, sample.Color, sample.OffsetX, sample.OffsetY);
        }
        return new ScenePose(layers);
    }
}

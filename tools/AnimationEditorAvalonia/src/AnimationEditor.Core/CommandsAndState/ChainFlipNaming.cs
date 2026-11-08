using AnimationEditor.Core.Rendering;
using FlatRedBall2.AnimationEditorCommon;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AnimationEditor.Core.CommandsAndState;

/// <summary>
/// Renames the direction words (Right, Left, Up, Down) in a duplicated chain's name to match how
/// the duplicate was flipped, so "WalkRight" flipped horizontally reads "WalkLeft".
/// </summary>
/// <remarks>
/// The mapping is the screen-space (Y down) transform the flip applies to what is displayed: the
/// frames' rendered matrix after the flip (<see cref="FlipScaleCalculator.ComputeMatrix"/>) times
/// the inverse of the matrix before it. H and V toggles always mirror the display; a Diagonal
/// toggle is the main-diagonal transpose (Right/Down, Left/Up swap) when the frame's H and V flags
/// agree and the anti-diagonal (Right/Up, Left/Down) when exactly one is set, matching the offset
/// baking in <c>FlipCommand.FlipFrame</c>. Toggles apply in Horizontal, Vertical, Diagonal order,
/// like <c>AppCommands.CloneChainWithFlip</c>. H+D and V+D are 90-degree rotations, not swaps.
/// Words match only as standalone PascalCase words: not followed by a lowercase letter.
/// </remarks>
internal static class ChainFlipNaming
{
    private static readonly Regex DirectionWord = new(@"(Right|Left|Up|Down)(?![a-z])", RegexOptions.Compiled);

    private readonly record struct Matrix(float A, float B, float C, float D);

    /// <summary>
    /// Returns <paramref name="name"/> with direction words mapped through <paramref name="flip"/>,
    /// or unchanged when it has none (or the flip leaves directions alone). When the diagonal's
    /// effect differs between <paramref name="sourceFrames"/> it has no single answer, so only the
    /// Horizontal and Vertical parts rename.
    /// </summary>
    public static string Rename(string name, ChainFlip flip, IReadOnlyList<AnimationFrameSave> sourceFrames)
    {
        var distinct = sourceFrames
            .Select(f => TransformOf(flip, f.FlipHorizontal, f.FlipVertical, f.FlipDiagonal))
            .Distinct()
            .ToList();

        Matrix transform = distinct.Count switch
        {
            0 => TransformOf(flip, false, false, false),
            1 => distinct[0],
            _ => TransformOf(flip & ~ChainFlip.Diagonal, false, false, false),
        };

        return DirectionWord.Replace(name, m => Word(Apply(transform, Direction(m.Value))));
    }

    // The displayed-content transform of toggling `flip` on a frame that starts with these flags.
    private static Matrix TransformOf(ChainFlip flip, bool h, bool v, bool d)
    {
        var before = FlipScaleCalculator.ComputeMatrix(h, v, d);
        if (flip.HasFlag(ChainFlip.Horizontal)) h = !h;
        if (flip.HasFlag(ChainFlip.Vertical)) v = !v;
        if (flip.HasFlag(ChainFlip.Diagonal)) d = !d;
        var after = FlipScaleCalculator.ComputeMatrix(h, v, d);

        // Flip matrices are signed permutations, so the inverse is the transpose.
        return new Matrix(
            after.a * before.a + after.b * before.b, after.a * before.c + after.b * before.d,
            after.c * before.a + after.d * before.b, after.c * before.c + after.d * before.d);
    }

    private static (float X, float Y) Direction(string word) => word switch
    {
        "Right" => (1, 0),
        "Left" => (-1, 0),
        "Down" => (0, 1),
        _ => (0, -1),
    };

    private static (float X, float Y) Apply(Matrix m, (float X, float Y) p) =>
        (m.A * p.X + m.B * p.Y, m.C * p.X + m.D * p.Y);

    private static string Word((float X, float Y) p) => p switch
    {
        (> 0, _) => "Right",
        (< 0, _) => "Left",
        (_, > 0) => "Down",
        _ => "Up",
    };
}

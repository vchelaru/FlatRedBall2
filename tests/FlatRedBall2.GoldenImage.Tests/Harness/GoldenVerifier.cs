using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit.Sdk;

namespace FlatRedBall2.GoldenImage;

/// <param name="GoldenDirectory">Folder of checked-in <c>&lt;name&gt;.png</c> goldens.</param>
/// <param name="ArtifactDirectory">Where <c>.actual.png</c>, <c>.expected.png</c> and <c>.diff.png</c> go on failure.</param>
/// <param name="UpdateGoldens">Write goldens instead of comparing. Only the CI regenerate workflow sets this.</param>
/// <param name="Tolerance">Allowed per-channel difference. llvmpipe is deterministic, so this absorbs Mesa-version drift, not noise.</param>
public sealed record GoldenOptions(
    string GoldenDirectory,
    string ArtifactDirectory,
    bool UpdateGoldens,
    int Tolerance = 2);

public enum GoldenOutcome
{
    Matched,
    Updated,
    MissingGolden,
    Mismatch,
    SizeMismatch,
}

public sealed record GoldenResult(GoldenOutcome Outcome, string Message)
{
    public bool Passed => Outcome is GoldenOutcome.Matched or GoldenOutcome.Updated;
}

public static partial class GoldenVerifier
{
    [GeneratedRegex("^[A-Za-z0-9_-]+$")]
    private static partial Regex ValidName();

    /// <summary>
    /// Compares <paramref name="actual"/> to the golden called <paramref name="name"/> and returns
    /// the outcome without throwing. Failures leave their images in
    /// <see cref="GoldenOptions.ArtifactDirectory"/>.
    /// </summary>
    public static GoldenResult Check(string name, PixelImage actual, GoldenOptions options)
    {
        if (!ValidName().IsMatch(name))
            throw new ArgumentException("Golden names are file names: letters, digits, '-' and '_' only.", nameof(name));

        string goldenPath = Path.Combine(options.GoldenDirectory, name + ".png");
        PixelImage? expected = File.Exists(goldenPath) ? PngCodec.Load(goldenPath) : null;

        if (options.UpdateGoldens)
        {
            // Leave an unchanged golden untouched so a PNG-encoder change cannot churn every file.
            if (expected is not null && GoldenComparer.Compare(expected, actual, tolerance: 0).IsMatch)
                return new GoldenResult(GoldenOutcome.Matched, $"{name}: unchanged");

            PngCodec.Save(actual, goldenPath);
            return new GoldenResult(GoldenOutcome.Updated, $"{name}: wrote {goldenPath}");
        }

        if (expected is null)
        {
            string actualPath = Path.Combine(options.ArtifactDirectory, name + ".actual.png");
            PngCodec.Save(actual, actualPath);
            return new GoldenResult(GoldenOutcome.MissingGolden,
                $"{name}: no golden at {goldenPath}. Run the 'Regenerate golden images' workflow on this branch " +
                $"(the rendered image is in the artifacts as {name}.actual.png).");
        }

        var comparison = GoldenComparer.Compare(expected, actual, options.Tolerance);
        if (comparison.IsMatch)
            return new GoldenResult(GoldenOutcome.Matched, $"{name}: {comparison.Summary}");

        PngCodec.Save(actual, Path.Combine(options.ArtifactDirectory, name + ".actual.png"));
        PngCodec.Save(expected, Path.Combine(options.ArtifactDirectory, name + ".expected.png"));
        if (comparison.Diff is not null)
            PngCodec.Save(comparison.Diff, Path.Combine(options.ArtifactDirectory, name + ".diff.png"));

        var outcome = comparison.Outcome == ComparisonOutcome.SizeMismatch
            ? GoldenOutcome.SizeMismatch
            : GoldenOutcome.Mismatch;
        string files = comparison.Diff is null
            ? $"{name}.actual.png and {name}.expected.png"
            : $"{name}.actual.png, {name}.expected.png and {name}.diff.png";
        return new GoldenResult(outcome,
            $"{name}: {comparison.Summary}. See {files} in {options.ArtifactDirectory} " +
            "(CI uploads them as the golden-image-failures artifact).");
    }
}

public static class GoldenImage
{
    /// <summary>Checks <paramref name="actual"/> against the golden using the CI environment's settings; fails the test on any problem.</summary>
    public static void Verify(string name, PixelImage actual)
    {
        var result = GoldenVerifier.Check(name, actual, GoldenEnvironment.Options());
        if (!result.Passed)
            throw new XunitException(result.Message);
    }
}

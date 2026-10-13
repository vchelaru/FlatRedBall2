using System;
using System.IO;
using Microsoft.Xna.Framework;
using Shouldly;
using Xunit;

namespace FlatRedBall2.GoldenImage.Tests;

public sealed class GoldenVerifierTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "frb2-golden-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private GoldenOptions Options(bool update = false, int tolerance = 2) =>
        new(Path.Combine(_root, "golden"), Path.Combine(_root, "artifacts"), update, tolerance);

    private static PixelImage Image(int width, int height, Color color)
    {
        var pixels = new Color[width * height];
        Array.Fill(pixels, color);
        return new PixelImage(width, height, pixels);
    }

    private void WriteGolden(string name, PixelImage image) =>
        PngCodec.Save(image, Path.Combine(_root, "golden", name + ".png"));

    [Fact]
    public void Check_MatchingGolden_PassesAndWritesNoArtifacts()
    {
        WriteGolden("scene", Image(2, 2, new Color(10, 20, 30, 255)));

        var result = GoldenVerifier.Check("scene", Image(2, 2, new Color(11, 20, 30, 255)), Options(tolerance: 2));

        result.Outcome.ShouldBe(GoldenOutcome.Matched);
        Directory.Exists(Path.Combine(_root, "artifacts")).ShouldBeFalse();
    }

    [Fact]
    public void Check_MismatchingGolden_FailsAndWritesActualExpectedAndDiff()
    {
        WriteGolden("scene", Image(2, 2, new Color(10, 20, 30, 255)));

        var result = GoldenVerifier.Check("scene", Image(2, 2, new Color(90, 20, 30, 255)), Options());

        result.Outcome.ShouldBe(GoldenOutcome.Mismatch);
        result.Passed.ShouldBeFalse();
        foreach (var suffix in new[] { "actual", "expected", "diff" })
            File.Exists(Path.Combine(_root, "artifacts", $"scene.{suffix}.png")).ShouldBeTrue(suffix);
        PngCodec.Load(Path.Combine(_root, "artifacts", "scene.actual.png"))[0, 0].ShouldBe(new Color(90, 20, 30, 255));
    }

    [Fact]
    public void Check_SizeMismatch_FailsWithActualButNoDiff()
    {
        WriteGolden("scene", Image(2, 2, Color.White));

        var result = GoldenVerifier.Check("scene", Image(3, 3, Color.White), Options());

        result.Outcome.ShouldBe(GoldenOutcome.SizeMismatch);
        File.Exists(Path.Combine(_root, "artifacts", "scene.actual.png")).ShouldBeTrue();
        File.Exists(Path.Combine(_root, "artifacts", "scene.diff.png")).ShouldBeFalse();
    }

    [Fact]
    public void Check_MissingGolden_FailsAndSavesActualForRegeneration()
    {
        var result = GoldenVerifier.Check("scene", Image(2, 2, Color.Red), Options());

        result.Outcome.ShouldBe(GoldenOutcome.MissingGolden);
        result.Passed.ShouldBeFalse();
        result.Message.ShouldContain("Regenerate golden images");
        File.Exists(Path.Combine(_root, "artifacts", "scene.actual.png")).ShouldBeTrue();
    }

    [Fact]
    public void Check_UpdateMode_WritesTheGolden()
    {
        var actual = Image(2, 2, new Color(1, 2, 3, 255));

        var result = GoldenVerifier.Check("scene", actual, Options(update: true));

        result.Outcome.ShouldBe(GoldenOutcome.Updated);
        PngCodec.Load(Path.Combine(_root, "golden", "scene.png"))[1, 1].ShouldBe(new Color(1, 2, 3, 255));
    }

    [Fact]
    public void Check_UpdateModeWithIdenticalGolden_LeavesFileUntouched()
    {
        WriteGolden("scene", Image(2, 2, Color.Blue));
        string path = Path.Combine(_root, "golden", "scene.png");
        var before = File.GetLastWriteTimeUtc(path);
        File.SetLastWriteTimeUtc(path, before.AddHours(-1));
        var stamped = File.GetLastWriteTimeUtc(path);

        var result = GoldenVerifier.Check("scene", Image(2, 2, Color.Blue), Options(update: true));

        result.Outcome.ShouldBe(GoldenOutcome.Matched);
        File.GetLastWriteTimeUtc(path).ShouldBe(stamped);
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("sub/dir")]
    [InlineData("")]
    public void Check_NameThatIsNotAFileName_Throws(string name)
    {
        Should.Throw<ArgumentException>(() => GoldenVerifier.Check(name, Image(1, 1, Color.White), Options()));
    }

    [Fact]
    public void PngCodec_RoundTrip_PreservesEveryChannel()
    {
        var image = new PixelImage(2, 1, new[] { new Color(1, 2, 3, 4), new Color(250, 251, 252, 253) });

        var decoded = PngCodec.Decode(PngCodec.Encode(image));

        decoded.Width.ShouldBe(2);
        decoded.Height.ShouldBe(1);
        decoded.Pixels.ShouldBe(image.Pixels);
    }
}

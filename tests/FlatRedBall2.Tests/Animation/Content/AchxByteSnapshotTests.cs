using System;
using System.IO;
using FlatRedBall2.Animation.Content;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Animation.Content;

// Byte-level snapshot guard: a canonical minimal .achx is built in code, saved, and diffed
// against a checked-in expected file. Any writer change that alters the on-disk byte shape will
// surface here. Hand-rolled (no Verify.Xunit) per design point #9 — one snapshot file isn't
// worth pulling in another dependency.
public class AchxByteSnapshotTests
{
    private static AnimationChainListSave BuildCanonicalMinimal()
    {
        var save = new AnimationChainListSave
        {
            FileRelativeTextures = true,
            TimeMeasurementUnit = TimeMeasurementUnit.Second,
            CoordinateType = TextureCoordinateType.UV,
            ProjectFile = "../project.gluj",
        };
        var chain = new AnimationChainSave { Name = "OneOfEach" };
        var frame = new AnimationFrameSave
        {
            TextureName = "Sheet.png",
            FrameLength = 0.1f,
            LeftCoordinate = 0f,
            RightCoordinate = 0.5f,
            TopCoordinate = 0f,
            BottomCoordinate = 0.5f,
            FlipHorizontal = true,
            RelativeX = 5f,
            ShapesSave = new ShapesSave(),
        };
        frame.ShapesSave.AARectSaves.Add(new AARectSave
        {
            Name = "Hit", X = 1, Y = 2, ScaleX = 3, ScaleY = 4,
        });
        frame.ShapesSave.CircleSaves.Add(new CircleSave
        {
            Name = "Origin", X = 5, Y = 6, Radius = 7,
        });
        var poly = new PolygonSave { Name = "Edge", X = 0, Y = 0 };
        poly.Points.Add(new Vector2Save { X = 0, Y = 0 });
        poly.Points.Add(new Vector2Save { X = 10, Y = 0 });
        frame.ShapesSave.PolygonSaves.Add(poly);
        chain.Frames.Add(frame);
        save.AnimationChains.Add(chain);
        return save;
    }

    [Fact]
    public void Save_CanonicalMinimal_MatchesCheckedInBytes()
    {
        var save = BuildCanonicalMinimal();
        var tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".achx");
        try
        {
            save.Save(tempPath);
            var actual = File.ReadAllBytes(tempPath);
            var expectedPath = Path.Combine(AppContext.BaseDirectory,
                "Animation", "Content", "Snapshot", "CanonicalMinimal.expected.achx");
            var expected = File.ReadAllBytes(expectedPath);

            // On byte mismatch, write the actual output next to the expected file so the
            // diff is inspectable locally.
            if (!actual.AsSpan().SequenceEqual(expected))
            {
                var actualPath = expectedPath + ".actual";
                File.WriteAllBytes(actualPath, actual);
                throw new Shouldly.ShouldAssertException(
                    $"CanonicalMinimal byte snapshot drifted. Actual written to {actualPath}; diff against expected to inspect.");
            }
        }
        finally { if (File.Exists(tempPath)) File.Delete(tempPath); }
    }
}

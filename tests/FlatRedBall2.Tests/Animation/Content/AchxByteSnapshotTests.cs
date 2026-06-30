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
        frame.ShapesSave.Shapes.Add(new AARectSave
        {
            Name = "Hit", X = 1, Y = 2, ScaleX = 3, ScaleY = 4,
        });
        frame.ShapesSave.Shapes.Add(new CircleSave
        {
            Name = "Origin", X = 5, Y = 6, Radius = 7,
        });
        var poly = new PolygonSave { Name = "Edge", X = 0, Y = 0 };
        poly.Points.Add(new Vector2Save { X = 0, Y = 0 });
        poly.Points.Add(new Vector2Save { X = 10, Y = 0 });
        frame.ShapesSave.Shapes.Add(poly);
        chain.Frames.Add(frame);
        save.AnimationChains.Add(chain);
        return save;
    }

    // Loading a real FRB1-authored .achx and saving it back must be byte-identical: re-saving an
    // unedited legacy file should produce a no-op git diff (issue #503). The earlier failure was
    // float-literal drift — FRB1 wrote the shortest round-trippable form (e.g. -5.0416665) but the
    // writer's G7-then-G9 fallback re-emitted a longer string (-5.04166651) for the same IEEE-754
    // value, churning ~45% of coordinates on every save.
    [Theory]
    [InlineData("KidDefenseFireball_FlatTextures.achx")]
    [InlineData("KidDefenseFireball_ParentTraversal.achx")]
    public void Save_AfterLoadingFrb1Corpus_IsByteIdentical(string corpusFileName)
    {
        var corpusPath = Path.Combine(AppContext.BaseDirectory,
            "Animation", "Content", "Corpus", corpusFileName);
        var expected = File.ReadAllBytes(corpusPath);

        var loaded = AnimationChainListSave.FromFile(corpusPath);
        var tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".achx");
        try
        {
            loaded.Save(tempPath);
            var actual = File.ReadAllBytes(tempPath);

            if (!actual.AsSpan().SequenceEqual(expected))
            {
                var actualPath = corpusPath + ".actual";
                File.WriteAllBytes(actualPath, actual);
                throw new Shouldly.ShouldAssertException(
                    $"{corpusFileName} re-save drifted from the FRB1 original. Actual written to {actualPath}; diff against the corpus file to inspect.");
            }
        }
        finally { if (File.Exists(tempPath)) File.Delete(tempPath); }
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

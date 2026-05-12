using System;
using System.IO;
using System.Linq;
using FlatRedBall2.Animation.Content;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Animation.Content;

// Round-trip tests: Save() then FromFile() must produce a structurally-equal AnimationChainListSave.
// Object-graph equality (field-by-field), not byte-exact — that distinction is per design point
// #9 in the migration discussion on issue #141. Byte-exact lives in AchxByteSnapshotTests.
//
// Includes the gold-master corpus (real KidDefense .achx files) which doubles as the back-compat
// reader test for FRB1-era files (design point #13).
public class AchxRoundTripTests
{
    private static AnimationChainListSave RoundTrip(AnimationChainListSave save)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".achx");
        try
        {
            save.Save(tempPath);
            return AnimationChainListSave.FromFile(tempPath);
        }
        finally { if (File.Exists(tempPath)) File.Delete(tempPath); }
    }

    private static AnimationChainListSave RoundTripFromCorpus(string corpusFileName)
    {
        var corpusPath = Path.Combine(AppContext.BaseDirectory, "Animation", "Content", "Corpus", corpusFileName);
        var loaded = AnimationChainListSave.FromFile(corpusPath);
        return RoundTrip(loaded);
    }

    private static AnimationChainListSave Load(string corpusFileName)
        => AnimationChainListSave.FromFile(
            Path.Combine(AppContext.BaseDirectory, "Animation", "Content", "Corpus", corpusFileName));

    private static void AssertStructurallyEqual(AnimationChainListSave expected, AnimationChainListSave actual)
    {
        actual.FileRelativeTextures.ShouldBe(expected.FileRelativeTextures);
        actual.TimeMeasurementUnit.ShouldBe(expected.TimeMeasurementUnit);
        actual.CoordinateType.ShouldBe(expected.CoordinateType);
        actual.ProjectFile.ShouldBe(expected.ProjectFile);
        actual.AnimationChains.Count.ShouldBe(expected.AnimationChains.Count);

        for (int c = 0; c < expected.AnimationChains.Count; c++)
        {
            var ec = expected.AnimationChains[c];
            var ac = actual.AnimationChains[c];
            ac.Name.ShouldBe(ec.Name);
            ac.Frames.Count.ShouldBe(ec.Frames.Count);
            for (int f = 0; f < ec.Frames.Count; f++)
                AssertFrameEqual(ec.Frames[f], ac.Frames[f]);
        }
    }

    private static void AssertFrameEqual(AnimationFrameSave expected, AnimationFrameSave actual)
    {
        actual.TextureName.ShouldBe(expected.TextureName);
        actual.FrameLength.ShouldBe(expected.FrameLength);
        actual.LeftCoordinate.ShouldBe(expected.LeftCoordinate);
        actual.RightCoordinate.ShouldBe(expected.RightCoordinate);
        actual.TopCoordinate.ShouldBe(expected.TopCoordinate);
        actual.BottomCoordinate.ShouldBe(expected.BottomCoordinate);
        actual.FlipHorizontal.ShouldBe(expected.FlipHorizontal);
        actual.FlipVertical.ShouldBe(expected.FlipVertical);
        actual.RelativeX.ShouldBe(expected.RelativeX);
        actual.RelativeY.ShouldBe(expected.RelativeY);

        if (expected.ShapesSave == null)
        {
            // Empty <ShapeCollectionSave> children round-trip into a non-null ShapesSave with empty
            // lists; treat that as structurally equal to the missing-shapes input case.
            if (actual.ShapesSave != null)
            {
                actual.ShapesSave.AARectSaves.ShouldBeEmpty();
                actual.ShapesSave.CircleSaves.ShouldBeEmpty();
                actual.ShapesSave.PolygonSaves.ShouldBeEmpty();
            }
            return;
        }

        actual.ShapesSave.ShouldNotBeNull();
        actual.ShapesSave!.AARectSaves.Count.ShouldBe(expected.ShapesSave.AARectSaves.Count);
        for (int i = 0; i < expected.ShapesSave.AARectSaves.Count; i++)
        {
            var er = expected.ShapesSave.AARectSaves[i];
            var ar = actual.ShapesSave.AARectSaves[i];
            ar.Name.ShouldBe(er.Name);
            ar.X.ShouldBe(er.X);
            ar.Y.ShouldBe(er.Y);
            ar.ScaleX.ShouldBe(er.ScaleX);
            ar.ScaleY.ShouldBe(er.ScaleY);
        }
        actual.ShapesSave.CircleSaves.Count.ShouldBe(expected.ShapesSave.CircleSaves.Count);
        for (int i = 0; i < expected.ShapesSave.CircleSaves.Count; i++)
        {
            var ec = expected.ShapesSave.CircleSaves[i];
            var ac = actual.ShapesSave.CircleSaves[i];
            ac.Name.ShouldBe(ec.Name);
            ac.X.ShouldBe(ec.X);
            ac.Y.ShouldBe(ec.Y);
            ac.Radius.ShouldBe(ec.Radius);
        }
        actual.ShapesSave.PolygonSaves.Count.ShouldBe(expected.ShapesSave.PolygonSaves.Count);
        for (int i = 0; i < expected.ShapesSave.PolygonSaves.Count; i++)
        {
            var ep = expected.ShapesSave.PolygonSaves[i];
            var ap = actual.ShapesSave.PolygonSaves[i];
            ap.Name.ShouldBe(ep.Name);
            ap.X.ShouldBe(ep.X);
            ap.Y.ShouldBe(ep.Y);
            ap.Points.Count.ShouldBe(ep.Points.Count);
            for (int j = 0; j < ep.Points.Count; j++)
            {
                ap.Points[j].X.ShouldBe(ep.Points[j].X);
                ap.Points[j].Y.ShouldBe(ep.Points[j].Y);
            }
        }
    }

    [Fact]
    public void RoundTrip_KidDefenseFireball_FlatTextures_ProducesStructurallyEqualGraph()
    {
        var loaded = Load("KidDefenseFireball_FlatTextures.achx");
        var roundTripped = RoundTrip(loaded);

        AssertStructurallyEqual(loaded, roundTripped);
        // Sanity: this corpus carries ProjectFile and TimeMeasurementUnit=Undefined, both
        // FRB1-era features that the back-compat reader must preserve.
        roundTripped.ProjectFile.ShouldBe("../../../../kiddefense.gluj");
        roundTripped.TimeMeasurementUnit.ShouldBe(TimeMeasurementUnit.Undefined);
    }

    [Fact]
    public void RoundTrip_KidDefenseFireball_ParentTraversal_PreservesRelativePaths()
    {
        var loaded = Load("KidDefenseFireball_ParentTraversal.achx");
        var roundTripped = RoundTrip(loaded);

        AssertStructurallyEqual(loaded, roundTripped);
        // The whole point of this corpus variant: ../ paths must survive a round-trip without
        // being collapsed or resolved.
        roundTripped.AnimationChains[0].Frames[0].TextureName.ShouldBe("../../../Entity_Sheet.png");
    }

    [Fact]
    public void RoundTrip_PolygonWithSinglePoint_PreservesPoint()
    {
        // Synthetic minimal case — exercising the degenerate single-point polygon would be
        // noisy to verify inside a real-world corpus file.
        var save = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "X" };
        var frame = new AnimationFrameSave { TextureName = "a.png", FrameLength = 0.1f };
        frame.ShapesSave = new ShapesSave();
        var poly = new PolygonSave { Name = "Dot", X = 0, Y = 0 };
        poly.Points.Add(new Vector2Save { X = 5, Y = 7 });
        frame.ShapesSave.PolygonSaves.Add(poly);
        chain.Frames.Add(frame);
        save.AnimationChains.Add(chain);

        var roundTripped = RoundTrip(save);

        var p = roundTripped.AnimationChains[0].Frames[0].ShapesSave!.PolygonSaves[0];
        p.Points.Count.ShouldBe(1);
        p.Points[0].X.ShouldBe(5f);
        p.Points[0].Y.ShouldBe(7f);
    }

    [Fact]
    public void RoundTrip_RelativePathWithParentTraversal_PreservesExactString()
    {
        // Synthetic minimal — same intent as the corpus parent-traversal test but isolated.
        var save = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "X" };
        chain.Frames.Add(new AnimationFrameSave
        {
            TextureName = "../../sprites/hero.png",
            FrameLength = 0.1f,
        });
        save.AnimationChains.Add(chain);

        var roundTripped = RoundTrip(save);

        roundTripped.AnimationChains[0].Frames[0].TextureName.ShouldBe("../../sprites/hero.png");
    }

    [Fact]
    public void RoundTrip_TextureName_PreservesCase()
    {
        // AE relies on case being preserved through the writer — texture filenames are matched
        // against the project's tracked PNGs and a case change would silently mis-resolve them
        // on case-sensitive filesystems (Linux).
        var save = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "X" };
        chain.Frames.Add(new AnimationFrameSave { TextureName = "Hero.PNG", FrameLength = 0.1f });
        save.AnimationChains.Add(chain);

        var roundTripped = RoundTrip(save);

        roundTripped.AnimationChains[0].Frames[0].TextureName.ShouldBe("Hero.PNG");
    }
}

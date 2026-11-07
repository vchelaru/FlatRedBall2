using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.IO;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>Which sprites <see cref="SequenceScene.At"/> shows at an instant, and where.</summary>
public class SequenceSceneTests
{
    // body/body.achj: Walk, two 0.25 s frames on body.png. fx/fx.achj: Spark, one 0.5 s frame on spark.png.
    // The sources sit in different folders, so each texture resolves against its own source's folder.
    private static SequenceDocument Load(TestHelpers.TempDir dir, params SequenceTrackSave[] tracks)
    {
        WriteSource(Path.Combine(dir.Path, "body", "body.achj"), "Walk", "body.png", 0.25f, 0.25f);
        WriteSource(Path.Combine(dir.Path, "fx", "fx.achj"), "Spark", "spark.png", 0.5f);
        var save = new SequenceListSave { Sources = { "body/body.achj", "fx/fx.achj" } };
        var sequence = new SequenceSave { Name = "Attack" };
        sequence.Tracks.AddRange(tracks);
        save.Sequences.Add(sequence);
        string path = Path.Combine(dir.Path, "boss.seqj");
        save.Save(path);
        return SequenceDocument.Load(path);
    }

    private static void WriteSource(string path, string chain, string texture, params float[] lengths)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var list = new AnimationChainListSave();
        var save = new AnimationChainSave { Name = chain };
        foreach (float length in lengths)
            save.Frames.Add(new AnimationFrameSave { TextureName = texture, FrameLength = length, RightCoordinate = 1, BottomCoordinate = 1 });
        list.AnimationChains.Add(save);
        list.SaveJson(path);
    }

    private static SequenceTrackSave Track(string name, int z, params (string Source, string Chain, double Start, float Loops)[] chains)
    {
        var track = new SequenceTrackSave { Name = name, Z = z };
        foreach (var (source, chain, start, loops) in chains)
            track.Chains.Add(new SequenceChainReferenceSave { Source = source, Chain = chain, Start = start, Loops = loops });
        return track;
    }

    [Fact]
    public void At_TwoTracksFromTwoSources_DrawsBothInZOrderWithTheirOwnTextures()
    {
        using var dir = new TestHelpers.TempDir();
        // The first track draws on top: its Z is higher.
        var document = Load(dir,
            Track("Fx", 5, ("fx/fx.achj", "Spark", 0, 1)),
            Track("Body", 1, ("body/body.achj", "Walk", 0, 1)));

        var sprites = SequenceScene.At(document, 0, 0.3);

        sprites.Select(s => s.Chain.Name).ShouldBe(new[] { "Walk", "Spark" });
        sprites.Select(s => s.Track).ShouldBe(new[] { 1, 0 });
        sprites[0].FrameIndex.ShouldBe(1);
        sprites[0].TexturePath.ShouldBe(new Paths.FilePath(Path.Combine(dir.Path, "body", "body.png")).FullPath);
        sprites[1].TexturePath.ShouldBe(new Paths.FilePath(Path.Combine(dir.Path, "fx", "spark.png")).FullPath);
    }

    [Fact]
    public void At_EqualZ_DrawsInTrackOrder()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir,
            Track("Body", 0, ("body/body.achj", "Walk", 0, 1)),
            Track("Fx", 0, ("fx/fx.achj", "Spark", 0, 1)));

        SequenceScene.At(document, 0, 0.1).Select(s => s.Track).ShouldBe(new[] { 0, 1 });
    }

    [Fact]
    public void At_BeforeItsChainStarts_TheTrackIsHidden()
    {
        // Hidden before the chain on purpose: this is how a sprite appears partway through a sequence.
        // If it showed before its chain, it would always be visible.
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir, Track("Body", 0, ("body/body.achj", "Walk", 0.4, 1)));

        SequenceScene.At(document, 0, 0).ShouldBeEmpty();
        SequenceScene.At(document, 0, 0.39).ShouldBeEmpty();
        SequenceScene.At(document, 0, 0.4).Single().FrameIndex.ShouldBe(0);
    }

    [Fact]
    public void At_AfterItsChainEnds_TheTrackIsHidden()
    {
        // Hidden after the chain on purpose: this is how a sprite disappears partway through a sequence.
        using var dir = new TestHelpers.TempDir();
        // Walk is 0.5 s, so this chain plays from 1 s to 1.5 s.
        var document = Load(dir, Track("Body", 0, ("body/body.achj", "Walk", 1, 1)));

        SequenceScene.At(document, 0, 1.2).Single().FrameIndex.ShouldBe(0);
        SequenceScene.At(document, 0, 1.5).ShouldBeEmpty();
        SequenceScene.At(document, 0, 2).ShouldBeEmpty();
    }

    [Fact]
    public void At_ACutChain_SwitchesToTheNextChainAtItsStart()
    {
        using var dir = new TestHelpers.TempDir();
        // Walk asks for 2 s (four cycles) but Spark starts at 0.6 s on the same track.
        var document = Load(dir, Track("Body", 0, ("body/body.achj", "Walk", 0, 4), ("fx/fx.achj", "Spark", 0.6, 1)));

        SequenceScene.At(document, 0, 0.55).Single().Chain.Name.ShouldBe("Walk");
        SequenceScene.At(document, 0, 0.65).Single().Chain.Name.ShouldBe("Spark");
    }

    [Fact]
    public void At_ALoopedChain_WrapsIntoItsCycleEvenWhenTheChainDoesNotLoop()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir, Track("Body", 0, ("body/body.achj", "Walk", 0, 3)));

        // 0.6 s is 0.1 s into the second 0.5 s cycle.
        var sprite = SequenceScene.At(document, 0, 0.6).Single();

        sprite.FrameIndex.ShouldBe(0);
        sprite.ElapsedInFrame.ShouldBe(0.1, 1e-6);
    }

    [Fact]
    public void Sample_AFlippedTrack_MirrorsTheFrameOffsetAndFlipAcrossTheTrack()
    {
        using var dir = new TestHelpers.TempDir();
        var track = Track("Body", 0, ("body/body.achj", "Walk", 0, 1));
        track.X = 10;
        track.Y = 4;
        track.FlipHorizontal = true;
        var document = Load(dir, track);
        var sprite = SequenceScene.At(document, 0, 0).Single();
        sprite.Frame.RelativeX = 3;
        sprite.Frame.RelativeY = 2;

        var sample = sprite.Sample();

        (sample.OffsetX, sample.OffsetY).ShouldBe((7f, 6f));
        sprite.FlipMatrix.ShouldBe((-1f, 0f, 0f, 1f));
    }

    [Fact]
    public void FlipMatrix_AnUnflippedTrack_IsNull()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir, Track("Body", 0, ("body/body.achj", "Walk", 0, 1)));

        SequenceScene.At(document, 0, 0).Single().FlipMatrix.ShouldBeNull();
    }

    [Fact]
    public void At_AHiddenTrack_IsLeftOut_AndTheOtherTrackStays()
    {
        using var dir = new TestHelpers.TempDir();
        var fx = Track("Fx", 5, ("fx/fx.achj", "Spark", 0, 1));
        fx.Visible = false;
        var document = Load(dir, fx, Track("Body", 1, ("body/body.achj", "Walk", 0, 1)));

        SequenceScene.At(document, 0, 0.1).Select(s => s.Chain.Name).ShouldBe(new[] { "Walk" });
    }

    [Fact]
    public void At_EveryTrackHidden_IsEmpty()
    {
        using var dir = new TestHelpers.TempDir();
        var fx = Track("Fx", 5, ("fx/fx.achj", "Spark", 0, 1));
        var body = Track("Body", 1, ("body/body.achj", "Walk", 0, 1));
        fx.Visible = false;
        body.Visible = false;
        var document = Load(dir, fx, body);

        SequenceScene.At(document, 0, 0.1).ShouldBeEmpty();
    }
}

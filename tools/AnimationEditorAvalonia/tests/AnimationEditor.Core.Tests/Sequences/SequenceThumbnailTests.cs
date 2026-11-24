using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>The instant, layout and cache key of a <c>.seqj</c> thumbnail (Open Project Folder rows and the sequence list).</summary>
public class SequenceThumbnailTests
{
    // body/body.achj: Walk, two 0.25 s frames on body.png (offset 4,2 on the first). fx/fx.achj: Spark, one 0.5 s frame.
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

    private static SequenceTrackSave Track(string name, params (string Source, string Chain, double Start, double? Length)[] chains)
    {
        var track = new SequenceTrackSave { Name = name };
        foreach (var (source, chain, start, length) in chains)
            track.Chains.Add(new SequenceChainReferenceSave { Source = source, Chain = chain, Start = start, Length = length });
        return track;
    }

    [Fact]
    public void ChooseTime_SomethingVisibleAtZero_IsZero()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir, Track("Body", ("body/body.achj", "Walk", 0, null)), Track("Fx", ("fx/fx.achj", "Spark", 0.3, null)));

        SequenceThumbnailScene.ChooseTime(document.Save.Sequences[0], document).ShouldBe(0);
    }

    [Fact]
    public void ChooseTime_EveryTrackStartsLater_IsTheEarliestStart()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir, Track("Body", ("body/body.achj", "Walk", 0.6, null)), Track("Fx", ("fx/fx.achj", "Spark", 0.4, null)));

        SequenceThumbnailScene.ChooseTime(document.Save.Sequences[0], document).ShouldBe(0.4);
        // Only the Spark is visible at that instant: Walk has not started.
        SequenceThumbnailScene.LayersFor(document.Save.Sequences[0], document).Count.ShouldBe(1);
    }

    // Boss: only a reference to Fx (Spark) at the given start. Fx: one Spark track.
    private static SequenceDocument LoadReferenceOnly(TestHelpers.TempDir dir, double start)
    {
        var document = Load(dir, Track("Fx", ("fx/fx.achj", "Spark", 0, null)));
        document.Save.Sequences[0].Name = "Fx";
        var boss = new SequenceSave { Name = "Boss" };
        boss.Tracks.Add(new SequenceTrackSave { Name = "Link", References = [new SequenceReferenceSave { Sequence = "Fx", Start = start }] });
        document.Save.Sequences.Add(boss);
        return document;
    }

    [Fact]
    public void LayersFor_AReferenceTrack_ShowsTheReferencedSequence()
    {
        using var dir = new TestHelpers.TempDir();
        var document = LoadReferenceOnly(dir, 0);
        var boss = document.Save.Sequences[1];

        SequenceThumbnailScene.LayersFor(boss, document).ShouldBeEmpty();
        SequenceThumbnailScene.LayersFor(boss, document, document.Save.Sequences)
            .Select(l => Path.GetFileName(l.TexturePath)).ShouldBe(new[] { "spark.png" });
    }

    [Fact]
    public void ChooseTime_AReferenceStartingLater_IsItsStart()
    {
        using var dir = new TestHelpers.TempDir();
        var document = LoadReferenceOnly(dir, 0.4);
        var boss = document.Save.Sequences[1];

        SequenceThumbnailScene.ChooseTime(boss, document, document.Save.Sequences).ShouldBe(0.4);
        SequenceThumbnailScene.LayersFor(boss, document, document.Save.Sequences).Count.ShouldBe(1);
    }

    [Fact]
    public void ChainReferencesOf_IncludesTheChainsOfReferencedSequences()
    {
        using var dir = new TestHelpers.TempDir();
        var document = LoadReferenceOnly(dir, 0);
        var boss = document.Save.Sequences[1];

        SequenceThumbnailScene.ChainReferencesOf(boss, document.Save.Sequences)
            .Select(r => r.Source + ":" + r.Chain).ShouldBe(new[] { "fx/fx.achj:Spark" });
    }

    [Fact]
    public void ChooseTime_AHiddenOrUnresolvedTrackDoesNotCount()
    {
        using var dir = new TestHelpers.TempDir();
        var hidden = Track("Hidden", ("body/body.achj", "Walk", 0, null));
        hidden.Visible = false;
        var document = Load(dir, hidden, Track("Gone", ("body/body.achj", "NoSuchChain", 0.1, null)), Track("Fx", ("fx/fx.achj", "Spark", 0.7, null)));

        SequenceThumbnailScene.ChooseTime(document.Save.Sequences[0], document).ShouldBe(0.7);
    }

    [Fact]
    public void ChooseTime_NothingResolves_IsNull()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir, Track("Gone", ("missing/missing.achj", "Walk", 0, null)));

        SequenceThumbnailScene.ChooseTime(document.Save.Sequences[0], document).ShouldBeNull();
        SequenceThumbnailScene.LayersFor(document.Save.Sequences[0], document).ShouldBeEmpty();
    }

    private static SequenceTrackSave Link(string name, string target, double start = 0) =>
        new() { Name = name, References = [new SequenceReferenceSave { Sequence = target, Start = start }] };

    [Fact]
    public void ChooseTime_NestedReferencesStartingLater_IsTheSumOfTheirStarts()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir, Track("Fx", ("fx/fx.achj", "Spark", 0, null)));
        document.Save.Sequences[0].Name = "Fx";
        document.Save.Sequences.Add(new SequenceSave { Name = "Mid", Tracks = { Link("Fx", "Fx", 0.5) } });
        var boss = new SequenceSave { Name = "Boss", Tracks = { Link("Mid", "Mid", 1) } };
        document.Save.Sequences.Add(boss);

        SequenceThumbnailScene.ChooseTime(boss, document, document.Save.Sequences).ShouldBe(1.5);
        SequenceThumbnailScene.LayersFor(boss, document, document.Save.Sequences).Count.ShouldBe(1);
    }

    [Fact]
    public void LayersFor_BrokenReferences_ShowOnlyTheOwnTracks()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir, Track("Body", ("body/body.achj", "Walk", 0, null)));
        var attack = document.Save.Sequences[0];
        attack.Tracks.Add(Link("Me", "Attack"));
        attack.Tracks.Add(Link("Gone", "Missing"));

        SequenceThumbnailScene.LayersFor(attack, document, document.Save.Sequences)
            .Select(l => Path.GetFileName(l.TexturePath)).ShouldBe(new[] { "body.png" });
        SequenceThumbnailScene.ChainReferencesOf(attack, document.Save.Sequences).Select(r => r.Chain).ShouldBe(new[] { "Walk" });
    }

    [Fact]
    public void LayersFor_TwoTracks_AreBackToFront_TopLaneLast()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir, Track("Fx", ("fx/fx.achj", "Spark", 0, null)), Track("Body", ("body/body.achj", "Walk", 0, null)));

        var layers = SequenceThumbnailScene.LayersFor(document.Save.Sequences[0], document);

        layers.Select(l => Path.GetFileName(l.TexturePath)).ShouldBe(new[] { "body.png", "spark.png" });
    }

    [Fact]
    public void Fit_OneSprite_FitsLikeASingleFrame_OffsetIgnored()
    {
        var layout = ThumbnailLayoutCalculator.Fit([new ThumbnailSpriteRect(32, 16, 40, -9)], 28, 28)!;

        layout.Width.ShouldBe(28);
        layout.Height.ShouldBe(14);
        layout.Placements.Single().ShouldBe(new ThumbnailPlacement(0, 0, 28, 14));
    }

    [Fact]
    public void Fit_TwoSprites_CentersTheUnionOfTheirBounds()
    {
        // A 16x16 body at the origin and a 16x16 effect 16 px to its right and 16 px up: the union is 32x32.
        var layout = ThumbnailLayoutCalculator.Fit(
            [new ThumbnailSpriteRect(16, 16, 0, 0), new ThumbnailSpriteRect(16, 16, 16, 16)], 28, 28)!;

        layout.Width.ShouldBe(28);
        layout.Height.ShouldBe(28);
        layout.Scale.ShouldBe(28f / 32f, 1e-4);
        // Y is up in the offsets and down in the thumbnail: the effect lands top-right, the body bottom-left.
        layout.Placements[0].X.ShouldBe(0, 1e-3);
        layout.Placements[0].Y.ShouldBe(16 * layout.Scale, 1e-3);
        layout.Placements[1].X.ShouldBe(16 * layout.Scale, 1e-3);
        layout.Placements[1].Y.ShouldBe(0, 1e-3);
    }

    [Fact]
    public void Fit_NoSprites_OrAZeroSizeSprite_IsNull()
    {
        ThumbnailLayoutCalculator.Fit([], 28, 28).ShouldBeNull();
        ThumbnailLayoutCalculator.Fit([new ThumbnailSpriteRect(0, 8, 0, 0)], 28, 28).ShouldBeNull();
    }

    [Theory]
    [InlineData("body/body.achj", "body.png", "body/body.png")]
    [InlineData("body.achj", "body.png", "body.png")]
    [InlineData("../shared/body.achj", "tex\\body.png", "../shared/tex/body.png")]
    [InlineData("a/b/x.achj", "../../c.png", "c.png")]
    [InlineData("a/x.achj", "../../c.png", "../c.png")]
    public void ResolveFrom_CollapsesDotSegments(string baseFile, string relative, string expected) =>
        RelativeFilePaths.ResolveFrom(baseFile, relative).ShouldBe(expected);

    [Fact]
    public void CacheKey_ChangesWhenAnyInputChanges_AndKeepsTheIdentityPrefix()
    {
        var baseline = new[]
        {
            SequenceThumbnailCacheKey.FileInput("seqj", "a/boss.seqj", 100, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            SequenceThumbnailCacheKey.FileInput("src", "body/body.achj", 200, new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero)),
            SequenceThumbnailCacheKey.FileInput("png", "body/body.png", 300, new DateTimeOffset(2026, 1, 3, 0, 0, 0, TimeSpan.Zero)),
        };
        string name = SequenceThumbnailCacheKey.BuildFileName("a/boss.seqj", baseline);

        SequenceThumbnailCacheKey.BuildFileName("a/boss.seqj", baseline.Reverse()).ShouldBe(name);
        for (int i = 0; i < baseline.Length; i++)
        {
            var changed = (string[])baseline.Clone();
            changed[i] = changed[i] + "x";
            var other = SequenceThumbnailCacheKey.BuildFileName("a/boss.seqj", changed);
            other.ShouldNotBe(name);
            other.Split('_')[0].ShouldBe(name.Split('_')[0]);
        }
        SequenceThumbnailCacheKey.BuildFileName("a/other.seqj", baseline).Split('_')[0].ShouldNotBe(name.Split('_')[0]);
    }
}

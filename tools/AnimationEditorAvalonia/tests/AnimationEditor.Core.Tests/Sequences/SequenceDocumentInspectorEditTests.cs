using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.IO;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>The inspector's edits on a <see cref="SequenceDocument"/>: a track's offset, flip and z, a chain's start and loops.</summary>
public class SequenceDocumentInspectorEditTests
{
    private static SequenceDocument Load(TestHelpers.TempDir dir)
    {
        var save = new SequenceListSave { Sources = { "hero.achj" } };
        var sequence = new SequenceSave { Name = "Attack" };
        var track = new SequenceTrackSave { Name = "Body" };
        track.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achj", Chain = "Walk", Start = 0.5, Loops = 1 });
        sequence.Tracks.Add(track);
        save.Sequences.Add(sequence);
        string path = Path.Combine(dir.Path, "boss.seqj");
        save.Save(path);
        return SequenceDocument.Load(path);
    }

    private static SequenceTrackSave SavedTrack(SequenceDocument document) =>
        SequenceListSave.FromFile(document.Path.FullPath).Sequences[0].Tracks[0];

    [Fact]
    public void TrackEdits_OffsetFlipAndZ_EachIsOneUndoStepAndSaves()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);
        var undo = new UndoManager();

        document.SetTrackOffset(undo, 0, 0, 4.5f, -2f);
        document.SetTrackFlip(undo, 0, 0, true);
        document.SetTrackZ(undo, 0, 0, 3);

        var saved = SavedTrack(document);
        (saved.X, saved.Y, saved.FlipHorizontal, saved.Z).ShouldBe((4.5f, -2f, true, 3));
        undo.UndoHistory.Select(c => c.Description).ShouldBe(new[]
        {
            "Set Offset of 'Body' to (4.5, -2)", "Turn Flip On for 'Body'", "Set Z of 'Body' to 3",
        });
        undo.Undo();
        SavedTrack(document).Z.ShouldBe(0);
        undo.Undo();
        SavedTrack(document).FlipHorizontal.ShouldBeFalse();
    }

    [Fact]
    public void TrackEdits_SettingTheCurrentValue_RecordsNothing()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);
        var undo = new UndoManager();

        document.SetTrackOffset(undo, 0, 0, 0, 0);
        document.SetTrackFlip(undo, 0, 0, false);
        document.SetTrackZ(undo, 0, 0, 0);

        undo.UndoHistory.ShouldBeEmpty();
    }

    [Fact]
    public void LocateDefinition_KnownChain_ReturnsTheSourcesAbsolutePath()
    {
        using var dir = new TestHelpers.TempDir();
        WriteHero(dir);
        var document = Load(dir);

        var (path, error) = document.LocateDefinition(new SequenceChainReferenceSave { Source = "hero.achj", Chain = "Walk" });

        error.ShouldBeNull();
        path.ShouldBe(new AnimationEditor.Core.Paths.FilePath(Path.Combine(dir.Path, "hero.achj")).FullPath);
    }

    [Fact]
    public void LocateDefinition_MissingSourceOrChain_ReturnsAnErrorAndNoPath()
    {
        using var dir = new TestHelpers.TempDir();
        WriteHero(dir);
        var document = Load(dir);

        var missingChain = document.LocateDefinition(new SequenceChainReferenceSave { Source = "hero.achj", Chain = "Gone" });
        var missingSource = document.LocateDefinition(new SequenceChainReferenceSave { Source = "nowhere.achj", Chain = "Walk" });

        missingChain.ShouldBe((null, "hero.achj has no chain named \"Gone\"."));
        missingSource.ShouldBe((null, "nowhere.achj is missing or could not be read."));
    }

    [Fact]
    public void LocateDefinition_WithNoChain_ReturnsTheSourcesPath_AndStillRefusesAMissingSource()
    {
        using var dir = new TestHelpers.TempDir();
        WriteHero(dir);
        var document = Load(dir);

        var whole = document.LocateDefinition(new SequenceChainReferenceSave { Source = "hero.achj" });
        var missing = document.LocateDefinition(new SequenceChainReferenceSave { Source = "nowhere.achj" });

        whole.ShouldBe((new AnimationEditor.Core.Paths.FilePath(Path.Combine(dir.Path, "hero.achj")).FullPath, null));
        missing.ShouldBe((null, "nowhere.achj is missing or could not be read."));
    }

    private static void WriteHero(TestHelpers.TempDir dir)
    {
        var list = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        list.AnimationChains.Add(new AnimationChainSave { Name = "Walk", Frames = { new AnimationFrameSave { FrameLength = 0.25f } } });
        list.SaveJson(Path.Combine(dir.Path, "hero.achj"));
    }

    [Fact]
    public void SetChainStart_ValidStart_IsOneUndoStep()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);
        var undo = new UndoManager();

        document.SetChainStart(undo, 0, 0, 0, 1.25).ShouldBeNull();

        SavedTrack(document).Chains.Single().Start.ShouldBe(1.25);
        undo.UndoHistory.Select(c => c.Description).ShouldBe(new[] { "Set Start of 'Walk' to 1.25" });
        undo.Undo();
        SavedTrack(document).Chains.Single().Start.ShouldBe(0.5);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void SetChainStart_NegativeOrNotFinite_IsRefusedWithNoUndoEntry(double start)
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);
        var undo = new UndoManager();

        document.SetChainStart(undo, 0, 0, 0, start).ShouldBe("Start must be 0 or more.");

        SavedTrack(document).Chains.Single().Start.ShouldBe(0.5);
        undo.UndoHistory.ShouldBeEmpty();
    }

    [Fact]
    public void SetChainLoops_FractionalLoops_AreKeptAsOneUndoStep()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);
        var undo = new UndoManager();

        document.SetChainLoops(undo, 0, 0, 0, 2.5f).ShouldBeNull();

        SavedTrack(document).Chains.Single().Loops.ShouldBe(2.5f);
        undo.UndoHistory.Select(c => c.Description).ShouldBe(new[] { "Set Loops of 'Walk' to 2.5" });
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void SetChainLoops_ZeroNegativeOrNotFinite_IsRefusedWithNoUndoEntry(float loops)
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);
        var undo = new UndoManager();

        document.SetChainLoops(undo, 0, 0, 0, loops).ShouldBe("Loops must be above 0.");

        SavedTrack(document).Chains.Single().Loops.ShouldBe(1f);
        undo.UndoHistory.ShouldBeEmpty();
    }
}

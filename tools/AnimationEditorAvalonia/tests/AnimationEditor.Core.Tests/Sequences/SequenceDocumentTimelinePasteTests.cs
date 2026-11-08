using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>Copying chains and tracks out of a sequence and pasting them into the same file or another.</summary>
public class SequenceDocumentTimelinePasteTests
{
    private static SequenceChainReferenceSave Chain(string name, double start, string source = "hero.achx", float loops = 1) =>
        new() { Source = source, Chain = name, Start = start, Loops = loops };

    private static SequenceTrackSave Track(string name, params SequenceChainReferenceSave[] chains)
    {
        var track = new SequenceTrackSave { Name = name };
        track.Chains.AddRange(chains);
        return track;
    }

    private static SequenceDocument Open(string folder, string[] sources, params SequenceTrackSave[] tracks)
    {
        Directory.CreateDirectory(folder);
        var sequence = new SequenceSave { Name = "Idle" };
        sequence.Tracks.AddRange(tracks);
        var save = new SequenceListSave();
        save.Sources.AddRange(sources);
        save.Sequences.Add(sequence);
        string path = Path.Combine(folder, "boss.seqj");
        save.Save(path);
        return SequenceDocument.Load(path);
    }

    private static TimelineClipboardContent Read(string text)
    {
        TimelineClipboard.TryRead(text, out var content, out _).ShouldBeTrue();
        return content!;
    }

    private static string[] Names(SequenceDocument document) => document.Save.Sequences[0].Tracks.Select(t => t.Name).ToArray();

    private static (string Chain, double Start)[] Chains(SequenceDocument document, int track) =>
        document.Save.Sequences[0].Tracks[track].Chains.Select(c => (c.Chain, c.Start)).ToArray();

    [Fact]
    public void CopyChains_WritesTheirAbsoluteSources_AndTheNameOfTheFirstChainsTrack()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir.Path, new[] { "hero.achx" }, Track("Body", Chain("Walk", 1), Chain("Run", 2)), Track("Head"));

        var content = Read(document.CopyChains(0, new[] { new TimelineChainId(0, 1), new TimelineChainId(0, 0) }));

        content.TrackName.ShouldBe("Body");
        content.Chains.Select(c => (c.Chain, c.Start)).ShouldBe(new[] { ("Walk", 0.0), ("Run", 1.0) }, "document order, the earliest copied chain at 0");
        content.Sources.Single().ShouldEndWith("/hero.achx");
        Path.IsPathRooted(content.Sources.Single()).ShouldBeTrue();
    }

    [Fact]
    public void PasteChains_OntoTheSelectedTrack_PutsTheEarliestAtTheTime_AndKeepsTheOthersRelative_AsOneUndoEntry()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir.Path, new[] { "hero.achx" }, Track("Body", Chain("Walk", 1), Chain("Run", 3)), Track("Head", Chain("Idle", 0)));
        var undo = new UndoManager();
        var content = Read(document.CopyChains(0, new[] { new TimelineChainId(0, 0), new TimelineChainId(0, 1) }));

        var result = document.PasteChains(undo, 0, content, selectedTrack: 1, time: 10);

        Chains(document, 1).ShouldBe(new[] { ("Idle", 0.0), ("Walk", 10.0), ("Run", 12.0) });
        result.Chains.ShouldBe(new[] { new TimelineChainId(1, 1), new TimelineChainId(1, 2) });
        result.Errors.ShouldBeEmpty();
        undo.UndoHistory.Select(c => c.Description).ShouldBe(new[] { "Paste 2 chains" });
        undo.Undo();
        Chains(document, 1).ShouldBe(new[] { ("Idle", 0.0) });
        undo.Redo();
        Chains(document, 1).Length.ShouldBe(3);
    }

    [Fact]
    public void PasteChains_WithNoSelectedTrack_GoesOntoTheTrackOfTheFirstCopiedChain()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir.Path, new[] { "hero.achx" }, Track("Body", Chain("Walk", 1)), Track("Head"));
        var content = Read(document.CopyChains(0, new[] { new TimelineChainId(0, 0) }));

        var result = document.PasteChains(new UndoManager(), 0, content, selectedTrack: null, time: 4);

        Chains(document, 0).ShouldBe(new[] { ("Walk", 1.0), ("Walk", 4.0) });
        result.Chains.ShouldBe(new[] { new TimelineChainId(0, 1) });
    }

    [Fact]
    public void PasteChains_WhenThatTrackIsMissing_MakesANewTrackWithAFreeName()
    {
        using var dir = new TestHelpers.TempDir();
        var source = Open(Path.Combine(dir.Path, "a"), new[] { "hero.achx" }, Track("Body", Chain("Walk", 1)));
        var destination = Open(Path.Combine(dir.Path, "b"), new string[0], Track("Head"));
        var content = Read(source.CopyChains(0, new[] { new TimelineChainId(0, 0) }));

        var result = destination.PasteChains(new UndoManager(), 0, content, selectedTrack: null, time: 2);

        Names(destination).ShouldBe(new[] { "Head", "Body" });
        Chains(destination, 1).ShouldBe(new[] { ("Walk", 2.0) });
        result.Chains.ShouldBe(new[] { new TimelineChainId(1, 0) });
    }

    [Fact]
    public void PasteChains_IntoAnotherFile_AddsTheSourceOnce_RelativeToIt_InTheSameUndoEntry()
    {
        using var dir = new TestHelpers.TempDir();
        var source = Open(Path.Combine(dir.Path, "a"), new[] { "hero.achx" }, Track("Body", Chain("Walk", 0), Chain("Run", 1)));
        var destination = Open(Path.Combine(dir.Path, "b", "deep"), new string[0], Track("Body"));
        var undo = new UndoManager();
        var content = Read(source.CopyChains(0, new[] { new TimelineChainId(0, 0), new TimelineChainId(0, 1) }));

        destination.PasteChains(undo, 0, content, selectedTrack: 0, time: 0);

        destination.Save.Sources.ShouldBe(new[] { "../../a/hero.achx" });
        destination.Save.Sequences[0].Tracks[0].Chains.Select(c => c.Source).ShouldBe(new[] { "../../a/hero.achx", "../../a/hero.achx" });
        undo.UndoHistory.Count.ShouldBe(1);
        undo.Undo();
        destination.Save.Sources.ShouldBeEmpty();
        destination.Save.Sequences[0].Tracks[0].Chains.ShouldBeEmpty();
    }

    [Fact]
    public void PasteChains_SourceAlreadyListedInOtherCase_IsNotAddedAgain()
    {
        using var dir = new TestHelpers.TempDir();
        var source = Open(Path.Combine(dir.Path, "a"), new[] { "hero.achx" }, Track("Body", Chain("Walk", 0)));
        var destination = Open(Path.Combine(dir.Path, "b"), new[] { "../A/HERO.achx" }, Track("Body"));
        var content = Read(source.CopyChains(0, new[] { new TimelineChainId(0, 0) }));

        destination.PasteChains(new UndoManager(), 0, content, 0, 0);

        destination.Save.Sources.ShouldBe(new[] { "../A/HERO.achx" });
        destination.Save.Sequences[0].Tracks[0].Chains[0].Source.ShouldBe("../A/HERO.achx");
    }

    [Fact]
    public void PasteTracks_GoRightAfterTheSelectedTrack_WithFreeNames_KeepingEverythingTheyHad_AsOneUndoEntry()
    {
        using var dir = new TestHelpers.TempDir();
        var body = Track("Body", Chain("Walk", 0.5, loops: 2), Chain("Run", 2));
        body.X = 3; body.Y = -1; body.FlipHorizontal = true; body.Visible = false;
        body.UnknownProperties["tag"] = "boss";
        var document = Open(dir.Path, new[] { "hero.achx" }, body, Track("Head"), Track("Tail"));
        var undo = new UndoManager();
        var content = Read(document.CopyTracks(0, new[] { 0, 1 }));

        var result = document.PasteTracks(undo, 0, content, afterTrack: 1);

        Names(document).ShouldBe(new[] { "Body", "Head", "Body 2", "Head 2", "Tail" });
        var copy = document.Save.Sequences[0].Tracks[2];
        (copy.X, copy.Y, copy.FlipHorizontal, copy.Visible).ShouldBe((3f, -1f, true, false));
        copy.UnknownProperties["tag"]!.GetValue<string>().ShouldBe("boss");
        copy.Chains.Select(c => (c.Chain, c.Start, c.Loops, c.Source)).ShouldBe(new[] { ("Walk", 0.5, 2f, "hero.achx"), ("Run", 2.0, 1f, "hero.achx") });
        result.Tracks.ShouldBe(new[] { 2, 3 });
        undo.UndoHistory.Select(c => c.Description).ShouldBe(new[] { "Paste 2 tracks" });
        undo.Undo();
        Names(document).ShouldBe(new[] { "Body", "Head", "Tail" });
    }

    [Fact]
    public void PasteTracks_WithNoSelectedTrack_GoAtTheEnd()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir.Path, new string[0], Track("Body"), Track("Head"));
        var content = Read(document.CopyTracks(0, new[] { 0 }));

        var result = document.PasteTracks(new UndoManager(), 0, content, afterTrack: null);

        Names(document).ShouldBe(new[] { "Body", "Head", "Body 2" });
        result.Tracks.ShouldBe(new[] { 2 });
    }

    [Fact]
    public void PasteTracks_IntoAnotherFile_MergesTheSources_AndKeepsTheNameWhenFree()
    {
        using var dir = new TestHelpers.TempDir();
        var source = Open(Path.Combine(dir.Path, "a"), new[] { "hero.achx", "fx.achx" },
            Track("Body", Chain("Walk", 0), Chain("Boom", 1, "fx.achx")));
        var destination = Open(Path.Combine(dir.Path, "b"), new string[0], Track("Other"));
        var undo = new UndoManager();
        var content = Read(source.CopyTracks(0, new[] { 0 }));

        destination.PasteTracks(undo, 0, content, afterTrack: 0);

        Names(destination).ShouldBe(new[] { "Other", "Body" });
        destination.Save.Sources.ShouldBe(new[] { "../a/hero.achx", "../a/fx.achx" });
        undo.UndoHistory.Count.ShouldBe(1);
        undo.Undo();
        destination.Save.Sources.ShouldBeEmpty();
    }
}

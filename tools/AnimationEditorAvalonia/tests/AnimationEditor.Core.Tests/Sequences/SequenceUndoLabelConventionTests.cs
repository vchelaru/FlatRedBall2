using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Sequences;
using AnimationEditor.Core.Utilities;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>
/// Every undo/History label a sequence edit produces follows the ANIMATIONS tree's wording: a Title Case verb, then one
/// quoted name or a count of Title Case nouns ("Delete 3 Tracks"), single quotes only, no trailing punctuation.
/// A new label that breaks it fails here.
/// </summary>
public class SequenceUndoLabelConventionTests
{
    private static SequenceChainReferenceSave Chain(string name, double start) =>
        new() { Source = "hero.achx", Chain = name, Start = start, Loops = 1 };

    private static SequenceTrackSave Track(string name, params SequenceChainReferenceSave[] chains)
    {
        var track = new SequenceTrackSave { Name = name };
        track.Chains.AddRange(chains);
        return track;
    }

    private static int _case;

    // A new folder per document: rewriting one file over and over can be refused on Windows (a scanner holds the last copy).
    private static SequenceDocument Open(string parent)
    {
        string folder = Path.Combine(parent, $"case{System.Threading.Interlocked.Increment(ref _case)}");
        Directory.CreateDirectory(folder);
        var list = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        foreach (string chain in new[] { "Walk", "Run", "Jump" }) list.AnimationChains.Add(new AnimationChainSave { Name = chain });
        list.Save(Path.Combine(folder, "hero.achx"));
        var save = new SequenceListSave();
        save.Sources.Add("hero.achx");
        foreach (string name in new[] { "Idle", "Attack", "Hurt" })
        {
            var sequence = new SequenceSave { Name = name };
            sequence.Tracks.Add(Track("Body", Chain("Walk", 0), Chain("Run", 1), Chain("Gone", 2), Chain("Old", 3)));
            sequence.Tracks.Add(Track("Head", Chain("Walk", 0)));
            sequence.Tracks.Add(Track("Tail", Chain("Run", 0)));
            save.Sequences.Add(sequence);
        }
        string path = Path.Combine(folder, "boss.seqj");
        save.Save(path);
        return SequenceDocument.Load(path);
    }

    private static TimelineClipboardContent Read(string text)
    {
        TimelineClipboard.TryRead(text, out var content, out _).ShouldBeTrue();
        return content!;
    }

    /// <summary>The label of each edit, every one run on a fresh document, for single and multi-item forms of every command.</summary>
    private static List<string> ProducedLabels(string folder)
    {
        var labels = new List<string>();


        void Run(System.Action<SequenceDocument, UndoManager> edit)
        {
            var document = Open(folder);
            var undo = new UndoManager();
            edit(document, undo);
            labels.AddRange(undo.UndoHistory.Select(c => c.Description));
        }

        var chains3 = new[] { new TimelineChainId(0, 0), new TimelineChainId(0, 1), new TimelineChainId(1, 0) };
        var chains2 = new[] { new TimelineChainId(0, 0), new TimelineChainId(0, 1) };
        var documentForClipboard = Open(folder);
        var copied = Read(documentForClipboard.CopyChains(0, chains3));
        var copiedOne = Read(documentForClipboard.CopyChains(0, new[] { chains3[0] }));
        var copiedTracks = Read(documentForClipboard.CopyTracks(0, new[] { 0, 1 }));
        var copiedTrack = Read(documentForClipboard.CopyTracks(0, new[] { 1 }));

        Run((document, undo) => document.DeleteChains(undo, 0, new[] { new TimelineChainId(0, 0) }));
        Run((document, undo) => document.DeleteChains(undo, 0, new[] { new TimelineChainId(2, 0), new TimelineChainId(1, 0) }));
        Run((document, undo) => document.DeleteTracks(undo, 0, new[] { 2 }));
        Run((document, undo) => document.AddTrack(undo, 0));
        Run((document, undo) => document.DeleteTracks(undo, 0, new[] { 0, 1 }));
        Run((document, undo) => document.PasteChains(undo, 0, copied, selectedTrack: null, time: 5));
        Run((document, undo) => document.PasteChains(undo, 0, copiedOne, selectedTrack: 0, time: 5));
        Run((document, undo) => document.PasteTracks(undo, 0, copiedTracks, selectedTrack: null));
        Run((document, undo) => document.PasteTracks(undo, 0, copiedTrack, selectedTrack: null));
        Run((document, undo) => document.DuplicateChains(undo, 0, chains2));
        Run((document, undo) => document.DuplicateChains(undo, 0, new[] { chains2[0] }));
        Run((document, undo) => document.DuplicateTracks(undo, 0, new[] { 0, 1 }));
        Run((document, undo) => document.DuplicateTracks(undo, 0, new[] { 0 }));
        Run((document, undo) => document.SetTracksVisible(undo, 0, new[] { 0, 1, 2 }, false));
        Run((document, undo) => document.SetTracksVisible(undo, 0, new[] { 0 }, true));
        Run((document, undo) => document.SetTrackVisible(undo, 0, 1, false));
        Run((document, undo) => document.ShowAllTracks(undo, 0));
        Run((document, undo) => document.SetTracksFlip(undo, 0, new[] { 0, 1 }, true));
        Run((document, undo) => document.SetTracksFlip(undo, 0, new[] { 0 }, false));
        Run((document, undo) => document.SetTrackFlip(undo, 0, 1, true));
        Run((document, undo) => document.MoveTrack(undo, 0, 0, 2));
        NumericEdit.TryParse("3", out var three).ShouldBeTrue();
        NumericEdit.TryParse("+ 1", out var plusOne).ShouldBeTrue();
        Run((document, undo) => document.SetChainsStart(undo, 0, chains3, three));
        Run((document, undo) => document.SetChainsStart(undo, 0, chains3, plusOne));
        Run((document, undo) => document.SetChainsLoops(undo, 0, chains2, three));
        Run((document, undo) => document.SetChainStart(undo, 0, 0, 0, 2));
        Run((document, undo) => document.SetChainLoops(undo, 0, 0, 0, 2));
        Run((document, undo) => document.PlaceChain(undo, 0, track: 0, "hero.achx", "Jump", 0));
        Run((document, undo) => document.PlaceChain(undo, 0, track: null, "hero.achx", "Jump", 0));
        Run((document, undo) => document.RenameTrack(undo, 0, 0, "Torso"));
        Run((document, undo) => document.RelinkChains(undo, 0, new[] { new TimelineChainId(0, 0) }, "Run"));
        Run((document, undo) => document.RelinkChains(undo, 0, new[] { new TimelineChainId(0, 1), new TimelineChainId(1, 0) }, "Jump"));
        Run((document, undo) => document.AddSequence(undo));
        Run((document, undo) => document.RenameSequence(undo, 0, "Stand"));
        Run((document, undo) => document.SetLoop(undo, 0, true));
        Run((document, undo) => document.DuplicateSequences(undo, new[] { 0 }));
        Run((document, undo) => document.DuplicateSequences(undo, new[] { 0, 1 }));
        Run((document, undo) => document.MoveSequences(undo, new[] { 0 }, 3));
        Run((document, undo) => document.MoveSequences(undo, new[] { 0, 1 }, 4));
        Run((document, undo) => document.DeleteSequences(undo, new[] { 0 }));
        Run((document, undo) => document.DeleteSequences(undo, new[] { 0, 1 }));

        // The timeline's drag gestures build their label from these.
        labels.Add(SequenceLabel.MoveChains(1, "Walk", null));
        labels.Add(SequenceLabel.MoveChains(1, "Walk", "Head"));
        labels.Add(SequenceLabel.MoveChains(3, "Walk", null));
        labels.Add(SequenceLabel.MoveChainsToNewTracks(1, "Walk", new[] { "Walk" }));
        labels.Add(SequenceLabel.MoveChainsToNewTracks(2, "Walk", new[] { "Walk" }));
        labels.Add(SequenceLabel.MoveChainsToNewTracks(2, "Walk", new[] { "Walk", "Run" }));
        return labels;
    }

    [Fact]
    public void EveryProducedLabel_FollowsTheTitleCaseCountedConvention()
    {
        using var dir = new TestHelpers.TempDir();

        var labels = ProducedLabels(dir.Path);

        labels.Count.ShouldBeGreaterThan(40);
        foreach (string label in labels)
        {
            Regex.IsMatch(label, @"^[A-Z][a-z]+ ").ShouldBeTrue($"'{label}' starts with a Title Case verb");
            Regex.IsMatch(label, @"[.,;:!?…]$").ShouldBeFalse($"'{label}' has trailing punctuation");
            label.ShouldNotContain("\"", customMessage: $"'{label}' uses double quotes");
            Regex.IsMatch(label, @"\b\d+ [a-z]").ShouldBeFalse($"'{label}' has a lowercase counted noun (want '3 Tracks')");
            Regex.IsMatch(label, @"\b1 [A-Z][A-Za-z ]*s\b").ShouldBeFalse($"'{label}' pluralises a count of 1");
        }
    }

    [Fact]
    public void CountedLabels_UseTitleCasePluralNouns()
    {
        SequenceLabel.Counted("Delete", 3, "Track").ShouldBe("Delete 3 Tracks");
        SequenceLabel.Counted("Paste", 2, "Chain").ShouldBe("Paste 2 Chains");
        SequenceLabel.OneOrMany("Delete", "Sequence", new[] { "Idle" }).ShouldBe("Delete Sequence 'Idle'");
        SequenceLabel.OneOrMany("Delete", "Sequence", new[] { "Idle", "Hurt" }).ShouldBe("Delete 2 Sequences");
        SequenceLabel.MoveChainsToNewTracks(2, "Walk", new[] { "A", "B" }).ShouldBe("Move 2 Chains to 2 New Tracks");
    }
}

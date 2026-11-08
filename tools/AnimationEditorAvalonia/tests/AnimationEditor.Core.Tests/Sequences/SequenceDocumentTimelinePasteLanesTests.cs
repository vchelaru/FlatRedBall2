using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.IO;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>
/// Which tracks pasted chains land on. With no track selected each chain goes back to the track it came from, matched by
/// name (a missing one is made with the copied track's settings). With a track selected, the first copied chain's track
/// (the topmost lane copied from) maps to it and the other copied tracks keep their lane distance from it.
/// </summary>
public class SequenceDocumentTimelinePasteLanesTests
{
    private static SequenceChainReferenceSave Chain(string name, double start) =>
        new() { Source = "hero.achx", Chain = name, Start = start };

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

    private static TimelineChainId[] All(params (int Track, int Chain)[] ids) => ids.Select(i => new TimelineChainId(i.Track, i.Chain)).ToArray();

    // Ship lane 0 (Walk 1, Run 2), Booster lane 1 (Glow 1.5), Spare lane 2 (Idle 3).
    private static SequenceDocument ThreeLanes(string folder) =>
        Open(folder, new[] { "hero.achx" }, Track("Ship", Chain("Walk", 1), Chain("Run", 2)), Track("Booster", Chain("Glow", 1.5)), Track("Spare", Chain("Idle", 3)));

    [Fact]
    public void WithNoTrackSelected_EachChainGoesBackOnItsOwnTrack_ChainsOfOneTrackSharingIt()
    {
        using var dir = new TestHelpers.TempDir();
        var document = ThreeLanes(dir.Path);
        var content = Read(document.CopyChains(0, All((0, 0), (1, 0), (2, 0), (0, 1))));
        var undo = new UndoManager();

        var result = document.PasteChains(undo, 0, content, selectedTrack: null, time: 10);

        // Walk (1) is the earliest copied chain, so it lands at 10 and the rest keep their distance from it.
        Chains(document, 0).ShouldBe(new[] { ("Walk", 1.0), ("Run", 2.0), ("Walk", 10.0), ("Run", 11.0) });
        Chains(document, 1).ShouldBe(new[] { ("Glow", 1.5), ("Glow", 10.5) });
        Chains(document, 2).ShouldBe(new[] { ("Idle", 3.0), ("Idle", 12.0) });
        result.Chains.ShouldBe(All((0, 2), (0, 3), (1, 1), (2, 1)), ignoreOrder: true);
        undo.UndoHistory.Count.ShouldBe(1);
    }

    [Fact]
    public void WithATrackSelected_TheFirstCopiedChainsTrackBecomesIt_AndTheOthersKeepTheirLaneOffsets()
    {
        using var dir = new TestHelpers.TempDir();
        var source = ThreeLanes(dir.Path);
        var destination = Open(Path.Combine(dir.Path, "b"), new string[0], Track("A"), Track("B"), Track("C"), Track("D"));
        // Ship (lane 0) and Spare (lane 2): two lanes apart.
        var content = Read(source.CopyChains(0, All((0, 0), (2, 0))));

        var result = destination.PasteChains(new UndoManager(), 0, content, selectedTrack: 1, time: 0);

        Chains(destination, 1).ShouldBe(new[] { ("Walk", 0.0) });
        Chains(destination, 3).ShouldBe(new[] { ("Idle", 2.0) });
        Chains(destination, 2).ShouldBeEmpty();
        result.Chains.ShouldBe(All((1, 0), (3, 0)), ignoreOrder: true);
    }

    [Fact]
    public void WithATrackSelected_AndEverythingFromOneTrack_AllLandsOnIt()
    {
        using var dir = new TestHelpers.TempDir();
        var document = ThreeLanes(dir.Path);
        var content = Read(document.CopyChains(0, All((0, 0), (0, 1))));

        document.PasteChains(new UndoManager(), 0, content, selectedTrack: 2, time: 5);

        Chains(document, 2).ShouldBe(new[] { ("Idle", 3.0), ("Walk", 5.0), ("Run", 6.0) });
    }

    [Fact]
    public void WithATrackSelected_AndTooFewLanes_MakesTheMissingTracksAtTheEnd_WithTheirCopiedSettings()
    {
        using var dir = new TestHelpers.TempDir();
        var booster = Track("Booster", Chain("Glow", 2));
        booster.FlipHorizontal = true; booster.Visible = false;
        var source = Open(Path.Combine(dir.Path, "a"), new[] { "hero.achx" }, Track("Ship", Chain("Walk", 1)), booster);
        var destination = Open(Path.Combine(dir.Path, "b"), new string[0], Track("A"), Track("B"));
        var content = Read(source.CopyChains(0, All((0, 0), (1, 0))));
        var undo = new UndoManager();

        var result = destination.PasteChains(undo, 0, content, selectedTrack: 1, time: 0);

        // Ship goes onto the selected lane 1; Booster would be lane 2, which does not exist, so it is made at the end.
        Names(destination).ShouldBe(new[] { "A", "B", "Booster" });
        var made = destination.Save.Sequences[0].Tracks[2];
        (made.FlipHorizontal, made.Visible).ShouldBe((true, false));
        Chains(destination, 1).ShouldBe(new[] { ("Walk", 0.0) });
        Chains(destination, 2).ShouldBe(new[] { ("Glow", 1.0) });
        result.Chains.ShouldBe(All((1, 0), (2, 0)), ignoreOrder: true);
        undo.UndoHistory.Count.ShouldBe(1);
        undo.Undo();
        Names(destination).ShouldBe(new[] { "A", "B" });
        destination.Save.Sources.ShouldBeEmpty();
    }

    [Fact]
    public void WithNoTrackSelected_IntoAnotherFile_MatchesTracksByName_AndMakesTheRestWithTheirSettings()
    {
        using var dir = new TestHelpers.TempDir();
        var booster = Track("Booster", Chain("Glow", 2));
        booster.FlipHorizontal = true; booster.Visible = false;
        var source = Open(Path.Combine(dir.Path, "a"), new[] { "hero.achx" }, Track("Ship", Chain("Walk", 1)), booster);
        var destination = Open(Path.Combine(dir.Path, "b"), new string[0], Track("Other"), Track("Ship"));
        var content = Read(source.CopyChains(0, All((0, 0), (1, 0))));
        var undo = new UndoManager();

        destination.PasteChains(undo, 0, content, selectedTrack: null, time: 3);

        Names(destination).ShouldBe(new[] { "Booster", "Other", "Ship" }, "a track the paste makes lands on top, in front");
        Chains(destination, 2).ShouldBe(new[] { ("Walk", 3.0) });
        Chains(destination, 0).ShouldBe(new[] { ("Glow", 4.0) });
        var made = destination.Save.Sequences[0].Tracks[0];
        (made.FlipHorizontal, made.Visible).ShouldBe((true, false));
        destination.Save.Sources.ShouldBe(new[] { "../a/hero.achx" });
        undo.UndoHistory.Count.ShouldBe(1);
        undo.Undo();
        Names(destination).ShouldBe(new[] { "Other", "Ship" });
        destination.Save.Sources.ShouldBeEmpty();
    }

    [Fact]
    public void FromAnEnvelopeWithoutTrackInfo_FallsBackToTheSingleTrackRule()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir.Path, new[] { "hero.achx" }, Track("Body"), Track("Head"));
        string folder = dir.Path.Replace("\\", "/");
        string legacy = "{ \"kind\": \"frb2-animation-editor-chains\", \"version\": 1, \"track\": \"Head\", \"chains\": ["
            + $"{{ \"source\": \"{folder}/hero.achx\", \"chain\": \"Walk\", \"start\": 0 }}, {{ \"source\": \"{folder}/hero.achx\", \"chain\": \"Run\", \"start\": 1 }} ] }}";

        document.PasteChains(new UndoManager(), 0, Read(legacy), selectedTrack: null, time: 2);
        Chains(document, 1).ShouldBe(new[] { ("Walk", 2.0), ("Run", 3.0) });

        document.PasteChains(new UndoManager(), 0, Read(legacy), selectedTrack: 0, time: 0);
        Chains(document, 0).ShouldBe(new[] { ("Walk", 0.0), ("Run", 1.0) });

        document.PasteChains(new UndoManager(), 0, Read(legacy.Replace("\"Head\"", "\"Gone\"")), selectedTrack: null, time: 0);
        Names(document).ShouldBe(new[] { "Gone", "Body", "Head" });
    }
}

using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>Duplicating a sequence whose clip plays a name that is gone.</summary>
public class SequenceDuplicateCycleTests
{
    [Fact]
    public void DuplicateSequences_CopyWouldTakeTheNameItsOwnClipIsLeftWith_IsMadeOrRefused()
    {
        using var dir = new TestHelpers.TempDir();
        string path = Path.Combine(dir.Path, "boss.seqj");
        File.WriteAllText(path, """
            { "version": 1, "sources": [], "sequences": [
              { "name": "Combo", "tracks": [ { "name": "R", "references": [ { "sequence": "Combo 2", "start": 0 } ] } ] },
              { "name": "Combo 2" } ] }
            """);
        var document = SequenceDocument.Load(path);
        var undo = new UndoManager();
        document.DeleteSequences(undo, new[] { 1 });

        var made = document.DuplicateSequences(undo, new[] { 0 }).Names;

        document.Save.Sequences.Select(s => s.Name).ShouldBe(new[] { "Combo" }.Concat(made));
    }

    // Cycles and chains on a reference track, the two edits the document drops: every caller that can make one says why.
    [Fact]
    public void DuplicateTracks_TrackPlaysItsOwnSequence_IsRefusedWithTheReason()
    {
        using var dir = new TestHelpers.TempDir();
        string path = Path.Combine(dir.Path, "boss.seqj");
        File.WriteAllText(path, """
            { "version": 1, "sources": [], "sequences": [
              { "name": "Combo", "tracks": [ { "name": "R", "references": [ { "sequence": "Combo", "start": 0 } ] } ] } ] }
            """);
        var document = SequenceDocument.Load(path);

        var result = document.DuplicateTracks(new UndoManager(), 0, new[] { 0 });

        (result.Tracks.Count, document.Save.Sequences[0].Tracks.Count).ShouldBe((0, 1));
        result.Errors.Single().ShouldContain("\"R\"");
    }

    [Fact]
    public void PlaceChain_OnATrackThatPlaysASequence_IsRefusedWithTheReason()
    {
        using var dir = new TestHelpers.TempDir();
        string path = Path.Combine(dir.Path, "boss.seqj");
        File.WriteAllText(path, """
            { "version": 1, "sources": ["hero.achx"], "sequences": [
              { "name": "Combo", "tracks": [ { "name": "R", "references": [ { "sequence": "Idle", "start": 0 } ] } ] },
              { "name": "Idle" } ] }
            """);
        var document = SequenceDocument.Load(path);

        string? refused = document.PlaceChain(new UndoManager(), 0, 0, "hero.achx", "Walk", 0);

        refused.ShouldNotBeNull().ShouldContain("plays a sequence");
        document.Save.Sequences[0].Tracks[0].Chains.ShouldBeEmpty();
    }
}

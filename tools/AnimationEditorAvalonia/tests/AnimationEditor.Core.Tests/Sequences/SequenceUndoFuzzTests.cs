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

/// <summary>Random runs of every sequence edit: undo, redo, Save As and the file on disk all agree.</summary>
public class SequenceUndoFuzzTests
{
    private static void WriteAchx(string path)
    {
        var list = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        foreach (string name in new[] { "Walk", "Run" })
        {
            var chain = new AnimationChainSave { Name = name };
            for (int i = 0; i < 3; i++) chain.Frames.Add(new AnimationFrameSave { TextureName = "a.png", FrameLength = 0.1f, RightCoordinate = 16, BottomCoordinate = 16 });
            list.AnimationChains.Add(chain);
        }
        list.Save(path);
    }

    private static void RandomEdit(SequenceDocument d, UndoManager undo, Random r)
    {
        int Seq() => r.Next(d.Save.Sequences.Count);
        int s = d.Save.Sequences.Count == 0 ? -1 : Seq();
        int tracks = s < 0 ? 0 : d.Save.Sequences[s].Tracks.Count;
        int t = tracks == 0 ? -1 : r.Next(tracks);
        var track = t < 0 ? null : d.Save.Sequences[s].Tracks[t];
        int c = track is { Chains.Count: > 0 } ? r.Next(track.Chains.Count) : -1;
        int clip = track?.References is { Count: > 0 } clips ? r.Next(clips.Count) : -1;
        double time = Math.Round(r.NextDouble() * 4, 3);
        int kind = r.Next(28);
        if (s < 0 || kind == 0) { d.AddSequence(undo); return; }
        switch (kind)
        {
            case 1: d.RenameSequence(undo, s, "N" + r.Next(6)); break;
            case 2: if (d.Save.Sequences.Count > 2) d.DeleteSequences(undo, new[] { s }); break;
            case 3: d.DuplicateSequences(undo, new[] { s, Seq() }); break;
            case 4: d.MoveSequences(undo, new[] { s }, r.Next(d.Save.Sequences.Count + 1)); break;
            case 5: d.SetLoop(undo, s, r.Next(2) == 0); break;
            case 6: d.SortSequences(undo); break;
            case 7: case 8: case 9: d.PlaceChain(undo, s, t >= 0 && track!.References is null ? t : null, "hero.achx", r.Next(2) == 0 ? "Walk" : "Run", time, r.Next(2) == 0 ? null : 0.7); break;
            case 10: d.AddSequenceReference(undo, s, d.Save.Sequences[Seq()].Name, time); break;
            case 11: if (t >= 0) d.AddReferenceClip(undo, s, t, d.Save.Sequences[Seq()].Name, time); break;
            case 12: if (clip >= 0) d.DeleteReferenceClip(undo, s, t, clip); break;
            case 13: if (clip >= 0) d.SetReferenceStart(undo, s, t, time, clip); break;
            case 14: if (clip >= 0) d.SetReferenceLength(undo, s, t, time, clip); break;
            case 15: if (clip >= 0) d.ClearReferenceLength(undo, s, t, clip); break;
            case 16: d.AddTrack(undo, s); break;
            case 17: if (c >= 0) d.DeleteChains(undo, s, new[] { new TimelineChainId(t, c) }); break;
            case 18: if (t >= 0) d.DeleteTracks(undo, s, new[] { t }); break;
            case 19: if (t >= 0) d.SetTracksVisible(undo, s, new[] { t, 0 }, r.Next(2) == 0); break;
            case 20: if (t >= 0) d.SetTracksContinuous(undo, s, new[] { t, 0 }, r.Next(2) == 0); break;
            case 21: if (c >= 0) d.SetChainStart(undo, s, t, c, time); break;
            case 22: if (c >= 0) d.SetChainLength(undo, s, t, c, time); break;
            case 23: if (c >= 0) d.ClearChainsLength(undo, s, new[] { new TimelineChainId(t, c) }); break;
            case 24: if (t >= 0) d.DuplicateTracks(undo, s, new[] { t }); break;
            case 25: if (c >= 0) d.DuplicateChains(undo, s, new[] { new TimelineChainId(t, c) }); break;
            case 26: if (t >= 0) d.MoveTrack(undo, s, t, r.Next(tracks + 1)); break;
            case 27:
                if (c < 0) break;
                string before = d.BeginGesture();
                d.Save.Sequences[s].Tracks[t].Chains[c].Start = time / 3;
                if (r.Next(2) == 0) d.CommitGesture(undo, "Move", before); else d.CancelGesture(before);
                break;
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void RandomEdits_UndoAllThenRedoAll_IsExact_AndTheFileMatchesMemory(int seed)
    {
        using var dir = new TestHelpers.TempDir();
        WriteAchx(Path.Combine(dir.Path, "hero.achx"));
        string path = Path.Combine(dir.Path, "boss.seqj");
        File.WriteAllText(path, """
            { "version": 3, "zoo": { "a": [1] }, "sources": ["hero.achx"], "sequences": [
              { "name": "A", "note": "keep", "tracks": [ { "name": "T", "tint": 1.50, "chains": [ { "source": "hero.achx", "chain": "Walk", "start": 0, "x": true } ] } ] },
              { "name": "B", "tracks": [ { "name": "R", "references": [ { "sequence": "A", "start": 0.5, "ease": "in" } ] } ] } ] }
            """);
        var document = SequenceDocument.Load(path);
        var undo = new UndoManager();
        var random = new Random(seed);
        string initial = document.Save.ToJsonString();

        for (int i = 0; i < 120; i++)
        {
            RandomEdit(document, undo, random);
            if (i > 0) SequenceListSave.FromFile(path).ToJsonString().ShouldBe(document.Save.ToJsonString(), $"after edit {i}");
        }
        string final = document.Save.ToJsonString();
        while (undo.CanUndo) undo.Undo();
        document.Save.ToJsonString().ShouldBe(initial);
        while (undo.CanRedo) undo.Redo();
        document.Save.ToJsonString().ShouldBe(final);

        document.SaveAs(Path.Combine(dir.Path, "deep", "er", "boss2.seqj")).ShouldBeNull();
        while (undo.CanUndo) undo.Undo();
        document.Save.Sources.ShouldBe(new[] { "../../hero.achx" });
        document.IsSourceMissing("../../hero.achx").ShouldBeFalse();
        document.Save.Sequences.SelectMany(s => s.Tracks).SelectMany(t => t.Chains).ShouldAllBe(chain => chain.Source == "../../hero.achx");
    }

    // The flattened view a runtime player would use and the preview of the editor must show the same chains.
    [Theory]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    public void RandomFiles_InlineAndThePreviewShowTheSameChains(int seed)
    {
        using var dir = new TestHelpers.TempDir();
        WriteAchx(Path.Combine(dir.Path, "hero.achx"));
        string path = Path.Combine(dir.Path, "boss.seqj");
        File.WriteAllText(path, """{ "version": 1, "sources": ["hero.achx"], "sequences": [ { "name": "A" }, { "name": "B" }, { "name": "C" } ] }""");
        var document = SequenceDocument.Load(path);
        var undo = new UndoManager();
        var random = new Random(seed);
        for (int i = 0; i < 80; i++) RandomEdit(document, undo, random);

        double Length(SequenceSave s) => document.SequenceLength(document.Save.Sequences.IndexOf(s));
        double Cycle(SequenceChainReferenceSave r) => document.CycleLength(r) ?? 0;
        for (int s = 0; s < document.Save.Sequences.Count; s++)
        {
            var sequence = document.Save.Sequences[s];
            for (double time = 0; time < Math.Min(6, document.SequenceLength(s)); time += 0.1)
            {
                double pose = SequenceTimeline.PoseTime(time, Length(sequence), sequence.Loop);
                var inlined = new List<string>();
                foreach (var track in SequenceReferences.Inline(document.Save.Sequences, sequence).Where(t => t.Visible))
                    if (SequenceReferences.InlinedTime(track, pose, Length) is { } at
                        && SequenceTimeline.ActiveAt(track.Track, at, Cycle, out _) is { } active) inlined.Add(active.Chain);
                SequenceScene.At(document, s, time).Reverse().Select(sprite => sprite.Chain.Name).ShouldBe(inlined, $"{sequence.Name} at {time}");
            }
        }
    }
}

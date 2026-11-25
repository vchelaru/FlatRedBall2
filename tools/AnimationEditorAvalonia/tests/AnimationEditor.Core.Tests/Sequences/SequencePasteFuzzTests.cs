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

/// <summary>Random copy, cut, paste and duplicate between two files in different folders.</summary>
public class SequencePasteFuzzTests
{
    private static void WriteAchx(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var list = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        foreach (string name in new[] { "Walk", "Run" })
        {
            var chain = new AnimationChainSave { Name = name };
            for (int i = 0; i < 3; i++) chain.Frames.Add(new AnimationFrameSave { TextureName = "a.png", FrameLength = 0.1f, RightCoordinate = 16, BottomCoordinate = 16 });
            list.AnimationChains.Add(chain);
        }
        list.Save(path);
    }

    private static string FullSource(SequenceDocument d, string source) =>
        Path.GetFullPath(Path.Combine(d.Path.GetDirectoryContainingThis().FullPath, source));

    private static List<(string, string, double, double?)> Blocks(SequenceDocument d, IEnumerable<SequenceChainReferenceSave> chains) =>
        chains.Select(c => (FullSource(d, c.Source), c.Chain, Math.Round(c.Start, 6), c.Length)).ToList();

    private static List<(string, string, double, double?)> Copied(IEnumerable<SequenceChainReferenceSave> chains, double shift = 0) =>
        chains.Select(c => (Path.GetFullPath(c.Source), c.Chain, Math.Round(c.Start + shift, 6), c.Length)).ToList();

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void RandomCopyCutPaste_BetweenTwoFolders_KeepsEveryBlock_AndUndoRedoAreExact(int seed)
    {
        using var dir = new TestHelpers.TempDir();
        WriteAchx(Path.Combine(dir.Path, "art", "hero.achx"));
        WriteAchx(Path.Combine(dir.Path, "art", "deep", "boss.achx"));
        string[] paths = { Path.Combine(dir.Path, "one", "a.seqj"), Path.Combine(dir.Path, "art", "deep", "er", "b.seqj") };
        foreach (string p in paths) Directory.CreateDirectory(Path.GetDirectoryName(p)!);
        File.WriteAllText(paths[0], """
            { "version": 3, "zoo": 1, "sources": ["../art/hero.achx"], "sequences": [
              { "name": "A", "note": "keep", "tracks": [ { "name": "T", "tint": 1.5, "chains": [ { "source": "../art/hero.achx", "chain": "Walk", "start": 0, "x": true } ] },
                                                        { "name": "U", "visible": false, "chains": [ { "source": "../art/hero.achx", "chain": "Run", "start": 2, "length": 0.75 } ] } ] },
              { "name": "B", "tracks": [ { "name": "R", "references": [ { "sequence": "A", "start": 0.5, "ease": "in" } ] } ] } ] }
            """);
        File.WriteAllText(paths[1], """
            { "version": 1, "sources": ["../boss.achx"], "sequences": [
              { "name": "A", "tracks": [ { "name": "T", "continuous": true, "chains": [ { "source": "../boss.achx", "chain": "Run", "start": 1 } ] } ] },
              { "name": "C", "tracks": [] } ] }
            """);
        var docs = paths.Select(SequenceDocument.Load).ToArray();
        var undos = new[] { new UndoManager(), new UndoManager() };
        var initial = docs.Select(d => d.Save.ToJsonString()).ToArray();
        var random = new Random(seed);
        string clip = "";
        int pasted = 0;

        for (int step = 0; step < 150; step++)
        {
            int w = random.Next(2);
            var d = docs[w];
            var undo = undos[w];
            if (d.Save.Sequences.Count == 0) d.AddSequence(undo);
            int s = random.Next(d.Save.Sequences.Count);
            d.SelectedSequence = s;
            d.Selection.Reconcile(d.Save.Sequences.Select(x => x.Name).ToList(), s);
            var tracks = d.Save.Sequences[s].Tracks;
            var ids = new List<TimelineChainId>();
            for (int t = 0; t < tracks.Count; t++)
                for (int c = 0; c < tracks[t].Chains.Count; c++)
                    if (random.Next(2) == 0) ids.Add(new TimelineChainId(t, c));
            var trackSet = Enumerable.Range(0, tracks.Count).Where(_ => random.Next(2) == 0).ToList();
            switch (random.Next(9))
            {
                case 0: clip = d.CopySequences(new[] { s, random.Next(d.Save.Sequences.Count) }); break;
                case 1: if (ids.Count > 0) clip = d.CopyChains(s, ids); break;
                case 2: if (trackSet.Count > 0) clip = d.CopyTracks(s, trackSet); break;
                case 3: if (ids.Count is > 0 and < 6) { clip = d.CopyChains(s, ids); d.DeleteChains(undo, s, ids); } break;
                case 4: if (trackSet.Count > 0 && random.Next(2) == 0) { clip = d.CopyTracks(s, trackSet); d.DeleteTracks(undo, s, trackSet); } break;
                case 5: if (ids.Count is > 0 and < 6) d.DuplicateChains(undo, s, ids); break;
                case 6: if (d.Save.Sequences.Count > 4) d.DeleteSequences(undo, new[] { s, 0 }); else d.DuplicateSequences(undo, new[] { s }); break;
                default:
                    if (SequenceClipboard.TryRead(clip, out var sequences, out _))
                    {
                        if (d.Save.Sequences.Count > 8) break;
                        var result = d.PasteSequences(undo, sequences!);
                        foreach (var (name, original) in result.Names.Zip(sequences!.Sequences))
                        {
                            var made = d.Save.Sequences.Single(x => x.Name == name);
                            Blocks(d, made.Tracks.SelectMany(t => t.Chains)).ShouldBe(Copied(original.Tracks.SelectMany(t => t.Chains)));
                            pasted++;
                        }
                    }
                    else if (TimelineClipboard.TryRead(clip, out var content, out _))
                    {
                        if (tracks.Count > 12 || tracks.Sum(t => t.Chains.Count) > 60) break;
                        int? lane = tracks.Count > 0 && random.Next(2) == 0 ? random.Next(tracks.Count) : null;
                        bool chains = content!.Kind == TimelineClipboardKind.Chains;
                        var result = chains ? d.PasteChains(undo, s, content, lane, 1.25) : d.PasteTracks(undo, s, content, lane);
                        if (result.Errors.Count > 0) break;
                        var after = d.Save.Sequences[s].Tracks;
                        if (chains) Blocks(d, result.Chains.Select(id => after[id.Track].Chains[id.Chain])).ShouldBe(Copied(content.Chains, 1.25));
                        else Blocks(d, result.Tracks.SelectMany(t => after[t].Chains)).ShouldBe(Copied(content.Tracks.SelectMany(t => t.Chains)));
                        pasted++;
                    }
                    break;
            }
            if (undo.CanUndo)
                SequenceListSave.FromFile(paths[w]).ToJsonString().ShouldBe(d.Save.ToJsonString(), $"step {step}");
            d.Save.Sequences.SelectMany(x => x.Tracks).SelectMany(t => t.Chains).ShouldAllBe(c => !d.IsSourceMissing(c.Source));
        }
        pasted.ShouldBeGreaterThan(10);
        for (int w = 0; w < 2; w++)
        {
            string final = docs[w].Save.ToJsonString();
            while (undos[w].CanUndo) undos[w].Undo();
            docs[w].Save.ToJsonString().ShouldBe(initial[w]);
            while (undos[w].CanRedo) undos[w].Redo();
            docs[w].Save.ToJsonString().ShouldBe(final);
            SequenceListSave.FromFile(paths[w]).ToJsonString().ShouldBe(final);
        }
    }
}

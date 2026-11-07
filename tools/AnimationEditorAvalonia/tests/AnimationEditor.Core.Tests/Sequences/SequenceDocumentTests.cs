using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System;
using System.IO;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

public sealed class SequenceDocumentTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("seqdoc").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    private string WriteSource(string name, params AnimationChainSave[] chains)
    {
        var list = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        list.AnimationChains.AddRange(chains);
        string path = Path.Combine(_dir, name);
        list.SaveJson(path);
        return path;
    }

    private static AnimationChainSave Chain(string name, bool pingPong, params float[] lengths)
    {
        var chain = new AnimationChainSave { Name = name, PingPong = pingPong };
        foreach (var length in lengths)
            chain.Frames.Add(new AnimationFrameSave { FrameLength = length, TextureName = "sheet.png" });
        return chain;
    }

    [Fact]
    public void Sample_PingPongChainWithTwoLoops_PlaysTwiceTheForwardLengthPerCycle()
    {
        // Forward length 0.4s, so one ping-pong cycle is 0.8s and two loops end at 1.6s.
        WriteSource("hero.achj", Chain("Wave", pingPong: true, 0.2f, 0.2f));
        var document = new SequenceDocument(Path.Combine(_dir, "hero.seqj"), new SequenceListSave { Sources = { "hero.achj" } });
        var sequence = new SequenceSave();
        var reference = new SequenceChainReferenceSave { Source = "hero.achj", Chain = "Wave", Loops = 2 };
        sequence.Tracks.Add(new SequenceTrackSave { Chains = { reference } });

        document.CycleLength(reference).ShouldBe(0.8, 1e-6);
        SequenceTimeline.Length(sequence, document.CycleLength).ShouldBe(1.6, 1e-6);
        // 0.5s is on the way back: the mirrored half shows frame 1 first.
        document.Sample(sequence, 0.5).ShouldHaveSingleItem().FrameIndex.ShouldBe(1);
        document.Sample(sequence, 1.7).ShouldBeEmpty();
    }

    [Fact]
    public void Sample_MissingChain_HidesTheTrackButKeepsTheReference()
    {
        var reference = new SequenceChainReferenceSave { Source = "gone.achj", Chain = "Old", Start = 0, Loops = 3 };
        var save = new SequenceListSave { Sources = { "gone.achj" } };
        save.Sequences.Add(new SequenceSave { Tracks = { new SequenceTrackSave { Chains = { reference } } } });
        var document = new SequenceDocument(Path.Combine(_dir, "a.seqj"), save);

        document.IsSourceMissing("gone.achj").ShouldBeTrue();
        document.Sample(save.Sequences[0], 0.1).ShouldBeEmpty();
        save.Sequences[0].Tracks[0].Chains.ShouldContain(reference);
    }

    [Fact]
    public void AddSource_SeqjFile_IsRefused()
    {
        var document = new SequenceDocument(Path.Combine(_dir, "a.seqj"), new SequenceListSave());

        document.AddSource(new UndoManager(), Path.Combine(_dir, "b.seqj")).ShouldNotBeNull();

        document.Save.Sources.ShouldBeEmpty();
    }

    [Fact]
    public void PlaceChain_OnNewTrackRow_AddsATrackAndUndoRemovesIt()
    {
        string seqj = Path.Combine(_dir, "a.seqj");
        var save = new SequenceListSave();
        save.Sequences.Add(new SequenceSave { Name = "Attack" });
        var document = new SequenceDocument(seqj, save);
        var undo = new UndoManager();

        document.PlaceChain(undo, sequenceIndex: 0, trackIndex: 0, "hero.achj", "Walk", start: 1.5);

        var track = document.Save.Sequences[0].Tracks.ShouldHaveSingleItem();
        track.Chains.ShouldHaveSingleItem().Start.ShouldBe(1.5);
        undo.UndoHistory.ShouldHaveSingleItem().Description.ShouldBe("Place 'Walk'");
        SequenceListSave.FromFile(seqj).Sequences[0].Tracks.Count.ShouldBe(1);

        undo.Undo();

        document.Save.Sequences[0].Tracks.ShouldBeEmpty();
        SequenceListSave.FromFile(seqj).Sequences[0].Tracks.ShouldBeEmpty();
    }
}

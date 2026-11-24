using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.IO;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>A played sequence never blanks at a multiple of its length (<see cref="SequenceTimeline.WrapTime(double, double, bool)"/>).</summary>
public class SequenceReferenceLoopWrapTests
{
    // Chain Aura: four 0.1 s frames, whose float lengths sum to a hair over 0.4 (exactly 0.4 in a ms file).
    private static SequenceDocument Load(TestHelpers.TempDir dir, TimeMeasurementUnit unit, params SequenceSave[] sequences)
    {
        var list = new AnimationChainListSave { TimeMeasurementUnit = unit };
        var chain = new AnimationChainSave { Name = "Aura", Loop = true };
        float length = unit == TimeMeasurementUnit.Millisecond ? 100f : 0.1f;
        for (int i = 0; i < 4; i++)
            chain.Frames.Add(new AnimationFrameSave { TextureName = "t.png", FrameLength = length, RightCoordinate = 1, BottomCoordinate = 1 });
        list.AnimationChains.Add(chain);
        list.SaveJson(Path.Combine(dir.Path, "fx.achj"));
        var save = new SequenceListSave { Sources = { "fx.achj" } };
        save.Sequences.AddRange(sequences);
        string path = Path.Combine(dir.Path, "boss.seqj");
        save.Save(path);
        return SequenceDocument.Load(path);
    }

    private static SequenceSave Aura(bool loop = true) => new()
    {
        Name = "Aura",
        Loop = loop,
        Tracks = { new SequenceTrackSave { Name = "Fx", Chains = { new SequenceChainReferenceSave { Source = "fx.achj", Chain = "Aura" } } } },
    };

    private static SequenceSave Holder(string name, bool continuous, params SequenceReferenceSave[] clips) => new()
    {
        Name = name,
        Loop = true,
        Tracks = { new SequenceTrackSave { Name = "Ref", Continuous = continuous, References = [.. clips] } },
    };

    private static SequenceReferenceSave Clip(string target, double start, double length) =>
        new() { Sequence = target, Start = start, Length = length };

    // The frame index shown at each time, "-" when nothing shows.
    private static string FramesAt(SequenceDocument document, int sequence, int pass, params double[] times) =>
        string.Join(" ", times.Select(time =>
        {
            var sprites = SequenceScene.At(document, sequence, time, pass);
            return sprites.Count == 0 ? "-" : string.Join("+", sprites.Select(sprite => sprite.FrameIndex));
        }));

    [Theory]
    [InlineData(TimeMeasurementUnit.Second)]
    [InlineData(TimeMeasurementUnit.Millisecond)]
    public void At_MultiplesOfALoopingTarget_ShowsItsFirstFrame(TimeMeasurementUnit unit)
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir, unit, Aura(), Holder("Boss", false, Clip("Aura", 0, 2)));

        FramesAt(document, 1, 0, 0.39, 0.4, 0.41, 0.79, 0.8, 0.81, 1.2, 1.6, 1.99, 2).ShouldBe("3 0 0 3 0 0 0 0 3 -");
    }

    [Fact]
    public void At_MultiplesOfALoopingTarget_InASecondClipAndThroughNestedReferences()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir, TimeMeasurementUnit.Second, Aura(),
            Holder("Boss", false, Clip("Aura", 0, 1), Clip("Aura", 1, 2)),
            Holder("Outer", false, Clip("Boss", 0.5, 9)));

        FramesAt(document, 1, 0, 1.4, 1.8, 2.6).ShouldBe("0 0 0");
        FramesAt(document, 2, 0, 0.9, 1.9, 3.5, 3.9).ShouldBe("0 0 0 0"); // Boss at 0.4, 1.4, 3 (its own wrap), 3.4
    }

    [Fact]
    public void At_ContinuousHolder_ShowsTheFirstFrameAtEveryMultipleAcrossPasses()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir, TimeMeasurementUnit.Second, Aura(), Holder("Boss", true, Clip("Aura", 0, 2)));

        FramesAt(document, 1, 1, 0, 0.4, 0.8).ShouldBe("0 0 0");
        FramesAt(document, 1, 7, 0, 0.4, 0.8).ShouldBe("0 0 0");
    }

    [Fact]
    public void At_NonLoopingTarget_HoldsItsFinalFrameAtExactlyItsLength()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir, TimeMeasurementUnit.Second, Aura(loop: false), Holder("Boss", false, Clip("Aura", 0, 2)));

        FramesAt(document, 1, 0, 0.39, 0.4, 0.41, 0.8).ShouldBe("3 3 3 3");
        FramesAt(document, 0, 0, 0.39, 0.4, 0.41).ShouldBe("3 3 3");
    }

    [Theory]
    [InlineData(120)]
    [InlineData(3600)]
    public void At_LongNonLoopingSequence_HoldsItsFinalFrameDirectlyAndAsATarget(double length)
    {
        using var dir = new TestHelpers.TempDir();
        var aura = Aura(loop: false);
        aura.Tracks[0].Chains[0].Length = length;
        var holder = Holder("Boss", false, Clip("Aura", 0, length));
        holder.Loop = false;
        var document = Load(dir, TimeMeasurementUnit.Second, aura, holder, Holder("Longer", false, Clip("Aura", 0, length + 10)));

        FramesAt(document, 0, 0, length, length + 5).ShouldBe("3 3");
        FramesAt(document, 1, 0, length, length + 5).ShouldBe("3 3");
        FramesAt(document, 2, 0, length, length + 5).ShouldBe("3 3");
    }

    [Fact]
    public void At_LongContinuousHolder_KeepsTheTargetsPhaseAcrossTheWrap()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir, TimeMeasurementUnit.Second, Aura(), Holder("Boss", true, Clip("Aura", 0, 120.25)));

        FramesAt(document, 1, 1, 0).ShouldBe("2"); // 120.25 s is 300 loops of 0.4 s and 0.25 s more
    }

    [Fact]
    public void Clock_WrappingWithinTheSlackOfTheEnd_StartsTheNextPassAtZero()
    {
        double length = 4 * (double)0.1f;
        var clock = new SequenceClock();
        clock.Play(length);

        clock.Advance(0.4, length, loop: true);
        (clock.Time, clock.Pass).ShouldBe((0.0, 1));

        clock.Advance(0.8, length, loop: true);
        (clock.Time, clock.Pass).ShouldBe((0.0, 3));
        clock.IsPlaying.ShouldBeTrue();
    }
}

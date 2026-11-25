using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.Export;
using AnimationEditor.Core.Keyframes;
using AnimationEditor.Core.Rendering;
using AnimationEditor.Core.Utilities;
using AnimationEditor.Core.ViewModels;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// #2137: the editor skips a frame of no length, as the game does, and an animation made only of such frames has no length.
/// Nothing that repeats or wraps by a length may loop forever, so those tests have a time limit and a hang fails.
/// </summary>
public class ZeroLengthFrameTests
{
    private const int TimeLimit = 10000;

    private static readonly FrameLengthUnit Sec = FrameLengthUnit.Seconds;

    private sealed class Frame : AnimationFrameBase { }

    private static AnimationChainSave Chain(bool loop, bool pingPong, bool reverse, params float[] lengths)
    {
        var chain = new AnimationChainSave { Name = "A", Loop = loop, PingPong = pingPong, Reverse = reverse };
        for (int i = 0; i < lengths.Length; i++)
            chain.Frames.Add(new AnimationFrameSave { FrameLength = lengths[i], RelativeX = i * 10, OffsetInterpolation = "Linear" });
        return chain;
    }

    private static AnimationPlayer<Frame> Player(AnimationChainSave save, TimeMeasurementUnit unit)
    {
        var chain = new AnimationChain<Frame> { Name = "A", Loop = save.Loop, PingPong = save.PingPong, Reversed = save.Reverse };
        chain.AddRange(save.Frames.Select(f => new Frame
        {
            FrameLength = f.GetDuration(unit), RelativeX = f.RelativeX,
            OffsetCurve = KeyframeCurve.Parse(f.OffsetInterpolation, f.OffsetEasing),
        }));
        var player = new AnimationPlayer<Frame>(new AnimationChainList<Frame> { chain });
        player.Play("A");
        return player;
    }

    public static TheoryData<float[], bool, bool, bool, bool> Cases()
    {
        var data = new TheoryData<float[], bool, bool, bool, bool>();
        float[][] lengths =
        [
            [0f], [0f, 0f, 0f], [0f, 0.2f, 0.2f], [0.2f, 0f, 0.2f], [0.2f, 0.2f, 0f], [0f, 0.2f, 0f], [0f, 0.0001f, 0f], [0f, 1e-7f, 0f],
            [0f, 1e-9f, 0f], [0f, 1e-30f, 0.2f],
        ];
        foreach (var one in lengths)
            foreach (bool loop in new[] { true, false })
                foreach (bool pingPong in new[] { false, true })
                    foreach (bool reverse in new[] { false, true })
                        data.Add(one, loop, pingPong, reverse, one.Length == 3 && loop);
        return data;
    }

    // The preview, the stateless clock and the game show the same frame and offset at every step.
    [Theory(Timeout = TimeLimit)]
    [MemberData(nameof(Cases))]
    public async Task Preview_ZeroLengthFrames_ShowsWhatTheGameShows(float[] lengths, bool loop, bool pingPong, bool reverse, bool milliseconds)
    {
        var unit = milliseconds ? FrameLengthUnit.Milliseconds : Sec;
        var save = Chain(loop, pingPong, reverse, lengths.Select(l => l * (float)unit.UnitsPerSecond).ToArray());
        var differences = new List<string>();

        await Task.Run(() =>
        {
            var player = Player(save, unit.Unit);
            var preview = new PlaybackController();
            preview.SetChain(save, unit);
            double time = 0;
            for (int step = 0; step < 60; step++)
            {
                int shown = player.CurrentFrameIndex;
                var (clock, elapsed) = ChainClock.Locate(save, time, unit);
                float game = player.CurrentOffset.X;
                float clockX = KeyframeSampler.Sample(save, unit, clock, elapsed).OffsetX;
                float previewX = KeyframeSampler.Sample(save, unit, preview.CurrentFrameIndex, preview.FrameElapsed).OffsetX;
                // A cycle of nanoseconds lands on an arbitrary one of its frames.
                bool longEnough = lengths.Sum() > 1e-3 || lengths.Max() < 1e-8f;
                if (longEnough && (preview.CurrentFrameIndex != shown || clock != shown || Math.Abs(previewX - game) > 0.2f || Math.Abs(clockX - game) > 0.2f))
                    differences.Add($"t={time}: game {shown} {game}, preview {preview.CurrentFrameIndex} {previewX}, clock {clock} {clockX}");
                player.Update(TimeSpan.FromSeconds(0.0317));
                preview.Advance(0.0317);
                time += 0.0317;
            }
            if (lengths.Length > 1) preview.IsPlaying.ShouldBe(player.Animate);
            player.Update(TimeSpan.FromDays(30));
            preview.Advance(TimeSpan.FromDays(30).TotalSeconds);
            ChainClock.Locate(save, TimeSpan.FromDays(30).TotalSeconds, unit);
        });

        differences.ShouldBeEmpty();
    }

    // Negative, NaN and infinite lengths are garbage the editor cannot produce: it skips them.
    [Theory]
    [InlineData(0f)]
    [InlineData(-0.5f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    [InlineData(1e-9f)]
    public void PlaybackSeconds_LengthTheGameCannotPlay_IsZero(float length)
    {
        Sec.PlaybackSeconds(length).ShouldBe(0);
        FrameLengthUnit.Milliseconds.PlaybackSeconds(length * 1000).ShouldBe(0);
        ChainClock.TotalLength(Chain(true, true, false, length, 0.5f, length), Sec).ShouldBe(1);
        ChainClock.Locate(Chain(true, false, false, length, 0.5f, length), 0.2, Sec).FrameIndex.ShouldBe(1);
    }

    [Fact]
    public void TotalLength_AllZero_IsZero() => ChainClock.TotalLength(Chain(true, true, true, 0f, 0f), Sec).ShouldBe(0);

    // A glide passes a zero-length frame in no time instead of taking 0.1 s over it.
    [Fact]
    public void Sample_GlideAcrossAZeroLengthFrame_TakesNoTimeOverIt()
    {
        var chain = Chain(false, false, false, 1f, 0f, 1f);
        chain.Frames[1].SkipOffsetKeyframe = true;

        KeyframeSampler.Sample(chain, Sec, 0, 0.5).OffsetX.ShouldBe(10f, 0.01f);
        KeyframeSampler.Sample(chain, Sec, 0, 1).OffsetX.ShouldBe(20f, 0.01f);
    }

    [Theory(Timeout = TimeLimit)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task SeekToFrame_ZeroLengthFrames_ShowsTheFrameUntilPlaybackMoves(bool pingPong, bool reverse)
    {
        var seen = new List<int>();
        await Task.Run(() =>
        {
            var preview = new PlaybackController();
            preview.SetChain(Chain(true, pingPong, reverse, 0f, 0.2f, 0f), Sec);
            preview.Pause();
            foreach (int frame in new[] { 0, 1, 2, 1, 0 })
            {
                preview.SeekToFrame(frame, 0.5);
                seen.Add(preview.CurrentFrameIndex);
            }
            preview.Play();
            preview.Advance(0.05);
            seen.Add(preview.CurrentFrameIndex);
        });

        seen.ShouldBe([0, 1, 2, 1, 0, 1]);
    }

    // A hot reload can zero the lengths of the animation that is playing.
    [Theory(Timeout = TimeLimit)]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Advance_LengthsBecomeZeroWhilePlaying_Returns(bool loop)
    {
        var chain = Chain(loop, false, false, 1f, 1f);
        var preview = new PlaybackController();
        preview.SetChain(chain, Sec);
        preview.Advance(1.5);
        foreach (var frame in chain.Frames) frame.FrameLength = 0;

        await Task.Run(() => { for (int i = 0; i < 100; i++) preview.Advance(86400); });

        preview.CurrentFrameIndex.ShouldBe(1);
        preview.IsPlaying.ShouldBe(loop);
    }

    [Theory(Timeout = TimeLimit)]
    [InlineData(0f, 1)]
    [InlineData(1e-7f, 1)]
    public async Task Plan_AnimationOfNoLength_IsOneFrame(float length, int frames)
    {
        var plan = await Task.Run(() => ExportTimelinePlanner.Plan([new ExportSourceChain(Chain(true, true, false, 0f, length, 0f), Sec)], 50, 100, 2));

        plan.Count.ShouldBe(frames);
        plan.Sum(f => f.DelayTicks).ShouldBe(2);
    }

    [Fact]
    public void Plan_ZeroLengthFrame_IsLeftOut()
    {
        var chain = Chain(true, false, false, 0.2f, 0f, 0.2f);
        foreach (var frame in chain.Frames) frame.OffsetInterpolation = null;

        var plan = ExportTimelinePlanner.Plan([new ExportSourceChain(chain, Sec)], 50, 100, 2);

        plan.Select(f => f.Layers[0].Frame).ShouldBe([chain.Frames[0], chain.Frames[2]]);
        plan.Sum(f => f.DelayTicks).ShouldBe(40);
    }

    // Beside an animation with a length, one with none holds the frame the game holds, for as long as the other plays.
    [Fact]
    public void Plan_AnimationOfNoLengthBesideAnother_LastsAsLongAsTheOther()
    {
        var plan = ExportTimelinePlanner.Plan(
            [new ExportSourceChain(Chain(true, false, false, 0f, 0f), Sec), new ExportSourceChain(Chain(true, false, false, 0.4f), Sec)], 50, 100, 2);

        plan.Sum(f => f.DelayTicks).ShouldBe(40);
    }

    // Godot and PixiJS already had a rule of their own for a zero-length frame, so these files stay as they were.
    [Fact]
    public void GodotAndPixiJs_ZeroLengthFrames_AreStillWritten()
    {
        var list = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        list.AnimationChains.Add(Chain(true, false, false, 0f, 0.2f, 0f));
        list.AnimationChains.Add(Chain(true, false, false, 0f, 0f));
        foreach (var frame in list.AnimationChains.SelectMany(c => c.Frames)) frame.TextureName = "a.png";

        var godot = GodotSpriteFramesExporter.Export(list, _ => (16, 16)).Text;
        var pixi = PixiJsSpriteSheetExporter.Export(list, _ => (16, 16)).Text;

        godot.Split("\"duration\": 0.01").Length.ShouldBe(5);
        godot.Split("SubResource(").Length.ShouldBe(6);
        pixi.Split("\"sourceSize\"").Length.ShouldBe(6);
    }

    [Theory(Timeout = TimeLimit)]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(1e-30f)]
    public async Task TimelineStrip_LengthTheGameCannotPlay_IsAMinimumCellAndAddsNothing(float length)
    {
        var chain = Chain(true, false, false, length, 0.5f);

        var cells = await Task.Run(() => TimelineBuilder.BuildFrameItems(chain, Sec));

        cells.Select(c => c.Width).ShouldBe([TimelineBuilder.MinCellWidth, 60]);
        TimelineBuilder.FormatTotal(chain, Sec).ShouldBe(Sec.Format(0.5f));
        TimelineBuilder.ComputeSharedEffectivePixelsPerSecond([chain], Sec).ShouldBe(TimelineBuilder.PixelsPerSecond);
    }
}

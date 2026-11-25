using System;
using System.Collections.Generic;
using System.Linq;
using AnimationEditor.Core.Rendering;
using AnimationEditor.Core.Utilities;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>The editor's clock and the runtime player show the same frame and offset for the same chain and time.</summary>
public class PreviewMatchesRuntimeTests
{
    private sealed class Frame : AnimationFrameBase { }

    // As AnimationChainListSaveExtensions.BuildList converts it.
    private static Frame ToRuntime(AnimationFrameSave save, bool ms) => new()
    {
        FrameLength = TimeSpan.FromSeconds(save.FrameLength / (ms ? 1000f : 1f)),
        RelativeX = save.RelativeX,
        SkipOffsetKeyframe = save.SkipOffsetKeyframe,
        OffsetCurve = KeyframeCurve.Parse(save.OffsetInterpolation, save.OffsetEasing),
    };

    [Fact]
    public void RandomChains_SameFrameAndOffsetAtEveryTime()
    {
        var random = new Random(7);
        string?[] curves = [null, "Linear", "Cubic", "Back"];
        var differences = new List<string>();
        for (int n = 0; n < 400; n++)
        {
            bool ms = n % 2 == 1;
            var save = new AnimationChainSave { Name = "A", Loop = random.Next(2) == 0, PingPong = random.Next(2) == 0, Reverse = random.Next(2) == 0 };
            for (int f = random.Next(1, 6); f > 0; f--)
                save.Frames.Add(new AnimationFrameSave
                {
                    FrameLength = ms ? random.Next(1, 300) : random.Next(1, 300) / 1000f, RelativeX = random.Next(-50, 50),
                    SkipOffsetKeyframe = random.Next(4) == 0, OffsetInterpolation = curves[random.Next(4)], OffsetEasing = "InOut",
                });
            var unit = ms ? FrameLengthUnit.Milliseconds : FrameLengthUnit.Seconds;
            var chain = new AnimationChain<Frame> { Name = "A", Loop = save.Loop, PingPong = save.PingPong, Reversed = save.Reverse };
            chain.AddRange(save.Frames.Select(f => ToRuntime(f, ms)));
            var player = new AnimationPlayer<Frame>(new AnimationChainList<Frame> { chain });
            player.Play("A");
            double time = 0;
            for (int step = 0; step < 40; step++)
            {
                double dt = random.NextDouble() * 0.4 + 0.0137;
                player.Update(TimeSpan.FromSeconds(dt));
                time += TimeSpan.FromSeconds(dt).TotalSeconds;
                var (frame, elapsed) = ChainClock.Locate(save, time, unit);
                var offset = KeyframeEvaluator.EvaluateOffset(save, frame, elapsed, unit.Unit);
                if (frame != player.CurrentFrameIndex || Math.Abs(offset.X - player.CurrentOffset.X) > 0.2f)
                    differences.Add($"chain {n} loop={save.Loop} pp={save.PingPong} rev={save.Reverse} ms={ms} t={time}: editor {frame} {offset.X}, game {player.CurrentFrameIndex} {player.CurrentOffset.X}");
            }
        }

        differences.ShouldBeEmpty();
    }
}

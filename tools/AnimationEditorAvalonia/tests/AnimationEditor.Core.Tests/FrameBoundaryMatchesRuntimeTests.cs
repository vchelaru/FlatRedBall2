using System;
using System.Collections.Generic;
using System.Linq;
using AnimationEditor.Core.Rendering;
using AnimationEditor.Core.Utilities;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>A time exactly on a frame boundary shows the later frame in the editor and in the runtime player.</summary>
public class FrameBoundaryMatchesRuntimeTests
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BoundaryTimes_EnterTheSameFrame(bool ms)
    {
        var differences = new List<string>();
        foreach (int length in new[] { 10, 16, 33, 50, 70, 83, 100, 130 })
        {
            var save = new AnimationChainSave { Name = "A" };
            for (int f = 0; f < 5; f++) save.Frames.Add(new AnimationFrameSave { FrameLength = ms ? length : length / 1000f });
            var chain = new AnimationChain<Frame> { Name = "A" };
            chain.AddRange(save.Frames.Select(f => ToRuntime(f, ms)));
            var player = new AnimationPlayer<Frame>(new AnimationChainList<Frame> { chain });
            player.Play("A");
            for (int k = 1; k <= 12; k++)
            {
                player.TimeIntoAnimation = TimeSpan.FromMilliseconds(length * k);
                int editor = ChainClock.Locate(save, length * k / 1000.0, ms ? FrameLengthUnit.Milliseconds : FrameLengthUnit.Seconds).FrameIndex;
                if (editor != player.CurrentFrameIndex) differences.Add($"{length}ms x{k}: editor {editor}, game {player.CurrentFrameIndex}");
            }
        }

        differences.ShouldBeEmpty();
    }
}

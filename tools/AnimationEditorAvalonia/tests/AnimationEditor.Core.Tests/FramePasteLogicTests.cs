using AnimationEditor.Core.IO;
using FlatRedBall2.Animation.Content;
using System.Collections.Generic;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class FramePasteLogicTests
{
    static AnimationFrameSave Frame(string name) => new AnimationFrameSave { Name = name };

    [Fact]
    public void AssignUniqueNames_CollidingName_RenamesWithNextFreeFrameN()
    {
        var existing = new List<AnimationFrameSave> { Frame("Frame 1") };
        var pasted   = new List<AnimationFrameSave> { Frame("Frame 1") };

        FramePasteLogic.AssignUniqueNames(existing, pasted);

        Assert.Equal("Frame 2", pasted[0].Name);
    }

    [Fact]
    public void AssignUniqueNames_CustomName_FallsBackToNextFreeFrameN()
    {
        var existing = new List<AnimationFrameSave> { Frame("Frame 1") };
        var pasted   = new List<AnimationFrameSave> { Frame("Jump") };

        FramePasteLogic.AssignUniqueNames(existing, pasted);

        Assert.Equal("Frame 2", pasted[0].Name);
    }

    [Fact]
    public void AssignUniqueNames_FrameAlreadyFreeWithSameName_KeepsName()
    {
        // "Frame 3" is free (Frame 1 and Frame 2 are taken), so NextFreeFrameName returns "Frame 3".
        var existing = new List<AnimationFrameSave> { Frame("Frame 1"), Frame("Frame 2") };
        var pasted   = new List<AnimationFrameSave> { Frame("Frame 3") };

        FramePasteLogic.AssignUniqueNames(existing, pasted);

        Assert.Equal("Frame 3", pasted[0].Name);
    }

    [Fact]
    public void AssignUniqueNames_GapInSequence_FillsGap()
    {
        var existing = new List<AnimationFrameSave> { Frame("Frame 1"), Frame("Frame 3") };
        var pasted   = new List<AnimationFrameSave> { Frame("Frame 1") };

        FramePasteLogic.AssignUniqueNames(existing, pasted);

        Assert.Equal("Frame 2", pasted[0].Name);
    }

    [Fact]
    public void AssignUniqueNames_MultipleFramesPasted_AllGetUniqueNames()
    {
        var existing = new List<AnimationFrameSave> { Frame("Frame 1") };
        var pasted   = new List<AnimationFrameSave> { Frame("Frame 1"), Frame("Frame 1") };

        FramePasteLogic.AssignUniqueNames(existing, pasted);

        Assert.Equal("Frame 2", pasted[0].Name);
        Assert.Equal("Frame 3", pasted[1].Name);
    }

    [Fact]
    public void AssignUniqueNames_NoExistingFrames_StartsAtFrame1()
    {
        var existing = new List<AnimationFrameSave>();
        var pasted   = new List<AnimationFrameSave> { Frame("Frame 1") };

        FramePasteLogic.AssignUniqueNames(existing, pasted);

        Assert.Equal("Frame 1", pasted[0].Name);
    }
}

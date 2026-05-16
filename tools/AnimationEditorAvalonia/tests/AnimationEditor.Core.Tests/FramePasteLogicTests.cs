using AnimationEditor.Core.IO;
using FlatRedBall2.Animation.Content;
using System.Collections.Generic;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class FramePasteLogicTests
{
    [Fact]
    public void AssignUniqueNames_NonCustomFrame_ClearsName()
    {
        var existing = new List<AnimationFrameSave> { new AnimationFrameSave { Name = "Frame 1" } };
        var pasted   = new List<AnimationFrameSave> { new AnimationFrameSave { Name = "Frame 1" } };
        // HasCustomName=false (default) → name should be cleared

        FramePasteLogic.AssignUniqueNames(existing, pasted);

        Assert.Equal(string.Empty, pasted[0].Name);
    }

    [Fact]
    public void AssignUniqueNames_CustomNamedFrame_KeepsName()
    {
        var existing = new List<AnimationFrameSave>();
        var pasted   = new List<AnimationFrameSave>
        {
            new AnimationFrameSave { HasCustomName = true, Name = "Jump" }
        };

        FramePasteLogic.AssignUniqueNames(existing, pasted);

        Assert.Equal("Jump", pasted[0].Name);
        Assert.True(pasted[0].HasCustomName);
    }

    [Fact]
    public void AssignUniqueNames_MultipleNonCustomFrames_AllCleared()
    {
        var existing = new List<AnimationFrameSave>();
        var pasted   = new List<AnimationFrameSave>
        {
            new AnimationFrameSave { Name = "Frame 1" },
            new AnimationFrameSave { Name = "Frame 2" },
        };

        FramePasteLogic.AssignUniqueNames(existing, pasted);

        Assert.Equal(string.Empty, pasted[0].Name);
        Assert.Equal(string.Empty, pasted[1].Name);
    }
}

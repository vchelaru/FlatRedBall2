using System.Collections.Generic;
using AnimationEditor.Core.Hotkeys;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class HotkeyRegistryTests
{
    private static HotkeyDefinition MakeDefinition(string id, params HotkeyGesture[] gestures) =>
        new()
        {
            Id = id,
            Description = id,
            Category = "Edit",
            Gestures = gestures,
            Action = () => { },
        };

    [Fact]
    public void FindDuplicateGestures_TwoDefinitionsShareAGesture_ReturnsThePair()
    {
        var shared = new HotkeyGesture("Z", HotkeyModifiers.Command);
        var hotkeys = new List<HotkeyDefinition>
        {
            MakeDefinition("undo", shared),
            MakeDefinition("some-other-z-command", shared),
        };

        var duplicates = HotkeyRegistry.FindDuplicateGestures(hotkeys);

        Assert.Single(duplicates);
        Assert.Equal("undo", duplicates[0].FirstId);
        Assert.Equal("some-other-z-command", duplicates[0].SecondId);
    }

    [Fact]
    public void FindDuplicateGestures_NoOverlap_ReturnsEmpty()
    {
        var hotkeys = new List<HotkeyDefinition>
        {
            MakeDefinition("copy", new HotkeyGesture("C", HotkeyModifiers.Command)),
            MakeDefinition("undo", new HotkeyGesture("Z", HotkeyModifiers.Command, Forbidden: HotkeyModifiers.Shift)),
            MakeDefinition("redo",
                new HotkeyGesture("Y", HotkeyModifiers.Command),
                new HotkeyGesture("Z", HotkeyModifiers.Command | HotkeyModifiers.Shift, Forbidden: HotkeyModifiers.Alt)),
        };

        var duplicates = HotkeyRegistry.FindDuplicateGestures(hotkeys);

        Assert.Empty(duplicates);
    }

    [Fact]
    public void FindMatch_RedoSecondGesture_CtrlShiftZ_ReturnsRedoNotUndo()
    {
        var hotkeys = new List<HotkeyDefinition>
        {
            MakeDefinition("undo", new HotkeyGesture("Z", HotkeyModifiers.Command, Forbidden: HotkeyModifiers.Shift)),
            MakeDefinition("redo",
                new HotkeyGesture("Y", HotkeyModifiers.Command),
                new HotkeyGesture("Z", HotkeyModifiers.Command | HotkeyModifiers.Shift, Forbidden: HotkeyModifiers.Alt)),
        };

        var match = HotkeyRegistry.FindMatch(hotkeys, "Z", HotkeyModifiers.Command | HotkeyModifiers.Shift);

        Assert.NotNull(match);
        Assert.Equal("redo", match!.Id);
    }

    [Fact]
    public void FindMatch_FirstDefinitionMatches_StopsBeforeLaterDefinitions()
    {
        var hotkeys = new List<HotkeyDefinition>
        {
            MakeDefinition("copy", new HotkeyGesture("C", HotkeyModifiers.Command)),
            MakeDefinition("copy-again", new HotkeyGesture("C", HotkeyModifiers.Command)),
        };

        var match = HotkeyRegistry.FindMatch(hotkeys, "C", HotkeyModifiers.Command);

        Assert.Equal("copy", match!.Id);
    }

    [Fact]
    public void FindMatch_NoDefinitionMatches_ReturnsNull()
    {
        var hotkeys = new List<HotkeyDefinition>
        {
            MakeDefinition("copy", new HotkeyGesture("C", HotkeyModifiers.Command)),
        };

        var match = HotkeyRegistry.FindMatch(hotkeys, "V", HotkeyModifiers.Command);

        Assert.Null(match);
    }
}

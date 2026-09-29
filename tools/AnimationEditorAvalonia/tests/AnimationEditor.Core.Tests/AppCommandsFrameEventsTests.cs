using System.Linq;
using FlatRedBall2.Animation;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Frame event editing (issue #1121): add, edit, remove through <c>AppCommands</c>, each undoable.
/// Round-trip of every command is also covered by <see cref="UndoCoverageRosterTests"/>.
/// </summary>
[Collection("SequentialSingletons")]
public class AppCommandsFrameEventsTests
{
    [Fact]
    public void AddFrameEvent_AppendsNamedEvent_UndoRemovesIt()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var frame = TestHelpers.MakeChain(ctx.Acls, "Walk", frameCount: 1).Frames[0];

        ctx.AppCommands.AddFrameEvent(frame, "Footstep");

        Assert.Equal("Footstep", Assert.Single(frame.Events).Name);
        Assert.Equal("Add Event 'Footstep'", ctx.UndoManager.UndoHistory.Last().Description);
        ctx.UndoManager.Undo();
        Assert.Empty(frame.Events);
    }

    [Fact]
    public void SetFrameEvent_BlankData_StoresNullAndUndoRestoresOriginal()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var frame = TestHelpers.MakeChain(ctx.Acls, "Walk", frameCount: 1).Frames[0];
        frame.Events.Add(new AnimationFrameEvent { Name = "Step", Data = "left" });

        ctx.AppCommands.SetFrameEvent(frame, 0, "Footstep", "  ");

        Assert.Equal("Footstep", frame.Events[0].Name);
        Assert.Null(frame.Events[0].Data);
        ctx.UndoManager.Undo();
        Assert.Equal("Step", frame.Events[0].Name);
        Assert.Equal("left", frame.Events[0].Data);
    }

    [Fact]
    public void SetFrameEvent_ValuesUnchanged_RecordsNoUndo()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var frame = TestHelpers.MakeChain(ctx.Acls, "Walk", frameCount: 1).Frames[0];
        frame.Events.Add(new AnimationFrameEvent { Name = "Step" });

        ctx.AppCommands.SetFrameEvent(frame, 0, "Step", "");

        Assert.False(ctx.UndoManager.CanUndo);
    }

    [Fact]
    public void RemoveFrameEvent_RemovesOnlyThatEvent_UndoRestoresOrder()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var frame = TestHelpers.MakeChain(ctx.Acls, "Walk", frameCount: 1).Frames[0];
        frame.Events.Add(new AnimationFrameEvent { Name = "A" });
        frame.Events.Add(new AnimationFrameEvent { Name = "B" });

        ctx.AppCommands.RemoveFrameEvent(frame, 0);

        Assert.Equal("B", Assert.Single(frame.Events).Name);
        ctx.UndoManager.Undo();
        Assert.Equal(new[] { "A", "B" }, frame.Events.Select(e => e.Name));
    }
}

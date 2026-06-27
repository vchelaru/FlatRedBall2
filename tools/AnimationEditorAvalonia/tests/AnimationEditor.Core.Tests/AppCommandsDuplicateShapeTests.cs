using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using FlatRedBall2.Animation.Content;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// DuplicateShape mirrors DuplicateFrame/DuplicateChain for shapes (rect/circle):
/// it deep-clones the source into the same frame with a unique name, selects the
/// clone, and is undoable. This closes the gap where Ctrl+D worked for chains and
/// frames but not shapes, even though Copy/Paste handled all three.
/// </summary>
[Collection("SequentialSingletons")]
public class AppCommandsDuplicateShapeTests
{
    [Fact]
    public void DuplicateShape_Rectangle_AddsDeepCloneToSameFrameWithUniqueName()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var frame = TestHelpers.MakeChain(ctx.Acls, "Walk", 1).Frames[0];
        var rect = new AARectSave { Name = "HitBox", X = 1, Y = 2, ScaleX = 3, ScaleY = 4 };
        frame.ShapesSave!.Shapes.Add(rect);

        var copy = (AARectSave)ctx.AppCommands.DuplicateShape(rect)!;

        Assert.Equal(2, frame.ShapesSave!.AARectSaves.Count());
        Assert.NotSame(rect, copy);
        Assert.NotEqual(rect.Name, copy.Name);   // uniquely renamed
        Assert.Equal(3, copy.ScaleX);            // deep copy carried field values
        Assert.Same(copy, ctx.SelectedState.SelectedRectangle);
    }

    [Fact]
    public void DuplicateShape_Rectangle_IsUndoable()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var frame = TestHelpers.MakeChain(ctx.Acls, "Walk", 1).Frames[0];
        var rect = new AARectSave { Name = "HitBox" };
        frame.ShapesSave!.Shapes.Add(rect);

        ctx.AppCommands.DuplicateShape(rect);
        Assert.Equal(2, frame.ShapesSave!.AARectSaves.Count());

        ctx.UndoManager.Undo();
        Assert.Single(frame.ShapesSave!.AARectSaves);
    }

    [Fact]
    public void DuplicateShape_Circle_AddsDeepCloneToSameFrame()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var frame = TestHelpers.MakeChain(ctx.Acls, "Walk", 1).Frames[0];
        var circle = new CircleSave { Name = "Hurt", X = 1, Y = 2, Radius = 5 };
        frame.ShapesSave!.Shapes.Add(circle);

        var copy = (CircleSave)ctx.AppCommands.DuplicateShape(circle)!;

        Assert.Equal(2, frame.ShapesSave!.CircleSaves.Count());
        Assert.NotSame(circle, copy);
        Assert.NotEqual(circle.Name, copy.Name);
        Assert.Equal(5, copy.Radius);
        Assert.Same(copy, ctx.SelectedState.SelectedCircle);
    }
}

using System;
using System.Numerics;
using FlatRedBall2.Collision;
using FlatRedBall2.Input;
using Microsoft.Xna.Framework.Input;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Input;

public class CursorIsOverTests
{
    private static MouseState MouseAt(int x, int y) =>
        new MouseState(
            x: x, y: y, scrollWheel: 0,
            leftButton: ButtonState.Released,
            middleButton: ButtonState.Released,
            rightButton: ButtonState.Released,
            xButton1: ButtonState.Released,
            xButton2: ButtonState.Released);

    // No camera is registered, so WorldPosition falls back to ScreenPosition (mouse X/Y).

    [Fact]
    public void IsOver_Shape_CursorOverCircle_ReturnsTrue()
    {
        var cursor = new Cursor();
        cursor.Update(MouseAt(50, 50), TimeSpan.Zero);
        var circle = new Circle { X = 50f, Y = 50f, Radius = 10f };

        cursor.IsOver(circle).ShouldBeTrue();
    }

    [Fact]
    public void IsOver_Shape_CursorOutsideCircle_ReturnsFalse()
    {
        var cursor = new Cursor();
        cursor.Update(MouseAt(0, 0), TimeSpan.Zero);
        var circle = new Circle { X = 100f, Y = 100f, Radius = 10f };

        cursor.IsOver(circle).ShouldBeFalse();
    }

    [Fact]
    public void IsOver_Entity_CursorOverAnyShape_ReturnsTrue()
    {
        var cursor = new Cursor();
        var entity = new Entity { X = 0f, Y = 0f };
        // Shape A at offset (50, 0), Shape B at offset (-50, 0). Both radius 5.
        var a = new Circle { X = 50f, Y = 0f, Radius = 5f };
        var b = new Circle { X = -50f, Y = 0f, Radius = 5f };
        entity.Add(a);
        entity.Add(b);

        cursor.Update(MouseAt(50, 0), TimeSpan.Zero);
        cursor.IsOver(entity).ShouldBeTrue(); // over A

        cursor.Update(MouseAt(-50, 0), TimeSpan.Zero);
        cursor.IsOver(entity).ShouldBeTrue(); // over B
    }

    [Fact]
    public void IsOver_Entity_CursorOutsideAllShapes_ReturnsFalse()
    {
        var cursor = new Cursor();
        var entity = new Entity();
        entity.Add(new Circle { X = 50f, Y = 0f, Radius = 5f });
        entity.Add(new Circle { X = -50f, Y = 0f, Radius = 5f });

        cursor.Update(MouseAt(0, 0), TimeSpan.Zero);
        cursor.IsOver(entity).ShouldBeFalse();
    }
}

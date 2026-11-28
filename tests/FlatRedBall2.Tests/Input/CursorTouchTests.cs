using System;
using System.Collections.Generic;
using System.Numerics;
using FlatRedBall2.Input;
using FlatRedBall2.Rendering;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using Shouldly;
using Xunit;

using XnaVector2 = Microsoft.Xna.Framework.Vector2;

namespace FlatRedBall2.Tests.Input;

public class CursorTouchTests
{
    private static MouseState MouseAt(int x, int y) =>
        new MouseState(x, y, 0, ButtonState.Released, ButtonState.Released,
            ButtonState.Released, ButtonState.Released, ButtonState.Released);

    private static TouchCollection Touch(TouchLocationState state, float x, float y) =>
        new TouchCollection(new[] { new TouchLocation(1, state, new XnaVector2(x, y)) });

    private static readonly TouchCollection NoTouch = default;

    private static TimeSpan Sec(double s) => TimeSpan.FromSeconds(s);

    [Fact]
    public void ScreenPosition_FrameTouchEnds_ReportsLastTouchPosition()
    {
        var cursor = new Cursor();
        cursor.Update(MouseAt(5, 5), Touch(TouchLocationState.Pressed, 300, 200), Sec(0));
        cursor.Update(MouseAt(5, 5), Touch(TouchLocationState.Moved, 400, 250), Sec(0.01));

        cursor.Update(MouseAt(5, 5), Touch(TouchLocationState.Released, 400, 250), Sec(0.02));

        cursor.PrimaryDown.ShouldBeFalse();
        cursor.ScreenPosition.ShouldBe(new Vector2(400, 250));
    }

    [Fact]
    public void ScreenPosition_MouseMovedDuringTouch_StillReportsTouchAfterRelease()
    {
        // Browsers can synthesize mouse events from a touch; mouse movement that happened while
        // the finger was down must not take the position over once the finger lifts.
        var cursor = new Cursor();
        cursor.Update(MouseAt(5, 5), Touch(TouchLocationState.Pressed, 300, 200), Sec(0));
        cursor.Update(MouseAt(300, 200), Touch(TouchLocationState.Moved, 400, 250), Sec(0.01));

        cursor.Update(MouseAt(300, 200), Touch(TouchLocationState.Released, 400, 250), Sec(0.02));
        cursor.Update(MouseAt(300, 200), NoTouch, Sec(0.03));

        cursor.ScreenPosition.ShouldBe(new Vector2(400, 250));
    }

    [Fact]
    public void ScreenPosition_MouseMovesAfterTouchEnds_ReportsMousePosition()
    {
        var cursor = new Cursor();
        cursor.Update(MouseAt(5, 5), Touch(TouchLocationState.Pressed, 300, 200), Sec(0));
        cursor.Update(MouseAt(5, 5), Touch(TouchLocationState.Released, 300, 200), Sec(0.01));

        cursor.Update(MouseAt(50, 60), NoTouch, Sec(0.02));

        cursor.ScreenPosition.ShouldBe(new Vector2(50, 60));
    }

    [Fact]
    public void WorldPosition_FrameTouchEnds_ProjectsLastTouchPosition()
    {
        // 1280x720 camera centered at the world origin: window pixel (640, 360) is world (0, 0),
        // and (740, 260) is 100 px right and 100 px up (Y+ up) of it.
        var camera = new Camera { NormalizedViewport = new NormalizedRectangle(0f, 0f, 1f, 1f) };
        camera.ApplyToHostRect(new Viewport(0, 0, 1280, 720), 720);
        var cursor = new Cursor();
        cursor.SetCameras(new List<Camera> { camera });
        cursor.Update(MouseAt(5, 5), Touch(TouchLocationState.Pressed, 740, 260), Sec(0));

        cursor.Update(MouseAt(5, 5), Touch(TouchLocationState.Released, 740, 260), Sec(0.01));

        cursor.WorldPosition.X.ShouldBe(100f, tolerance: 1f);
        cursor.WorldPosition.Y.ShouldBe(100f, tolerance: 1f);
    }
}

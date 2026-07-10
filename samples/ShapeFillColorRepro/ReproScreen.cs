using FlatRedBall2;
using FlatRedBall2.Collision;
using Microsoft.Xna.Framework;

namespace ShapeFillColorRepro;

// Manual repro for issue #663: on macOS DesktopGL the FIRST filled Apos.Shapes shape of a session
// renders the wrong color, because Apos.Shapes leaves sampler unit 0 unbound for pure filled shapes
// and the macOS GL driver substitutes a "zero texture".
//
// The window shows a single centered rectangle — the only filled shape drawn, so it IS that first
// shape. Just look at its color:
//     black -> fixed
//     blue  -> bug (run with FRB2_DISABLE_FILL_PRIME=1 to see it on an affected Mac)
public class ReproScreen : Screen
{
    public override void CustomInitialize()
    {
        // Light-gray clear so both a black (fixed) and a blue (bug) rectangle stand out clearly.
        Camera.BackgroundColor = Color.LightGray;

        var rectangle = new AARect
        {
            X = 0,            // centered in the default view
            Y = 0,
            Width = 400,
            Height = 250,
            Color = Color.Black,
            IsFilled = true,
            IsVisible = true, // AARect defaults to invisible — it is primarily a collision shape.
        };
        Add(rectangle);
    }
}

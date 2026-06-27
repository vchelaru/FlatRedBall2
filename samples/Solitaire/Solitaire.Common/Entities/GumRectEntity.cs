using FlatRedBall2;
using Gum.GueDeriving;
using RenderingLibrary.Graphics;

namespace Solitaire.Entities;

// Minimal repro for the entity-attached Gum visual positioning bug:
// an Entity that owns a RectangleRuntime via Entity.Add(GraphicalUiElement).
public class GumRectEntity : Entity
{
    public override void CustomInitialize()
    {
        var rect = new RectangleRuntime
        {
            Width = 40,
            Height = 40,
            XOrigin = HorizontalAlignment.Center,
            YOrigin = VerticalAlignment.Center,
            IsFilled = true,
            FillRed = 255,
            FillGreen = 0,
            FillBlue = 0,
        };
        Add(rect);
    }
}

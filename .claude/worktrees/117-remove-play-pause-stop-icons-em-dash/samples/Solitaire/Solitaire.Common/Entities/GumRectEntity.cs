using FlatRedBall2;
using MonoGameGum.GueDeriving;
using RenderingLibrary.Graphics;

namespace Solitaire.Entities;

// Minimal repro for the entity-attached Gum visual positioning bug:
// an Entity that owns a ColoredRectangleRuntime via Entity.Add(GraphicalUiElement).
public class GumRectEntity : Entity
{
    public override void CustomInitialize()
    {
        var rect = new ColoredRectangleRuntime
        {
            Width = 40,
            Height = 40,
            XOrigin = HorizontalAlignment.Center,
            YOrigin = VerticalAlignment.Center,
            Red = 255,
            Green = 0,
            Blue = 0,
        };
        Add(rect);
    }
}

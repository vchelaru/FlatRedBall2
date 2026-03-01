using Microsoft.Xna.Framework;

namespace FlatRedBall2.Diagnostics;

public class DebugRenderer
{
    // TODO: Implement debug drawing using a primitive renderer (e.g. Apos.Shapes NuGet).
    // All methods are currently no-ops. See design/TODOS.md

    public bool IsEnabled { get; set; }

    public void DrawCircle(Vector2 center, float radius, Color color)
    {
        // TODO: Draw debug circle outline
    }

    public void DrawRectangle(float x, float y, float width, float height, Color color)
    {
        // TODO: Draw debug rectangle outline
    }

    public void DrawLine(Vector2 start, Vector2 end, Color color)
    {
        // TODO: Draw debug line
    }

    public void DrawText(Vector2 position, string text, Color color)
    {
        // TODO: Draw debug text (requires SpriteFont)
    }
}

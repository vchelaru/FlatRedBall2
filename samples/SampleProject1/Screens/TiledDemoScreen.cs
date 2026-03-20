using FlatRedBall2;
using FlatRedBall2.Tiled;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Tiled;

namespace SampleProject1.Screens;

public class TiledDemoScreen : Screen
{
    public override void CustomInitialize()
    {
        Camera.BackgroundColor = new Color(20, 30, 20);

        var tiledMap = ContentManager.Load<TiledMap>("Tiled/OverworldTopDownA");

        // Place map so its center aligns with the world origin.
        float mapX = -tiledMap.WidthInPixels / 2f;
        float mapY = tiledMap.HeightInPixels / 2f;

        foreach (var layer in tiledMap.TileLayers)
        {
            var renderable = new TiledMapLayerRenderable(tiledMap, layer)
            {
                X = mapX,
                Y = mapY,
            };
            Add(renderable);
        }

        var panel = new Panel();
        panel.Dock(Dock.Fill);
        var hint = new Label { Text = "Tiled map — OverworldTopDownA" };
        hint.Anchor(Anchor.TopLeft);
        hint.X = 10;
        hint.Y = 10;
        panel.AddChild(hint);
        Add(panel);
    }
}

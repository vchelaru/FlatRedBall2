using FlatRedBall2;
using FlatRedBall2.Tiled;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Tilemaps;
using MonoGame.Extended.Tilemaps.Tiled;

namespace SampleProject1.Screens;

public class TiledDemoScreen : Screen
{
    public override void CustomInitialize()
    {
        Camera.BackgroundColor = new Color(20, 30, 20);

        var parser = new TiledTmxParser();
        var tilemap = parser.ParseFromFile("Content/Tiled/OverworldTopDownA.tmx", Engine.GraphicsDevice);

        // Place map so its center aligns with the world origin.
        float mapX = -(float)tilemap.WorldBounds.Width / 2f;
        float mapY = (float)tilemap.WorldBounds.Height / 2f;

        foreach (var layer in tilemap.Layers)
        {
            if (layer is TilemapTileLayer tileLayer)
            {
                var renderable = new TileMapLayerRenderable(tilemap, tileLayer)
                {
                    X = mapX,
                    Y = mapY,
                };
                Add(renderable);

                // Generate visible collision overlay from tiles with class "SolidCollision".
                if (tileLayer.Name == "GameplayLayer")
                {
                    var solidCollision = TileMapCollisionGenerator.GenerateFromClass(
                        tilemap, tileLayer, "SolidCollision", mapX, mapY);
                    solidCollision.IsVisible = true;
                    Add(solidCollision);
                }
            }
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

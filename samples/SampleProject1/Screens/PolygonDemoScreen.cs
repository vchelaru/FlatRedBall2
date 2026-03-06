using System;
using FlatRedBall2;
using NVec2 = System.Numerics.Vector2;
using FlatRedBall2.Collision;
using FlatRedBall2.Math;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using SampleProject1.Entities;

namespace SampleProject1.Screens;

public class PolygonDemoScreen : Screen
{
    private Factory<TopDownPlayer> _playerFactory = null!;
    private Factory<PolygonObstacle> _obstacleFactory = null!;
    private Factory<SightLine> _sightLineFactory = null!;

    public override void CustomInitialize()
    {
        Camera.BackgroundColor = new Color(18, 18, 28);

        _playerFactory = new Factory<TopDownPlayer>(this);
        _obstacleFactory = new Factory<PolygonObstacle>(this);
        _sightLineFactory = new Factory<SightLine>(this);

        BuildObstacles();

        var player = _playerFactory.Create();
        player.X = 0f;
        player.Y = 0f;

        var sightLine = _sightLineFactory.Create();
        sightLine.Player = player;
        sightLine.Obstacles = _obstacleFactory;

        AddCollisionRelationship(_playerFactory, _obstacleFactory)
            .MoveFirstOnCollision();

        SetupHud();
    }

    private void BuildObstacles()
    {
        // Triangle — top-left (filled)
        AddObstacle(Polygon.FromPoints(new[]
        {
            new NVec2(-45f, -40f),
            new NVec2( 45f, -40f),
            new NVec2(  0f,  50f),
        }), -330f, 160f, new Color(220, 80, 80, 255), filled: true,
            rotVelocity: Angle.FromDegrees(60f));

        // Regular pentagon — top-right (filled)
        AddObstacle(Polygon.FromPoints(RegularPolygon(5, 55f)),
            320f, 160f, new Color(80, 160, 220, 255), filled: true,
            rotVelocity: Angle.FromDegrees(-45f));

        // Wide plank — top-center (outline only)
        AddObstacle(Polygon.CreateRectangle(160f, 28f),
            0f, 220f, new Color(200, 170, 60, 255), filled: false,
            rotVelocity: Angle.FromDegrees(30f));

        // Regular hexagon — left side (filled)
        AddObstacle(Polygon.FromPoints(RegularPolygon(6, 48f)),
            -340f, -40f, new Color(130, 220, 130, 255), filled: true,
            rotVelocity: Angle.FromDegrees(-80f));

        // Diamond — right side (filled)
        AddObstacle(Polygon.FromPoints(new[]
        {
            new NVec2(  0f, -55f),
            new NVec2( 38f,   0f),
            new NVec2(  0f,  55f),
            new NVec2(-38f,   0f),
        }), 340f, -40f, new Color(200, 100, 220, 255), filled: true,
            rotVelocity: Angle.FromDegrees(90f));

        // Irregular hexagon — bottom-center (filled)
        AddObstacle(Polygon.FromPoints(new[]
        {
            new NVec2(-50f, -15f),
            new NVec2(-20f, -50f),
            new NVec2( 30f, -42f),
            new NVec2( 55f,  10f),
            new NVec2( 18f,  50f),
            new NVec2(-40f,  30f),
        }), 0f, -190f, new Color(220, 140, 60, 255), filled: true,
            rotVelocity: Angle.FromDegrees(-50f));

        // Thin bar — bottom-left (outline only)
        AddObstacle(Polygon.CreateRectangle(120f, 20f),
            -290f, -180f, new Color(60, 200, 200, 255), filled: false,
            rotVelocity: Angle.FromDegrees(70f));

        // Regular heptagon — bottom-right (filled)
        AddObstacle(Polygon.FromPoints(RegularPolygon(7, 48f)),
            290f, -190f, new Color(220, 80, 140, 255), filled: true,
            rotVelocity: Angle.FromDegrees(-35f));
    }

    private void AddObstacle(Polygon poly, float x, float y, Color color,
        bool filled = true, Angle rotVelocity = default)
    {
        var obstacle = _obstacleFactory.Create();
        obstacle.X = x;
        obstacle.Y = y;
        obstacle.RotationVelocity = rotVelocity;

        poly.Color = color;
        poly.IsFilled = filled;
        poly.OutlineThickness = 3f;
        poly.Visible = true;
        obstacle.SetPolygon(poly);
    }

    private static NVec2[] RegularPolygon(int sides, float radius)
    {
        var pts = new NVec2[sides];
        for (int i = 0; i < sides; i++)
        {
            float angle = MathF.PI * 2f * i / sides - MathF.PI / 2f;
            pts[i] = new NVec2(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius);
        }
        return pts;
    }

    private void SetupHud()
    {
        var panel = new Panel();
        panel.Dock(Dock.Fill);

        var hint = new Label { Text = "Arrow Keys / WASD to move  |  Filled, outlined, and mixed polygons" };
        hint.Anchor(Anchor.TopLeft);
        hint.X = 10;
        hint.Y = 10;
        panel.AddChild(hint);

        Add(panel);
    }
}

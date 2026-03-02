using FlatRedBall2;
using FlatRedBall2.Collision;
using Microsoft.Xna.Framework;
using PlatformerSample.Entities;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace PlatformerSample.Screens;

public class PlatformerScreen : Screen
{
    private Factory<Player> _playerFactory = null!;
    private Factory<Platform> _platformFactory = null!;

    private Player _player = null!;

    public override void CustomInitialize()
    {
        Camera.BackgroundColor = new Color(20, 20, 40);

        _playerFactory = new Factory<Player>(this);
        _platformFactory = new Factory<Platform>(this);

        SpawnWorld();
        SpawnPlayer();
        SetupCollision();
    }

    private void SpawnWorld()
    {
        // Ground — wide floor, only pushes up so the player glides across without snagging on edges
        var ground = _platformFactory.Create();
        ground.X = 0f;
        ground.Y = -280f;
        ground.Rectangle.Width = 1200f;
        ground.Rectangle.Height = 40f;
        ground.Rectangle.Color = new XnaColor(100, 130, 60, 255);
        ground.Rectangle.RepositionDirections = RepositionDirections.Up;

        // Left wall
        var leftWall = _platformFactory.Create();
        leftWall.X = -620f;
        leftWall.Y = 0f;
        leftWall.Rectangle.Width = 40f;
        leftWall.Rectangle.Height = 800f;
        leftWall.Rectangle.Color = new XnaColor(80, 80, 100, 255);

        // Right wall
        var rightWall = _platformFactory.Create();
        rightWall.X = 620f;
        rightWall.Y = 0f;
        rightWall.Rectangle.Width = 40f;
        rightWall.Rectangle.Height = 800f;
        rightWall.Rectangle.Color = new XnaColor(80, 80, 100, 255);

        // Raised platforms — solid on all sides so the player can stand on top, land from any angle
        SpawnPlatform(-300f, -140f, 220f);
        SpawnPlatform(200f,  -80f,  180f);
        SpawnPlatform(-80f,   20f,  160f);
        SpawnPlatform(320f,   80f,  200f);
        SpawnPlatform( 60f,  170f,  140f);
        SpawnPlatform(-340f, 180f,  180f);
    }

    private void SpawnPlatform(float x, float y, float width)
    {
        var platform = _platformFactory.Create();
        platform.X = x;
        platform.Y = y;
        platform.Rectangle.Width = width;
    }

    private void SpawnPlayer()
    {
        _player = _playerFactory.Create();
        _player.X = 0f;
        _player.Y = -200f;
    }

    private void SetupCollision()
    {
        // BounceOnCollision with elasticity 0 separates the player (populating LastReposition
        // for ground detection) and zeroes the velocity component into the surface, so ceiling
        // hits kill upward velocity and wall hits kill horizontal velocity.
        AddCollisionRelationship<Player, Platform>(_playerFactory, _platformFactory)
            .BounceOnCollision(firstMass: 0f, secondMass: 1f, elasticity: 0f);
    }

    public override void CustomActivity(FrameTime time)
    {
        // Camera smoothly follows the player
        Camera.X = _player.X;
        Camera.Y = _player.Y;
    }
}

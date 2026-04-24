using FlatRedBall2;
using FlatRedBall2.Collision;
using FlatRedBall2.Entities;
using FlatRedBall2.Math;
using FlatRedBall2.Tiled;
using PlatformKing.Entities;

namespace PlatformKing.Screens;

public class GameScreen : Screen
{
    public int LevelIndex { get; set; }

    private Factory<Player> _playerFactory = null!;
    private Factory<Box> _boxFactory = null!;
    private Factory<Enemy> _enemyFactory = null!;
    private Factory<Door> _doorFactory = null!;
    private Factory<CameraControllingEntity> _cameraFactory = null!;

    private TileShapeCollection _solid = null!;
    private TileShapeCollection _jumpThrough = null!;
    private TileShapeCollection _ladders = null!;
    private TileShapeCollection _water = null!;
    private TileShapeCollection _deathTiles = null!;

    public override void CustomInitialize()
    {
        WatchContentDirectory("Content", _ => RestartScreen(RestartMode.HotReload));

        // Load map.
        string mapPath = LevelIndex == 0
            ? "Content/Tiled/Level1.tmx"
            : "Content/Tiled/Level2.tmx";

        var map = new TileMap(mapPath, Engine.GraphicsDevice);
        map.CenterOn(0f, 0f);
        Add(map);

        // Generate collision layers.
        _solid = map.GenerateCollisionFromClass("SolidCollision");
        _jumpThrough = map.GenerateCollisionFromClass("JumpThroughCollision");
        _ladders = map.GenerateCollisionFromClass("Ladder");
        _water = map.GenerateCollisionFromClass("Water");
        _deathTiles = map.GenerateCollisionFromClass("Death");

        Add(_solid);
        Add(_jumpThrough);
        Add(_ladders);
        Add(_water);
        Add(_deathTiles);

        // Create factories.
        _playerFactory = new Factory<Player>(this);
        _boxFactory = new Factory<Box>(this);
        _enemyFactory = new Factory<Enemy>(this);
        _doorFactory = new Factory<Door>(this);
        _boxFactory.IsSolidGrid = true;

        // Spawn entities from object layers.
        map.CreateEntities("BreakableCollision", _boxFactory);
        var enemies = map.CreateEntities("EnemyFlag", _enemyFactory, Origin.BottomCenter);
        foreach (var enemy in enemies)
        {
            enemy.SolidCollision = _solid;
            enemy.JumpThroughCollision = _jumpThrough;
        }
        map.CreateEntities("Door", _doorFactory, Origin.BottomCenter);

        var players = map.CreateEntities("PlayerFlag", _playerFactory, Origin.BottomCenter);
        var player = players[0];
        player.Ladders = _ladders;
        player.WaterZones = _water;

        _cameraFactory = new Factory<CameraControllingEntity>(this);
        var cam = _cameraFactory.Create();
        cam.Target = player;
        cam.Map = new BoundsRectangle(map.X + map.Width / 2f, map.Y - map.Height / 2f, map.Width, map.Height);
        cam.TargetApproachStyle = TargetApproachStyle.Smooth;
        cam.TargetApproachCoefficient = 8f;

        // Collision relationships.
        var playerVsSolid = AddCollisionRelationship(_playerFactory, _solid);
        playerVsSolid.SlopeMode = SlopeCollisionMode.PlatformerFloor;
        playerVsSolid.BounceFirstOnCollision(elasticity: 0f);

        var playerVsJumpThrough = AddCollisionRelationship(_playerFactory, _jumpThrough);
        playerVsJumpThrough.OneWayDirection = OneWayDirection.Up;
        playerVsJumpThrough.AllowDropThrough = true;
        playerVsJumpThrough.BounceFirstOnCollision(elasticity: 0f);

        var playerVsBox = AddCollisionRelationship<Player, Box>(_playerFactory, _boxFactory);
        playerVsBox.BounceFirstOnCollision(elasticity: 0f);
        playerVsBox.CollisionOccurred += (p, box) =>
        {
            // Destroy box when player stomps it (downward velocity, pushed upward).
            if (p.VelocityYBeforeCollision < 0f && p.LastReposition.Y > 0f)
                box.Destroy();
        };

        var enemyVsSolid = AddCollisionRelationship(_enemyFactory, _solid);
        enemyVsSolid.BounceFirstOnCollision(elasticity: 0f);

        var enemyVsJumpThrough = AddCollisionRelationship(_enemyFactory, _jumpThrough);
        enemyVsJumpThrough.OneWayDirection = OneWayDirection.Up;
        enemyVsJumpThrough.BounceFirstOnCollision(elasticity: 0f);

        AddCollisionRelationship<Player, Enemy>(_playerFactory, _enemyFactory)
            .CollisionOccurred += (_, _) => RestartScreen();

        AddCollisionRelationship(_playerFactory, _deathTiles)
            .CollisionOccurred += (_, _) => RestartScreen();

        AddCollisionRelationship<Player, Door>(_playerFactory, _doorFactory)
            .CollisionOccurred += (_, _) =>
            {
                int nextLevel = 1 - LevelIndex;
                MoveToScreen<GameScreen>(s => s.LevelIndex = nextLevel);
            };
    }

    public override void CustomActivity(FrameTime time)
    {
        // Water overlap is handled reactively in Player.CustomActivity via WaterZones.
        // All collision-event-based logic runs in the registered callbacks above.
    }
}

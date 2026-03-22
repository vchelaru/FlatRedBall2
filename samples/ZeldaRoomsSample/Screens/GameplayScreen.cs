using FlatRedBall2;
using FlatRedBall2.Collision;
using FlatRedBall2.Movement;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using MonoGameGum.GueDeriving;
using ZeldaRoomsSample.Entities;

namespace ZeldaRoomsSample.Screens;

public class GameplayScreen : Screen
{
    // --- Public state passed between rooms ---
    public int RoomIndex { get; set; } = 0;
    // Which rooms have been cleared (exit stays open on revisit)
    public HashSet<int> ClearedRooms { get; set; } = new();

    private const float CellSize = 64f;
    private const int Cols = 20;
    private const int Rows = 11;
    // Grid origin: top-left of cell (0,0) in world space (Y+ up, centered at origin)
    private static readonly float GridLeft  = -(Cols / 2f) * CellSize;
    private static readonly float GridTop   =  (Rows / 2f) * CellSize;

    private Factory<Wall> _wallFactory = null!;
    private Factory<Enemy> _enemyFactory = null!;
    private Factory<Player> _playerFactory = null!;
    private Factory<SwordHitbox> _swordFactory = null!;

    private Player _player = null!;
    private bool _transitioning = false;
    private bool _roomCleared = false;

    // HUD hearts
    private readonly ColoredRectangleRuntime[] _heartFills = new ColoredRectangleRuntime[3];

    public override void CustomInitialize()
    {
        Camera.BackgroundColor = new Color(20, 20, 30);

        _wallFactory   = new Factory<Wall>(this);
        _enemyFactory  = new Factory<Enemy>(this);
        _playerFactory = new Factory<Player>(this);
        _swordFactory  = new Factory<SwordHitbox>(this);

        LoadRoom(RoomIndex);
        BuildHud();

        AddCollisionRelationship<Player, Wall>(_playerFactory, _wallFactory)
            .MoveFirstOnCollision();

        AddCollisionRelationship<Enemy, Wall>(_enemyFactory, _wallFactory)
            .MoveFirstOnCollision();

        AddCollisionRelationship<Enemy>(_enemyFactory)
            .MoveBothOnCollision(1f, 1f);

        AddCollisionRelationship<SwordHitbox, Enemy>(_swordFactory, _enemyFactory)
            .CollisionOccurred += OnSwordHitEnemy;

        AddCollisionRelationship<Player, Enemy>(_playerFactory, _enemyFactory)
            .CollisionOccurred += OnPlayerTouchEnemy;
    }

    private void LoadRoom(int roomIndex)
    {
        var layout = RoomData.Rooms[roomIndex].Layout;
        _roomCleared = ClearedRooms.Contains(roomIndex);

        for (int row = 0; row < layout.Length; row++)
        {
            for (int col = 0; col < layout[row].Length; col++)
            {
                float x = GridLeft + col * CellSize + CellSize / 2f;
                float y = GridTop  - row * CellSize - CellSize / 2f;

                switch (layout[row][col])
                {
                    case '#':
                        var wall = _wallFactory.Create();
                        wall.X = x;
                        wall.Y = y;
                        break;
                    case 'E':
                        var enemy = _enemyFactory.Create();
                        enemy.X = x;
                        enemy.Y = y;
                        break;
                }
            }
        }

        // Spawn player at left-center of room
        _player = _playerFactory.Create();
        _player.X = GridLeft + CellSize * 1.5f;
        _player.Y = 0f;
        _player.AttackPressed += OnPlayerAttack;
        _player.Died += OnPlayerDied;
    }

    private void BuildHud()
    {
        const float heartSize = 28f;
        const float gap = 8f;
        const float startX = 20f;
        const float y = 20f;

        for (int i = 0; i < 3; i++)
        {
            float x = startX + i * (heartSize + gap);

            var outline = new ColoredRectangleRuntime { Width = heartSize, Height = heartSize, Color = Color.White };
            outline.X = x;
            outline.Y = y;
            Add(outline);

            var fill = new ColoredRectangleRuntime { Width = heartSize - 4f, Height = heartSize - 4f, Color = Color.Red };
            fill.X = x + 2f;
            fill.Y = y + 2f;
            Add(fill);
            _heartFills[i] = fill;
        }

        UpdateHud();
    }

    private void UpdateHud()
    {
        for (int i = 0; i < 3; i++)
            _heartFills[i].Visible = i < _player.Hearts;
    }

    private void OnPlayerAttack()
    {
        _player.BeginAttack();

        var dir = _player.FacingDirection.ToVector2();
        var sword = _swordFactory.Create();
        sword.X = _player.X + dir.X * 44f;
        sword.Y = _player.Y + dir.Y * 44f;
        sword.Activate(0.25f);

        _ = EndAttackAfterDelay(0.25f);
    }

    private async System.Threading.Tasks.Task EndAttackAfterDelay(float seconds)
    {
        await Engine.Time.DelaySeconds(seconds, Token);
        _player?.EndAttack();
    }

    private void OnSwordHitEnemy(SwordHitbox sword, Enemy enemy)
    {
        enemy.Hit();
        sword.Destroy();
    }

    private void OnPlayerTouchEnemy(Player player, Enemy enemy)
    {
        var knockback = new Vector2(player.X - enemy.X, player.Y - enemy.Y);
        float len = knockback.Length();
        knockback = len < 0.001f ? new Vector2(1f, 0f) : knockback / len;
        player.TakeHit(knockback);
    }

    private void OnPlayerDied()
    {
        if (_transitioning) return;
        _transitioning = true;
        MoveToScreen<GameOverScreen>(s => s.Win = false);
    }

    private bool IsPlayerAtExit()
    {
        // Right edge of world grid
        float rightBoundary = GridLeft + Cols * CellSize;
        return _player.X > rightBoundary - CellSize;
    }

    private bool IsExitOpen()
    {
        return _roomCleared || _enemyFactory.Instances.Count == 0;
    }

    public override void CustomActivity(FrameTime time)
    {
        if (_transitioning) return;

        UpdateHud();

        if (!_roomCleared && _enemyFactory.Instances.Count == 0)
        {
            _roomCleared = true;
            ClearedRooms.Add(RoomIndex);
        }

        if (IsExitOpen() && IsPlayerAtExit())
        {
            _transitioning = true;

            int nextRoom = RoomIndex + 1;
            if (nextRoom >= RoomData.Rooms.Length)
            {
                MoveToScreen<GameOverScreen>(s => s.Win = true);
            }
            else
            {
                _ = DoRoomTransition(nextRoom);
            }
        }
    }

    private async System.Threading.Tasks.Task DoRoomTransition(int nextRoom)
    {
        // Slide camera one screen width to the right.
        // Use DelayFrames (not DelaySeconds) so it works without PauseThisScreen.
        // 60 frames ≈ 1 second at 60 fps.
        float targetX = Camera.X + Camera.TargetWidth;
        Camera.VelocityX = Camera.TargetWidth; // one screen per second
        await Engine.Time.DelayFrames(60);
        Camera.VelocityX = 0f;
        Camera.X = targetX;

        MoveToScreen<GameplayScreen>(s =>
        {
            s.RoomIndex    = nextRoom;
            s.ClearedRooms = ClearedRooms;
        });
    }
}

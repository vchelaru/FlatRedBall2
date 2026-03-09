using System.Collections.Generic;
using FlatRedBall2;
using FlatRedBall2.AI;
using FlatRedBall2.Collision;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ZombieSample.Entities;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace ZombieSample.Screens;

/// <summary>
/// Abstract base for all ZombieSample levels. Handles tile construction, node network
/// pathfinding, entity spawning, collision, and HUD.
/// Press <c>Tab</c> at runtime to toggle the node-network debug overlay.
/// </summary>
public abstract class GameScreen : Screen
{
    // ── Grid ─────────────────────────────────────────────────────────────────
    private const int GridCols = 80;
    private const int GridRows = 45;
    protected const float TileSize     = 16f;
    protected const float TilesOriginX = -640f;   // left edge of cell (0, 0)
    protected const float TilesOriginY = -360f;   // bottom edge of cell (0, 0)

    private static readonly XnaColor WallColor  = new(120, 100, 80, 255);
    private static readonly XnaColor DebugColor = new(0, 220, 220, 100);

    // ── Health bar ────────────────────────────────────────────────────────────
    private const float HealthBarLeft     = -620f;
    private const float HealthBarY        = 340f;
    private const float HealthBarMaxWidth = 200f;
    private const float HealthBarHeight   = 16f;

    // ── Fields ────────────────────────────────────────────────────────────────
    protected Factory<Player>   _playerFactory   = null!;
    protected Factory<Zombie>   _zombieFactory   = null!;
    protected Factory<GoalZone> _goalZoneFactory = null!;
    protected TileShapeCollection _tiles         = null!;

    private TileNodeNetwork _nodeNetwork = null!;
    private HashSet<(int, int)> _wallCells = new();

    private Player _player = null!;
    private AxisAlignedRectangle _healthBarBg = null!;
    private AxisAlignedRectangle _healthBarFg = null!;
    private bool _playerReachedGoal;

    // Debug visualization
    private readonly List<AxisAlignedRectangle> _debugRects = new();
    private bool _debugVisible;

    // ── Abstract level API ────────────────────────────────────────────────────

    /// <summary>Grid cell (col, row) where the player spawns. Row 0 = bottom.</summary>
    protected abstract (int Col, int Row) GetPlayerCell();

    /// <summary>Grid cell (col, row) where the goal zone is placed.</summary>
    protected abstract (int Col, int Row) GetGoalCell();

    /// <summary>Grid cells (col, row) where zombies spawn.</summary>
    protected abstract IEnumerable<(int Col, int Row)> GetZombieCells();

    /// <summary>
    /// Add interior walls. Border walls are added automatically.
    /// Use <see cref="AddWallTile"/> and <see cref="AddWallRect"/>.
    /// </summary>
    protected abstract void PopulateTiles(TileShapeCollection tiles);

    // ── Level progression ─────────────────────────────────────────────────────
    protected virtual void OnWin()  => MoveToScreen<WinScreen>();
    protected virtual void OnLose() => MoveToScreen<LoseScreen>();

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Converts a grid cell to world-space center.</summary>
    protected static Vector2 CellToWorld(int col, int row) =>
        new(TilesOriginX + col * TileSize + TileSize / 2f,
            TilesOriginY + row * TileSize + TileSize / 2f);

    /// <summary>Adds and colorizes a single wall tile, tracking the cell for the node network.</summary>
    protected void AddWallTile(TileShapeCollection tiles, int col, int row)
    {
        tiles.AddTileAtCell(col, row);
        var tile = tiles.GetTileAtCell(col, row);
        if (tile == null) return;
        tile.Color    = WallColor;
        tile.IsFilled = true;
        _wallCells.Add((col, row));
    }

    /// <summary>Adds a solid rectangular block of wall tiles.</summary>
    protected void AddWallRect(TileShapeCollection tiles, int col, int row, int width, int height)
    {
        for (int c = col; c < col + width; c++)
            for (int r = row; r < row + height; r++)
                AddWallTile(tiles, c, r);
    }

    // ── Init ──────────────────────────────────────────────────────────────────

    public override void CustomInitialize()
    {
        Camera.BackgroundColor = new XnaColor(15, 25, 15);

        _playerFactory   = new Factory<Player>(this);
        _zombieFactory   = new Factory<Zombie>(this);
        _goalZoneFactory = new Factory<GoalZone>(this);

        _tiles = new TileShapeCollection
        {
            X        = TilesOriginX,
            Y        = TilesOriginY,
            GridSize = TileSize,
        };

        AddBorderWalls();
        PopulateTiles(_tiles);
        Add(_tiles);
        _tiles.IsVisible = true;

        BuildNodeNetwork();
        SpawnEntities();
        SetupCollision();
        SetupHealthBar();
    }

    private void AddBorderWalls()
    {
        for (int c = 0; c < GridCols; c++)
        {
            AddWallTile(_tiles, c, 0);
            AddWallTile(_tiles, c, GridRows - 1);
        }
        for (int r = 1; r < GridRows - 1; r++)
        {
            AddWallTile(_tiles, 0, r);
            AddWallTile(_tiles, GridCols - 1, r);
        }
    }

    private void BuildNodeNetwork()
    {
        // Node centers align with tile centers: origin offset by half a tile.
        _nodeNetwork = new TileNodeNetwork(
            xOrigin:         TilesOriginX + TileSize / 2f,
            yOrigin:         TilesOriginY + TileSize / 2f,
            gridSpacing:     TileSize,
            xCount:          GridCols,
            yCount:          GridRows,
            directionalType: DirectionalType.Eight);

        _nodeNetwork.FillCompletely();

        foreach (var (col, row) in _wallCells)
            _nodeNetwork.RemoveAt(col, row);

        _nodeNetwork.EliminateCutCorners();
    }

    private void SpawnEntities()
    {
        var (pc, pr) = GetPlayerCell();
        var pw = CellToWorld(pc, pr);
        _player   = _playerFactory.Create();
        _player.X = pw.X;
        _player.Y = pw.Y;

        var (gc, gr) = GetGoalCell();
        var gw   = CellToWorld(gc, gr);
        var goal = _goalZoneFactory.Create();
        goal.X = gw.X;
        goal.Y = gw.Y;

        foreach (var (zc, zr) in GetZombieCells())
        {
            var zw     = CellToWorld(zc, zr);
            var zombie = _zombieFactory.Create();
            zombie.X   = zw.X;
            zombie.Y   = zw.Y;
            zombie.SetTarget(_player);
            zombie.SetNodeNetwork(_nodeNetwork);
        }
    }

    private void SetupCollision()
    {
        AddCollisionRelationship(_playerFactory, _tiles).MoveFirstOnCollision();
        AddCollisionRelationship(_zombieFactory, _tiles).MoveFirstOnCollision();
        AddCollisionRelationship<Zombie, Player>(_zombieFactory, _playerFactory)
            .CollisionOccurred += (_, player) => player.TryTakeDamage();
        AddCollisionRelationship<Player, GoalZone>(_playerFactory, _goalZoneFactory)
            .CollisionOccurred += (_, _) => _playerReachedGoal = true;
    }

    private void SetupHealthBar()
    {
        float bgCenterX = HealthBarLeft + HealthBarMaxWidth / 2f;
        _healthBarBg = new AxisAlignedRectangle
        {
            X = bgCenterX, Y = HealthBarY,
            Width = HealthBarMaxWidth + 6f, Height = HealthBarHeight + 6f,
            Color = new XnaColor(20, 20, 20, 200), IsFilled = true, IsVisible = true,
        };
        _healthBarFg = new AxisAlignedRectangle
        {
            X = bgCenterX, Y = HealthBarY,
            Width = HealthBarMaxWidth, Height = HealthBarHeight,
            Color = new XnaColor(200, 50, 50, 255), IsFilled = true, IsVisible = true,
        };
        Add(_healthBarBg);
        Add(_healthBarFg);
    }

    // ── Activity ──────────────────────────────────────────────────────────────

    public override void CustomActivity(FrameTime time)
    {
        if (Engine.Input.Keyboard.WasKeyPressed(Keys.Tab))
            ToggleNodeNetworkDebug();

        UpdateHealthBar();

        if (_playerReachedGoal) { OnWin(); return; }
        if (_player.DiedThisFrame) OnLose();
    }

    private void UpdateHealthBar()
    {
        float fraction     = (float)_player.Health / _player.MaxHealth;
        float currentWidth = HealthBarMaxWidth * fraction;
        _healthBarFg.Width = currentWidth;
        _healthBarFg.X     = HealthBarLeft + currentWidth / 2f;
    }

    // ── Debug visualization ───────────────────────────────────────────────────

    /// <summary>
    /// Shows or hides small cyan squares at every walkable node position.
    /// Toggled at runtime with <c>Tab</c>.
    /// </summary>
    public bool DebugShowNodeNetwork
    {
        get => _debugVisible;
        set
        {
            if (_debugVisible == value) return;
            _debugVisible = value;
            if (value) CreateDebugRects();
            else DestroyDebugRects();
        }
    }

    private void ToggleNodeNetworkDebug() => DebugShowNodeNetwork = !_debugVisible;

    private void CreateDebugRects()
    {
        // Apos.Shapes uses 32-bit index buffers when vertex count exceeds ~65k.
        // GraphicsProfile.Reach doesn't support 32-bit indices, so cap the shape
        // count to stay safely within 16-bit limits on Reach hardware.
        bool isReach = Engine.Game.GraphicsDevice.GraphicsProfile == GraphicsProfile.Reach;
        int remaining = isReach ? 1500 : int.MaxValue;

        for (int x = 0; x < GridCols && remaining > 0; x++)
        {
            for (int y = 0; y < GridRows && remaining > 0; y++)
            {
                var node = _nodeNetwork.NodeAt(x, y);
                if (node == null) continue;

                var rect = new AxisAlignedRectangle
                {
                    X         = node.Position.X,
                    Y         = node.Position.Y,
                    Width     = 5f,
                    Height    = 5f,
                    Color     = DebugColor,
                    IsFilled  = true,
                    IsVisible = true,
                };
                remaining--;
                Add(rect);
                _debugRects.Add(rect);
            }
        }
    }

    private void DestroyDebugRects()
    {
        foreach (var r in _debugRects)
        {
            Remove(r);
            r.Destroy();
        }
        _debugRects.Clear();
    }

    // ── Destroy ───────────────────────────────────────────────────────────────

    public override void CustomDestroy()
    {
        DestroyDebugRects();
        Remove(_healthBarBg); _healthBarBg.Destroy();
        Remove(_healthBarFg); _healthBarFg.Destroy();
    }
}

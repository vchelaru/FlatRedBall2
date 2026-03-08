using FlatRedBall2;
using FlatRedBall2.Collision;
using Microsoft.Xna.Framework;
using ZombieSample.Entities;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace ZombieSample.Screens;

/// <summary>
/// Abstract base for all Dead Run levels. Handles tile map construction, entity spawning,
/// collision wiring, and HUD. Concrete level subclasses supply the level layout via
/// <see cref="PopulateTiles"/>, <see cref="GetPlayerCell"/>, <see cref="GetGoalCell"/>,
/// and <see cref="GetZombieCells"/>.
/// </summary>
public abstract class GameScreen : Screen
{
    // ------------------------------------------------------------------ Grid constants
    // Window: 1280×720. Grid: 32 cols × 18 rows, 40px tiles.
    // Bottom-left corner of cell (0,0) in world space:

    private const int GridCols = 32;
    private const int GridRows = 18;

    protected const float TileSize      = 40f;
    protected const float TilesOriginX  = -640f;   // left edge of cell (0,0)
    protected const float TilesOriginY  = -360f;   // bottom edge of cell (0,0)

    private static readonly XnaColor WallColor = new(120, 100, 80, 255);

    // ------------------------------------------------------------------ Health bar layout

    private const float HealthBarLeft     = -620f;
    private const float HealthBarY        = 330f;
    private const float HealthBarMaxWidth = 200f;
    private const float HealthBarHeight   = 20f;

    // ------------------------------------------------------------------ Fields

    protected Factory<Player>   _playerFactory   = null!;
    protected Factory<Zombie>   _zombieFactory   = null!;
    protected Factory<GoalZone> _goalZoneFactory = null!;
    protected TileShapeCollection _tiles         = null!;

    private Player _player = null!;
    private AxisAlignedRectangle _healthBarBg = null!;
    private AxisAlignedRectangle _healthBarFg = null!;
    private bool _playerReachedGoal;

    // ------------------------------------------------------------------ Abstract level API

    /// <summary>Grid cell (col, row) where the player spawns. Row 0 = bottom, Row 17 = top.</summary>
    protected abstract (int Col, int Row) GetPlayerCell();

    /// <summary>Grid cell (col, row) where the goal zone is placed.</summary>
    protected abstract (int Col, int Row) GetGoalCell();

    /// <summary>Grid cells (col, row) where zombies spawn.</summary>
    protected abstract IEnumerable<(int Col, int Row)> GetZombieCells();

    /// <summary>
    /// Add interior walls to <paramref name="tiles"/>. Border walls are added automatically.
    /// Use <see cref="AddWallTile"/> and <see cref="AddWallRect"/> helpers.
    /// </summary>
    protected abstract void PopulateTiles(TileShapeCollection tiles);

    // ------------------------------------------------------------------ Level progression

    /// <summary>Called when the player reaches the goal. Default: go to WinScreen.</summary>
    protected virtual void OnWin()  => MoveToScreen<WinScreen>();

    /// <summary>Called when the player's health reaches zero. Default: go to LoseScreen.</summary>
    protected virtual void OnLose() => MoveToScreen<LoseScreen>();

    // ------------------------------------------------------------------ Helpers

    /// <summary>Converts a grid cell to its world-space center position.</summary>
    protected static Vector2 CellToWorld(int col, int row) =>
        new(TilesOriginX + col * TileSize + TileSize / 2f,
            TilesOriginY + row * TileSize + TileSize / 2f);

    /// <summary>Adds and colorizes a single wall tile.</summary>
    protected void AddWallTile(TileShapeCollection tiles, int col, int row)
    {
        tiles.AddTileAtCell(col, row);
        var tile = tiles.GetTileAtCell(col, row);
        if (tile == null) return;
        tile.Color    = WallColor;
        tile.IsFilled = true;
    }

    /// <summary>Adds and colorizes a solid rectangular block of wall tiles.</summary>
    protected void AddWallRect(TileShapeCollection tiles, int col, int row, int width, int height)
    {
        for (int c = col; c < col + width; c++)
            for (int r = row; r < row + height; r++)
                AddWallTile(tiles, c, r);
    }

    // ------------------------------------------------------------------ Init

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

        // Add tiles first (border + level interior), then register for rendering.
        // Tiles created before Screen.Add are captured by AllTiles in Add(TileShapeCollection).
        AddBorderWalls();
        PopulateTiles(_tiles);
        Add(_tiles);
        _tiles.IsVisible = true;

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
            var zw    = CellToWorld(zc, zr);
            var zombie = _zombieFactory.Create();
            zombie.X = zw.X;
            zombie.Y = zw.Y;
            zombie.SetTarget(_player);
        }
    }

    private void SetupCollision()
    {
        // Player vs walls — player gets pushed out, tiles stay fixed
        AddCollisionRelationship(_playerFactory, _tiles)
            .MoveFirstOnCollision();

        // Zombies vs walls — zombies slide along walls rather than clipping through
        AddCollisionRelationship(_zombieFactory, _tiles)
            .MoveFirstOnCollision();

        // Zombie contact → drain player health (invincibility handled inside Player)
        AddCollisionRelationship<Zombie, Player>(_zombieFactory, _playerFactory)
            .CollisionOccurred += (_, player) => player.TryTakeDamage();

        // Player reaches goal → flag for win transition
        AddCollisionRelationship<Player, GoalZone>(_playerFactory, _goalZoneFactory)
            .CollisionOccurred += (_, _) => _playerReachedGoal = true;
    }

    private void SetupHealthBar()
    {
        float bgCenterX = HealthBarLeft + HealthBarMaxWidth / 2f;

        _healthBarBg = new AxisAlignedRectangle
        {
            X        = bgCenterX,
            Y        = HealthBarY,
            Width    = HealthBarMaxWidth + 6f,
            Height   = HealthBarHeight + 6f,
            Color    = new XnaColor(20, 20, 20, 200),
            IsFilled = true,
            IsVisible  = true,
        };

        _healthBarFg = new AxisAlignedRectangle
        {
            X        = bgCenterX,
            Y        = HealthBarY,
            Width    = HealthBarMaxWidth,
            Height   = HealthBarHeight,
            Color    = new XnaColor(200, 50, 50, 255),
            IsFilled = true,
            IsVisible  = true,
        };

        Add(_healthBarBg);
        Add(_healthBarFg);
    }

    // ------------------------------------------------------------------ Activity

    public override void CustomActivity(FrameTime time)
    {
        UpdateHealthBar();

        if (_playerReachedGoal)
        {
            OnWin();
            return;
        }

        if (_player.DiedThisFrame)
            OnLose();
    }

    private void UpdateHealthBar()
    {
        float fraction     = (float)_player.Health / _player.MaxHealth;
        float currentWidth = HealthBarMaxWidth * fraction;
        _healthBarFg.Width = currentWidth;
        _healthBarFg.X     = HealthBarLeft + currentWidth / 2f;
    }

    // ------------------------------------------------------------------ Destroy

    public override void CustomDestroy()
    {
        Remove(_healthBarBg);
        _healthBarBg.Destroy();
        Remove(_healthBarFg);
        _healthBarFg.Destroy();
    }
}

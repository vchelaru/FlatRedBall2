using System;
using System.Collections;
using System.Collections.Generic;
using FlatRedBall2.Collision;

namespace FlatRedBall2;

/// <summary>Non-generic interface used by <see cref="FlatRedBallService"/> to destroy all factory instances on screen exit.</summary>
internal interface IFactory
{
    void DestroyAll();
    Axis? PartitionAxis { get; }
    void SortForPartition();
}

/// <summary>
/// Creates, tracks, and destroys entities of type <typeparamref name="T"/> for a single screen.
/// </summary>
/// <remarks>
/// <para>
/// <c>Factory&lt;T&gt;</c> is the standard way to create entities — use it even when you only need
/// one instance. It registers the entity with the engine, wires up the activity loop, and ensures
/// automatic cleanup when the screen exits.
/// </para>
/// <para>
/// Declare one factory per entity type as a field on your <see cref="Screen"/>, construct it in
/// <see cref="Screen.CustomInitialize"/>, and call <see cref="Create"/> to spawn instances:
/// <code>
/// private Factory&lt;Player&gt; _playerFactory = null!;
///
/// public override void CustomInitialize()
/// {
///     _playerFactory = new Factory&lt;Player&gt;(this);
///     var player = _playerFactory.Create();
/// }
/// </code>
/// </para>
/// </remarks>
public class Factory<T> : IEnumerable<T>, IReadOnlyList<T>, IFactory where T : Entity, new()
{
    private readonly Screen _screen;
    private readonly List<T> _instances = new();

    // IsSolidGrid state — only populated while IsSolidGrid = true.
    private readonly Dictionary<(int col, int row), T> _grid = new();
    private readonly Dictionary<T, (int col, int row)> _entityCells = new();
    private readonly HashSet<T> _gridMembers = new();
    private float? _cellWidth;
    private float? _cellHeight;
    private int _batchDepth;

    public Factory(Screen screen)
    {
        _screen = screen;
        screen.Engine.RegisterFactory(this);
    }

    /// <summary>
    /// When <c>true</c>, entities created by this factory are treated as cells of a regular grid
    /// of solid blocks (e.g., rows of destructible bricks). Each entity's first
    /// <see cref="AxisAlignedRectangle"/> child has its <c>RepositionDirections</c> maintained
    /// automatically so adjacent cells share suppressed interior faces — identical to
    /// <see cref="TileShapeCollection"/>'s seam-suppression, but for entity factories.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Cell size is inferred from the first entity added after the flag is set (body width and
    /// height). Subsequent entities must match — a mismatched body throws
    /// <see cref="InvalidOperationException"/>.
    /// </para>
    /// <para>
    /// The grid is indexed from the body's world-space position (cell origin at world 0, 0).
    /// When bulk-spawning via <c>TileMap.CreateEntities</c> the engine automatically wraps the
    /// spawn in <see cref="BeginGridBatch"/> so reposition-direction recomputation happens once
    /// at the end. For hand-authored grids, wrap the spawn loop in a <c>using</c> block around
    /// <see cref="BeginGridBatch"/> to avoid O(N) per-add recomputation.
    /// </para>
    /// <para>
    /// Default is <c>false</c> — factories that don't opt in behave exactly as before with zero
    /// overhead.
    /// </para>
    /// </remarks>
    public bool IsSolidGrid { get; set; }

    /// <summary>
    /// Begins a batch in which grid reposition-direction updates are suspended. Returns a
    /// disposable that resumes updates and performs a single full recompute on dispose. Nested
    /// batches are supported — only the outermost dispose flushes.
    /// </summary>
    public IDisposable BeginGridBatch()
    {
        _batchDepth++;
        return new GridBatch(this);
    }

    private sealed class GridBatch : IDisposable
    {
        private Factory<T>? _owner;
        public GridBatch(Factory<T> owner) { _owner = owner; }
        public void Dispose()
        {
            var o = _owner; if (o == null) return;
            _owner = null;
            o._batchDepth--;
            if (o._batchDepth == 0 && o.IsSolidGrid)
                o.FlushGrid();
        }
    }

    public IReadOnlyList<T> Instances => _instances;

    /// <summary>
    /// When set, this factory's entity list is sorted along the chosen axis once per frame before
    /// collision relationships run. Any <see cref="Collision.CollisionRelationship{A,B}"/> whose both
    /// lists are factories sharing the same non-null <see cref="PartitionAxis"/> will automatically use
    /// broad-phase culling — no extra setup needed.
    /// Set to <c>null</c> (default) to disable sorting and broad-phase for this factory.
    /// </summary>
    public Axis? PartitionAxis { get; set; }

    void IFactory.SortForPartition()
    {
        if (PartitionAxis == null) return;
        bool byX = PartitionAxis == Axis.X;
        // Insertion sort — O(n) on nearly-sorted data (entities move slowly relative to sort order).
        for (int i = 1; i < _instances.Count; i++)
        {
            var key = _instances[i];
            float keyVal = byX ? key.AbsoluteX : key.AbsoluteY;
            int j = i - 1;
            while (j >= 0)
            {
                float jVal = byX ? _instances[j].AbsoluteX : _instances[j].AbsoluteY;
                if (jVal <= keyVal) break;
                _instances[j + 1] = _instances[j];
                j--;
            }
            _instances[j + 1] = key;
        }
    }

    // IReadOnlyList<T> — allows SelfCollisionRelationship to iterate by index without GetEnumerator.
    public int Count => _instances.Count;
    public T this[int index] => _instances[index];

    public T Create()
    {
        var entity = new T();
        entity.Engine = _screen.Engine;
        _screen.AddEntity(entity);
        _instances.Add(entity);
        entity._onDestroy = () =>
        {
            _instances.Remove(entity);
            _screen.RemoveEntity(entity);
            if (IsSolidGrid && _gridMembers.Remove(entity))
                OnGridEntityDestroyed(entity);
        };
        if (_screen.Layer != null)
            entity.Layer = _screen.Layer;
        entity.CustomInitialize();
        if (IsSolidGrid)
        {
            _gridMembers.Add(entity);
            if (_batchDepth == 0)
                IndexEntity(entity);
        }
        return entity;
    }

    private static AxisAlignedRectangle FindBody(T entity)
    {
        foreach (var child in entity.Children)
        {
            if (child is AxisAlignedRectangle rect)
                return rect;
        }
        throw new InvalidOperationException(
            $"Factory<{typeof(T).Name}>.IsSolidGrid requires each entity to have an " +
            $"AxisAlignedRectangle child, but none was found.");
    }

    private (int col, int row) CellOf(AxisAlignedRectangle body)
    {
        float cw = _cellWidth!.Value;
        float ch = _cellHeight!.Value;
        // Floor, not Round. Round uses banker's rounding (ties-to-even), so bodies at half-cell
        // offsets (e.g. X = 8, 24, 40 with cell width 16) collapse to the same cell index.
        // Floor is stable under any consistent sub-cell offset — the cell origin becomes the
        // implicit offset of the first entity, and every subsequent body spaced cellWidth apart
        // yields a distinct integer index.
        int col = (int)MathF.Floor(body.AbsoluteX / cw);
        int row = (int)MathF.Floor(body.AbsoluteY / ch);
        return (col, row);
    }

    private void IndexEntity(T entity)
    {
        var body = FindBody(entity);
        if (_cellWidth == null)
        {
            _cellWidth = body.Width;
            _cellHeight = body.Height;
        }
        else if (!FloatsEqual(body.Width, _cellWidth.Value) ||
                 !FloatsEqual(body.Height, _cellHeight.Value))
        {
            throw new InvalidOperationException(
                $"Factory<{typeof(T).Name}>.IsSolidGrid requires all entities to share the same " +
                $"cell size. Expected {_cellWidth.Value}x{_cellHeight.Value} but got " +
                $"{body.Width}x{body.Height}.");
        }

        var cell = CellOf(body);
        _grid[cell] = entity;
        _entityCells[entity] = cell;
        UpdateCellDirections(cell);
        UpdateCellDirections((cell.col - 1, cell.row));
        UpdateCellDirections((cell.col + 1, cell.row));
        UpdateCellDirections((cell.col, cell.row - 1));
        UpdateCellDirections((cell.col, cell.row + 1));
    }

    private void OnGridEntityDestroyed(T entity)
    {
        // Entity.Destroy clears child shapes before firing _onDestroy, so we cannot read the body
        // here — we rely on the cell index recorded when the entity was added to the grid.
        if (!_entityCells.TryGetValue(entity, out var cell))
            return;
        _entityCells.Remove(entity);

        if (_grid.TryGetValue(cell, out var stored) && ReferenceEquals(stored, entity))
            _grid.Remove(cell);

        if (_batchDepth > 0) return;

        UpdateCellDirections((cell.col - 1, cell.row));
        UpdateCellDirections((cell.col + 1, cell.row));
        UpdateCellDirections((cell.col, cell.row - 1));
        UpdateCellDirections((cell.col, cell.row + 1));
    }

    private void UpdateCellDirections((int col, int row) cell)
    {
        if (!_grid.TryGetValue(cell, out var entity)) return;
        var body = FindBody(entity);
        var dirs = RepositionDirections.All;
        if (_grid.ContainsKey((cell.col - 1, cell.row))) dirs &= ~RepositionDirections.Left;
        if (_grid.ContainsKey((cell.col + 1, cell.row))) dirs &= ~RepositionDirections.Right;
        if (_grid.ContainsKey((cell.col, cell.row - 1))) dirs &= ~RepositionDirections.Down;
        if (_grid.ContainsKey((cell.col, cell.row + 1))) dirs &= ~RepositionDirections.Up;
        body.RepositionDirections = dirs;
    }

    // Flushes all pending members into _grid and recomputes RepositionDirections in one pass.
    // Rebuilds from scratch so membership matches the authoritative _gridMembers set.
    private void FlushGrid()
    {
        _grid.Clear();
        _entityCells.Clear();
        foreach (var entity in _gridMembers)
        {
            var body = FindBody(entity);
            if (_cellWidth == null)
            {
                _cellWidth = body.Width;
                _cellHeight = body.Height;
            }
            else if (!FloatsEqual(body.Width, _cellWidth.Value) ||
                     !FloatsEqual(body.Height, _cellHeight.Value))
            {
                throw new InvalidOperationException(
                    $"Factory<{typeof(T).Name}>.IsSolidGrid requires all entities to share the same " +
                    $"cell size. Expected {_cellWidth.Value}x{_cellHeight.Value} but got " +
                    $"{body.Width}x{body.Height}.");
            }
            var cell = CellOf(body);
            _grid[cell] = entity;
            _entityCells[entity] = cell;
        }

        foreach (var cell in _grid.Keys)
            UpdateCellDirections(cell);
    }

    private static bool FloatsEqual(float a, float b) => MathF.Abs(a - b) < 1e-4f;

    /// <summary>Destroys the entity. Equivalent to calling <see cref="Entity.Destroy"/> directly.</summary>
    public void Destroy(T instance) => instance.Destroy();

    public void DestroyAll()
    {
        foreach (var instance in new List<T>(_instances))
            Destroy(instance);
    }

    /// <summary>
    /// Enumerates a snapshot of current instances. Safe to call <see cref="Destroy"/> on any
    /// instance during enumeration — the live list can be modified without affecting the iterator.
    /// </summary>
    public IEnumerator<T> GetEnumerator() => new List<T>(_instances).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

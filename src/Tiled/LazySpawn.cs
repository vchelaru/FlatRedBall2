using System;
using System.Collections.Generic;

namespace FlatRedBall2.Tiled;

/// <summary>
/// SNES-style lazy-spawn behavior for <see cref="Factory{T}"/> when spawning from a
/// <see cref="TileMap"/> placeholder. Consulted only by the TileMap-driven spawn path —
/// direct <see cref="Factory{T}.Create()"/> calls are unaffected.
/// </summary>
public enum LazySpawnMode
{
    /// <summary>Eager spawn at TMX load (default, current behavior).</summary>
    Disabled = 0,

    /// <summary>
    /// Spawn the first time the camera's activation rect reaches the placement, then never
    /// again — even after the entity is destroyed and the camera returns. Use for unique pickups
    /// (key, boss-room trigger) and any "you only get this once" content.
    /// </summary>
    OneShot,

    /// <summary>
    /// Spawn when the activation rect reaches the placement. After the spawned entity is
    /// destroyed AND the activation rect has fully left the placement's region, the record
    /// re-arms and may spawn again on a subsequent re-entry. Mirrors Super Mario World's
    /// off-screen-respawn behavior. While the entity is alive, the activation rect leaving
    /// has no effect — the entity continues to live independently.
    /// </summary>
    Reloadable,
}

/// <summary>
/// Tracks <see cref="LazySpawnRecord{T}"/>s for a <see cref="TileMap"/> and ticks them each
/// frame against an activation rect. Records are inert until added; the manager performs no
/// allocation on the per-frame hotpath.
/// </summary>
public sealed class LazySpawnManager
{
    private readonly List<ILazySpawnRecord> _records = new();

    /// <summary>
    /// Registers a lazy-spawn record. The factory's current <see cref="Factory{T}.LazySpawn"/>
    /// and <see cref="Factory{T}.LazySpawnBuffer"/> are read every frame — changes after Add
    /// take effect on the next tick.
    /// </summary>
    public void Add<T>(Factory<T> factory, float worldX, float worldY, Action<T>? applyAfterInit)
        where T : Entity, new()
    {
        _records.Add(new LazySpawnRecord<T>(factory, worldX, worldY, applyAfterInit));
    }

    /// <summary>
    /// Advances every record's state machine against the activation rect (camera bounds; each
    /// record expands by its factory's <see cref="Factory{T}.LazySpawnBuffer"/>).
    /// </summary>
    public void Update(float left, float right, float bottom, float top)
    {
        for (int i = 0; i < _records.Count; i++)
            _records[i].Tick(left, right, bottom, top);
    }
}

internal interface ILazySpawnRecord
{
    void Tick(float left, float right, float bottom, float top);
}

internal enum LazySpawnRecordState
{
    Dormant,
    Live,
    Consumed,
    AwaitingRectExit,
}

internal sealed class LazySpawnRecord<T> : ILazySpawnRecord where T : Entity, new()
{
    private readonly Factory<T> _factory;
    private readonly float _worldX;
    private readonly float _worldY;
    private readonly Action<T>? _applyAfterInit;
    private LazySpawnRecordState _state = LazySpawnRecordState.Dormant;
    private bool _rectInsideLastTick;

    public LazySpawnRecord(Factory<T> factory, float worldX, float worldY, Action<T>? applyAfterInit)
    {
        _factory = factory;
        _worldX = worldX;
        _worldY = worldY;
        _applyAfterInit = applyAfterInit;
    }

    public void Tick(float left, float right, float bottom, float top)
    {
        if (_state == LazySpawnRecordState.Consumed) return;

        float buf = _factory.LazySpawnBuffer;
        bool inside =
            _worldX >= left - buf && _worldX <= right + buf &&
            _worldY >= bottom - buf && _worldY <= top + buf;
        _rectInsideLastTick = inside;

        switch (_state)
        {
            case LazySpawnRecordState.Dormant:
                if (inside) Spawn();
                break;
            case LazySpawnRecordState.AwaitingRectExit:
                if (!inside) _state = LazySpawnRecordState.Dormant;
                break;
            // Live: rect movement is irrelevant — entity owns its own life.
        }
    }

    private void Spawn()
    {
        var entity = _factory.Create();
        entity.X = _worldX;
        entity.Y = _worldY;
        _applyAfterInit?.Invoke(entity);
        _state = LazySpawnRecordState.Live;
        entity.Destroyed += OnEntityDestroyed;
    }

    private void OnEntityDestroyed()
    {
        if (_factory.LazySpawn == LazySpawnMode.OneShot)
        {
            _state = LazySpawnRecordState.Consumed;
            return;
        }
        // Reloadable: re-arm only after rect has left. If the rect already isn't covering the
        // record at destroy time (e.g. entity died offscreen), skip the wait — go straight to
        // Dormant so the next re-entry spawns. Otherwise wait for the rect to exit.
        _state = _rectInsideLastTick
            ? LazySpawnRecordState.AwaitingRectExit
            : LazySpawnRecordState.Dormant;
    }
}

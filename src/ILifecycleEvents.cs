using System;

namespace FlatRedBall2;

/// <summary>
/// Exposes observable lifecycle events for engine objects that have an initialize/update/destroy lifecycle.
/// External listeners can subscribe without overriding virtuals.
/// </summary>
public interface ILifecycleEvents
{
    /// <summary>Raised after CustomInitialize completes.</summary>
    event Action? Initialized;
    /// <summary>Raised after each CustomActivity call.</summary>
    event Action? Updated;
    /// <summary>Raised after CustomDestroy completes, before the object is removed from the engine.</summary>
    event Action? Destroyed;
}

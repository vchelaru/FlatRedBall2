namespace RiftboundSample.Systems;

public enum BattleAnimationType
{
    Slide,
    Flash,
    Fade,
    Bounce,
    HealFlash,
}

public class BattleAnimation
{
    public string EntityId { get; init; } = "";
    public BattleAnimationType Type { get; init; }
    public float Duration { get; init; }
    public float Elapsed { get; set; }
    public Action? OnComplete { get; init; }

    /// <summary>For Slide: the target position offset to move to, then return from.</summary>
    public float SlideOffsetX { get; init; }

    public float Progress => Duration > 0 ? Math.Clamp(Elapsed / Duration, 0f, 1f) : 1f;
    public bool IsComplete => Elapsed >= Duration;
}

/// <summary>
/// Queues and plays simple battle animations (slide, flash, fade, bounce).
/// Call Update each frame; read CurrentAnimation to apply visual effects.
/// </summary>
public class BattleAnimator
{
    private readonly Queue<BattleAnimation> _queue = new();
    private BattleAnimation? _current;

    public BattleAnimation? CurrentAnimation => _current;
    public bool IsPlaying => _current != null;

    public void Enqueue(BattleAnimation animation)
    {
        _queue.Enqueue(animation);
    }

    /// <summary>
    /// Queues an attack slide: entity moves toward target then back.
    /// </summary>
    public void EnqueueAttackSlide(string attackerId, float offsetX = 40f, float duration = 0.3f)
    {
        Enqueue(new BattleAnimation
        {
            EntityId = attackerId,
            Type = BattleAnimationType.Slide,
            Duration = duration,
            SlideOffsetX = offsetX,
        });
    }

    /// <summary>
    /// Queues a damage flash: target toggles visibility.
    /// </summary>
    public void EnqueueDamageFlash(string targetId, float duration = 0.2f)
    {
        Enqueue(new BattleAnimation
        {
            EntityId = targetId,
            Type = BattleAnimationType.Flash,
            Duration = duration,
        });
    }

    /// <summary>
    /// Queues a death fade-out.
    /// </summary>
    public void EnqueueDeathFade(string targetId, float duration = 0.5f)
    {
        Enqueue(new BattleAnimation
        {
            EntityId = targetId,
            Type = BattleAnimationType.Fade,
            Duration = duration,
        });
    }

    /// <summary>
    /// Queues a heal flash (green tint toggle).
    /// </summary>
    public void EnqueueHealFlash(string targetId, float duration = 0.3f)
    {
        Enqueue(new BattleAnimation
        {
            EntityId = targetId,
            Type = BattleAnimationType.HealFlash,
            Duration = duration,
        });
    }

    /// <summary>
    /// Queues a victory bounce for all entities in the list.
    /// </summary>
    public void EnqueueVictoryBounce(IEnumerable<string> entityIds, float duration = 1.0f)
    {
        foreach (var id in entityIds)
        {
            Enqueue(new BattleAnimation
            {
                EntityId = id,
                Type = BattleAnimationType.Bounce,
                Duration = duration,
            });
        }
    }

    /// <summary>
    /// Advances the current animation. Returns the animation that just completed, if any.
    /// </summary>
    public BattleAnimation? Update(float deltaSeconds)
    {
        if (_current == null)
        {
            if (_queue.Count == 0)
                return null;
            _current = _queue.Dequeue();
        }

        _current.Elapsed += deltaSeconds;

        if (_current.IsComplete)
        {
            var completed = _current;
            completed.OnComplete?.Invoke();
            _current = null;
            return completed;
        }

        return null;
    }

    /// <summary>
    /// Returns the visual offset for a slide animation (ping-pong motion).
    /// </summary>
    public static float GetSlideOffset(BattleAnimation anim)
    {
        float t = anim.Progress;
        // Move out in first half, return in second half
        float pingPong = t < 0.5f ? t * 2f : (1f - t) * 2f;
        return anim.SlideOffsetX * pingPong;
    }

    /// <summary>
    /// Returns whether the entity should be visible during a flash animation.
    /// Toggles every 0.05s.
    /// </summary>
    public static bool GetFlashVisible(BattleAnimation anim)
    {
        return ((int)(anim.Elapsed / 0.05f)) % 2 == 0;
    }

    /// <summary>
    /// Returns a scale factor for fade-out (1.0 -> 0.0).
    /// </summary>
    public static float GetFadeScale(BattleAnimation anim)
    {
        return 1f - anim.Progress;
    }

    /// <summary>
    /// Returns a Y offset for bounce animation (sinusoidal).
    /// </summary>
    public static float GetBounceOffsetY(BattleAnimation anim)
    {
        return (float)Math.Abs(Math.Sin(anim.Progress * Math.PI * 4)) * 8f;
    }

    public void Clear()
    {
        _queue.Clear();
        _current = null;
    }
}

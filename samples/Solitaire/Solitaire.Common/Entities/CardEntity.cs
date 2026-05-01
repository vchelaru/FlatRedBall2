using System;
using System.Threading;
using System.Threading.Tasks;
using FlatRedBall2;
using FlatRedBall2.Collision;
using FlatRedBall2.Tweening;
using FlatRedBall.Glue.StateInterpolation;
using RenderingLibrary.Graphics;
using Solitaire.Cards;
using Solitaire.Components;

namespace Solitaire.Entities;

public class CardEntity : Entity
{
    public const float Width = 80f;
    public const float Height = 112f;

    private CardGum _gum = null!;
    private Card? _model;
    private Tweener? _moveTween;
    private CancellationTokenSource? _pendingHideCts;

    public AARect HitBox { get; private set; } = null!;

    public Card? Model
    {
        get => _model;
        set
        {
            _model = value;
            ApplyModel();
        }
    }

    public override void CustomInitialize()
    {
        _gum = new CardGum();
        // The entity is positioned by its center, but Gum's default origin is
        // top-left. Re-anchor the visual to its center on this instance so the
        // entity's X/Y matches the visual's geometric center. Authored on the
        // instance (not the component) — centering is a consumer concern, not
        // a property of the card itself.
        _gum.Visual.XOrigin = HorizontalAlignment.Center;
        _gum.Visual.YOrigin = VerticalAlignment.Center;
        Add(_gum);

        HitBox = new AARect { Width = Width, Height = Height };
        Add(HitBox);

        ApplyModel();
    }

    private void ApplyModel()
    {
        if (_gum is null || _model is null) return;

        _gum.SuitState = _model.Suit switch
        {
            Suit.Spades => CardGum.Suit.Spades,
            Suit.Hearts => CardGum.Suit.Hearts,
            Suit.Clubs => CardGum.Suit.Clubs,
            Suit.Diamonds => CardGum.Suit.Diamonds,
            _ => CardGum.Suit.Spades,
        };

        _gum.RankState = (int)_model.Rank switch
        {
            1 => CardGum.Rank.Ace,
            2 => CardGum.Rank._2,
            3 => CardGum.Rank._3,
            4 => CardGum.Rank._4,
            5 => CardGum.Rank._5,
            6 => CardGum.Rank._6,
            7 => CardGum.Rank._7,
            8 => CardGum.Rank._8,
            9 => CardGum.Rank._9,
            10 => CardGum.Rank._10,
            11 => CardGum.Rank.Jack,
            12 => CardGum.Rank.Queen,
            13 => CardGum.Rank.King,
            _ => CardGum.Rank.Ace,
        };

        _gum.FacingState = _model.IsFaceUp ? CardGum.Facing.Up : CardGum.Facing.Down;
    }

    /// <summary>
    /// Re-applies only the Facing state from the model. Cheaper than re-assigning
    /// <see cref="Model"/>, which re-runs all three categories (Suit, Rank, Facing)
    /// every call.
    /// </summary>
    public void RefreshFacing()
    {
        if (_gum is null || _model is null) return;
        _gum.FacingState = _model.IsFaceUp ? CardGum.Facing.Up : CardGum.Facing.Down;
    }

    // Lifted Z while a card is sliding to its destination — keeps the moving card on top
    // of any cards it passes over (e.g. stock→waste cards travelling out from under the
    // top of the stock, whose remaining cards have higher Z than the new arrival's
    // destination Z). Final Z is restored on natural completion. Below DragLiftZ (100)
    // so an actively-dragged card still beats a tweening one.
    private const float TweenLiftZ = 50f;

    /// <summary>
    /// Hides the card after <paramref name="delay"/>, unless the card becomes visible
    /// again or another hide is scheduled in the meantime. Used to keep a stack's
    /// previous-top card on screen while the next-top is animating in to cover it.
    /// </summary>
    public void HideAfter(TimeSpan delay)
    {
        _pendingHideCts?.Cancel();
        var cts = new CancellationTokenSource();
        _pendingHideCts = cts;
        _ = HideAfterCore(delay, cts.Token);
    }

    private async Task HideAfterCore(TimeSpan delay, CancellationToken token)
    {
        try { await Engine.Time.Delay(delay, token); }
        catch (TaskCanceledException) { return; }
        if (!token.IsCancellationRequested) IsVisible = false;
    }

    /// <summary>
    /// Cancels any pending <see cref="HideAfter"/>. Called whenever the card is
    /// explicitly made visible so a stale deferred hide doesn't fire afterwards.
    /// </summary>
    public void CancelPendingHide()
    {
        _pendingHideCts?.Cancel();
        _pendingHideCts = null;
    }

    /// <summary>
    /// Tweens X/Y to the target over <paramref name="duration"/>. Z is lifted for the
    /// duration of the tween so the moving card draws over stationary cards along its
    /// path; on completion Z settles to <paramref name="z"/>. Stops any in-flight move
    /// tween first to avoid the two-tweens-fighting stacking gotcha.
    /// </summary>
    public void TweenMoveTo(float x, float y, float z, TimeSpan duration)
    {
        _moveTween?.Stop();

        if (X == x && Y == y)
        {
            Z = z;
            _moveTween = null;
            return;
        }

        // Preserve relative ordering between concurrently-tweening cards by adding
        // the destination Z (a within-pile stack offset) on top of the lift. Three
        // cards tweening to waste with z = 3,4,5 * StackZStep stay in the same
        // top-to-bottom order during the slide.
        Z = TweenLiftZ + z;

        float fromX = X, fromY = Y;
        float toX = x, toY = y;
        float finalZ = z;
        var tween = this.Tween(
            t => { X = fromX + (toX - fromX) * t; Y = fromY + (toY - fromY) * t; },
            0f, 1f, duration, InterpolationType.Quadratic, Easing.Out);
        tween.Ended += () => Z = finalZ;
        _moveTween = tween;
    }
}

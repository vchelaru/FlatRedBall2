using FlatRedBall2;
using Gum.DataTypes;
using Gum.Forms.Controls;
using Gum.Wireframe;
using MonoGameGum.GueDeriving;

namespace RiftboundSample.Systems;

public enum TransitionType
{
    FadeToBlack,
    BattleFlash,
}

/// <summary>
/// Provides fade-to-black and flash transition overlays using a Gum panel.
/// Call Start to begin a transition, Update each frame, and check IsComplete.
/// </summary>
public class ScreenTransitionEffect
{
    private ColoredRectangleRuntime _overlay = null!;
    private Screen _screen = null!;

    private TransitionType _type;
    private float _duration;
    private float _elapsed;
    private bool _active;
    private Action? _onMidpoint;
    private Action? _onComplete;
    private bool _midpointFired;

    public bool IsActive => _active;
    public bool IsComplete => !_active;

    public void Initialize(Screen screen)
    {
        _screen = screen;

        _overlay = new ColoredRectangleRuntime
        {
            Width = 0,
            Height = 0,
            WidthUnits = DimensionUnitType.RelativeToParent,
            HeightUnits = DimensionUnitType.RelativeToParent,
            Red = 0,
            Green = 0,
            Blue = 0,
            Alpha = 0,
        };

        var panel = new Panel();
        panel.Dock(Dock.Fill);
        panel.Visual.Children.Add(_overlay);
        _screen.Add(panel);
    }

    /// <summary>
    /// Starts a transition effect. onMidpoint fires when fully opaque (for screen swaps).
    /// </summary>
    public void Start(TransitionType type, float duration = 0.5f, Action? onMidpoint = null, Action? onComplete = null)
    {
        _type = type;
        _duration = duration;
        _elapsed = 0f;
        _active = true;
        _midpointFired = false;
        _onMidpoint = onMidpoint;
        _onComplete = onComplete;
    }

    public void Update(float deltaSeconds)
    {
        if (!_active) return;

        _elapsed += deltaSeconds;
        float progress = Math.Clamp(_elapsed / _duration, 0f, 1f);

        switch (_type)
        {
            case TransitionType.FadeToBlack:
            {
                // First half: fade in (0 -> 255), second half: fade out (255 -> 0)
                float half = _duration / 2f;
                if (_elapsed < half)
                {
                    _overlay.Alpha = (int)(255 * (_elapsed / half));
                }
                else
                {
                    if (!_midpointFired)
                    {
                        _midpointFired = true;
                        _onMidpoint?.Invoke();
                    }
                    _overlay.Alpha = (int)(255 * (1f - (_elapsed - half) / half));
                }
                break;
            }

            case TransitionType.BattleFlash:
            {
                // Quick white flash: ramp up to white, then fade
                _overlay.Red = 255;
                _overlay.Green = 255;
                _overlay.Blue = 255;
                float quarter = _duration / 4f;
                if (_elapsed < quarter)
                {
                    _overlay.Alpha = (int)(255 * (_elapsed / quarter));
                }
                else
                {
                    if (!_midpointFired)
                    {
                        _midpointFired = true;
                        _onMidpoint?.Invoke();
                    }
                    float remaining = (_elapsed - quarter) / (_duration - quarter);
                    _overlay.Alpha = (int)(255 * (1f - remaining));
                }
                break;
            }
        }

        if (_elapsed >= _duration)
        {
            _overlay.Alpha = 0;
            _active = false;
            _onComplete?.Invoke();
        }
    }
}

using AnimationEditor.Core.Keyframes;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Media;

namespace AnimationEditor.Views.Controls;

/// <summary>
/// The keyframe diamond and eight-icon curve picker for one property group (#1478). It owns only
/// presentation: <see cref="Show"/> displays a <see cref="KeyframeGroupState"/>, and a user click raises
/// <see cref="KeyframeToggled"/> or <see cref="CurveChosen"/> for the caller to turn into a command.
/// </summary>
public partial class KeyframeControl : UserControl
{
    // 16x16 glyphs, start bottom-left and end top-right, drawn a little exaggerated so they read at 24 px.
    private static readonly Dictionary<KeyframeCurveChoice, string> Glyphs = new()
    {
        [KeyframeCurveChoice.Snap] = "M 2,13 L 8,13 L 8,3 L 14,3",
        [KeyframeCurveChoice.Linear] = "M 2,13 L 14,3",
        [KeyframeCurveChoice.EaseIn] = "M 2,13 C 9,13 12,9 14,3",
        [KeyframeCurveChoice.EaseOut] = "M 2,13 C 4,7 7,3 14,3",
        [KeyframeCurveChoice.EaseInOut] = "M 2,13 C 8,13 8,3 14,3",
        [KeyframeCurveChoice.Anticipate] = "M 2,12 C 5,16 9,12 14,3",
        [KeyframeCurveChoice.Overshoot] = "M 2,13 C 7,4 11,0 14,4",
        [KeyframeCurveChoice.Bounce] = "M 2,13 C 3,9 4,3 6,3 C 7,3 8,5 8.5,6 C 9,5 10,3 11,3 C 12,3 12.5,4 13,4 C 13.5,3 14,3 14,3",
    };

    private readonly Dictionary<KeyframeCurveChoice, ToggleButton> _curveButtons = new();
    private KeyframeCurveChoice? _selected;
    private string? _note;

    /// <summary>Raised when the user clicks the diamond; the argument is whether it is now a keyframe.</summary>
    public event Action<bool>? KeyframeToggled;

    /// <summary>Raised when the user clicks a curve icon.</summary>
    public event Action<KeyframeCurveChoice>? CurveChosen;

    public KeyframeControl()
    {
        InitializeComponent();

        foreach (var choice in KeyframeCurvePicker.All)
        {
            var button = new ToggleButton
            {
                Classes = { "kfCurve" },
                Content = new Avalonia.Controls.Shapes.Path { Data = Geometry.Parse(Glyphs[choice]), Stretch = Stretch.None },
            };
            button.Click += (_, _) => OnCurveClicked(choice);
            _curveButtons[choice] = button;
            CurvePanel.Children.Add(button);
        }

        Diamond.Click += (_, _) => KeyframeToggled?.Invoke(Diamond.IsChecked == true);
        UpdateCurveTooltips();
    }

    /// <summary>The diamond, for tests and automation.</summary>
    public ToggleButton DiamondButton => Diamond;

    /// <summary>The curve buttons in picker order, for tests and automation.</summary>
    public IReadOnlyList<ToggleButton> CurveButtons => KeyframeCurvePicker.All.Select(c => _curveButtons[c]).ToList();

    /// <summary>The curve button for <paramref name="choice"/>.</summary>
    public ToggleButton CurveButton(KeyframeCurveChoice choice) => _curveButtons[choice];

    /// <summary>The highlighted curve, or <c>null</c> when none is (mixed, or a stored curve outside the eight).</summary>
    public KeyframeCurveChoice? SelectedCurve => _selected;

    /// <summary>The tooltip suffix naming a stored curve the picker cannot show, or <c>null</c>.</summary>
    public string? CurveNote => _note;

    /// <summary>Whether the curve icons are showing (they hide while the keyframe is off).</summary>
    public bool CurvesVisible => CurvePanel.IsVisible;

    /// <summary>Displays <paramref name="state"/>. Never raises <see cref="KeyframeToggled"/> or <see cref="CurveChosen"/>.</summary>
    public void Show(KeyframeGroupState state)
    {
        Diamond.IsChecked = state.IsKeyframe;
        Diamond.IsEnabled = state.CanToggle;
        ToolTip.SetTip(Diamond, state.CanToggle ? "Keyframe" : "Keyframe (the first frame is always a keyframe)");

        CurvePanel.IsVisible = state.CurvesVisible;
        Divider.IsVisible = state.CurvesVisible;
        _selected = state.Curve;
        _note = state.CurveNote;
        foreach (var (choice, button) in _curveButtons)
            button.IsChecked = state.Curve == choice;
        UpdateCurveTooltips();
    }

    private void OnCurveClicked(KeyframeCurveChoice choice)
    {
        // A click on the lit icon would uncheck it; the caller's refresh re-lights the real state.
        foreach (var (other, button) in _curveButtons)
            button.IsChecked = other == choice;
        _selected = choice;
        _note = null;
        UpdateCurveTooltips();
        CurveChosen?.Invoke(choice);
    }

    private void UpdateCurveTooltips()
    {
        foreach (var (choice, button) in _curveButtons)
        {
            string name = KeyframeCurvePicker.DisplayName(choice);
            ToolTip.SetTip(button, _note is null ? name : $"{name}\nCurrent: {_note}");
        }
    }
}

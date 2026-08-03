using FlatRedBall2;
using Gum.DataTypes;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework.Input;
using MonoGameGum.GueDeriving;
using RiftboundSample.Systems;

namespace RiftboundSample.UI;

/// <summary>
/// Scrollable list of all seen story events. Select one to replay its dialogue.
/// </summary>
public class CutsceneReplayPanel
{
    private Screen _screen = null!;
    private Panel _root = null!;
    private StackPanel _list = null!;
    private List<Label> _labels = [];
    private int _selectedIndex;

    private CutsceneReplaySystem? _replaySystem;

    public bool IsVisible => _root?.Visual.Visible ?? false;

    public event Action<string>? ReplaySelected;
    public event Action? Closed;

    public void Initialize(Screen screen)
    {
        _screen = screen;

        _root = new Panel();
        _root.Dock(Dock.Fill);
        _root.Visual.Visible = false;

        var bg = new ColoredRectangleRuntime
        {
            Width = 0,
            Height = 0,
            WidthUnits = DimensionUnitType.RelativeToParent,
            HeightUnits = DimensionUnitType.RelativeToParent,
            Red = 10,
            Green = 10,
            Blue = 30,
            Alpha = 220,
        };
        _root.Visual.Children.Add(bg);

        _list = new StackPanel { Spacing = 6 };
        _list.Anchor(Anchor.Center);

        var title = new Label { Text = "CUTSCENE REPLAY" };
        _list.AddChild(title);

        _root.AddChild(_list);
        _screen.Add(_root);
    }

    public void Show(CutsceneReplaySystem replaySystem)
    {
        _replaySystem = replaySystem;
        _selectedIndex = 0;
        RebuildList();
        _root.Visual.Visible = true;
    }

    public void Hide()
    {
        _root.Visual.Visible = false;
    }

    public void Update(FlatRedBallService engine)
    {
        if (!IsVisible || _replaySystem == null) return;

        var kb = engine.InputManager.Keyboard;

        if (kb.WasKeyPressed(Keys.Escape))
        {
            Hide();
            Closed?.Invoke();
            return;
        }

        if (_labels.Count == 0) return;

        if (kb.WasKeyPressed(Keys.Up))
        {
            _selectedIndex = (_selectedIndex - 1 + _labels.Count) % _labels.Count;
            UpdateHighlight();
        }
        else if (kb.WasKeyPressed(Keys.Down))
        {
            _selectedIndex = (_selectedIndex + 1) % _labels.Count;
            UpdateHighlight();
        }
        else if (kb.WasKeyPressed(Keys.Enter) || kb.WasKeyPressed(Keys.Space))
        {
            var events = _replaySystem.SeenEvents;
            if (_selectedIndex >= 0 && _selectedIndex < events.Count)
            {
                Hide();
                ReplaySelected?.Invoke(events[_selectedIndex].EventId);
            }
        }
    }

    private void RebuildList()
    {
        // Remove old labels
        foreach (var label in _labels)
            _list.Visual.Children.Remove(label.Visual);
        _labels.Clear();

        if (_replaySystem == null) return;

        foreach (var (_, displayName) in _replaySystem.SeenEvents)
        {
            var label = new Label { Text = $"  {displayName}" };
            _list.AddChild(label);
            _labels.Add(label);
        }

        if (_labels.Count == 0)
        {
            var empty = new Label { Text = "  (no cutscenes seen yet)" };
            _list.AddChild(empty);
            _labels.Add(empty);
        }

        var hint = new Label { Text = "[Enter] Replay  [Esc] Back" };
        _list.AddChild(hint);

        UpdateHighlight();
    }

    private void UpdateHighlight()
    {
        var events = _replaySystem?.SeenEvents;
        if (events == null) return;

        for (int i = 0; i < _labels.Count && i < events.Count; i++)
        {
            string prefix = i == _selectedIndex ? "> " : "  ";
            _labels[i].Text = $"{prefix}{events[i].DisplayName}";
        }
    }
}

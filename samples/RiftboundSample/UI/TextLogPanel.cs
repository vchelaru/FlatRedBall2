using FlatRedBall2;
using Gum.DataTypes;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework.Input;
using MonoGameGum.GueDeriving;

namespace RiftboundSample.UI;

/// <summary>
/// Scrollable text log of all dialogue lines seen this session.
/// </summary>
public class TextLogPanel
{
    private Screen _screen = null!;
    private Panel _root = null!;
    private StackPanel _list = null!;
    private List<Label> _lineLabels = [];
    private int _scrollOffset;

    private const int MaxVisibleLines = 12;

    public bool IsVisible => _root?.Visual.Visible ?? false;

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
            Blue = 20,
            Alpha = 230,
        };
        _root.Visual.Children.Add(bg);

        _list = new StackPanel { Spacing = 4 };
        _list.Anchor(Anchor.Center);

        var title = new Label { Text = "TEXT LOG" };
        _list.AddChild(title);

        _root.AddChild(_list);
        _screen.Add(_root);
    }

    public void Show(IReadOnlyList<string> log)
    {
        _scrollOffset = Math.Max(0, log.Count - MaxVisibleLines);
        RebuildLines(log);
        _root.Visual.Visible = true;
    }

    public void Hide()
    {
        _root.Visual.Visible = false;
    }

    public void Update(FlatRedBallService engine, IReadOnlyList<string> log)
    {
        if (!IsVisible) return;

        var kb = engine.InputManager.Keyboard;

        if (kb.WasKeyPressed(Keys.Escape))
        {
            Hide();
            Closed?.Invoke();
            return;
        }

        bool changed = false;
        if (kb.WasKeyPressed(Keys.Up) && _scrollOffset > 0)
        {
            _scrollOffset--;
            changed = true;
        }
        else if (kb.WasKeyPressed(Keys.Down) && _scrollOffset < Math.Max(0, log.Count - MaxVisibleLines))
        {
            _scrollOffset++;
            changed = true;
        }

        if (changed)
            RebuildLines(log);
    }

    private void RebuildLines(IReadOnlyList<string> log)
    {
        foreach (var label in _lineLabels)
            _list.Visual.Children.Remove(label.Visual);
        _lineLabels.Clear();

        int end = Math.Min(_scrollOffset + MaxVisibleLines, log.Count);
        for (int i = _scrollOffset; i < end; i++)
        {
            var label = new Label { Text = log[i] };
            _list.AddChild(label);
            _lineLabels.Add(label);
        }

        var hint = new Label { Text = "[Up/Down] Scroll  [Esc] Close" };
        _list.AddChild(hint);
        _lineLabels.Add(hint);
    }
}

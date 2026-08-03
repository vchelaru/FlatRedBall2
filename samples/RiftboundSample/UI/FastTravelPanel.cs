using FlatRedBall2;
using Gum.DataTypes;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework.Input;
using MonoGameGum.GueDeriving;
using RiftboundSample.Levels;
using RiftboundSample.Systems;

namespace RiftboundSample.UI;

/// <summary>
/// Lists visited maps for fast travel selection. Navigate with Up/Down, confirm with Enter, cancel with Escape.
/// </summary>
public class FastTravelPanel
{
    private Screen _screen = null!;
    private Panel _root = null!;
    private StackPanel _mapList = null!;

    private List<Label> _mapLabels = [];
    private List<string> _mapIds = [];
    private int _selectedIndex;

    public bool IsVisible => _root?.Visual.Visible ?? false;

    public event Action? Closed;
    public event Action<string>? MapSelected;

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
            Green = 15,
            Blue = 30,
            Alpha = 230,
        };
        _root.Visual.Children.Add(bg);

        var layout = new StackPanel { Spacing = 4 };
        layout.X = 20;
        layout.Y = 10;

        layout.AddChild(new Label { Text = "FAST TRAVEL" });
        layout.AddChild(new Label { Text = "Up/Down: Select  Enter: Travel  Esc: Cancel" });

        _mapList = new StackPanel { Spacing = 2 };
        layout.AddChild(_mapList);

        _root.AddChild(layout);
        _screen.Add(_root);
    }

    public void Show(FastTravelSystem fastTravel)
    {
        _selectedIndex = 0;
        _root.Visual.Visible = true;
        RebuildList(fastTravel);
    }

    public void Hide()
    {
        _root.Visual.Visible = false;
    }

    public void Update(FlatRedBallService engine)
    {
        if (!IsVisible) return;

        var kb = engine.InputManager.Keyboard;

        if (kb.WasKeyPressed(Keys.Escape))
        {
            Hide();
            Closed?.Invoke();
            return;
        }

        int count = _mapLabels.Count;
        if (count == 0) return;

        if (kb.WasKeyPressed(Keys.Up))
        {
            _selectedIndex = (_selectedIndex - 1 + count) % count;
            UpdateHighlight();
        }
        else if (kb.WasKeyPressed(Keys.Down))
        {
            _selectedIndex = (_selectedIndex + 1) % count;
            UpdateHighlight();
        }
        else if (kb.WasKeyPressed(Keys.Enter) || kb.WasKeyPressed(Keys.Space))
        {
            if (_selectedIndex < _mapIds.Count)
            {
                string mapId = _mapIds[_selectedIndex];
                Hide();
                MapSelected?.Invoke(mapId);
            }
        }
    }

    private void RebuildList(FastTravelSystem fastTravel)
    {
        foreach (var label in _mapLabels)
            _mapList.Visual.Children.Remove(label.Visual);
        _mapLabels.Clear();
        _mapIds.Clear();

        var sorted = fastTravel.GetSortedVisitedMaps();
        for (int i = 0; i < sorted.Count; i++)
        {
            string mapId = sorted[i];
            string displayName = MapRegistry.Get(mapId).Name;
            string prefix = i == _selectedIndex ? "> " : "  ";
            var label = new Label { Text = $"{prefix}{displayName}" };
            _mapList.AddChild(label);
            _mapLabels.Add(label);
            _mapIds.Add(mapId);
        }
    }

    private void UpdateHighlight()
    {
        for (int i = 0; i < _mapLabels.Count && i < _mapIds.Count; i++)
        {
            string displayName = MapRegistry.Get(_mapIds[i]).Name;
            string prefix = i == _selectedIndex ? "> " : "  ";
            _mapLabels[i].Text = $"{prefix}{displayName}";
        }
    }
}

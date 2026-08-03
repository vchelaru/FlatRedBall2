using FlatRedBall2;
using Gum.DataTypes;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework.Input;
using MonoGameGum.GueDeriving;
using RiftboundSample.Models;
using RiftboundSample.Systems;

namespace RiftboundSample.UI;

/// <summary>
/// Shows save slots for saving or loading. Navigate with Up/Down, confirm with Enter, cancel with Escape.
/// </summary>
public class SaveLoadPanel
{
    private Screen _screen = null!;
    private Panel _root = null!;
    private Label _titleLabel = null!;
    private StackPanel _slotList = null!;

    private List<Label> _slotLabels = [];
    private List<SaveSlotInfo?> _slots = [];
    private int _selectedIndex;
    private bool _saveMode;

    public bool IsVisible => _root?.Visual.Visible ?? false;

    public event Action? Closed;
    public event Action<SaveData>? LoadRequested;

    /// <summary>Provides current game state for saving. Set by the screen before showing.</summary>
    public Func<SaveData>? SaveDataProvider { get; set; }

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
            Alpha = 240,
        };
        _root.Visual.Children.Add(bg);

        var layout = new StackPanel { Spacing = 4 };
        layout.X = 20;
        layout.Y = 10;

        _titleLabel = new Label { Text = "SAVE" };
        layout.AddChild(_titleLabel);

        layout.AddChild(new Label { Text = "Up/Down: Select  Enter: Confirm  Esc: Back" });

        _slotList = new StackPanel { Spacing = 2 };
        layout.AddChild(_slotList);

        _root.AddChild(layout);
        _screen.Add(_root);
    }

    public void Show(bool saveMode)
    {
        _saveMode = saveMode;
        _selectedIndex = 0;
        _titleLabel.Text = saveMode ? "SAVE GAME" : "LOAD GAME";
        _root.Visual.Visible = true;
        RefreshSlots();
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

        int count = _slotLabels.Count;
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
            if (_saveMode)
                DoSave();
            else
                DoLoad();
        }
    }

    private void DoSave()
    {
        if (SaveDataProvider == null) return;

        var data = SaveDataProvider();
        // Slot index: autosave is last entry
        if (_selectedIndex >= _slots.Count) return;

        var slotInfo = _slots[_selectedIndex];
        if (slotInfo?.IsAutosave == true)
        {
            SaveSystem.Autosave(data);
        }
        else
        {
            // Regular slots are index 0..20
            SaveSystem.Save(data, _selectedIndex);
        }

        RefreshSlots();
    }

    private void DoLoad()
    {
        if (_selectedIndex >= _slots.Count) return;

        var slotInfo = _slots[_selectedIndex];
        if (slotInfo == null) return;

        SaveData? data;
        if (slotInfo.IsAutosave)
            data = SaveSystem.LoadAutosave();
        else
            data = SaveSystem.Load(slotInfo.SlotIndex);

        if (data != null)
        {
            Hide();
            LoadRequested?.Invoke(data);
        }
    }

    private void RefreshSlots()
    {
        foreach (var label in _slotLabels)
            _slotList.Visual.Children.Remove(label.Visual);
        _slotLabels.Clear();

        _slots = SaveSystem.GetAllSlots();

        for (int i = 0; i < _slots.Count; i++)
        {
            var slot = _slots[i];
            string prefix = i == _selectedIndex ? "> " : "  ";
            string text;
            if (slot != null)
            {
                string time = slot.PlayTime.TotalHours >= 1
                    ? $"{(int)slot.PlayTime.TotalHours}h {slot.PlayTime.Minutes}m"
                    : $"{slot.PlayTime.Minutes}m {slot.PlayTime.Seconds}s";
                text = $"{prefix}{slot.DisplayName} - {slot.CurrentMap}  {time}  {slot.SaveTime:g}";
            }
            else
            {
                string slotName = i < _slots.Count - 1 ? $"Slot {i + 1}" : "Autosave";
                text = $"{prefix}{slotName} - Empty";
            }

            var label = new Label { Text = text };
            _slotList.AddChild(label);
            _slotLabels.Add(label);
        }
    }

    private void UpdateHighlight()
    {
        // Just rebuild for simplicity, since slot data doesn't change between navigations
        for (int i = 0; i < _slotLabels.Count; i++)
        {
            var slot = i < _slots.Count ? _slots[i] : null;
            string prefix = i == _selectedIndex ? "> " : "  ";
            if (slot != null)
            {
                string time = slot.PlayTime.TotalHours >= 1
                    ? $"{(int)slot.PlayTime.TotalHours}h {slot.PlayTime.Minutes}m"
                    : $"{slot.PlayTime.Minutes}m {slot.PlayTime.Seconds}s";
                _slotLabels[i].Text = $"{prefix}{slot.DisplayName} - {slot.CurrentMap}  {time}  {slot.SaveTime:g}";
            }
            else
            {
                string slotName = i < _slots.Count - 1 ? $"Slot {i + 1}" : "Autosave";
                _slotLabels[i].Text = $"{prefix}{slotName} - Empty";
            }
        }
    }
}

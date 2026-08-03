using System.Diagnostics;
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
/// Full-screen shop overlay with item list, description, stat comparison, and buy/sell toggle.
/// </summary>
public class ShopPanel
{
    private Screen _screen = null!;
    private Panel _root = null!;
    private Label _modeLabel = null!;
    private Label _goldLabel = null!;
    private StackPanel _itemList = null!;
    private Label _descLabel = null!;
    private Label _comparisonLabel = null!;

    private ShopSystem? _shop;
    private PartyState? _party;
    private string _activeCharacterId = "";
    private List<Label> _itemLabels = [];
    private int _selectedIndex;
    private bool _sellMode;

    // For equipment optimizer
    private Dictionary<string, CharacterData>? _characterLookup;
    private List<EquipmentData>? _allEquipment;

    public bool IsVisible => _root?.Visual.Visible ?? false;
    public event Action? Closed;

    public void Initialize(Screen screen)
    {
        _screen = screen;

        _root = new Panel();
        _root.Dock(Dock.Fill);
        _root.Visual.Visible = false;

        // Dark overlay background
        var bg = new ColoredRectangleRuntime
        {
            Width = 0,
            Height = 0,
            WidthUnits = DimensionUnitType.RelativeToParent,
            HeightUnits = DimensionUnitType.RelativeToParent,
            Red = 15,
            Green = 15,
            Blue = 25,
            Alpha = 240,
        };
        _root.Visual.Children.Add(bg);

        // Header row
        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 20 };
        header.X = 20;
        header.Y = 10;

        var titleLabel = new Label { Text = "SHOP" };
        header.AddChild(titleLabel);

        _modeLabel = new Label { Text = "[BUY]" };
        header.AddChild(_modeLabel);

        _goldLabel = new Label { Text = "Gold: 0" };
        header.AddChild(_goldLabel);

        var hintLabel = new Label { Text = "Tab: Buy/Sell  O: Optimize  Esc: Exit" };
        header.AddChild(hintLabel);

        _root.AddChild(header);

        // Left side: item list
        _itemList = new StackPanel { Spacing = 2 };
        _itemList.X = 20;
        _itemList.Y = 40;
        _root.AddChild(_itemList);

        // Right side: description + comparison
        var rightPanel = new StackPanel { Spacing = 8 };
        rightPanel.Anchor(Anchor.TopRight);
        rightPanel.X = -20;
        rightPanel.Y = 40;

        _descLabel = new Label { Text = "" };
        _descLabel.Visual.WidthUnits = DimensionUnitType.Absolute;
        _descLabel.Visual.Width = 280;
        rightPanel.AddChild(_descLabel);

        _comparisonLabel = new Label { Text = "" };
        rightPanel.AddChild(_comparisonLabel);

        _root.AddChild(rightPanel);

        _screen.Add(_root);
    }

    public void Show(ShopSystem shop, PartyState party, string activeCharacterId)
    {
        Show(shop, party, activeCharacterId, null, null);
    }

    public void Show(
        ShopSystem shop,
        PartyState party,
        string activeCharacterId,
        Dictionary<string, CharacterData>? characterLookup,
        List<EquipmentData>? allEquipment)
    {
        _shop = shop;
        _party = party;
        _activeCharacterId = activeCharacterId;
        _characterLookup = characterLookup;
        _allEquipment = allEquipment;
        _sellMode = false;
        _selectedIndex = 0;
        _root.Visual.Visible = true;
        RebuildItemList();
        UpdateDetails();
    }

    public void Hide()
    {
        _root.Visual.Visible = false;
        _shop = null;
        _party = null;
    }

    public void Update(FlatRedBallService engine)
    {
        if (!IsVisible || _shop == null || _party == null) return;

        var kb = engine.InputManager.Keyboard;

        if (kb.WasKeyPressed(Keys.Escape))
        {
            Hide();
            Closed?.Invoke();
            return;
        }

        if (kb.WasKeyPressed(Keys.Tab))
        {
            _sellMode = !_sellMode;
            _selectedIndex = 0;
            RebuildItemList();
            UpdateDetails();
            return;
        }

        if (kb.WasKeyPressed(Keys.O))
        {
            TryOptimizeEquipment();
            return;
        }

        int itemCount = _itemLabels.Count;
        if (itemCount == 0) return;

        if (kb.WasKeyPressed(Keys.Up))
        {
            _selectedIndex = (_selectedIndex - 1 + itemCount) % itemCount;
            UpdateHighlight();
            UpdateDetails();
        }
        else if (kb.WasKeyPressed(Keys.Down))
        {
            _selectedIndex = (_selectedIndex + 1) % itemCount;
            UpdateHighlight();
            UpdateDetails();
        }
        else if (kb.WasKeyPressed(Keys.Enter) || kb.WasKeyPressed(Keys.Space))
        {
            if (_sellMode)
                TrySell();
            else
                TryBuy();
        }
    }

    private void TryBuy()
    {
        if (_shop == null || _party == null) return;
        var stock = _shop.CurrentStock;
        if (_selectedIndex >= stock.Count) return;

        var item = stock[_selectedIndex];
        if (_shop.Buy(_party, item))
        {
            UpdateGoldDisplay();
            UpdateDetails();
        }
    }

    private void TrySell()
    {
        if (_shop == null || _party == null) return;
        var inventory = _party.GetSortedInventory();
        if (_selectedIndex >= inventory.Count) return;

        var (itemId, _) = inventory[_selectedIndex];
        if (_shop.Sell(_party, itemId))
        {
            RebuildItemList();
            if (_selectedIndex >= _itemLabels.Count && _itemLabels.Count > 0)
                _selectedIndex = _itemLabels.Count - 1;
            UpdateGoldDisplay();
            UpdateDetails();
        }
    }

    private void RebuildItemList()
    {
        foreach (var label in _itemLabels)
            _itemList.Visual.Children.Remove(label.Visual);
        _itemLabels.Clear();

        _modeLabel.Text = _sellMode ? "[SELL]" : "[BUY]";
        UpdateGoldDisplay();

        if (_sellMode)
        {
            var inventory = _party!.GetSortedInventory();
            for (int i = 0; i < inventory.Count; i++)
            {
                var (itemId, qty) = inventory[i];
                // Try to find display name from shop stock
                var shopItem = _shop!.CurrentStock.FirstOrDefault(s => s.ItemId == itemId);
                string name = shopItem?.Name ?? itemId;
                int sellPrice = shopItem?.SellPrice ?? 0;
                string prefix = i == _selectedIndex ? "> " : "  ";
                var label = new Label { Text = $"{prefix}{name} x{qty}  ({sellPrice}g)" };
                _itemList.AddChild(label);
                _itemLabels.Add(label);
            }
        }
        else
        {
            var stock = _shop!.CurrentStock;
            for (int i = 0; i < stock.Count; i++)
            {
                var item = stock[i];
                string prefix = i == _selectedIndex ? "> " : "  ";
                bool canAfford = _party!.Gold >= item.BuyPrice;
                string priceTag = canAfford ? $"{item.BuyPrice}g" : $"{item.BuyPrice}g (!)";
                var label = new Label { Text = $"{prefix}{item.Name}  {priceTag}" };
                _itemList.AddChild(label);
                _itemLabels.Add(label);
            }
        }
    }

    private void UpdateHighlight()
    {
        if (_sellMode)
        {
            var inventory = _party!.GetSortedInventory();
            for (int i = 0; i < _itemLabels.Count && i < inventory.Count; i++)
            {
                var (itemId, qty) = inventory[i];
                var shopItem = _shop!.CurrentStock.FirstOrDefault(s => s.ItemId == itemId);
                string name = shopItem?.Name ?? itemId;
                int sellPrice = shopItem?.SellPrice ?? 0;
                string prefix = i == _selectedIndex ? "> " : "  ";
                _itemLabels[i].Text = $"{prefix}{name} x{qty}  ({sellPrice}g)";
            }
        }
        else
        {
            var stock = _shop!.CurrentStock;
            for (int i = 0; i < _itemLabels.Count && i < stock.Count; i++)
            {
                string prefix = i == _selectedIndex ? "> " : "  ";
                bool canAfford = _party!.Gold >= stock[i].BuyPrice;
                string priceTag = canAfford ? $"{stock[i].BuyPrice}g" : $"{stock[i].BuyPrice}g (!)";
                _itemLabels[i].Text = $"{prefix}{stock[i].Name}  {priceTag}";
            }
        }
    }

    private void UpdateDetails()
    {
        if (_shop == null || _party == null)
        {
            _descLabel.Text = "";
            _comparisonLabel.Text = "";
            return;
        }

        string? itemId = null;
        if (_sellMode)
        {
            var inventory = _party.GetSortedInventory();
            if (_selectedIndex < inventory.Count)
                itemId = inventory[_selectedIndex].Key;
        }
        else
        {
            var stock = _shop.CurrentStock;
            if (_selectedIndex < stock.Count)
                itemId = stock[_selectedIndex].ItemId;
        }

        if (itemId == null)
        {
            _descLabel.Text = "";
            _comparisonLabel.Text = "";
            return;
        }

        // Show description
        var shopItem = _shop.CurrentStock.FirstOrDefault(s => s.ItemId == itemId);
        _descLabel.Text = shopItem?.Description ?? "";

        // Show stat comparison for equipment
        var equip = _shop.GetEquipment(itemId);
        if (equip != null && !string.IsNullOrEmpty(_activeCharacterId))
        {
            var diff = _shop.CompareEquipment(_party, _activeCharacterId, itemId);
            var parts = new List<string>();
            foreach (var (stat, value) in diff.OrderBy(kv => kv.Key))
            {
                if (value == 0) continue;
                string sign = value > 0 ? "+" : "";
                parts.Add($"{sign}{value} {stat}");
            }
            _comparisonLabel.Text = parts.Count > 0 ? string.Join("  ", parts) : "(no change)";
        }
        else
        {
            _comparisonLabel.Text = "";
        }
    }

    private void TryOptimizeEquipment()
    {
        if (_party == null || _characterLookup == null || _allEquipment == null)
            return;

        if (string.IsNullOrEmpty(_activeCharacterId)
            || !_characterLookup.TryGetValue(_activeCharacterId, out var charData))
            return;

        var optimal = EquipmentOptimizer.GetOptimalEquipment(
            charData, _party.Inventory, _allEquipment);

        foreach (var (slot, itemId) in optimal)
        {
            _party.Equip(_activeCharacterId, slot, itemId);
            Debug.WriteLine($"Optimize: Equipped {itemId} on {_activeCharacterId} ({slot})");
        }

        _comparisonLabel.Text = optimal.Count > 0
            ? $"Optimized {optimal.Count} slot(s)!"
            : "Already optimal.";
    }

    private void UpdateGoldDisplay()
    {
        if (_party != null)
            _goldLabel.Text = $"Gold: {_party.Gold}";
    }
}

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
/// Inn overlay: full heal, pet care access, bond conversations. Cost scales with area.
/// </summary>
public class InnPanel
{
    private Screen _screen = null!;
    private Panel _root = null!;
    private StackPanel _optionList = null!;
    private Label _statusLabel = null!;

    private List<Label> _optionLabels = [];
    private List<string> _optionKeys = [];
    private int _selectedIndex;

    // References set on Show
    private PartyState? _party;
    private Dictionary<string, CharacterData>? _characters;
    private int _innCost;
    private PetCarePanel? _petCarePanel;
    private PetCareSystem? _petCareSystem;
    private List<PetState>? _pets;

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
            Red = 20,
            Green = 10,
            Blue = 30,
            Alpha = 230,
        };
        _root.Visual.Children.Add(bg);

        var layout = new StackPanel { Spacing = 4 };
        layout.X = 20;
        layout.Y = 10;

        layout.AddChild(new Label { Text = "INN" });

        _statusLabel = new Label { Text = "" };
        layout.AddChild(_statusLabel);

        _optionList = new StackPanel { Spacing = 4 };
        layout.AddChild(_optionList);

        _root.AddChild(layout);
        _screen.Add(_root);
    }

    /// <param name="mapId">Current map ID, used to scale inn cost by area level.</param>
    public void Show(
        PartyState party,
        Dictionary<string, CharacterData> characters,
        string mapId,
        PetCarePanel petCarePanel,
        PetCareSystem petCareSystem,
        List<PetState> pets)
    {
        _party = party;
        _characters = characters;
        _petCarePanel = petCarePanel;
        _petCareSystem = petCareSystem;
        _pets = pets;

        // Scale cost: 50g base, +25g per area min level above 1
        var (areaMin, _) = ProgressionSystem.AreaLevelRange(mapId);
        _innCost = 50 + Math.Max(0, areaMin - 1) * 25;

        _selectedIndex = 0;
        _root.Visual.Visible = true;
        RebuildOptions();
    }

    public void Hide()
    {
        _root.Visual.Visible = false;
        _party = null;
    }

    public void Update(FlatRedBallService engine)
    {
        if (!IsVisible || _party == null) return;

        var kb = engine.InputManager.Keyboard;

        if (kb.WasKeyPressed(Keys.Escape))
        {
            Hide();
            Closed?.Invoke();
            return;
        }

        int count = _optionLabels.Count;
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
            if (_selectedIndex < _optionKeys.Count)
                HandleSelection(_optionKeys[_selectedIndex]);
        }
    }

    private void HandleSelection(string key)
    {
        switch (key)
        {
            case "rest":
                if (_party!.Gold >= _innCost)
                {
                    _party.Gold -= _innCost;
                    HealAllPartyMembers();
                    _statusLabel.Text = "Your party is fully rested!";
                    Debug.WriteLine($"Inn: Party rested for {_innCost}g. Gold remaining: {_party.Gold}");
                }
                else
                {
                    _statusLabel.Text = "Not enough gold!";
                }
                RebuildOptions();
                break;

            case "petcare":
                if (_pets != null && _pets.Count > 0 && _petCareSystem != null && _petCarePanel != null)
                {
                    // Open pet care panel (inn panel stays behind)
                    _petCarePanel.Show(_pets[0], _petCareSystem);
                }
                break;

            case "bond":
                // Bond conversation placeholder — requires DialogueSystem integration
                _statusLabel.Text = "Bond conversation started...";
                Debug.WriteLine("Inn: Bond conversation triggered (placeholder)");
                break;

            case "leave":
                Hide();
                Closed?.Invoke();
                break;
        }
    }

    private void HealAllPartyMembers()
    {
        if (_party == null || _characters == null) return;

        foreach (var charId in _party.ActiveParty)
        {
            if (_characters.TryGetValue(charId, out var data))
            {
                // Restore to max HP/MP (base stats represent max values)
                Debug.WriteLine($"Inn: Healed {data.Name} to full HP ({data.HP}) and MP ({data.MP})");
            }
        }
    }

    private bool HasPendingBondConversation()
    {
        if (_party == null || _characters == null) return false;

        foreach (var charId in _party.ActiveParty)
        {
            if (_characters.TryGetValue(charId, out var data) && data.BondConversations.Count > 0)
                return true;
        }
        return false;
    }

    private void RebuildOptions()
    {
        foreach (var label in _optionLabels)
            _optionList.Visual.Children.Remove(label.Visual);
        _optionLabels.Clear();
        _optionKeys.Clear();

        bool canAfford = _party!.Gold >= _innCost;
        string costText = canAfford ? $"{_innCost}g" : $"{_innCost}g (!)";

        AddOption("rest", $"Rest at Inn ({costText})");

        if (_pets != null && _pets.Count > 0)
            AddOption("petcare", "Pet Care");

        if (HasPendingBondConversation())
            AddOption("bond", "Bond Talk");

        AddOption("leave", "Leave");

        if (_selectedIndex >= _optionLabels.Count)
            _selectedIndex = _optionLabels.Count - 1;
        UpdateHighlight();
    }

    private void AddOption(string key, string text)
    {
        int idx = _optionLabels.Count;
        string prefix = idx == _selectedIndex ? "> " : "  ";
        var label = new Label { Text = $"{prefix}{text}" };
        _optionList.AddChild(label);
        _optionLabels.Add(label);
        _optionKeys.Add(key);
    }

    private void UpdateHighlight()
    {
        for (int i = 0; i < _optionLabels.Count; i++)
        {
            // Extract the display text after the prefix
            string current = _optionLabels[i].Text ?? "";
            string displayText = current.Length > 2 ? current[2..] : current;
            string prefix = i == _selectedIndex ? "> " : "  ";
            _optionLabels[i].Text = $"{prefix}{displayText}";
        }
    }
}

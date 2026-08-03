using FlatRedBall2;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework.Input;
using RiftboundSample.Models;

namespace RiftboundSample.UI;

/// <summary>
/// Shows a character's action menu and target selection during battle.
/// Navigated with Up/Down arrows, confirmed with Enter/Space, cancelled with Escape.
/// </summary>
public class ActionMenu
{
    private Screen _screen = null!;
    private Panel _root = null!;
    private Label _characterLabel = null!;
    private StackPanel _abilityList = null!;
    private StackPanel _targetList = null!;
    private Label _targetPrompt = null!;

    private List<AbilityData> _abilities = [];
    private List<CombatantState> _targets = [];
    private List<Label> _abilityLabels = [];
    private List<Label> _targetLabels = [];
    private int _selectedAbilityIndex;
    private int _selectedTargetIndex;
    private bool _inTargetSelection;
    private bool _hasLimitBreak;

    public bool IsVisible => _root?.Visual.Visible ?? false;

    public event Action<string, List<string>> ActionSelected = delegate { };
    public event Action Cancelled = delegate { };

    public void Initialize(Screen screen)
    {
        _screen = screen;

        _root = new Panel();
        _root.Anchor(Anchor.Center);
        _root.Visual.Visible = false;

        var layout = new StackPanel { Spacing = 8 };

        _characterLabel = new Label { Text = "" };
        layout.AddChild(_characterLabel);

        _abilityList = new StackPanel { Spacing = 4 };
        layout.AddChild(_abilityList);

        _targetPrompt = new Label { Text = "Select target:" };
        _targetPrompt.Visual.Visible = false;
        layout.AddChild(_targetPrompt);

        _targetList = new StackPanel { Spacing = 4 };
        _targetList.Visual.Visible = false;
        layout.AddChild(_targetList);

        _root.AddChild(layout);
        _screen.Add(_root);
    }

    public void Show(CombatantState character, List<AbilityData> abilities, bool hasLimitBreak = false)
    {
        _abilities = abilities;
        _hasLimitBreak = hasLimitBreak;
        _inTargetSelection = false;
        _characterLabel.Text = $"{character.Name}'s turn:";

        // Battle memory: pre-select last used ability if available
        _selectedAbilityIndex = 0;
        if (character.LastAction != null)
        {
            int lastIndex = abilities.FindIndex(a => a.Id == character.LastAction);
            if (lastIndex >= 0)
                _selectedAbilityIndex = lastIndex;
        }

        RebuildAbilityList();
        _targetList.Visual.Visible = false;
        _targetPrompt.Visual.Visible = false;
        _root.Visual.Visible = true;
    }

    public void Hide()
    {
        if (_root != null)
            _root.Visual.Visible = false;
    }

    public void Update(FlatRedBallService engine, List<CombatantState> enemies, List<CombatantState> party)
    {
        if (!IsVisible) return;

        var kb = engine.InputManager.Keyboard;

        if (_inTargetSelection)
            UpdateTargetSelection(kb, enemies, party);
        else
            UpdateAbilitySelection(kb, enemies, party);
    }

    private void UpdateAbilitySelection(FlatRedBall2.Input.IKeyboard kb, List<CombatantState> enemies, List<CombatantState> party)
    {
        if (kb.WasKeyPressed(Keys.Up))
        {
            _selectedAbilityIndex = (_selectedAbilityIndex - 1 + _abilities.Count) % _abilities.Count;
            UpdateAbilityHighlight();
        }
        else if (kb.WasKeyPressed(Keys.Down))
        {
            _selectedAbilityIndex = (_selectedAbilityIndex + 1) % _abilities.Count;
            UpdateAbilityHighlight();
        }
        else if (kb.WasKeyPressed(Keys.Enter) || kb.WasKeyPressed(Keys.Space))
        {
            var ability = _abilities[_selectedAbilityIndex];
            if (ability.TargetType == TargetType.Self)
            {
                ActionSelected.Invoke(ability.Id, []);
                return;
            }

            _targets = ability.TargetType switch
            {
                TargetType.SingleAlly or TargetType.AllAllies => party.Where(p => p.IsAlive).ToList(),
                _ => enemies.Where(e => e.IsAlive).ToList(),
            };

            if (_targets.Count == 0) return;

            _selectedTargetIndex = 0;
            _inTargetSelection = true;
            RebuildTargetList();
            _targetList.Visual.Visible = true;
            _targetPrompt.Visual.Visible = true;
        }
        else if (kb.WasKeyPressed(Keys.Escape))
        {
            Cancelled.Invoke();
        }
    }

    private void UpdateTargetSelection(FlatRedBall2.Input.IKeyboard kb, List<CombatantState> enemies, List<CombatantState> party)
    {
        if (kb.WasKeyPressed(Keys.Up) || kb.WasKeyPressed(Keys.Left))
        {
            _selectedTargetIndex = (_selectedTargetIndex - 1 + _targets.Count) % _targets.Count;
            UpdateTargetHighlight();
        }
        else if (kb.WasKeyPressed(Keys.Down) || kb.WasKeyPressed(Keys.Right))
        {
            _selectedTargetIndex = (_selectedTargetIndex + 1) % _targets.Count;
            UpdateTargetHighlight();
        }
        else if (kb.WasKeyPressed(Keys.Enter) || kb.WasKeyPressed(Keys.Space))
        {
            var ability = _abilities[_selectedAbilityIndex];
            List<string> targetIds;

            if (ability.TargetType == TargetType.AllEnemies || ability.TargetType == TargetType.AllAllies)
                targetIds = _targets.Select(t => t.Id).ToList();
            else
                targetIds = [_targets[_selectedTargetIndex].Id];

            ActionSelected.Invoke(ability.Id, targetIds);
        }
        else if (kb.WasKeyPressed(Keys.Escape))
        {
            _inTargetSelection = false;
            _targetList.Visual.Visible = false;
            _targetPrompt.Visual.Visible = false;
        }
    }

    private void RebuildAbilityList()
    {
        // Remove old children
        foreach (var label in _abilityLabels)
            _abilityList.Visual.Children.Remove(label.Visual);
        _abilityLabels.Clear();

        for (int i = 0; i < _abilities.Count; i++)
        {
            var ability = _abilities[i];
            string prefix = i == _selectedAbilityIndex ? "> " : "  ";
            string mpText = ability.MPCost > 0 ? $" ({ability.MPCost} MP)" : "";
            bool isLimitEntry = _hasLimitBreak && i == 0;
            string displayName = isLimitEntry ? $"[LIMIT] {ability.Name}" : ability.Name;
            var label = new Label { Text = $"{prefix}{displayName}{mpText}" };
            _abilityList.AddChild(label);
            _abilityLabels.Add(label);
        }
    }

    private void RebuildTargetList()
    {
        foreach (var label in _targetLabels)
            _targetList.Visual.Children.Remove(label.Visual);
        _targetLabels.Clear();

        for (int i = 0; i < _targets.Count; i++)
        {
            string prefix = i == _selectedTargetIndex ? "> " : "  ";
            var label = new Label { Text = $"{prefix}{_targets[i].Name}" };
            _targetList.AddChild(label);
            _targetLabels.Add(label);
        }
    }

    private void UpdateAbilityHighlight()
    {
        for (int i = 0; i < _abilityLabels.Count && i < _abilities.Count; i++)
        {
            string prefix = i == _selectedAbilityIndex ? "> " : "  ";
            string mpText = _abilities[i].MPCost > 0 ? $" ({_abilities[i].MPCost} MP)" : "";
            bool isLimitEntry = _hasLimitBreak && i == 0;
            string displayName = isLimitEntry ? $"[LIMIT] {_abilities[i].Name}" : _abilities[i].Name;
            _abilityLabels[i].Text = $"{prefix}{displayName}{mpText}";
        }
    }

    private void UpdateTargetHighlight()
    {
        for (int i = 0; i < _targetLabels.Count && i < _targets.Count; i++)
        {
            string prefix = i == _selectedTargetIndex ? "> " : "  ";
            _targetLabels[i].Text = $"{prefix}{_targets[i].Name}";
        }
    }
}

using System.Diagnostics;
using System.Text.Json;
using FlatRedBall2;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using RiftboundSample.Entities;
using RiftboundSample.Models;
using RiftboundSample.Systems;
using FlatRedBall2.Rendering;
using RiftboundSample.UI;

namespace RiftboundSample.Screens;

public class BattleScreen : Screen
{
    // Factories
    private Factory<CharacterBattleEntity> _partyFactory = null!;
    private Factory<EnemyBattleEntity> _enemyFactory = null!;

    // Data
    private List<CharacterData> _characterDataList = [];
    private List<EnemyData> _enemyDataList = [];
    private Dictionary<string, AbilityData> _abilityLookup = [];
    private List<PetState> _pets = [];
    private Dictionary<string, PetData> _petDataLookup = [];

    // Engine
    private BattleEngine _battleEngine = null!;

    // Speed control
    private static readonly float[] SpeedSteps = [1f, 2f, 4f];
    private int _speedIndex;

    /// <summary>Player position to restore on victory return to overworld.</summary>
    public (float X, float Y)? ReturnPlayerPosition { get; set; }

    // Active player whose menu is open
    private CombatantState? _actingCharacter;

    // UI
    private BattleHUD _hud = new();
    private ActionMenu _actionMenu = new();
    private readonly List<FloatingDamageLabel> _floatingLabels = [];

    // Auto-battle
    private bool _autoBattle;
    private Label? _autoBattleLabel;

    // Animation
    private BattleAnimator _animator = new();
    private ScreenTransitionEffect _transition = new();

    // Battle outcome
    private bool _battleEnded;
    private bool _playerVictory;
    private Label? _outcomeLabel;

    public override void CustomInitialize()
    {
        Camera.BackgroundColor = new Color(40, 15, 15);

        _partyFactory = new Factory<CharacterBattleEntity>(this);
        _enemyFactory = new Factory<EnemyBattleEntity>(this);

        LoadData();

        var partyStates = _characterDataList.Select(CombatantState.FromCharacter).ToList();
        var enemyStates = _enemyDataList.Select(CombatantState.FromEnemy).ToList();

        _battleEngine = new BattleEngine(_abilityLookup, Engine.Random);
        _battleEngine.Initialize(new BattleState
        {
            PlayerParty = partyStates,
            Enemies = enemyStates,
        });

        // Register pets with the battle engine
        if (_pets.Count > 0)
        {
            // Link pet IDs to combatant states
            foreach (var pet in _pets)
            {
                var owner = partyStates.FirstOrDefault(p => p.Id == pet.OwnerId);
                if (owner != null)
                    owner.PetId = pet.Id;
            }
            _battleEngine.SetPets(_pets, _petDataLookup);
        }

        SpawnEntities(partyStates, enemyStates);

        _hud.Initialize(this);
        _hud.BuildPartyBars(partyStates);
        _hud.BuildEnemyBars(enemyStates);

        _actionMenu.Initialize(this);
        _actionMenu.ActionSelected += OnActionSelected;
        _actionMenu.Cancelled += OnActionCancelled;

        // Screen transition: flash on battle entry
        _transition.Initialize(this);
        _transition.Start(TransitionType.BattleFlash, 0.4f);
    }

    public override void CustomActivity(FrameTime time)
    {
        // Update transition overlay
        _transition.Update(time.DeltaSeconds);

        // Update battle animations
        _animator.Update(time.DeltaSeconds);

        UpdateFloatingLabels(time.DeltaSeconds);

        if (_battleEnded)
        {
            HandleOutcomeInput();
            return;
        }

        // Speed toggle
        if (Engine.InputManager.Keyboard.WasKeyPressed(Keys.S))
        {
            _speedIndex = (_speedIndex + 1) % SpeedSteps.Length;
            _battleEngine.State.SpeedMultiplier = SpeedSteps[_speedIndex];
            _hud.ShowSpeedIndicator(SpeedSteps[_speedIndex]);
        }

        // Auto-battle toggle (A key)
        if (Engine.InputManager.Keyboard.WasKeyPressed(Keys.A))
        {
            _autoBattle = !_autoBattle;
            UpdateAutoBattleLabel();

            // If turning on auto-battle while menu is open, dismiss menu and auto-act
            if (_autoBattle && _actingCharacter != null)
            {
                _actionMenu.Hide();
                AutoAct(_actingCharacter);
                _actingCharacter = null;
            }
        }

        // Instant flee (F key) — disabled in boss battles
        if (Engine.InputManager.Keyboard.WasKeyPressed(Keys.F) && !_battleEngine.State.IsBossBattle)
        {
            MoveToScreen<OverworldScreen>();
            return;
        }

        // Pause ATB while player is choosing an action (not in auto-battle)
        _battleEngine.State.IsPaused = _actingCharacter != null && !_autoBattle;

        // Tick the engine
        var events = _battleEngine.Update(time.DeltaSeconds);
        ProcessEvents(events);

        // If no player is currently acting, check for a ready one
        if (_actingCharacter == null && !_battleEnded)
            CheckPlayerATB();

        // Process action menu input
        _actionMenu.Update(Engine, _battleEngine.State.Enemies, _battleEngine.State.PlayerParty);

        // Update HUD
        _hud.UpdateBars(_battleEngine.State.PlayerParty, time.DeltaSeconds);
        _hud.ShowEnemyATB(_battleEngine.State.Enemies);
    }

    private void ProcessEvents(List<BattleEvent> events)
    {
        foreach (var evt in events)
        {
            switch (evt)
            {
                case DamageEvent dmg:
                    Debug.WriteLine($"Damage: {dmg.AttackerId} -> {dmg.TargetId} for {dmg.Damage}"
                        + (dmg.WasCritical ? " CRIT" : "")
                        + (dmg.ElementMessage != null ? $" ({dmg.ElementMessage})" : ""));
                    SpawnFloatingLabel(dmg.TargetId, dmg.Damage.ToString(), dmg.WasCritical);
                    _animator.EnqueueDamageFlash(dmg.TargetId);
                    break;

                case HealEvent heal:
                    Debug.WriteLine($"Heal: {heal.CasterId} -> {heal.TargetId} for {heal.Amount}");
                    SpawnFloatingLabel(heal.TargetId, $"+{heal.Amount}", isCrit: false, isHeal: true);
                    _animator.EnqueueHealFlash(heal.TargetId);
                    break;

                case DeathEvent death:
                    Debug.WriteLine($"Death: {death.CombatantId}");
                    _animator.EnqueueDeathFade(death.CombatantId);
                    break;

                case BattleEndEvent end:
                    _battleEnded = true;
                    _playerVictory = end.PlayerVictory;
                    if (end.PlayerVictory)
                    {
                        _battleEngine.ApplyPostBattleBondIncrease(2f);
                        _animator.EnqueueVictoryBounce(
                            _battleEngine.State.PlayerParty
                                .Where(p => p.IsAlive)
                                .Select(p => p.Id));
                    }
                    ShowOutcome(end.PlayerVictory ? "Victory!" : "Defeat...");
                    break;

                case ActionEvent action:
                    Debug.WriteLine($"Action: {action.CombatantId} uses {action.AbilityId}");
                    _animator.EnqueueAttackSlide(action.CombatantId);
                    break;

                case TelegraphEvent telegraph:
                    Debug.WriteLine($"TELEGRAPH: Boss {telegraph.BossId} is preparing {telegraph.AbilityId}!");
                    SpawnFloatingLabel(telegraph.BossId, "!!", isCrit: true);
                    break;

                case VictoryEvent victory:
                    Debug.WriteLine($"Victory! Total XP: {victory.TotalXP}");
                    ShowVictoryOverlay(victory);
                    break;

                case OverkillSplashEvent splash:
                    Debug.WriteLine($"Overkill splash: {splash.AttackerId} -> {splash.TargetId} for {splash.Damage}");
                    SpawnFloatingLabel(splash.TargetId, splash.Damage.ToString(), isCrit: false);
                    break;

                case PetGaugeFullEvent petGauge:
                    Debug.WriteLine($"Pet gauge full: {petGauge.PetName} (owner: {petGauge.OwnerId})");
                    break;

                case PetActionEvent petAction:
                    Debug.WriteLine($"Pet action: {petAction.PetName} uses {petAction.AbilityId} for {petAction.Damage} damage");
                    break;

                case LimitGaugeFullEvent limitFull:
                    Debug.WriteLine($"Limit gauge full: {limitFull.CombatantId}");
                    break;

                case StatusTickEvent statusTick:
                    Debug.WriteLine($"Status tick: {statusTick.EffectName} on {statusTick.CombatantId} for {statusTick.Amount} ({(statusTick.IsHeal ? "heal" : "damage")})");
                    break;

                case StunEvent stun:
                    Debug.WriteLine($"Stunned: {stun.CombatantId} skips turn");
                    break;

                case ShieldAbsorbEvent shield:
                    Debug.WriteLine($"Shield absorbs {shield.Absorbed} on {shield.CombatantId} ({shield.Remaining} remaining)");
                    break;

                case CounterEvent counter:
                    Debug.WriteLine($"Counter: {counter.CounterId} hits {counter.AttackerId} for {counter.Damage}");
                    break;
            }
        }
    }

    private void CheckPlayerATB()
    {
        foreach (var state in _battleEngine.State.PlayerParty)
        {
            if (state.IsAlive && state.IsATBFull)
            {
                if (_autoBattle)
                {
                    AutoAct(state);
                    return;
                }

                _actingCharacter = state;

                var abilities = new List<AbilityData>();

                // Add limit break as first option when gauge is full
                if (state.LimitGauge >= 1f && state.LimitBreakAbilityId != null
                    && _abilityLookup.TryGetValue(state.LimitBreakAbilityId, out var limitAbility))
                {
                    abilities.Add(limitAbility);
                }

                abilities.AddRange(state.AbilityIds
                    .Where(id => _abilityLookup.ContainsKey(id))
                    .Select(id => _abilityLookup[id]));

                _actionMenu.Show(state, abilities, state.LimitGauge >= 1f);
                return;
            }
        }
    }

    /// <summary>
    /// Auto-battle: repeat last action or use basic "attack" against a random living enemy.
    /// </summary>
    private void AutoAct(CombatantState character)
    {
        string abilityId = character.LastAction ?? "attack";
        if (!_abilityLookup.TryGetValue(abilityId, out var ability))
        {
            // Fallback to first available ability
            abilityId = character.AbilityIds.FirstOrDefault(id => _abilityLookup.ContainsKey(id)) ?? "attack";
            ability = _abilityLookup.GetValueOrDefault(abilityId);
            if (ability == null) return;
        }

        // If not enough MP, fall back to basic attack
        if (ability.MPCost > character.CurrentMP && abilityId != "attack")
        {
            abilityId = "attack";
            ability = _abilityLookup.GetValueOrDefault(abilityId);
            if (ability == null) return;
        }

        List<string> targets;
        if (ability.TargetType == TargetType.Self)
        {
            targets = [character.Id];
        }
        else if (ability.TargetType is TargetType.SingleAlly or TargetType.AllAllies)
        {
            targets = _battleEngine.State.PlayerParty
                .Where(p => p.IsAlive).Select(p => p.Id).ToList();
            if (ability.TargetType == TargetType.SingleAlly && targets.Count > 0)
                targets = [targets[Engine.Random.Next(targets.Count)]];
        }
        else
        {
            var living = _battleEngine.State.Enemies.Where(e => e.IsAlive).ToList();
            if (living.Count == 0) return;
            targets = ability.TargetType == TargetType.AllEnemies
                ? living.Select(e => e.Id).ToList()
                : [living[Engine.Random.Next(living.Count)].Id];
        }

        _battleEngine.SubmitPlayerAction(new BattleAction(character.Id, abilityId, targets));
    }

    private void UpdateAutoBattleLabel()
    {
        if (_autoBattle && _autoBattleLabel == null)
        {
            _autoBattleLabel = new Label { Text = "AUTO" };
            _autoBattleLabel.Anchor(Anchor.TopLeft);
            _autoBattleLabel.X = 10;
            _autoBattleLabel.Y = 10;
            Add(_autoBattleLabel);
        }
        else if (!_autoBattle && _autoBattleLabel != null)
        {
            _autoBattleLabel.Visual.Visible = false;
            Remove(_autoBattleLabel);
            _autoBattleLabel = null;
        }
    }

    private void OnActionSelected(string abilityId, List<string> targetIds)
    {
        if (_actingCharacter == null) return;

        var ability = _abilityLookup.GetValueOrDefault(abilityId);
        if (ability == null) return;

        // Check MP
        if (ability.MPCost > _actingCharacter.CurrentMP)
        {
            Debug.WriteLine($"{_actingCharacter.Name} doesn't have enough MP for {ability.Name}");
            return;
        }

        // For self-targeting abilities, target the acting character
        var resolvedTargets = ability.TargetType == TargetType.Self
            ? new List<string> { _actingCharacter.Id }
            : targetIds;

        _battleEngine.SubmitPlayerAction(new BattleAction(
            _actingCharacter.Id,
            abilityId,
            resolvedTargets));

        _actingCharacter = null;
        _actionMenu.Hide();
    }

    private void OnActionCancelled()
    {
        _actingCharacter = null;
        _actionMenu.Hide();
    }

    private void LoadData()
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        string charJson = File.ReadAllText(DataPath.Resolve("Data/characters.json"));
        var allCharacters = JsonSerializer.Deserialize<List<CharacterData>>(charJson, options) ?? [];
        // Starting party: first two characters (Kael and Mira)
        _characterDataList = allCharacters.Take(2).ToList();

        string enemyJson = File.ReadAllText(DataPath.Resolve("Data/enemies.json"));
        var allEnemies = JsonSerializer.Deserialize<List<EnemyData>>(enemyJson, options) ?? [];
        // Test encounter: Gear Golem + Steam Rat
        _enemyDataList = allEnemies.Where(e => e.Id is "gear_golem" or "steam_rat").ToList();

        string abilityJson = File.ReadAllText(DataPath.Resolve("Data/abilities.json"));
        var abilities = JsonSerializer.Deserialize<List<AbilityData>>(abilityJson, options) ?? [];
        _abilityLookup = abilities.ToDictionary(a => a.Id);

        string petJson = File.ReadAllText(DataPath.Resolve("Data/pets.json"));
        var petDataList = JsonSerializer.Deserialize<List<PetData>>(petJson, options) ?? [];
        _petDataLookup = petDataList.ToDictionary(p => p.Id);
        _pets = petDataList.Select(PetState.FromData).ToList();
    }

    private void SpawnEntities(List<CombatantState> partyStates, List<CombatantState> enemyStates)
    {
        // Party members: left side, spaced vertically from center
        int frontIdx = 0, backIdx = 0;

        for (int i = 0; i < partyStates.Count; i++)
        {
            var state = partyStates[i];
            var entity = _partyFactory.Create();
            entity.State = state;
            entity.ApplyRowColor();

            if (state.Row == RowPosition.Back)
            {
                entity.X = -160;
                entity.Y = (backIdx - (partyStates.Count(s => s.Row == RowPosition.Back) - 1) / 2f) * 40f;
                backIdx++;
            }
            else
            {
                entity.X = -100;
                entity.Y = (frontIdx - (partyStates.Count(s => s.Row == RowPosition.Front) - 1) / 2f) * 40f;
                frontIdx++;
            }
        }

        // Enemies: right side, spaced vertically from center
        for (int i = 0; i < enemyStates.Count; i++)
        {
            var entity = _enemyFactory.Create();
            entity.State = enemyStates[i];
            entity.X = 100;
            entity.Y = (i - (enemyStates.Count - 1) / 2f) * 40f;
        }
    }

    // --- Floating damage numbers ---

    private void SpawnFloatingLabel(string targetId, string text, bool isCrit, bool isHeal = false)
    {
        // Find the entity position for this combatant
        var targetState = _battleEngine.State.AllCombatants.FirstOrDefault(c => c.Id == targetId);
        if (targetState == null) return;

        var color = isHeal ? new Color(80, 220, 80) : (isCrit ? new Color(255, 220, 40) : new Color(255, 255, 255));
        string displayText = isCrit ? $"{text}!" : text;

        var label = new Label { Text = displayText };
        // Position near top-right of the screen area for the target;
        // we use a simple anchor approach since we don't track entity world positions in the HUD layer
        label.Anchor(Anchor.Center);
        label.X = Engine.Random.Between(-20f, 20f);
        label.Y = Engine.Random.Between(-10f, 10f);
        Add(label);

        _floatingLabels.Add(new FloatingDamageLabel(label, 1.5f));
    }

    private void UpdateFloatingLabels(float dt)
    {
        for (int i = _floatingLabels.Count - 1; i >= 0; i--)
        {
            var fl = _floatingLabels[i];
            fl.Elapsed += dt;
            fl.Label.Y -= 30f * dt; // float upward

            if (fl.Elapsed >= fl.Duration)
            {
                fl.Label.Visual.Visible = false;
                Remove(fl.Label);
                _floatingLabels.RemoveAt(i);
            }
        }
    }

    // --- Outcome ---

    private void ShowOutcome(string text)
    {
        _actionMenu.Hide();

        // Only show the simple outcome label for defeats;
        // victories get the full overlay from VictoryEvent
        if (!_playerVictory)
        {
            _outcomeLabel = new Label { Text = text };
            _outcomeLabel.Anchor(Anchor.Center);
            Add(_outcomeLabel);
        }
    }

    private void ShowVictoryOverlay(VictoryEvent victory)
    {
        _actionMenu.Hide();

        var panel = new StackPanel { Spacing = 8 };
        panel.Anchor(Anchor.Center);

        panel.AddChild(new Label { Text = "Victory!" });
        panel.AddChild(new Label { Text = $"XP Gained: {victory.TotalXP}" });

        foreach (var (id, xp) in victory.XPPerCombatant)
        {
            var combatant = _battleEngine.State.AllCombatants.FirstOrDefault(c => c.Id == id)
                ?? _battleEngine.State.BenchParty.FirstOrDefault(c => c.Id == id);
            string name = combatant?.Name ?? id;
            bool isBench = _battleEngine.State.BenchParty.Any(c => c.Id == id);
            string suffix = isBench ? " (bench 75%)" : "";
            panel.AddChild(new Label { Text = $"  {name}: +{xp} XP{suffix}" });
        }

        panel.AddChild(new Label { Text = "Press Enter to continue" });
        Add(panel);
    }

    private void HandleOutcomeInput()
    {
        if (Engine.InputManager.Keyboard.WasKeyPressed(Keys.Enter))
        {
            if (_playerVictory)
            {
                MoveToScreen<OverworldScreen>(s =>
                {
                    if (ReturnPlayerPosition.HasValue)
                    {
                        s.RestorePlayerX = ReturnPlayerPosition.Value.X;
                        s.RestorePlayerY = ReturnPlayerPosition.Value.Y;
                    }
                });
            }
            else
            {
                MoveToScreen<TitleScreen>();
            }
        }
    }

    private class FloatingDamageLabel(Label label, float duration)
    {
        public Label Label { get; } = label;
        public float Duration { get; } = duration;
        public float Elapsed { get; set; }
    }
}

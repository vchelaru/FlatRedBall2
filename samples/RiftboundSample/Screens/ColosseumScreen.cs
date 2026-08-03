using System.Diagnostics;
using System.Text.Json;
using FlatRedBall2;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using RiftboundSample.Models;
using RiftboundSample.Systems;
using RiftboundSample.UI;

namespace RiftboundSample.Screens;

/// <summary>
/// Wave-based combat mode. Generates increasingly difficult enemy waves,
/// with boss encounters every 5 waves.
/// </summary>
public class ColosseumScreen : Screen
{
    private ColosseumState _state = new();
    private ColosseumWaveGenerator _waveGen = null!;
    private ColosseumHUD _hud = new();
    private BattleEngine _battleEngine = null!;

    // Data
    private List<CharacterData> _characterDataList = [];
    private List<EnemyData> _allEnemies = [];
    private Dictionary<string, AbilityData> _abilityLookup = [];

    // Phase tracking
    private enum Phase { PreWave, Battle, BetweenWave, Summary }
    private Phase _phase = Phase.PreWave;

    // Battle UI (reuse from BattleScreen patterns)
    private BattleHUD _battleHud = new();
    private ActionMenu _actionMenu = new();
    private CombatantState? _actingCharacter;

    public override void CustomInitialize()
    {
        Camera.BackgroundColor = new Color(30, 10, 10);
        LoadData();

        _waveGen = new ColosseumWaveGenerator(_allEnemies, Engine.Random);
        _battleEngine = new BattleEngine(_abilityLookup, Engine.Random);

        _hud.Initialize(this);
        _hud.ContinueSelected += OnContinue;
        _hud.LeaveSelected += OnLeave;
        _hud.SummaryDismissed += () => MoveToScreen<OverworldScreen>();

        _battleHud.Initialize(this);
        _actionMenu.Initialize(this);
        _actionMenu.ActionSelected += OnActionSelected;
        _actionMenu.Cancelled += OnActionCancelled;

        StartNextWave();
    }

    public override void CustomActivity(FrameTime time)
    {
        switch (_phase)
        {
            case Phase.Battle:
                UpdateBattle(time);
                break;
            case Phase.BetweenWave:
                _hud.UpdateBetweenWaveInput(Engine);
                break;
            case Phase.Summary:
                _hud.UpdateSummaryInput(Engine);
                break;
        }
    }

    private void UpdateBattle(FrameTime time)
    {
        _battleEngine.State.IsPaused = _actingCharacter != null;

        var events = _battleEngine.Update(time.DeltaSeconds);
        foreach (var evt in events)
        {
            switch (evt)
            {
                case BattleEndEvent end:
                    if (end.PlayerVictory)
                        OnWaveVictory();
                    else
                        OnDefeat();
                    return;

                case DamageEvent dmg:
                    Debug.WriteLine($"Colosseum: {dmg.AttackerId} -> {dmg.TargetId} for {dmg.Damage}");
                    break;
            }
        }

        // Check for ready player
        if (_actingCharacter == null)
            CheckPlayerATB();

        _actionMenu.Update(Engine, _battleEngine.State.Enemies, _battleEngine.State.PlayerParty);
        _battleHud.UpdateBars(_battleEngine.State.PlayerParty, time.DeltaSeconds);
    }

    private void StartNextWave()
    {
        _state.CurrentWave++;
        if (_state.CurrentWave > _state.HighestWave)
            _state.HighestWave = _state.CurrentWave;

        var enemies = _waveGen.GenerateWave(_state.CurrentWave);
        var partyStates = _characterDataList.Select(CombatantState.FromCharacter).ToList();
        var enemyStates = enemies.Select(CombatantState.FromEnemy).ToList();

        _battleEngine.Initialize(new BattleState
        {
            PlayerParty = partyStates,
            Enemies = enemyStates,
            IsBossBattle = _state.CurrentWave % 5 == 0,
        });

        _hud.UpdateWaveInfo(_state);
        _battleHud.BuildPartyBars(partyStates);
        _battleHud.BuildEnemyBars(enemyStates);
        _phase = Phase.Battle;
        _actingCharacter = null;
    }

    private void OnWaveVictory()
    {
        int xp = _waveGen.GetWaveXP(_state.CurrentWave);
        int gold = _waveGen.GetWaveGold(_state.CurrentWave);
        _state.TotalXPEarned += xp;
        _state.TotalGoldEarned += gold;
        _hud.UpdateWaveInfo(_state);

        _phase = Phase.BetweenWave;
        _hud.ShowBetweenWave();
    }

    private void OnDefeat()
    {
        _phase = Phase.Summary;
        _hud.ShowSummary(_state, defeated: true);
    }

    private void OnContinue()
    {
        _actionMenu.Hide();
        StartNextWave();
    }

    private void OnLeave()
    {
        _phase = Phase.Summary;
        _hud.ShowSummary(_state, defeated: false);
    }

    private void CheckPlayerATB()
    {
        foreach (var state in _battleEngine.State.PlayerParty)
        {
            if (state.IsAlive && state.IsATBFull)
            {
                _actingCharacter = state;
                var abilities = state.AbilityIds
                    .Where(id => _abilityLookup.ContainsKey(id))
                    .Select(id => _abilityLookup[id])
                    .ToList();
                _actionMenu.Show(state, abilities, false);
                return;
            }
        }
    }

    private void OnActionSelected(string abilityId, List<string> targetIds)
    {
        if (_actingCharacter == null) return;
        var ability = _abilityLookup.GetValueOrDefault(abilityId);
        if (ability == null) return;

        var resolvedTargets = ability.TargetType == TargetType.Self
            ? new List<string> { _actingCharacter.Id }
            : targetIds;

        _battleEngine.SubmitPlayerAction(new BattleAction(_actingCharacter.Id, abilityId, resolvedTargets));
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
        _characterDataList = JsonSerializer.Deserialize<List<CharacterData>>(charJson, options) ?? [];

        string enemyJson = File.ReadAllText(DataPath.Resolve("Data/enemies.json"));
        _allEnemies = JsonSerializer.Deserialize<List<EnemyData>>(enemyJson, options) ?? [];

        string abilityJson = File.ReadAllText(DataPath.Resolve("Data/abilities.json"));
        var abilities = JsonSerializer.Deserialize<List<AbilityData>>(abilityJson, options) ?? [];
        _abilityLookup = abilities.ToDictionary(a => a.Id);
    }
}

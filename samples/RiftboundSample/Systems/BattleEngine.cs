using RiftboundSample.Models;

namespace RiftboundSample.Systems;

public class BattleEngine
{
    private BattleState _state = new();
    private ATBSystem _atb = new();
    private DamageCalculator _damage;
    private EnemyAI _ai;
    private readonly Random _random;
    private readonly Dictionary<string, AbilityData> _abilityLookup;
    private readonly Queue<BattleAction> _playerActionQueue = new();

    /// <summary>Tracks which HP thresholds each boss has already telegraphed.</summary>
    private readonly Dictionary<string, HashSet<float>> _bossTelegraphsSent = new();

    // Pet support
    private Dictionary<string, PetState> _petsByOwner = [];
    private Dictionary<string, PetData> _petDataLookup = [];

    public BattleState State => _state;
    public IReadOnlyDictionary<string, PetState> PetsByOwner => _petsByOwner;

    public BattleEngine(Dictionary<string, AbilityData> abilityLookup, Random? random = null)
    {
        _abilityLookup = abilityLookup;
        _random = random ?? Random.Shared;
        _damage = new DamageCalculator(_random);
        _ai = new EnemyAI(abilityLookup, _random);
    }

    /// <summary>
    /// Registers pets for battle. Call after Initialize.
    /// </summary>
    public void SetPets(List<PetState> pets, Dictionary<string, PetData> petDataLookup)
    {
        _petDataLookup = petDataLookup;
        _petsByOwner = [];
        foreach (var pet in pets)
        {
            if (pet.IsAlive)
                _petsByOwner[pet.OwnerId] = pet;
        }
    }

    public void Initialize(BattleState state)
    {
        _state = state;
        _atb = new ATBSystem { SpeedMultiplier = state.SpeedMultiplier };
        _playerActionQueue.Clear();

        foreach (var c in state.AllCombatants)
        {
            c.ATB = 0f;
            _atb.AddCombatant(c);
        }
    }

    /// <summary>
    /// Advances the battle by deltaSeconds. Returns events generated this frame.
    /// Enemy actions are resolved automatically; player actions come from SubmitPlayerAction.
    /// </summary>
    public List<BattleEvent> Update(float deltaSeconds)
    {
        var events = new List<BattleEvent>();

        if (_state.IsVictory || _state.IsDefeat)
            return events;

        _atb.SetPaused(_state.IsPaused);
        _atb.SpeedMultiplier = _state.SpeedMultiplier;
        _atb.Update(deltaSeconds);

        // Advance pet gauges for living player combatants
        if (!_state.IsPaused)
            events.AddRange(UpdatePetGauges(deltaSeconds));

        // Process player actions from queue
        while (_playerActionQueue.Count > 0)
        {
            var action = _playerActionQueue.Dequeue();
            events.AddRange(ExecuteAction(action));

            if (_state.IsVictory || _state.IsDefeat)
            {
                events.AddRange(EmitBattleEnd());
                return events;
            }
        }

        // Process ready enemies
        var ready = _atb.GetReadyCombatants();
        foreach (var combatant in ready)
        {
            if (!combatant.IsAlive)
                continue;

            // Process status effects at turn start
            var statusEvents = ProcessStatusEffects(combatant);
            events.AddRange(statusEvents);
            if (!combatant.IsAlive) continue;

            // Skip turn if stunned
            if (statusEvents.Any(e => e is StunEvent))
            {
                _atb.ResetGauge(combatant.Id);
                continue;
            }

            // Only auto-act for enemies
            if (combatant.IsPlayer)
                continue;

            var action = _ai.DecideAction(combatant, _state);
            events.AddRange(ExecuteAction(action));

            if (_state.IsVictory || _state.IsDefeat)
            {
                events.AddRange(EmitBattleEnd());
                return events;
            }
        }

        // Update limit gauges for critically low HP characters
        if (!_state.IsPaused)
            events.AddRange(UpdateLimitGauges(deltaSeconds));

        // Check boss telegraph thresholds after damage has been applied
        events.AddRange(CheckBossTelegraphs());

        return events;
    }

    /// <summary>
    /// Queues a player action for execution on the next Update.
    /// </summary>
    public void SubmitPlayerAction(BattleAction action)
    {
        _playerActionQueue.Enqueue(action);
    }

    /// <summary>
    /// Resolves a single action: applies damage/healing, deducts MP, resets gauge, checks deaths.
    /// Includes smart retargeting, overkill splash, LastAction tracking, and 1% MP regen.
    /// </summary>
    public List<BattleEvent> ExecuteAction(BattleAction action)
    {
        var events = new List<BattleEvent>();
        var actor = FindCombatant(action.CombatantId);
        if (actor == null || !actor.IsAlive)
            return events;

        if (!_abilityLookup.TryGetValue(action.AbilityId, out var ability))
            return events;

        // Track last action for auto-battle and menu memory
        actor.LastAction = action.AbilityId;

        events.Add(new ActionEvent(action.CombatantId, action.AbilityId, action.TargetIds));

        // Consume limit gauge if this is a limit break
        bool isLimitBreak = actor.LimitBreakAbilityId != null
            && action.AbilityId == actor.LimitBreakAbilityId;
        if (isLimitBreak)
            actor.LimitGauge = 0f;

        // Deduct MP
        actor.CurrentMP = Math.Max(0, actor.CurrentMP - ability.MPCost);

        // Reset ATB gauge
        _atb.ResetGauge(action.CombatantId);
        actor.IsDefending = false;

        // Self-targeting defend: set defending flag and return early
        if (ability.TargetType == TargetType.Self && ability.Id == "defend")
        {
            actor.IsDefending = true;
            ApplyMPRegen(actor);
            return events;
        }

        foreach (var targetId in action.TargetIds)
        {
            var target = FindCombatant(targetId);

            // Smart retargeting: if target is dead, pick a random living enemy
            if (target == null || !target.IsAlive)
            {
                target = GetRetarget(actor);
                if (target == null)
                    continue;
            }

            if (ability.DamageType == DamageType.Healing)
            {
                var result = _damage.CalculateHealing(actor, ability);
                target.CurrentHP = Math.Min(target.MaxHP, target.CurrentHP + result.Amount);
                events.Add(new HealEvent(actor.Id, target.Id, result.Amount));
            }
            else if (ability.DamageType == DamageType.Physical || ability.DamageType == DamageType.Magical)
            {
                var result = _damage.Calculate(actor, target, ability);
                int finalDamage = ApplyShieldAbsorption(target, result.Amount, events);
                int hpBefore = target.CurrentHP;
                target.CurrentHP = Math.Max(0, target.CurrentHP - finalDamage);
                events.Add(new DamageEvent(actor.Id, target.Id, finalDamage, result.WasCritical, result.ElementMessage));

                // Fill limit gauge when a player character takes damage
                FillLimitGaugeFromDamage(target.Id, finalDamage);

                if (!target.IsAlive)
                {
                    events.Add(new DeathEvent(target.Id));

                    // Fill allies' limit gauges when a player ally dies
                    if (target.IsPlayer)
                        FillLimitGaugeFromAllyDeath(target.Id);

                    // Overkill splash: excess damage at 50% to another living enemy
                    int excess = finalDamage - hpBefore;
                    if (excess > 0)
                    {
                        var splashTarget = GetRetarget(actor);
                        if (splashTarget != null)
                        {
                            int splashDamage = Math.Max(1, excess / 2);
                            splashTarget.CurrentHP = Math.Max(0, splashTarget.CurrentHP - splashDamage);
                            events.Add(new OverkillSplashEvent(actor.Id, splashTarget.Id, splashDamage));

                            if (!splashTarget.IsAlive)
                                events.Add(new DeathEvent(splashTarget.Id));
                        }
                    }
                }
            }
        }

        // 1% MP regen after acting (rounded up, minimum 1)
        ApplyMPRegen(actor);

        return events;
    }

    /// <summary>
    /// Checks boss HP thresholds and emits TelegraphEvents for upcoming attacks.
    /// Called by EnemyAI integration — checks thresholds once per crossing.
    /// </summary>
    public List<BattleEvent> CheckBossTelegraphs()
    {
        var events = new List<BattleEvent>();
        foreach (var enemy in _state.Enemies)
        {
            if (!enemy.IsBoss || !enemy.IsAlive)
                continue;

            if (!_bossTelegraphsSent.ContainsKey(enemy.Id))
                _bossTelegraphsSent[enemy.Id] = [];

            float hpPercent = (float)enemy.CurrentHP / enemy.MaxHP;
            foreach (float threshold in new[] { 0.75f, 0.50f, 0.25f })
            {
                if (hpPercent <= threshold && _bossTelegraphsSent[enemy.Id].Add(threshold))
                {
                    // Find strongest offensive ability for telegraph
                    var strongest = enemy.AbilityIds
                        .Where(id => _abilityLookup.ContainsKey(id))
                        .Select(id => _abilityLookup[id])
                        .Where(a => a.DamageType is DamageType.Physical or DamageType.Magical)
                        .OrderByDescending(a => a.Multiplier)
                        .FirstOrDefault();

                    if (strongest != null)
                        events.Add(new TelegraphEvent(enemy.Id, strongest.Id));
                }
            }
        }
        return events;
    }

    /// <summary>
    /// Handles victory: restores MP to max for living party, distributes XP.
    /// Returns a VictoryEvent with XP distribution.
    /// </summary>
    public VictoryEvent HandleVictory()
    {
        // Restore all living party members' MP to max
        foreach (var member in _state.PlayerParty)
        {
            if (member.IsAlive)
                member.CurrentMP = member.MaxMP;
        }

        // Calculate total XP from defeated enemies
        int totalXP = _state.Enemies.Sum(e => e.XPReward);

        // Distribute: active party gets 100%, bench gets 75%
        var xpDistribution = new Dictionary<string, int>();
        foreach (var member in _state.PlayerParty)
        {
            if (member.IsAlive)
                xpDistribution[member.Id] = totalXP;
        }
        foreach (var bench in _state.BenchParty)
        {
            xpDistribution[bench.Id] = (int)(totalXP * 0.75f);
        }

        return new VictoryEvent(totalXP, xpDistribution);
    }

    private List<BattleEvent> EmitBattleEnd()
    {
        var events = new List<BattleEvent>();
        events.Add(new BattleEndEvent(_state.IsVictory));
        if (_state.IsVictory)
            events.Add(HandleVictory());
        return events;
    }

    private static void ApplyMPRegen(CombatantState actor)
    {
        if (actor.MaxMP > 0)
        {
            int regen = Math.Max(1, (int)Math.Ceiling(actor.MaxMP * 0.01));
            actor.CurrentMP = Math.Min(actor.MaxMP, actor.CurrentMP + regen);
        }
    }

    /// <summary>
    /// Returns a random living enemy from the opposing side, or null if none remain.
    /// </summary>
    private CombatantState? GetRetarget(CombatantState actor)
    {
        var candidates = actor.IsPlayer
            ? _state.Enemies.Where(e => e.IsAlive).ToList()
            : _state.PlayerParty.Where(p => p.IsAlive).ToList();

        return candidates.Count > 0
            ? candidates[_random.Next(candidates.Count)]
            : null;
    }

    /// <summary>
    /// Increases Bond by the given amount for each pet whose owner was in the active party.
    /// Call after battle victory.
    /// </summary>
    public void ApplyPostBattleBondIncrease(float amount)
    {
        foreach (var member in _state.PlayerParty)
        {
            if (_petsByOwner.TryGetValue(member.Id, out var pet) && pet.IsAlive)
                pet.Bond = Math.Min(100, pet.Bond + amount);
        }
    }

    private List<BattleEvent> UpdatePetGauges(float deltaSeconds)
    {
        var events = new List<BattleEvent>();

        foreach (var member in _state.PlayerParty)
        {
            if (!member.IsAlive) continue;
            if (!_petsByOwner.TryGetValue(member.Id, out var pet)) continue;
            if (!pet.IsAlive) continue;

            float fillRate = 0.05f * (1 + pet.Bond / 100f) * _state.SpeedMultiplier;
            member.PetGauge = Math.Min(1f, member.PetGauge + fillRate * deltaSeconds);

            if (member.PetGauge >= 1f)
            {
                events.Add(new PetGaugeFullEvent(member.Id, pet.Name));
                events.AddRange(ExecutePetAction(member, pet));
            }
        }

        return events;
    }

    private List<BattleEvent> ExecutePetAction(CombatantState owner, PetState pet)
    {
        var events = new List<BattleEvent>();

        if (!_petDataLookup.TryGetValue(pet.Id, out var petData))
            return events;

        // Select ability list based on current tier
        var abilityList = pet.CurrentTier switch
        {
            PetTier.Ultimate => petData.Abilities.Ultimate,
            PetTier.Advanced => petData.Abilities.Advanced,
            _ => petData.Abilities.Basic,
        };

        if (abilityList.Count == 0)
            return events;

        string abilityId = abilityList[0];
        if (!_abilityLookup.TryGetValue(abilityId, out var ability))
            return events;

        // Pick targets
        var targets = ability.TargetType switch
        {
            TargetType.AllEnemies => _state.Enemies.Where(e => e.IsAlive).ToList(),
            TargetType.SingleEnemy => _state.Enemies.Where(e => e.IsAlive).Take(1).ToList(),
            _ => [],
        };

        int totalDamage = 0;
        foreach (var target in targets)
        {
            DamageResult result;
            if (ability.Id == "overdrive")
                result = _damage.CalculateIgnoringDefense(owner, ability);
            else
                result = _damage.Calculate(owner, target, ability);

            target.CurrentHP = Math.Max(0, target.CurrentHP - result.Amount);
            totalDamage += result.Amount;

            events.Add(new DamageEvent(owner.Id, target.Id, result.Amount, result.WasCritical, result.ElementMessage));

            if (!target.IsAlive)
                events.Add(new DeathEvent(target.Id));
        }

        events.Add(new PetActionEvent(owner.Id, pet.Name, abilityId, totalDamage));

        // Reset gauge
        owner.PetGauge = 0f;

        return events;
    }

    /// <summary>
    /// Fills limit gauge for critically low HP characters (+0.1 per tick when HP below 25%).
    /// </summary>
    private List<BattleEvent> UpdateLimitGauges(float deltaSeconds)
    {
        var events = new List<BattleEvent>();
        foreach (var member in _state.PlayerParty)
        {
            if (!member.IsAlive || member.LimitBreakAbilityId == null) continue;

            float hpPct = (float)member.CurrentHP / member.MaxHP;
            if (hpPct <= 0.25f && member.LimitGauge < 1f)
            {
                float oldGauge = member.LimitGauge;
                member.LimitGauge = Math.Min(1f, member.LimitGauge + 0.1f * deltaSeconds);
                if (oldGauge < 1f && member.LimitGauge >= 1f)
                    events.Add(new LimitGaugeFullEvent(member.Id));
            }
        }
        return events;
    }

    /// <summary>
    /// Called after damage is dealt to fill the target's limit gauge.
    /// Fills by (damage / maxHP * 0.5).
    /// </summary>
    public void FillLimitGaugeFromDamage(string targetId, int damage)
    {
        var target = FindCombatant(targetId);
        if (target == null || !target.IsPlayer || target.LimitBreakAbilityId == null) return;

        float fill = (float)damage / target.MaxHP * 0.5f;
        target.LimitGauge = Math.Min(1f, target.LimitGauge + fill);
    }

    /// <summary>
    /// Called when an ally dies to fill all living allies' limit gauges by 0.25.
    /// </summary>
    public void FillLimitGaugeFromAllyDeath(string deadAllyId)
    {
        foreach (var member in _state.PlayerParty)
        {
            if (!member.IsAlive || member.Id == deadAllyId) continue;
            if (member.LimitBreakAbilityId == null) continue;
            member.LimitGauge = Math.Min(1f, member.LimitGauge + 0.25f);
        }
    }

    /// <summary>
    /// Processes status effects at the start of a combatant's turn.
    /// Returns events for DoT, HoT, and stun. Decrements remaining turns.
    /// </summary>
    public List<BattleEvent> ProcessStatusEffects(CombatantState combatant)
    {
        var events = new List<BattleEvent>();

        for (int i = combatant.StatusEffects.Count - 1; i >= 0; i--)
        {
            var effect = combatant.StatusEffects[i];

            switch (effect.Type)
            {
                case StatusEffectType.DamageOverTime:
                    combatant.CurrentHP = Math.Max(0, combatant.CurrentHP - effect.Amount);
                    events.Add(new StatusTickEvent(combatant.Id, effect.Name, effect.Amount, false));
                    if (!combatant.IsAlive)
                        events.Add(new DeathEvent(combatant.Id));
                    break;

                case StatusEffectType.HealOverTime:
                    combatant.CurrentHP = Math.Min(combatant.MaxHP, combatant.CurrentHP + effect.Amount);
                    events.Add(new StatusTickEvent(combatant.Id, effect.Name, effect.Amount, true));
                    break;

                case StatusEffectType.Stun:
                    events.Add(new StunEvent(combatant.Id));
                    break;
            }

            if (effect.RemainingTurns > 0)
            {
                effect.RemainingTurns--;
                if (effect.RemainingTurns <= 0)
                    combatant.StatusEffects.RemoveAt(i);
            }
        }

        return events;
    }

    /// <summary>
    /// Applies shield absorption to incoming damage. Returns the reduced damage amount.
    /// </summary>
    public int ApplyShieldAbsorption(CombatantState target, int damage, List<BattleEvent> events)
    {
        for (int i = target.StatusEffects.Count - 1; i >= 0; i--)
        {
            var effect = target.StatusEffects[i];
            if (effect.Type != StatusEffectType.Shield || damage <= 0) continue;

            int absorbed = Math.Min(damage, effect.Amount);
            effect.Amount -= absorbed;
            damage -= absorbed;
            events.Add(new ShieldAbsorbEvent(target.Id, absorbed, effect.Amount));

            if (effect.Amount <= 0)
                target.StatusEffects.RemoveAt(i);
        }
        return damage;
    }

    private CombatantState? FindCombatant(string id)
    {
        return _state.AllCombatants.FirstOrDefault(c => c.Id == id);
    }
}

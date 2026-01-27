using Microsoft.Xna.Framework;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Battle.Abilities
{
    public class FlatStatBonusAbility : IAbility
    {
        public string Name => "Stat Bonus";
        public string Description => "Increases stats.";
        private readonly Dictionary<string, int> _modifiers;
        public FlatStatBonusAbility(Dictionary<string, int> modifiers) { _modifiers = modifiers; }

        public void OnCombatEvent(CombatEventType type, CombatTriggerContext ctx)
        {
            if (type == CombatEventType.CalculateStat)
            {
                if (_modifiers.TryGetValue(ctx.StatType.ToString(), out int bonus))
                {
                    ctx.StatValue += bonus;
                }
            }
        }
    }

    public class CorneredAnimalAbility : IAbility
    {
        public string Name => "Cornered Animal";
        public string Description => "Boosts stat when low HP.";
        private readonly float _hpThreshold;
        private readonly OffensiveStatType _statToRaise;
        private readonly int _amount;

        public CorneredAnimalAbility(float hpThreshold, OffensiveStatType stat, int amount)
        {
            _hpThreshold = hpThreshold;
            _statToRaise = stat;
            _amount = amount;
        }

        public void OnCombatEvent(CombatEventType type, CombatTriggerContext ctx)
        {
            if (type == CombatEventType.CalculateStat && ctx.StatType == _statToRaise)
            {
                float hpPercent = (float)ctx.Actor.Stats.CurrentHP / ctx.Actor.Stats.MaxHP * 100f;
                if (hpPercent < _hpThreshold)
                {
                    float multiplier = 1.0f + (0.5f * _amount);
                    ctx.StatValue *= multiplier;
                }
            }
        }
    }

    public class FlatCritBonusAbility : IAbility
    {
        public string Name => "Sniper";
        public string Description => "Increases crit chance.";
        private readonly float _bonusPercent;
        public FlatCritBonusAbility(float bonusPercent) { _bonusPercent = bonusPercent; }

        public void OnCombatEvent(CombatEventType type, CombatTriggerContext ctx)
        {
            if (type == CombatEventType.CheckCritChance)
            {
                ctx.StatValue += (_bonusPercent / 100f);
            }
        }
    }

    public class CritImmunityAbility : IAbility
    {
        public string Name => "Bulwark";
        public string Description => "Immune to critical hits.";

        public void OnCombatEvent(CombatEventType type, CombatTriggerContext ctx)
        {
            if (type == CombatEventType.CheckCritChance)
            {
                ctx.StatValue = 0f;
            }
        }
    }

    public class LowHPDamageBonusAbility : IAbility
    {
        public string Name => "Adrenaline";
        public string Description => "Deal more damage when HP is low.";
        private readonly float _hpThresholdPercent;
        private readonly float _damageMultiplier;

        public LowHPDamageBonusAbility(float threshold, float multiplier) { _hpThresholdPercent = threshold; _damageMultiplier = multiplier; }

        public void OnCombatEvent(CombatEventType type, CombatTriggerContext ctx)
        {
            if (type == CombatEventType.CalculateOutgoingDamage)
            {
                float hpPercent = (float)ctx.Actor.Stats.CurrentHP / ctx.Actor.Stats.MaxHP * 100f;
                if (hpPercent < _hpThresholdPercent)
                {
                    if (!ctx.IsSimulation) EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
                    ctx.DamageMultiplier *= _damageMultiplier;
                }
            }
        }
    }

    public class FullHPDamageAbility : IAbility
    {
        public string Name => "Full Power";
        public string Description => "Deal more damage at full HP.";
        private readonly float _damageMultiplier;
        public FullHPDamageAbility(float multiplier) { _damageMultiplier = multiplier; }

        public void OnCombatEvent(CombatEventType type, CombatTriggerContext ctx)
        {
            if (type == CombatEventType.CalculateOutgoingDamage)
            {
                if (ctx.Actor.Stats.CurrentHP >= ctx.Actor.Stats.MaxHP)
                {
                    if (!ctx.IsSimulation) EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
                    ctx.DamageMultiplier *= _damageMultiplier;
                }
            }
        }
    }

    public class FirstAttackDamageAbility : IAbility
    {
        public string Name => "First Blood";
        public string Description => "First attack deals bonus damage.";
        private readonly float _multiplier;
        public FirstAttackDamageAbility(float multiplier) { _multiplier = multiplier; }

        public void OnCombatEvent(CombatEventType type, CombatTriggerContext ctx)
        {
            if (type == CombatEventType.CalculateOutgoingDamage)
            {
                if (!ctx.Actor.HasUsedFirstAttack)
                {
                    if (!ctx.IsSimulation) EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
                    ctx.DamageMultiplier *= _multiplier;
                }
            }
        }
    }

    public class StatusImmunityAbility : IAbility
    {
        public string Name => "Unwavering";
        public string Description => "Immune to specific status effects.";
        private readonly HashSet<StatusEffectType> _immuneTypes;
        public StatusImmunityAbility(IEnumerable<StatusEffectType> types) { _immuneTypes = new HashSet<StatusEffectType>(types); }

        public void OnCombatEvent(CombatEventType type, CombatTriggerContext ctx)
        {
            if (type == CombatEventType.CheckStatusImmunity)
            {
                if (_immuneTypes.Contains(ctx.StatusType))
                {
                    EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
                    ctx.IsCancelled = true;
                }
            }
        }
    }

    public class ApplyStatusOnHitAbility : IAbility
    {
        public string Name => "Venomous";
        public string Description => "Chance to apply status on hit.";
        private readonly StatusEffectType _type;
        private readonly int _chance;
        private readonly int _duration;
        private readonly bool _requiresContact;
        private static readonly Random _random = new Random();
        public ApplyStatusOnHitAbility(StatusEffectType type, int chance, int duration, bool requiresContact) { _type = type; _chance = chance; _duration = duration; _requiresContact = requiresContact; }

        public void OnCombatEvent(CombatEventType type, CombatTriggerContext ctx)
        {
            if (type == CombatEventType.OnHit)
            {
                if (_requiresContact && ctx.Move != null && !ctx.Move.MakesContact) return;
                if (_random.Next(1, 101) <= _chance)
                {
                    bool applied = ctx.Target.AddStatusEffect(new StatusEffectInstance(_type, _duration));
                    if (applied)
                    {
                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{ctx.Actor.Name}'s ability applied {_type} to {ctx.Target.Name}!" });
                        EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
                    }
                }
            }
        }
    }

    public class ThornsAbility : IAbility
    {
        public string Name => "Iron Barbs";
        public string Description => "Damage attacker on contact.";
        private readonly float _damagePercent;
        public ThornsAbility(float percent) { _damagePercent = percent; }

        public void OnCombatEvent(CombatEventType type, CombatTriggerContext ctx)
        {
            if (type == CombatEventType.OnDamaged)
            {
                if (ctx.Move != null && ctx.Move.MakesContact)
                {
                    int recoil = Math.Max(1, (int)(ctx.Actor.Stats.MaxHP * (_damagePercent / 100f)));
                    ctx.Actor.ApplyDamage(recoil);
                    EventBus.Publish(new GameEvents.CombatantRecoiled { Actor = ctx.Actor, RecoilDamage = recoil });
                    EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Target, Ability = this });
                }
            }
        }
    }

    public class ImpactDamageBonusAbility : IAbility
    {
        public string Name => "Impact Bonus";
        public string Description => "Boosts damage of a specific impact type.";
        private readonly ImpactType _type;
        private readonly float _multiplier;

        public ImpactDamageBonusAbility(ImpactType type, float multiplier)
        {
            _type = type;
            _multiplier = multiplier;
        }

        public void OnCombatEvent(CombatEventType type, CombatTriggerContext ctx)
        {
            if (type == CombatEventType.CalculateOutgoingDamage)
            {
                if (ctx.Move != null && ctx.Move.ImpactType == _type)
                {
                    if (!ctx.IsSimulation) EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
                    ctx.DamageMultiplier *= _multiplier;
                }
            }
        }
    }

    public class ArcaneEyeAbility : IAbility
    {
        public string Name => "Arcane Eye";
        public string Description => "Single target attacks hit both enemies for half damage.";

        public void OnCombatEvent(CombatEventType type, CombatTriggerContext ctx)
        {
            if (type == CombatEventType.ActionDeclared)
            {
                if (ctx.Action.ChosenMove != null && ctx.Action.ChosenMove.Target == TargetType.Single)
                {
                    ctx.Action.ChosenMove.Target = TargetType.Both;
                    if (!ctx.IsSimulation)
                        EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
                }
            }
            else if (type == CombatEventType.CalculateOutgoingDamage)
            {
                // Check if the original move was Single target
                if (ctx.Move != null && BattleDataCache.Moves.TryGetValue(ctx.Move.MoveID, out var originalMove))
                {
                    if (originalMove.Target == TargetType.Single)
                    {
                        ctx.DamageMultiplier *= 0.5f;
                    }
                }
            }
        }
    }

    public class TidalStoneAbility : IAbility
    {
        public string Name => "Tidal Stone";
        public string Description => "Odd turns: 2x Damage. Even turns: 0.5x Damage.";

        public void OnCombatEvent(CombatEventType type, CombatTriggerContext ctx)
        {
            if (type == CombatEventType.CalculateOutgoingDamage)
            {
                var bm = ServiceLocator.Get<BattleManager>();
                if (bm.RoundNumber % 2 != 0) // Odd
                {
                    ctx.DamageMultiplier *= 2.0f;
                }
                else // Even
                {
                    ctx.DamageMultiplier *= 0.5f;
                }
                if (!ctx.IsSimulation)
                    EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
            }
        }
    }

    // --- PARTY MEMBER PASSIVES ---

    public class PMPyromancerAbility : IAbility
    {
        public string Name => "Pyromancer";
        public string Description => "Deal 1.2x Magic damage.";
        private const float DAMAGE_MULTIPLIER = 1.2f;

        public void OnCombatEvent(CombatEventType type, CombatTriggerContext ctx)
        {
            if (type == CombatEventType.CalculateOutgoingDamage)
            {
                if (ctx.Move != null && ctx.Move.MoveType == MoveType.Spell)
                {
                    if (!ctx.IsSimulation) EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
                    ctx.DamageMultiplier *= DAMAGE_MULTIPLIER;
                }
            }
        }
    }

    public class PMAnnoyingAbility : IAbility
    {
        public string Name => "Annoying";
        public string Description => "Status moves have +1 priority.";

        public void OnCombatEvent(CombatEventType type, CombatTriggerContext ctx)
        {
            if (type == CombatEventType.ActionDeclared)
            {
                if (ctx.Action.ChosenMove != null && ctx.Action.ChosenMove.ImpactType == ImpactType.Status)
                {
                    ctx.Action.Priority += 1;
                }
            }
        }
    }

    public class PMScrappyAbility : IAbility
    {
        public string Name => "Scrappy";
        public string Description => "Immune to Strength drops, Stun, and Daze.";

        public void OnCombatEvent(CombatEventType type, CombatTriggerContext ctx)
        {
            if (type == CombatEventType.CheckStatChangeBlock)
            {
                if (ctx.StatType == OffensiveStatType.Strength && ctx.StatValue < 0)
                {
                    EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
                    ctx.IsCancelled = true;
                }
            }
            else if (type == CombatEventType.CheckStatusImmunity)
            {
                if (ctx.StatusType == StatusEffectType.Stun)
                {
                    EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
                    ctx.IsCancelled = true;
                }
            }
            else if (type == CombatEventType.CheckDazeImmunity)
            {
                EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
                ctx.IsCancelled = true;
            }
        }
    }

    public class PMShortTemperAbility : IAbility
    {
        public string Name => "Short Temper";
        public string Description => "Maxes Strength when hit by a critical hit.";

        public void OnCombatEvent(CombatEventType type, CombatTriggerContext ctx)
        {
            if (type == CombatEventType.OnCritReceived)
            {
                var (success, msg) = ctx.Target.ModifyStatStage(OffensiveStatType.Strength, 12);
                if (success)
                {
                    EventBus.Publish(new GameEvents.CombatantStatStageChanged { Target = ctx.Target, Stat = OffensiveStatType.Strength, Amount = 12 });
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{ctx.Target.Name}'s {Name} maxed their [cstr]Strength[/]!" });
                    EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Target, Ability = this });
                }
            }
        }
    }

    public class PMMajesticAbility : IAbility
    {
        public string Name => "Majestic";
        public string Description => "Lowers enemy Strength on entry.";
        private const OffensiveStatType STAT_TO_LOWER = OffensiveStatType.Strength;
        private const int AMOUNT = -1;

        public void OnCombatEvent(CombatEventType type, CombatTriggerContext ctx)
        {
            if (type == CombatEventType.CombatantEnter)
            {
                var battleManager = ServiceLocator.Get<BattleManager>();
                var enemies = battleManager.AllCombatants.Where(c => c.IsPlayerControlled != ctx.Actor.IsPlayerControlled && !c.IsDefeated && c.IsActiveOnField).ToList();
                bool anyAffected = false;
                foreach (var enemy in enemies)
                {
                    var (success, _) = enemy.ModifyStatStage(STAT_TO_LOWER, AMOUNT);
                    if (success)
                    {
                        anyAffected = true;
                        EventBus.Publish(new GameEvents.CombatantStatStageChanged { Target = enemy, Stat = STAT_TO_LOWER, Amount = AMOUNT });
                    }
                }
                if (anyAffected)
                {
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{ctx.Actor.Name}'s {Name} lowered opponents' {STAT_TO_LOWER}!" });
                    EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
                }
            }
        }
    }

    public class PMSweetpeaAbility : IAbility
    {
        public string Name => "Sweetpea";
        public string Description => "Reduces damage taken by allies.";

        public void OnCombatEvent(CombatEventType type, CombatTriggerContext ctx)
        {
            if (type == CombatEventType.CalculateAllyDamage)
            {
                if (!ctx.IsSimulation) EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
                ctx.DamageMultiplier *= 0.75f;
            }
        }
    }

    public class PMSkepticAbility : IAbility
    {
        public string Name => "Skeptic";
        public string Description => "Takes half damage from Spells.";

        public void OnCombatEvent(CombatEventType type, CombatTriggerContext ctx)
        {
            if (type == CombatEventType.CalculateIncomingDamage)
            {
                if (ctx.Move != null && ctx.Move.MoveType == MoveType.Spell)
                {
                    if (!ctx.IsSimulation) EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Target, Ability = this });
                    ctx.DamageMultiplier *= 0.5f;
                }
            }
        }
    }

    public class PM9LivesAbility : IAbility
    {
        public string Name => "9 Lives";
        public string Description => "Survive lethal damage if at full HP.";

        public void OnCombatEvent(CombatEventType type, CombatTriggerContext ctx)
        {
            if (type == CombatEventType.CalculateIncomingDamage)
            {
                var target = ctx.Target;
                if (target.Stats.CurrentHP == target.Stats.MaxHP)
                {
                    float currentDamage = ctx.StatValue * ctx.DamageMultiplier;
                    if (currentDamage >= target.Stats.CurrentHP)
                    {
                        if (!ctx.IsSimulation)
                        {
                            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{target.Name} endured the hit!" });
                            EventBus.Publish(new GameEvents.AbilityActivated { Combatant = target, Ability = this });
                        }
                        float desiredDamage = Math.Max(0, target.Stats.CurrentHP - 1);
                        if (ctx.StatValue > 0)
                            ctx.DamageMultiplier = desiredDamage / ctx.StatValue;
                    }
                }
            }
        }
    }

    public class PMMinutiaeAbility : IAbility
    {
        public string Name => "Minutiae";
        public string Description => "Boosts moves with 60 or less Power by 1.5x.";

        public void OnCombatEvent(CombatEventType type, CombatTriggerContext ctx)
        {
            if (type == CombatEventType.CalculateOutgoingDamage)
            {
                if (ctx.Move != null && ctx.Move.Power > 0 && ctx.Move.Power <= 60)
                {
                    if (!ctx.IsSimulation) EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
                    ctx.DamageMultiplier *= 1.5f;
                }
            }
        }
    }

    public class PMGentleSoulAbility : IAbility
    {
        public string Name => "Gentle Soul";
        public string Description => "Restores ally HP on switch-in.";

        public void OnCombatEvent(CombatEventType type, CombatTriggerContext ctx)
        {
            if (type == CombatEventType.CombatantEnter)
            {
                var bm = ServiceLocator.Get<BattleManager>();
                if (bm.CurrentPhase == BattleManager.BattlePhase.BattleStartIntro) return;

                var ally = bm.AllCombatants.FirstOrDefault(c => c.IsPlayerControlled == ctx.Actor.IsPlayerControlled && c != ctx.Actor && c.IsActiveOnField && !c.IsDefeated);

                if (ally != null)
                {
                    int healAmount = (int)(ally.Stats.MaxHP * 0.25f);
                    if (healAmount > 0)
                    {
                        int oldHP = (int)ally.VisualHP;
                        ally.ApplyHealing(healAmount);
                        EventBus.Publish(new GameEvents.CombatantHealed { Actor = ctx.Actor, Target = ally, HealAmount = healAmount, VisualHPBefore = oldHP });
                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{ctx.Actor.Name}'s {Name} healed {ally.Name}!" });
                        EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
                    }
                }
            }
        }
    }

    public class PMWellfedAbility : IAbility
    {
        public string Name => "Well-Fed";
        public string Description => "Halves damage taken when at full HP.";

        public void OnCombatEvent(CombatEventType type, CombatTriggerContext ctx)
        {
            if (type == CombatEventType.CalculateIncomingDamage)
            {
                if (ctx.Target.Stats.CurrentHP >= ctx.Target.Stats.MaxHP)
                {
                    if (!ctx.IsSimulation)
                    {
                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{ctx.Target.Name}'s {Name} reduced the damage!" });
                        EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Target, Ability = this });
                    }
                    ctx.DamageMultiplier *= 0.5f;
                }
            }
        }
    }

    public class PMStubbornAbility : IAbility
    {
        public string Name => "Stubborn";
        public string Description => "Boosts Strength by 1.5x, but locks user into one move.";
        private string _lockedMoveID = null;

        public void OnCombatEvent(CombatEventType type, CombatTriggerContext ctx)
        {
            if (type == CombatEventType.CalculateStat)
            {
                if (ctx.StatType == OffensiveStatType.Strength)
                {
                    ctx.StatValue *= 1.5f;
                }
            }
            else if (type == CombatEventType.ActionComplete)
            {
                if (_lockedMoveID == null && ctx.Action.ChosenMove != null)
                {
                    _lockedMoveID = ctx.Action.ChosenMove.MoveID;
                    EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{ctx.Actor.Name} is [cStatus]Stubborn[/]! Locked into {ctx.Action.ChosenMove.MoveName}!" });
                }
            }
            else if (type == CombatEventType.BattleStart || type == CombatEventType.CombatantEnter)
            {
                _lockedMoveID = null;
            }
            else if (type == CombatEventType.QueryMoveLock)
            {
                ctx.LockedMoveID = _lockedMoveID;
            }
        }
    }
} 
using Microsoft.Xna.Framework;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Battle.Abilities
{
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
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Utils;
using System.Linq;

namespace ProjectVagabond.Battle.Abilities
{
    public class PMPyromancerAbility : IAbility
    {
        public string Name => "Pyromancer";
        public string Description => "Deal 1.2x Magic damage.";
        public int Priority => 0;
        private const float DAMAGE_MULTIPLIER = 1.2f;

        public void OnEvent(GameEvent e, BattleContext context)
        {
            if (e is CalculateDamageEvent dmgEvent && dmgEvent.Actor.Abilities.Contains(this))
            {
                if (dmgEvent.Move.MoveType == MoveType.Spell)
                {
                    dmgEvent.DamageMultiplier *= DAMAGE_MULTIPLIER;
                    if (!context.IsSimulation)
                    {
                        EventBus.Publish(new GameEvents.AbilityActivated { Combatant = dmgEvent.Actor, Ability = this });
                    }
                }
            }
        }
    }

    public class PMAnnoyingAbility : IAbility
    {
        public string Name => "Annoying";
        public string Description => "Status moves have +1 priority.";
        public int Priority => 0;

        public void OnEvent(GameEvent e, BattleContext context)
        {
            // Placeholder: Priority modification logic would go here if ActionDeclaredEvent supported it.
        }
    }

    public class PMScrappyAbility : IAbility
    {
        public string Name => "Scrappy";
        public string Description => "Immune to Strength drops, Stun, and Daze.";
        public int Priority => 0;

        public void OnEvent(GameEvent e, BattleContext context)
        {
            if (e is StatusAppliedEvent statusEvent && statusEvent.Target.Abilities.Contains(this))
            {
                if (statusEvent.StatusEffect.EffectType == StatusEffectType.Stun)
                {
                    statusEvent.IsHandled = true;
                    EventBus.Publish(new GameEvents.AbilityActivated { Combatant = statusEvent.Target, Ability = this });
                }
            }
        }
    }

    public class PMShortTemperAbility : IAbility
    {
        public string Name => "Short Temper";
        public string Description => "Maxes Strength when hit by a critical hit.";
        public int Priority => 0;

        public void OnEvent(GameEvent e, BattleContext context)
        {
            if (e is CalculateDamageEvent dmgEvent && dmgEvent.Target.Abilities.Contains(this))
            {
                if (dmgEvent.IsCritical)
                {
                    if (!context.IsSimulation)
                    {
                        dmgEvent.Target.ModifyStatStage(OffensiveStatType.Strength, 12);
                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{dmgEvent.Target.Name}'s {Name} maxed their [cstr]Strength[/]!" });
                        EventBus.Publish(new GameEvents.AbilityActivated { Combatant = dmgEvent.Target, Ability = this });
                    }
                }
            }
        }
    }

    public class PMMajesticAbility : IAbility
    {
        public string Name => "Majestic";
        public string Description => "Lowers enemy Strength on entry.";
        public int Priority => 0;

        public void OnEvent(GameEvent e, BattleContext context)
        {
            if (e is BattleStartedEvent battleEvent)
            {
                foreach (var c in battleEvent.Combatants)
                {
                    if (c.Abilities.Contains(this))
                    {
                        foreach (var enemy in battleEvent.Combatants.Where(x => x.IsPlayerControlled != c.IsPlayerControlled))
                        {
                            enemy.ModifyStatStage(OffensiveStatType.Strength, -1);
                        }
                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{c.Name}'s {Name} lowered opponents' Strength!" });
                        EventBus.Publish(new GameEvents.AbilityActivated { Combatant = c, Ability = this });
                    }
                }
            }
        }
    }

    public class PMSkepticAbility : IAbility
    {
        public string Name => "Skeptic";
        public string Description => "Takes half damage from Spells.";
        public int Priority => 0;

        public void OnEvent(GameEvent e, BattleContext context)
        {
            if (e is CalculateDamageEvent dmgEvent && dmgEvent.Target.Abilities.Contains(this))
            {
                if (dmgEvent.Move.MoveType == MoveType.Spell)
                {
                    dmgEvent.DamageMultiplier *= 0.5f;
                    if (!context.IsSimulation)
                    {
                        EventBus.Publish(new GameEvents.AbilityActivated { Combatant = dmgEvent.Target, Ability = this });
                    }
                }
            }
        }
    }

    public class PM9LivesAbility : IAbility
    {
        public string Name => "9 Lives";
        public string Description => "Survive lethal damage if at full HP.";
        public int Priority => 0;

        public void OnEvent(GameEvent e, BattleContext context)
        {
            if (e is CalculateDamageEvent dmgEvent && dmgEvent.Target.Abilities.Contains(this))
            {
                if (dmgEvent.Target.Stats.CurrentHP == dmgEvent.Target.Stats.MaxHP)
                {
                    if (dmgEvent.FinalDamage >= dmgEvent.Target.Stats.CurrentHP)
                    {
                        dmgEvent.FinalDamage = dmgEvent.Target.Stats.CurrentHP - 1;
                        if (!context.IsSimulation)
                        {
                            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{dmgEvent.Target.Name} endured the hit!" });
                            EventBus.Publish(new GameEvents.AbilityActivated { Combatant = dmgEvent.Target, Ability = this });
                        }
                    }
                }
            }
        }
    }

    public class PMMinutiaeAbility : IAbility
    {
        public string Name => "Minutiae";
        public string Description => "Boosts moves with 60 or less Power by 1.5x.";
        public int Priority => 0;

        public void OnEvent(GameEvent e, BattleContext context)
        {
            if (e is CalculateDamageEvent dmgEvent && dmgEvent.Actor.Abilities.Contains(this))
            {
                if (dmgEvent.Move.Power > 0 && dmgEvent.Move.Power <= 60)
                {
                    dmgEvent.DamageMultiplier *= 1.5f;
                    if (!context.IsSimulation)
                    {
                        EventBus.Publish(new GameEvents.AbilityActivated { Combatant = dmgEvent.Actor, Ability = this });
                    }
                }
            }
        }
    }

    public class PMWellfedAbility : IAbility
    {
        public string Name => "Well-Fed";
        public string Description => "Halves damage taken when at full HP.";
        public int Priority => 0;

        public void OnEvent(GameEvent e, BattleContext context)
        {
            if (e is CalculateDamageEvent dmgEvent && dmgEvent.Target.Abilities.Contains(this))
            {
                if (dmgEvent.Target.Stats.CurrentHP >= dmgEvent.Target.Stats.MaxHP)
                {
                    dmgEvent.DamageMultiplier *= 0.5f;
                    if (!context.IsSimulation)
                    {
                        EventBus.Publish(new GameEvents.AbilityActivated { Combatant = dmgEvent.Target, Ability = this });
                    }
                }
            }
        }
    }

    public class PMStubbornAbility : IAbility
    {
        public string Name => "Stubborn";
        public string Description => "Boosts Strength by 1.5x.";
        public int Priority => 0;

        public void OnEvent(GameEvent e, BattleContext context)
        {
            if (e is CalculateStatEvent statEvent && statEvent.Actor.Abilities.Contains(this))
            {
                if (statEvent.StatType == OffensiveStatType.Strength)
                {
                    statEvent.FinalValue *= 1.5f;
                }
            }
        }
    }
}
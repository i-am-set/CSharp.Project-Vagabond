using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Utils;
using System.Linq;
using static ProjectVagabond.GameEvents;

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
            if (e is CheckActionPriorityEvent prioEvent && prioEvent.Actor.Abilities.Contains(this))
            {
                if (prioEvent.Move.ImpactType == ImpactType.Status)
                {
                    prioEvent.Priority += 1;
                    // Optional: Visual log or debug print
                }
            }
        }
    }

    public class PMScrappyAbility : IAbility
    {
        public string Name => "Scrappy";
        public string Description => "Immune to Strength drops, Stun, and Daze.";
        public int Priority => 100;

        public void OnEvent(GameEvent e, BattleContext context)
        {
            // 1. Block Stun (Status Effect)
            if (e is StatusAppliedEvent statusEvent && statusEvent.Target == context.Actor)
            {
                if (statusEvent.StatusEffect.EffectType == StatusEffectType.Stun)
                {
                    statusEvent.IsHandled = true;
                    EventBus.Publish(new GameEvents.AbilityActivated { Combatant = context.Actor, Ability = this, NarrationText = "Scrappy prevented Stun!" });
                }
            }

            // 2. Block Strength Drops (Stat Change)
            if (e is StatChangeAttemptEvent statEvent && statEvent.Target == context.Actor)
            {
                if (statEvent.Stat == OffensiveStatType.Strength && statEvent.Amount < 0)
                {
                    statEvent.IsHandled = true; // Cancel the change
                    EventBus.Publish(new GameEvents.AbilityActivated { Combatant = context.Actor, Ability = this, NarrationText = "Scrappy prevents Strength loss!" });
                }
            }

            // 3. Block Daze (Tag)
            // Daze is a tag, usually checked at TurnStart. We remove it immediately if found.
            if (e is TurnStartEvent turnEvent && turnEvent.Actor == context.Actor)
            {
                if (turnEvent.Actor.Tags.Has(GameplayTags.States.Dazed))
                {
                    turnEvent.Actor.Tags.Remove(GameplayTags.States.Dazed);
                    EventBus.Publish(new GameEvents.AbilityActivated { Combatant = context.Actor, Ability = this, NarrationText = "shook off the Daze!" });
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
            // Use ReactionEvent to react AFTER the hit is confirmed
            if (e is ReactionEvent reaction && reaction.Target.Abilities.Contains(this))
            {
                if (reaction.Result.WasCritical)
                {
                    // Calculate distance to Max (+2)
                    int currentStr = reaction.Target.StatStages[OffensiveStatType.Strength];
                    int needed = 2 - currentStr;

                    if (needed > 0)
                    {
                        // Force the change to exactly +2
                        reaction.Target.ModifyStatStage(OffensiveStatType.Strength, needed);

                        if (!context.IsSimulation)
                        {
                            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{reaction.Target.Name}'s {Name} maxed their [cstr]Strength[/]!" });
                            EventBus.Publish(new GameEvents.AbilityActivated { Combatant = reaction.Target, Ability = this });
                        }
                    }
                    else if (!context.IsSimulation)
                    {
                        // Optional: Flavor text if already maxed
                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{reaction.Target.Name} is already furious!" });
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
            var bm = ServiceLocator.Get<BattleManager>();

            // 1. Battle Start
            if (e is BattleStartedEvent battleEvent)
            {
                if (battleEvent.Combatants.Contains(context.Actor))
                {
                    QueueMajesticEffects(context.Actor, bm);
                }
            }
            // 2. Entry (Switch or Reinforcement)
            else if (e is GameEvents.CombatantEnteredEvent entryEvent && entryEvent.Combatant == context.Actor)
            {
                QueueMajesticEffects(context.Actor, bm);
            }
        }

        private void QueueMajesticEffects(BattleCombatant user, BattleManager bm)
        {
            var enemies = bm.AllCombatants
                .Where(c => c.IsPlayerControlled != user.IsPlayerControlled && c.IsActiveOnField && !c.IsDefeated)
                .OrderBy(c => c.BattleSlot) // Slot 1 then Slot 2
                .ToList();

            if (enemies.Any())
            {
                // Initial Notification
                bm.EnqueueStartupEvent(() =>
                {
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{user.Name}'s {Name} activates!" });
                    EventBus.Publish(new GameEvents.AbilityActivated { Combatant = user, Ability = this });
                });

                // Staggered Effect per Enemy
                foreach (var enemy in enemies)
                {
                    bm.EnqueueStartupEvent(() =>
                    {
                        var result = enemy.ModifyStatStage(OffensiveStatType.Strength, -1);
                        // Explicitly trigger particle here to ensure visibility
                        if (result.success)
                        {
                        }
                    });
                }
            }
        }
    }

    public class PMSkepticAbility : IAbility
    {
        public string Name => "Skeptic";
        public string Description => "Takes half damage from Spells. Refuses to cast Spells.";
        public int Priority => 0;

        public void OnEvent(GameEvent e, BattleContext context)
        {
            // 1. Damage Reduction Logic
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

            // 2. Spell Refusal Logic
            if (e is ActionDeclaredEvent actionEvent && actionEvent.Actor.Abilities.Contains(this))
            {
                if (actionEvent.Move.MoveType == MoveType.Spell)
                {
                    actionEvent.IsHandled = true;
                    if (!context.IsSimulation)
                    {
                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{actionEvent.Actor.Name} refuses to believe in magic!" });
                        EventBus.Publish(new GameEvents.ActionFailed
                        {
                            Actor = actionEvent.Actor,
                            Reason = "skeptic",
                            MoveName = actionEvent.Move.MoveName
                        });
                    }
                }
            }
        }
    }

    public class PM9LivesAbility : IAbility
    {
        public string Name => "9 Lives";
        public string Description => "Survive lethal damage if at full HP.";

        public int Priority => -10;

        public void OnEvent(GameEvent e, BattleContext context)
        {
            if (e is CalculateDamageEvent dmgEvent && dmgEvent.Target.Abilities.Contains(this))
            {
                if (dmgEvent.Target.Stats.CurrentHP == dmgEvent.Target.Stats.MaxHP)
                {
                    if (dmgEvent.FinalDamage >= dmgEvent.Target.Stats.CurrentHP)
                    {
                        // Clamp damage to leave 1 HP
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

    public class PMSweetpeaAbility : IAbility
    {
        public string Name => "Sweet Pea";
        public string Description => "Healing moves gain +2 Priority.";
        public int Priority => 0;

        public void OnEvent(GameEvent e, BattleContext context)
        {
            if (e is CheckActionPriorityEvent prioEvent && prioEvent.Actor.Abilities.Contains(this))
            {
                if (prioEvent.Move.Tags.Has("Heal"))
                {
                    prioEvent.Priority += 2;
                }
            }
        }
    }
}
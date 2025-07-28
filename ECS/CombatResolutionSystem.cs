using Microsoft.Xna.Framework;
using ProjectVagabond.Dice;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond
{
    /// <summary>
    /// Processes combat actions as they happen.
    /// </summary>
    public class CombatResolutionSystem : ISystem
    {
        private readonly ComponentStore _componentStore;
        private GameState _gameState; // Lazy loaded
        private EntityManager _entityManager; // Lazy loaded
        private readonly DiceRollingSystem _diceRollingSystem;
        private readonly Global _global;

        private AttackAction _pendingAttackAction;
        private Dictionary<string, int> _flatModifiers = new Dictionary<string, int>();
        public event Action OnAttackResolved;

        public CombatResolutionSystem()
        {
            _componentStore = ServiceLocator.Get<ComponentStore>();
            _diceRollingSystem = ServiceLocator.Get<DiceRollingSystem>();
            _global = ServiceLocator.Get<Global>();
            _diceRollingSystem.OnRollCompleted += HandleDiceRollCompleted;
        }

        // This system is not updated by the SystemManager, but called explicitly.
        public void Update(GameTime gameTime) { }

        /// <summary>
        /// Initiates an attack by parsing all dice notations, requesting a dice roll for non-flat values,
        /// and preparing for the result.
        /// </summary>
        public void InitiateAttackResolution(AttackAction action)
        {
            _pendingAttackAction = action;
            _flatModifiers.Clear();
            var rollRequest = new List<DiceGroup>();

            var attackerCombatantComp = _componentStore.GetComponent<CombatantComponent>(action.ActorId);
            var attackerAttacksComp = _componentStore.GetComponent<AvailableAttacksComponent>(action.ActorId);
            var attack = attackerAttacksComp?.Attacks.FirstOrDefault(a => a.Name == action.AttackName);

            if (attackerCombatantComp == null || attack == null)
            {
                ResolveZeroDamageAttack();
                return;
            }

            // 1. Process Base Damage
            ProcessNotation(attackerCombatantComp.AttackPower, "damage", _global.Palette_BrightWhite, rollRequest, attack.DamageMultiplier);

            // 2. Process Status Effect Amounts
            if (attack.StatusEffectsToApply != null)
            {
                for (int i = 0; i < attack.StatusEffectsToApply.Count; i++)
                {
                    var effectApp = attack.StatusEffectsToApply[i];
                    string groupId = $"{effectApp.EffectName.ToLower()}_amount_{i}";
                    // This could be expanded to have different colors per effect type
                    Color tint = effectApp.EffectName.ToLower() == "poison" ? _global.Palette_DarkGreen : _global.Palette_LightPurple;
                    ProcessNotation(effectApp.Amount, groupId, tint, rollRequest);
                }
            }

            // 3. Submit the roll request or resolve immediately if no dice were needed
            if (rollRequest.Any())
            {
                _diceRollingSystem.Roll(rollRequest);
            }
            else
            {
                // No dice to roll, all values are flat modifiers. Resolve immediately.
                HandleDiceRollCompleted(new DiceRollResult());
            }
        }

        /// <summary>
        /// Helper to parse a notation string and either add a dice group to the request
        /// or store a flat modifier value.
        /// </summary>
        private void ProcessNotation(string notation, string groupId, Color tint, List<DiceGroup> rollRequest, float multiplier = 1.0f)
        {
            var (numDice, numSides, modifier) = DiceParser.Parse(notation);

            if (numDice > 0 && numSides > 0)
            {
                rollRequest.Add(new DiceGroup
                {
                    GroupId = groupId,
                    NumberOfDice = numDice,
                    Tint = tint,
                    ResultProcessing = DiceResultProcessing.Sum,
                    Multiplier = multiplier,
                    Modifier = modifier
                });
            }
            else
            {
                // If there are no dice, the "modifier" is the entire flat value.
                // We apply the multiplier to it directly here.
                int finalValue = (int)Math.Ceiling(modifier * multiplier);
                _flatModifiers[groupId] = finalValue;
            }
        }

        private void ResolveZeroDamageAttack()
        {
            var attackerName = EntityNamer.GetName(_pendingAttackAction.ActorId);
            var targetName = EntityNamer.GetName(_pendingAttackAction.TargetId);
            EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = $"{attackerName} attacks {targetName} but deals no damage." });

            _pendingAttackAction = null;
            OnAttackResolved?.Invoke();
        }

        private void HandleDiceRollCompleted(DiceRollResult result)
        {
            if (_pendingAttackAction == null) return;

            _gameState ??= ServiceLocator.Get<GameState>();
            _entityManager ??= ServiceLocator.Get<EntityManager>();

            var attackerId = _pendingAttackAction.ActorId;
            var targetId = _pendingAttackAction.TargetId;
            var attackName = _pendingAttackAction.AttackName;

            var attackerAttacksComp = _componentStore.GetComponent<AvailableAttacksComponent>(attackerId);
            var targetHealthComp = _componentStore.GetComponent<HealthComponent>(targetId);
            var attackerName = EntityNamer.GetName(attackerId);
            var targetName = EntityNamer.GetName(targetId);

            if (attackerAttacksComp == null || targetHealthComp == null)
            {
                EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = $"[error]Could not resolve attack for {attackerName}. Missing components." });
                CleanupAndSignalCompletion();
                return;
            }

            var attack = attackerAttacksComp.Attacks.FirstOrDefault(a => a.Name == attackName);
            if (attack == null)
            {
                EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = $"[error]{attackerName} tried to use an unknown attack: {attackName}" });
                CleanupAndSignalCompletion();
                return;
            }

            // --- DAMAGE RESOLUTION ---
            int finalDamage = GetResolvedValue("damage", result);
            var attackerStatusEffects = _componentStore.GetComponent<ActiveStatusEffectComponent>(attackerId);
            bool isWeakened = attackerStatusEffects?.ActiveEffects.Any(e => e.BaseEffect.Name == "Weakness") ?? false;
            if (isWeakened) finalDamage /= 2;

            targetHealthComp.TakeDamage(finalDamage);
            EventBus.Publish(new GameEvents.EntityTookDamage { EntityId = targetId, DamageAmount = finalDamage });
            EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = $"{attackerName} attacks {targetName} with {attack.Name} for [red]{finalDamage}[/] damage! {targetName} has {targetHealthComp.CurrentHealth}/{targetHealthComp.MaxHealth} HP remaining." });

            // --- STATUS EFFECT RESOLUTION ---
            if (attack.StatusEffectsToApply != null && attack.StatusEffectsToApply.Any())
            {
                var statusEffectSystem = ServiceLocator.Get<StatusEffectSystem>();
                for (int i = 0; i < attack.StatusEffectsToApply.Count; i++)
                {
                    var effectApp = attack.StatusEffectsToApply[i];
                    string groupId = $"{effectApp.EffectName.ToLower()}_amount_{i}";
                    int effectValue = GetResolvedValue(groupId, result);

                    var effect = statusEffectSystem.CreateEffectFromName(effectApp.EffectName, attack.Name);
                    if (effect != null && effectValue > 0)
                    {
                        // For poison, the rolled amount is the duration.
                        if (effect.Name == "Poison")
                        {
                            statusEffectSystem.ApplyEffect(targetId, effect, effectValue, attackerId, effectValue);
                        }
                        else
                        {
                            // For other effects, it might be a fixed duration with a variable amount.
                            statusEffectSystem.ApplyEffect(targetId, effect, 3f, attackerId, effectValue);
                        }
                    }
                }
            }

            // --- PROVOKE & DEATH CHECKS ---
            var playerTag = _componentStore.GetComponent<PlayerTagComponent>(attackerId);
            if (playerTag != null)
            {
                var targetPersonality = _componentStore.GetComponent<AIPersonalityComponent>(targetId);
                if (targetPersonality != null && !targetPersonality.IsProvoked)
                {
                    if (targetPersonality.Personality == AIPersonalityType.Neutral || targetPersonality.Personality == AIPersonalityType.Passive)
                    {
                        targetPersonality.IsProvoked = true;
                        EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = $"{targetName} becomes hostile!" });
                    }
                }
            }

            if (targetHealthComp.CurrentHealth <= 0)
            {
                HandleTargetDeath(targetId, targetName);
            }

            CleanupAndSignalCompletion();
        }

        private int GetResolvedValue(string groupId, DiceRollResult result)
        {
            // The final value is the sum of the dice roll for that group (or 0 if none)
            // plus any flat modifier stored for that group.
            int rolledValue = result.ResultsByGroup.ContainsKey(groupId) ? result.ResultsByGroup[groupId].Sum() : 0;
            int modifierValue = _flatModifiers.ContainsKey(groupId) ? _flatModifiers[groupId] : 0;
            return rolledValue + modifierValue;
        }

        private void HandleTargetDeath(int targetId, string targetName)
        {
            if (targetId == _gameState.PlayerEntityId)
            {
                EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = "[red]You have been defeated!" });
                _gameState.EndCombat();
                return;
            }

            EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = $"[red]{targetName} has been defeated![/]" });

            var worldPos = _componentStore.GetComponent<PositionComponent>(targetId)?.WorldPosition ?? Vector2.Zero;
            var localPos = _componentStore.GetComponent<LocalPositionComponent>(targetId)?.LocalPosition ?? Vector2.Zero;
            int corpseId = Spawner.Spawn("corpse", worldPos, localPos);
            var corpseComp = _componentStore.GetComponent<CorpseComponent>(corpseId);
            if (corpseComp != null)
            {
                corpseComp.OriginalEntityId = targetId;
            }

            _gameState.RemoveEntityFromCombat(targetId);
            _componentStore.EntityDestroyed(targetId);
            _entityManager.DestroyEntity(targetId);

            bool enemiesRemain = _gameState.Combatants.Any(id => id != _gameState.PlayerEntityId);
            if (!enemiesRemain)
            {
                EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = "[palette_yellow]Victory! All enemies have been defeated." });
                _gameState.EndCombat();
            }
        }

        private void CleanupAndSignalCompletion()
        {
            _pendingAttackAction = null;
            _flatModifiers.Clear();
            OnAttackResolved?.Invoke();
        }
    }
}
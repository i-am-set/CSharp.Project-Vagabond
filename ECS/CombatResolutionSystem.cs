using Microsoft.Xna.Framework;
using ProjectVagabond.Dice;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

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
        /// Initiates an attack by requesting a dice roll. The actual damage resolution
        /// will occur once the dice roll is complete.
        /// </summary>
        /// <param name="action">The attack action to resolve.</param>
        public void InitiateAttackResolution(AttackAction action)
        {
            _pendingAttackAction = action;

            var attackerCombatantComp = _componentStore.GetComponent<CombatantComponent>(action.ActorId);
            if (attackerCombatantComp == null || string.IsNullOrWhiteSpace(attackerCombatantComp.AttackPower))
            {
                // Failsafe: if no attack power, resolve immediately with 0 damage.
                ResolveZeroDamageAttack();
                return;
            }

            var (numDice, numSides) = ParseDiceString(attackerCombatantComp.AttackPower);
            if (numDice <= 0)
            {
                ResolveZeroDamageAttack();
                return;
            }

            // For now, we assume all attacks are red "damage" dice. This could be expanded later.
            var rollRequest = new List<DiceGroup>
            {
                new DiceGroup
                {
                    GroupId = "damage", // A specific ID for this roll type
                    NumberOfDice = numDice,
                    Tint = _global.Palette_Red,
                    ResultProcessing = DiceResultProcessing.Sum
                }
            };

            _diceRollingSystem.Roll(rollRequest);
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
            // This handler might be called for other dice rolls in the game.
            // We must check if it's the one we're waiting for.
            if (_pendingAttackAction == null || !result.ResultsByGroup.ContainsKey("damage"))
            {
                return;
            }

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
                _pendingAttackAction = null;
                OnAttackResolved?.Invoke();
                return;
            }

            var attack = attackerAttacksComp.Attacks.FirstOrDefault(a => a.Name == attackName);

            if (attack == null)
            {
                EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = $"[error]{attackerName} tried to use an unknown attack: {attackName}" });
                _pendingAttackAction = null;
                OnAttackResolved?.Invoke();
                return;
            }

            // Get the base damage from the dice roll result.
            int baseDamage = result.ResultsByGroup["damage"].Sum();

            var attackerStatusEffects = _componentStore.GetComponent<ActiveStatusEffectComponent>(attackerId);
            bool isWeakened = attackerStatusEffects?.ActiveEffects.Any(e => e.BaseEffect.Name == "Weakness") ?? false;

            int finalDamage = (int)(baseDamage * attack.DamageMultiplier);
            if (isWeakened)
            {
                finalDamage /= 2;
            }

            targetHealthComp.TakeDamage(finalDamage);

            // Publish the event so other systems (like Haptics) can react.
            EventBus.Publish(new GameEvents.EntityTookDamage { EntityId = targetId, DamageAmount = finalDamage });

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

            EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = $"{attackerName} attacks {targetName} with {attack.Name} for [red]{finalDamage}[/] damage! ({baseDamage} base) {targetName} has {targetHealthComp.CurrentHealth}/{targetHealthComp.MaxHealth} HP remaining." });

            if (attack.StatusEffectsToApply != null && attack.StatusEffectsToApply.Any())
            {
                var statusEffectSystem = ServiceLocator.Get<StatusEffectSystem>();
                foreach (var effectName in attack.StatusEffectsToApply)
                {
                    var effect = statusEffectSystem.CreateEffectFromName(effectName, attack.Name);
                    if (effect != null)
                    {
                        float durationInRounds = 3f; // Lasts 3 rounds
                        statusEffectSystem.ApplyEffect(targetId, effect, durationInRounds, attackerId);
                    }
                }
            }

            if (targetHealthComp.CurrentHealth <= 0)
            {
                // Check for player death first
                if (targetId == _gameState.PlayerEntityId)
                {
                    EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = "[red]You have been defeated!" });
                    _gameState.EndCombat();
                    // TODO: Implement proper game over screen/logic
                    _pendingAttackAction = null;
                    OnAttackResolved?.Invoke();
                    return; // Stop processing, combat is over.
                }

                EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = $"[red]{targetName} has been defeated![/]" });

                // Spawn a corpse
                var worldPos = _componentStore.GetComponent<PositionComponent>(targetId)?.WorldPosition ?? Vector2.Zero;
                var localPos = _componentStore.GetComponent<LocalPositionComponent>(targetId)?.LocalPosition ?? Vector2.Zero;
                int corpseId = Spawner.Spawn("corpse", worldPos, localPos);
                var corpseComp = _componentStore.GetComponent<CorpseComponent>(corpseId);
                if (corpseComp != null)
                {
                    corpseComp.OriginalEntityId = targetId;
                }

                // Remove the defeated entity from the game
                _gameState.RemoveEntityFromCombat(targetId);
                _componentStore.EntityDestroyed(targetId);
                _entityManager.DestroyEntity(targetId);

                // Check for victory condition
                bool enemiesRemain = _gameState.Combatants.Any(id => id != _gameState.PlayerEntityId);
                if (!enemiesRemain)
                {
                    EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = "[palette_yellow]Victory! All enemies have been defeated." });
                    _gameState.EndCombat();
                }
            }

            // Clean up and signal that the resolution is complete.
            _pendingAttackAction = null;
            OnAttackResolved?.Invoke();
        }

        private (int numDice, int numSides) ParseDiceString(string diceNotation)
        {
            if (string.IsNullOrWhiteSpace(diceNotation))
            {
                return (0, 0);
            }

            // Make it case-insensitive
            diceNotation = diceNotation.ToLower();

            // Handle cases like "d6" which implies "1d6"
            if (diceNotation.StartsWith("d"))
            {
                diceNotation = "1" + diceNotation;
            }

            var match = Regex.Match(diceNotation, @"(\d+)d(\d+)");

            if (match.Success && match.Groups.Count == 3)
            {
                if (int.TryParse(match.Groups[1].Value, out int numDice) &&
                    int.TryParse(match.Groups[2].Value, out int numSides))
                {
                    // For now, we only support d6, but the parser is ready for more.
                    if (numSides == 6)
                    {
                        return (numDice, numSides);
                    }
                }
            }

            // Return 0 if parsing fails
            return (0, 0);
        }
    }
}
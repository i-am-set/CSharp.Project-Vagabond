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
        private ItemManager _itemManager;

        private AttackAction _pendingAttackAction;
        private Dictionary<string, int> _flatModifiers = new Dictionary<string, int>();
        public event Action OnAttackResolved;

        /// <summary>
        /// A temporary data structure to hold the combined, final stats for a single attack.
        /// </summary>
        private struct EffectiveAttackStats
        {
            public string DamageNotation;
            public float Range;
            public List<StatusEffectApplication> StatusEffects;
            public string WeaponName;
        }

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
        /// Calculates the final attack stats for an entity by combining its base stats
        /// with the properties of its equipped weapon.
        /// </summary>
        /// <param name="attackerId">The ID of the attacking entity.</param>
        /// <returns>A struct containing the effective stats for the attack.</returns>
        private EffectiveAttackStats GetEffectiveAttackStats(int attackerId)
        {
            _itemManager ??= ServiceLocator.Get<ItemManager>();

            var combatantComp = _componentStore.GetComponent<CombatantComponent>(attackerId);
            var equipmentComp = _componentStore.GetComponent<EquipmentComponent>(attackerId);

            // Default to unarmed stats if components are missing
            if (combatantComp == null)
            {
                return new EffectiveAttackStats
                {
                    DamageNotation = "0",
                    Range = 0,
                    StatusEffects = new List<StatusEffectApplication>(),
                    WeaponName = "Fists"
                };
            }

            string weaponId = equipmentComp?.EquippedWeaponId ?? "unarmed";
            var weapon = _itemManager.GetWeapon(weaponId) ?? _itemManager.GetWeapon("unarmed");

            if (weapon == null) // Failsafe if even "unarmed" is missing
            {
                return new EffectiveAttackStats
                {
                    DamageNotation = combatantComp.AttackPower,
                    Range = combatantComp.AttackRange,
                    StatusEffects = new List<StatusEffectApplication>(),
                    WeaponName = "Fists"
                };
            }

            // --- Hybrid Logic Implementation ---
            string finalDamage;
            if (weapon.Type == WeaponType.Ranged)
            {
                // Ranged weapons completely override base damage.
                finalDamage = weapon.Damage;
            }
            else // Melee
            {
                // Melee weapons add their damage to the wielder's base damage.
                string baseDamage = combatantComp.AttackPower;
                string weaponDamage = weapon.Damage;

                if (string.IsNullOrWhiteSpace(baseDamage) || baseDamage == "0")
                {
                    finalDamage = weaponDamage;
                }
                else if (string.IsNullOrWhiteSpace(weaponDamage) || weaponDamage == "0")
                {
                    finalDamage = baseDamage;
                }
                else
                {
                    finalDamage = $"{baseDamage}+{weaponDamage}";
                }
            }

            // Range is overridden by the weapon if specified.
            float finalRange = weapon.Range > 0 ? weapon.Range : combatantComp.AttackRange;

            // Status effects are taken directly from the weapon.
            var finalStatusEffects = new List<StatusEffectApplication>(weapon.StatusEffectsToApply);

            return new EffectiveAttackStats
            {
                DamageNotation = finalDamage,
                Range = finalRange,
                StatusEffects = finalStatusEffects,
                WeaponName = weapon.Name
            };
        }

        /// <summary>
        /// Initiates an attack by parsing all dice notations, requesting a dice roll for non-flat values,
        /// and preparing for the result.
        /// </summary>
        public void InitiateAttackResolution(AttackAction action)
        {
            _pendingAttackAction = action;
            _flatModifiers.Clear();
            var rollRequest = new List<DiceGroup>();

            var effectiveStats = GetEffectiveAttackStats(action.ActorId);

            // 1. Process Damage by combining all sources into a single group.
            int totalDamageDice = 0;
            int totalDamageModifier = 0;
            string[] damageTerms = effectiveStats.DamageNotation.Split('+', StringSplitOptions.RemoveEmptyEntries);

            foreach (var term in damageTerms)
            {
                var (numDice, numSides, modifier) = DiceParser.Parse(term);
                // We assume all damage dice can be grouped (e.g., they are all d6s).
                // The parser handles flat numbers as modifiers with 0 dice.
                if (numDice > 0)
                {
                    totalDamageDice += numDice;
                }
                totalDamageModifier += modifier;
            }

            if (totalDamageDice > 0)
            {
                rollRequest.Add(new DiceGroup
                {
                    GroupId = "damage",
                    NumberOfDice = totalDamageDice,
                    Tint = _global.Palette_BrightWhite,
                    Scale = 1.0f,
                    ResultProcessing = DiceResultProcessing.Sum,
                    Modifier = totalDamageModifier
                });
            }
            else if (totalDamageModifier > 0)
            {
                _flatModifiers["damage"] = totalDamageModifier;
            }


            // 2. Process Status Effect Amounts as separate, smaller dice groups.
            if (effectiveStats.StatusEffects != null)
            {
                for (int i = 0; i < effectiveStats.StatusEffects.Count; i++)
                {
                    var effectApp = effectiveStats.StatusEffects[i];
                    string groupId = $"{effectApp.EffectName.ToLower()}_amount_{i}";
                    Color tint = effectApp.EffectName.ToLower() == "poison" ? _global.Palette_DarkGreen : _global.Palette_LightPurple;
                    ProcessNotation(effectApp.Amount, groupId, tint, 0.6f, rollRequest);
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
        private void ProcessNotation(string notation, string groupId, Color tint, float scale, List<DiceGroup> rollRequest)
        {
            var (numDice, numSides, modifier) = DiceParser.Parse(notation);

            if (numDice > 0 && numSides > 0)
            {
                rollRequest.Add(new DiceGroup
                {
                    GroupId = groupId,
                    NumberOfDice = numDice,
                    Tint = tint,
                    Scale = scale,
                    ResultProcessing = DiceResultProcessing.Sum,
                    Modifier = modifier
                });
            }
            else
            {
                // If there are no dice, the "modifier" is the entire flat value.
                _flatModifiers[groupId] = modifier;
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

            var effectiveStats = GetEffectiveAttackStats(attackerId);
            var targetHealthComp = _componentStore.GetComponent<HealthComponent>(targetId);
            var attackerName = EntityNamer.GetName(attackerId);
            var targetName = EntityNamer.GetName(targetId);

            if (targetHealthComp == null)
            {
                EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = $"[error]Could not resolve attack for {attackerName}. Target missing components." });
                CleanupAndSignalCompletion();
                return;
            }

            // --- DAMAGE RESOLUTION ---
            // Get the final value from the single, consolidated "damage" group.
            int finalDamage = GetResolvedValue("damage", result);

            var attackerStatusEffects = _componentStore.GetComponent<ActiveStatusEffectComponent>(attackerId);
            bool isWeakened = attackerStatusEffects?.ActiveEffects.Any(e => e.BaseEffect.Name == "Weakness") ?? false;
            if (isWeakened) finalDamage /= 2;

            targetHealthComp.TakeDamage(finalDamage);
            EventBus.Publish(new GameEvents.EntityTookDamage { EntityId = targetId, DamageAmount = finalDamage });
            EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = $"{attackerName} attacks {targetName} with {effectiveStats.WeaponName} for [red]{finalDamage}[/] damage! {targetName} has {targetHealthComp.CurrentHealth}/{targetHealthComp.MaxHealth} HP remaining." });

            // --- STATUS EFFECT RESOLUTION ---
            if (effectiveStats.StatusEffects != null && effectiveStats.StatusEffects.Any())
            {
                var statusEffectSystem = ServiceLocator.Get<StatusEffectSystem>();
                for (int i = 0; i < effectiveStats.StatusEffects.Count; i++)
                {
                    var effectApp = effectiveStats.StatusEffects[i];
                    string groupId = $"{effectApp.EffectName.ToLower()}_amount_{i}";
                    int effectValue = GetResolvedValue(groupId, result);

                    var effect = statusEffectSystem.CreateEffectFromName(effectApp.EffectName, effectiveStats.WeaponName);
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
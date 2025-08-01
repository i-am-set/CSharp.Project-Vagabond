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
        private readonly DiceRollingSystem _diceRollingSystem;
        private readonly Global _global;
        private ItemManager _itemManager;

        private IAction _pendingAttackAction;
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
        /// Converts an integer number of sides into a DieType enum.
        /// </summary>
        /// <param name="numSides">The number of sides on the die.</param>
        /// <returns>The corresponding DieType, defaulting to D6.</returns>
        private static DieType DieTypeFromSides(int numSides)
        {
            switch (numSides)
            {
                case 4:
                    return DieType.D4;
                // Future-proofing for other dice
                // case 8: return DieType.D8;
                // case 10: return DieType.D10;
                // case 12: return DieType.D12;
                // case 20: return DieType.D20;
                case 6:
                default:
                    return DieType.D6;
            }
        }

        /// <summary>
        /// Initiates an attack by parsing all dice notations, requesting a dice roll for non-flat values,
        /// and preparing for the result.
        /// </summary>
        public void InitiateAttackResolution(IAction action)
        {
            _pendingAttackAction = action;
            // Immediately resolve the attack without rolling dice.
            HandleDiceRollCompleted(new DiceRollResult());
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
                    DisplayGroupId = groupId, // For status effects, they are their own display group
                    NumberOfDice = numDice,
                    DieType = DieTypeFromSides(numSides),
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

        private void HandleDiceRollCompleted(DiceRollResult result)
        {
            if (_pendingAttackAction == null) return;

            var attackerName = EntityNamer.GetName(_pendingAttackAction.ActorId);
            var targetName = EntityNamer.GetName(0); // Placeholder, as AttackAction is gone

            EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = $"{attackerName} attacks {targetName}!" });

            CleanupAndSignalCompletion();
        }

        private void CleanupAndSignalCompletion()
        {
            _pendingAttackAction = null;
            _flatModifiers.Clear();
            OnAttackResolved?.Invoke();
        }
    }
}
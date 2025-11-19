using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond;
using ProjectVagabond.Battle;
using ProjectVagabond.Progression;
using ProjectVagabond.Scenes;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace ProjectVagabond.Battle
{
    public class BattleCombatant
    {
        public int EntityId { get; set; }
        public string ArchetypeId { get; set; }
        public string CombatantID { get; set; }
        public string Name { get; set; }
        public CombatantStats Stats { get; set; }
        public float VisualHP { get; set; }
        public float VisualAlpha { get; set; } = 1.0f;

        public List<MoveData> AvailableMoves
        {
            get
            {
                if (IsPlayerControlled)
                {
                    return EquippedSpells
                        .Where(entry => entry != null && BattleDataCache.Moves.ContainsKey(entry.MoveID))
                        .Select(entry => BattleDataCache.Moves[entry.MoveID])
                        .ToList();
                }
                return _staticMoves;
            }
        }
        private List<MoveData> _staticMoves = new List<MoveData>();

        public MoveEntry?[] EquippedSpells { get; set; }

        public string DefaultStrikeMoveID { get; set; }
        public List<StatusEffectInstance> ActiveStatusEffects { get; set; } = new List<StatusEffectInstance>();
        public List<RelicData> ActiveRelics { get; set; } = new List<RelicData>();
        public List<int> DefensiveElementIDs { get; set; } = new List<int>();
        public bool IsPlayerControlled { get; set; }
        public bool IsDefeated => Stats.CurrentHP <= 0;
        public bool IsDying { get; set; } = false;
        public bool IsRemovalProcessed { get; set; } = false;
        public Dictionary<string, int> RampingMoveCounters { get; set; } = new Dictionary<string, int>();
        public DelayedAction ChargingAction { get; set; }
        public Queue<DelayedAction> DelayedActions { get; set; } = new Queue<DelayedAction>();
        public bool HasUsedFirstAttack { get; set; } = false;
        public bool IsSpellweaverActive { get; set; } = false;
        public bool IsMomentumActive { get; set; } = false;
        public int EscalationStacks { get; set; } = 0;
        public Dictionary<OffensiveStatType, int> StatStages { get; private set; }

        public BattleCombatant()
        {
            StatStages = new Dictionary<OffensiveStatType, int>
            {
                { OffensiveStatType.Strength, 0 },
                { OffensiveStatType.Intelligence, 0 },
                { OffensiveStatType.Tenacity, 0 },
                { OffensiveStatType.Agility, 0 }
            };
        }

        public void ApplyDamage(int damageAmount)
        {
            Stats.CurrentHP -= damageAmount;
            if (Stats.CurrentHP < 0) Stats.CurrentHP = 0;
        }

        public void ApplyHealing(int healAmount)
        {
            Stats.CurrentHP += healAmount;
            if (Stats.CurrentHP > Stats.MaxHP) Stats.CurrentHP = Stats.MaxHP;
        }

        public bool HasStatusEffect(StatusEffectType effectType) => ActiveStatusEffects.Any(e => e.EffectType == effectType);

        public bool AddStatusEffect(StatusEffectInstance newEffect)
        {
            foreach (var relic in ActiveRelics)
            {
                if (relic.Effects.TryGetValue("StatusImmunity", out var immunityValue))
                {
                    var immuneTypes = immunityValue.Split(',');
                    foreach (var typeStr in immuneTypes)
                    {
                        if (Enum.TryParse<StatusEffectType>(typeStr.Trim(), true, out var immuneType))
                        {
                            if (newEffect.EffectType == immuneType) return false;
                        }
                    }
                }
            }

            bool hadEffectBefore = HasStatusEffect(newEffect.EffectType);
            ActiveStatusEffects.RemoveAll(e => e.EffectType == newEffect.EffectType);
            newEffect.DurationInTurns += 1;
            ActiveStatusEffects.Add(newEffect);
            return !hadEffectBefore;
        }

        public void SetStaticMoves(List<MoveData> moves) { _staticMoves = moves; }

        public (bool success, string message) ModifyStatStage(OffensiveStatType stat, int amount)
        {
            int currentStage = StatStages[stat];
            if (amount > 0 && currentStage >= 6) return (false, $"{Name}'s {stat} won't go any higher!");
            if (amount < 0 && currentStage <= -6) return (false, $"{Name}'s {stat} won't go any lower!");

            int newStage = Math.Clamp(currentStage + amount, -6, 6);
            StatStages[stat] = newStage;

            string changeText = amount > 0 ? (amount > 1 ? "sharply rose" : "rose") : (amount < -1 ? "harshly fell" : "fell");
            return (true, $"{Name}'s {stat} {changeText}!");
        }

        public List<int> GetEffectiveDefensiveElementIDs()
        {
            var effectiveElements = new List<int>(this.DefensiveElementIDs);
            foreach (var relic in ActiveRelics)
            {
                if (relic.Effects.TryGetValue("AddDefensiveElement", out var elementIdStr) && int.TryParse(elementIdStr, out int elementId))
                {
                    if (!effectiveElements.Contains(elementId)) effectiveElements.Add(elementId);
                }
            }
            return effectiveElements;
        }

        public int GetEffectiveStrength()
        {
            float stat = Stats.Strength * BattleConstants.StatStageMultipliers[StatStages[OffensiveStatType.Strength]];
            if (HasStatusEffect(StatusEffectType.Fear)) stat *= 0.8f;
            return (int)Math.Round(stat);
        }

        public int GetEffectiveIntelligence()
        {
            float stat = Stats.Intelligence * BattleConstants.StatStageMultipliers[StatStages[OffensiveStatType.Intelligence]];
            if (HasStatusEffect(StatusEffectType.Fear)) stat *= 0.8f;
            return (int)Math.Round(stat);
        }

        public int GetEffectiveTenacity()
        {
            float stat = Stats.Tenacity * BattleConstants.StatStageMultipliers[StatStages[OffensiveStatType.Tenacity]];
            if (HasStatusEffect(StatusEffectType.Fear)) stat *= 0.8f;
            return (int)Math.Round(stat);
        }

        public int GetEffectiveAgility()
        {
            float stat = Stats.Agility * BattleConstants.StatStageMultipliers[StatStages[OffensiveStatType.Agility]];
            if (HasStatusEffect(StatusEffectType.Freeze)) stat *= 0.5f;
            if (HasStatusEffect(StatusEffectType.Fear)) stat *= 0.8f;

            foreach (var relic in ActiveRelics)
            {
                if (relic.Effects.TryGetValue("CorneredAnimal", out var value) && EffectParser.TryParseFloatArray(value, out float[] p) && p.Length == 3)
                {
                    var battleManager = ServiceLocator.Get<BattleManager>();
                    bool hpCondition = (float)Stats.CurrentHP / Stats.MaxHP * 100f < p[0];
                    int enemyCount = battleManager.AllCombatants.Count(c => c.IsPlayerControlled != this.IsPlayerControlled && !c.IsDefeated);
                    bool enemyCountCondition = enemyCount >= p[1];

                    if (hpCondition || enemyCountCondition)
                    {
                        stat *= (1.0f + (p[2] / 100f));
                        EventBus.Publish(new GameEvents.AbilityActivated { Combatant = this, Ability = relic });
                    }
                }
            }

            return (int)Math.Round(stat);
        }

        public int GetEffectiveAccuracy(int baseAccuracy)
        {
            float accuracy = baseAccuracy;
            if (HasStatusEffect(StatusEffectType.Blind)) accuracy *= 0.5f;
            return (int)Math.Round(accuracy);
        }
    }
}

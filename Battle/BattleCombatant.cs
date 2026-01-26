using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using MonoGame.Extended.ECS.Systems;
using ProjectVagabond;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Particles;
using ProjectVagabond.Progression;
using ProjectVagabond.Scenes;
using ProjectVagabond.Systems;
using ProjectVagabond.Transitions;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace ProjectVagabond.Battle
{
    public enum Gender
    {
        Male,
        Female,
        Thing, // It
        Neutral // They
    }

    public class BattleCombatant
    {
        public int EntityId { get; set; }
        public string ArchetypeId { get; set; }
        public string CombatantID { get; set; }
        public string Name { get; set; }
        public Gender Gender { get; set; } = Gender.Neutral;
        public bool IsProperNoun { get; set; } = false;

        public CombatantStats Stats { get; set; }
        public float VisualHP { get; set; }
        public float VisualAlpha { get; set; } = 1.0f;

        // --- NEW: HUD Visibility Control ---
        public float HudVisualAlpha { get; set; } = 0f;

        public float VisualSilhouetteAmount { get; set; } = 0f;
        public Color? VisualSilhouetteColorOverride { get; set; } = null;

        public int BattleSlot { get; set; } = -1;
        public bool IsActiveOnField => BattleSlot == 0 || BattleSlot == 1;
        public int CoinReward { get; set; } = 0;

        public List<MoveData> AvailableMoves
        {
            get
            {
                if (IsPlayerControlled)
                {
                    return Spells
                        .Where(entry => entry != null && BattleDataCache.Moves.ContainsKey(entry.MoveID))
                        .Select(entry => BattleDataCache.Moves[entry.MoveID])
                        .ToList();
                }
                return _staticMoves;
            }
        }
        private List<MoveData> _staticMoves = new List<MoveData>();
        public MoveEntry?[] Spells { get; set; } = new MoveEntry?[4];
        public string DefaultStrikeMoveID { get; set; }

        public MoveData StrikeMove
        {
            get
            {
                if (!string.IsNullOrEmpty(DefaultStrikeMoveID) && BattleDataCache.Moves.TryGetValue(DefaultStrikeMoveID, out var move))
                {
                    return move;
                }
                if (BattleDataCache.Moves.TryGetValue("0", out var punch)) return punch;
                return null;
            }
        }

        public int PortraitIndex { get; set; } = 0;
        public List<StatusEffectInstance> ActiveStatusEffects { get; set; } = new List<StatusEffectInstance>();
        public List<RelicData> ActiveRelics { get; set; } = new List<RelicData>();

        public List<IAbility> Abilities { get; private set; } = new List<IAbility>();

        public bool IsPlayerControlled { get; set; }
        public bool IsDefeated => Stats.CurrentHP <= 0;
        public bool IsDying { get; set; } = false;
        public bool IsRemovalProcessed { get; set; } = false;
        public Dictionary<string, int> RampingMoveCounters { get; set; } = new Dictionary<string, int>();
        public DelayedAction ChargingAction { get; set; }
        public Queue<DelayedAction> DelayedActions { get; set; } = new Queue<DelayedAction>();
        public bool HasUsedFirstAttack { get; set; } = false;
        public Dictionary<OffensiveStatType, int> StatStages { get; private set; }

        public int ConsecutiveProtectUses { get; set; } = 0;
        public bool UsedProtectThisTurn { get; set; } = false;
        public bool PendingDisengage { get; set; } = false;

        private bool _isDazed;
        public bool IsDazed
        {
            get => _isDazed;
            set
            {
                if (value == true)
                {
                    var ctx = new CombatTriggerContext { Actor = this };
                    NotifyAbilities(CombatEventType.CheckDazeImmunity, ctx);
                    if (ctx.IsCancelled) return;
                }
                _isDazed = value;
            }
        }

        public float HealthBarVisibleTimer { get; set; } = 0f;
        public float ManaBarVisibleTimer { get; set; } = 0f;
        public float VisualHealthBarAlpha { get; set; } = 0f;
        public float VisualManaBarAlpha { get; set; } = 0f;
        public float HealthBarDelayTimer { get; set; } = 0f;
        public float ManaBarDelayTimer { get; set; } = 0f;
        public float HealthBarDisappearTimer { get; set; } = 0f;
        public float ManaBarDisappearTimer { get; set; } = 0f;
        public float LowHealthFlashTimer { get; set; } = 0f;
        public const float BAR_DISAPPEAR_DURATION = 2.0f;
        public const float BAR_DELAY_DURATION = 1.2f;
        public const float BAR_VARIANCE_MAX = 0.5f;
        public float CurrentBarVariance { get; set; } = 0f;

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

        public void RegisterAbility(IAbility ability)
        {
            Abilities.Add(ability);
        }

        public void RegisterAbilities(IEnumerable<IAbility> abilities)
        {
            foreach (var ability in abilities) RegisterAbility(ability);
        }

        public void NotifyAbilities(CombatEventType type, CombatTriggerContext ctx)
        {
            foreach (var ability in Abilities)
            {
                ability.OnCombatEvent(type, ctx);
            }
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
            var ctx = new CombatTriggerContext { Actor = this, StatusType = newEffect.EffectType };
            NotifyAbilities(CombatEventType.CheckStatusImmunity, ctx);
            if (ctx.IsCancelled) return false;

            bool hadEffectBefore = HasStatusEffect(newEffect.EffectType);
            ActiveStatusEffects.RemoveAll(e => e.EffectType == newEffect.EffectType);
            ActiveStatusEffects.Add(newEffect);
            return !hadEffectBefore;
        }

        public void SetStaticMoves(List<MoveData> moves) { _staticMoves = moves; }

        public (bool success, string message) ModifyStatStage(OffensiveStatType stat, int amount)
        {
            var ctx = new CombatTriggerContext { Actor = this, StatType = stat, StatValue = amount };
            NotifyAbilities(CombatEventType.CheckStatChangeBlock, ctx);
            if (ctx.IsCancelled) return (false, $"{Name}'s ability prevented the stat change!");

            int currentStage = StatStages[stat];
            if (amount > 0 && currentStage >= 6) return (false, $"{Name}'s {stat} won't go any higher!");
            if (amount < 0 && currentStage <= -6) return (false, $"{Name}'s {stat} won't go any lower!");

            int newStage = Math.Clamp(currentStage + amount, -6, 6);
            StatStages[stat] = newStage;

            string changeText = amount > 0 ? (amount > 1 ? "sharply rose" : "rose") : (amount < -1 ? "harshly fell" : "fell");
            return (true, $"{Name}'s {stat} {changeText}!");
        }

        public int GetEffectiveStrength()
        {
            var ctx = new CombatTriggerContext { Actor = this, StatType = OffensiveStatType.Strength, StatValue = Stats.Strength };
            NotifyAbilities(CombatEventType.CalculateStat, ctx);
            float stat = ctx.StatValue * BattleConstants.StatStageMultipliers[StatStages[OffensiveStatType.Strength]];
            return (int)Math.Round(stat);
        }

        public int GetEffectiveIntelligence()
        {
            var ctx = new CombatTriggerContext { Actor = this, StatType = OffensiveStatType.Intelligence, StatValue = Stats.Intelligence };
            NotifyAbilities(CombatEventType.CalculateStat, ctx);
            float stat = ctx.StatValue * BattleConstants.StatStageMultipliers[StatStages[OffensiveStatType.Intelligence]];
            return (int)Math.Round(stat);
        }

        public int GetEffectiveTenacity()
        {
            var ctx = new CombatTriggerContext { Actor = this, StatType = OffensiveStatType.Tenacity, StatValue = Stats.Tenacity };
            NotifyAbilities(CombatEventType.CalculateStat, ctx);
            float stat = ctx.StatValue * BattleConstants.StatStageMultipliers[StatStages[OffensiveStatType.Tenacity]];
            return (int)Math.Round(stat);
        }

        public int GetEffectiveAgility()
        {
            var ctx = new CombatTriggerContext { Actor = this, StatType = OffensiveStatType.Agility, StatValue = Stats.Agility };
            NotifyAbilities(CombatEventType.CalculateStat, ctx);
            float stat = ctx.StatValue * BattleConstants.StatStageMultipliers[StatStages[OffensiveStatType.Agility]];
            if (HasStatusEffect(StatusEffectType.Frostbite)) stat *= Global.Instance.FrostbiteAgilityMultiplier;
            return (int)Math.Round(stat);
        }

        public int GetEffectiveAccuracy(int baseAccuracy)
        {
            var ctx = new CombatTriggerContext { Actor = this, StatValue = baseAccuracy };
            NotifyAbilities(CombatEventType.CheckAccuracy, ctx);
            return (int)Math.Round(ctx.StatValue);
        }
    }
}

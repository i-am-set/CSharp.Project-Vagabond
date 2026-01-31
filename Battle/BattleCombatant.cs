using Microsoft.Xna.Framework;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

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
        public string ArchetypeId { get; set; }
        public string CombatantID { get; set; }
        public string Name { get; set; }
        public Gender Gender { get; set; } = Gender.Neutral;
        public bool IsProperNoun { get; set; } = false;

        public CombatantStats Stats { get; set; }

        // CurrentTenacity acts as the shield points. Max is derived from Stats.Tenacity.
        public int CurrentTenacity { get; set; }

        public float VisualHP { get; set; }
        public float VisualAlpha { get; set; } = 1.0f;

        public float HudVisualAlpha { get; set; } = 0f;

        public float VisualSilhouetteAmount { get; set; } = 0f;
        public Color? VisualSilhouetteColorOverride { get; set; } = null;

        public int BattleSlot { get; set; } = -1;
        public bool IsActiveOnField => BattleSlot == 0 || BattleSlot == 1;
        public int CoinReward { get; set; } = 0;

        public TagContainer Tags { get; private set; } = new TagContainer();

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

        public List<IAbility> Abilities { get; private set; } = new List<IAbility>();

        // Kept for UI/Input logic distinction, but also mirrored in Tags ("Type.Player")
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

        // Visual/UI Timers
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
            // Sort by Priority Descending (High to Low) so high priority abilities execute first
            Abilities.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }

        public void RegisterAbilities(IEnumerable<IAbility> abilities)
        {
            foreach (var ability in abilities)
            {
                Abilities.Add(ability);
            }
            Abilities.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }

        public void NotifyAbilities(GameEvent e)
        {
            // 1. Intrinsic Abilities
            foreach (var ability in Abilities)
            {
                ability.OnEvent(e);
                if (e.IsHandled) return;
            }

            // 2. Status Effects (which contain abilities)
            // Iterate backwards in case status effects remove themselves during processing
            for (int i = ActiveStatusEffects.Count - 1; i >= 0; i--)
            {
                ActiveStatusEffects[i].OnEvent(e);
                if (e.IsHandled) return;
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
            // Note: Immunity checks should now be handled by the system triggering this, 
            // or by a "CanApplyStatusEvent" if implemented. 
            // For now, we assume the check happens before calling this or via the StatusAppliedEvent.

            bool hadEffectBefore = HasStatusEffect(newEffect.EffectType);
            ActiveStatusEffects.RemoveAll(e => e.EffectType == newEffect.EffectType);
            ActiveStatusEffects.Add(newEffect);

            // Notify that status was applied
            NotifyAbilities(new StatusAppliedEvent(this, newEffect));

            return !hadEffectBefore;
        }

        public void SetStaticMoves(List<MoveData> moves) { _staticMoves = moves; }

        public (bool success, string message) ModifyStatStage(OffensiveStatType stat, int amount)
        {
            // Note: Blocking logic (e.g. Scrappy) should now be handled via a specific event 
            // before calling this, or we need a "StatChangeAttemptEvent".
            // For this refactor step, we proceed with the modification.

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
            var evt = new CalculateStatEvent(this, OffensiveStatType.Strength, Stats.Strength);
            NotifyAbilities(evt);
            float stat = evt.FinalValue * BattleConstants.StatStageMultipliers[StatStages[OffensiveStatType.Strength]];
            return (int)Math.Round(stat);
        }

        public int GetEffectiveIntelligence()
        {
            var evt = new CalculateStatEvent(this, OffensiveStatType.Intelligence, Stats.Intelligence);
            NotifyAbilities(evt);
            float stat = evt.FinalValue * BattleConstants.StatStageMultipliers[StatStages[OffensiveStatType.Intelligence]];
            return (int)Math.Round(stat);
        }

        public int GetEffectiveTenacity()
        {
            var evt = new CalculateStatEvent(this, OffensiveStatType.Tenacity, Stats.Tenacity);
            NotifyAbilities(evt);
            float stat = evt.FinalValue * BattleConstants.StatStageMultipliers[StatStages[OffensiveStatType.Tenacity]];
            return (int)Math.Round(stat);
        }

        public int GetEffectiveAgility()
        {
            var evt = new CalculateStatEvent(this, OffensiveStatType.Agility, Stats.Agility);
            NotifyAbilities(evt);
            float stat = evt.FinalValue * BattleConstants.StatStageMultipliers[StatStages[OffensiveStatType.Agility]];

            // Frostbite logic should ideally be moved to an ability/event handler, 
            // but keeping here for parity if not migrated yet.
            if (HasStatusEffect(StatusEffectType.Frostbite)) stat *= Global.Instance.FrostbiteAgilityMultiplier;

            return (int)Math.Round(stat);
        }

        public int GetEffectiveAccuracy(int baseAccuracy)
        {
            // Note: This is a simplified check. Full accuracy logic usually involves target evasion.
            // This method might be deprecated in favor of CheckHitChanceEvent in the DamageCalculator.
            return baseAccuracy;
        }
    }
}
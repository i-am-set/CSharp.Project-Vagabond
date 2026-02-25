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

        public int CurrentGuard { get; set; }
        public int MaxGuard { get; set; }

        // VisualHP is distinct from Stats.CurrentHP to allow for smooth animation
        public float VisualHP { get; set; }
        public float VisualAlpha { get; set; } = 1.0f;

        public float HudVisualAlpha { get; set; } = 0f;

        public float VisualSilhouetteAmount { get; set; } = 0f;
        public Color? VisualSilhouetteColorOverride { get; set; } = null;

        public int BattleSlot { get; set; } = -1;
        public bool IsActiveOnField => BattleSlot == 0 || BattleSlot == 1;

        public TagContainer Tags { get; private set; } = new TagContainer();

        public List<MoveData> AvailableMoves
        {
            get
            {
                var moves = new List<MoveData>();
                if (BasicMove != null && BattleDataCache.Moves.TryGetValue(BasicMove.MoveID, out var str)) moves.Add(str);
                if (CoreMove != null && BattleDataCache.Moves.TryGetValue(CoreMove.MoveID, out var ctp)) moves.Add(ctp);
                if (AltMove != null && BattleDataCache.Moves.TryGetValue(AltMove.MoveID, out var spl)) moves.Add(spl);
                return moves;
            }
        }

        public MoveEntry? BasicMove { get; set; }
        public MoveEntry? CoreMove { get; set; }
        public MoveEntry? AltMove { get; set; }

        public int PortraitIndex { get; set; } = 0;
        public List<StatusEffectInstance> ActiveStatusEffects { get; set; } = new List<StatusEffectInstance>();

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

        public float HealthBarVisibleTimer { get; set; } = 0f;
        public float VisualHealthBarAlpha { get; set; } = 0f;
        public float HealthBarDelayTimer { get; set; } = 0f;
        public float HealthBarDisappearTimer { get; set; } = 0f;
        public float LowHealthFlashTimer { get; set; } = 0f;
        public const float BAR_DISAPPEAR_DURATION = 2.0f;
        public const float BAR_DELAY_DURATION = 1.2f;
        public const float BAR_VARIANCE_MAX = 0.5f;
        public float CurrentBarVariance { get; set; } = 0f;

        private readonly BattleContext _statContext = new BattleContext();

        public BattleCombatant()
        {
            StatStages = new Dictionary<OffensiveStatType, int>
            {
                { OffensiveStatType.Strength, 0 },
                { OffensiveStatType.Intelligence, 0 },
                { OffensiveStatType.Tenacity, 0 },
                { OffensiveStatType.Agility, 0 }
            };

            RegisterAbility(new StandardRulesAbility());
        }

        public void SnapVisuals()
        {
            VisualHP = Stats.CurrentHP;
        }

        public void RegisterAbility(IAbility ability)
        {
            Abilities.Add(ability);
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

        public void NotifyAbilities(GameEvent e, BattleContext context)
        {
            foreach (var ability in Abilities)
            {
                ability.OnEvent(e, context);
                if (e.IsHandled) return;
            }

            for (int i = ActiveStatusEffects.Count - 1; i >= 0; i--)
            {
                ActiveStatusEffects[i].OnEvent(e, context);
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
            bool hadEffectBefore = HasStatusEffect(newEffect.EffectType);
            ActiveStatusEffects.RemoveAll(e => e.EffectType == newEffect.EffectType);
            ActiveStatusEffects.Add(newEffect);

            var context = new BattleContext { Actor = this, Target = this };
            NotifyAbilities(new StatusAppliedEvent(this, newEffect), context);

            return !hadEffectBefore;
        }

        public (bool success, string message) ModifyStatStage(OffensiveStatType stat, int amount)
        {
            if (stat == OffensiveStatType.Tenacity) return (false, "Tenacity cannot be modified!");

            // Allow abilities (like Scrappy) to intercept or modify the change
            var attemptEvent = new StatChangeAttemptEvent(this, stat, amount);
            var context = new BattleContext { Actor = this, Target = this };
            NotifyAbilities(attemptEvent, context);

            if (attemptEvent.IsHandled) return (false, "Stat change prevented!");
            amount = attemptEvent.Amount;

            int currentStage = StatStages[stat];
            if (amount > 0 && currentStage >= 2) return (false, $"{Name}'s {stat} won't go higher!");
            if (amount < 0 && currentStage <= -2) return (false, $"{Name}'s {stat} won't go lower!");

            StatStages[stat] = Math.Clamp(currentStage + amount, -2, 2);

            // Fire the UI event (Struct)
            EventBus.Publish(new GameEvents.CombatantStatStageChanged { Target = this, Stat = stat, Amount = amount });

            string changeText = amount > 0 ? (amount > 1 ? "sharply rose" : "rose") : (amount < -1 ? "harshly fell" : "fell");
            return (true, $"{Name}'s {stat} {changeText}!");
        }

        public int GetEffectiveStrength()
        {
            _statContext.ResetMultipliers();
            _statContext.Actor = this;
            var evt = new CalculateStatEvent(this, OffensiveStatType.Strength, Stats.Strength);
            NotifyAbilities(evt, _statContext);
            float stat = evt.FinalValue * BattleConstants.StatStageMultipliers[StatStages[OffensiveStatType.Strength]];
            return (int)Math.Round(stat);
        }

        public int GetEffectiveIntelligence()
        {
            _statContext.ResetMultipliers();
            _statContext.Actor = this;
            var evt = new CalculateStatEvent(this, OffensiveStatType.Intelligence, Stats.Intelligence);
            NotifyAbilities(evt, _statContext);
            float stat = evt.FinalValue * BattleConstants.StatStageMultipliers[StatStages[OffensiveStatType.Intelligence]];
            return (int)Math.Round(stat);
        }

        public int GetEffectiveTenacity()
        {
            _statContext.ResetMultipliers();
            _statContext.Actor = this;
            var evt = new CalculateStatEvent(this, OffensiveStatType.Tenacity, Stats.Tenacity);
            NotifyAbilities(evt, _statContext);
            //float stat = evt.FinalValue * BattleConstants.StatStageMultipliers[StatStages[OffensiveStatType.Tenacity]];
            return (int)Math.Round(evt.FinalValue);
        }

        public int GetEffectiveAgility()
        {
            _statContext.ResetMultipliers();
            _statContext.Actor = this;
            var evt = new CalculateStatEvent(this, OffensiveStatType.Agility, Stats.Agility);
            NotifyAbilities(evt, _statContext);
            float stat = evt.FinalValue * BattleConstants.StatStageMultipliers[StatStages[OffensiveStatType.Agility]];

            if (HasStatusEffect(StatusEffectType.Frostbite)) stat *= Global.Instance.FrostbiteAgilityMultiplier;

            return (int)Math.Round(stat);
        }

        public int GetEffectiveAccuracy(int baseAccuracy)
        {
            return baseAccuracy;
        }
    }
}
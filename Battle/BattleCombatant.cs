using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Progression;
using ProjectVagabond.Scenes;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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

        // --- NARRATION DATA ---
        public Gender Gender { get; set; } = Gender.Neutral;
        public bool IsProperNoun { get; set; } = false; // If true, "THE" is omitted (e.g. "Oakley"). If false, "THE" is added (e.g. "The Spider").

        public CombatantStats Stats { get; set; }
        public float VisualHP { get; set; }
        public float VisualAlpha { get; set; } = 1.0f;
        public float VisualSilhouetteAmount { get; set; } = 0f;
        public Color? VisualSilhouetteColorOverride { get; set; } = null;

        public int BattleSlot { get; set; } = -1;
        public bool IsActiveOnField => BattleSlot == 0 || BattleSlot == 1;

        // --- ECONOMY ---
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

        // Reverted to string ID for serialization/simplicity
        public string DefaultStrikeMoveID { get; set; }

        // Cache for the generated weapon move to avoid rebuilding it every frame
        private MoveData _cachedWeaponMove;
        private string _cachedWeaponId;

        /// <summary>
        /// Gets the actual MoveData to use for the "Strike" command.
        /// If a weapon is equipped, returns the weapon's custom move.
        /// Otherwise, returns the move defined by DefaultStrikeMoveID.
        /// </summary>
        public MoveData StrikeMove
        {
            get
            {
                // 1. Check for Equipped Weapon
                if (!string.IsNullOrEmpty(EquippedWeaponId))
                {
                    // If weapon changed or cache is empty, rebuild
                    if (_cachedWeaponId != EquippedWeaponId)
                    {
                        if (BattleDataCache.Weapons.TryGetValue(EquippedWeaponId, out var weapon))
                        {
                            _cachedWeaponMove = weapon.ToMoveData();
                            _cachedWeaponId = EquippedWeaponId;
                        }
                    }

                    if (_cachedWeaponMove != null)
                    {
                        return _cachedWeaponMove;
                    }
                }

                // 2. Fallback to Default ID (Unarmed)
                if (!string.IsNullOrEmpty(DefaultStrikeMoveID) && BattleDataCache.Moves.TryGetValue(DefaultStrikeMoveID, out var move))
                {
                    return move;
                }

                // 3. Absolute Failsafe (Punch)
                if (BattleDataCache.Moves.TryGetValue("0", out var punch))
                {
                    return punch;
                }

                return null;
            }
        }

        public int PortraitIndex { get; set; } = 0;

        // Equipment
        public string? EquippedWeaponId { get; set; }
        public string? EquippedRelicId { get; set; }

        public List<StatusEffectInstance> ActiveStatusEffects { get; set; } = new List<StatusEffectInstance>();

        public List<RelicData> ActiveRelics { get; set; } = new List<RelicData>();

        public List<IAbility> Abilities { get; private set; } = new List<IAbility>();

        // Cached Interface Lists
        public List<IStatModifier> StatModifiers { get; private set; } = new List<IStatModifier>();
        public List<IStatChangeModifier> StatChangeModifiers { get; private set; } = new List<IStatChangeModifier>();
        public List<IOutgoingDamageModifier> OutgoingDamageModifiers { get; private set; } = new List<IOutgoingDamageModifier>();
        public List<IIncomingDamageModifier> IncomingDamageModifiers { get; private set; } = new List<IIncomingDamageModifier>();
        public List<IAllyDamageModifier> AllyDamageModifiers { get; private set; } = new List<IAllyDamageModifier>();
        public List<IDefensePenetrationModifier> DefensePenetrationModifiers { get; private set; } = new List<IDefensePenetrationModifier>();
        public List<IElementalAffinityModifier> ElementalAffinityModifiers { get; private set; } = new List<IElementalAffinityModifier>();
        public List<IIncomingStatusModifier> IncomingStatusModifiers { get; private set; } = new List<IIncomingStatusModifier>();
        public List<IOutgoingStatusModifier> OutgoingStatusModifiers { get; private set; } = new List<IOutgoingStatusModifier>();
        public List<IDazeImmunity> DazeImmunityModifiers { get; private set; } = new List<IDazeImmunity>();
        public List<IOnHitEffect> OnHitEffects { get; private set; } = new List<IOnHitEffect>();
        public List<IOnDamagedEffect> OnDamagedEffects { get; private set; } = new List<IOnDamagedEffect>();
        public List<ICritModifier> CritModifiers { get; private set; } = new List<ICritModifier>();
        public List<IAccuracyModifier> AccuracyModifiers { get; private set; } = new List<IAccuracyModifier>();
        public List<ITurnLifecycle> TurnLifecycleEffects { get; private set; } = new List<ITurnLifecycle>();
        public List<IBattleLifecycle> BattleLifecycleEffects { get; private set; } = new List<IBattleLifecycle>();
        public List<IActionModifier> ActionModifiers { get; private set; } = new List<IActionModifier>();
        public List<IOnActionComplete> OnActionCompleteEffects { get; private set; } = new List<IOnActionComplete>();
        public List<IOnKill> OnKillEffects { get; private set; } = new List<IOnKill>();
        public List<IOnCritReceived> OnCritReceivedEffects { get; private set; } = new List<IOnCritReceived>();
        public List<IOnStatusApplied> OnStatusAppliedEffects { get; private set; } = new List<IOnStatusApplied>();
        public List<ILifestealReaction> LifestealReactions { get; private set; } = new List<ILifestealReaction>();

        public List<int> WeaknessElementIDs { get; set; } = new List<int>();
        public List<int> ResistanceElementIDs { get; set; } = new List<int>();

        public bool IsPlayerControlled { get; set; }
        public bool IsDefeated => Stats.CurrentHP <= 0;
        public bool IsDying { get; set; } = false;
        public bool IsRemovalProcessed { get; set; } = false;
        public Dictionary<string, int> RampingMoveCounters { get; set; } = new Dictionary<string, int>();
        public DelayedAction ChargingAction { get; set; }
        public Queue<DelayedAction> DelayedActions { get; set; } = new Queue<DelayedAction>();
        public bool HasUsedFirstAttack { get; set; } = false;
        public Dictionary<OffensiveStatType, int> StatStages { get; private set; }

        // --- PROTECT MECHANIC STATE ---
        public int ConsecutiveProtectUses { get; set; } = 0;
        public bool UsedProtectThisTurn { get; set; } = false;

        // --- DISENGAGE MECHANIC STATE ---
        public bool PendingDisengage { get; set; } = false;

        // --- DAZED MECHANIC STATE ---
        private bool _isDazed;
        public bool IsDazed
        {
            get => _isDazed;
            set
            {
                if (value == true)
                {
                    // Check for immunity
                    foreach (var mod in DazeImmunityModifiers)
                    {
                        if (mod.ShouldBlockDaze(this)) return;
                    }
                }
                _isDazed = value;
            }
        }

        // --- UI VISIBILITY STATE ---
        public float HealthBarVisibleTimer { get; set; } = 0f;
        public float ManaBarVisibleTimer { get; set; } = 0f;

        // --- UI FADE STATE ---
        public float VisualHealthBarAlpha { get; set; } = 0f;
        public float VisualManaBarAlpha { get; set; } = 0f;

        // --- UI DISAPPEAR ANIMATION STATE ---
        public float HealthBarDelayTimer { get; set; } = 0f;
        public float ManaBarDelayTimer { get; set; } = 0f;
        public float HealthBarDisappearTimer { get; set; } = 0f;
        public float ManaBarDisappearTimer { get; set; } = 0f;

        // --- LOW HEALTH FLASH STATE ---
        public float LowHealthFlashTimer { get; set; } = 0f;

        // --- TUNING: Bar Animation ---
        public const float BAR_DISAPPEAR_DURATION = 2.0f; // Time to fade out (Slowed down from 0.5f)
        public const float BAR_DELAY_DURATION = 1.2f;     // Time to wait before fading
        public const float BAR_VARIANCE_MAX = 0.5f;       // Max random added delay

        // Instance-specific variance to desynchronize bars
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

            if (ability is IStatModifier sm) StatModifiers.Add(sm);
            if (ability is IStatChangeModifier scm) StatChangeModifiers.Add(scm);
            if (ability is IOutgoingDamageModifier odm) OutgoingDamageModifiers.Add(odm);
            if (ability is IIncomingDamageModifier idm) IncomingDamageModifiers.Add(idm);
            if (ability is IAllyDamageModifier adm) AllyDamageModifiers.Add(adm);
            if (ability is IDefensePenetrationModifier dpm) DefensePenetrationModifiers.Add(dpm);
            if (ability is IElementalAffinityModifier eam) ElementalAffinityModifiers.Add(eam);
            if (ability is IIncomingStatusModifier ism) IncomingStatusModifiers.Add(ism);
            if (ability is IOutgoingStatusModifier osm) OutgoingStatusModifiers.Add(osm);
            if (ability is IDazeImmunity dim) DazeImmunityModifiers.Add(dim);
            if (ability is IOnHitEffect ohe) OnHitEffects.Add(ohe);
            if (ability is IOnDamagedEffect ode) OnDamagedEffects.Add(ode);
            if (ability is ICritModifier cm) CritModifiers.Add(cm);
            if (ability is IAccuracyModifier am) AccuracyModifiers.Add(am);
            if (ability is ITurnLifecycle tl) TurnLifecycleEffects.Add(tl);
            if (ability is IBattleLifecycle bl) BattleLifecycleEffects.Add(bl);
            if (ability is IActionModifier actm) ActionModifiers.Add(actm);
            if (ability is IOnActionComplete oac) OnActionCompleteEffects.Add(oac);
            if (ability is IOnKill ok) OnKillEffects.Add(ok);
            if (ability is IOnCritReceived ocr) OnCritReceivedEffects.Add(ocr);
            if (ability is IOnStatusApplied osa) OnStatusAppliedEffects.Add(osa);
            if (ability is ILifestealReaction lsr) LifestealReactions.Add(lsr);
        }

        public void RegisterAbilities(IEnumerable<IAbility> abilities)
        {
            foreach (var ability in abilities) RegisterAbility(ability);
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
            foreach (var mod in IncomingStatusModifiers)
            {
                if (mod.ShouldBlockStatus(newEffect.EffectType, this)) return false;
            }

            bool hadEffectBefore = HasStatusEffect(newEffect.EffectType);
            ActiveStatusEffects.RemoveAll(e => e.EffectType == newEffect.EffectType);

            ActiveStatusEffects.Add(newEffect);
            return !hadEffectBefore;
        }

        public void SetStaticMoves(List<MoveData> moves) { _staticMoves = moves; }

        public (bool success, string message) ModifyStatStage(OffensiveStatType stat, int amount)
        {
            // Check for immunity to stat changes
            foreach (var mod in StatChangeModifiers)
            {
                if (mod.ShouldBlockStatChange(stat, amount, this))
                {
                    return (false, $"{Name}'s {mod.Name} prevented the stat change!");
                }
            }

            int currentStage = StatStages[stat];
            if (amount > 0 && currentStage >= 6) return (false, $"{Name}'s {stat} won't go any higher!");
            if (amount < 0 && currentStage <= -6) return (false, $"{Name}'s {stat} won't go any lower!");

            int newStage = Math.Clamp(currentStage + amount, -6, 6);
            StatStages[stat] = newStage;

            string changeText = amount > 0 ? (amount > 1 ? "sharply rose" : "rose") : (amount < -1 ? "harshly fell" : "fell");
            return (true, $"{Name}'s {stat} {changeText}!");
        }

        public (List<int> Weaknesses, List<int> Resistances) GetEffectiveElementalAffinities()
        {
            var effectiveWeaknesses = new List<int>(this.WeaknessElementIDs);
            var effectiveResistances = new List<int>(this.ResistanceElementIDs);

            foreach (var mod in ElementalAffinityModifiers)
            {
                mod.ModifyElementalAffinities(effectiveWeaknesses, effectiveResistances, this);
            }
            return (effectiveWeaknesses, effectiveResistances);
        }

        public int GetEffectiveStrength()
        {
            float stat = Stats.Strength;
            foreach (var mod in StatModifiers) stat = mod.ModifyStat(OffensiveStatType.Strength, (int)stat, this);
            stat *= BattleConstants.StatStageMultipliers[StatStages[OffensiveStatType.Strength]];
            return (int)Math.Round(stat);
        }

        public int GetEffectiveIntelligence()
        {
            float stat = Stats.Intelligence;
            foreach (var mod in StatModifiers) stat = mod.ModifyStat(OffensiveStatType.Intelligence, (int)stat, this);
            stat *= BattleConstants.StatStageMultipliers[StatStages[OffensiveStatType.Intelligence]];
            return (int)Math.Round(stat);
        }

        public int GetEffectiveTenacity()
        {
            float stat = Stats.Tenacity;
            foreach (var mod in StatModifiers) stat = mod.ModifyStat(OffensiveStatType.Tenacity, (int)stat, this);
            stat *= BattleConstants.StatStageMultipliers[StatStages[OffensiveStatType.Tenacity]];
            return (int)Math.Round(stat);
        }

        public int GetEffectiveAgility()
        {
            float stat = Stats.Agility;
            foreach (var mod in StatModifiers)
            {
                if (mod is CorneredAnimalAbility ca) stat = ca.ModifyStat(OffensiveStatType.Agility, (int)stat, this);
                else stat = mod.ModifyStat(OffensiveStatType.Agility, (int)stat, this);
            }
            stat *= BattleConstants.StatStageMultipliers[StatStages[OffensiveStatType.Agility]];

            // Frostbite cuts agility in half
            if (HasStatusEffect(StatusEffectType.Frostbite))
            {
                stat *= Global.Instance.FrostbiteAgilityMultiplier;
            }

            return (int)Math.Round(stat);
        }

        public int GetEffectiveAccuracy(int baseAccuracy)
        {
            float accuracy = baseAccuracy;
            var ctx = new CombatContext { Actor = this };
            foreach (var mod in AccuracyModifiers) accuracy = mod.ModifyAccuracy((int)accuracy, ctx);
            return (int)Math.Round(accuracy);
        }
    }
}
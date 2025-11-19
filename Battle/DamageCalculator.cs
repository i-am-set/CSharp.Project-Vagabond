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
    public static class DamageCalculator
    {
        private static readonly Random _random = new Random();

        public enum ElementalEffectiveness { Neutral, Effective, Resisted, Immune }

        public struct DamageResult
        {
            public int DamageAmount;
            public bool WasCritical;
            public bool WasGraze;
            public ElementalEffectiveness Effectiveness;
            public List<RelicData> AttackerAbilitiesTriggered;
            public List<RelicData> DefenderAbilitiesTriggered;
        }

        public static int GetEffectiveMovePower(BattleCombatant attacker, MoveData move)
        {
            float movePower = move.Power;

            foreach (var relic in attacker.ActiveRelics)
            {
                if (relic.Effects.TryGetValue("DamageBonus", out var damageBonusValue))
                {
                    var parts = damageBonusValue.Split(',');
                    if (parts.Length == 2 &&
                        EffectParser.TryParseInt(parts[0].Trim(), out int elementId) &&
                        EffectParser.TryParseFloat(parts[1].Trim(), out float bonusPercent))
                    {
                        if (move.MoveType == MoveType.Spell && move.OffensiveElementIDs.Contains(elementId))
                        {
                            movePower *= (1.0f + (bonusPercent / 100f));
                        }
                    }
                }
            }

            return (int)Math.Round(movePower);
        }

        public static DamageResult CalculateDamage(QueuedAction action, BattleCombatant target, MoveData move, float multiTargetModifier = 1.0f)
        {
            var attacker = action.Actor;
            var result = new DamageResult
            {
                Effectiveness = ElementalEffectiveness.Neutral,
                AttackerAbilitiesTriggered = new List<RelicData>(),
                DefenderAbilitiesTriggered = new List<RelicData>()
            };

            if (move.Effects.TryGetValue("FixedDamage", out var fixedDamageValue) && EffectParser.TryParseInt(fixedDamageValue, out int fixedDamage))
            {
                result.DamageAmount = fixedDamage;
                return result;
            }

            if (move.Power == 0)
            {
                return result;
            }

            if (move.Accuracy != -1)
            {
                if (target.HasStatusEffect(StatusEffectType.Dodging))
                {
                    result.WasGraze = true;
                }
                else
                {
                    int effectiveAccuracy = attacker.GetEffectiveAccuracy(move.Accuracy);
                    if (_random.Next(1, 101) > effectiveAccuracy)
                    {
                        result.WasGraze = true;
                    }
                }
            }

            float movePower = GetEffectiveMovePower(attacker, move);

            if (action.IsLastActionInRound)
            {
                foreach (var relic in attacker.ActiveRelics)
                {
                    if (relic.Effects.TryGetValue("PowerBonusLastAct", out var value) && EffectParser.TryParseFloat(value, out float bonusPercent))
                    {
                        movePower *= (1.0f + (bonusPercent / 100f));
                        result.AttackerAbilitiesTriggered.Add(relic);
                    }
                }
            }

            if (move.Effects.ContainsKey("RampUp") && attacker.RampingMoveCounters.TryGetValue(move.MoveID, out int useCount))
            {
                if (EffectParser.TryParseIntArray(move.Effects["RampUp"], out int[] rampParams) && rampParams.Length == 2)
                {
                    int bonusPerUse = rampParams[0];
                    int maxBonus = rampParams[1];
                    movePower += Math.Min(useCount * bonusPerUse, maxBonus);
                }
            }

            float offensiveStat;
            switch (move.OffensiveStat)
            {
                case OffensiveStatType.Strength: offensiveStat = attacker.GetEffectiveStrength(); break;
                case OffensiveStatType.Intelligence: offensiveStat = attacker.GetEffectiveIntelligence(); break;
                case OffensiveStatType.Tenacity: offensiveStat = attacker.GetEffectiveTenacity(); break;
                case OffensiveStatType.Agility: offensiveStat = attacker.GetEffectiveAgility(); break;
                default: offensiveStat = attacker.GetEffectiveStrength(); break;
            }

            float defensiveStat = target.GetEffectiveTenacity();
            if (move.Effects.TryGetValue("ArmorPierce", out var armorPierceValue) && EffectParser.TryParseFloat(armorPierceValue, out float piercePercent))
            {
                defensiveStat *= (1.0f - (piercePercent / 100f));
            }
            if (defensiveStat <= 0) defensiveStat = 1;

            float baseDamage = ((((2f * attacker.Stats.Level / 5f + 2f) * movePower * (offensiveStat / defensiveStat)) / 50f) + 2f);

            float finalDamage = baseDamage;

            foreach (var relic in attacker.ActiveRelics)
            {
                if (relic.Effects.TryGetValue("DamageBonusLowHP", out var adrValue))
                {
                    var parts = adrValue.Split(',');
                    if (parts.Length == 2 && EffectParser.TryParseFloat(parts[0], out float hpThreshold) && EffectParser.TryParseFloat(parts[1], out float bonus))
                    {
                        if ((float)attacker.Stats.CurrentHP / attacker.Stats.MaxHP * 100f < hpThreshold)
                        {
                            finalDamage *= (1.0f + (bonus / 100f));
                            result.AttackerAbilitiesTriggered.Add(relic);
                        }
                    }
                }

                if (relic.Effects.TryGetValue("DamageBonusVsStatused", out var oppValue))
                {
                    if (target.ActiveStatusEffects.Any() && EffectParser.TryParseFloat(oppValue, out float bonus))
                    {
                        finalDamage *= (1.0f + (bonus / 100f));
                        result.AttackerAbilitiesTriggered.Add(relic);
                    }
                }

                if (!attacker.HasUsedFirstAttack && relic.Effects.TryGetValue("FirstAttackBonus", out var fbValue))
                {
                    if (EffectParser.TryParseFloat(fbValue, out float bonus))
                    {
                        finalDamage *= (1.0f + (bonus / 100f));
                        result.AttackerAbilitiesTriggered.Add(relic);
                    }
                }

                if (relic.Effects.TryGetValue("Bloodletter", out var bloodletterValue) && EffectParser.TryParseFloatArray(bloodletterValue, out float[] bloodletterParams) && bloodletterParams.Length == 2)
                {
                    if (move.MoveType == MoveType.Spell && move.OffensiveElementIDs.Contains(9))
                    {
                        finalDamage *= (1.0f + (bloodletterParams[1] / 100f));
                        result.AttackerAbilitiesTriggered.Add(relic);
                    }
                }

                if (attacker.IsSpellweaverActive && relic.Effects.TryGetValue("Spellweaver", out var spellweaverValue))
                {
                    if (move.MoveType == MoveType.Spell && EffectParser.TryParseFloat(spellweaverValue, out float bonus))
                    {
                        finalDamage *= (1.0f + (bonus / 100f));
                        result.AttackerAbilitiesTriggered.Add(relic);
                    }
                }

                if (attacker.IsMomentumActive && relic.Effects.TryGetValue("Momentum", out var momentumValue))
                {
                    if (move.Power > 0 && EffectParser.TryParseFloat(momentumValue, out float bonus))
                    {
                        finalDamage *= (1.0f + (bonus / 100f));
                        result.AttackerAbilitiesTriggered.Add(relic);
                    }
                }

                if (attacker.EscalationStacks > 0 && relic.Effects.TryGetValue("Escalation", out var escalationValue))
                {
                    if (EffectParser.TryParseIntArray(escalationValue, out int[] escalationParams) && escalationParams.Length == 2)
                    {
                        float bonusPerStack = escalationParams[0];
                        float totalBonus = attacker.EscalationStacks * bonusPerStack;
                        finalDamage *= (1.0f + (totalBonus / 100f));
                        result.AttackerAbilitiesTriggered.Add(relic);
                    }
                }
            }

            if (move.Effects.TryGetValue("Execute", out var executeValue) && EffectParser.TryParseFloatArray(executeValue, out float[] execParams) && execParams.Length == 2)
            {
                float hpThreshold = execParams[0];
                float damageMultiplier = execParams[1];
                if ((float)target.Stats.CurrentHP / target.Stats.MaxHP <= (hpThreshold / 100f))
                {
                    finalDamage *= damageMultiplier;
                }
            }

            finalDamage *= multiTargetModifier;

            if (!result.WasGraze)
            {
                double critChance = BattleConstants.CRITICAL_HIT_CHANCE;
                foreach (var relic in attacker.ActiveRelics)
                {
                    if (relic.Effects.TryGetValue("CritChanceBonus", out var value) && EffectParser.TryParseFloat(value, out float bonus))
                    {
                        critChance += bonus / 100.0;
                    }
                }

                if (target.HasStatusEffect(StatusEffectType.Root)) critChance *= 2.0;
                if (_random.NextDouble() < critChance)
                {
                    result.WasCritical = true;
                    finalDamage *= BattleConstants.CRITICAL_HIT_MULTIPLIER;

                    foreach (var relic in target.ActiveRelics)
                    {
                        if (relic.Effects.TryGetValue("CritDamageReduction", out var value) && EffectParser.TryParseFloat(value, out float reductionPercent))
                        {
                            finalDamage *= (1.0f - (reductionPercent / 100f));
                            result.DefenderAbilitiesTriggered.Add(relic);
                        }
                    }
                }
            }

            float elementalMultiplier = GetElementalMultiplier(move, target, result.DefenderAbilitiesTriggered);
            if (elementalMultiplier > 1.0f) result.Effectiveness = ElementalEffectiveness.Effective;
            else if (elementalMultiplier > 0f && elementalMultiplier < 1.0f) result.Effectiveness = ElementalEffectiveness.Resisted;
            else if (elementalMultiplier == 0f) result.Effectiveness = ElementalEffectiveness.Immune;

            finalDamage *= elementalMultiplier;

            if (target.HasStatusEffect(StatusEffectType.Freeze) && move.ImpactType == ImpactType.Physical)
            {
                finalDamage *= 2.0f;
            }

            foreach (var relic in target.ActiveRelics)
            {
                if (relic.Effects.TryGetValue("DamageReductionPhysical", out var value) && EffectParser.TryParseFloat(value, out float reduction))
                {
                    if (move.ImpactType == ImpactType.Physical)
                    {
                        finalDamage *= (1.0f - (reduction / 100f));
                    }
                }
            }

            foreach (var relic in target.ActiveRelics)
            {
                if (relic.Effects.TryGetValue("Vigor", out var vigorValue) && EffectParser.TryParseFloatArray(vigorValue, out float[] vigorParams) && vigorParams.Length == 2)
                {
                    float hpThreshold = vigorParams[0];
                    float damageReduction = vigorParams[1];

                    if ((float)target.Stats.CurrentHP / target.Stats.MaxHP * 100f > hpThreshold)
                    {
                        finalDamage *= (1.0f - (damageReduction / 100f));
                        result.DefenderAbilitiesTriggered.Add(relic);
                    }
                }
            }

            finalDamage *= (float)(_random.NextDouble() * (BattleConstants.RANDOM_VARIANCE_MAX - BattleConstants.RANDOM_VARIANCE_MIN) + BattleConstants.RANDOM_VARIANCE_MIN);

            if (result.WasGraze)
            {
                finalDamage *= BattleConstants.GRAZE_MULTIPLIER;
            }

            int finalDamageAmount = (int)Math.Floor(finalDamage);

            if (finalDamage > 0 && finalDamageAmount == 0)
            {
                finalDamageAmount = 1;
            }

            result.DamageAmount = finalDamageAmount;
            return result;
        }

        public static int CalculateBaselineDamage(BattleCombatant attacker, BattleCombatant target, MoveData move)
        {
            if (move.Power == 0) return 0;

            float movePower = move.Power;

            float offensiveStat;
            switch (move.OffensiveStat)
            {
                case OffensiveStatType.Strength: offensiveStat = attacker.Stats.Strength; break;
                case OffensiveStatType.Intelligence: offensiveStat = attacker.Stats.Intelligence; break;
                case OffensiveStatType.Tenacity: offensiveStat = attacker.Stats.Tenacity; break;
                case OffensiveStatType.Agility: offensiveStat = attacker.Stats.Agility; break;
                default: offensiveStat = attacker.Stats.Strength; break;
            }

            float defensiveStat = target.Stats.Tenacity;
            if (defensiveStat <= 0) defensiveStat = 1;

            float baseDamage = ((((2f * attacker.Stats.Level / 5f + 2f) * movePower * (offensiveStat / defensiveStat)) / 50f) + 2f);

            baseDamage *= BattleConstants.RANDOM_VARIANCE_MAX;

            int finalDamageAmount = (int)Math.Floor(baseDamage);
            if (baseDamage > 0 && finalDamageAmount == 0)
            {
                finalDamageAmount = 1;
            }

            return finalDamageAmount;
        }


        public static float GetElementalMultiplier(MoveData move, BattleCombatant target)
        {
            return GetElementalMultiplier(move, target, null);
        }

        public static float GetElementalMultiplier(MoveData move, BattleCombatant target, List<RelicData> defenderAbilitiesTriggered)
        {
            foreach (var relic in target.ActiveRelics)
            {
                if (relic.Effects.TryGetValue("ElementImmunityAndHeal", out var healValue))
                {
                    var parts = healValue.Split(',');
                    if (parts.Length == 2 && EffectParser.TryParseInt(parts[0], out int immuneElementId))
                    {
                        if (move.OffensiveElementIDs.Contains(immuneElementId))
                        {
                            defenderAbilitiesTriggered?.Add(relic);
                            return 0f;
                        }
                    }
                }

                if (relic.Effects.TryGetValue("ElementImmunity", out var immunityValue))
                {
                    if (EffectParser.TryParseInt(immunityValue, out int immuneElementId))
                    {
                        if (move.OffensiveElementIDs.Contains(immuneElementId))
                        {
                            defenderAbilitiesTriggered?.Add(relic);
                            return 0f;
                        }
                    }
                }
            }

            var targetDefensiveElements = target.GetEffectiveDefensiveElementIDs();
            if (move.OffensiveElementIDs == null || !move.OffensiveElementIDs.Any() ||
                targetDefensiveElements == null || !targetDefensiveElements.Any())
            {
                return 1.0f;
            }

            float finalMultiplier = 1.0f;

            foreach (int offensiveId in move.OffensiveElementIDs)
            {
                if (BattleDataCache.InteractionMatrix.TryGetValue(offensiveId, out var attackRow))
                {
                    foreach (int defensiveId in targetDefensiveElements)
                    {
                        if (attackRow.TryGetValue(defensiveId, out float multiplier))
                        {
                            finalMultiplier *= multiplier;
                        }
                    }
                }
            }

            return finalMultiplier;
        }
    }
}

using ProjectVagabond.Battle;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ProjectVagabond.Battle.Abilities
{
    public static class AbilityFactory
    {
        public static List<IAbility> CreateAbilitiesFromData(Dictionary<string, string> effects, Dictionary<string, int> statModifiers)
        {
            var abilities = new List<IAbility>();

            if (statModifiers != null && statModifiers.Count > 0)
            {
                abilities.Add(new FlatStatBonusAbility(statModifiers));
            }

            if (effects == null) return abilities;

            foreach (var kvp in effects)
            {
                try
                {
                    IAbility ability = CreateAbility(kvp.Key, kvp.Value);
                    if (ability != null) abilities.Add(ability);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AbilityFactory] Error parsing ability '{kvp.Key}': {ex.Message}");
                }
            }

            return abilities;
        }

        private static IAbility CreateAbility(string key, string value)
        {
            string[] parts = value.Split(',');

            switch (key)
            {
                case "DamageBonusLowHP":
                    if (parts.Length == 2 && EffectParser.TryParseFloat(parts[0], out float hpThresh) && EffectParser.TryParseFloat(parts[1], out float hpBonus))
                        return new LowHPDamageBonusAbility(hpThresh, hpBonus);
                    break;

                case "DamageBonus":
                    if (parts.Length == 2 && EffectParser.TryParseInt(parts[0], out int elemId) && EffectParser.TryParseFloat(parts[1], out float elemBonus))
                        return new ElementalDamageBonusAbility(elemId, elemBonus);
                    break;

                case "DamageReductionPhysical":
                    if (EffectParser.TryParseFloat(value, out float physRed))
                        return new PhysicalDamageReductionAbility(physRed);
                    break;

                case "ApplyStatusOnContact":
                    if (EffectParser.TryParseStatusEffectParams(value, out var statusType, out int chance, out int duration))
                        return new ApplyStatusOnHitAbility(statusType, chance, duration, true);
                    break;

                case "IronBarbsOnContact":
                    if (EffectParser.TryParseFloat(value, out float thornsPercent))
                        return new ThornsAbility(thornsPercent);
                    break;

                case "CritChanceBonus":
                    if (EffectParser.TryParseFloat(value, out float critBonus))
                        return new FlatCritBonusAbility(critBonus);
                    break;

                case "CritDamageReduction":
                    if (EffectParser.TryParseFloat(value, out float critRed))
                        return new CritDamageReductionAbility(critRed);
                    break;

                case "IgnoreDodging":
                    return new IgnoreEvasionAbility();

                case "RegenEndOfTurn":
                    if (EffectParser.TryParseFloat(value, out float regenPercent))
                        return new RegenAbility(regenPercent);
                    break;

                case "AuraApplyStatusEndOfTurn":
                    if (parts.Length == 3 && Enum.TryParse<StatusEffectType>(parts[0], true, out var auraType) && int.TryParse(parts[1], out int auraChance) && int.TryParse(parts[2], out int auraDur))
                        return new ToxicAuraAbility(auraChance, auraDur);
                    break;

                case "IntimidateOnEnter":
                    if (EffectParser.TryParseStatStageAbilityParams(value, out var stat, out int amount))
                        return new IntimidateAbility(stat, amount);
                    break;

                case "CorneredAnimal":
                    if (parts.Length == 3 && float.TryParse(parts[0], out float caHp) && int.TryParse(parts[1], out int caCount) && float.TryParse(parts[2], out float caBonus))
                        return new CorneredAnimalAbility(caHp, caCount, caBonus);
                    break;

                case "GlassCannon":
                    if (parts.Length == 2 && float.TryParse(parts[0], out float gcOut) && float.TryParse(parts[1], out float gcIn))
                        return new GlassCannonAbility(gcOut, gcIn);
                    break;

                case "ElementImmunityAndHeal":
                    if (parts.Length == 2 && int.TryParse(parts[0], out int immId) && float.TryParse(parts[1], out float immHeal))
                        return new SunBlessedLeafAbility(immId, immHeal);
                    break;

                case "AmbushPredator":
                    if (parts.Length == 2 && int.TryParse(parts[0], out int prio) && float.TryParse(parts[1], out float pwr))
                        return new AmbushPredatorAbility(prio, pwr);
                    break;

                case "Spellweaver":
                    if (float.TryParse(value, out float swBonus))
                        return new SpellweaverAbility(swBonus);
                    break;

                case "Momentum":
                    if (float.TryParse(value, out float momBonus))
                        return new MomentumAbility(momBonus);
                    break;

                case "Escalation":
                    if (parts.Length == 2 && float.TryParse(parts[0], out float escBonus) && float.TryParse(parts[1], out float escMax))
                        return new EscalationAbility(escBonus, escMax);
                    break;

                case "PainFuel":
                    return new PainFuelAbility();

                case "Contagion":
                    if (parts.Length == 2 && int.TryParse(parts[0], out int contChance) && int.TryParse(parts[1], out int contDur))
                        return new ContagionAbility(contChance, contDur);
                    break;

                case "Sadist":
                    return new SadistAbility();

                case "CausticBlood":
                    return new CausticBloodAbility();

                case "FirstAttackBonus":
                    if (float.TryParse(value, out float faBonus))
                        return new FirstAttackDamageAbility(faBonus);
                    break;
            }

            return null;
        }
    }
}
using System.Collections.Generic;
using ProjectVagabond.Battle.Abilities;

namespace ProjectVagabond.Battle
{
    public enum ModifierCategory
    {
        Targeting,
        BaseDamage,
        Cooldown,
        Element,
        StatusEffect,
        Priority,
        AnimationOverride,
        Lifesteal
    }

    public class ModifierToken
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public HashSet<ModifierCategory> ModifiedCategories { get; set; } = new HashSet<ModifierCategory>();
        public bool IsDisabled { get; set; }

        public TargetType? TargetOverride { get; set; }
        public int? FlatDamageBonus { get; set; }
        public float? DamageMultiplier { get; set; }
        public int? CooldownModifier { get; set; }
        public string AnimationIdOverride { get; set; }

        public List<IAbility> AppendedAbilities { get; set; } = new List<IAbility>();

        public static void ResolveConflicts(List<ModifierToken> chronologicalTokens)
        {
            var claimedCategories = new HashSet<ModifierCategory>();

            for (int i = chronologicalTokens.Count - 1; i >= 0; i--)
            {
                var token = chronologicalTokens[i];

                if (token.ModifiedCategories.Overlaps(claimedCategories))
                {
                    token.IsDisabled = true;
                }
                else
                {
                    token.IsDisabled = false;
                    foreach (var category in token.ModifiedCategories)
                    {
                        claimedCategories.Add(category);
                    }
                }
            }
        }
    }
}
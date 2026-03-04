using System.Collections.Generic;
using System.Linq;
using ProjectVagabond.Battle.Abilities;

namespace ProjectVagabond.Battle
{
    public class CompiledMove
    {
        public readonly MoveData BaseTemplate;
        public readonly List<ModifierToken> Tokens;

        public readonly TargetType FinalTargetType;
        public readonly int FinalPower;
        public readonly int FinalAccuracy;
        public readonly int FinalCooldown;
        public readonly int FinalPriority;
        public readonly string FinalAnimationId;
        public readonly List<IAbility> FinalAbilities;

        public readonly string CachedTooltipStatsLine1;
        public readonly string CachedTooltipStatsLine2;
        public readonly List<string> CachedTokenLines;

        public CompiledMove(MoveData baseTemplate, List<ModifierToken> tokens)
        {
            BaseTemplate = baseTemplate;
            Tokens = tokens ?? new List<ModifierToken>();
            FinalAbilities = new List<IAbility>();

            ModifierToken.ResolveConflicts(Tokens);

            FinalTargetType = BaseTemplate.Target;
            FinalPower = BaseTemplate.Power;
            FinalAccuracy = BaseTemplate.Accuracy;
            FinalCooldown = BaseTemplate.Cooldown;
            FinalPriority = BaseTemplate.Priority;
            FinalAnimationId = BaseTemplate.AnimationId;

            if (BaseTemplate.Abilities != null)
            {
                FinalAbilities.AddRange(BaseTemplate.Abilities);
            }

            foreach (var token in Tokens)
            {
                if (token.IsDisabled) continue;

                if (token.TargetOverride.HasValue)
                {
                    FinalTargetType = token.TargetOverride.Value;
                }

                if (token.FlatDamageBonus.HasValue)
                {
                    FinalPower += token.FlatDamageBonus.Value;
                }

                if (token.DamageMultiplier.HasValue)
                {
                    FinalPower = (int)(FinalPower * token.DamageMultiplier.Value);
                }

                if (token.CooldownModifier.HasValue)
                {
                    FinalCooldown += token.CooldownModifier.Value;
                }

                if (!string.IsNullOrEmpty(token.AnimationIdOverride))
                {
                    FinalAnimationId = token.AnimationIdOverride;
                }

                if (token.AppendedAbilities != null)
                {
                    FinalAbilities.AddRange(token.AppendedAbilities);
                }
            }

            FinalAbilities.Sort((a, b) => b.Priority.CompareTo(a.Priority));

            string accStr = FinalAccuracy > 0 ? $"{FinalAccuracy}%" : "--";
            CachedTooltipStatsLine1 = $"PWR: {FinalPower}  ACC: {accStr}  CD: {FinalCooldown}";

            string tgtStr = FinalTargetType switch
            {
                TargetType.SingleAll => "SINGLE ALL",
                TargetType.SingleTeam => "SINGLE TEAM",
                TargetType.RandomBoth => "RANDOM BOTH",
                TargetType.RandomEvery => "RANDOM EVERY",
                TargetType.RandomAll => "RANDOM ALL",
                _ => FinalTargetType.ToString().ToUpper()
            };
            CachedTooltipStatsLine2 = $"TARGET: {tgtStr}";

            CachedTokenLines = Tokens.Where(t => !t.IsDisabled).Select(t => t.Name.ToUpper()).ToList();
            if (CachedTokenLines.Count == 0)
            {
                CachedTokenLines.Add("BASE SPELL");
            }
        }
    }
}
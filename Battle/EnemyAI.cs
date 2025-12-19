using Microsoft.Xna.Framework;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Battle
{
    /// <summary>
    /// A sophisticated AI evaluator that determines the optimal move for an enemy combatant.
    /// Inspired by VGC competitive logic and high-difficulty ROM hacks.
    /// </summary>
    public static class EnemyAI
    {
        // --- SCORING WEIGHTS ---
        // Offensive (vs Enemies)
        private const float SCORE_KILL_ENEMY = 10000f;          // Securing a KO is top priority
        private const float SCORE_DAMAGE_ENEMY = 100f;          // Per 1% HP damage dealt
        private const float SCORE_DEBUFF_ENEMY = 1500f;         // Lowering enemy stats
        private const float SCORE_STATUS_ENEMY = 2000f;         // Inflicting status
        private const float SCORE_DISABLE_ENEMY = 3000f;        // Hard CC (Stun/Freeze/Sleep)

        // Supportive (vs Allies)
        private const float SCORE_HEAL_ALLY = 150f;             // Per 1% HP healed (weighted higher than damage to prioritize survival)
        private const float SCORE_BUFF_ALLY = 2000f;            // Boosting ally stats
        private const float SCORE_CLEANSE_ALLY = 1500f;         // Removing negative status

        // Penalties (Bad Moves)
        private const float PENALTY_IMMUNE = -10000f;           // Move does nothing
        private const float PENALTY_RESISTED = -500f;           // Move is resisted
        private const float PENALTY_OVERKILL = -200f;           // Wasting big resources on low HP target
        private const float PENALTY_REDUNDANT = -5000f;         // Buffing maxed stat / Statusing statused target

        // Friendly Fire Penalties (The "Don't Hit Teammates" Logic)
        private const float PENALTY_DAMAGE_ALLY = -500f;        // Per 1% HP damage dealt to ally (5x penalty vs damage value)
        private const float PENALTY_KILL_ALLY = -1000000f;      // Absolutely forbid killing a teammate
        private const float PENALTY_DEBUFF_ALLY = -5000f;       // Lowering ally stats
        private const float PENALTY_STATUS_ALLY = -5000f;       // Statusing ally

        // "Helping the Enemy" Penalties
        private const float PENALTY_HEAL_ENEMY = -10000f;       // Healing the player
        private const float PENALTY_BUFF_ENEMY = -5000f;        // Buffing the player

        private static readonly Random _random = new Random();

        public static QueuedAction DetermineBestAction(BattleCombatant actor, List<BattleCombatant> allCombatants)
        {
            var possibleActions = new List<(QueuedAction Action, float Score)>();

            // 1. Identify Teams
            // aiEnemies = The Player's Party
            var aiEnemies = allCombatants.Where(c => c.IsPlayerControlled && !c.IsDefeated && c.IsActiveOnField).ToList();
            // aiAllies = The AI's Party (excluding self)
            var aiAllies = allCombatants.Where(c => !c.IsPlayerControlled && !c.IsDefeated && c.IsActiveOnField && c != actor).ToList();

            // If no enemies, stall
            if (!aiEnemies.Any()) return CreateStallAction(actor);

            // 2. Get Valid Moves
            var moves = actor.AvailableMoves
                .Where(m => actor.Stats.CurrentMana >= m.ManaCost) // Check Mana
                .ToList();

            // Check Silence
            if (actor.HasStatusEffect(StatusEffectType.Silence))
            {
                moves = moves.Where(m => m.MoveType != MoveType.Spell).ToList();
            }

            if (!moves.Any()) return CreateStallAction(actor);

            // 3. Evaluate Each Move
            foreach (var move in moves)
            {
                var validTargets = TargetingHelper.GetValidTargets(actor, move.Target, allCombatants);

                // Handle Multi-Target vs Single-Target Evaluation
                if (IsMultiTargetMove(move.Target))
                {
                    // Evaluate the move as a whole against the entire group of valid targets
                    float score = EvaluateAction(actor, move, validTargets, aiEnemies, aiAllies);
                    possibleActions.Add((CreateAction(actor, move, null), score));
                }
                else
                {
                    // Evaluate against each specific target individually
                    foreach (var target in validTargets)
                    {
                        float score = EvaluateAction(actor, move, new List<BattleCombatant> { target }, aiEnemies, aiAllies);
                        possibleActions.Add((CreateAction(actor, move, target), score));
                    }
                }
            }

            // 4. Select Best Action
            if (possibleActions.Any())
            {
                // Sort descending by score
                possibleActions.Sort((a, b) => b.Score.CompareTo(a.Score));

                var bestAction = possibleActions[0];

                // If the best score is terrible (negative), the AI might just Stall or Defend if possible, 
                // but for now we pick the "least bad" option.

                // Add slight randomness to top choices if scores are close (within 5%)
                // This makes the AI feel less robotic.
                var topActions = possibleActions.Where(a => a.Score >= bestAction.Score * 0.95f).ToList();
                if (topActions.Count > 1)
                {
                    return topActions[_random.Next(topActions.Count)].Action;
                }

                return bestAction.Action;
            }

            return CreateStallAction(actor);
        }

        private static bool IsMultiTargetMove(TargetType type)
        {
            return type == TargetType.All || type == TargetType.Both || type == TargetType.Every ||
                   type == TargetType.Team || type == TargetType.RandomAll ||
                   type == TargetType.RandomBoth || type == TargetType.RandomEvery ||
                   type == TargetType.Self; // Self is effectively single, but doesn't require target selection
        }

        private static float EvaluateAction(BattleCombatant actor, MoveData move, List<BattleCombatant> targets, List<BattleCombatant> aiEnemies, List<BattleCombatant> aiAllies)
        {
            float totalScore = 0f;

            foreach (var target in targets)
            {
                bool isTargetEnemy = aiEnemies.Contains(target);
                bool isTargetAlly = aiAllies.Contains(target) || target == actor; // Treat Self as Ally for scoring

                // --- 1. DAMAGE CALCULATION ---
                if (move.Power > 0)
                {
                    // Simulate damage
                    var dummyAction = new QueuedAction { Actor = actor, ChosenMove = move, Target = target };
                    var result = DamageCalculator.CalculateDamage(dummyAction, target, move);
                    int predictedDamage = result.DamageAmount;

                    float hpPercentDealt = (float)predictedDamage / target.Stats.MaxHP * 100f;

                    if (isTargetEnemy)
                    {
                        // Hitting Enemy: Good
                        if (predictedDamage >= target.Stats.CurrentHP)
                        {
                            totalScore += SCORE_KILL_ENEMY;
                            // Bonus for killing faster targets
                            if (target.GetEffectiveAgility() > actor.GetEffectiveAgility()) totalScore += 500f;
                        }
                        else
                        {
                            totalScore += hpPercentDealt * SCORE_DAMAGE_ENEMY;

                            // Penalty for using high-cost moves on low HP targets (Overkill)
                            if (target.Stats.CurrentHP < target.Stats.MaxHP * 0.1f && move.ManaCost > 20)
                                totalScore += PENALTY_OVERKILL;
                        }

                        // Effectiveness Bonuses
                        if (result.Effectiveness == DamageCalculator.ElementalEffectiveness.Effective) totalScore += 500f;
                        if (result.Effectiveness == DamageCalculator.ElementalEffectiveness.Resisted) totalScore += PENALTY_RESISTED;
                        if (result.Effectiveness == DamageCalculator.ElementalEffectiveness.Immune) totalScore += PENALTY_IMMUNE;
                    }
                    else if (isTargetAlly)
                    {
                        // Hitting Ally: BAD (unless absorbed)
                        if (result.Effectiveness == DamageCalculator.ElementalEffectiveness.Immune)
                        {
                            // No penalty if immune
                        }
                        else
                        {
                            if (predictedDamage >= target.Stats.CurrentHP)
                            {
                                totalScore += PENALTY_KILL_ALLY; // Never kill ally
                            }
                            else
                            {
                                // Heavy penalty per % HP lost
                                totalScore += hpPercentDealt * PENALTY_DAMAGE_ALLY;
                            }
                        }
                    }
                }

                // --- 2. STATUS EFFECTS ---
                if (move.Effects.TryGetValue("ApplyStatus", out var statusVal))
                {
                    if (EffectParser.TryParseStatusEffectParams(statusVal, out var type, out int chance, out int duration))
                    {
                        bool isBuff = type == StatusEffectType.Regen || type == StatusEffectType.Dodging;
                        bool isDebuff = !isBuff;

                        if (isTargetEnemy)
                        {
                            if (isDebuff)
                            {
                                if (target.HasStatusEffect(type)) totalScore += PENALTY_REDUNDANT;
                                else
                                {
                                    float score = (type == StatusEffectType.Stun || type == StatusEffectType.Freeze) ? SCORE_DISABLE_ENEMY : SCORE_STATUS_ENEMY;
                                    totalScore += score * (chance / 100f);
                                }
                            }
                            else // Buffing Enemy
                            {
                                totalScore += PENALTY_BUFF_ENEMY;
                            }
                        }
                        else if (isTargetAlly)
                        {
                            if (isBuff)
                            {
                                if (target.HasStatusEffect(type)) totalScore += PENALTY_REDUNDANT;
                                else totalScore += SCORE_BUFF_ALLY * (chance / 100f);
                            }
                            else // Debuffing Ally
                            {
                                totalScore += PENALTY_STATUS_ALLY;
                            }
                        }
                    }
                }

                // --- 3. STAT MODIFIERS ---
                foreach (var effectKey in move.Effects.Keys)
                {
                    if (effectKey.StartsWith("ModifyStatStage"))
                    {
                        if (EffectParser.TryParseStatStageParams(move.Effects[effectKey], out var stat, out int amount, out int chance, out string targetStr))
                        {
                            // Check if this effect actually targets the current target loop variable
                            // If targetStr is "Self", it only applies if target == actor.
                            // If targetStr is "Target", it applies to the current target.

                            bool appliesToCurrentTarget = (targetStr == "Self" && target == actor) || (targetStr != "Self" && target != actor);

                            if (appliesToCurrentTarget)
                            {
                                bool isBoost = amount > 0;
                                int currentStage = target.StatStages[stat];

                                // Check Caps
                                if ((isBoost && currentStage >= 6) || (!isBoost && currentStage <= -6))
                                {
                                    totalScore += PENALTY_REDUNDANT;
                                    continue;
                                }

                                if (isTargetEnemy)
                                {
                                    if (!isBoost) totalScore += SCORE_DEBUFF_ENEMY * Math.Abs(amount);
                                    else totalScore += PENALTY_BUFF_ENEMY;
                                }
                                else if (isTargetAlly)
                                {
                                    if (isBoost) totalScore += SCORE_BUFF_ALLY * Math.Abs(amount);
                                    else totalScore += PENALTY_DEBUFF_ALLY;
                                }
                            }
                        }
                    }
                }

                // --- 4. HEALING ---
                if (move.Effects.ContainsKey("Heal") || move.Effects.ContainsKey("Lifesteal"))
                {
                    // Lifesteal usually targets enemy but heals self.
                    // Heal usually targets ally/self.

                    if (move.Effects.ContainsKey("Lifesteal"))
                    {
                        // Heals Actor based on damage.
                        // We already calculated damage score. Now add score for self-healing.
                        float hpPercent = (float)actor.Stats.CurrentHP / actor.Stats.MaxHP;
                        if (hpPercent < 0.5f) totalScore += SCORE_HEAL_ALLY * 20f; // Bonus for lifestealing when low
                    }
                    else if (move.Effects.ContainsKey("Heal"))
                    {
                        // Direct Heal
                        if (isTargetAlly)
                        {
                            float hpPercent = (float)target.Stats.CurrentHP / target.Stats.MaxHP;
                            if (hpPercent >= 1.0f) totalScore += PENALTY_REDUNDANT;
                            else
                            {
                                float missingPercent = 1.0f - hpPercent;
                                totalScore += (missingPercent * 100f) * SCORE_HEAL_ALLY;
                            }
                        }
                        else if (isTargetEnemy)
                        {
                            totalScore += PENALTY_HEAL_ENEMY;
                        }
                    }
                }
            }

            return totalScore;
        }

        private static QueuedAction CreateAction(BattleCombatant actor, MoveData move, BattleCombatant target)
        {
            return new QueuedAction
            {
                Actor = actor,
                ChosenMove = move,
                Target = target,
                Priority = move.Priority,
                ActorAgility = actor.GetEffectiveAgility(),
                Type = QueuedActionType.Move
            };
        }

        private static QueuedAction CreateStallAction(BattleCombatant actor)
        {
            return new QueuedAction
            {
                Actor = actor,
                ChosenMove = BattleDataCache.Moves["Stall"],
                Target = actor,
                Priority = 0,
                ActorAgility = actor.GetEffectiveAgility(),
                Type = QueuedActionType.Move
            };
        }
    }
}
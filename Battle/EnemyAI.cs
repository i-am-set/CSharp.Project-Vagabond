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
    /// AI evaluator that determines the optimal move for an enemy combatant.
    /// Evaluates moves based on damage potential, kill thresholds, speed advantages, and team synergy.
    /// </summary>
    public static class EnemyAI
    {
        private static readonly Random _random = new Random();

        // --- Scoring Constants ---
        private const int BASE_SCORE = 6;

        // Kill Threshold Scores
        private const int SCORE_HIGHEST_DAMAGE = 6; // Best damage option, but does not kill
        private const int SCORE_SLOW_KILL = 9;      // Kills, but actor is slower (risk of being hit first)
        private const int SCORE_FAST_KILL = 12;     // Kills, and actor is faster (safe kill)

        // Decision Variance
        // Adds a small chance for the AI to pick slightly sub-optimal moves to feel less robotic.
        private const int RNG_BONUS = 2;

        // Penalties
        private const int PENALTY_IMMUNE = -20;
        private const int PENALTY_RESISTED = -5;
        private const int PENALTY_REDUNDANT = -10; // e.g. Paralyzing a paralyzed target
        private const int PENALTY_HARMFUL_TO_ALLY = -100; // Strict prohibition against hurting teammates

        // Team Synergy
        private const int SCORE_HEAL_ALLY = 8;
        private const int SCORE_BUFF_ALLY = 7;
        private const int SCORE_DEBUFF_ENEMY = 2;

        public static QueuedAction DetermineBestAction(BattleCombatant actor, List<BattleCombatant> allCombatants)
        {
            var possibleActions = new List<(QueuedAction Action, int Score)>();

            // 1. Identify Teams
            var enemies = allCombatants.Where(c => c.IsPlayerControlled && !c.IsDefeated && c.IsActiveOnField).ToList();
            var allies = allCombatants.Where(c => !c.IsPlayerControlled && !c.IsDefeated && c.IsActiveOnField && c != actor).ToList();

            // If no valid targets exist, default to stalling.
            if (!enemies.Any()) return CreateStallAction(actor);

            // 2. Get Valid Moves (Filter out unusable ones like silenced spells)
            var moves = actor.AvailableMoves.Where(m => actor.Stats.CurrentMana >= m.ManaCost).ToList();
            if (actor.HasStatusEffect(StatusEffectType.Silence))
            {
                moves = moves.Where(m => m.MoveType != MoveType.Spell).ToList();
            }

            if (!moves.Any()) return CreateStallAction(actor);

            // 3. Pre-Calculate Highest Damage Options
            // The AI identifies the single highest damage number it can inflict on each target
            // to determine if a specific move is the "optimal" damage choice.
            var bestDamagePerTarget = new Dictionary<string, int>();
            foreach (var enemy in enemies)
            {
                int maxDmg = 0;
                foreach (var move in moves.Where(m => m.Power > 0))
                {
                    var dummyAction = new QueuedAction { Actor = actor, ChosenMove = move, Target = enemy };
                    var result = DamageCalculator.CalculateDamage(dummyAction, enemy, move);
                    if (result.DamageAmount > maxDmg) maxDmg = result.DamageAmount;
                }
                bestDamagePerTarget[enemy.CombatantID] = maxDmg;
            }

            // 4. Evaluate Moves
            foreach (var move in moves)
            {
                var validTargets = TargetingHelper.GetValidTargets(actor, move.Target, allCombatants);

                // If the move targets a specific unit (Single, Ally, etc.)
                if (!IsSpreadMove(move.Target))
                {
                    foreach (var target in validTargets)
                    {
                        int score = EvaluateAction(actor, move, new List<BattleCombatant> { target }, enemies, allies, bestDamagePerTarget);
                        possibleActions.Add((CreateAction(actor, move, target), score));
                    }
                }
                // If the move targets a group (Both, All, Team) - Evaluate the net result
                else
                {
                    // For spread moves, we evaluate the impact on ALL valid targets simultaneously.
                    // This allows the AI to weigh hitting two enemies vs hitting one enemy and one ally.
                    int score = EvaluateAction(actor, move, validTargets, enemies, allies, bestDamagePerTarget);

                    // For spread moves, pass the first valid target as a reference point.
                    possibleActions.Add((CreateAction(actor, move, validTargets.FirstOrDefault()), score));
                }
            }

            // 5. Select Best Action
            if (possibleActions.Any())
            {
                // Shuffle first to break ties randomly, preventing predictable patterns.
                var shuffledActions = possibleActions.OrderBy(x => _random.Next()).ToList();

                // Then order by score descending to pick the best option.
                var bestAction = shuffledActions.OrderByDescending(x => x.Score).First();

                return bestAction.Action;
            }

            return CreateStallAction(actor);
        }

        private static int EvaluateAction(
            BattleCombatant actor,
            MoveData move,
            List<BattleCombatant> targets,
            List<BattleCombatant> enemies,
            List<BattleCombatant> allies,
            Dictionary<string, int> bestDamagePerTarget)
        {
            int totalScore = 0;
            bool isDamagingMove = move.Power > 0;

            // --- Decision Variance ---
            // Applies a small RNG bonus to the base score of the move 20% of the time.
            int rngBonus = (_random.NextDouble() < 0.2) ? RNG_BONUS : 0;

            foreach (var target in targets)
            {
                int targetScore = BASE_SCORE + rngBonus;
                bool isTargetEnemy = enemies.Contains(target);
                bool isTargetAlly = allies.Contains(target) || target == actor;

                // --- 1. DAMAGE EVALUATION ---
                if (isDamagingMove)
                {
                    var dummyAction = new QueuedAction { Actor = actor, ChosenMove = move, Target = target };
                    var result = DamageCalculator.CalculateDamage(dummyAction, target, move);
                    int predictedDamage = result.DamageAmount;

                    if (isTargetAlly)
                    {
                        // STRICT: Never attack allies
                        targetScore += PENALTY_HARMFUL_TO_ALLY;
                    }
                    else if (isTargetEnemy)
                    {
                        // Check Immunities
                        if (result.Effectiveness == DamageCalculator.ElementalEffectiveness.Immune)
                        {
                            targetScore += PENALTY_IMMUNE;
                        }
                        else
                        {
                            bool isKill = predictedDamage >= target.Stats.CurrentHP;
                            bool isFaster = IsFaster(actor, target);

                            // Check if this is the highest damaging move available against this target
                            bool isHighestDamage = predictedDamage >= bestDamagePerTarget.GetValueOrDefault(target.CombatantID, 0);

                            if (isKill)
                            {
                                // Prioritize kills where the actor moves first to prevent retaliation.
                                if (isFaster || move.Priority > 0)
                                    targetScore = SCORE_FAST_KILL + rngBonus;
                                else
                                    targetScore = SCORE_SLOW_KILL + rngBonus;
                            }
                            else if (isHighestDamage)
                            {
                                // If we can't kill, prioritize the move that deals the most damage.
                                targetScore = SCORE_HIGHEST_DAMAGE + rngBonus;
                            }

                            // Penalty for resisted moves if they aren't the best option available.
                            if (result.Effectiveness == DamageCalculator.ElementalEffectiveness.Resisted && !isHighestDamage)
                            {
                                targetScore += PENALTY_RESISTED;
                            }
                        }
                    }
                }

                // --- 2. STAT STAGE MODIFIERS (Buffs/Debuffs) ---
                if (move.Effects.TryGetValue("ModifyStatStage", out var statVal))
                {
                    if (EffectParser.TryParseStatStageParams(statVal, out var stat, out int amount, out int chance, out string targetStr))
                    {
                        bool isDebuff = amount < 0;

                        if (isTargetAlly)
                        {
                            if (isDebuff)
                            {
                                targetScore += PENALTY_HARMFUL_TO_ALLY; // Never debuff ally
                            }
                            else
                            {
                                // Buffing ally is good
                                targetScore += SCORE_BUFF_ALLY;
                            }
                        }
                        else if (isTargetEnemy)
                        {
                            if (isDebuff)
                            {
                                // Debuffing enemy is good
                                // Check redundancy: If stat is already min (-6), it's redundant.
                                if (target.StatStages[stat] <= -6) targetScore += PENALTY_REDUNDANT;
                                else targetScore += SCORE_DEBUFF_ENEMY;
                            }
                            else
                            {
                                // Buffing enemy is bad
                                targetScore += PENALTY_HARMFUL_TO_ALLY; // Treat helping enemy as bad as hurting ally
                            }
                        }
                    }
                }

                // --- 3. STATUS EFFECTS ---
                if (move.Effects.TryGetValue("ApplyStatus", out var statusVal))
                {
                    if (EffectParser.TryParseStatusEffectParams(statusVal, out var type, out int chance, out int duration))
                    {
                        bool isBuff = IsBuff(type);

                        if (isTargetAlly)
                        {
                            if (!isBuff)
                            {
                                targetScore += PENALTY_HARMFUL_TO_ALLY; // Never apply negative status to ally
                            }
                            else
                            {
                                // Incentivize buffing allies.
                                if (target.HasStatusEffect(type)) targetScore += PENALTY_REDUNDANT;
                                else targetScore += SCORE_BUFF_ALLY;
                            }
                        }
                        else if (isTargetEnemy)
                        {
                            if (isBuff)
                            {
                                targetScore += PENALTY_HARMFUL_TO_ALLY; // Never buff enemy
                            }
                            else
                            {
                                // Don't try to apply a status the target already has.
                                if (target.HasStatusEffect(type)) targetScore += PENALTY_REDUNDANT;
                                else targetScore += SCORE_DEBUFF_ENEMY;
                            }
                        }
                    }
                }

                // --- 4. HEALING LOGIC ---
                if (move.Effects.ContainsKey("Heal"))
                {
                    if (isTargetAlly)
                    {
                        float hpPercent = (float)target.Stats.CurrentHP / target.Stats.MaxHP;
                        if (hpPercent > 0.85f) targetScore += PENALTY_REDUNDANT; // Don't heal if healthy
                        else if (hpPercent < 0.5f) targetScore += SCORE_HEAL_ALLY; // Prioritize low HP
                    }
                    else if (isTargetEnemy)
                    {
                        targetScore += PENALTY_HARMFUL_TO_ALLY; // Don't heal enemies (Treat as harmful to self)
                    }
                }

                // --- 5. SPECIFIC MOVE LOGIC ---
                // Apply custom logic for complex moves that don't fit standard categories.
                targetScore += GetMoveSpecificModifier(move, actor, target, enemies, allies);

                // Add this target's score to the total for the move
                totalScore += targetScore;
            }

            return totalScore;
        }

        /// <summary>
        /// Contains specific logic for moves that don't fit the general damage/status rules.
        /// Add cases here as you implement specific moves to fine-tune AI behavior.
        /// </summary>
        private static int GetMoveSpecificModifier(MoveData move, BattleCombatant actor, BattleCombatant target, List<BattleCombatant> enemies, List<BattleCombatant> allies)
        {
            int modifier = 0;

            switch (move.MoveID)
            {
                case "Counter Spell":
                    // Logic: If used last turn, apply penalty to prevent spamming predictable priority.
                    break;

                case "Taze":
                    // Logic: Only useful on the first turn the actor is active.
                    if (!actor.HasUsedFirstAttack) modifier += 3; // Boost score
                    else modifier += -20; // Useless after first turn
                    break;

                case "Explode":
                    // Logic: Score increases as HP decreases.
                    float hpPercent = (float)actor.Stats.CurrentHP / actor.Stats.MaxHP;
                    if (hpPercent < 0.1f) modifier += 4; // Desperation move
                    else if (hpPercent < 0.33f) modifier += 2;

                    // Check if it kills the last ally (bad idea to leave field empty)
                    if (allies.Count == 0) modifier += -1;
                    break;

                // Example of Support Logic
                case "HypeUp:P":
                    if (target == actor) modifier += -20; // Don't coach self
                    else if (allies.Contains(target))
                    {
                        // Prefer coaching physical attackers
                        if (target.Stats.Strength > target.Stats.Intelligence) modifier += 3;
                    }
                    break;
            }

            return modifier;
        }

        private static bool IsFaster(BattleCombatant a, BattleCombatant b)
        {
            // AI assumes it wins speed ties for calculation purposes.
            return a.GetEffectiveAgility() >= b.GetEffectiveAgility();
        }

        private static bool IsSpreadMove(TargetType type)
        {
            return type == TargetType.All || type == TargetType.Both || type == TargetType.Every ||
                   type == TargetType.RandomAll || type == TargetType.RandomBoth || type == TargetType.RandomEvery;
        }

        private static bool IsBuff(StatusEffectType type)
        {
            return type == StatusEffectType.Regen || type == StatusEffectType.Dodging;
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
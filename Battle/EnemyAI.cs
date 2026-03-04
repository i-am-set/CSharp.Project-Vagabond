using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Battle.UI;
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
    public static class EnemyAI
    {
        private static readonly Random _random = new Random();

        private const int BASE_SCORE = 6;
        private const int SCORE_HIGHEST_DAMAGE = 6;
        private const int SCORE_SLOW_KILL = 9;
        private const int SCORE_FAST_KILL = 12;
        private const int RNG_BONUS = 2;
        private const int PENALTY_REDUNDANT = -10;
        private const int PENALTY_HARMFUL_TO_ALLY = -100;
        private const int SCORE_HEAL_ALLY = 8;
        private const int SCORE_BUFF_ALLY = 7;
        private const int SCORE_DEBUFF_ENEMY = 2;

        public static QueuedAction DetermineBestAction(BattleCombatant actor, List<BattleCombatant> allCombatants)
        {
            var possibleActions = new List<(QueuedAction Action, int Score)>();

            var enemies = allCombatants.Where(c => c.IsPlayerControlled && !c.IsDefeated && c.IsActiveOnField).ToList();
            var allies = allCombatants.Where(c => !c.IsPlayerControlled && !c.IsDefeated && c.IsActiveOnField && c != actor).ToList();

            if (!enemies.Any()) return CreateStallAction(actor);

            var moves = actor.AvailableMoves.ToList();

            if (actor.HasStatusEffect(StatusEffectType.Silence))
            {
                moves = moves.Where(m => m.MoveType != MoveType.Spell).ToList();
            }
            if (actor.HasStatusEffect(StatusEffectType.Provoked))
            {
                moves = moves.Where(m => m.ImpactType != ImpactType.Status).ToList();
            }

            if (!moves.Any()) return CreateStallAction(actor);

            var context = new BattleContext();

            var bestDamagePerTarget = new Dictionary<string, int>();
            foreach (var enemy in enemies)
            {
                int maxDmg = 0;
                foreach (var move in moves.Where(m => m.Power > 0))
                {
                    var dummyAction = new QueuedAction { Actor = actor, ChosenMove = move, Target = enemy };

                    context.ResetMultipliers();
                    context.Actor = actor;
                    context.Target = enemy;
                    context.Move = move;
                    context.Action = dummyAction;
                    context.IsSimulation = true;

                    var result = DamageCalculator.CalculateDamage(dummyAction, enemy, move, 1.0f, null, true, context);
                    if (result.DamageAmount > maxDmg) maxDmg = result.DamageAmount;
                }
                bestDamagePerTarget[enemy.CombatantID] = maxDmg;
            }

            foreach (var move in moves)
            {
                var validTargets = TargetingHelper.GetValidTargets(actor, move.Target, allCombatants);

                if (!IsSpreadMove(move.Target))
                {
                    foreach (var target in validTargets)
                    {
                        int score = EvaluateAction(actor, move, new List<BattleCombatant> { target }, enemies, allies, bestDamagePerTarget, context);
                        possibleActions.Add((CreateAction(actor, move, target), score));
                    }
                }
                else
                {
                    int score = EvaluateAction(actor, move, validTargets, enemies, allies, bestDamagePerTarget, context);
                    possibleActions.Add((CreateAction(actor, move, validTargets.FirstOrDefault()), score));
                }
            }

            if (possibleActions.Any())
            {
                var shuffledActions = possibleActions.OrderBy(x => _random.Next()).ToList();
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
            Dictionary<string, int> bestDamagePerTarget,
            BattleContext context)
        {
            int totalScore = 0;
            bool isDamagingMove = move.Power > 0;
            int rngBonus = (_random.NextDouble() < 0.2) ? RNG_BONUS : 0;

            foreach (var target in targets)
            {
                int targetScore = BASE_SCORE + rngBonus;
                bool isTargetEnemy = enemies.Contains(target);
                bool isTargetAlly = allies.Contains(target) || target == actor;

                if (isDamagingMove)
                {
                    var dummyAction = new QueuedAction { Actor = actor, ChosenMove = move, Target = target };

                    context.ResetMultipliers();
                    context.Actor = actor;
                    context.Target = target;
                    context.Move = move;
                    context.Action = dummyAction;
                    context.IsSimulation = true;

                    var result = DamageCalculator.CalculateDamage(dummyAction, target, move, 1.0f, null, true, context);
                    int predictedDamage = result.DamageAmount;

                    if (isTargetAlly)
                    {
                        targetScore += PENALTY_HARMFUL_TO_ALLY;
                    }
                    else if (isTargetEnemy)
                    {
                        bool isKill = predictedDamage >= target.Stats.CurrentHP;
                        bool isFaster = IsFaster(actor, target);
                        bool isHighestDamage = predictedDamage >= bestDamagePerTarget.GetValueOrDefault(target.CombatantID, 0);

                        if (isKill)
                        {
                            if (isFaster || move.Priority > 0)
                                targetScore = SCORE_FAST_KILL + rngBonus;
                            else
                                targetScore = SCORE_SLOW_KILL + rngBonus;
                        }
                        else if (isHighestDamage)
                        {
                            targetScore = SCORE_HIGHEST_DAMAGE + rngBonus;
                        }
                    }
                }

                if (move.Effects.TryGetValue("ModifyStatStage", out var statVal))
                {
                    if (EffectParser.TryParseStatStageParams(statVal, out var stat, out int amount, out int chance, out string targetStr))
                    {
                        bool isDebuff = amount < 0;

                        if (isTargetAlly)
                        {
                            if (isDebuff) targetScore += PENALTY_HARMFUL_TO_ALLY;
                            else targetScore += SCORE_BUFF_ALLY;
                        }
                        else if (isTargetEnemy)
                        {
                            if (isDebuff)
                            {
                                if (target.StatStages[stat] <= -2) targetScore += PENALTY_REDUNDANT;
                                else targetScore += SCORE_DEBUFF_ENEMY;
                            }
                            else targetScore += PENALTY_HARMFUL_TO_ALLY;
                        }
                    }
                }

                if (move.Effects.TryGetValue("InflictStatChange", out var inflictVal))
                {
                    var parts = inflictVal.Split(',');
                    for (int i = 0; i < parts.Length; i += 2)
                    {
                        if (i + 1 >= parts.Length) break;
                        if (int.TryParse(parts[i + 1], out int amount))
                        {
                            bool isDebuff = amount < 0;
                            if (isTargetAlly)
                            {
                                if (isDebuff) targetScore += PENALTY_HARMFUL_TO_ALLY;
                                else targetScore += SCORE_BUFF_ALLY;
                            }
                            else if (isTargetEnemy)
                            {
                                if (isDebuff) targetScore += SCORE_DEBUFF_ENEMY;
                                else targetScore += PENALTY_HARMFUL_TO_ALLY;
                            }
                        }
                    }
                }

                if (move.Effects.TryGetValue("ApplyStatus", out var statusVal))
                {
                    if (EffectParser.TryParseStatusEffectParams(statusVal, out var type, out int chance, out int duration))
                    {
                        EvaluateStatusEffect(type, target, isTargetAlly, isTargetEnemy, ref targetScore);
                    }
                }

                CheckInflictStatusKey(move, "InflictStatusPoison", StatusEffectType.Poison, target, isTargetAlly, isTargetEnemy, ref targetScore);
                CheckInflictStatusKey(move, "InflictStatusBurn", StatusEffectType.Burn, target, isTargetAlly, isTargetEnemy, ref targetScore);
                CheckInflictStatusKey(move, "InflictStatusFrostbite", StatusEffectType.Frostbite, target, isTargetAlly, isTargetEnemy, ref targetScore);
                CheckInflictStatusKey(move, "InflictStatusStun", StatusEffectType.Stun, target, isTargetAlly, isTargetEnemy, ref targetScore);
                CheckInflictStatusKey(move, "InflictStatusSilence", StatusEffectType.Silence, target, isTargetAlly, isTargetEnemy, ref targetScore);

                if (move.Effects.ContainsKey("Heal"))
                {
                    if (isTargetAlly)
                    {
                        float hpPercent = (float)target.Stats.CurrentHP / target.Stats.MaxHP;
                        if (hpPercent > 0.85f) targetScore += PENALTY_REDUNDANT;
                        else if (hpPercent < 0.5f) targetScore += SCORE_HEAL_ALLY;
                    }
                    else if (isTargetEnemy)
                    {
                        targetScore += PENALTY_HARMFUL_TO_ALLY;
                    }
                }

                targetScore += GetMoveSpecificModifier(move, actor, target, enemies, allies);
                totalScore += targetScore;
            }

            return totalScore;
        }

        private static void CheckInflictStatusKey(MoveData move, string key, StatusEffectType type, BattleCombatant target, bool isTargetAlly, bool isTargetEnemy, ref int targetScore)
        {
            if (move.Effects.ContainsKey(key))
            {
                EvaluateStatusEffect(type, target, isTargetAlly, isTargetEnemy, ref targetScore);
            }
        }

        private static void EvaluateStatusEffect(StatusEffectType type, BattleCombatant target, bool isTargetAlly, bool isTargetEnemy, ref int targetScore)
        {
            bool isBuff = IsBuff(type);

            if (isTargetAlly)
            {
                if (!isBuff) targetScore += PENALTY_HARMFUL_TO_ALLY;
                else
                {
                    if (target.HasStatusEffect(type)) targetScore += PENALTY_REDUNDANT;
                    else targetScore += SCORE_BUFF_ALLY;
                }
            }
            else if (isTargetEnemy)
            {
                if (isBuff) targetScore += PENALTY_HARMFUL_TO_ALLY;
                else
                {
                    if (target.HasStatusEffect(type)) targetScore += PENALTY_REDUNDANT;
                    else targetScore += SCORE_DEBUFF_ENEMY;
                }
            }
        }

        private static int GetMoveSpecificModifier(MoveData move, BattleCombatant actor, BattleCombatant target, List<BattleCombatant> enemies, List<BattleCombatant> allies)
        {
            int modifier = 0;

            return modifier;
        }

        private static bool IsFaster(BattleCombatant a, BattleCombatant b)
        {
            return a.GetEffectiveAgility() >= b.GetEffectiveAgility();
        }

        private static bool IsSpreadMove(TargetType type)
        {
            return type == TargetType.All || type == TargetType.Both || type == TargetType.Every ||
                   type == TargetType.RandomAll || type == TargetType.RandomBoth || type == TargetType.RandomEvery;
        }

        private static bool IsBuff(StatusEffectType type)
        {
            return type == StatusEffectType.Regen || type == StatusEffectType.Dodging || type == StatusEffectType.Protected || type == StatusEffectType.Empowered;
        }

        private static QueuedAction CreateAction(BattleCombatant actor, MoveData move, BattleCombatant target)
        {
            MoveEntry? entry = null;
            if (actor.Spell1 != null && actor.Spell1.MoveID == move.MoveID) entry = actor.Spell1;
            else if (actor.Spell2 != null && actor.Spell2.MoveID == move.MoveID) entry = actor.Spell2;
            else if (actor.Spell3 != null && actor.Spell3.MoveID == move.MoveID) entry = actor.Spell3;
            else if (actor.BasicMove != null && actor.BasicMove.MoveID == move.MoveID) entry = actor.BasicMove;

            return new QueuedAction
            {
                Actor = actor,
                ChosenMove = move,
                SpellbookEntry = entry,
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
                ChosenMove = BattleDataCache.Moves["6"],
                Target = actor,
                Priority = 0,
                ActorAgility = actor.GetEffectiveAgility(),
                Type = QueuedActionType.Move
            };
        }
    }
}
using Microsoft.Xna.Framework;
using ProjectVagabond.Scenes;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Battle
{
    public class WizardAIController
    {
        public float MinReactionTime { get; set; } = 0.2f;
        public float MaxReactionTime { get; set; } = 0.6f;

        public float ReactionChance { get; set; } = 0.65f;

        public float OpportunityCheckInterval { get; set; } = 0.5f;

        private float _reactionTimer = 0f;
        private Action _plannedAction = null;
        private float _opportunityTimer = 0f;

        private readonly HashSet<object> _knownThreats = new HashSet<object>();
        private static readonly Random _random = new Random();

        public void Update(float dt, ArenaScene arena, ArenaWizard self)
        {
            if (self.Data.Combat.State == WizardState.Dead || self.Data.Combat.IsSuspended) return;

            if (_plannedAction != null)
            {
                _reactionTimer -= dt;
                if (_reactionTimer <= 0)
                {
                    _plannedAction.Invoke();
                    _plannedAction = null;
                }
            }

            _knownThreats.RemoveWhere(t =>
            {
                if (t is ArenaWizard w) return w.Data.Combat.State != WizardState.Telegraphing && w.Data.Combat.State != WizardState.Casting;
                if (t is ActiveAttack a) return a.IsFinished || a.IsCanceled;
                return true;
            });

            if (self.Data.Combat.EquippedActiveSpell != null && self.Data.Combat.ActiveSpellCooldownTimer <= 0 && _plannedAction == null)
            {
                string spellId = self.Data.Combat.EquippedActiveSpell.ID;
                if (spellId == "ward" || spellId == "teleport")
                {
                    ScanForThreats(arena, self);
                }
            }

            _opportunityTimer -= dt;
            if (_opportunityTimer <= 0)
            {
                _opportunityTimer = OpportunityCheckInterval;
                if (self.Data.Combat.EquippedActiveSpell != null && self.Data.Combat.ActiveSpellCooldownTimer <= 0 && _plannedAction == null)
                {
                    EvaluateOpportunities(arena, self);
                }
            }
        }

        private void ScanForThreats(ArenaScene arena, ArenaWizard self)
        {
            foreach (var enemy in arena.Wizards)
            {
                if (enemy == self || enemy.Data.Combat.State != WizardState.Telegraphing || _knownThreats.Contains(enemy)) continue;

                bool isThreat = false;

                if (enemy.Data.Combat.QueuedMove.Delivery is InstantAOEDelivery aoe)
                {
                    if (Vector2.Distance(self.Data.Combat.Position, enemy.Data.Combat.QueuedTargetPos) <= aoe.Radius + 5f) isThreat = true;
                }
                else if (enemy.Data.Combat.QueuedMove.Delivery is TickingBeamDelivery beam)
                {
                    if (CollisionMath.PointInOBB(self.Data.Combat.Position, enemy.Data.Combat.Position, enemy.Data.Combat.QueuedDirection, beam.Width + 10f, beam.Length)) isThreat = true;
                }
                else if (enemy.Data.Combat.QueuedMove.Delivery is DashMeleeDelivery dash)
                {
                    if (CollisionMath.PointInOBB(self.Data.Combat.Position, enemy.Data.Combat.Position, enemy.Data.Combat.QueuedDirection, dash.Width + 10f, dash.DashDistance)) isThreat = true;
                }
                else if (enemy.Data.Combat.QueuedTargetWizard == self)
                {
                    isThreat = true;
                }

                if (isThreat)
                {
                    _knownThreats.Add(enemy);
                    if (_random.NextDouble() <= ReactionChance)
                    {
                        PlanDefensiveAction(self, arena);
                        return;
                    }
                }
            }

            foreach (var attack in arena.ActiveAttacks)
            {
                if (attack.Caster == self || _knownThreats.Contains(attack)) continue;

                bool isThreat = false;
                if (attack.TargetWizard == self) isThreat = true;
                else if (attack.DeliveryInstance is InstantAOEDelivery aoe && Vector2.Distance(self.Data.Combat.Position, attack.TargetPosition) <= aoe.Radius + 5f) isThreat = true;

                if (isThreat)
                {
                    _knownThreats.Add(attack);
                    if (_random.NextDouble() <= ReactionChance)
                    {
                        PlanDefensiveAction(self, arena);
                        return;
                    }
                }
            }
        }

        private void PlanDefensiveAction(ArenaWizard self, ArenaScene arena)
        {
            _reactionTimer = MinReactionTime + (float)_random.NextDouble() * (MaxReactionTime - MinReactionTime);
            _plannedAction = () =>
            {
                if (self.Data.Combat.ActiveSpellCooldownTimer <= 0 && self.Data.Combat.State != WizardState.Dead && !self.Data.Combat.IsSuspended)
                {
                    self.Controller.TriggerActiveSpell(arena);
                }
            };
        }

        private void EvaluateOpportunities(ArenaScene arena, ArenaWizard self)
        {
            string spellId = self.Data.Combat.EquippedActiveSpell.ID;

            if (spellId == "force_cast" && self.Data.Combat.State == WizardState.Moving)
            {
                if (arena.Wizards.Any(w => w != self && w.Data.Stats.CurrentHP > 0))
                {
                    if (_random.NextDouble() < 0.3f)
                    {
                        _reactionTimer = MinReactionTime;
                        _plannedAction = () => self.Controller.TriggerActiveSpell(arena);
                    }
                }
            }
            else if (spellId == "teleport")
            {
                int closeEnemies = arena.Wizards.Count(w => w != self && w.Data.Stats.CurrentHP > 0 && Vector2.Distance(self.Data.Combat.Position, w.Data.Combat.Position) < 40f);
                if (closeEnemies >= 3)
                {
                    _reactionTimer = MinReactionTime;
                    _plannedAction = () => self.Controller.TriggerActiveSpell(arena);
                }
            }
        }
    }
}
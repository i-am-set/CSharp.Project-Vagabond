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
        // --- TUNING PARAMETERS ---

        // How long it takes the AI to physically react after noticing a threat (in seconds)
        public float MinReactionTime { get; set; } = 0.2f;
        public float MaxReactionTime { get; set; } = 0.6f;

        // The probability (0.0 to 1.0) that the AI will successfully react to an incoming attack.
        // 0.65 means a 65% chance to react, and a 35% chance to completely ignore it (miscalculation).
        public float ReactionChance { get; set; } = 0.65f;

        // How often the AI checks for non-threat opportunities (like using Force Cast)
        public float OpportunityCheckInterval { get; set; } = 0.5f;

        // --- STATE ---
        private float _reactionTimer = 0f;
        private Action _plannedAction = null;
        private float _opportunityTimer = 0f;

        // Track threats we've already evaluated so we don't roll awareness every frame
        private readonly HashSet<object> _knownThreats = new HashSet<object>();
        private static readonly Random _random = new Random();

        public void Update(float dt, ArenaScene arena, ArenaWizard self)
        {
            if (self.State == WizardState.Dead || self.IsSuspended) return;

            // 1. Execute planned actions if reaction time has passed
            if (_plannedAction != null)
            {
                _reactionTimer -= dt;
                if (_reactionTimer <= 0)
                {
                    _plannedAction.Invoke();
                    _plannedAction = null;
                }
            }

            // 2. Clean up stale threats
            _knownThreats.RemoveWhere(t =>
            {
                if (t is ArenaWizard w) return w.State != WizardState.Telegraphing && w.State != WizardState.Casting;
                if (t is ActiveAttack a) return a.IsFinished || a.IsCanceled;
                return true;
            });

            // 3. Scan for incoming threats (Defensive Spells)
            if (self.EquippedActiveSpell != null && self.ActiveSpellCooldownTimer <= 0 && _plannedAction == null)
            {
                string spellId = self.EquippedActiveSpell.ID;
                if (spellId == "ward" || spellId == "teleport")
                {
                    ScanForThreats(arena, self);
                }
            }

            // 4. Scan for opportunities (Offensive/Utility Spells)
            _opportunityTimer -= dt;
            if (_opportunityTimer <= 0)
            {
                _opportunityTimer = OpportunityCheckInterval;
                if (self.EquippedActiveSpell != null && self.ActiveSpellCooldownTimer <= 0 && _plannedAction == null)
                {
                    EvaluateOpportunities(arena, self);
                }
            }
        }

        private void ScanForThreats(ArenaScene arena, ArenaWizard self)
        {
            // Check telegraphing wizards
            foreach (var enemy in arena.Wizards)
            {
                if (enemy == self || enemy.State != WizardState.Telegraphing || _knownThreats.Contains(enemy)) continue;

                bool isThreat = false;

                if (enemy.QueuedMove.Delivery is InstantAOEDelivery aoe)
                {
                    if (Vector2.Distance(self.Position, enemy.QueuedTargetPos) <= aoe.Radius + 5f) isThreat = true;
                }
                else if (enemy.QueuedMove.Delivery is TickingBeamDelivery beam)
                {
                    if (CollisionMath.PointInOBB(self.Position, enemy.Position, enemy.QueuedDirection, beam.Width + 10f, beam.Length)) isThreat = true;
                }
                else if (enemy.QueuedMove.Delivery is DashMeleeDelivery dash)
                {
                    if (CollisionMath.PointInOBB(self.Position, enemy.Position, enemy.QueuedDirection, dash.Width + 10f, dash.DashDistance)) isThreat = true;
                }
                else if (enemy.QueuedTargetWizard == self)
                {
                    isThreat = true;
                }

                if (isThreat)
                {
                    _knownThreats.Add(enemy);
                    if (_random.NextDouble() <= ReactionChance)
                    {
                        PlanDefensiveAction(self, arena);
                        return; // Only react to one thing at a time
                    }
                }
            }

            // Check active projectiles
            foreach (var attack in arena.ActiveAttacks)
            {
                if (attack.Caster == self || _knownThreats.Contains(attack)) continue;

                bool isThreat = false;
                if (attack.TargetWizard == self) isThreat = true;
                else if (attack.DeliveryInstance is InstantAOEDelivery aoe && Vector2.Distance(self.Position, attack.TargetPosition) <= aoe.Radius + 5f) isThreat = true;

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
                if (self.ActiveSpellCooldownTimer <= 0 && self.State != WizardState.Dead && !self.IsSuspended)
                {
                    self.TriggerActiveSpell(arena);
                }
            };
        }

        private void EvaluateOpportunities(ArenaScene arena, ArenaWizard self)
        {
            string spellId = self.EquippedActiveSpell.ID;

            if (spellId == "force_cast" && self.State == WizardState.Moving)
            {
                // If moving and enemies are alive, randomly decide to burst
                if (arena.Wizards.Any(w => w != self && w.CurrentHP > 0))
                {
                    if (_random.NextDouble() < 0.3f) // 30% chance per check interval
                    {
                        _reactionTimer = MinReactionTime;
                        _plannedAction = () => self.TriggerActiveSpell(arena);
                    }
                }
            }
            else if (spellId == "teleport")
            {
                // If surrounded by 3 or more enemies, teleport away
                int closeEnemies = arena.Wizards.Count(w => w != self && w.CurrentHP > 0 && Vector2.Distance(self.Position, w.Position) < 40f);
                if (closeEnemies >= 3)
                {
                    _reactionTimer = MinReactionTime;
                    _plannedAction = () => self.TriggerActiveSpell(arena);
                }
            }
        }
    }
}
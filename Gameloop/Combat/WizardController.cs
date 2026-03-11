using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.Animations;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Animations;
using ProjectVagabond.Battle;
using ProjectVagabond.Deliveries;
using ProjectVagabond.Particles;
using ProjectVagabond.Scenes;
using ProjectVagabond.Transitions;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;

namespace ProjectVagabond.Battle
{
    public class WizardController
    {
        private readonly ArenaWizard _wizard;
        private static readonly Random _random = new Random();

        // Cached Dependencies
        private readonly Global _global;
        private readonly SpriteManager _spriteManager;
        private readonly Core _core;
        private readonly ParticleSystemManager _particleSystemManager;
        private readonly TextureFactory _textureFactory;
        private readonly Texture2D _pixel;

        public WizardController(ArenaWizard wizard)
        {
            _wizard = wizard;
            _global = ServiceLocator.Get<Global>();
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _core = ServiceLocator.Get<Core>();
            _particleSystemManager = ServiceLocator.Get<ParticleSystemManager>();
            _textureFactory = ServiceLocator.Get<TextureFactory>();
            _pixel = ServiceLocator.Get<Texture2D>();
        }

        public void Initialize(WizardCatData data, Vector2 startPos, bool isPlayer)
        {
            var stats = _wizard.Data.Stats;
            var combat = _wizard.Data.Combat;
            var ui = _wizard.Data.UI;

            stats.Name = data.Name;
            combat.Position = startPos;
            combat.TargetPosition = startPos;
            combat.PreviousPosition = startPos;
            stats.IsPlayer = isPlayer;
            stats.PortraitIndex = int.TryParse(data.MemberID, out int pid) ? pid : 0;
            ui.HopTimer = (float)(_random.NextDouble() * MathHelper.TwoPi);
            combat.IsFacingRight = false;

            ui.FloatingHeartWaveInterval = 1f + (float)_random.NextDouble() * 4f;
            ui.FloatingHeartWaveTimer = 0f;
            ui.HudHeartWaveInterval = 1f + (float)_random.NextDouble() * 4f;
            ui.HudHeartWaveTimer = 0f;

            stats.HP = data.HP;
            stats.Power = data.Power;
            stats.Tenacity = data.Tenacity;
            stats.Agility = data.Agility;

            stats.MaxHP = stats.HP * 3;
            stats.Rating = (stats.Power + stats.Tenacity + stats.Agility) * stats.MaxHP;

            int maxHearts = (stats.MaxHP + 2) / 3;
            ui.HeartFlashTimers = new float[maxHearts];
            ui.HeartFlashFrame = new int[maxHearts];

            stats.CurrentHP = stats.MaxHP;
            stats.Speed = stats.Agility * 2.0f + 2.5f;

            ui.HealthBarAlpha = 0f;
            combat.ActionTimer = GetRandomActionTime();

            if (!string.IsNullOrEmpty(data.ActiveSpell) && GameDataCache.ActiveSpells.TryGetValue(data.ActiveSpell, out var spellData))
            {
                combat.EquippedActiveSpell = spellData;
            }

            if (!isPlayer)
            {
                _wizard.AIController = new WizardAIController();
            }

            LoadMoves(data);
        }

        private void LoadMoves(WizardCatData data)
        {
            var combat = _wizard.Data.Combat;
            combat.Moves.Clear();
            string[] slots = { data.Move1, data.Move2, data.Move3, data.Move4 };

            foreach (var slot in slots)
            {
                if (!string.IsNullOrWhiteSpace(slot) && GameDataCache.Moves.TryGetValue(slot, out var moveData))
                {
                    combat.Moves.Add(MoveFactory.CreateMove(moveData));
                }
            }
        }

        public Rectangle GetHitbox(SpriteManager spriteManager)
        {
            var combat = _wizard.Data.Combat;
            var stats = _wizard.Data.Stats;
            var ui = _wizard.Data.UI;

            if (combat.IsSuspended) return Rectangle.Empty;

            var bounds = spriteManager.GetPlayerSpriteBounds(stats.PortraitIndex, PlayerSpriteType.Portrait5x5);
            float hopOffset = combat.State == WizardState.Dead ? 0f : -MathF.Abs(MathF.Sin(ui.HopTimer)) * 4f;

            if (combat.IsFacingRight)
            {
                bounds = new Rectangle(-(bounds.X + bounds.Width), bounds.Y, bounds.Width, bounds.Height);
            }

            if (combat.State == WizardState.Dead)
            {
                int newX = -(bounds.Y + bounds.Height);
                int newY = bounds.X;
                bounds = new Rectangle(newX, newY, bounds.Height, bounds.Width);
            }

            return new Rectangle(
                (int)MathF.Round(combat.Position.X) + bounds.X,
                (int)MathF.Round(combat.Position.Y + hopOffset) + bounds.Y,
                bounds.Width,
                bounds.Height
            );
        }

        public bool TakeDamage(int amount, bool isCrit = false)
        {
            var combat = _wizard.Data.Combat;
            var stats = _wizard.Data.Stats;
            var ui = _wizard.Data.UI;

            if (combat.WardTimer > 0)
            {
                combat.WardHitTimer = 0.4f;
                return false;
            }

            if (combat.InvincibilityTimer > 0 || combat.State == WizardState.Dead || stats.CurrentHP <= 0) return false;

            if (combat.IsSuspended)
            {
                combat.SuspendedActions.Enqueue(() => TakeDamage(amount, isCrit));
                return false;
            }

            int oldHP = stats.CurrentHP;
            stats.CurrentHP = Math.Clamp(stats.CurrentHP - amount, 0, stats.MaxHP);
            int actualDamage = oldHP - stats.CurrentHP;

            if (actualDamage > 0)
            {
                TriggerHeartFlash(oldHP, stats.CurrentHP);
                combat.InvincibilityTimer = combat.InvincibilityDuration;
                ui.HudShakeTimer = 0.4f;

                ui.HealthBarVisibilityTimer = ui.HealthBarLingerDuration;
                ui.HealthBarAlpha = 1.0f;

                var hitbox = GetHitbox(_spriteManager);
                Vector2 centerOffset = new Vector2(hitbox.Center.X - combat.Position.X, hitbox.Center.Y - combat.Position.Y);

                var ft = Pools.FloatingText.Get();
                ft.Reset();
                ft.Number = actualDamage;
                ft.IsHealing = false;
                ft.IsCrit = isCrit;
                ft.Duration = 1.0f;
                ft.Timer = 1.0f;
                ft.LocalOffset = centerOffset + new Vector2(_random.Next(-8, 9), 0);
                ui.FloatingTexts.Add(ft);

                return true;
            }
            return false;
        }

        public void Heal(int amount)
        {
            var combat = _wizard.Data.Combat;
            var stats = _wizard.Data.Stats;
            var ui = _wizard.Data.UI;

            if (combat.State == WizardState.Dead) return;

            if (combat.IsSuspended)
            {
                combat.SuspendedActions.Enqueue(() => Heal(amount));
                return;
            }

            int oldHP = stats.CurrentHP;
            stats.CurrentHP = Math.Clamp(stats.CurrentHP + amount, 0, stats.MaxHP);
            int actualHeal = stats.CurrentHP - oldHP;

            if (actualHeal > 0)
            {
                var hitbox = GetHitbox(_spriteManager);
                Vector2 centerOffset = new Vector2(hitbox.Center.X - combat.Position.X, hitbox.Center.Y - combat.Position.Y);

                var ft = Pools.FloatingText.Get();
                ft.Reset();
                ft.Number = actualHeal;
                ft.IsHealing = true;
                ft.IsCrit = false;
                ft.Duration = 1.0f;
                ft.Timer = 1.0f;
                ft.LocalOffset = centerOffset + new Vector2(_random.Next(-8, 9), 0);
                ui.FloatingTexts.Add(ft);
            }
        }

        public void ApplyKnockback(Vector2 sourcePosition, float distance, ArenaScene arena)
        {
            var combat = _wizard.Data.Combat;

            if (combat.State == WizardState.Dead || combat.WardTimer > 0) return;

            if (combat.IsSuspended)
            {
                combat.SuspendedActions.Enqueue(() => ApplyKnockback(sourcePosition, distance, arena));
                return;
            }

            if ((combat.State == WizardState.Casting || combat.State == WizardState.Telegraphing) && combat.QueuedMove != null)
            {
                if (combat.QueuedMove.RequiresFocus)
                {
                    if (combat.CurrentActiveAttack != null && !combat.CurrentActiveAttack.IsPooled)
                    {
                        combat.CurrentActiveAttack.IsCanceled = true;
                    }
                    combat.CurrentActiveAttack = null;
                    combat.State = WizardState.Recovering;
                    combat.StateTimer = 0.5f;
                    combat.TargetPosition = combat.Position;
                }
            }

            Vector2 dir = combat.Position - sourcePosition;
            if (dir.LengthSquared() > 0)
                dir.Normalize();
            else
                dir = new Vector2(1, 0);

            combat.KnockbackStartPos = combat.Position;
            Vector2 desiredTarget = combat.Position + dir * distance;

            combat.KnockbackTargetPos = arena.ClampToArena(desiredTarget, 12f);

            combat.KnockbackDuration = 0.5f + (distance / 80f);
            combat.KnockbackTimer = combat.KnockbackDuration;
        }

        public bool TriggerActiveSpell(BattleContext context)
        {
            var combat = _wizard.Data.Combat;

            if (combat.EquippedActiveSpell == null || combat.ActiveSpellCooldownTimer > 0 || combat.State == WizardState.Dead || combat.IsSuspended) return false;

            if (combat.EquippedActiveSpell.ID == "force_cast" && combat.State != WizardState.Moving) return false;

            combat.ActiveSpellCooldownTimer = combat.EquippedActiveSpell.Cooldown;

            if (combat.EquippedActiveSpell.ID == "ward")
            {
                combat.WardTimer = combat.EquippedActiveSpell.Duration;
            }
            else if (combat.EquippedActiveSpell.ID == "force_cast")
            {
                combat.ActionTimer = 0f;
                PrepareAttack(context);
            }
            else if (combat.EquippedActiveSpell.ID == "teleport")
            {
                combat.IsTeleporting = true;
                combat.TeleportTimer = combat.EquippedActiveSpell.Duration;
                combat.KnockbackTimer = 0f;

                var emitter = _particleSystemManager.CreateEmitter(ParticleEffects.CreateTeleportParticles());
                emitter.Position = combat.Position;
                emitter.EmitBurst(20);

                Vector2 target;
                int attempts = 0;
                do
                {
                    target = context.Arena.GetRandomArenaPoint();
                    attempts++;
                } while (Vector2.Distance(combat.Position, target) < combat.EquippedActiveSpell.MinDistance && attempts < 50);

                combat.TeleportTargetPos = target;
            }

            return true;
        }

        private void TriggerHeartFlash(int oldHP, int newHP)
        {
            var ui = _wizard.Data.UI;
            if (ui.HeartFlashTimers == null) return;
            int maxHearts = ui.HeartFlashTimers.Length;
            for (int i = 0; i < maxHearts; i++)
            {
                int oldHeartVal = Math.Clamp(oldHP - i * 3, 0, 3);
                int newHeartVal = Math.Clamp(newHP - i * 3, 0, 3);
                if (oldHeartVal > newHeartVal)
                {
                    ui.HeartFlashTimers[i] = 0.75f;
                    if (oldHeartVal == 3 && newHeartVal == 2) ui.HeartFlashFrame[i] = 5;
                    else if (oldHeartVal == 2 && newHeartVal == 1) ui.HeartFlashFrame[i] = 6;
                    else if (oldHeartVal == 1 && newHeartVal == 0) ui.HeartFlashFrame[i] = 7;
                    else if (oldHeartVal == 2 && newHeartVal == 0) ui.HeartFlashFrame[i] = 8;
                    else ui.HeartFlashFrame[i] = 4;
                }
            }
        }

        public int GetHeartFlashFrame(int index)
        {
            var ui = _wizard.Data.UI;
            if (ui.HeartFlashTimers != null && index < ui.HeartFlashTimers.Length && ui.HeartFlashTimers[index] > 0)
            {
                bool isFlashFrame = (ui.HeartFlashTimers[index] % 0.15f) > 0.075f;
                if (isFlashFrame) return ui.HeartFlashFrame[index];
            }
            return -1;
        }

        public float GetDeathAlpha()
        {
            var combat = _wizard.Data.Combat;
            var ui = _wizard.Data.UI;
            if (combat.State != WizardState.Dead) return 1.0f;
            float progress = Math.Clamp(combat.TimeSinceDeath / ui.DeadBodyFadeDuration, 0f, 1f);
            return MathHelper.Lerp(1.0f, ui.DeadBodyMinAlpha, progress);
        }

        public void Update(float dt, BattleContext context)
        {
            var combat = _wizard.Data.Combat;
            var stats = _wizard.Data.Stats;
            var ui = _wizard.Data.UI;

            if (combat.ActiveSpellCooldownTimer > 0) combat.ActiveSpellCooldownTimer -= dt;
            if (combat.WardTimer > 0) combat.WardTimer -= dt;
            if (combat.WardHitTimer > 0) combat.WardHitTimer -= dt;

            if (combat.IsTeleporting)
            {
                combat.TeleportTimer -= dt;
                if (combat.TeleportTimer <= 0)
                {
                    combat.IsTeleporting = false;
                    combat.Position = combat.TeleportTargetPos;
                    combat.TargetPosition = combat.Position;
                    var emitter = _particleSystemManager.CreateEmitter(ParticleEffects.CreateTeleportParticles());
                    emitter.Position = combat.Position;
                    emitter.EmitBurst(20);

                    while (combat.SuspendedActions.Count > 0)
                    {
                        combat.SuspendedActions.Dequeue()?.Invoke();
                    }
                }
                return;
            }

            _wizard.AIController?.Update(dt, context, _wizard);

            for (int i = ui.FloatingTexts.Count - 1; i >= 0; i--)
            {
                var ft = ui.FloatingTexts[i];
                ft.Timer -= dt;
                ft.LocalOffset.Y -= 8f * dt;
                if (ft.Timer <= 0)
                {
                    ui.FloatingTexts.RemoveAt(i);
                    Pools.FloatingText.Return(ft);
                }
            }

            int maxHearts = (stats.MaxHP + 2) / 3;
            float waveDuration = maxHearts * 0.08f + 0.15f;

            ui.FloatingHeartWaveTimer += dt;
            if (ui.FloatingHeartWaveTimer > ui.FloatingHeartWaveInterval + waveDuration)
            {
                ui.FloatingHeartWaveTimer = 0f;
                ui.FloatingHeartWaveInterval = 2f + (float)_random.NextDouble() * 4f;
            }

            ui.HudHeartWaveTimer += dt;
            if (ui.HudHeartWaveTimer > ui.HudHeartWaveInterval + waveDuration)
            {
                ui.HudHeartWaveTimer = 0f;
                ui.HudHeartWaveInterval = 2f + (float)_random.NextDouble() * 4f;
            }

            if (combat.InvincibilityTimer > 0)
            {
                combat.InvincibilityTimer -= dt;
            }

            if (combat.KnockbackTimer > 0)
            {
                combat.KnockbackTimer -= dt;
                float progress = 1f - Math.Max(0, combat.KnockbackTimer) / combat.KnockbackDuration;

                float eased = Easing.EaseOutQuad(progress);

                combat.Position = Vector2.Lerp(combat.KnockbackStartPos, combat.KnockbackTargetPos, eased);
                combat.Position = context.Arena.ClampToArena(combat.Position, 12f);
            }
            else
            {
                float deltaX = combat.Position.X - combat.PreviousPosition.X;
                if (Math.Abs(deltaX) > 0.001f)
                {
                    combat.IsFacingRight = deltaX > 0;
                }
            }

            combat.PreviousPosition = combat.Position;

            if (ui.HudShakeTimer > 0)
            {
                ui.HudShakeTimer -= dt;
            }

            if (ui.MoveTextTimer > 0)
            {
                ui.MoveTextTimer -= dt;
            }

            if (ui.HeartFlashTimers != null)
            {
                for (int i = 0; i < ui.HeartFlashTimers.Length; i++)
                {
                    if (ui.HeartFlashTimers[i] > 0)
                    {
                        ui.HeartFlashTimers[i] -= dt;
                    }
                }
            }

            if (ui.IsHovered)
            {
                ui.HealthBarVisibilityTimer = ui.HealthBarLingerDuration;
                ui.HealthBarAlpha = 1.0f;
            }
            else if (ui.HealthBarVisibilityTimer > 0)
            {
                ui.HealthBarVisibilityTimer -= dt;
                ui.HealthBarAlpha = 1.0f;
            }
            else if (ui.HealthBarAlpha > 0f)
            {
                ui.HealthBarAlpha = Math.Max(0f, ui.HealthBarAlpha - dt * 4f);
            }

            if (combat.State == WizardState.Dead)
            {
                combat.TimeSinceDeath += dt;
                return;
            }

            if (stats.CurrentHP <= 0)
            {
                if (combat.InvincibilityTimer <= 0)
                {
                    combat.State = WizardState.Dead;
                    combat.TimeSinceDeath = 0f;
                }
                return;
            }

            switch (combat.State)
            {
                case WizardState.Moving:
                    if (combat.KnockbackTimer <= 0) UpdateMovement(dt, context.Arena);
                    combat.ActionTimer -= dt;
                    if (combat.ActionTimer <= 0)
                    {
                        PrepareAttack(context);
                    }
                    break;

                case WizardState.Telegraphing:
                    if (combat.QueuedTargetWizard != null && combat.QueuedTargetWizard.Data.Combat.IsSuspended)
                    {
                        combat.State = WizardState.Recovering;
                        combat.StateTimer = 0.25f;
                        combat.QueuedTargetWizard = null;
                        break;
                    }

                    if (combat.QueuedMove.TargetSelf)
                    {
                        combat.QueuedTargetPos = combat.Position;
                    }

                    combat.QueuedDirection = combat.QueuedTargetPos - combat.Position;
                    if (combat.QueuedDirection.LengthSquared() > 0)
                    {
                        combat.QueuedDirection.Normalize();
                    }
                    else
                    {
                        combat.QueuedDirection = new Vector2(1, 0);
                    }

                    combat.StateTimer -= dt;
                    if (combat.StateTimer <= 0)
                    {
                        ExecuteAttack(context);
                    }
                    break;

                case WizardState.Casting:
                    if (combat.CurrentActiveAttack == null || combat.CurrentActiveAttack.IsPooled)
                    {
                        combat.CurrentActiveAttack = null;
                        combat.State = WizardState.Recovering;
                        combat.StateTimer = 0.25f;
                        combat.TargetPosition = combat.Position;
                        break;
                    }

                    bool animFinished = combat.CurrentActiveAttack.Animation == null || combat.CurrentActiveAttack.Animation.IsFinished;
                    bool deliveryFinished = combat.CurrentActiveAttack.DeliveryInstance == null || combat.CurrentActiveAttack.DeliveryInstance.IsFinished;

                    if ((deliveryFinished && animFinished) || combat.CurrentActiveAttack.IsCanceled)
                    {
                        combat.CurrentActiveAttack = null;
                        combat.State = WizardState.Recovering;
                        combat.StateTimer = 0.25f;
                        combat.TargetPosition = combat.Position;
                    }
                    break;

                case WizardState.Recovering:
                    combat.StateTimer -= dt;
                    if (combat.StateTimer <= 0)
                    {
                        combat.State = WizardState.Moving;
                        combat.ActionTimer = GetRandomActionTime();
                    }
                    break;
            }
        }

        private void UpdateMovement(float dt, ArenaScene arena)
        {
            var combat = _wizard.Data.Combat;
            var stats = _wizard.Data.Stats;
            var ui = _wizard.Data.UI;

            float dist = Vector2.Distance(combat.Position, combat.TargetPosition);
            if (dist < 1f)
            {
                combat.TargetPosition = arena.GetRandomArenaPoint();
            }

            Vector2 dir = combat.TargetPosition - combat.Position;
            if (dir.LengthSquared() > 0)
            {
                dir.Normalize();
                combat.Position += dir * stats.Speed * dt;
                combat.Position = arena.ClampToArena(combat.Position, 12f);
                ui.HopTimer += dt * stats.Speed * 0.5f;
            }
        }

        private void PrepareAttack(BattleContext context)
        {
            var combat = _wizard.Data.Combat;
            var ui = _wizard.Data.UI;

            if (combat.Moves.Count == 0)
            {
                combat.ActionTimer = GetRandomActionTime();
                return;
            }

            int totalWeight = 0;
            foreach (var move in combat.Moves)
            {
                totalWeight += move.Weight;
            }

            int roll = _random.Next(totalWeight);
            int currentWeight = 0;

            for (int i = 0; i < combat.Moves.Count; i++)
            {
                currentWeight += combat.Moves[i].Weight;
                if (roll < currentWeight)
                {
                    combat.QueuedMove = combat.Moves[i];
                    break;
                }
            }

            ArenaWizard target = null;

            if (combat.QueuedMove.TargetSelf)
            {
                target = _wizard;
            }
            else
            {
                if (combat.QueuedMove.TargetClosest || combat.QueuedMove.Delivery is DashMeleeDelivery || combat.QueuedMove.Delivery is SeekAndDashDelivery)
                {
                    float closestDist = float.MaxValue;
                    foreach (var w in context.Arena.Wizards)
                    {
                        if (w == _wizard || w.Data.Stats.CurrentHP <= 0 || w.Data.Combat.IsSuspended) continue;
                        float dist = Vector2.DistanceSquared(combat.Position, w.Data.Combat.Position);
                        if (dist < closestDist)
                        {
                            closestDist = dist;
                            target = w;
                        }
                    }
                }
                else
                {
                    int validCount = 0;
                    foreach (var w in context.Arena.Wizards)
                    {
                        if (w != _wizard && w.Data.Stats.CurrentHP > 0 && !w.Data.Combat.IsSuspended) validCount++;
                    }

                    if (validCount > 0)
                    {
                        int targetRoll = _random.Next(validCount);
                        int curr = 0;
                        foreach (var w in context.Arena.Wizards)
                        {
                            if (w != _wizard && w.Data.Stats.CurrentHP > 0 && !w.Data.Combat.IsSuspended)
                            {
                                if (curr == targetRoll)
                                {
                                    target = w;
                                    break;
                                }
                                curr++;
                            }
                        }
                    }
                }

                if (target == null && !(combat.QueuedMove.Delivery is SeekAndDashDelivery))
                {
                    combat.ActionTimer = GetRandomActionTime();
                    return;
                }
            }

            combat.QueuedTargetWizard = target;
            combat.QueuedTargetPos = target != null ? target.Data.Combat.Position : combat.Position;

            combat.QueuedDirection = combat.QueuedTargetPos - combat.Position;
            if (combat.QueuedDirection.LengthSquared() > 0)
            {
                combat.QueuedDirection.Normalize();
                combat.IsFacingRight = combat.QueuedDirection.X > 0;
            }
            else
            {
                combat.QueuedDirection = new Vector2(1, 0);
                combat.IsFacingRight = true;
            }

            ui.ActiveMoveText = combat.QueuedMove.Name;
            ui.MoveTextDuration = Math.Max(0.8f, combat.QueuedMove.ChargeTime + 0.2f);
            ui.MoveTextTimer = ui.MoveTextDuration;

            if (combat.QueuedMove.ExecuteOnChargeStart)
            {
                ExecuteAttack(context);
            }
            else
            {
                combat.State = WizardState.Telegraphing;
                combat.StateTimer = combat.QueuedMove.ChargeTime;
            }
        }

        private void ExecuteAttack(BattleContext context)
        {
            var combat = _wizard.Data.Combat;

            var attack = Pools.ActiveAttack.Get();
            attack.Reset();
            attack.Context = context;
            attack.Caster = _wizard;
            attack.TargetWizard = combat.QueuedTargetWizard;
            attack.Move = combat.QueuedMove;
            attack.Origin = combat.Position;
            attack.Direction = combat.QueuedDirection;
            attack.TargetPosition = combat.QueuedTargetPos;
            attack.DeliveryInstance = combat.QueuedMove.Delivery.GetInstanceFromPool();
            attack.Animation = AnimationFactory.CreateAnimation(combat.QueuedMove.AnimationID);
            attack.HasTriggeredImpact = false;

            combat.CurrentActiveAttack = attack;
            context.Arena.SpawnAttack(attack);

            combat.State = WizardState.Casting;
        }

        private float GetRandomActionTime()
        {
            var stats = _wizard.Data.Stats;
            float baseTime = 2.0f + (float)_random.NextDouble() * 6.0f;
            float speedMultiplier = 1.0f + (stats.Agility - 5) * 0.1f;
            speedMultiplier = Math.Clamp(speedMultiplier, 0.1f, 3.0f);
            return baseTime / speedMultiplier;
        }
    }
}
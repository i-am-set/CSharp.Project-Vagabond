using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Animations;
using ProjectVagabond.Particles;
using ProjectVagabond.Scenes;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;

namespace ProjectVagabond.Battle
{
    public enum WizardState
    {
        Moving,
        Telegraphing,
        Casting,
        Recovering,
        Dead
    }
    public class ArenaWizard
    {
        public string Name;
        public Vector2 Position;
        public Vector2 TargetPosition;
        public float Speed;
        public int PortraitIndex;
        public bool IsPlayer;
        public float HopTimer;
        public bool IsFacingRight { get; private set; } = false;

        public int MaxHP;
        public int CurrentHP { get; private set; }

        public float InvincibilityDuration = 0.4f;
        public float InvincibilityTimer { get; private set; }
        public float HudShakeTimer { get; private set; }

        public float FloatingHeartWaveTimer { get; private set; }
        public float FloatingHeartWaveInterval { get; private set; }
        public float HudHeartWaveTimer { get; private set; }
        public float HudHeartWaveInterval { get; private set; }

        public int HP;
        public int Power;
        public int Tenacity;
        public int Agility;

        public int Rating { get; private set; }
        public float PayoutMultiplier { get; set; }

        public WizardState State = WizardState.Moving;
        public List<MoveDefinition> Moves = new List<MoveDefinition>();

        public bool IsHovered;

        public float HealthBarLingerDuration = 2.5f;
        public float DeadBodyFadeDuration = 16.0f;
        public float DeadBodyMinAlpha = 0.0f;

        public float TimeSinceDeath { get; private set; } = 0f;

        // HUD Layout Cache
        public Vector2 HudNameSize;
        public Vector2 HudNamePos;
        public Vector2 HudHeartStartPos;
        public bool HudIsLeft;

        private float _actionTimer;
        private float _stateTimer;
        private MoveDefinition _queuedMove;
        private Vector2 _queuedTargetPos;
        private ArenaWizard _queuedTargetWizard;
        private Vector2 _queuedDirection;
        private ActiveAttack _currentActiveAttack;

        private float[] _heartFlashTimers;
        private int[] _heartFlashFrame;

        private float _healthBarVisibilityTimer = 0f;
        private float _healthBarAlpha = 0f;

        private string _activeMoveText;
        private float _moveTextTimer;
        private float _moveTextDuration;

        private Vector2 _knockbackStartPos;
        private Vector2 _knockbackTargetPos;
        private float _knockbackTimer;
        private float _knockbackDuration;
        private Vector2 _previousPosition;

        private static readonly Random _random = new Random();

        private class FloatingText
        {
            public int Number;
            public bool IsHealing;
            public bool IsCrit;
            public float Timer;
            public float Duration;
            public Vector2 LocalOffset;
        }
        private readonly List<FloatingText> _floatingTexts = new List<FloatingText>();

        public void Initialize(WizardCatData data, Vector2 startPos, bool isPlayer)
        {
            Name = data.Name;
            Position = startPos;
            TargetPosition = startPos;
            _previousPosition = startPos;
            IsPlayer = isPlayer;
            PortraitIndex = int.TryParse(data.MemberID, out int pid) ? pid : 0;
            HopTimer = (float)(_random.NextDouble() * MathHelper.TwoPi);
            IsFacingRight = false;

            FloatingHeartWaveInterval = 1f + (float)_random.NextDouble() * 4f;
            FloatingHeartWaveTimer = 0f;
            HudHeartWaveInterval = 1f + (float)_random.NextDouble() * 4f;
            HudHeartWaveTimer = 0f;

            HP = data.HP;
            Power = data.Power;
            Tenacity = data.Tenacity;
            Agility = data.Agility;

            MaxHP = HP * 3;
            Rating = (Power + Tenacity + Agility) * MaxHP;

            int maxHearts = (MaxHP + 2) / 3;
            _heartFlashTimers = new float[maxHearts];
            _heartFlashFrame = new int[maxHearts];

            CurrentHP = MaxHP;
            Speed = Agility * 2.0f + 2.5f;

            _healthBarAlpha = 0f;
            _actionTimer = GetRandomActionTime();

            LoadMoves(data);
        }

        private void LoadMoves(WizardCatData data)
        {
            Moves.Clear();
            string[] slots = { data.Move1, data.Move2, data.Move3, data.Move4 };

            foreach (var slot in slots)
            {
                if (!string.IsNullOrWhiteSpace(slot) && GameDataCache.Moves.TryGetValue(slot, out var moveData))
                {
                    Moves.Add(MoveFactory.CreateMove(moveData));
                }
            }
        }

        public Rectangle GetHitbox(SpriteManager spriteManager)
        {
            var bounds = spriteManager.GetPlayerSpriteBounds(PortraitIndex, PlayerSpriteType.Portrait5x5);
            float hopOffset = State == WizardState.Dead ? 0f : -MathF.Abs(MathF.Sin(HopTimer)) * 4f;

            if (IsFacingRight)
            {
                bounds = new Rectangle(-(bounds.X + bounds.Width), bounds.Y, bounds.Width, bounds.Height);
            }

            if (State == WizardState.Dead)
            {
                int newX = -(bounds.Y + bounds.Height);
                int newY = bounds.X;
                bounds = new Rectangle(newX, newY, bounds.Height, bounds.Width);
            }

            return new Rectangle(
                (int)MathF.Round(Position.X) + bounds.X,
                (int)MathF.Round(Position.Y + hopOffset) + bounds.Y,
                bounds.Width,
                bounds.Height
            );
        }

        public bool TakeDamage(int amount, bool isCrit = false)
        {
            if (InvincibilityTimer > 0 || State == WizardState.Dead || CurrentHP <= 0) return false;

            int oldHP = CurrentHP;
            CurrentHP = Math.Clamp(CurrentHP - amount, 0, MaxHP);
            int actualDamage = oldHP - CurrentHP;

            if (actualDamage > 0)
            {
                TriggerHeartFlash(oldHP, CurrentHP);
                InvincibilityTimer = InvincibilityDuration;
                HudShakeTimer = 0.4f;

                _healthBarVisibilityTimer = HealthBarLingerDuration;
                _healthBarAlpha = 1.0f;

                var spriteManager = ServiceLocator.Get<SpriteManager>();
                var hitbox = GetHitbox(spriteManager);
                Vector2 centerOffset = new Vector2(hitbox.Center.X - Position.X, hitbox.Center.Y - Position.Y);

                _floatingTexts.Add(new FloatingText
                {
                    Number = actualDamage,
                    IsHealing = false,
                    IsCrit = isCrit,
                    Duration = 1.0f,
                    Timer = 1.0f,
                    LocalOffset = centerOffset + new Vector2(_random.Next(-8, 9), 0)
                });

                return true;
            }
            return false;
        }

        public void Heal(int amount)
        {
            if (State == WizardState.Dead) return;

            int oldHP = CurrentHP;
            CurrentHP = Math.Clamp(CurrentHP + amount, 0, MaxHP);
            int actualHeal = CurrentHP - oldHP;

            if (actualHeal > 0)
            {
                var spriteManager = ServiceLocator.Get<SpriteManager>();
                var hitbox = GetHitbox(spriteManager);
                Vector2 centerOffset = new Vector2(hitbox.Center.X - Position.X, hitbox.Center.Y - Position.Y);

                _floatingTexts.Add(new FloatingText
                {
                    Number = actualHeal,
                    IsHealing = true,
                    IsCrit = false,
                    Duration = 1.0f,
                    Timer = 1.0f,
                    LocalOffset = centerOffset + new Vector2(_random.Next(-8, 9), 0)
                });
            }
        }

        public void ApplyKnockback(Vector2 sourcePosition, float distance, ArenaScene arena)
        {
            if (State == WizardState.Dead) return;

            if ((State == WizardState.Casting || State == WizardState.Telegraphing) && _queuedMove != null)
            {
                if (_queuedMove.RequiresFocus)
                {
                    if (_currentActiveAttack != null)
                    {
                        _currentActiveAttack.IsCanceled = true;
                        _currentActiveAttack = null;
                    }
                    State = WizardState.Recovering;
                    _stateTimer = 0.5f;
                }
            }

            Vector2 dir = Position - sourcePosition;
            if (dir.LengthSquared() > 0)
                dir.Normalize();
            else
                dir = new Vector2(1, 0);

            _knockbackStartPos = Position;
            Vector2 desiredTarget = Position + dir * distance;

            _knockbackTargetPos = arena.ClampToArena(desiredTarget, 12f);
            
            _knockbackDuration = 0.5f + (distance / 80f);
            _knockbackTimer = _knockbackDuration;
        }

        private void TriggerHeartFlash(int oldHP, int newHP)
        {
            if (_heartFlashTimers == null) return;
            int maxHearts = _heartFlashTimers.Length;
            for (int i = 0; i < maxHearts; i++)
            {
                int oldHeartVal = Math.Clamp(oldHP - i * 3, 0, 3);
                int newHeartVal = Math.Clamp(newHP - i * 3, 0, 3);
                if (oldHeartVal > newHeartVal)
                {
                    _heartFlashTimers[i] = 0.75f;
                    if (oldHeartVal == 3 && newHeartVal == 2) _heartFlashFrame[i] = 5; // 3/3 only flash
                    else if (oldHeartVal == 2 && newHeartVal == 1) _heartFlashFrame[i] = 6; // 2/3 only flash
                    else if (oldHeartVal == 1 && newHeartVal == 0) _heartFlashFrame[i] = 7; // 1/3 only flash
                    else if (oldHeartVal == 2 && newHeartVal == 0) _heartFlashFrame[i] = 8; // 1/3 and 2/3 flash
                    else _heartFlashFrame[i] = 4; // Full flash (covers 3->0 and 3->1)
                }
            }
        }

        public int GetHeartFlashFrame(int index)
        {
            if (_heartFlashTimers != null && index < _heartFlashTimers.Length && _heartFlashTimers[index] > 0)
            {
                bool isFlashFrame = (_heartFlashTimers[index] % 0.15f) > 0.075f;
                if (isFlashFrame) return _heartFlashFrame[index];
            }
            return -1;
        }

        public float GetDeathAlpha()
        {
            if (State != WizardState.Dead) return 1.0f;
            float progress = Math.Clamp(TimeSinceDeath / DeadBodyFadeDuration, 0f, 1f);
            return MathHelper.Lerp(1.0f, DeadBodyMinAlpha, progress);
        }

        public void Update(float dt, ArenaScene arena)
        {
            for (int i = _floatingTexts.Count - 1; i >= 0; i--)
            {
                var ft = _floatingTexts[i];
                ft.Timer -= dt;
                ft.LocalOffset.Y -= 8f * dt;
                if (ft.Timer <= 0)
                {
                    _floatingTexts.RemoveAt(i);
                }
            }

            int maxHearts = (MaxHP + 2) / 3;
            float waveDuration = maxHearts * 0.08f + 0.15f;

            FloatingHeartWaveTimer += dt;
            if (FloatingHeartWaveTimer > FloatingHeartWaveInterval + waveDuration)
            {
                FloatingHeartWaveTimer = 0f;
                FloatingHeartWaveInterval = 2f + (float)_random.NextDouble() * 4f;
            }

            HudHeartWaveTimer += dt;
            if (HudHeartWaveTimer > HudHeartWaveInterval + waveDuration)
            {
                HudHeartWaveTimer = 0f;
                HudHeartWaveInterval = 2f + (float)_random.NextDouble() * 4f;
            }

            if (InvincibilityTimer > 0)
            {
                InvincibilityTimer -= dt;
            }

            if (_knockbackTimer > 0)
            {
                _knockbackTimer -= dt;
                float progress = 1f - Math.Max(0, _knockbackTimer) / _knockbackDuration;

                // Use EaseOutQuad instead of Cubic for a softer, less jarring initial push
                float eased = Easing.EaseOutQuad(progress);

                Position = Vector2.Lerp(_knockbackStartPos, _knockbackTargetPos, eased);
                Position = arena.ClampToArena(Position, 12f);
            }
            else
            {
                float deltaX = Position.X - _previousPosition.X;
                if (Math.Abs(deltaX) > 0.001f)
                {
                    IsFacingRight = deltaX > 0;
                }
            }

            _previousPosition = Position;

            if (HudShakeTimer > 0)
            {
                HudShakeTimer -= dt;
            }

            if (_moveTextTimer > 0)
            {
                _moveTextTimer -= dt;
            }

            if (_heartFlashTimers != null)
            {
                for (int i = 0; i < _heartFlashTimers.Length; i++)
                {
                    if (_heartFlashTimers[i] > 0)
                    {
                        _heartFlashTimers[i] -= dt;
                    }
                }
            }

            if (IsHovered)
            {
                _healthBarVisibilityTimer = HealthBarLingerDuration;
                _healthBarAlpha = 1.0f;
            }
            else if (_healthBarVisibilityTimer > 0)
            {
                _healthBarVisibilityTimer -= dt;
                _healthBarAlpha = 1.0f;
            }
            else if (_healthBarAlpha > 0f)
            {
                _healthBarAlpha = Math.Max(0f, _healthBarAlpha - dt * 4f);
            }

            if (State == WizardState.Dead)
            {
                TimeSinceDeath += dt;
                return;
            }

            if (CurrentHP <= 0)
            {
                if (InvincibilityTimer <= 0)
                {
                    State = WizardState.Dead;
                    TimeSinceDeath = 0f;
                }
                return;
            }

            switch (State)
            {
                case WizardState.Moving:
                    if (_knockbackTimer <= 0) UpdateMovement(dt, arena);
                    _actionTimer -= dt;
                    if (_actionTimer <= 0)
                    {
                        PrepareAttack(arena);
                    }
                    break;

                case WizardState.Telegraphing:
                    if (_queuedMove.TargetSelf)
                    {
                        _queuedTargetPos = Position;
                    }

                    _queuedDirection = _queuedTargetPos - Position;
                    if (_queuedDirection.LengthSquared() > 0)
                    {
                        _queuedDirection.Normalize();
                    }
                    else
                    {
                        _queuedDirection = new Vector2(1, 0);
                    }

                    _stateTimer -= dt;
                    if (_stateTimer <= 0)
                    {
                        ExecuteAttack(arena);
                    }
                    break;

                case WizardState.Casting:
                    bool animFinished = _currentActiveAttack == null || _currentActiveAttack.Animation == null || _currentActiveAttack.Animation.IsFinished;
                    if (_currentActiveAttack == null || (_currentActiveAttack.DeliveryInstance.IsFinished && animFinished))
                    {
                        State = WizardState.Recovering;
                        _stateTimer = 0.25f;
                    }
                    break;

                case WizardState.Recovering:
                    _stateTimer -= dt;
                    if (_stateTimer <= 0)
                    {
                        State = WizardState.Moving;
                        _actionTimer = GetRandomActionTime();
                    }
                    break;
            }
        }

        private void UpdateMovement(float dt, ArenaScene arena)
        {
            float dist = Vector2.Distance(Position, TargetPosition);
            if (dist < 1f)
            {
                TargetPosition = arena.GetRandomArenaPoint();
            }

            Vector2 dir = TargetPosition - Position;
            if (dir.LengthSquared() > 0)
            {
                dir.Normalize();
                Position += dir * Speed * dt;
                Position = arena.ClampToArena(Position, 12f);
                HopTimer += dt * Speed * 0.5f;
            }
        }

        private void PrepareAttack(ArenaScene arena)
        {
            if (Moves.Count == 0)
            {
                _actionTimer = GetRandomActionTime();
                return;
            }

            int totalWeight = 0;
            foreach (var move in Moves)
            {
                totalWeight += move.Weight;
            }

            int roll = _random.Next(totalWeight);
            int currentWeight = 0;

            for (int i = 0; i < Moves.Count; i++)
            {
                currentWeight += Moves[i].Weight;
                if (roll < currentWeight)
                {
                    _queuedMove = Moves[i];
                    break;
                }
            }

            ArenaWizard target = null;

            if (_queuedMove.TargetSelf)
            {
                target = this;
            }
            else
            {
                if (_queuedMove.TargetClosest || _queuedMove.Delivery is DashMeleeDelivery || _queuedMove.Delivery is SeekAndDashDelivery)
                {
                    float closestDist = float.MaxValue;
                    foreach (var w in arena.Wizards)
                    {
                        if (w == this || w.CurrentHP <= 0) continue;
                        float dist = Vector2.DistanceSquared(Position, w.Position);
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
                    foreach (var w in arena.Wizards)
                    {
                        if (w != this && w.CurrentHP > 0) validCount++;
                    }

                    if (validCount > 0)
                    {
                        int targetRoll = _random.Next(validCount);
                        int curr = 0;
                        foreach (var w in arena.Wizards)
                        {
                            if (w != this && w.CurrentHP > 0)
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

                if (target == null && !(_queuedMove.Delivery is SeekAndDashDelivery))
                {
                    _actionTimer = GetRandomActionTime();
                    return;
                }
            }

            _queuedTargetWizard = target;
            _queuedTargetPos = target != null ? target.Position : Position;

            _queuedDirection = _queuedTargetPos - Position;
            if (_queuedDirection.LengthSquared() > 0)
            {
                _queuedDirection.Normalize();
                IsFacingRight = _queuedDirection.X > 0;
            }
            else
            {
                _queuedDirection = new Vector2(1, 0);
                IsFacingRight = true;
            }

            _activeMoveText = _queuedMove.Name;
            _moveTextDuration = Math.Max(0.8f, _queuedMove.ChargeTime + 0.2f);
            _moveTextTimer = _moveTextDuration;

            if (_queuedMove.ExecuteOnChargeStart)
            {
                ExecuteAttack(arena);
            }
            else
            {
                State = WizardState.Telegraphing;
                _stateTimer = _queuedMove.ChargeTime;
            }
        }

        private void ExecuteAttack(ArenaScene arena)
        {
            var attack = new ActiveAttack
            {
                Caster = this,
                TargetWizard = _queuedTargetWizard,
                Move = _queuedMove,
                Origin = Position,
                Direction = _queuedDirection,
                TargetPosition = _queuedTargetPos,
                DeliveryInstance = _queuedMove.Delivery.Clone(),
                Animation = AnimationFactory.CreateAnimation(_queuedMove.AnimationID),
                HasTriggeredImpact = false
            };

            _currentActiveAttack = attack;
            arena.SpawnAttack(attack);

            State = WizardState.Casting;
        }

        public void DrawUI(SpriteBatch spriteBatch, SpriteManager spriteManager, GameTime gameTime)
        {
            int wizX = (int)MathF.Round(Position.X);
            float hopOffset = State == WizardState.Dead ? 0f : -MathF.Abs(MathF.Sin(HopTimer)) * 4f;
            int wizY = (int)MathF.Round(Position.Y + hopOffset);

            var global = ServiceLocator.Get<Global>();
            var core = ServiceLocator.Get<Core>();
            var tertiaryFont = core.TertiaryFont;
            var secondaryFont = core.SecondaryFont;
            var defaultFont = core.DefaultFont;

            if (_moveTextTimer > 0 && !string.IsNullOrEmpty(_activeMoveText) && State != WizardState.Dead)
            {
                var font = core.SecondaryFont;

                float timeElapsed = _moveTextDuration - _moveTextTimer;
                float scale = 1f;
                float alpha = 1f;

                float appearDuration = 0.15f;
                float expireDuration = 0.2f;

                if (timeElapsed < appearDuration)
                {
                    scale = Easing.EaseOutBack(timeElapsed / appearDuration);
                }
                else if (_moveTextTimer < expireDuration)
                {
                    float shrinkProgress = 1f - (_moveTextTimer / expireDuration);
                    scale = Math.Max(0f, 1f - Easing.EaseInBack(shrinkProgress));
                    alpha = (_moveTextTimer / expireDuration);
                }

                if (scale > 0.01f)
                {
                    Vector2 textSize = font.MeasureString(_activeMoveText);
                    Vector2 textPos = new Vector2(wizX, wizY - 16);
                    Vector2 origin = new Vector2(textSize.X / 2f, textSize.Y / 2f);

                    spriteBatch.DrawStringOutlinedSnapped(font, _activeMoveText, textPos, global.Palette_Sun * alpha, global.Palette_Off * alpha, 0f, origin, scale, SpriteEffects.None, 0f);
                }
            }

            foreach (var ft in _floatingTexts)
            {
                bool isFlash = (ft.Timer % 0.1f) > 0.05f;
                string text = (ft.IsHealing ? $"+{ft.Number}" : $"-{ft.Number}");

                BitmapFont font = core.TertiaryFont;
                if (ft.Number > 4)
                {
                    font = core.DefaultFont;
                }
                else if (ft.Number > 2)
                {
                    font = core.SecondaryFont;
                }
                else
                {
                    font = core.TertiaryFont;
                }

                Color textColor = ft.IsHealing
                    ? (isFlash ? global.Palette_Sun : global.Palette_Leaf)
                    : (isFlash ? global.Palette_Sun : global.Palette_Rust);

                float alphaMult = Math.Clamp(ft.Timer / 0.2f, 0f, 1f);
                Color finalTextColor = textColor * alphaMult;
                Color outlineColor = global.Palette_Off * alphaMult;

                Vector2 textPos = new Vector2(MathF.Round(Position.X), MathF.Round(Position.Y)) + ft.LocalOffset;
                Vector2 textSize = font.MeasureString(text);
                Vector2 origin = new Vector2(MathF.Round(textSize.X / 2f), MathF.Round(textSize.Y / 2f));

                spriteBatch.DrawStringOutlinedSnapped(font, text, textPos, finalTextColor, outlineColor, 0f, origin, 1f, SpriteEffects.None, 0f);

                if (ft.IsCrit)
                {
                    string critText = "CRIT";
                    BitmapFont critFont = core.TertiaryFont;
                    Vector2 critSize = critFont.MeasureString(critText);

                    // Position it centered above the damage number
                    Vector2 critCenter = textPos - new Vector2(0, MathF.Round(textSize.Y / 2f + critSize.Y / 2f + 1));
                    Vector2 critTopLeft = critCenter - new Vector2(MathF.Round(critSize.X / 2f), MathF.Round(critSize.Y / 2f));

                    Color critTextColor = isFlash ? global.Palette_Sun : global.CritcalHitIndicatorColor;
                    Color finalCritTextColor = critTextColor * alphaMult;

                    TextAnimator.DrawTextWithEffectOutlined(
                        spriteBatch,
                        critFont,
                        critText,
                        critTopLeft,
                        finalCritTextColor,
                        outlineColor,
                        TextEffectType.Shake,
                        (float)gameTime.TotalGameTime.TotalSeconds,
                        Vector2.One,
                        0f
                    );
                }
            }

            if (State == WizardState.Dead || _healthBarAlpha <= 0f) return;

            var sheet = spriteManager.HealthHearts3x3SpriteSheet;
            if (sheet == null) return;

            int maxHearts = (MaxHP + 2) / 3;
            int heartWidth = 3;
            int spacing = 1;
            int totalWidth = maxHearts * heartWidth + (maxHearts - 1) * spacing;

            int startX = wizX - (totalWidth / 2) - 1;
            int startY = wizY + 11;

            Color drawColor = Color.White * _healthBarAlpha;

            for (int i = 0; i < maxHearts; i++)
            {
                int heartVal = Math.Clamp(CurrentHP - i * 3, 0, 3);
                int frameIndex = 3; // 0/3

                if (heartVal == 3) frameIndex = 0; // 3/3
                else if (heartVal == 2) frameIndex = 1; // 2/3
                else if (heartVal == 1) frameIndex = 2; // 1/3

                int flashFrame = GetHeartFlashFrame(i);
                if (flashFrame != -1) frameIndex = flashFrame;

                var sourceRect = new Rectangle(frameIndex * heartWidth, 0, heartWidth, 3);

                int yOffset = 0;
                if (CurrentHP > 0)
                {
                    float localWaveTime = FloatingHeartWaveTimer - FloatingHeartWaveInterval - (i * 0.08f);
                    if (localWaveTime > 0 && localWaveTime < 0.15f)
                    {
                        yOffset = -1;
                    }
                }

                Vector2 pos = new Vector2(startX + i * (heartWidth + spacing), startY + yOffset);

                spriteBatch.DrawSnapped(sheet, pos, sourceRect, drawColor);
            }
        }

        public void DrawDebug(SpriteBatch spriteBatch, SpriteManager spriteManager)
        {
            if (ServiceLocator.Get<Global>().ShowDebugOverlays)
            {
                var pixel = ServiceLocator.Get<Texture2D>();
                var hitbox = GetHitbox(spriteManager);
                spriteBatch.Draw(pixel, new Rectangle(hitbox.X, hitbox.Y, hitbox.Width, 1), Color.Lime * 0.5f);
                spriteBatch.Draw(pixel, new Rectangle(hitbox.X, hitbox.Bottom - 1, hitbox.Width, 1), Color.Lime * 0.5f);
                spriteBatch.Draw(pixel, new Rectangle(hitbox.X, hitbox.Y, 1, hitbox.Height), Color.Lime * 0.5f);
                spriteBatch.Draw(pixel, new Rectangle(hitbox.Right - 1, hitbox.Y, 1, hitbox.Height), Color.Lime * 0.5f);
            }

            if (State == WizardState.Telegraphing && _queuedMove != null)
            {
                _queuedMove.Delivery.DrawTelegraph(spriteBatch, Position, _queuedDirection, _queuedTargetPos);
            }
        }

        private float GetRandomActionTime()
        {
            float baseTime = 2.0f + (float)_random.NextDouble() * 6.0f;
            float speedMultiplier = 1.0f + (Agility - 5) * 0.1f;
            speedMultiplier = Math.Clamp(speedMultiplier, 0.1f, 3.0f);
            return baseTime / speedMultiplier;
        }
    }
}
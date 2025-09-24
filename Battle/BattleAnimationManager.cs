using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Battle.UI
{
    /// <summary>
    /// Manages all visual feedback animations during a battle, such as health bars, hit flashes, and damage indicators.
    /// </summary>
    public class BattleAnimationManager
    {
        // --- Tuning ---
        private const float HEALTH_ANIMATION_DURATION = 0.25f; // Duration of the health bar drain animation in seconds.

        // Internal animation state structs
        public class HealthAnimationState { public string CombatantID; public float StartHP; public float TargetHP; public float Timer; }
        public class AlphaAnimationState { public string CombatantID; public float StartAlpha; public float TargetAlpha; public float Timer; public const float Duration = 0.167f; }
        public class HitAnimationState { public string CombatantID; public float Timer; public const float Duration = 1.0f; }
        public class HealBounceAnimationState { public string CombatantID; public float Timer; public const float Duration = 0.1f; }
        public class HealFlashAnimationState { public string CombatantID; public float Timer; public const float Duration = 0.5f; }
        public class PoisonEffectAnimationState { public string CombatantID; public float Timer; public const float Duration = 1.5f; }
        public class ResourceBarAnimationState
        {
            public enum BarResourceType { HP, Mana }
            public enum BarAnimationType { Loss, Recovery }
            public enum LossPhase { Preview, FlashBlack, FlashWhite, Shrink }

            public string CombatantID;
            public BarResourceType ResourceType;
            public BarAnimationType AnimationType;
            public LossPhase CurrentLossPhase;

            public float ValueBefore; // e.g. 100 HP
            public float ValueAfter;  // e.g. 80 HP

            public float Timer;

            // Loss Animation Tuning
            public const float PREVIEW_DURATION = 0.5f;
            public const float FLASH_BLACK_DURATION = 0.05f;
            public const float FLASH_WHITE_DURATION = 0.05f;
            public const float SHRINK_DURATION = 0.4f;

            // Recovery Animation Tuning
            public const float GHOST_FILL_DURATION = 0.5f;
        }
        public class AbilityIndicatorState
        {
            public enum AnimationPhase { EasingIn, Flashing, Holding, EasingOut }
            public AnimationPhase Phase;
            public string OriginalText;
            public string Text;
            public int Count;
            public Vector2 InitialPosition; // Off-screen bottom
            public Vector2 TargetPosition;  // On-screen "hang" position
            public Vector2 CurrentPosition;
            public float Timer;
            public float ShakeTimer;
            public const float SHAKE_DURATION = 0.5f;
            public const float SHAKE_MAGNITUDE = 4.0f;
            public const float SHAKE_FREQUENCY = 15f;
            public const float EASE_IN_DURATION = 0.1f;
            public const float FLASH_DURATION = 0.15f;
            public const float HOLD_DURATION = 1.0f;
            public const float EASE_OUT_DURATION = 0.4f;
            public const float TOTAL_DURATION = EASE_IN_DURATION + FLASH_DURATION + HOLD_DURATION + EASE_OUT_DURATION;
        }
        public class DamageIndicatorState
        {
            public enum IndicatorType { Text, Number, HealNumber, EmphasizedNumber, Effectiveness }
            public IndicatorType Type;
            public string CombatantID;
            public string Text;
            public Vector2 Position; // Current position
            public Vector2 Velocity; // For physics-based indicators
            public Vector2 InitialPosition; // For simple animation paths
            public Color Color; // Used for text indicators like "CRITICAL"
            public float Timer;
            public const float DURATION = 1.75f;
            public const float RISE_DISTANCE = 5f;
        }

        private readonly List<HealthAnimationState> _activeHealthAnimations = new List<HealthAnimationState>();
        private readonly List<AlphaAnimationState> _activeAlphaAnimations = new List<AlphaAnimationState>();
        private readonly List<HitAnimationState> _activeHitAnimations = new List<HitAnimationState>();
        private readonly List<HealBounceAnimationState> _activeHealBounceAnimations = new List<HealBounceAnimationState>();
        private readonly List<HealFlashAnimationState> _activeHealFlashAnimations = new List<HealFlashAnimationState>();
        private readonly List<PoisonEffectAnimationState> _activePoisonEffectAnimations = new List<PoisonEffectAnimationState>();
        private readonly List<DamageIndicatorState> _activeDamageIndicators = new List<DamageIndicatorState>();
        private readonly List<AbilityIndicatorState> _activeAbilityIndicators = new List<AbilityIndicatorState>();
        private readonly List<ResourceBarAnimationState> _activeBarAnimations = new List<ResourceBarAnimationState>();

        private readonly Random _random = new Random();
        private readonly Global _global;

        public bool IsAnimating => _activeHealthAnimations.Any() || _activeAlphaAnimations.Any() || _activeHealBounceAnimations.Any() || _activeHealFlashAnimations.Any() || _activePoisonEffectAnimations.Any() || _activeBarAnimations.Any();

        public BattleAnimationManager()
        {
            _global = ServiceLocator.Get<Global>();
        }

        public void Reset()
        {
            _activeHealthAnimations.Clear();
            _activeAlphaAnimations.Clear();
            _activeHitAnimations.Clear();
            _activeHealBounceAnimations.Clear();
            _activeHealFlashAnimations.Clear();
            _activePoisonEffectAnimations.Clear();
            _activeDamageIndicators.Clear();
            _activeAbilityIndicators.Clear();
            _activeBarAnimations.Clear();
        }

        public void StartHealthAnimation(string combatantId, int hpBefore, int hpAfter)
        {
            // Remove any existing health animation for this combatant to ensure the new one takes precedence.
            // This is crucial for multi-hit moves, where each hit should restart the animation.
            _activeHealthAnimations.RemoveAll(a => a.CombatantID == combatantId);

            _activeHealthAnimations.Add(new HealthAnimationState
            {
                CombatantID = combatantId,
                StartHP = hpBefore,
                TargetHP = hpAfter,
                Timer = 0f
            });
        }

        public void StartHealthLossAnimation(string combatantId, float hpBefore, float hpAfter)
        {
            _activeBarAnimations.RemoveAll(a => a.CombatantID == combatantId && a.ResourceType == ResourceBarAnimationState.BarResourceType.HP);
            _activeBarAnimations.Add(new ResourceBarAnimationState
            {
                CombatantID = combatantId,
                ResourceType = ResourceBarAnimationState.BarResourceType.HP,
                AnimationType = ResourceBarAnimationState.BarAnimationType.Loss,
                CurrentLossPhase = ResourceBarAnimationState.LossPhase.Preview,
                ValueBefore = hpBefore,
                ValueAfter = hpAfter,
                Timer = 0f
            });
        }

        public void StartManaLossAnimation(string combatantId, float manaBefore, float manaAfter)
        {
            _activeBarAnimations.RemoveAll(a => a.CombatantID == combatantId && a.ResourceType == ResourceBarAnimationState.BarResourceType.Mana);
            _activeBarAnimations.Add(new ResourceBarAnimationState
            {
                CombatantID = combatantId,
                ResourceType = ResourceBarAnimationState.BarResourceType.Mana,
                AnimationType = ResourceBarAnimationState.BarAnimationType.Loss,
                CurrentLossPhase = ResourceBarAnimationState.LossPhase.Preview,
                ValueBefore = manaBefore,
                ValueAfter = manaAfter,
                Timer = 0f
            });
        }

        public void StartHealthRecoveryAnimation(string combatantId, float hpBefore, float hpAfter)
        {
            _activeBarAnimations.RemoveAll(a => a.CombatantID == combatantId && a.ResourceType == ResourceBarAnimationState.BarResourceType.HP);
            _activeBarAnimations.Add(new ResourceBarAnimationState
            {
                CombatantID = combatantId,
                ResourceType = ResourceBarAnimationState.BarResourceType.HP,
                AnimationType = ResourceBarAnimationState.BarAnimationType.Recovery,
                ValueBefore = hpBefore,
                ValueAfter = hpAfter,
                Timer = 0f
            });
        }

        public void StartManaRecoveryAnimation(string combatantId, float manaBefore, float manaAfter)
        {
            _activeBarAnimations.RemoveAll(a => a.CombatantID == combatantId && a.ResourceType == ResourceBarAnimationState.BarResourceType.Mana);
            _activeBarAnimations.Add(new ResourceBarAnimationState
            {
                CombatantID = combatantId,
                ResourceType = ResourceBarAnimationState.BarResourceType.Mana,
                AnimationType = ResourceBarAnimationState.BarAnimationType.Recovery,
                ValueBefore = manaBefore,
                ValueAfter = manaAfter,
                Timer = 0f
            });
        }

        public void StartAlphaAnimation(string combatantId, float alphaBefore, float alphaAfter)
        {
            _activeAlphaAnimations.Add(new AlphaAnimationState
            {
                CombatantID = combatantId,
                StartAlpha = alphaBefore,
                TargetAlpha = alphaAfter,
                Timer = 0f
            });
        }

        public void StartHitAnimation(string combatantId)
        {
            _activeHitAnimations.RemoveAll(a => a.CombatantID == combatantId);
            _activeHitAnimations.Add(new HitAnimationState
            {
                CombatantID = combatantId,
                Timer = 0f
            });
        }

        public void StartHealBounceAnimation(string combatantId)
        {
            _activeHealBounceAnimations.RemoveAll(a => a.CombatantID == combatantId);
            _activeHealBounceAnimations.Add(new HealBounceAnimationState
            {
                CombatantID = combatantId,
                Timer = 0f
            });
        }

        public void StartHealFlashAnimation(string combatantId)
        {
            _activeHealFlashAnimations.RemoveAll(a => a.CombatantID == combatantId);
            _activeHealFlashAnimations.Add(new HealFlashAnimationState
            {
                CombatantID = combatantId,
                Timer = 0f
            });
        }

        public void StartPoisonEffectAnimation(string combatantId)
        {
            _activePoisonEffectAnimations.RemoveAll(a => a.CombatantID == combatantId);
            _activePoisonEffectAnimations.Add(new PoisonEffectAnimationState
            {
                CombatantID = combatantId,
                Timer = 0f
            });
        }

        public void StartAbilityIndicator(string abilityName)
        {
            var font = ServiceLocator.Get<Core>().SecondaryFont;
            string text = abilityName.ToUpper();

            // Check if an indicator for this ability already exists.
            var existingIndicator = _activeAbilityIndicators.FirstOrDefault(ind => ind.OriginalText == text);
            if (existingIndicator != null)
            {
                existingIndicator.Count++;
                existingIndicator.Text = $"{existingIndicator.OriginalText} x{existingIndicator.Count}";
                existingIndicator.Timer = 0f; // Reset timer to restart animation/duration
                existingIndicator.Phase = AbilityIndicatorState.AnimationPhase.EasingIn;
                existingIndicator.ShakeTimer = AbilityIndicatorState.SHAKE_DURATION;
                return;
            }

            Vector2 textSize = font.MeasureString(text);
            const int paddingY = 1;
            int boxHeight = (int)textSize.Y + paddingY * 2;

            var indicator = new AbilityIndicatorState
            {
                OriginalText = text,
                Text = text,
                Count = 1,
                Timer = 0f,
                Phase = AbilityIndicatorState.AnimationPhase.EasingIn,
                ShakeTimer = AbilityIndicatorState.SHAKE_DURATION
            };
            indicator.InitialPosition = new Vector2(Global.VIRTUAL_WIDTH / 2f, Global.VIRTUAL_HEIGHT + boxHeight);
            indicator.CurrentPosition = indicator.InitialPosition;

            _activeAbilityIndicators.Add(indicator);
        }

        public void StartDamageIndicator(string combatantId, string text, Vector2 startPosition, Color color)
        {
            _activeDamageIndicators.Add(new DamageIndicatorState
            {
                Type = DamageIndicatorState.IndicatorType.Text,
                CombatantID = combatantId,
                Text = text,
                Position = startPosition,
                InitialPosition = startPosition,
                Color = color,
                Timer = 0f
            });
        }

        public void StartEffectivenessIndicator(string combatantId, string text, Vector2 startPosition)
        {
            _activeDamageIndicators.Add(new DamageIndicatorState
            {
                Type = DamageIndicatorState.IndicatorType.Effectiveness,
                CombatantID = combatantId,
                Text = text,
                Position = startPosition,
                InitialPosition = startPosition,
                Timer = 0f
            });
        }

        public void StartDamageNumberIndicator(string combatantId, int damageAmount, Vector2 startPosition)
        {
            _activeDamageIndicators.Add(new DamageIndicatorState
            {
                Type = DamageIndicatorState.IndicatorType.Number,
                CombatantID = combatantId,
                Text = damageAmount.ToString(),
                Position = startPosition,
                Velocity = new Vector2((float)(_random.NextDouble() * 30 - 15), (float)(_random.NextDouble() * -20 - 20)), // Slower, floatier arc
                Timer = 0f
            });
        }

        public void StartEmphasizedDamageNumberIndicator(string combatantId, int damageAmount, Vector2 startPosition)
        {
            _activeDamageIndicators.Add(new DamageIndicatorState
            {
                Type = DamageIndicatorState.IndicatorType.EmphasizedNumber,
                CombatantID = combatantId,
                Text = damageAmount.ToString(),
                Position = startPosition,
                Velocity = new Vector2((float)(_random.NextDouble() * 30 - 15), (float)(_random.NextDouble() * -20 - 40)), // Higher initial upward velocity
                Timer = 0f
            });
        }

        public void StartHealNumberIndicator(string combatantId, int healAmount, Vector2 startPosition)
        {
            _activeDamageIndicators.Add(new DamageIndicatorState
            {
                Type = DamageIndicatorState.IndicatorType.HealNumber,
                CombatantID = combatantId,
                Text = healAmount.ToString(),
                Position = startPosition,
                Velocity = new Vector2((float)(_random.NextDouble() * 30 - 15), (float)(_random.NextDouble() * -20 - 20)),
                Timer = 0f
            });
        }

        public void SkipAllHealthAnimations(IEnumerable<BattleCombatant> combatants)
        {
            foreach (var anim in _activeHealthAnimations)
            {
                var combatant = combatants.FirstOrDefault(c => c.CombatantID == anim.CombatantID);
                if (combatant != null)
                {
                    combatant.VisualHP = anim.TargetHP;
                }
            }
            _activeHealthAnimations.Clear();
        }

        public void SkipAllBarAnimations()
        {
            _activeBarAnimations.Clear();
        }

        public HitAnimationState GetHitAnimationState(string combatantId)
        {
            return _activeHitAnimations.FirstOrDefault(a => a.CombatantID == combatantId);
        }

        public HealBounceAnimationState GetHealBounceAnimationState(string combatantId)
        {
            return _activeHealBounceAnimations.FirstOrDefault(a => a.CombatantID == combatantId);
        }

        public HealFlashAnimationState GetHealFlashAnimationState(string combatantId)
        {
            return _activeHealFlashAnimations.FirstOrDefault(a => a.CombatantID == combatantId);
        }

        public PoisonEffectAnimationState GetPoisonEffectAnimationState(string combatantId)
        {
            return _activePoisonEffectAnimations.FirstOrDefault(a => a.CombatantID == combatantId);
        }

        public void Update(GameTime gameTime, IEnumerable<BattleCombatant> combatants)
        {
            UpdateHealthAnimations(gameTime, combatants);
            UpdateAlphaAnimations(gameTime, combatants);
            UpdateHitAnimations(gameTime);
            UpdateHealAnimations(gameTime);
            UpdatePoisonEffectAnimations(gameTime);
            UpdateDamageIndicators(gameTime);
            UpdateAbilityIndicators(gameTime);
            UpdateBarAnimations(gameTime);
        }

        private void UpdateBarAnimations(GameTime gameTime)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            for (int i = _activeBarAnimations.Count - 1; i >= 0; i--)
            {
                var anim = _activeBarAnimations[i];
                anim.Timer += deltaTime;

                if (anim.AnimationType == ResourceBarAnimationState.BarAnimationType.Loss)
                {
                    switch (anim.CurrentLossPhase)
                    {
                        case ResourceBarAnimationState.LossPhase.Preview:
                            if (anim.Timer >= ResourceBarAnimationState.PREVIEW_DURATION)
                            {
                                anim.Timer = 0;
                                anim.CurrentLossPhase = ResourceBarAnimationState.LossPhase.FlashBlack;
                            }
                            break;
                        case ResourceBarAnimationState.LossPhase.FlashBlack:
                            if (anim.Timer >= ResourceBarAnimationState.FLASH_BLACK_DURATION)
                            {
                                anim.Timer = 0;
                                anim.CurrentLossPhase = ResourceBarAnimationState.LossPhase.FlashWhite;
                            }
                            break;
                        case ResourceBarAnimationState.LossPhase.FlashWhite:
                            if (anim.Timer >= ResourceBarAnimationState.FLASH_WHITE_DURATION)
                            {
                                anim.Timer = 0;
                                anim.CurrentLossPhase = ResourceBarAnimationState.LossPhase.Shrink;
                            }
                            break;
                        case ResourceBarAnimationState.LossPhase.Shrink:
                            if (anim.Timer >= ResourceBarAnimationState.SHRINK_DURATION)
                            {
                                _activeBarAnimations.RemoveAt(i);
                            }
                            break;
                    }
                }
                else // Recovery
                {
                    if (anim.Timer >= ResourceBarAnimationState.GHOST_FILL_DURATION)
                    {
                        _activeBarAnimations.RemoveAt(i);
                    }
                }
            }
        }

        private void UpdateHealthAnimations(GameTime gameTime, IEnumerable<BattleCombatant> combatants)
        {
            for (int i = _activeHealthAnimations.Count - 1; i >= 0; i--)
            {
                var anim = _activeHealthAnimations[i];
                var combatant = combatants.FirstOrDefault(c => c.CombatantID == anim.CombatantID);
                if (combatant == null)
                {
                    _activeHealthAnimations.RemoveAt(i);
                    continue;
                }

                anim.Timer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (anim.Timer >= HEALTH_ANIMATION_DURATION)
                {
                    combatant.VisualHP = anim.TargetHP;
                    _activeHealthAnimations.RemoveAt(i);
                }
                else
                {
                    float progress = anim.Timer / HEALTH_ANIMATION_DURATION;
                    combatant.VisualHP = MathHelper.Lerp(anim.StartHP, anim.TargetHP, Easing.EaseOutQuart(progress));
                }
            }
        }

        private void UpdateAlphaAnimations(GameTime gameTime, IEnumerable<BattleCombatant> combatants)
        {
            for (int i = _activeAlphaAnimations.Count - 1; i >= 0; i--)
            {
                var anim = _activeAlphaAnimations[i];
                var combatant = combatants.FirstOrDefault(c => c.CombatantID == anim.CombatantID);
                if (combatant == null)
                {
                    _activeAlphaAnimations.RemoveAt(i);
                    continue;
                }

                anim.Timer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (anim.Timer >= AlphaAnimationState.Duration)
                {
                    combatant.VisualAlpha = anim.TargetAlpha;
                    _activeAlphaAnimations.RemoveAt(i);
                }
                else
                {
                    float progress = anim.Timer / AlphaAnimationState.Duration;
                    combatant.VisualAlpha = MathHelper.Lerp(anim.StartAlpha, anim.TargetAlpha, Easing.EaseOutQuad(progress));
                }
            }
        }

        private void UpdateHitAnimations(GameTime gameTime)
        {
            for (int i = _activeHitAnimations.Count - 1; i >= 0; i--)
            {
                var anim = _activeHitAnimations[i];
                anim.Timer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (anim.Timer >= HitAnimationState.Duration)
                {
                    _activeHitAnimations.RemoveAt(i);
                }
            }
        }

        private void UpdateHealAnimations(GameTime gameTime)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            for (int i = _activeHealBounceAnimations.Count - 1; i >= 0; i--)
            {
                var anim = _activeHealBounceAnimations[i];
                anim.Timer += deltaTime;
                if (anim.Timer >= HealBounceAnimationState.Duration)
                {
                    _activeHealBounceAnimations.RemoveAt(i);
                }
            }
            for (int i = _activeHealFlashAnimations.Count - 1; i >= 0; i--)
            {
                var anim = _activeHealFlashAnimations[i];
                anim.Timer += deltaTime;
                if (anim.Timer >= HealFlashAnimationState.Duration)
                {
                    _activeHealFlashAnimations.RemoveAt(i);
                }
            }
        }

        private void UpdatePoisonEffectAnimations(GameTime gameTime)
        {
            for (int i = _activePoisonEffectAnimations.Count - 1; i >= 0; i--)
            {
                var anim = _activePoisonEffectAnimations[i];
                anim.Timer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (anim.Timer >= PoisonEffectAnimationState.Duration)
                {
                    _activePoisonEffectAnimations.RemoveAt(i);
                }
            }
        }

        private void UpdateDamageIndicators(GameTime gameTime)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            for (int i = _activeDamageIndicators.Count - 1; i >= 0; i--)
            {
                var indicator = _activeDamageIndicators[i];
                indicator.Timer += deltaTime;
                if (indicator.Timer >= DamageIndicatorState.DURATION)
                {
                    _activeDamageIndicators.RemoveAt(i);
                    continue;
                }

                // Apply physics or simple animation based on the indicator type
                if (indicator.Type == DamageIndicatorState.IndicatorType.Number || indicator.Type == DamageIndicatorState.IndicatorType.HealNumber || indicator.Type == DamageIndicatorState.IndicatorType.EmphasizedNumber)
                {
                    const float gravity = 80f; // Reduced gravity for a floatier effect
                    indicator.Velocity.Y += gravity * deltaTime;
                    indicator.Position += indicator.Velocity * deltaTime;
                }
                else if (indicator.Type == DamageIndicatorState.IndicatorType.Effectiveness)
                {
                    float progress = indicator.Timer / DamageIndicatorState.DURATION;
                    float yOffset = Easing.EaseOutQuad(progress) * DamageIndicatorState.RISE_DISTANCE; // Move down
                    indicator.Position = indicator.InitialPosition + new Vector2(0, yOffset);
                }
                else // Text indicators like GRAZE
                {
                    float progress = indicator.Timer / DamageIndicatorState.DURATION;
                    float yOffset = -Easing.EaseOutQuad(progress) * DamageIndicatorState.RISE_DISTANCE;
                    indicator.Position = indicator.InitialPosition + new Vector2(0, yOffset);
                }
            }
        }

        private void UpdateAbilityIndicators(GameTime gameTime)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            var font = ServiceLocator.Get<Core>().SecondaryFont;
            const int paddingY = 1;
            const int gap = 2;

            // Recalculate all target positions every frame for smooth stacking
            float yStackOffset = Global.VIRTUAL_HEIGHT;
            for (int i = 0; i < _activeAbilityIndicators.Count; i++)
            {
                var indicator = _activeAbilityIndicators[i];
                Vector2 textSize = font.MeasureString(indicator.Text);
                int boxHeight = (int)textSize.Y + paddingY * 2;
                yStackOffset -= boxHeight;
                indicator.TargetPosition = new Vector2(Global.VIRTUAL_WIDTH / 2f, yStackOffset);
                yStackOffset -= gap;
            }

            for (int i = _activeAbilityIndicators.Count - 1; i >= 0; i--)
            {
                var indicator = _activeAbilityIndicators[i];
                indicator.Timer += deltaTime;
                if (indicator.ShakeTimer > 0)
                {
                    indicator.ShakeTimer -= deltaTime;
                }

                if (indicator.Timer >= AbilityIndicatorState.TOTAL_DURATION)
                {
                    _activeAbilityIndicators.RemoveAt(i);
                    continue;
                }

                // Smoothly move towards the (potentially changing) target position
                indicator.CurrentPosition = Vector2.Lerp(indicator.CurrentPosition, indicator.TargetPosition, 10f * deltaTime);

                // Update animation phase based on timer
                if (indicator.Phase == AbilityIndicatorState.AnimationPhase.EasingIn && indicator.Timer >= AbilityIndicatorState.EASE_IN_DURATION)
                {
                    indicator.Phase = AbilityIndicatorState.AnimationPhase.Flashing;
                }
                else if (indicator.Phase == AbilityIndicatorState.AnimationPhase.Flashing && indicator.Timer >= AbilityIndicatorState.EASE_IN_DURATION + AbilityIndicatorState.FLASH_DURATION)
                {
                    indicator.Phase = AbilityIndicatorState.AnimationPhase.Holding;
                }
                else if (indicator.Phase == AbilityIndicatorState.AnimationPhase.Holding && indicator.Timer >= AbilityIndicatorState.EASE_IN_DURATION + AbilityIndicatorState.FLASH_DURATION + AbilityIndicatorState.HOLD_DURATION)
                {
                    indicator.Phase = AbilityIndicatorState.AnimationPhase.EasingOut;
                }
            }
        }

        public void DrawResourceBarAnimations(SpriteBatch spriteBatch, IEnumerable<BattleCombatant> allCombatants)
        {
            var pixel = ServiceLocator.Get<Texture2D>();

            foreach (var anim in _activeBarAnimations)
            {
                var combatant = allCombatants.FirstOrDefault(c => c.CombatantID == anim.CombatantID);
                if (combatant == null) continue;

                Rectangle barRect;
                float maxResource;

                if (combatant.IsPlayerControlled)
                {
                    const int barWidth = 60;
                    const int barPaddingX = 10;
                    const int hpBarY = 105 - 9;
                    const int manaBarY = hpBarY + 3;
                    float startX = Global.VIRTUAL_WIDTH - barPaddingX - barWidth;

                    if (anim.ResourceType == ResourceBarAnimationState.BarResourceType.HP)
                    {
                        barRect = new Rectangle((int)startX, hpBarY, barWidth, 2);
                        maxResource = combatant.Stats.MaxHP;
                    }
                    else // Mana
                    {
                        barRect = new Rectangle((int)startX, manaBarY, barWidth, 2);
                        maxResource = combatant.Stats.MaxMana;
                    }
                }
                else // Enemy
                {
                    if (anim.ResourceType != ResourceBarAnimationState.BarResourceType.HP) continue; // Enemies only have HP bars

                    var enemies = allCombatants.Where(c => !c.IsPlayerControlled).ToList();
                    int enemyIndex = enemies.FindIndex(e => e.CombatantID == combatant.CombatantID);
                    if (enemyIndex == -1) continue;

                    const int enemyAreaPadding = 20;
                    const int enemyHudY = 80;
                    int availableWidth = Global.VIRTUAL_WIDTH - (enemyAreaPadding * 2);
                    int slotWidth = availableWidth / enemies.Count;
                    var centerPosition = new Vector2(enemyAreaPadding + (enemyIndex * slotWidth) + (slotWidth / 2), enemyHudY);

                    const int barWidth = 40;
                    const int barHeight = 2;
                    barRect = new Rectangle((int)(centerPosition.X - barWidth / 2f), (int)(centerPosition.Y + 2), barWidth, barHeight);
                    maxResource = combatant.Stats.MaxHP;
                }

                if (maxResource <= 0) continue;

                // --- Shared Drawing Logic ---
                if (anim.AnimationType == ResourceBarAnimationState.BarAnimationType.Loss)
                {
                    float percentBefore = anim.ValueBefore / maxResource;
                    float percentAfter = anim.ValueAfter / maxResource;

                    int previewStartX = (int)(barRect.X + barRect.Width * percentAfter);
                    int previewWidth = (int)(barRect.Width * (percentBefore - percentAfter));
                    var previewRect = new Rectangle(previewStartX, barRect.Y, previewWidth, barRect.Height);

                    switch (anim.CurrentLossPhase)
                    {
                        case ResourceBarAnimationState.LossPhase.Preview:
                            Color previewColor;
                            if (anim.ResourceType == ResourceBarAnimationState.BarResourceType.HP)
                            {
                                previewColor = _global.Palette_Red;
                            }
                            else // Mana
                            {
                                previewColor = Color.White;
                            }
                            spriteBatch.DrawSnapped(pixel, previewRect, previewColor);
                            break;
                        case ResourceBarAnimationState.LossPhase.FlashBlack:
                            spriteBatch.DrawSnapped(pixel, previewRect, Color.Black);
                            break;
                        case ResourceBarAnimationState.LossPhase.FlashWhite:
                            spriteBatch.DrawSnapped(pixel, previewRect, Color.White);
                            break;
                        case ResourceBarAnimationState.LossPhase.Shrink:
                            float progress = anim.Timer / ResourceBarAnimationState.SHRINK_DURATION;
                            float easedProgress = Easing.EaseOutCubic(progress);
                            int shrinkingWidth = (int)(previewWidth * (1.0f - easedProgress));
                            var shrinkingRect = new Rectangle(previewRect.X, previewRect.Y, shrinkingWidth, previewRect.Height);

                            Color shrinkColor = (anim.ResourceType == ResourceBarAnimationState.BarResourceType.HP)
                                ? _global.Palette_Red
                                : Color.White;

                            spriteBatch.DrawSnapped(pixel, shrinkingRect, shrinkColor);
                            break;
                    }
                }
                else // Recovery
                {
                    float progress = anim.Timer / ResourceBarAnimationState.GHOST_FILL_DURATION;
                    float easedProgress = Easing.EaseOutCubic(progress);

                    float percentBefore = anim.ValueBefore / maxResource;
                    float percentAfter = anim.ValueAfter / maxResource;

                    float currentFillPercent = MathHelper.Lerp(percentBefore, percentAfter, easedProgress);

                    int ghostStartX = (int)(barRect.X + barRect.Width * percentBefore);
                    int ghostWidth = (int)(barRect.Width * (currentFillPercent - percentBefore));
                    var ghostRect = new Rectangle(ghostStartX, barRect.Y, ghostWidth, barRect.Height);

                    Color ghostColor = (anim.ResourceType == ResourceBarAnimationState.BarResourceType.HP) ? _global.Palette_LightGreen : _global.Palette_LightBlue;
                    float alpha = 1.0f - easedProgress; // Fade out as it fills

                    spriteBatch.DrawSnapped(pixel, ghostRect, ghostColor * alpha * 0.75f);
                }
            }
        }

        public void DrawDamageIndicators(SpriteBatch spriteBatch, BitmapFont font)
        {
            // Two-pass rendering: text first, then numbers on top.
            DrawIndicatorsOfType(spriteBatch, font, DamageIndicatorState.IndicatorType.Text);
            DrawIndicatorsOfType(spriteBatch, font, DamageIndicatorState.IndicatorType.Effectiveness);
            DrawIndicatorsOfType(spriteBatch, font, DamageIndicatorState.IndicatorType.Number);
            DrawIndicatorsOfType(spriteBatch, font, DamageIndicatorState.IndicatorType.HealNumber);
            DrawIndicatorsOfType(spriteBatch, font, DamageIndicatorState.IndicatorType.EmphasizedNumber);
        }

        private void DrawIndicatorsOfType(SpriteBatch spriteBatch, BitmapFont font, DamageIndicatorState.IndicatorType typeToDraw)
        {
            var defaultFont = ServiceLocator.Get<BitmapFont>();

            foreach (var indicator in _activeDamageIndicators)
            {
                if (indicator.Type != typeToDraw) continue;

                float progress = indicator.Timer / DamageIndicatorState.DURATION;

                float alpha = 1.0f;
                if (progress > 0.5f)
                {
                    float fadeProgress = (progress - 0.5f) * 2f;
                    alpha = 1.0f - Easing.EaseInQuad(fadeProgress);
                }

                Color drawColor;
                BitmapFont activeFont = font;

                if (indicator.Type == DamageIndicatorState.IndicatorType.EmphasizedNumber)
                {
                    activeFont = defaultFont;
                    const float flashInterval = 0.05f; // Faster flash
                    bool useRed = (int)(indicator.Timer / flashInterval) % 2 == 0;
                    drawColor = useRed ? _global.Palette_Red : _global.Palette_BrightWhite;
                }
                else if (indicator.Type == DamageIndicatorState.IndicatorType.Number)
                {
                    drawColor = _global.Palette_Red;
                }
                else if (indicator.Type == DamageIndicatorState.IndicatorType.HealNumber)
                {
                    const float flashInterval = 0.1f;
                    bool useLightGreen = (int)(indicator.Timer / flashInterval) % 2 == 0;
                    drawColor = useLightGreen ? _global.Palette_LightGreen : _global.Palette_DarkGreen;
                }
                else if (indicator.Type == DamageIndicatorState.IndicatorType.Effectiveness)
                {
                    const float flashInterval = 0.2f;
                    bool useAltColor = (int)(indicator.Timer / flashInterval) % 2 == 0;
                    switch (indicator.Text)
                    {
                        case "EFFECTIVE":
                            drawColor = useAltColor ? _global.Palette_LightBlue : _global.Palette_Yellow;
                            break;
                        case "RESISTED":
                            drawColor = useAltColor ? _global.Palette_Red : _global.Palette_White;
                            break;
                        case "IMMUNE":
                            drawColor = useAltColor ? _global.Palette_White : _global.Palette_LightGray;
                            break;
                        default:
                            drawColor = Color.White;
                            break;
                    }
                }
                else // Text indicator
                {
                    drawColor = indicator.Color;
                    if (indicator.Text == "GRAZE")
                    {
                        const float flashInterval = 0.2f;
                        bool useYellow = (int)(indicator.Timer / flashInterval) % 2 == 0;
                        drawColor = useYellow ? _global.Palette_Yellow : _global.Palette_LightBlue;
                    }
                }

                Vector2 textSize = activeFont.MeasureString(indicator.Text);
                Vector2 textPosition = indicator.Position - new Vector2(textSize.X / 2f, textSize.Y);

                spriteBatch.DrawStringOutlinedSnapped(activeFont, indicator.Text, textPosition, drawColor * alpha, Color.Black * alpha);
            }
        }

        public void DrawAbilityIndicators(SpriteBatch spriteBatch, BitmapFont font)
        {
            var pixel = ServiceLocator.Get<Texture2D>();
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;

            foreach (var indicator in _activeAbilityIndicators)
            {
                // --- Alpha Calculation ---
                float alpha = 1.0f;
                if (indicator.Phase == AbilityIndicatorState.AnimationPhase.EasingIn)
                {
                    alpha = Easing.EaseOutQuint(indicator.Timer / AbilityIndicatorState.EASE_IN_DURATION);
                }
                else if (indicator.Phase == AbilityIndicatorState.AnimationPhase.EasingOut)
                {
                    float easeOutStart = AbilityIndicatorState.EASE_IN_DURATION + AbilityIndicatorState.FLASH_DURATION + AbilityIndicatorState.HOLD_DURATION;
                    float fadeOutProgress = (indicator.Timer - easeOutStart) / AbilityIndicatorState.EASE_OUT_DURATION;
                    alpha = 1.0f - Easing.EaseInQuad(fadeOutProgress);
                }

                // --- Pulsing Flash Logic ---
                const float PULSE_SPEED = 15f;
                float pulse = (MathF.Sin(indicator.Timer * PULSE_SPEED) + 1f) / 2f; // Oscillates between 0.0 and 1.0

                // --- Color Determination ---
                Color pulseTargetColor;
                if (indicator.Count >= 5)
                {
                    pulseTargetColor = _global.Palette_Pink;
                }
                else if (indicator.Count == 4)
                {
                    pulseTargetColor = _global.Palette_Red;
                }
                else if (indicator.Count == 3)
                {
                    pulseTargetColor = _global.Palette_Orange;
                }
                else if (indicator.Count == 2)
                {
                    pulseTargetColor = _global.Palette_Yellow;
                }
                else // Count is 1
                {
                    pulseTargetColor = _global.Palette_Gray;
                }

                Color bgColor = Color.Lerp(_global.TerminalBg, pulseTargetColor, pulse);
                Color outlineColor = Color.Black;
                Color textColor = Color.White;

                // --- Final Transparency ---
                float finalDrawAlpha = alpha;

                // --- Shake Calculation ---
                Vector2 shakeOffset = Vector2.Zero;
                if (indicator.ShakeTimer > 0)
                {
                    float progress = 1.0f - (indicator.ShakeTimer / AbilityIndicatorState.SHAKE_DURATION);
                    float magnitude = AbilityIndicatorState.SHAKE_MAGNITUDE * (1.0f - Easing.EaseOutQuad(progress));
                    shakeOffset.X = MathF.Sin(indicator.Timer * AbilityIndicatorState.SHAKE_FREQUENCY) * magnitude;
                }

                // --- Layout Calculation ---
                Vector2 textSize = secondaryFont.MeasureString(indicator.Text);
                const int paddingX = 8;
                const int paddingY = 1;

                const int maxChars = 20;
                float maxWidth = secondaryFont.MeasureString(new string('W', maxChars)).Width;
                int boxWidth = (int)maxWidth + paddingX * 2;
                int boxHeight = (int)textSize.Y + paddingY * 2;

                var boxRect = new Rectangle(
                    (int)(indicator.CurrentPosition.X - boxWidth / 2f + shakeOffset.X),
                    (int)(indicator.CurrentPosition.Y - boxHeight + shakeOffset.Y),
                    boxWidth,
                    boxHeight
                );

                var textPosition = new Vector2(
                    boxRect.X + (boxWidth - textSize.X) / 2f,
                    boxRect.Y + paddingY - 1
                );

                // --- Drawing ---
                spriteBatch.DrawSnapped(pixel, boxRect, bgColor * 0.8f * finalDrawAlpha);
                spriteBatch.DrawLineSnapped(new Vector2(boxRect.Left, boxRect.Top), new Vector2(boxRect.Right, boxRect.Top), _global.Palette_White * finalDrawAlpha);
                spriteBatch.DrawLineSnapped(new Vector2(boxRect.Left, boxRect.Bottom), new Vector2(boxRect.Right, boxRect.Bottom), _global.Palette_White * finalDrawAlpha);
                spriteBatch.DrawLineSnapped(new Vector2(boxRect.Left, boxRect.Top), new Vector2(boxRect.Left, boxRect.Bottom), _global.Palette_White * finalDrawAlpha);
                spriteBatch.DrawLineSnapped(new Vector2(boxRect.Right, boxRect.Top), new Vector2(boxRect.Right, boxRect.Bottom), _global.Palette_White * finalDrawAlpha);

                spriteBatch.DrawStringOutlinedSnapped(secondaryFont, indicator.Text, textPosition, textColor * finalDrawAlpha, outlineColor * finalDrawAlpha);
            }
        }
    }
}
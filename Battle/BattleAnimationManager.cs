using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Scenes;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        public class DamageIndicatorState
        {
            public enum IndicatorType { Text, Number, HealNumber, EmphasizedNumber }
            public IndicatorType Type;
            public string CombatantID;
            public string Text;
            public Vector2 Position; // Current position
            public Vector2 Velocity; // For physics-based indicators
            public Vector2 InitialPosition; // For simple animation paths
            public Color Color; // Used for text indicators like "CRITICAL"
            public float Timer;
            public const float DURATION = 1.2f;
            public const float RISE_DISTANCE = 10f;
        }

        private readonly List<HealthAnimationState> _activeHealthAnimations = new List<HealthAnimationState>();
        private readonly List<AlphaAnimationState> _activeAlphaAnimations = new List<AlphaAnimationState>();
        private readonly List<HitAnimationState> _activeHitAnimations = new List<HitAnimationState>();
        private readonly List<HealBounceAnimationState> _activeHealBounceAnimations = new List<HealBounceAnimationState>();
        private readonly List<HealFlashAnimationState> _activeHealFlashAnimations = new List<HealFlashAnimationState>();
        private readonly List<DamageIndicatorState> _activeDamageIndicators = new List<DamageIndicatorState>();

        private readonly Random _random = new Random();
        private readonly Global _global;

        public bool IsAnimating => _activeHealthAnimations.Any() || _activeAlphaAnimations.Any() || _activeHealBounceAnimations.Any() || _activeHealFlashAnimations.Any();

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
            _activeDamageIndicators.Clear();
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

        public void Update(GameTime gameTime, IEnumerable<BattleCombatant> combatants)
        {
            UpdateHealthAnimations(gameTime, combatants);
            UpdateAlphaAnimations(gameTime, combatants);
            UpdateHitAnimations(gameTime);
            UpdateHealAnimations(gameTime);
            UpdateDamageIndicators(gameTime);
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
                else // Text indicators like GRAZE
                {
                    float progress = indicator.Timer / DamageIndicatorState.DURATION;
                    float yOffset = -Easing.EaseOutQuad(progress) * DamageIndicatorState.RISE_DISTANCE;
                    indicator.Position = indicator.InitialPosition + new Vector2(0, yOffset);
                }
            }
        }

        public void DrawDamageIndicators(SpriteBatch spriteBatch, BitmapFont font)
        {
            // Two-pass rendering: text first, then numbers on top.
            DrawIndicatorsOfType(spriteBatch, font, DamageIndicatorState.IndicatorType.Text);
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
    }
}

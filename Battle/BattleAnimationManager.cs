using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
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
        // Internal animation state structs
        public class HealthAnimationState { public string CombatantID; public float StartHP; public float TargetHP; public float Timer; public const float Duration = 1.0f; }
        public class AlphaAnimationState { public string CombatantID; public float StartAlpha; public float TargetAlpha; public float Timer; public const float Duration = 0.167f; }
        public class HitAnimationState { public string CombatantID; public float Timer; public const float Duration = 1.0f; }
        public class DamageIndicatorState { public string CombatantID; public string Text; public Vector2 StartPosition; public Color Color; public float Timer; public const float DURATION = 1.2f; public const float RISE_DISTANCE = 10f; }

        private readonly List<HealthAnimationState> _activeHealthAnimations = new List<HealthAnimationState>();
        private readonly List<AlphaAnimationState> _activeAlphaAnimations = new List<AlphaAnimationState>();
        private readonly List<HitAnimationState> _activeHitAnimations = new List<HitAnimationState>();
        private readonly List<DamageIndicatorState> _activeDamageIndicators = new List<DamageIndicatorState>();

        private readonly Random _random = new Random();
        private readonly Global _global;

        public bool IsAnimating => _activeHealthAnimations.Any() || _activeAlphaAnimations.Any();

        public BattleAnimationManager()
        {
            _global = ServiceLocator.Get<Global>();
        }

        public void Reset()
        {
            _activeHealthAnimations.Clear();
            _activeAlphaAnimations.Clear();
            _activeHitAnimations.Clear();
            _activeDamageIndicators.Clear();
        }

        public void StartHealthAnimation(string combatantId, int hpBefore, int hpAfter)
        {
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

        public void StartDamageIndicator(string combatantId, string text, Vector2 startPosition, Color color)
        {
            _activeDamageIndicators.Add(new DamageIndicatorState
            {
                CombatantID = combatantId,
                Text = text,
                StartPosition = startPosition,
                Color = color,
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

        public void Update(GameTime gameTime, IEnumerable<BattleCombatant> combatants)
        {
            UpdateHealthAnimations(gameTime, combatants);
            UpdateAlphaAnimations(gameTime, combatants);
            UpdateHitAnimations(gameTime);
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
                if (anim.Timer >= HealthAnimationState.Duration)
                {
                    combatant.VisualHP = anim.TargetHP;
                    _activeHealthAnimations.RemoveAt(i);
                }
                else
                {
                    float progress = anim.Timer / HealthAnimationState.Duration;
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

        private void UpdateDamageIndicators(GameTime gameTime)
        {
            for (int i = _activeDamageIndicators.Count - 1; i >= 0; i--)
            {
                var indicator = _activeDamageIndicators[i];
                indicator.Timer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (indicator.Timer >= DamageIndicatorState.DURATION)
                {
                    _activeDamageIndicators.RemoveAt(i);
                }
            }
        }

        public void DrawDamageIndicators(SpriteBatch spriteBatch, BitmapFont font)
        {
            foreach (var indicator in _activeDamageIndicators)
            {
                float progress = indicator.Timer / DamageIndicatorState.DURATION;

                float yOffset = -Easing.EaseOutQuad(progress) * DamageIndicatorState.RISE_DISTANCE;
                Vector2 position = indicator.StartPosition + new Vector2(0, yOffset);

                float alpha = 1.0f;
                if (progress > 0.5f)
                {
                    float fadeProgress = (progress - 0.5f) * 2f;
                    alpha = 1.0f - Easing.EaseInQuad(fadeProgress);
                }

                Color drawColor = indicator.Color;
                if (indicator.Text == "GRAZE")
                {
                    const float flashInterval = 0.2f;
                    bool useYellow = (int)(indicator.Timer / flashInterval) % 2 == 0;
                    drawColor = useYellow ? _global.Palette_Yellow : _global.Palette_LightBlue;
                }

                Vector2 textSize = font.MeasureString(indicator.Text);
                Vector2 textPosition = position - new Vector2(textSize.X / 2f, textSize.Y);

                spriteBatch.DrawStringSnapped(font, indicator.Text, textPosition, drawColor * alpha);
            }
        }
    }
}
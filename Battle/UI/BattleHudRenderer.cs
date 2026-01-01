using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Battle.UI
{
    /// <summary>
    /// Handles drawing UI elements attached to combatants: Health bars, Mana bars, Names, and Status Icons.
    /// </summary>
    public class BattleHudRenderer
    {
        private readonly SpriteManager _spriteManager;
        private readonly Global _global;
        private readonly Texture2D _pixel;

        public BattleHudRenderer()
        {
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _global = ServiceLocator.Get<Global>();
            _pixel = ServiceLocator.Get<Texture2D>();
        }

        public void DrawEnemyBars(SpriteBatch spriteBatch, BattleCombatant combatant, float barX, float barY, int barWidth, int barHeight, BattleAnimationManager animationManager, float hpAlpha, float manaAlpha, GameTime gameTime)
        {
            // --- HEALTH BAR ---
            var barRect = new Rectangle((int)barX, (int)barY, barWidth, barHeight);
            float hpPercent = combatant.Stats.MaxHP > 0 ? Math.Clamp(combatant.VisualHP / combatant.Stats.MaxHP, 0f, 1f) : 0f;
            var hpAnim = animationManager.GetResourceBarAnimation(combatant.CombatantID, BattleAnimationManager.ResourceBarAnimationState.BarResourceType.HP);

            DrawBar(spriteBatch, barRect, hpPercent, _global.Palette_DarkGray, _global.Palette_LightGreen, _global.Palette_Black, hpAlpha, hpAnim, combatant.Stats.MaxHP);

            // --- MANA BAR ---
            float manaBarY = barY + barHeight + 1;
            var manaRect = new Rectangle((int)barX, (int)manaBarY, barWidth, 1);
            float manaPercent = combatant.Stats.MaxMana > 0 ? Math.Clamp((float)combatant.Stats.CurrentMana / combatant.Stats.MaxMana, 0f, 1f) : 0f;
            var manaAnim = animationManager.GetResourceBarAnimation(combatant.CombatantID, BattleAnimationManager.ResourceBarAnimationState.BarResourceType.Mana);

            DrawBar(spriteBatch, manaRect, manaPercent, _global.Palette_DarkGray, _global.Palette_LightBlue, _global.Palette_Black, manaAlpha, manaAnim, combatant.Stats.MaxMana);
        }

        private void DrawBar(SpriteBatch spriteBatch, Rectangle fullBarRect, float fillPercent, Color bgColor, Color fgColor, Color borderColor, float alpha, BattleAnimationManager.ResourceBarAnimationState? anim, float maxResource)
        {
            if (alpha <= 0.01f) return;

            int currentX = fullBarRect.X;
            int y = fullBarRect.Y;
            int w = fullBarRect.Width;
            int h = fullBarRect.Height;

            // Background
            spriteBatch.DrawSnapped(_pixel, new Rectangle(currentX, y, w, h), bgColor * alpha);

            // Foreground
            int totalFgWidth = (int)(w * fillPercent);
            if (fillPercent > 0 && totalFgWidth == 0) totalFgWidth = 1;

            if (totalFgWidth > 0)
            {
                spriteBatch.DrawSnapped(_pixel, new Rectangle(currentX, y, totalFgWidth, h), fgColor * alpha);
            }

            // Outline
            // Top
            spriteBatch.DrawSnapped(_pixel, new Rectangle(currentX, y - 1, w, 1), borderColor * alpha);
            // Bottom
            spriteBatch.DrawSnapped(_pixel, new Rectangle(currentX, y + h, w, 1), borderColor * alpha);
            // Left
            spriteBatch.DrawSnapped(_pixel, new Rectangle(currentX - 1, y - 1, 1, h + 2), borderColor * alpha);
            // Right
            spriteBatch.DrawSnapped(_pixel, new Rectangle(currentX + w, y - 1, 1, h + 2), borderColor * alpha);

            // Animation Overlay
            if (anim != null)
            {
                DrawBarAnimationOverlay(spriteBatch, fullBarRect, maxResource, anim, alpha);
            }
        }

        public void DrawPlayerBars(SpriteBatch spriteBatch, BattleCombatant player, float barX, float barY, int barWidth, int barHeight, BattleAnimationManager animationManager, float hpAlpha, float manaAlpha, GameTime gameTime, BattleUIManager uiManager, bool isActiveActor)
        {
            // Reuse Enemy Bar logic for base drawing, then add overlays
            DrawEnemyBars(spriteBatch, player, barX, barY, barWidth, barHeight, animationManager, hpAlpha, manaAlpha, gameTime);

            // --- MANA COST PREVIEW ---
            if (isActiveActor && uiManager.HoveredMove != null && manaAlpha > 0.01f)
            {
                // Don't draw preview if collapsing or holding white or expanding
                if (player.ManaBarDisappearTimer > 0 || player.ManaBarDelayTimer > 0) return;

                var move = uiManager.HoveredMove;
                bool isManaDump = move.Abilities.Any(a => a is ManaDumpAbility);
                int cost = move.ManaCost;

                if (isManaDump)
                {
                    cost = player.Stats.CurrentMana;
                }

                if (cost > 0)
                {
                    float manaBarY = barY + barHeight + 1;

                    // Calculate widths
                    float currentPercent = player.Stats.MaxMana > 0 ? Math.Clamp((float)player.Stats.CurrentMana / player.Stats.MaxMana, 0f, 1f) : 0f;
                    int currentWidth = (int)(barWidth * currentPercent);

                    float costPercent = (float)cost / player.Stats.MaxMana;
                    int costWidth = (int)(barWidth * costPercent);

                    // Determine if affordable
                    bool affordable = player.Stats.CurrentMana >= cost;

                    Rectangle previewRect;
                    Color previewColor;

                    if (affordable)
                    {
                        // Draw at the end of the current bar
                        int previewX = (int)barX + currentWidth - costWidth;
                        // Clamp
                        if (previewX < (int)barX) previewX = (int)barX;

                        previewRect = new Rectangle(previewX, (int)manaBarY, costWidth, 1);

                        // Pulse Color
                        float pulse = (MathF.Sin(uiManager.SharedPulseTimer * 4f) + 1f) / 2f;
                        previewColor = Color.Lerp(_global.Palette_Yellow, _global.Palette_BrightWhite, pulse);
                    }
                    else
                    {
                        // Draw over the whole current bar (or required amount) in red
                        previewRect = new Rectangle((int)barX, (int)manaBarY, currentWidth, 1);
                        previewColor = _global.Palette_Red;
                    }

                    spriteBatch.DrawSnapped(_pixel, previewRect, previewColor * manaAlpha);
                }
            }
        }

        public void DrawStatusIcons(SpriteBatch spriteBatch, BattleCombatant combatant, float startX, float startY, int width, bool isPlayer, List<StatusIconInfo> iconTracker, Func<string, StatusEffectType, float> getOffsetFunc, Func<string, StatusEffectType, bool> isAnimatingFunc)
        {
            if (!combatant.ActiveStatusEffects.Any()) return;

            iconTracker?.Clear();

            int iconSize = BattleLayout.STATUS_ICON_SIZE;
            int gap = BattleLayout.STATUS_ICON_GAP;
            int iconY = (int)startY - iconSize - 2;
            int currentX = (int)startX;
            int step = iconSize + gap;

            // For player slot 1 (right side), icons grow leftwards
            if (isPlayer && combatant.BattleSlot == 1)
            {
                currentX = (int)(startX + width - iconSize);
                step = -(iconSize + gap);
            }

            foreach (var effect in combatant.ActiveStatusEffects)
            {
                float hopOffset = getOffsetFunc(combatant.CombatantID, effect.EffectType);
                bool isAnimating = isAnimatingFunc(combatant.CombatantID, effect.EffectType);

                var iconBounds = new Rectangle(currentX, (int)(iconY + hopOffset), iconSize, iconSize);

                if (iconTracker != null)
                {
                    iconTracker.Add(new StatusIconInfo { Effect = effect, Bounds = iconBounds });
                }

                var iconTexture = _spriteManager.GetStatusEffectIcon(effect.EffectType);
                spriteBatch.DrawSnapped(iconTexture, iconBounds, Color.White);

                if (isAnimating)
                {
                    // Simple additive flash simulation by drawing again
                    spriteBatch.DrawSnapped(iconTexture, iconBounds, Color.White * 0.5f);
                }

                currentX += step;
            }
        }

        private void DrawBarAnimationOverlay(SpriteBatch spriteBatch, Rectangle bgRect, float maxResource, BattleAnimationManager.ResourceBarAnimationState anim, float alpha)
        {
            float percentBefore = anim.ValueBefore / maxResource;
            float percentAfter = anim.ValueAfter / maxResource;

            int widthBefore = (int)(bgRect.Width * percentBefore);
            int widthAfter = (int)(bgRect.Width * percentAfter);

            // FIX: Ensure we respect the minimum 1-pixel width for remaining health
            if (percentAfter > 0 && widthAfter == 0) widthAfter = 1;
            if (percentBefore > 0 && widthBefore == 0) widthBefore = 1;

            // Define the visible area of the bar (in screen space)
            int visibleStartX = bgRect.X;
            int visibleWidth = bgRect.Width;
            int visibleEndX = visibleStartX + visibleWidth;

            if (visibleWidth <= 0) return;

            if (anim.AnimationType == BattleAnimationManager.ResourceBarAnimationState.BarAnimationType.Loss)
            {
                int previewStartX = bgRect.X + widthAfter;
                int previewWidth = widthBefore - widthAfter;

                Color color = Color.Transparent;
                switch (anim.CurrentLossPhase)
                {
                    case BattleAnimationManager.ResourceBarAnimationState.LossPhase.Preview:
                        color = (anim.ResourceType == BattleAnimationManager.ResourceBarAnimationState.BarResourceType.HP) ? _global.Palette_Red : Color.White;
                        break;
                    case BattleAnimationManager.ResourceBarAnimationState.LossPhase.FlashBlack:
                        color = Color.Black;
                        break;
                    case BattleAnimationManager.ResourceBarAnimationState.LossPhase.FlashWhite:
                        color = Color.White;
                        break;
                    case BattleAnimationManager.ResourceBarAnimationState.LossPhase.Shrink:
                        float progress = anim.Timer / BattleAnimationManager.ResourceBarAnimationState.SHRINK_DURATION;
                        float eased = Easing.EaseOutCubic(progress);
                        previewWidth = (int)(previewWidth * (1.0f - eased));
                        color = (anim.ResourceType == BattleAnimationManager.ResourceBarAnimationState.BarResourceType.HP) ? _global.Palette_Red : _global.Palette_White;
                        break;
                }

                if (color != Color.Transparent && previewWidth > 0)
                {
                    // Clip against the visible area
                    int drawStartX = Math.Max(previewStartX, visibleStartX);
                    int drawEndX = Math.Min(previewStartX + previewWidth, visibleEndX);
                    int drawWidth = drawEndX - drawStartX;

                    if (drawWidth > 0)
                    {
                        var previewRect = new Rectangle(drawStartX, bgRect.Y, drawWidth, bgRect.Height);
                        spriteBatch.DrawSnapped(_pixel, previewRect, color * alpha);
                    }
                }
            }
            else // Recovery
            {
                int wBefore = (int)(bgRect.Width * percentBefore);
                int wAfter = (int)(bgRect.Width * percentAfter);

                if (percentBefore > 0 && wBefore == 0) wBefore = 1;
                if (percentAfter > 0 && wAfter == 0) wAfter = 1;

                int ghostStartX = bgRect.X + wBefore;
                int ghostWidth = wAfter - wBefore;

                if (ghostWidth > 0)
                {
                    // Clip
                    int drawStartX = Math.Max(ghostStartX, visibleStartX);
                    int drawEndX = Math.Min(ghostStartX + ghostWidth, visibleEndX);
                    int drawWidth = drawEndX - drawStartX;

                    if (drawWidth > 0)
                    {
                        var ghostRect = new Rectangle(drawStartX, bgRect.Y, drawWidth, bgRect.Height);

                        Color ghostColor = (anim.ResourceType == BattleAnimationManager.ResourceBarAnimationState.BarResourceType.HP)
                            ? _global.HealOverlayColor
                            : _global.ManaOverlayColor;

                        float overlayAlpha = alpha;
                        if (anim.CurrentRecoveryPhase == BattleAnimationManager.ResourceBarAnimationState.RecoveryPhase.Fade)
                        {
                            float progress = anim.Timer / _global.HealOverlayFadeDuration;
                            overlayAlpha *= (1.0f - progress);
                        }

                        spriteBatch.DrawSnapped(_pixel, ghostRect, ghostColor * overlayAlpha);
                    }
                }
            }
        }
    }
}
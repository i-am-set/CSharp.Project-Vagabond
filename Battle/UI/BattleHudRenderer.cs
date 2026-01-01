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
            int hpCrop = 0;
            if (combatant.HealthBarDisappearTimer > 0)
            {
                float progress = Math.Clamp(combatant.HealthBarDisappearTimer / BattleCombatant.BAR_DISAPPEAR_DURATION, 0f, 1f);
                float eased = Easing.EaseInCubic(progress);
                hpCrop = (int)(barWidth * eased);
            }

            if (hpCrop < barWidth)
            {
                var barRect = new Rectangle((int)barX, (int)barY, barWidth, barHeight);
                float hpPercent = combatant.Stats.MaxHP > 0 ? Math.Clamp(combatant.VisualHP / combatant.Stats.MaxHP, 0f, 1f) : 0f;
                var hpAnim = animationManager.GetResourceBarAnimation(combatant.CombatantID, BattleAnimationManager.ResourceBarAnimationState.BarResourceType.HP);

                DrawClippedBar(spriteBatch, barRect, hpPercent, _global.Palette_DarkGray, _global.Palette_LightGreen, _global.Palette_Black, hpAlpha, hpCrop, hpAnim, combatant.Stats.MaxHP);
            }

            // --- MANA BAR ---
            float manaBarY = barY + barHeight + 1;
            int manaCrop = 0;
            if (combatant.ManaBarDisappearTimer > 0)
            {
                float progress = Math.Clamp(combatant.ManaBarDisappearTimer / BattleCombatant.BAR_DISAPPEAR_DURATION, 0f, 1f);
                float eased = Easing.EaseInCubic(progress);
                manaCrop = (int)(barWidth * eased);
            }

            if (manaCrop < barWidth)
            {
                var manaRect = new Rectangle((int)barX, (int)manaBarY, barWidth, 1);
                float manaPercent = combatant.Stats.MaxMana > 0 ? Math.Clamp((float)combatant.Stats.CurrentMana / combatant.Stats.MaxMana, 0f, 1f) : 0f;
                var manaAnim = animationManager.GetResourceBarAnimation(combatant.CombatantID, BattleAnimationManager.ResourceBarAnimationState.BarResourceType.Mana);

                DrawClippedBar(spriteBatch, manaRect, manaPercent, _global.Palette_DarkGray, _global.Palette_LightBlue, _global.Palette_Black, manaAlpha, manaCrop, manaAnim, combatant.Stats.MaxMana);
            }
        }

        private void DrawClippedBar(SpriteBatch spriteBatch, Rectangle fullBarRect, float fillPercent, Color bgColor, Color fgColor, Color borderColor, float alpha, int cropOffset, BattleAnimationManager.ResourceBarAnimationState? anim, float maxResource)
        {
            if (cropOffset >= fullBarRect.Width) return;

            int visibleWidth = fullBarRect.Width - cropOffset;
            int currentX = fullBarRect.X + cropOffset;
            int y = fullBarRect.Y;
            int h = fullBarRect.Height;

            // Background
            spriteBatch.DrawSnapped(_pixel, new Rectangle(currentX, y, visibleWidth, h), bgColor * alpha);

            // Foreground
            int totalFgWidth = (int)(fullBarRect.Width * fillPercent);
            if (fillPercent > 0 && totalFgWidth == 0) totalFgWidth = 1;

            if (cropOffset < totalFgWidth)
            {
                int visibleFgWidth = totalFgWidth - cropOffset;
                spriteBatch.DrawSnapped(_pixel, new Rectangle(currentX, y, visibleFgWidth, h), fgColor * alpha);
            }

            // Outline
            // Top
            spriteBatch.DrawSnapped(_pixel, new Rectangle(currentX, y - 1, visibleWidth, 1), borderColor * alpha);
            // Bottom
            spriteBatch.DrawSnapped(_pixel, new Rectangle(currentX, y + h, visibleWidth, 1), borderColor * alpha);
            // Right (Always visible if bar is visible)
            spriteBatch.DrawSnapped(_pixel, new Rectangle(fullBarRect.Right, y - 1, 1, h + 2), borderColor * alpha);
            // Left (Only if not cropped)
            if (cropOffset == 0)
            {
                spriteBatch.DrawSnapped(_pixel, new Rectangle(fullBarRect.X - 1, y - 1, 1, h + 2), borderColor * alpha);
            }

            // Animation Overlay
            if (anim != null)
            {
                DrawBarAnimationOverlay(spriteBatch, fullBarRect, maxResource, anim, cropOffset);
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

        private void DrawBarAnimationOverlay(SpriteBatch spriteBatch, Rectangle bgRect, float maxResource, BattleAnimationManager.ResourceBarAnimationState anim, int cropOffset)
        {
            float percentBefore = anim.ValueBefore / maxResource;
            float percentAfter = anim.ValueAfter / maxResource;

            int widthBefore = (int)(bgRect.Width * percentBefore);
            int widthAfter = (int)(bgRect.Width * percentAfter);

            // Define the visible area of the bar (in screen space)
            int visibleStartX = bgRect.X + cropOffset;
            int visibleEndX = bgRect.Right;

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
                    // Clip
                    int drawStartX = Math.Max(previewStartX, visibleStartX);
                    int drawEndX = Math.Min(previewStartX + previewWidth, visibleEndX);
                    int drawWidth = drawEndX - drawStartX;

                    if (drawWidth > 0)
                    {
                        var previewRect = new Rectangle(drawStartX, bgRect.Y, drawWidth, bgRect.Height);
                        spriteBatch.DrawSnapped(_pixel, previewRect, color);
                    }
                }
            }
            else // Recovery
            {
                // Calculate widths based on integer truncation to match the main bar's rendering logic
                // This ensures the overlay aligns perfectly with the filled segments
                int wBefore = (int)(bgRect.Width * percentBefore);
                int wAfter = (int)(bgRect.Width * percentAfter);

                // Match the "at least 1 pixel" logic from DrawClippedBar
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

                        float alpha = 1.0f;
                        if (anim.CurrentRecoveryPhase == BattleAnimationManager.ResourceBarAnimationState.RecoveryPhase.Fade)
                        {
                            float progress = anim.Timer / _global.HealOverlayFadeDuration;
                            alpha = 1.0f - progress;
                        }

                        spriteBatch.DrawSnapped(_pixel, ghostRect, ghostColor * alpha);
                    }
                }
            }
        }
    }
}
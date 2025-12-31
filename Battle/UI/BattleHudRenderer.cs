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
            if (combatant.HealthBarDisappearTimer > 0)
            {
                // Draw Collapse Animation
                float progress = Math.Clamp(combatant.HealthBarDisappearTimer / BattleCombatant.BAR_DISAPPEAR_DURATION, 0f, 1f);
                float scale = 1.0f - Easing.EaseInCirc(progress);
                int currentWidth = (int)(barWidth * scale);
                int centerX = (int)(barX + barWidth / 2f);
                int drawX = centerX - (currentWidth / 2);

                if (currentWidth > 0)
                {
                    spriteBatch.DrawSnapped(_pixel, new Rectangle(drawX, (int)barY, currentWidth, barHeight), Color.White);
                }
            }
            else if (combatant.HealthBarWhiteHoldTimer > 0)
            {
                // Draw White Hold (Solid White Bar covering the entire width)
                var barRect = new Rectangle((int)barX, (int)barY, barWidth, barHeight);

                // Draw Outline
                var hpBorderRect = new Rectangle(barRect.X - 1, barRect.Y - 1, barRect.Width + 2, barRect.Height + 2);
                DrawRectangleBorder(spriteBatch, hpBorderRect, _global.Palette_Black * hpAlpha);

                // Draw Full White Fill
                spriteBatch.DrawSnapped(_pixel, barRect, Color.White * hpAlpha);
            }
            else if (combatant.HealthBarWhiteExpandTimer > 0)
            {
                // Draw Normal Bar underneath
                var barRect = new Rectangle((int)barX, (int)barY, barWidth, barHeight);

                // Draw Outline
                var hpBorderRect = new Rectangle(barRect.X - 1, barRect.Y - 1, barRect.Width + 2, barRect.Height + 2);
                DrawRectangleBorder(spriteBatch, hpBorderRect, _global.Palette_Black * hpAlpha);

                float hpPercent = combatant.Stats.MaxHP > 0 ? Math.Clamp(combatant.VisualHP / combatant.Stats.MaxHP, 0f, 1f) : 0f;
                int fgWidth = (int)(barRect.Width * hpPercent);
                if (hpPercent > 0 && fgWidth == 0) fgWidth = 1;

                var hpFgRect = new Rectangle(barRect.X, barRect.Y, fgWidth, barRect.Height);

                spriteBatch.DrawSnapped(_pixel, barRect, _global.Palette_DarkGray * hpAlpha);
                spriteBatch.DrawSnapped(_pixel, hpFgRect, _global.Palette_LightGreen * hpAlpha);

                // Draw Expanding White Overlay
                float progress = Math.Clamp(combatant.HealthBarWhiteExpandTimer / BattleCombatant.BAR_WHITE_EXPAND_DURATION, 0f, 1f);
                float eased = Easing.EaseOutCubic(progress);
                int whiteWidth = (int)(barWidth * eased);
                int centerX = (int)(barX + barWidth / 2f);
                int drawX = centerX - (whiteWidth / 2);

                if (whiteWidth > 0)
                {
                    spriteBatch.DrawSnapped(_pixel, new Rectangle(drawX, (int)barY, whiteWidth, barHeight), Color.White);
                }
            }
            else
            {
                var barRect = new Rectangle((int)barX, (int)barY, barWidth, barHeight);

                // Draw Outline
                var hpBorderRect = new Rectangle(barRect.X - 1, barRect.Y - 1, barRect.Width + 2, barRect.Height + 2);
                DrawRectangleBorder(spriteBatch, hpBorderRect, _global.Palette_Black * hpAlpha);

                float hpPercent = combatant.Stats.MaxHP > 0 ? Math.Clamp(combatant.VisualHP / combatant.Stats.MaxHP, 0f, 1f) : 0f;
                int fgWidth = (int)(barRect.Width * hpPercent);
                if (hpPercent > 0 && fgWidth == 0) fgWidth = 1;

                var hpFgRect = new Rectangle(barRect.X, barRect.Y, fgWidth, barRect.Height);

                spriteBatch.DrawSnapped(_pixel, barRect, _global.Palette_DarkGray * hpAlpha);
                spriteBatch.DrawSnapped(_pixel, hpFgRect, _global.Palette_LightGreen * hpAlpha);

                var hpAnim = animationManager.GetResourceBarAnimation(combatant.CombatantID, BattleAnimationManager.ResourceBarAnimationState.BarResourceType.HP);
                if (hpAnim != null)
                {
                    DrawBarAnimationOverlay(spriteBatch, barRect, combatant.Stats.MaxHP, hpAnim);
                }
            }

            // --- MANA BAR ---
            float manaBarY = barY + barHeight + 1;

            if (combatant.ManaBarDisappearTimer > 0)
            {
                // Draw Collapse Animation
                float progress = Math.Clamp(combatant.ManaBarDisappearTimer / BattleCombatant.BAR_DISAPPEAR_DURATION, 0f, 1f);
                float scale = 1.0f - Easing.EaseInCirc(progress);
                int currentWidth = (int)(barWidth * scale);
                int centerX = (int)(barX + barWidth / 2f);
                int drawX = centerX - (currentWidth / 2);

                if (currentWidth > 0)
                {
                    // Mana bar is 1px high
                    spriteBatch.DrawSnapped(_pixel, new Rectangle(drawX, (int)manaBarY, currentWidth, 1), Color.White);
                }
            }
            else if (combatant.ManaBarWhiteHoldTimer > 0)
            {
                // Draw White Hold (Solid White Bar covering entire width)
                var manaRect = new Rectangle((int)barX, (int)manaBarY, barWidth, 1);

                // Draw Outline
                var manaBorderRect = new Rectangle(manaRect.X - 1, manaRect.Y - 1, manaRect.Width + 2, manaRect.Height + 2);
                DrawRectangleBorder(spriteBatch, manaBorderRect, _global.Palette_Black * manaAlpha);

                // Draw Full White Fill
                spriteBatch.DrawSnapped(_pixel, manaRect, Color.White * manaAlpha);
            }
            else if (combatant.ManaBarWhiteExpandTimer > 0)
            {
                // Draw Normal Bar underneath
                var manaRect = new Rectangle((int)barX, (int)manaBarY, barWidth, 1);

                // Draw Outline
                var manaBorderRect = new Rectangle(manaRect.X - 1, manaRect.Y - 1, manaRect.Width + 2, manaRect.Height + 2);
                DrawRectangleBorder(spriteBatch, manaBorderRect, _global.Palette_Black * manaAlpha);

                float manaPercent = combatant.Stats.MaxMana > 0 ? Math.Clamp((float)combatant.Stats.CurrentMana / combatant.Stats.MaxMana, 0f, 1f) : 0f;
                int manaFgWidth = (int)(manaRect.Width * manaPercent);
                if (manaPercent > 0 && manaFgWidth == 0) manaFgWidth = 1;

                var manaFgRect = new Rectangle(manaRect.X, manaRect.Y, manaFgWidth, manaRect.Height);

                spriteBatch.DrawSnapped(_pixel, manaRect, _global.Palette_DarkGray * manaAlpha);

                if (_spriteManager.ManaBarPattern != null)
                {
                    spriteBatch.DrawSnapped(_pixel, manaFgRect, _global.Palette_LightBlue * manaAlpha);
                }
                else
                {
                    spriteBatch.DrawSnapped(_pixel, manaFgRect, _global.Palette_LightBlue * manaAlpha);
                }

                // Draw Expanding White Overlay
                float progress = Math.Clamp(combatant.ManaBarWhiteExpandTimer / BattleCombatant.BAR_WHITE_EXPAND_DURATION, 0f, 1f);
                float eased = Easing.EaseOutCubic(progress);
                int whiteWidth = (int)(barWidth * eased);
                int centerX = (int)(barX + barWidth / 2f);
                int drawX = centerX - (whiteWidth / 2);

                if (whiteWidth > 0)
                {
                    spriteBatch.DrawSnapped(_pixel, new Rectangle(drawX, (int)manaBarY, whiteWidth, 1), Color.White);
                }
            }
            else
            {
                var manaRect = new Rectangle((int)barX, (int)manaBarY, barWidth, 1);

                // Draw Outline
                var manaBorderRect = new Rectangle(manaRect.X - 1, manaRect.Y - 1, manaRect.Width + 2, manaRect.Height + 2);
                DrawRectangleBorder(spriteBatch, manaBorderRect, _global.Palette_Black * manaAlpha);

                float manaPercent = combatant.Stats.MaxMana > 0 ? Math.Clamp((float)combatant.Stats.CurrentMana / combatant.Stats.MaxMana, 0f, 1f) : 0f;
                int manaFgWidth = (int)(manaRect.Width * manaPercent);
                if (manaPercent > 0 && manaFgWidth == 0) manaFgWidth = 1;

                var manaFgRect = new Rectangle(manaRect.X, manaRect.Y, manaFgWidth, manaRect.Height);

                spriteBatch.DrawSnapped(_pixel, manaRect, _global.Palette_DarkGray * manaAlpha);

                if (_spriteManager.ManaBarPattern != null)
                {
                    spriteBatch.DrawSnapped(_pixel, manaFgRect, _global.Palette_LightBlue * manaAlpha);
                }
                else
                {
                    spriteBatch.DrawSnapped(_pixel, manaFgRect, _global.Palette_LightBlue * manaAlpha);
                }

                var manaAnim = animationManager.GetResourceBarAnimation(combatant.CombatantID, BattleAnimationManager.ResourceBarAnimationState.BarResourceType.Mana);
                if (manaAnim != null)
                {
                    DrawBarAnimationOverlay(spriteBatch, manaRect, combatant.Stats.MaxMana, manaAnim);
                }
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
                if (player.ManaBarDisappearTimer > 0 || player.ManaBarWhiteHoldTimer > 0 || player.ManaBarWhiteExpandTimer > 0) return;

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
                        // If we can't afford it, show the whole cost width starting from 0, clamped to bar width?
                        // Or just flash the current bar red?
                        // Let's flash the current bar red to indicate "Not enough".
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

        private void DrawBarAnimationOverlay(SpriteBatch spriteBatch, Rectangle bgRect, float maxResource, BattleAnimationManager.ResourceBarAnimationState anim)
        {
            float percentBefore = anim.ValueBefore / maxResource;
            float percentAfter = anim.ValueAfter / maxResource;

            int widthBefore = (int)(bgRect.Width * percentBefore);
            int widthAfter = (int)(bgRect.Width * percentAfter);

            if (anim.AnimationType == BattleAnimationManager.ResourceBarAnimationState.BarAnimationType.Loss)
            {
                int previewStartX = bgRect.X + widthAfter;
                int previewWidth = widthBefore - widthAfter;
                var previewRect = new Rectangle(previewStartX, bgRect.Y, previewWidth, bgRect.Height);

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
                        previewRect.Width = (int)(previewWidth * (1.0f - eased));
                        color = (anim.ResourceType == BattleAnimationManager.ResourceBarAnimationState.BarResourceType.HP) ? _global.Palette_Red : _global.Palette_White;
                        break;
                }
                if (color != Color.Transparent) spriteBatch.DrawSnapped(_pixel, previewRect, color);
            }
            else // Recovery
            {
                float progress = anim.Timer / BattleAnimationManager.ResourceBarAnimationState.GHOST_FILL_DURATION;
                float eased = Easing.EaseOutCubic(progress);

                int ghostStartX = (int)(bgRect.X + bgRect.Width * percentBefore);
                int ghostWidth = (int)(bgRect.Width * (percentAfter - percentBefore));
                if (percentAfter > percentBefore && ghostWidth == 0) ghostWidth = 1;

                if (ghostWidth > 0)
                {
                    var ghostRect = new Rectangle(ghostStartX, bgRect.Y, ghostWidth, bgRect.Height);
                    Color ghostColor = (anim.ResourceType == BattleAnimationManager.ResourceBarAnimationState.BarResourceType.HP)
                        ? Color.Lerp(Color.White, _global.Palette_LightGreen, 0.5f)
                        : Color.Lerp(Color.White, _global.Palette_LightBlue, 0.5f);

                    float alpha = 1.0f;
                    if (progress > 0.7f) alpha = 1.0f - ((progress - 0.7f) / 0.3f);

                    spriteBatch.DrawSnapped(_pixel, ghostRect, ghostColor * alpha);
                }
            }
        }

        private void DrawRectangleBorder(SpriteBatch spriteBatch, Rectangle rect, Color color)
        {
            // Top
            spriteBatch.DrawSnapped(_pixel, new Rectangle(rect.Left, rect.Top, rect.Width, 1), color);
            // Bottom
            spriteBatch.DrawSnapped(_pixel, new Rectangle(rect.Left, rect.Bottom - 1, rect.Width, 1), color);
            // Left
            spriteBatch.DrawSnapped(_pixel, new Rectangle(rect.Left, rect.Top, 1, rect.Height), color);
            // Right
            spriteBatch.DrawSnapped(_pixel, new Rectangle(rect.Right - 1, rect.Top, 1, rect.Height), color);
        }
    }
}
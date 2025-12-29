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

            // --- MANA BAR ---
            float manaBarY = barY + barHeight + 1;
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
                // Note: Scissor test handling should be done by caller or wrapped here if needed. 
                // For simplicity, we assume standard drawing or simple texture draw.
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
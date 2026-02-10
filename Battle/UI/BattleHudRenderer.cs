using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Battle.UI
{
    /// <summary>
    /// Handles drawing UI elements attached to combatants: Health bars, Names, and Status Icons.
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

        public void DrawEnemyBars(SpriteBatch spriteBatch, BattleCombatant combatant, float barX, float barY, int barWidth, int barHeight, BattleAnimationManager animationManager, float hpAlpha, GameTime gameTime, bool isRightAligned = false, (int Min, int Max)? projectedDamage = null)
        {
            // --- NAME ---
            var tertiaryFont = ServiceLocator.Get<Core>().TertiaryFont;
            if (hpAlpha > 0.01f)
            {
                string name = combatant.Name.ToUpper();
                Vector2 namePos;
                if (isRightAligned)
                {
                    float nameWidth = tertiaryFont.MeasureString(name).Width;
                    namePos = new Vector2(barX + barWidth - nameWidth, barY - tertiaryFont.LineHeight - 1);
                }
                else
                {
                    namePos = new Vector2(barX, barY - tertiaryFont.LineHeight - 1);
                }
                spriteBatch.DrawStringSnapped(tertiaryFont, name, namePos, _global.Palette_Sun * hpAlpha);
            }

            // --- TENACITY BAR ---
            float nameTopY = barY - tertiaryFont.LineHeight - 1;
            float tenacityY = nameTopY - 2 - 3;

            DrawTenacityBar(spriteBatch, combatant, barX, tenacityY, barWidth, hpAlpha, isRightAligned);

            // --- HEALTH BAR ---
            float hpPercent = combatant.Stats.MaxHP > 0 ? Math.Clamp(combatant.VisualHP / combatant.Stats.MaxHP, 0f, 1f) : 0f;
            var hpAnim = animationManager.GetResourceBarAnimation(combatant.CombatantID, BattleAnimationManager.ResourceBarAnimationState.BarResourceType.HP);

            DrawBar(spriteBatch, new Vector2(barX, barY), barWidth, barHeight, hpPercent, _global.Palette_DarkShadow, _global.Palette_Leaf, _global.Palette_Black, hpAlpha, hpAnim, combatant.Stats.MaxHP, isRightAligned, projectedDamage, gameTime);
        }

        private void DrawBar(SpriteBatch spriteBatch, Vector2 position, int width, int height, float fillPercent, Color bgColor, Color fgColor, Color borderColor, float alpha, BattleAnimationManager.ResourceBarAnimationState? anim, float maxResource, bool isRightAligned, (int Min, int Max)? projectedDamage = null, GameTime gameTime = null)
        {
            if (alpha <= 0.01f) return;

            float x = position.X;
            float y = position.Y;
            float w = width;
            float h = height;

            // Background
            spriteBatch.DrawSnapped(_pixel, new Vector2(x, y), null, bgColor * alpha, 0f, Vector2.Zero, new Vector2(w, h), SpriteEffects.None, 0f);

            // Foreground
            float totalFgWidth = w * fillPercent;
            // Keep width integer-aligned for clean fill, but position is float
            int pixelFgWidth = (int)totalFgWidth;
            if (fillPercent > 0 && pixelFgWidth == 0) pixelFgWidth = 1;

            if (pixelFgWidth > 0)
            {
                float fgX = isRightAligned ? (x + w - pixelFgWidth) : x;
                spriteBatch.DrawSnapped(_pixel, new Vector2(fgX, y), null, fgColor * alpha, 0f, Vector2.Zero, new Vector2(pixelFgWidth, h), SpriteEffects.None, 0f);
            }

            // Damage Preview Overlay
            if (projectedDamage.HasValue && projectedDamage.Value.Max > 0 && gameTime != null)
            {
                int minDmg = projectedDamage.Value.Min;
                int maxDmg = projectedDamage.Value.Max;

                float currentPixels = w * fillPercent;

                float minDmgPercent = (float)minDmg / maxResource;
                float maxDmgPercent = (float)maxDmg / maxResource;

                float minDmgPixels = w * minDmgPercent;
                float maxDmgPixels = w * maxDmgPercent;

                // Clamp to current visual
                if (minDmgPixels > currentPixels) minDmgPixels = currentPixels;
                if (maxDmgPixels > currentPixels) maxDmgPixels = currentPixels;

                float flashMinRollAlpha = 0.9f + 0.1f * MathF.Sin((float)gameTime.TotalGameTime.TotalSeconds * 4f);
                float flashMaxRollAlpha = 0.4f + 0.2f * MathF.Sin((float)gameTime.TotalGameTime.TotalSeconds * 4f);
                Color minRollColor = _global.Palette_Black * alpha * flashMinRollAlpha;
                Color maxRollColor = _global.Palette_Black * alpha * flashMaxRollAlpha;

                if (isRightAligned)
                {
                    // Right aligned: Bar fills Right to Left.
                    // Current bar occupies [x + w - current, x + w]
                    // Damage eats from the LEFT side of the filled bar.

                    // Min Damage (Guaranteed)
                    if (minDmgPixels > 0)
                    {
                        float minX = x + w - currentPixels;
                        spriteBatch.DrawSnapped(_pixel, new Vector2(minX, y), null, minRollColor, 0f, Vector2.Zero, new Vector2(minDmgPixels, h), SpriteEffects.None, 0f);
                    }

                    // Max Damage Range (Potential)
                    float rangePixels = maxDmgPixels - minDmgPixels;
                    if (rangePixels > 0)
                    {
                        float rangeX = x + w - currentPixels + minDmgPixels;
                        spriteBatch.DrawSnapped(_pixel, new Vector2(rangeX, y), null, maxRollColor, 0f, Vector2.Zero, new Vector2(rangePixels, h), SpriteEffects.None, 0f);
                    }
                }
                else
                {
                    // Left aligned: Bar fills Left to Right.
                    // Current bar occupies [x, x + current]
                    // Damage eats from the RIGHT side of the filled bar.

                    // Min Damage (Guaranteed)
                    if (minDmgPixels > 0)
                    {
                        float minX = x + currentPixels - minDmgPixels;
                        spriteBatch.DrawSnapped(_pixel, new Vector2(minX, y), null, minRollColor, 0f, Vector2.Zero, new Vector2(minDmgPixels, h), SpriteEffects.None, 0f);
                    }

                    // Max Damage Range (Potential)
                    float rangePixels = maxDmgPixels - minDmgPixels;
                    if (rangePixels > 0)
                    {
                        float rangeX = x + currentPixels - maxDmgPixels;
                        spriteBatch.DrawSnapped(_pixel, new Vector2(rangeX, y), null, maxRollColor, 0f, Vector2.Zero, new Vector2(rangePixels, h), SpriteEffects.None, 0f);
                    }
                }
            }

            // Outline (1px thickness)
            // Top
            spriteBatch.DrawSnapped(_pixel, new Vector2(x, y - 1), null, borderColor * alpha, 0f, Vector2.Zero, new Vector2(w, 1), SpriteEffects.None, 0f);
            // Bottom
            spriteBatch.DrawSnapped(_pixel, new Vector2(x, y + h), null, borderColor * alpha, 0f, Vector2.Zero, new Vector2(w, 1), SpriteEffects.None, 0f);
            // Left
            spriteBatch.DrawSnapped(_pixel, new Vector2(x - 1, y - 1), null, borderColor * alpha, 0f, Vector2.Zero, new Vector2(1, h + 2), SpriteEffects.None, 0f);
            // Right
            spriteBatch.DrawSnapped(_pixel, new Vector2(x + w, y - 1), null, borderColor * alpha, 0f, Vector2.Zero, new Vector2(1, h + 2), SpriteEffects.None, 0f);

            // Animation Overlay
            if (anim != null)
            {
                DrawBarAnimationOverlay(spriteBatch, position, width, height, maxResource, anim, alpha, isRightAligned);
            }
        }

        private void DrawBar(SpriteBatch spriteBatch, Vector2 position, int width, int height, float fillPercent, Color bgColor, Color fgColor, Color borderColor, float alpha, BattleAnimationManager.ResourceBarAnimationState? anim, float maxResource, bool isRightAligned, int? projectedDamage = null, GameTime gameTime = null)
        {
            if (alpha <= 0.01f) return;

            float x = position.X;
            float y = position.Y;
            float w = width;
            float h = height;

            // Background
            spriteBatch.DrawSnapped(_pixel, new Vector2(x, y), null, bgColor * alpha, 0f, Vector2.Zero, new Vector2(w, h), SpriteEffects.None, 0f);

            // Foreground
            float totalFgWidth = w * fillPercent;
            // Keep width integer-aligned for clean fill, but position is float
            int pixelFgWidth = (int)totalFgWidth;
            if (fillPercent > 0 && pixelFgWidth == 0) pixelFgWidth = 1;

            if (pixelFgWidth > 0)
            {
                float fgX = isRightAligned ? (x + w - pixelFgWidth) : x;
                spriteBatch.DrawSnapped(_pixel, new Vector2(fgX, y), null, fgColor * alpha, 0f, Vector2.Zero, new Vector2(pixelFgWidth, h), SpriteEffects.None, 0f);
            }

            // Damage Preview Overlay
            if (projectedDamage.HasValue && projectedDamage.Value > 0 && gameTime != null)
            {
                float currentPixels = w * fillPercent;
                float damagePercent = (float)projectedDamage.Value / maxResource;
                float damagePixels = w * damagePercent;

                // Clamp damage visual to current visual
                if (damagePixels > currentPixels) damagePixels = currentPixels;

                if (damagePixels > 0)
                {
                    float flashX;
                    if (isRightAligned)
                    {
                        // Right aligned: Bar fills from Right to Left.
                        // Filled part is [x + w - current, x + w]
                        // We lose the Left-most part of the filled bar.
                        flashX = x + w - currentPixels;
                    }
                    else
                    {
                        // Left aligned: Bar fills from Left to Right.
                        // Filled part is [x, x + current]
                        // We lose the Right-most part.
                        flashX = x + currentPixels - damagePixels;
                    }

                    // Slower flash (8f) and Rust color
                    float flashAlpha = 0.6f + 0.4f * MathF.Sin((float)gameTime.TotalGameTime.TotalSeconds * 8f);
                    Color flashColor = _global.Palette_Rust * alpha * flashAlpha;

                    spriteBatch.DrawSnapped(_pixel, new Vector2(flashX, y), null, flashColor, 0f, Vector2.Zero, new Vector2(damagePixels, h), SpriteEffects.None, 0f);
                }
            }

            // Outline (1px thickness)
            // Top
            spriteBatch.DrawSnapped(_pixel, new Vector2(x, y - 1), null, borderColor * alpha, 0f, Vector2.Zero, new Vector2(w, 1), SpriteEffects.None, 0f);
            // Bottom
            spriteBatch.DrawSnapped(_pixel, new Vector2(x, y + h), null, borderColor * alpha, 0f, Vector2.Zero, new Vector2(w, 1), SpriteEffects.None, 0f);
            // Left
            spriteBatch.DrawSnapped(_pixel, new Vector2(x - 1, y - 1), null, borderColor * alpha, 0f, Vector2.Zero, new Vector2(1, h + 2), SpriteEffects.None, 0f);
            // Right
            spriteBatch.DrawSnapped(_pixel, new Vector2(x + w, y - 1), null, borderColor * alpha, 0f, Vector2.Zero, new Vector2(1, h + 2), SpriteEffects.None, 0f);

            // Animation Overlay
            if (anim != null)
            {
                DrawBarAnimationOverlay(spriteBatch, position, width, height, maxResource, anim, alpha, isRightAligned);
            }
        }

        private void DrawTenacityBar(SpriteBatch spriteBatch, BattleCombatant combatant, float startX, float startY, float barWidth, float alpha, bool isRightAligned)
        {
            if (alpha <= 0.01f) return;

            int maxTenacity = combatant.Stats.Tenacity;
            int currentTenacity = combatant.CurrentTenacity;

            const int pipSize = 3;
            const int gap = 1;

            var fullRect = new Rectangle(0, 0, 3, 3);
            var emptyRect = new Rectangle(3, 0, 3, 3);

            for (int i = 0; i < maxTenacity; i++)
            {
                float x;
                if (isRightAligned)
                {
                    x = startX + barWidth - ((i + 1) * (pipSize + gap)) + gap;
                }
                else
                {
                    x = startX + (i * (pipSize + gap));
                }

                // Use Vector2 position
                var pos = new Vector2(x, startY);
                var sourceRect = (i < currentTenacity) ? fullRect : emptyRect;

                // DrawSnapped with Vector2 position and 1.0 scale (since source rect defines size)
                spriteBatch.DrawSnapped(_spriteManager.TenacityPipTexture, pos, sourceRect, Color.White * alpha, 0f, Vector2.Zero, 1.0f, SpriteEffects.None, 0f);
            }
        }

        public void DrawPlayerBars(SpriteBatch spriteBatch, BattleCombatant player, float barX, float barY, int barWidth, int barHeight, BattleAnimationManager animationManager, float hpAlpha, GameTime gameTime, BattleUIManager uiManager, bool isActiveActor, bool isRightAligned = false, (int Min, int Max)? projectedDamage = null)
        {
            var tertiaryFont = ServiceLocator.Get<Core>().TertiaryFont;
            if (hpAlpha > 0.01f)
            {
                string name = player.Name.ToUpper();
                Vector2 namePos;
                if (isRightAligned)
                {
                    float nameWidth = tertiaryFont.MeasureString(name).Width;
                    namePos = new Vector2(barX + barWidth - nameWidth, barY - tertiaryFont.LineHeight - 1);
                }
                else
                {
                    namePos = new Vector2(barX, barY - tertiaryFont.LineHeight - 1);
                }
                spriteBatch.DrawStringSnapped(tertiaryFont, name, namePos, _global.Palette_Sun * hpAlpha);
            }

            float nameTopY = barY - tertiaryFont.LineHeight - 1;
            float tenacityY = nameTopY - 2 - 3;
            DrawTenacityBar(spriteBatch, player, barX, tenacityY, barWidth, hpAlpha, isRightAligned);

            float hpPercent = player.Stats.MaxHP > 0 ? Math.Clamp(player.VisualHP / player.Stats.MaxHP, 0f, 1f) : 0f;
            var hpAnim = animationManager.GetResourceBarAnimation(player.CombatantID, BattleAnimationManager.ResourceBarAnimationState.BarResourceType.HP);

            DrawBar(spriteBatch, new Vector2(barX, barY), barWidth, barHeight, hpPercent, _global.Palette_DarkShadow, _global.Palette_Leaf, _global.Palette_Black, hpAlpha, hpAnim, player.Stats.MaxHP, isRightAligned, projectedDamage, gameTime);
        }

        public void DrawStatusIcons(SpriteBatch spriteBatch, BattleCombatant combatant, float startX, float startY, int width, bool isPlayer, List<StatusIconInfo> iconTracker, Func<string, StatusEffectType, float> getOffsetFunc, Func<string, StatusEffectType, bool> isAnimatingFunc, bool isRightAligned = false)
        {
            if (!combatant.ActiveStatusEffects.Any()) return;

            iconTracker?.Clear();

            int iconSize = BattleLayout.STATUS_ICON_SIZE;
            int gap = BattleLayout.STATUS_ICON_GAP;

            // Position below health bar (removed mana bar gap)
            float iconY = startY + BattleLayout.ENEMY_BAR_HEIGHT + 2;

            int step = iconSize + gap;

            // Calculate animation frame (1 second cycle)
            int frameIndex = (DateTime.Now.Millisecond < 500) ? 0 : 1;

            for (int i = 0; i < combatant.ActiveStatusEffects.Count; i++)
            {
                var effect = combatant.ActiveStatusEffects[i];
                // Only draw permanent effects
                if (!effect.IsPermanent) continue;

                float hopOffset = getOffsetFunc(combatant.CombatantID, effect.EffectType);
                bool isAnimating = isAnimatingFunc(combatant.CombatantID, effect.EffectType);

                float x;
                if (isRightAligned)
                {
                    x = startX + width - ((i + 1) * step) + gap;
                }
                else
                {
                    x = startX + (i * step);
                }

                var iconPos = new Vector2(x, iconY + hopOffset);
                var iconBounds = new Rectangle((int)x, (int)(iconY + hopOffset), iconSize, iconSize);

                if (iconTracker != null)
                {
                    iconTracker.Add(new StatusIconInfo { Effect = effect, Bounds = iconBounds });
                }

                var sourceRect = _spriteManager.GetPermanentStatusIconSourceRect(effect.EffectType, frameIndex);

                if (sourceRect != Rectangle.Empty)
                {
                    spriteBatch.DrawSnapped(_spriteManager.PermanentStatusIconsSpriteSheet, iconPos, sourceRect, Color.White);

                    if (isAnimating)
                    {
                        spriteBatch.DrawSnapped(_spriteManager.PermanentStatusIconsSpriteSheet, iconPos, sourceRect, Color.White * 0.5f);
                    }
                }
            }
        }

        private void DrawBarAnimationOverlay(SpriteBatch spriteBatch, Vector2 position, int width, int height, float maxResource, BattleAnimationManager.ResourceBarAnimationState anim, float alpha, bool isRightAligned)
        {
            float percentBefore = anim.ValueBefore / maxResource;
            float percentAfter = anim.ValueAfter / maxResource;

            int widthBefore = (int)(width * percentBefore);
            int widthAfter = (int)(width * percentAfter);

            if (percentAfter > 0 && widthAfter == 0) widthAfter = 1;
            if (percentBefore > 0 && widthBefore == 0) widthBefore = 1;

            float visibleStartX = position.X;
            float visibleWidth = width;
            float visibleEndX = visibleStartX + visibleWidth;

            if (visibleWidth <= 0) return;

            if (anim.AnimationType == BattleAnimationManager.ResourceBarAnimationState.BarAnimationType.Loss)
            {
                float previewStartX;
                int previewWidth = widthBefore - widthAfter;

                if (isRightAligned)
                {
                    previewStartX = visibleStartX + visibleWidth - widthBefore;
                }
                else
                {
                    previewStartX = visibleStartX + widthAfter;
                }

                Color color = Color.Transparent;
                switch (anim.CurrentLossPhase)
                {
                    case BattleAnimationManager.ResourceBarAnimationState.LossPhase.Preview:
                        color = (anim.ResourceType == BattleAnimationManager.ResourceBarAnimationState.BarResourceType.HP) ? _global.Palette_Rust : Color.White;
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

                        if (isRightAligned)
                        {
                            int originalDiff = widthBefore - widthAfter;
                            int lostAmt = originalDiff - previewWidth;
                            previewStartX += lostAmt;
                        }

                        color = (anim.ResourceType == BattleAnimationManager.ResourceBarAnimationState.BarResourceType.HP) ? _global.Palette_Rust : _global.Palette_Sun;
                        break;
                }

                if (color != Color.Transparent && previewWidth > 0)
                {
                    float drawStartX = Math.Max(previewStartX, visibleStartX);
                    float drawEndX = Math.Min(previewStartX + previewWidth, visibleEndX);
                    float drawWidth = drawEndX - drawStartX;

                    if (drawWidth > 0)
                    {
                        spriteBatch.DrawSnapped(_pixel, new Vector2(drawStartX, position.Y), null, color * alpha, 0f, Vector2.Zero, new Vector2(drawWidth, height), SpriteEffects.None, 0f);
                    }
                }
            }
            else // Recovery
            {
                float ghostStartX;
                int ghostWidth = widthAfter - widthBefore;

                if (isRightAligned)
                {
                    ghostStartX = visibleStartX + visibleWidth - widthAfter;
                }
                else
                {
                    ghostStartX = visibleStartX + widthBefore;
                }

                if (ghostWidth > 0)
                {
                    float drawStartX = Math.Max(ghostStartX, visibleStartX);
                    float drawEndX = Math.Min(ghostStartX + ghostWidth, visibleEndX);
                    float drawWidth = drawEndX - drawStartX;

                    if (drawWidth > 0)
                    {
                        Color ghostColor = _global.HealOverlayColor;
                        float overlayAlpha = alpha;
                        if (anim.CurrentRecoveryPhase == BattleAnimationManager.ResourceBarAnimationState.RecoveryPhase.Fade)
                        {
                            float progress = anim.Timer / _global.HealOverlayFadeDuration;
                            overlayAlpha *= (1.0f - progress);
                        }

                        spriteBatch.DrawSnapped(_pixel, new Vector2(drawStartX, position.Y), null, ghostColor * overlayAlpha, 0f, Vector2.Zero, new Vector2(drawWidth, height), SpriteEffects.None, 0f);
                    }
                }
            }
        }
    }
}
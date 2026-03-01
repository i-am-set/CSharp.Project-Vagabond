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

        // --- TUNING NUMBERS ---
        public const int HP_WRAP_THRESHOLD = 110; // Wrap if MaxHP is >= this
        public const int HP_WRAP_CHUNK = 100;     // Pips per line when wrapped
        public const int HP_WRAP_GAP = 1;         // Vertical pixels between lines

        public BattleHudRenderer()
        {
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _global = ServiceLocator.Get<Global>();
            _pixel = ServiceLocator.Get<Texture2D>();
        }

        /// <summary>
        /// Calculates the extra vertical pixels needed for UI elements below the health bar
        /// based on whether the bar is wrapped.
        /// </summary>
        public static float GetVerticalOffset(int maxHP, int barHeight)
        {
            if (maxHP >= HP_WRAP_THRESHOLD)
            {
                return barHeight + HP_WRAP_GAP;
            }
            return 0f;
        }

        public void DrawEnemyBars(SpriteBatch spriteBatch, BattleCombatant combatant, float barX, float barY, int barWidth, int barHeight, BattleAnimationManager animationManager, float hpAlpha, GameTime gameTime, bool isRightAligned = false, (int Min, int Max)? projectedDamage = null, (int Min, int Max)? projectedHeal = null)
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

            // --- HEALTH BAR (PIPS) ---
            float hpPercent = combatant.Stats.MaxHP > 0 ? Math.Clamp(combatant.VisualHP / combatant.Stats.MaxHP, 0f, 1f) : 0f;
            var hpAnim = animationManager.GetResourceBarAnimation(combatant.CombatantID, BattleAnimationManager.ResourceBarAnimationState.BarResourceType.HP);

            DrawPipBar(spriteBatch, new Vector2(barX, barY), barWidth, barHeight, hpPercent, _global.Palette_DarkShadow, _global.Palette_Leaf, hpAlpha, hpAnim, combatant.Stats.MaxHP, isRightAligned, projectedDamage, projectedHeal, gameTime);
        }

        public void DrawPlayerBars(SpriteBatch spriteBatch, BattleCombatant player, float barX, float barY, int barWidth, int barHeight, BattleAnimationManager animationManager, float hpAlpha, GameTime gameTime, BattleUIManager uiManager, bool isActiveActor, bool isRightAligned = false, (int Min, int Max)? projectedDamage = null, (int Min, int Max)? projectedHeal = null)
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

            DrawPipBar(spriteBatch, new Vector2(barX, barY), barWidth, barHeight, hpPercent, _global.Palette_DarkShadow, _global.Palette_Leaf, hpAlpha, hpAnim, player.Stats.MaxHP, isRightAligned, projectedDamage, projectedHeal, gameTime);

            // --- EXP PROGRESSION BAR ---
            if (hpAlpha > 0.01f)
            {
                int maxPips = player.Stats.MaxHP;
                int pipsPerLine = maxPips >= HP_WRAP_THRESHOLD ? HP_WRAP_CHUNK : maxPips;
                int rows = maxPips > 0 ? ((maxPips - 1) / pipsPerLine) + 1 : 1;
                float hpBarTotalHeight = rows * barHeight + (rows - 1) * HP_WRAP_GAP;

                float expBarY = barY + hpBarTotalHeight + 2;
                float textY = expBarY - 1;

                string lvValue = player.VisualLevel.ToString();
                float lvLabelWidth = _spriteManager.LevelIconSprite != null ? _spriteManager.LevelIconSprite.Width + 1 : 0;
                float lvValueWidth = tertiaryFont.MeasureString(lvValue).Width;

                int totalBarWidth = 60;
                float totalBlockWidth = lvLabelWidth + lvValueWidth + 2 + totalBarWidth;

                float iconX, textX, barStartX;

                if (isRightAligned)
                {
                    // Right-align the entire block to the right edge of the HP bar
                    float blockStartX = barX + barWidth - totalBlockWidth;

                    // Order: Bar -> Icon -> Text
                    barStartX = blockStartX;
                    iconX = barStartX + totalBarWidth + 2;
                    textX = iconX + lvLabelWidth;
                }
                else
                {
                    // Left-align the entire block to the left edge of the HP bar
                    float blockStartX = barX;

                    // Order: Icon -> Text -> Bar
                    iconX = blockStartX;
                    textX = iconX + lvLabelWidth;
                    barStartX = textX + lvValueWidth + 2;
                }

                // 1. Draw Level Icon
                if (_spriteManager.LevelIconSprite != null)
                {
                    spriteBatch.DrawSnapped(_spriteManager.LevelIconSprite, new Vector2(iconX, textY + 1), _global.Palette_DarkestPale * hpAlpha);
                }

                // 2. Draw Level Text
                spriteBatch.DrawStringSnapped(tertiaryFont, lvValue, new Vector2(textX, textY), _global.Palette_DarkestPale * hpAlpha);

                // 3. Draw EXP Bar
                float expProgress = player.VisualMaxEXP > 0 ? Math.Clamp(player.VisualEXP / player.VisualMaxEXP, 0f, 1f) : 0f;
                float pixelProgress = expProgress * totalBarWidth;
                int filledPixels = (int)pixelProgress;
                float subProgress = pixelProgress - filledPixels;

                Color pxColor = _global.Palette_Sun;
                if (filledPixels < totalBarWidth)
                {
                    if (subProgress < 0.25f) pxColor = _global.Palette_Sea;
                    else if (subProgress < 0.50f) pxColor = _global.Palette_Sky;
                    else if (subProgress < 0.75f) pxColor = _global.Palette_Leaf;
                }

                int emptyPixels = totalBarWidth - filledPixels - (filledPixels < totalBarWidth ? 1 : 0);

                if (isRightAligned)
                {
                    // Draw Empty (Left side)
                    if (emptyPixels > 0)
                        spriteBatch.DrawSnapped(_pixel, new Vector2(barStartX, expBarY), null, _global.Palette_DarkestPale * hpAlpha, 0f, Vector2.Zero, new Vector2(emptyPixels, 1), SpriteEffects.None, 0f);

                    // Draw Progressing Pixel (Middle)
                    if (filledPixels < totalBarWidth)
                        spriteBatch.DrawSnapped(_pixel, new Vector2(barStartX + emptyPixels, expBarY), null, pxColor * hpAlpha, 0f, Vector2.Zero, new Vector2(1, 1), SpriteEffects.None, 0f);

                    // Draw Filled (Right side)
                    if (filledPixels > 0)
                    {
                        int filledStartX = (int)(barStartX + emptyPixels + (filledPixels < totalBarWidth ? 1 : 0));
                        spriteBatch.DrawSnapped(_pixel, new Vector2(filledStartX, expBarY), null, _global.Palette_Sun * hpAlpha, 0f, Vector2.Zero, new Vector2(filledPixels, 1), SpriteEffects.None, 0f);
                    }
                }
                else
                {
                    // Draw Filled (Left side)
                    if (filledPixels > 0)
                        spriteBatch.DrawSnapped(_pixel, new Vector2(barStartX, expBarY), null, _global.Palette_Sun * hpAlpha, 0f, Vector2.Zero, new Vector2(filledPixels, 1), SpriteEffects.None, 0f);

                    // Draw Progressing Pixel (Middle)
                    if (filledPixels < totalBarWidth)
                        spriteBatch.DrawSnapped(_pixel, new Vector2(barStartX + filledPixels, expBarY), null, pxColor * hpAlpha, 0f, Vector2.Zero, new Vector2(1, 1), SpriteEffects.None, 0f);

                    // Draw Empty (Right side)
                    if (emptyPixels > 0)
                    {
                        int emptyStartX = (int)(barStartX + filledPixels + (filledPixels < totalBarWidth ? 1 : 0));
                        spriteBatch.DrawSnapped(_pixel, new Vector2(emptyStartX, expBarY), null, _global.Palette_DarkestPale * hpAlpha, 0f, Vector2.Zero, new Vector2(emptyPixels, 1), SpriteEffects.None, 0f);
                    }
                }
            }
        }

        private void DrawPipBar(SpriteBatch spriteBatch, Vector2 position, int width, int height, float fillPercent, Color bgColor, Color fgColor, float alpha, BattleAnimationManager.ResourceBarAnimationState? anim, float maxResource, bool isRightAligned, (int Min, int Max)? projectedDamage = null, (int Min, int Max)? projectedHeal = null, GameTime gameTime = null)
        {
            if (alpha <= 0.01f) return;

            int maxPips = (int)maxResource;
            int currentPips = (int)Math.Round(fillPercent * maxResource);

            // Pip Constants
            const int pipWidth = 1;
            const int pipGap = 1;
            const int stride = pipWidth + pipGap;

            // Damage Preview Calculation
            int dmgMinStart = int.MaxValue;
            int dmgMaxStart = int.MaxValue;

            if (projectedDamage.HasValue && projectedDamage.Value.Max > 0)
            {
                int minDmg = projectedDamage.Value.Min;
                int maxDmg = projectedDamage.Value.Max;
                dmgMinStart = Math.Max(0, currentPips - minDmg);
                dmgMaxStart = Math.Max(0, currentPips - maxDmg);
            }

            // Heal Preview Calculation
            int healMinEnd = -1;
            int healMaxEnd = -1;
            if (projectedHeal.HasValue && projectedHeal.Value.Max > 0)
            {
                int minHeal = projectedHeal.Value.Min;
                int maxHeal = projectedHeal.Value.Max;
                healMinEnd = Math.Min(maxPips, currentPips + minHeal);
                healMaxEnd = Math.Min(maxPips, currentPips + maxHeal);
            }

            // Flash Timers
            float flashMinRollAlpha = 0f;
            float flashMaxRollAlpha = 0f;
            if (gameTime != null)
            {
                flashMinRollAlpha = 0.6f + 0.4f * MathF.Sin((float)gameTime.TotalGameTime.TotalSeconds * 8f);
                flashMaxRollAlpha = 0.8f + 0.2f * MathF.Sin((float)gameTime.TotalGameTime.TotalSeconds * 2f);
            }

            // --- WRAPPING LOGIC (TUNED) ---
            int visualColsPerLine = (maxPips + 1) / 2;
            if (maxPips >= HP_WRAP_THRESHOLD)
            {
                visualColsPerLine = HP_WRAP_CHUNK / 2;
            }

            // --- DRAW LOOP ---
            for (int i = 0; i < maxPips; i++)
            {
                // Calculate Row and Column based on visual columns
                int visualColIndex = i / 2;
                int row = visualColIndex / visualColsPerLine;
                int col = visualColIndex % visualColsPerLine;

                // Calculate Y Position (with Gap)
                float rowY = position.Y + (row * (height + HP_WRAP_GAP));
                float pipY = rowY + ((i % 2 == 0) ? 1 : 0);

                // Calculate X Position
                float pipX;
                if (isRightAligned)
                {
                    // Anchored Right: Reset X for each row using 'width' anchor
                    float rightAnchor = position.X + width;
                    pipX = rightAnchor - ((col + 1) * stride) + pipGap;
                }
                else
                {
                    // Anchored Left: Reset X for each row
                    pipX = position.X + (col * stride);
                }

                Color pipColor = bgColor;

                // 1. Base State
                if (i < currentPips) pipColor = fgColor;

                // 2. Animation Overlay
                if (anim != null)
                {
                    int valueBefore = (int)anim.ValueBefore;

                    if (anim.AnimationType == BattleAnimationManager.ResourceBarAnimationState.BarAnimationType.Loss)
                    {
                        if (anim.CurrentLossPhase == BattleAnimationManager.ResourceBarAnimationState.LossPhase.Shrink)
                        {
                            if (i >= currentPips && i < anim.AnimatedHP) pipColor = _global.Palette_Rust;
                        }
                        else
                        {
                            if (i >= currentPips && i < valueBefore)
                            {
                                if (anim.CurrentLossPhase == BattleAnimationManager.ResourceBarAnimationState.LossPhase.Preview) pipColor = _global.Palette_Rust;
                                else if (anim.CurrentLossPhase == BattleAnimationManager.ResourceBarAnimationState.LossPhase.FlashBlack) pipColor = Color.Black;
                                else if (anim.CurrentLossPhase == BattleAnimationManager.ResourceBarAnimationState.LossPhase.FlashWhite) pipColor = Color.White;
                            }
                        }
                    }
                    else // Recovery
                    {
                        if (anim.CurrentRecoveryPhase == BattleAnimationManager.ResourceBarAnimationState.RecoveryPhase.Hang)
                        {
                            if (i >= valueBefore && i < currentPips) pipColor = _global.Palette_Sun;
                        }
                        else if (anim.CurrentRecoveryPhase == BattleAnimationManager.ResourceBarAnimationState.RecoveryPhase.Fade)
                        {
                            if (i >= valueBefore && i < anim.AnimatedHP) pipColor = _global.Palette_Sky;
                        }
                    }
                }

                // 3. Damage Preview
                if (i < currentPips && projectedDamage.HasValue)
                {
                    if (i >= dmgMinStart) pipColor = _global.Palette_Shadow * flashMinRollAlpha;
                    else if (i >= dmgMaxStart) pipColor = _global.Palette_Rust * flashMaxRollAlpha;
                }

                // 4. Heal Preview
                if (i >= currentPips && projectedHeal.HasValue)
                {
                    if (i < healMinEnd) pipColor = _global.Palette_Sea;
                    else if (i < healMaxEnd) pipColor = _global.Palette_Sky * 0.7f;
                }

                spriteBatch.DrawSnapped(_pixel, new Vector2(pipX, pipY), null, pipColor * alpha, 0f, Vector2.Zero, new Vector2(1, 1), SpriteEffects.None, 0f);
            }
        }

        private void DrawTenacityBar(SpriteBatch spriteBatch, BattleCombatant combatant, float startX, float startY, float barWidth, float alpha, bool isRightAligned)
        {
            if (alpha <= 0.01f) return;

            // Use MaxGuard for loop limit
            int maxGuard = combatant.MaxGuard;
            // Use CurrentGuard for fill check
            int currentGuard = combatant.CurrentGuard;

            const int pipSize = 3;
            const int gap = 1;

            var fullRect = new Rectangle(0, 0, 3, 3);
            var emptyRect = new Rectangle(3, 0, 3, 3);

            for (int i = 0; i < maxGuard; i++)
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

                var pos = new Vector2(x, startY);
                var sourceRect = (i < currentGuard) ? fullRect : emptyRect;

                spriteBatch.DrawSnapped(_spriteManager.TenacityPipTexture, pos, sourceRect, Color.White * alpha, 0f, Vector2.Zero, 1.0f, SpriteEffects.None, 0f);
            }
        }

        public void DrawStatusIcons(SpriteBatch spriteBatch, BattleCombatant combatant, float startX, float startY, int width, bool isPlayer, List<StatusIconInfo> iconTracker, Func<string, StatusEffectType, float> getOffsetFunc, Func<string, StatusEffectType, bool> isAnimatingFunc, bool isRightAligned = false)
        {
            if (!combatant.ActiveStatusEffects.Any()) return;

            iconTracker?.Clear();

            int iconSize = BattleLayout.STATUS_ICON_SIZE;
            int gap = BattleLayout.STATUS_ICON_GAP;

            // --- OFFSET LOGIC FOR WRAPPED BARS ---
            float wrapOffset = GetVerticalOffset(combatant.Stats.MaxHP, BattleLayout.ENEMY_BAR_HEIGHT);
            float iconY = startY + BattleLayout.ENEMY_BAR_HEIGHT + 2 + wrapOffset;

            int step = iconSize + gap;
            int frameIndex = (DateTime.Now.Millisecond < 500) ? 0 : 1;

            for (int i = 0; i < combatant.ActiveStatusEffects.Count; i++)
            {
                var effect = combatant.ActiveStatusEffects[i];
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
    }
}
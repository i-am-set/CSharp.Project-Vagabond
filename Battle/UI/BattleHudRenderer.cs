using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Battle.UI
{
    public class BattleHudRenderer
    {
        private readonly SpriteManager _spriteManager;
        private readonly Global _global;
        private readonly Texture2D _pixel;

        public const int HP_WRAP_THRESHOLD = 110;
        public const int HP_WRAP_CHUNK = 100;
        public const int HP_WRAP_GAP = 1;

        public BattleHudRenderer()
        {
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _global = ServiceLocator.Get<Global>();
            _pixel = ServiceLocator.Get<Texture2D>();
        }

        public static float GetVerticalOffset(int maxHP, int barHeight)
        {
            if (maxHP >= HP_WRAP_THRESHOLD) return barHeight + HP_WRAP_GAP;
            return 0f;
        }

        public void DrawEnemyBars(SpriteBatch spriteBatch, BattleCombatant combatant, float barX, float barY, int barWidth, int barHeight, BattleAnimationManager animationManager, float hpAlpha, GameTime gameTime, bool isRightAligned = false, (int Min, int Max)? projectedDamage = null, (int Min, int Max)? projectedHeal = null)
        {
            var tertiaryFont = ServiceLocator.Get<Core>().TertiaryFont;
            if (hpAlpha > 0.01f)
            {
                string name = combatant.Name.ToUpper();
                Vector2 namePos = isRightAligned
                    ? new Vector2(barX + barWidth - tertiaryFont.MeasureString(name).Width, barY - tertiaryFont.LineHeight - 1)
                    : new Vector2(barX, barY - tertiaryFont.LineHeight - 1);
                spriteBatch.DrawStringSnapped(tertiaryFont, name, namePos, _global.Palette_Sun * hpAlpha);
            }

            float nameTopY = barY - tertiaryFont.LineHeight - 1;
            float tenacityY = nameTopY - 5;
            DrawTenacityBar(spriteBatch, combatant, barX, tenacityY, barWidth, hpAlpha, isRightAligned);

            float hpPercent = combatant.Stats.MaxHP > 0 ? Math.Clamp(combatant.VisualHP / combatant.Stats.MaxHP, 0f, 1f) : 0f;
            var hpAnim = animationManager.GetResourceBarAnimation(combatant.CombatantID, BattleAnimationManager.ResourceBarAnimationState.BarResourceType.HP);

            DrawHeartBar(spriteBatch, combatant, new Vector2(barX, barY + 1), barWidth, hpPercent, hpAlpha, hpAnim, combatant.Stats.MaxHP, isRightAligned);
        }

        public void DrawPlayerBars(SpriteBatch spriteBatch, BattleCombatant player, float barX, float barY, int barWidth, int barHeight, BattleAnimationManager animationManager, float hpAlpha, GameTime gameTime, BattleUIManager uiManager, bool isActiveActor, bool isRightAligned = false, (int Min, int Max)? projectedDamage = null, (int Min, int Max)? projectedHeal = null)
        {
            var tertiaryFont = ServiceLocator.Get<Core>().TertiaryFont;

            if (hpAlpha > 0.01f)
            {
                string name = player.Name.ToUpper();
                Vector2 namePos = isRightAligned
                    ? new Vector2(barX + barWidth - tertiaryFont.MeasureString(name).Width, barY - tertiaryFont.LineHeight - 1)
                    : new Vector2(barX, barY - tertiaryFont.LineHeight - 1);
                spriteBatch.DrawStringSnapped(tertiaryFont, name, namePos, _global.Palette_Sun * hpAlpha);
            }

            float nameTopY = barY - tertiaryFont.LineHeight - 1;
            float tenacityY = nameTopY - 5;
            DrawTenacityBar(spriteBatch, player, barX, tenacityY, barWidth, hpAlpha, isRightAligned);

            float hpPercent = player.Stats.MaxHP > 0 ? Math.Clamp(player.VisualHP / player.Stats.MaxHP, 0f, 1f) : 0f;
            var hpAnim = animationManager.GetResourceBarAnimation(player.CombatantID, BattleAnimationManager.ResourceBarAnimationState.BarResourceType.HP);

            DrawHeartBar(spriteBatch, player, new Vector2(barX, barY + 1), barWidth, hpPercent, hpAlpha, hpAnim, player.Stats.MaxHP, isRightAligned);

            // EXP PROGRESSION BAR
            if (hpAlpha > 0.01f)
            {
                float expBarY = barY + 6 + 2 + 1; // 5px heart + gap + 2 + 1px drop
                float textY = expBarY - 1;

                string lvValue = player.VisualLevel.ToString();
                float lvLabelWidth = _spriteManager.LevelIconSprite != null ? _spriteManager.LevelIconSprite.Width + 1 : 0;
                float lvValueWidth = tertiaryFont.MeasureString(lvValue).Width;

                int totalBarWidth = 60;
                float totalBlockWidth = lvLabelWidth + lvValueWidth + 2 + totalBarWidth;

                float iconX, textX, barStartX;
                if (isRightAligned)
                {
                    float blockStartX = barX + barWidth - totalBlockWidth;
                    barStartX = blockStartX;
                    iconX = barStartX + totalBarWidth + 2;
                    textX = iconX + lvLabelWidth;
                }
                else
                {
                    float blockStartX = barX;
                    iconX = blockStartX;
                    textX = iconX + lvLabelWidth;
                    barStartX = textX + lvValueWidth + 2;
                }

                if (_spriteManager.LevelIconSprite != null) spriteBatch.DrawSnapped(_spriteManager.LevelIconSprite, new Vector2(iconX, textY + 1), _global.Palette_DarkestPale * hpAlpha);
                spriteBatch.DrawStringSnapped(tertiaryFont, lvValue, new Vector2(textX, textY), _global.Palette_DarkestPale * hpAlpha);

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
                    if (emptyPixels > 0) spriteBatch.DrawSnapped(_pixel, new Vector2(barStartX, expBarY), null, _global.Palette_DarkestPale * hpAlpha, 0f, Vector2.Zero, new Vector2(emptyPixels, 1), SpriteEffects.None, 0f);
                    if (filledPixels < totalBarWidth) spriteBatch.DrawSnapped(_pixel, new Vector2(barStartX + emptyPixels, expBarY), null, pxColor * hpAlpha, 0f, Vector2.Zero, new Vector2(1, 1), SpriteEffects.None, 0f);
                    if (filledPixels > 0) spriteBatch.DrawSnapped(_pixel, new Vector2(barStartX + emptyPixels + (filledPixels < totalBarWidth ? 1 : 0), expBarY), null, _global.Palette_Sun * hpAlpha, 0f, Vector2.Zero, new Vector2(filledPixels, 1), SpriteEffects.None, 0f);
                }
                else
                {
                    if (filledPixels > 0) spriteBatch.DrawSnapped(_pixel, new Vector2(barStartX, expBarY), null, _global.Palette_Sun * hpAlpha, 0f, Vector2.Zero, new Vector2(filledPixels, 1), SpriteEffects.None, 0f);
                    if (filledPixels < totalBarWidth) spriteBatch.DrawSnapped(_pixel, new Vector2(barStartX + filledPixels, expBarY), null, pxColor * hpAlpha, 0f, Vector2.Zero, new Vector2(1, 1), SpriteEffects.None, 0f);
                    if (emptyPixels > 0) spriteBatch.DrawSnapped(_pixel, new Vector2(barStartX + filledPixels + (filledPixels < totalBarWidth ? 1 : 0), expBarY), null, _global.Palette_DarkestPale * hpAlpha, 0f, Vector2.Zero, new Vector2(emptyPixels, 1), SpriteEffects.None, 0f);
                }
            }
        }

        private void DrawHeartBar(SpriteBatch spriteBatch, BattleCombatant combatant, Vector2 position, int width, float fillPercent, float alpha, BattleAnimationManager.ResourceBarAnimationState anim, float maxResource, bool isRightAligned)
        {
            if (alpha <= 0.01f || _spriteManager.HealthHeartsSpriteSheet == null) return;

            int maxHearts = (int)Math.Ceiling(maxResource / 2f);
            int currentHP = (int)Math.Round(fillPercent * maxResource);

            const int heartWidth = 5;
            const int heartGap = 1;
            const int stride = heartWidth + heartGap;

            int maxDamagedIndex = -1;
            if (anim != null && anim.AnimationType == BattleAnimationManager.ResourceBarAnimationState.BarAnimationType.Loss)
            {
                for (int j = 0; j < maxHearts; j++)
                {
                    int vBefore = Math.Clamp((int)anim.ValueBefore - (j * 2), 0, 2);
                    int vAfter = Math.Clamp((int)anim.ValueAfter - (j * 2), 0, 2);
                    if (vBefore > vAfter) maxDamagedIndex = Math.Max(maxDamagedIndex, j);
                }
            }

            SpriteEffects spriteEffect = isRightAligned ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            for (int i = 0; i < maxHearts; i++)
            {
                float heartX = isRightAligned ? (position.X + width - ((i + 1) * stride) + heartGap) : (position.X + (i * stride));
                float heartY = position.Y;

                // --- 1. Damage Bounce Stagger ---
                if (maxDamagedIndex != -1)
                {
                    int vBefore = Math.Clamp((int)anim.ValueBefore - (i * 2), 0, 2);
                    int vAfter = Math.Clamp((int)anim.ValueAfter - (i * 2), 0, 2);

                    if (vBefore > vAfter && anim.CurrentLossPhase == BattleAnimationManager.ResourceBarAnimationState.LossPhase.Preview)
                    {
                        float delay = (maxDamagedIndex - i) * 0.06f;
                        float localTimer = anim.Timer - delay;
                        float popDuration = 0.25f;

                        if (localTimer > 0 && localTimer < popDuration)
                        {
                            float bounce = MathF.Sin((localTimer / popDuration) * MathF.PI) * -3f;
                            heartY += MathF.Round(bounce);
                        }
                    }
                }
                // --- 2. Idle Wave ---
                else if (combatant.IsHeartWaving)
                {
                    float waveCenter = combatant.HeartWaveProgress * (maxHearts + 2) - 1;
                    float dist = Math.Abs(waveCenter - i);
                    if (dist < 1.5f)
                    {
                        float waveAmt = MathF.Cos((dist / 1.5f) * MathHelper.PiOver2);
                        heartY += MathF.Round(-1f * waveAmt);
                    }
                }

                int heartValueBefore = Math.Clamp((anim != null ? (int)anim.ValueBefore : currentHP) - (i * 2), 0, 2);
                int heartValueAfter = Math.Clamp((anim != null ? (int)anim.ValueAfter : currentHP) - (i * 2), 0, 2);

                int frameIndex = 0;

                if (anim != null && anim.AnimationType == BattleAnimationManager.ResourceBarAnimationState.BarAnimationType.Loss && heartValueBefore > heartValueAfter)
                {
                    int damage = heartValueBefore - heartValueAfter;
                    int flashFrame = 3;
                    if (heartValueBefore == 2 && damage == 1) flashFrame = 4;
                    else if (heartValueBefore == 1 && damage == 1) flashFrame = 5;

                    bool showFlash = (anim.Timer % 0.2f) < 0.1f;

                    if (anim.CurrentLossPhase == BattleAnimationManager.ResourceBarAnimationState.LossPhase.Shrink)
                    {
                        frameIndex = heartValueAfter == 2 ? 0 : (heartValueAfter == 1 ? 1 : 2);
                    }
                    else
                    {
                        int baseFrame = heartValueBefore == 2 ? 0 : (heartValueBefore == 1 ? 1 : 2);
                        frameIndex = showFlash ? flashFrame : baseFrame;
                    }
                }
                else
                {
                    int val = (anim != null && anim.AnimationType == BattleAnimationManager.ResourceBarAnimationState.BarAnimationType.Loss && anim.CurrentLossPhase != BattleAnimationManager.ResourceBarAnimationState.LossPhase.Shrink) ? heartValueBefore : heartValueAfter;
                    frameIndex = val == 2 ? 0 : (val == 1 ? 1 : 2);
                }

                Rectangle sourceRect = new Rectangle(frameIndex * 5, 0, 5, 5);

                // Draw with extended parameters to apply horizontal flip
                spriteBatch.DrawSnapped(
                    _spriteManager.HealthHeartsSpriteSheet,
                    new Vector2(heartX, heartY),
                    sourceRect,
                    Color.White * alpha,
                    0f,
                    Vector2.Zero,
                    1f,
                    spriteEffect,
                    0f
                );
            }
        }

        private void DrawTenacityBar(SpriteBatch spriteBatch, BattleCombatant combatant, float startX, float startY, float barWidth, float alpha, bool isRightAligned)
        {
            if (alpha <= 0.01f) return;
            int maxGuard = combatant.MaxGuard;
            int currentGuard = combatant.CurrentGuard;

            const int pipSize = 3;
            const int gap = 1;
            var fullRect = new Rectangle(0, 0, 3, 3);
            var emptyRect = new Rectangle(3, 0, 3, 3);

            for (int i = 0; i < maxGuard; i++)
            {
                float x = isRightAligned ? (startX + barWidth - ((i + 1) * (pipSize + gap)) + gap) : (startX + (i * (pipSize + gap)));
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
            float iconY = startY + 6 + 2;

            int step = iconSize + gap;
            int frameIndex = (DateTime.Now.Millisecond < 500) ? 0 : 1;

            for (int i = 0; i < combatant.ActiveStatusEffects.Count; i++)
            {
                var effect = combatant.ActiveStatusEffects[i];
                if (!effect.IsPermanent) continue;

                float hopOffset = getOffsetFunc(combatant.CombatantID, effect.EffectType);
                bool isAnimating = isAnimatingFunc(combatant.CombatantID, effect.EffectType);

                float x = isRightAligned ? (startX + width - ((i + 1) * step) + gap) : (startX + (i * step));
                var iconPos = new Vector2(x, iconY + hopOffset);
                var iconBounds = new Rectangle((int)x, (int)(iconY + hopOffset), iconSize, iconSize);

                if (iconTracker != null) iconTracker.Add(new StatusIconInfo { Effect = effect, Bounds = iconBounds });

                var sourceRect = _spriteManager.GetPermanentStatusIconSourceRect(effect.EffectType, frameIndex);
                if (sourceRect != Rectangle.Empty)
                {
                    spriteBatch.DrawSnapped(_spriteManager.PermanentStatusIconsSpriteSheet, iconPos, sourceRect, Color.White);
                    if (isAnimating) spriteBatch.DrawSnapped(_spriteManager.PermanentStatusIconsSpriteSheet, iconPos, sourceRect, Color.White * 0.5f);
                }
            }
        }
    }
}
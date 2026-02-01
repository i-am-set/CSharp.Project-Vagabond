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

        public void DrawEnemyBars(SpriteBatch spriteBatch, BattleCombatant combatant, float barX, float barY, int barWidth, int barHeight, BattleAnimationManager animationManager, float hpAlpha, float manaAlpha, GameTime gameTime, bool isRightAligned = false)
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
            var barRect = new Rectangle((int)barX, (int)barY, barWidth, barHeight);
            float hpPercent = combatant.Stats.MaxHP > 0 ? Math.Clamp(combatant.VisualHP / combatant.Stats.MaxHP, 0f, 1f) : 0f;
            var hpAnim = animationManager.GetResourceBarAnimation(combatant.CombatantID, BattleAnimationManager.ResourceBarAnimationState.BarResourceType.HP);

            DrawBar(spriteBatch, barRect, hpPercent, _global.Palette_DarkShadow, _global.Palette_Leaf, _global.Palette_Black, hpAlpha, hpAnim, combatant.Stats.MaxHP, isRightAligned);

            // --- MANA BAR (Discrete) ---
            float manaBarY = barY + barHeight + 1;
            DrawDiscreteManaBar(spriteBatch, combatant, barX, manaBarY, barWidth, manaAlpha, null, isRightAligned);
        }

        private void DrawBar(SpriteBatch spriteBatch, Rectangle fullBarRect, float fillPercent, Color bgColor, Color fgColor, Color borderColor, float alpha, BattleAnimationManager.ResourceBarAnimationState? anim, float maxResource, bool isRightAligned)
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
                int fgX = isRightAligned ? (currentX + w - totalFgWidth) : currentX;
                spriteBatch.DrawSnapped(_pixel, new Rectangle(fgX, y, totalFgWidth, h), fgColor * alpha);
            }

            // Outline
            spriteBatch.DrawSnapped(_pixel, new Rectangle(currentX, y - 1, w, 1), borderColor * alpha);
            spriteBatch.DrawSnapped(_pixel, new Rectangle(currentX, y + h, w, 1), borderColor * alpha);
            spriteBatch.DrawSnapped(_pixel, new Rectangle(currentX - 1, y - 1, 1, h + 2), borderColor * alpha);
            spriteBatch.DrawSnapped(_pixel, new Rectangle(currentX + w, y - 1, 1, h + 2), borderColor * alpha);

            // Animation Overlay
            if (anim != null)
            {
                DrawBarAnimationOverlay(spriteBatch, fullBarRect, maxResource, anim, alpha, isRightAligned);
            }
        }

        private void DrawDiscreteManaBar(SpriteBatch spriteBatch, BattleCombatant combatant, float startX, float startY, float barWidth, float alpha, int? previewCost, bool isRightAligned)
        {
            if (alpha <= 0.01f) return;

            int maxMana = combatant.Stats.MaxMana;
            int currentMana = combatant.Stats.CurrentMana;

            const int pipSize = 1;
            const int gap = 1;
            const int pipHeight = 1;

            Color emptyColor = _global.Palette_DarkShadow * alpha;
            Color filledColor = _global.Palette_Sky * alpha;
            Color previewColor = _global.Palette_Sun * alpha;

            for (int i = 0; i < maxMana; i++)
            {
                float x;
                if (isRightAligned)
                {
                    // Start from right edge, move left
                    x = startX + barWidth - ((i + 1) * (pipSize + gap)) + gap;
                }
                else
                {
                    x = startX + (i * (pipSize + gap));
                }

                var rect = new Rectangle((int)x, (int)startY, pipSize, pipHeight);

                Color drawColor = emptyColor;

                if (i < currentMana)
                {
                    drawColor = filledColor;
                    if (previewCost.HasValue && previewCost.Value > 0)
                    {
                        int cost = previewCost.Value;
                        if (i >= (currentMana - cost))
                        {
                            drawColor = previewColor;
                        }
                    }
                }

                spriteBatch.DrawSnapped(_pixel, rect, drawColor);
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

                var destRect = new Rectangle((int)x, (int)startY, pipSize, pipSize);
                var sourceRect = (i < currentTenacity) ? fullRect : emptyRect;
                spriteBatch.DrawSnapped(_spriteManager.TenacityPipTexture, destRect, sourceRect, Color.White * alpha);
            }
        }

        public void DrawPlayerBars(SpriteBatch spriteBatch, BattleCombatant player, float barX, float barY, int barWidth, int barHeight, BattleAnimationManager animationManager, float hpAlpha, float manaAlpha, GameTime gameTime, BattleUIManager uiManager, bool isActiveActor, bool isRightAligned = false)
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

            var barRect = new Rectangle((int)barX, (int)barY, barWidth, barHeight);
            float hpPercent = player.Stats.MaxHP > 0 ? Math.Clamp(player.VisualHP / player.Stats.MaxHP, 0f, 1f) : 0f;
            var hpAnim = animationManager.GetResourceBarAnimation(player.CombatantID, BattleAnimationManager.ResourceBarAnimationState.BarResourceType.HP);

            DrawBar(spriteBatch, barRect, hpPercent, _global.Palette_DarkShadow, _global.Palette_Leaf, _global.Palette_Black, hpAlpha, hpAnim, player.Stats.MaxHP, isRightAligned);

            float manaBarY = barY + barHeight + 1;
            int? previewCost = null;

            if (isActiveActor && uiManager.HoveredMove != null)
            {
                if (player.ManaBarDisappearTimer <= 0 && player.ManaBarDelayTimer <= 0)
                {
                    var move = uiManager.HoveredMove;
                    int cost = 0;

                    if (move.AffectsUserMana)
                    {
                        // If ManaCost is 0 but AffectsUserMana is true, assume it's a dump (all mana)
                        cost = (move.ManaCost > 0) ? move.ManaCost : player.Stats.CurrentMana;
                    }

                    if (cost > 0)
                    {
                        previewCost = cost;
                    }
                }
            }

            DrawDiscreteManaBar(spriteBatch, player, barX, manaBarY, barWidth, manaAlpha, previewCost, isRightAligned);
        }

        public void DrawStatusIcons(SpriteBatch spriteBatch, BattleCombatant combatant, float startX, float startY, int width, bool isPlayer, List<StatusIconInfo> iconTracker, Func<string, StatusEffectType, float> getOffsetFunc, Func<string, StatusEffectType, bool> isAnimatingFunc, bool isRightAligned = false)
        {
            if (!combatant.ActiveStatusEffects.Any()) return;

            iconTracker?.Clear();

            int iconSize = BattleLayout.STATUS_ICON_SIZE;
            int gap = BattleLayout.STATUS_ICON_GAP;

            // Position below mana bar
            int iconY = (int)startY + BattleLayout.ENEMY_BAR_HEIGHT + 1 + 1 + 2;

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

                int x;
                if (isRightAligned)
                {
                    x = (int)(startX + width - ((i + 1) * step) + gap);
                }
                else
                {
                    x = (int)(startX + (i * step));
                }

                var iconBounds = new Rectangle(x, (int)(iconY + hopOffset), iconSize, iconSize);

                if (iconTracker != null)
                {
                    iconTracker.Add(new StatusIconInfo { Effect = effect, Bounds = iconBounds });
                }

                var sourceRect = _spriteManager.GetPermanentStatusIconSourceRect(effect.EffectType, frameIndex);

                if (sourceRect != Rectangle.Empty)
                {
                    spriteBatch.DrawSnapped(_spriteManager.PermanentStatusIconsSpriteSheet, iconBounds, sourceRect, Color.White);

                    if (isAnimating)
                    {
                        spriteBatch.DrawSnapped(_spriteManager.PermanentStatusIconsSpriteSheet, iconBounds, sourceRect, Color.White * 0.5f);
                    }
                }
            }
        }

        private void DrawBarAnimationOverlay(SpriteBatch spriteBatch, Rectangle bgRect, float maxResource, BattleAnimationManager.ResourceBarAnimationState anim, float alpha, bool isRightAligned)
        {
            float percentBefore = anim.ValueBefore / maxResource;
            float percentAfter = anim.ValueAfter / maxResource;

            int widthBefore = (int)(bgRect.Width * percentBefore);
            int widthAfter = (int)(bgRect.Width * percentAfter);

            if (percentAfter > 0 && widthAfter == 0) widthAfter = 1;
            if (percentBefore > 0 && widthBefore == 0) widthBefore = 1;

            int visibleStartX = bgRect.X;
            int visibleWidth = bgRect.Width;
            int visibleEndX = visibleStartX + visibleWidth;

            if (visibleWidth <= 0) return;

            if (anim.AnimationType == BattleAnimationManager.ResourceBarAnimationState.BarAnimationType.Loss)
            {
                int previewStartX;
                int previewWidth = widthBefore - widthAfter;

                if (isRightAligned)
                {
                    // Loss shrinks from Left to Right (Right side anchored)
                    // Before: [XXXXX] (5)
                    // After:  [..XXX] (3)
                    // Diff:   [XX...] (2) at Left
                    // StartX = bgRect.X + bgRect.Width - widthBefore
                    previewStartX = bgRect.X + bgRect.Width - widthBefore;
                }
                else
                {
                    // Loss shrinks from Right to Left (Left side anchored)
                    // Before: [XXXXX]
                    // After:  [XXX..]
                    // Diff:   [...XX]
                    previewStartX = bgRect.X + widthAfter;
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
                int ghostStartX;
                int ghostWidth = widthAfter - widthBefore;

                if (isRightAligned)
                {
                    // Recovery grows Leftwards.
                    // Before: [..XXX]
                    // After:  [XXXXX]
                    // Ghost:  [XX...]
                    // Ghost is at `bgRect.X + bgRect.Width - widthAfter`.
                    ghostStartX = bgRect.X + bgRect.Width - widthAfter;
                }
                else
                {
                    // Recovery grows Rightwards.
                    // Before: [XXX..]
                    // After:  [XXXXX]
                    // Ghost:  [...XX]
                    // Ghost is at `bgRect.X + widthBefore`.
                    ghostStartX = bgRect.X + widthBefore;
                }

                if (ghostWidth > 0)
                {
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
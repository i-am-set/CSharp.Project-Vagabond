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

        public void DrawEnemyBars(SpriteBatch spriteBatch, BattleCombatant combatant, float barX, float barY, int barWidth, int barHeight, BattleAnimationManager animationManager, float hpAlpha, float manaAlpha, GameTime gameTime)
        {
            // --- NAME ---
            var tertiaryFont = ServiceLocator.Get<Core>().TertiaryFont;
            if (hpAlpha > 0.01f)
            {
                spriteBatch.DrawStringSnapped(tertiaryFont, combatant.Name.ToUpper(), new Vector2(barX, barY - tertiaryFont.LineHeight - 1), _global.Palette_Sun * hpAlpha);
            }

            // --- HEALTH BAR ---
            var barRect = new Rectangle((int)barX, (int)barY, barWidth, barHeight);
            float hpPercent = combatant.Stats.MaxHP > 0 ? Math.Clamp(combatant.VisualHP / combatant.Stats.MaxHP, 0f, 1f) : 0f;
            var hpAnim = animationManager.GetResourceBarAnimation(combatant.CombatantID, BattleAnimationManager.ResourceBarAnimationState.BarResourceType.HP);

            DrawBar(spriteBatch, barRect, hpPercent, _global.Palette_DarkShadow, _global.Palette_Leaf, _global.Palette_Black, hpAlpha, hpAnim, combatant.Stats.MaxHP);

            // --- MANA BAR (Discrete) ---
            float manaBarY = barY + barHeight + 1;
            DrawDiscreteManaBar(spriteBatch, combatant, barX, manaBarY, manaAlpha, null);
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

        private void DrawDiscreteManaBar(SpriteBatch spriteBatch, BattleCombatant combatant, float startX, float startY, float alpha, int? previewCost)
        {
            if (alpha <= 0.01f) return;

            int maxMana = combatant.Stats.MaxMana;
            int currentMana = combatant.Stats.CurrentMana;

            // Tuning: 1x1 pixel, 1 pixel gap
            const int pipSize = 1;
            const int gap = 1;
            const int pipHeight = 1;

            // Colors
            Color emptyColor = _global.Palette_DarkShadow * alpha;
            Color filledColor = _global.Palette_Sky * alpha;
            Color previewColor = _global.Palette_Sun * alpha; // Highlight color for cost preview

            for (int i = 0; i < maxMana; i++)
            {
                float x = startX + (i * (pipSize + gap));
                var rect = new Rectangle((int)x, (int)startY, pipSize, pipHeight);

                Color drawColor = emptyColor;

                if (i < currentMana)
                {
                    drawColor = filledColor;

                    // Apply Preview Logic: Highlight the top-most pips that will be consumed
                    // We want to highlight 'cost' number of pips, starting from (currentMana - 1) downwards.
                    if (previewCost.HasValue && previewCost.Value > 0)
                    {
                        int cost = previewCost.Value;
                        // Highlight range: [currentMana - cost, currentMana - 1]
                        if (i >= (currentMana - cost))
                        {
                            drawColor = previewColor;
                        }
                    }
                }

                spriteBatch.DrawSnapped(_pixel, rect, drawColor);
            }
        }

        public void DrawPlayerBars(SpriteBatch spriteBatch, BattleCombatant player, float barX, float barY, int barWidth, int barHeight, BattleAnimationManager animationManager, float hpAlpha, float manaAlpha, GameTime gameTime, BattleUIManager uiManager, bool isActiveActor)
        {
            // --- NAME ---
            var tertiaryFont = ServiceLocator.Get<Core>().TertiaryFont;
            if (hpAlpha > 0.01f)
            {
                spriteBatch.DrawStringSnapped(tertiaryFont, player.Name.ToUpper(), new Vector2(barX, barY - tertiaryFont.LineHeight - 1), _global.Palette_Sun * hpAlpha);
            }

            // --- HEALTH BAR ---
            var barRect = new Rectangle((int)barX, (int)barY, barWidth, barHeight);
            float hpPercent = player.Stats.MaxHP > 0 ? Math.Clamp(player.VisualHP / player.Stats.MaxHP, 0f, 1f) : 0f;
            var hpAnim = animationManager.GetResourceBarAnimation(player.CombatantID, BattleAnimationManager.ResourceBarAnimationState.BarResourceType.HP);

            DrawBar(spriteBatch, barRect, hpPercent, _global.Palette_DarkShadow, _global.Palette_Leaf, _global.Palette_Black, hpAlpha, hpAnim, player.Stats.MaxHP);

            // --- MANA BAR (Discrete with Preview) ---
            float manaBarY = barY + barHeight + 1;
            int? previewCost = null;

            if (isActiveActor && uiManager.HoveredMove != null)
            {
                // Don't draw preview if collapsing or holding white or expanding
                if (player.ManaBarDisappearTimer <= 0 && player.ManaBarDelayTimer <= 0)
                {
                    var move = uiManager.HoveredMove;
                    var manaDump = move.Abilities.OfType<ManaDumpAbility>().FirstOrDefault();
                    int cost = move.ManaCost;

                    if (manaDump != null)
                    {
                        cost = player.Stats.CurrentMana;
                    }

                    if (cost > 0)
                    {
                        previewCost = cost;
                    }
                }
            }

            DrawDiscreteManaBar(spriteBatch, player, barX, manaBarY, manaAlpha, previewCost);
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
                        color = (anim.ResourceType == BattleAnimationManager.ResourceBarAnimationState.BarResourceType.HP) ? _global.Palette_Rust : _global.Palette_Sun;
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
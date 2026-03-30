using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.Animations;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Deliveries;
using ProjectVagabond.Particles;
using ProjectVagabond.Scenes;
using ProjectVagabond.Transitions;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;

namespace ProjectVagabond.Battle
{
    public class WizardRenderer
    {
        private readonly Global _global;
        private readonly Core _core;
        private readonly Texture2D _pixel;

        public WizardRenderer()
        {
            _global = ServiceLocator.Get<Global>();
            _core = ServiceLocator.Get<Core>();
            _pixel = ServiceLocator.Get<Texture2D>();
        }

        public void DrawUI(ArenaWizard wizard, SpriteBatch spriteBatch, SpriteManager spriteManager, GameTime gameTime)
        {
            var combat = wizard.Data.Combat;
            var ui = wizard.Data.UI;
            var stats = wizard.Data.Stats;

            int wizX = (int)MathF.Round(combat.Position.X);
            float hopOffset = combat.State == WizardState.Dead ? 0f : -MathF.Abs(MathF.Sin(ui.HopTimer)) * 4f;
            int wizY = (int)MathF.Round(combat.Position.Y + hopOffset);

            if (ui.MoveTextTimer > 0 && !string.IsNullOrEmpty(ui.ActiveMoveText) && combat.State != WizardState.Dead)
            {
                var font = _core.TertiaryFont;

                float timeElapsed = ui.MoveTextDuration - ui.MoveTextTimer;
                float scale = 1f;
                float alpha = 1f;
                float yOffset = 0f;
                float xOffset = 0f;
                Color textColor = ui.IsActiveMoveRare ? _global.Palette_Sun : _global.Palette_DarkestPale;

                if (ui.IsMoveCanceled)
                {
                    float progress = 1f - (ui.MoveTextTimer / ui.MoveTextDuration);
                    float ease = Easing.EaseOutCubic(progress);

                    yOffset = -ease * 5f;

                    float shakeDecay = 1f - progress;
                    xOffset = MathF.Sin(progress * 60f) * 5f * shakeDecay;

                    alpha = 1f - ease;
                    textColor = _global.Palette_Rust;
                }
                else
                {
                    float appearDuration = 0.15f;
                    float expireDuration = 0.2f;

                    if (timeElapsed < appearDuration)
                    {
                        scale = Easing.EaseOutBack(timeElapsed / appearDuration);
                    }
                    else if (ui.MoveTextTimer < expireDuration)
                    {
                        float shrinkProgress = 1f - (ui.MoveTextTimer / expireDuration);
                        scale = Math.Max(0f, 1f - Easing.EaseInBack(shrinkProgress));
                        alpha = (ui.MoveTextTimer / expireDuration);
                    }
                }

                if (scale > 0.01f && alpha > 0.01f)
                {
                    Vector2 textSize = font.MeasureString(ui.ActiveMoveText);
                    Vector2 textPos = new Vector2(wizX + xOffset, wizY - 16 + yOffset);
                    Vector2 origin = new Vector2(MathF.Round(textSize.X / 2f), MathF.Round(textSize.Y / 2f));

                    spriteBatch.DrawStringOutlinedSnapped(font, ui.ActiveMoveText, textPos, textColor * alpha, _global.Palette_Off * alpha, 0f, origin, scale, SpriteEffects.None, 0f);
                }
            }

            foreach (var ft in ui.FloatingTexts)
            {
                bool isFlash = (ft.Timer % 0.1f) > 0.05f;
                string text = (ft.IsHealing ? $"+{ft.Number}" : $"-{ft.Number}");

                BitmapFont font = _core.TertiaryFont;
                if (ft.Number >= 15)
                {
                    font = _core.DefaultFont;
                }
                else if (ft.Number >= 5)
                {
                    font = _core.SecondaryFont;
                }

                Color textColor = ft.IsHealing
                    ? (isFlash ? _global.Palette_Sun : _global.Palette_Leaf)
                    : (isFlash ? _global.Palette_Sun : _global.Palette_Rust);

                float alphaMult = Math.Clamp(ft.Timer / 0.2f, 0f, 1f);
                Color finalTextColor = textColor * alphaMult;
                Color outlineColor = _global.Palette_Off * alphaMult;

                Vector2 textPos = new Vector2(MathF.Round(combat.Position.X), MathF.Round(combat.Position.Y)) + ft.LocalOffset;
                Vector2 textSize = font.MeasureString(text);
                Vector2 origin = new Vector2(MathF.Round(textSize.X / 2f), MathF.Round(textSize.Y / 2f));

                spriteBatch.DrawStringOutlinedSnapped(font, text, textPos, finalTextColor, outlineColor, 0f, origin, 1f, SpriteEffects.None, 0f);

                if (ft.IsCrit)
                {
                    string critText = "CRIT";
                    BitmapFont critFont = _core.TertiaryFont;
                    Vector2 critSize = critFont.MeasureString(critText);

                    Vector2 critCenter = textPos - new Vector2(0, MathF.Round(textSize.Y / 2f + critSize.Y / 2f + 1));
                    Vector2 critTopLeft = new Vector2(MathF.Round(critCenter.X - critSize.X / 2f), MathF.Round(critCenter.Y - critSize.Y / 2f));

                    Color critTextColor = isFlash ? _global.Palette_Sun : _global.CritcalHitIndicatorColor;
                    Color finalCritTextColor = critTextColor * alphaMult;

                    TextAnimator.DrawTextWithEffectOutlined(
                        spriteBatch,
                        critFont,
                        critText,
                        critTopLeft,
                        finalCritTextColor,
                        outlineColor,
                        TextEffectType.Shake,
                        (float)gameTime.TotalGameTime.TotalSeconds,
                        Vector2.One,
                        0f
                    );
                }
            }

            if (combat.State == WizardState.Dead || ui.HealthBarAlpha <= 0f) return;

            var sheet = spriteManager.HealthHearts3x3SpriteSheet;
            if (sheet == null) return;

            int maxHearts = (stats.MaxHP + 2) / 3;
            int heartWidth = 3;
            int spacing = 1;
            int totalWidth = maxHearts * heartWidth + (maxHearts - 1) * spacing;

            int startX = wizX - (totalWidth / 2) - 1;
            int startY = wizY + 11;

            Color drawColor = Color.White * ui.HealthBarAlpha;

            for (int i = 0; i < maxHearts; i++)
            {
                int heartVal = Math.Clamp(stats.CurrentHP - i * 3, 0, 3);
                int frameIndex = 3;

                if (heartVal == 3) frameIndex = 0;
                else if (heartVal == 2) frameIndex = 1;
                else if (heartVal == 1) frameIndex = 2;

                int flashFrame = wizard.Controller.GetHeartFlashFrame(i);
                if (flashFrame != -1) frameIndex = flashFrame;

                var sourceRect = new Rectangle(frameIndex * heartWidth, 0, heartWidth, 3);

                int yOffset = 0;
                if (stats.CurrentHP > 0)
                {
                    float localWaveTime = ui.FloatingHeartWaveTimer - ui.FloatingHeartWaveInterval - (i * 0.08f);
                    if (localWaveTime > 0 && localWaveTime < 0.15f)
                    {
                        yOffset = -1;
                    }
                }

                Vector2 pos = new Vector2(startX + i * (heartWidth + spacing), startY + yOffset);

                spriteBatch.DrawSnapped(sheet, pos, sourceRect, drawColor);
            }
        }

        public void DrawDebug(ArenaWizard wizard, SpriteBatch spriteBatch, BattleContext context)
        {
            var combat = wizard.Data.Combat;

            if (_global.ShowDebugOverlays)
            {
                var hitbox = wizard.Controller.GetHitbox(context.SpriteManager);
                spriteBatch.Draw(_pixel, new Rectangle(hitbox.X, hitbox.Y, hitbox.Width, 1), Color.Lime * 0.5f);
                spriteBatch.Draw(_pixel, new Rectangle(hitbox.X, hitbox.Bottom - 1, hitbox.Width, 1), Color.Lime * 0.5f);
                spriteBatch.Draw(_pixel, new Rectangle(hitbox.X, hitbox.Y, 1, hitbox.Height), Color.Lime * 0.5f);
                spriteBatch.Draw(_pixel, new Rectangle(hitbox.Right - 1, hitbox.Y, 1, hitbox.Height), Color.Lime * 0.5f);
            }

            if (combat.State == WizardState.Telegraphing && combat.QueuedMove != null)
            {
                combat.QueuedMove.Delivery.DrawTelegraph(spriteBatch, combat.Position, combat.QueuedDirection, combat.QueuedTargetPos, context);
            }
        }

        public void DrawWizard(ArenaWizard wizard, SpriteBatch spriteBatch, SpriteManager spriteManager, Global global)
        {
            var combat = wizard.Data.Combat;
            var stats = wizard.Data.Stats;
            var ui = wizard.Data.UI;

            if (combat.IsTeleporting) return;

            var sheet = spriteManager.PlayerMasterSpriteSheet;
            if (sheet == null) return;

            Vector2 origin = new Vector2(16, 16);
            var sourceRect = spriteManager.GetPlayerSourceRect(stats.PortraitIndex, PlayerSpriteType.Portrait8x8);

            bool isDead = combat.State == WizardState.Dead;
            float hopOffset = isDead ? 0f : -MathF.Abs(MathF.Sin(ui.HopTimer)) * 4f;
            Vector2 drawPos = new Vector2(MathF.Round(combat.Position.X), MathF.Round(combat.Position.Y + hopOffset));
            float rotation = isDead ? MathHelper.PiOver2 : 0f;

            float alpha = wizard.Controller.GetDeathAlpha();
            Color color = isDead ? (Color.Gray * alpha) : Color.White;

            bool drawSilhouette = false;
            bool skipDraw = false;

            if (combat.InvincibilityTimer > 0 && !isDead)
            {
                float timeActive = combat.InvincibilityDuration - combat.InvincibilityTimer;
                int state = (int)(timeActive / 0.05f) % 3;

                if (state == 0) drawSilhouette = true;
                else if (state == 1) skipDraw = true;
            }

            SpriteEffects spriteEffects = combat.IsFacingRight ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            if (!skipDraw)
            {
                if (stats.IsPlayer && !isDead)
                {
                    var silhouette = spriteManager.PlayerMasterSpriteSheetSilhouette;
                    if (silhouette != null)
                    {
                        spriteBatch.DrawSnapped(silhouette, drawPos + new Vector2(-1, 0), sourceRect, global.Palette_Sun, rotation, origin, 1f, spriteEffects, 0f);
                        spriteBatch.DrawSnapped(silhouette, drawPos + new Vector2(1, 0), sourceRect, global.Palette_Sun, rotation, origin, 1f, spriteEffects, 0f);
                        spriteBatch.DrawSnapped(silhouette, drawPos + new Vector2(0, -1), sourceRect, global.Palette_Sun, rotation, origin, 1f, spriteEffects, 0f);
                        spriteBatch.DrawSnapped(silhouette, drawPos + new Vector2(0, 1), sourceRect, global.Palette_Sun, rotation, origin, 1f, spriteEffects, 0f);
                    }
                }

                if (drawSilhouette)
                {
                    var silhouette = spriteManager.PlayerMasterSpriteSheetSilhouette;
                    spriteBatch.DrawSnapped(silhouette ?? sheet, drawPos, sourceRect, global.Palette_Sun, rotation, origin, 1f, spriteEffects, 0f);
                }
                else
                {
                    spriteBatch.DrawSnapped(sheet, drawPos, sourceRect, color, rotation, origin, 1f, spriteEffects, 0f);
                }
            }

            if (combat.WardTimer > 0 && !isDead)
            {
                var circle = spriteManager.CircleTextureSprite;
                var ring = spriteManager.RingTextureSprite;
                if (circle != null)
                {
                    float duration = combat.EquippedActiveSpell?.Duration ?? 4.0f;
                    float timeActive = duration - combat.WardTimer;

                    float bubbleProgress = Math.Clamp(timeActive / 0.3f, 0f, 1f);
                    float popProgress = Math.Clamp(combat.WardTimer / 0.15f, 0f, 1f);

                    float scaleAnim = Easing.EaseOutBackSlight(bubbleProgress) * Easing.EaseInQuad(popProgress);

                    float wobbleX = MathF.Sin(timeActive * 5f) * 0.05f;
                    float wobbleY = MathF.Cos(timeActive * 7f) * 0.05f;

                    float hitProgress = Math.Clamp(combat.WardHitTimer / 0.4f, 0f, 1f);
                    float hitSquishX = MathF.Sin(hitProgress * MathHelper.Pi * 3f) * hitProgress * 0.4f;
                    float hitSquishY = -MathF.Sin(hitProgress * MathHelper.Pi * 3f) * hitProgress * 0.4f;

                    float targetRadius = 8f;
                    float currentRadiusX = Math.Max(0.1f, targetRadius * (scaleAnim + wobbleX + hitSquishX));
                    float currentRadiusY = Math.Max(0.1f, targetRadius * (scaleAnim + wobbleY + hitSquishY));

                    Vector2 currentCenter = drawPos;

                    Vector2 circleOrigin = new Vector2(circle.Width / 2f, circle.Height / 2f);
                    Vector2 scaleVec = new Vector2(currentRadiusX * 2f / circle.Width, currentRadiusY * 2f / circle.Height);

                    Color baseColor = Color.Lerp(global.Palette_Sky * 0.3f, Color.White * 0.7f, hitProgress);
                    spriteBatch.DrawSnapped(circle, currentCenter, null, baseColor, 0f, circleOrigin, scaleVec, SpriteEffects.None, 0f);

                    if (ring != null)
                    {
                        Vector2 ringOrigin = new Vector2(ring.Width / 2f, ring.Height / 2f);
                        Vector2 ringScaleVec = new Vector2(currentRadiusX * 2f / ring.Width, currentRadiusY * 2f / ring.Height);
                        Color ringColor = Color.Lerp(Color.White * 0.3f, Color.White, hitProgress);
                        spriteBatch.DrawSnapped(ring, currentCenter, null, ringColor, 0f, ringOrigin, ringScaleVec, SpriteEffects.None, 0f);

                        Vector2 highlightPos = currentCenter + new Vector2(-currentRadiusX * 0.4f, -currentRadiusY * 0.4f);
                        spriteBatch.DrawSnapped(circle, highlightPos, null, Color.White * 0.6f * scaleAnim, 0f, circleOrigin, scaleVec * 0.25f, SpriteEffects.None, 0f);
                    }
                }
            }
        }
    }
}
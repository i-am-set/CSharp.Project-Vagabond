using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectVagabond.Utils;
using System;

namespace ProjectVagabond.Battle.UI
{
    /// <summary>
    /// Handles the complex logic of drawing combatant sprites, including silhouettes,
    /// outlines, flattening for transparency, and hit flashes.
    /// </summary>
    public class BattleEntityRenderer
    {
        private readonly SpriteManager _spriteManager;
        private readonly Global _global;
        private readonly Core _core;
        // Render Target for flattening sprites to apply outlines/transparency correctly
        private readonly RenderTarget2D _flattenTarget;
        private const int FLATTEN_TARGET_SIZE = 256;
        private const int FLATTEN_MARGIN = 16;

        public BattleEntityRenderer()
        {
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _global = ServiceLocator.Get<Global>();
            _core = ServiceLocator.Get<Core>();

            _flattenTarget = new RenderTarget2D(
                _core.GraphicsDevice,
                FLATTEN_TARGET_SIZE,
                FLATTEN_TARGET_SIZE,
                false,
                SurfaceFormat.Color,
                DepthFormat.None,
                0,
                RenderTargetUsage.PreserveContents
            );
        }

        public void DrawEnemy(
            SpriteBatch spriteBatch,
            BattleCombatant enemy,
            Rectangle spriteRect,
            Vector2[] partOffsets,
            Vector2 shakeOffset,
            float finalAlpha,
            float silhouetteFactor,
            Color silhouetteColor,
            bool isHighlighted,
            Color? highlightColor,
            Color outlineColor,
            bool isFlashingWhite,
            Color tintColor,
            float scale,
            Matrix transform)
        {
            Texture2D enemySprite = _spriteManager.GetEnemySprite(enemy.ArchetypeId);
            Texture2D enemySilhouette = _spriteManager.GetEnemySpriteSilhouette(enemy.ArchetypeId);

            if (enemySprite == null) return;

            bool isMajor = _spriteManager.IsMajorEnemySprite(enemy.ArchetypeId);
            int spritePartSize = isMajor ? 96 : 64;
            int numParts = enemySprite.Width / spritePartSize;

            // --- COMPOSITE OUTLINE LOGIC ---
            // If we need a complex outline (not highlighted, not fully silhouetted), we generate it here.
            if (enemySilhouette != null && silhouetteFactor < 1.0f && !isHighlighted)
            {
                DrawCompositeOutline(spriteBatch, enemySilhouette, spriteRect, partOffsets, shakeOffset, finalAlpha, outlineColor, numParts, spritePartSize, transform);
            }

            // --- FLATTENING LOGIC ---
            // If alpha < 1.0, we draw to a render target first to prevent overlapping parts from doubling opacity.
            bool useFlattening = finalAlpha < 1.0f;

            if (useFlattening)
            {
                DrawFlattenedEnemy(spriteBatch, enemySprite, enemySilhouette, spriteRect, partOffsets, shakeOffset, finalAlpha, silhouetteFactor, silhouetteColor, isHighlighted, highlightColor, isFlashingWhite, numParts, spritePartSize, transform);
            }
            else
            {
                DrawDirectEnemy(spriteBatch, enemySprite, enemySilhouette, spriteRect, partOffsets, shakeOffset, tintColor, silhouetteFactor, silhouetteColor, isHighlighted, highlightColor, isFlashingWhite, scale, numParts, spritePartSize);
            }

            // --- TARGETING INDICATOR ---
            if (isHighlighted)
            {
                DrawTargetingIndicator(spriteBatch, enemy, spriteRect, shakeOffset, highlightColor);
            }
        }

        private void DrawCompositeOutline(SpriteBatch spriteBatch, Texture2D silhouette, Rectangle spriteRect, Vector2[] offsets, Vector2 shakeOffset, float finalAlpha, Color outlineColor, int numParts, int partSize, Matrix transform)
        {
            var currentRTs = _core.GraphicsDevice.GetRenderTargets();
            spriteBatch.End();
            _core.GraphicsDevice.SetRenderTarget(_flattenTarget);
            _core.GraphicsDevice.Clear(Color.Transparent);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);

            Vector2 rtBasePos = new Vector2(FLATTEN_MARGIN, FLATTEN_MARGIN);

            for (int i = 0; i < numParts; i++)
            {
                var sourceRect = new Rectangle(i * partSize, 0, partSize, partSize);
                var partOffset = offsets != null && i < offsets.Length ? offsets[i] : Vector2.Zero;
                spriteBatch.DrawSnapped(silhouette, rtBasePos + partOffset, sourceRect, Color.White);
            }

            spriteBatch.End();
            _core.GraphicsDevice.SetRenderTargets(currentRTs);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, transform);

            Vector2 screenDrawPos = new Vector2(spriteRect.X, spriteRect.Y) + shakeOffset - rtBasePos;
            Color cBlack = _global.Palette_Black * finalAlpha;
            Color cColored = outlineColor;

            // Draw 3-layer outline (Outer Black, Middle Color, Inner Black)
            // Layer 3: Outer Black (3px)
            DrawOffsets(spriteBatch, _flattenTarget, screenDrawPos, 3, cBlack);
            DrawOffsets(spriteBatch, _flattenTarget, screenDrawPos, 2, cBlack, true); // Diagonals

            // Layer 2: Middle Colored (2px)
            DrawOffsets(spriteBatch, _flattenTarget, screenDrawPos, 2, cColored);
            DrawOffsets(spriteBatch, _flattenTarget, screenDrawPos, 1, cColored, true);

            // Layer 1: Inner Black (1px)
            DrawOffsets(spriteBatch, _flattenTarget, screenDrawPos, 1, cBlack);
        }

        private void DrawOffsets(SpriteBatch sb, Texture2D tex, Vector2 basePos, int dist, Color c, bool diagonal = false)
        {
            if (diagonal)
            {
                sb.Draw(tex, basePos + new Vector2(-dist, -dist), c);
                sb.Draw(tex, basePos + new Vector2(dist, -dist), c);
                sb.Draw(tex, basePos + new Vector2(-dist, dist), c);
                sb.Draw(tex, basePos + new Vector2(dist, dist), c);
            }
            else
            {
                sb.Draw(tex, basePos + new Vector2(-dist, 0), c);
                sb.Draw(tex, basePos + new Vector2(dist, 0), c);
                sb.Draw(tex, basePos + new Vector2(0, -dist), c);
                sb.Draw(tex, basePos + new Vector2(0, dist), c);
            }
        }

        private void DrawFlattenedEnemy(SpriteBatch spriteBatch, Texture2D sprite, Texture2D silhouette, Rectangle spriteRect, Vector2[] offsets, Vector2 shakeOffset, float finalAlpha, float silhouetteFactor, Color silhouetteColor, bool isHighlighted, Color? highlightColor, bool isFlashingWhite, int numParts, int partSize, Matrix transform)
        {
            var currentRTs = _core.GraphicsDevice.GetRenderTargets();
            spriteBatch.End();
            _core.GraphicsDevice.SetRenderTarget(_flattenTarget);
            _core.GraphicsDevice.Clear(Color.Transparent);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);

            Vector2 rtOffset = new Vector2(FLATTEN_MARGIN, FLATTEN_MARGIN);

            for (int i = 0; i < numParts; i++)
            {
                var sourceRect = new Rectangle(i * partSize, 0, partSize, partSize);
                var partOffset = offsets != null && i < offsets.Length ? offsets[i] : Vector2.Zero;
                var localDrawPos = rtOffset + partOffset + shakeOffset;

                if (silhouetteFactor >= 1.0f && silhouette != null)
                {
                    spriteBatch.DrawSnapped(silhouette, localDrawPos, sourceRect, silhouetteColor);
                }
                else if (isHighlighted && silhouette != null)
                {
                    Color hColor = highlightColor ?? Color.Yellow;
                    spriteBatch.DrawSnapped(silhouette, localDrawPos, sourceRect, hColor);
                }
                else
                {
                    spriteBatch.DrawSnapped(sprite, localDrawPos, sourceRect, Color.White);
                    if (silhouetteFactor > 0f && silhouette != null)
                    {
                        spriteBatch.DrawSnapped(silhouette, localDrawPos, sourceRect, silhouetteColor * silhouetteFactor);
                    }
                }

                if (isFlashingWhite && silhouette != null)
                {
                    spriteBatch.DrawSnapped(silhouette, localDrawPos, sourceRect, Color.White * 0.8f);
                }
            }

            spriteBatch.End();
            _core.GraphicsDevice.SetRenderTargets(currentRTs);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, transform);

            Vector2 drawPos = spriteRect.Location.ToVector2() - new Vector2(FLATTEN_MARGIN, FLATTEN_MARGIN);
            var srcRect = new Rectangle(0, 0, partSize + FLATTEN_MARGIN * 2, partSize + FLATTEN_MARGIN * 2);
            spriteBatch.Draw(_flattenTarget, drawPos, srcRect, Color.White * finalAlpha);
        }

        private void DrawDirectEnemy(SpriteBatch spriteBatch, Texture2D sprite, Texture2D silhouette, Rectangle spriteRect, Vector2[] offsets, Vector2 shakeOffset, Color tintColor, float silhouetteFactor, Color silhouetteColor, bool isHighlighted, Color? highlightColor, bool isFlashingWhite, float scale, int numParts, int partSize)
        {
            for (int i = 0; i < numParts; i++)
            {
                var sourceRect = new Rectangle(i * partSize, 0, partSize, partSize);
                var partOffset = offsets != null && i < offsets.Length ? offsets[i] : Vector2.Zero;
                var drawPosition = new Vector2(spriteRect.X + partOffset.X, spriteRect.Y + partOffset.Y) + shakeOffset;

                Vector2 origin = new Vector2(partSize / 2f, partSize / 2f);
                Vector2 centerPos = drawPosition + origin;

                if (silhouetteFactor >= 1.0f && silhouette != null)
                {
                    // FIX: Parentheses added to ensure float division happens before Color multiplication
                    spriteBatch.DrawSnapped(silhouette, centerPos, sourceRect, silhouetteColor * (tintColor.A / 255f), 0f, origin, scale, SpriteEffects.None, 0f);
                }
                else if (isHighlighted && silhouette != null)
                {
                    Color hColor = highlightColor ?? Color.Yellow;
                    // FIX: Parentheses added
                    spriteBatch.DrawSnapped(silhouette, centerPos, sourceRect, hColor * (tintColor.A / 255f), 0f, origin, scale, SpriteEffects.None, 0f);
                }
                else
                {
                    spriteBatch.DrawSnapped(sprite, centerPos, sourceRect, tintColor, 0f, origin, scale, SpriteEffects.None, 0f);
                    if (silhouetteFactor > 0f && silhouette != null)
                    {
                        // FIX: Parentheses added
                        spriteBatch.DrawSnapped(silhouette, centerPos, sourceRect, silhouetteColor * silhouetteFactor * (tintColor.A / 255f), 0f, origin, scale, SpriteEffects.None, 0f);
                    }
                }

                if (isFlashingWhite && silhouette != null)
                {
                    spriteBatch.DrawSnapped(silhouette, centerPos, sourceRect, Color.White * 0.8f, 0f, origin, scale, SpriteEffects.None, 0f);
                }
            }
        }

        private void DrawTargetingIndicator(SpriteBatch spriteBatch, BattleCombatant combatant, Rectangle spriteRect, Vector2 shakeOffset, Color? highlightColor)
        {
            var indicator = _spriteManager.TargetingIndicatorSprite;
            if (indicator != null && highlightColor == Color.Yellow)
            {
                Vector2 visualCenterOffset = _spriteManager.GetVisualCenterOffset(combatant.ArchetypeId);
                Vector2 spriteCenter = new Vector2(spriteRect.Center.X, spriteRect.Center.Y);
                Vector2 targetCenter = new Vector2(spriteCenter.X, spriteCenter.Y + visualCenterOffset.Y);

                // Simple sway calculation
                float t = (float)DateTime.Now.TimeOfDay.TotalSeconds * _global.TargetIndicatorNoiseSpeed;
                float swayX = (float)Math.Sin(t) * _global.TargetIndicatorOffsetX;
                float swayY = (float)Math.Cos(t * 1.3f) * _global.TargetIndicatorOffsetY;

                Vector2 animatedPos = targetCenter + new Vector2(swayX, swayY) + shakeOffset;
                Vector2 origin = new Vector2(indicator.Width / 2f, indicator.Height / 2f);

                spriteBatch.DrawSnapped(indicator, animatedPos, null, Color.White, 0f, origin, 1.0f, SpriteEffects.None, 0f);
            }
        }
    }
}
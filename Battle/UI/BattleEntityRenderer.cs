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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

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

        // INCREASED: Size to accommodate high-res scaling (e.g. 5x scale of 96px sprite = 480px)
        private const int FLATTEN_TARGET_SIZE = 1024;
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
            Vector2 position,
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
            Vector2 scale,
            Matrix transform,
            Color? lowHealthOverlay = null)
        {
            Texture2D enemySprite = _spriteManager.GetEnemySprite(enemy.ArchetypeId);
            Texture2D enemySilhouette = _spriteManager.GetEnemySpriteSilhouette(enemy.ArchetypeId);

            if (enemySprite == null) return;

            bool isMajor = _spriteManager.IsMajorEnemySprite(enemy.ArchetypeId);
            int spritePartSize = isMajor ? 96 : 64;
            int numParts = enemySprite.Width / spritePartSize;

            // --- COMPOSITE OUTLINE LOGIC ---
            // Always draw the composite outline if a silhouette exists.
            if (enemySilhouette != null)
            {
                DrawCompositeOutline(spriteBatch, enemySilhouette, position, partOffsets, shakeOffset, finalAlpha, outlineColor, numParts, spritePartSize, transform, scale);
            }

            // --- FLATTENING LOGIC ---
            // If alpha < 1.0, we draw to a render target first to prevent overlapping parts from doubling opacity.
            bool useFlattening = finalAlpha < 1.0f;

            if (useFlattening)
            {
                DrawFlattenedEnemy(spriteBatch, enemySprite, enemySilhouette, position, partOffsets, shakeOffset, finalAlpha, silhouetteFactor, silhouetteColor, isHighlighted, highlightColor, isFlashingWhite, numParts, spritePartSize, transform, lowHealthOverlay, scale);
            }
            else
            {
                // Pass Color.Transparent for the outline override since we drew the composite one above.
                DrawDirectEnemy(spriteBatch, enemySprite, enemySilhouette, position, partOffsets, shakeOffset, tintColor, silhouetteFactor, silhouetteColor, isHighlighted, highlightColor, isFlashingWhite, scale, numParts, spritePartSize, Color.Transparent, lowHealthOverlay);
            }
        }

        private void DrawCompositeOutline(SpriteBatch spriteBatch, Texture2D silhouette, Vector2 position, Vector2[] offsets, Vector2 shakeOffset, float finalAlpha, Color outlineColor, int numParts, int partSize, Matrix transform, Vector2 scale)
        {
            var currentRTs = _core.GraphicsDevice.GetRenderTargets();
            spriteBatch.End();
            _core.GraphicsDevice.SetRenderTarget(_flattenTarget);
            _core.GraphicsDevice.Clear(Color.Transparent);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);

            Vector2 rtBasePos = new Vector2(FLATTEN_MARGIN, FLATTEN_MARGIN);
            Vector2 origin = new Vector2(partSize / 2f, partSize / 2f);

            for (int i = 0; i < numParts; i++)
            {
                var sourceRect = new Rectangle(i * partSize, 0, partSize, partSize);
                var partOffset = offsets != null && i < offsets.Length ? offsets[i] : Vector2.Zero;

                // Draw to RT with scale applied
                spriteBatch.DrawSnapped(silhouette, rtBasePos + partOffset + origin, sourceRect, Color.White, 0f, origin, scale, SpriteEffects.None, 0f);
            }

            spriteBatch.End();
            _core.GraphicsDevice.SetRenderTargets(currentRTs);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, transform);

            Vector2 screenDrawPos = position + shakeOffset - rtBasePos;
            Color cInner = _global.Palette_Black * finalAlpha;
            Color cOuter = outlineColor;

            // Draw 2-layer outline (Outer Color, Inner Black)
            // Note: The texture in _flattenTarget is already scaled, so we draw it at 1.0 scale here.

            // Layer 2: Outer Color
            DrawOffsets(spriteBatch, _flattenTarget, screenDrawPos, 2, cOuter); // Cardinals
            DrawOffsets(spriteBatch, _flattenTarget, screenDrawPos, 1, cOuter, true); // Diagonals

            // Layer 1: Inner Black
            DrawOffsets(spriteBatch, _flattenTarget, screenDrawPos, 1, cInner);
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

        private void DrawFlattenedEnemy(SpriteBatch spriteBatch, Texture2D sprite, Texture2D silhouette, Vector2 position, Vector2[] offsets, Vector2 shakeOffset, float finalAlpha, float silhouetteFactor, Color silhouetteColor, bool isHighlighted, Color? highlightColor, bool isFlashingWhite, int numParts, int partSize, Matrix transform, Color? lowHealthOverlay, Vector2 scale)
        {
            var currentRTs = _core.GraphicsDevice.GetRenderTargets();
            spriteBatch.End();
            _core.GraphicsDevice.SetRenderTarget(_flattenTarget);
            _core.GraphicsDevice.Clear(Color.Transparent);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);

            Vector2 rtOffset = new Vector2(FLATTEN_MARGIN, FLATTEN_MARGIN);
            Vector2 origin = new Vector2(partSize / 2f, partSize / 2f);

            for (int i = 0; i < numParts; i++)
            {
                var sourceRect = new Rectangle(i * partSize, 0, partSize, partSize);
                var partOffset = offsets != null && i < offsets.Length ? offsets[i] : Vector2.Zero;

                // Center position for scaling
                var localDrawPos = rtOffset + partOffset + shakeOffset + origin;

                if (silhouetteFactor >= 1.0f && silhouette != null)
                {
                    spriteBatch.DrawSnapped(silhouette, localDrawPos, sourceRect, silhouetteColor, 0f, origin, scale, SpriteEffects.None, 0f);
                }
                else if (isHighlighted && silhouette != null)
                {
                    Color hColor = highlightColor ?? Color.Yellow;
                    spriteBatch.DrawSnapped(silhouette, localDrawPos, sourceRect, hColor, 0f, origin, scale, SpriteEffects.None, 0f);
                }
                else
                {
                    spriteBatch.DrawSnapped(sprite, localDrawPos, sourceRect, Color.White, 0f, origin, scale, SpriteEffects.None, 0f);
                    if (silhouetteFactor > 0f && silhouette != null)
                    {
                        spriteBatch.DrawSnapped(silhouette, localDrawPos, sourceRect, silhouetteColor * silhouetteFactor, 0f, origin, scale, SpriteEffects.None, 0f);
                    }
                }

                if (isFlashingWhite && silhouette != null)
                {
                    spriteBatch.DrawSnapped(silhouette, localDrawPos, sourceRect, Color.White * 0.8f, 0f, origin, scale, SpriteEffects.None, 0f);
                }

                // Draw Low Health Overlay
                if (lowHealthOverlay.HasValue && silhouette != null)
                {
                    spriteBatch.DrawSnapped(silhouette, localDrawPos, sourceRect, lowHealthOverlay.Value, 0f, origin, scale, SpriteEffects.None, 0f);
                }
            }

            spriteBatch.End();
            _core.GraphicsDevice.SetRenderTargets(currentRTs);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, transform);

            Vector2 drawPos = position - new Vector2(FLATTEN_MARGIN, FLATTEN_MARGIN);
            // Note: The RT content is already scaled, so we draw the RT at 1.0 scale
            var srcRect = new Rectangle(0, 0, _flattenTarget.Width, _flattenTarget.Height);
            spriteBatch.Draw(_flattenTarget, drawPos, srcRect, Color.White * finalAlpha);
        }

        private void DrawDirectEnemy(SpriteBatch spriteBatch, Texture2D sprite, Texture2D silhouette, Vector2 position, Vector2[] offsets, Vector2 shakeOffset, Color tintColor, float silhouetteFactor, Color silhouetteColor, bool isHighlighted, Color? highlightColor, bool isFlashingWhite, Vector2 scale, int numParts, int partSize, Color? outlineColorOverride, Color? lowHealthOverlay)
        {
            Color outlineColor = (outlineColorOverride ?? _global.Palette_DarkShadow) * (tintColor.A / 255f);

            for (int i = 0; i < numParts; i++)
            {
                var sourceRect = new Rectangle(i * partSize, 0, partSize, partSize);
                var partOffset = offsets != null && i < offsets.Length ? offsets[i] : Vector2.Zero;

                var drawPosition = position + partOffset + shakeOffset;

                Vector2 origin = new Vector2(partSize / 2f, partSize / 2f);
                Vector2 centerPos = drawPosition + origin;

                // --- Draw Outline ---
                if (silhouette != null && outlineColor.A > 0)
                {
                    spriteBatch.DrawSnapped(silhouette, centerPos + new Vector2(-1, 0), sourceRect, outlineColor, 0f, origin, scale, SpriteEffects.None, 0.49f);
                    spriteBatch.DrawSnapped(silhouette, centerPos + new Vector2(1, 0), sourceRect, outlineColor, 0f, origin, scale, SpriteEffects.None, 0.49f);
                    spriteBatch.DrawSnapped(silhouette, centerPos + new Vector2(0, -1), sourceRect, outlineColor, 0f, origin, scale, SpriteEffects.None, 0.49f);
                    spriteBatch.DrawSnapped(silhouette, centerPos + new Vector2(0, 1), sourceRect, outlineColor, 0f, origin, scale, SpriteEffects.None, 0.49f);
                }

                if (silhouetteFactor >= 1.0f && silhouette != null)
                {
                    spriteBatch.DrawSnapped(silhouette, centerPos, sourceRect, silhouetteColor * (tintColor.A / 255f), 0f, origin, scale, SpriteEffects.None, 0f);
                }
                else if (isHighlighted && silhouette != null)
                {
                    Color hColor = highlightColor ?? Color.Yellow;
                    spriteBatch.DrawSnapped(silhouette, centerPos, sourceRect, hColor * (tintColor.A / 255f), 0f, origin, scale, SpriteEffects.None, 0f);
                }
                else
                {
                    spriteBatch.DrawSnapped(sprite, centerPos, sourceRect, tintColor, 0f, origin, scale, SpriteEffects.None, 0f);
                    if (silhouetteFactor > 0f && silhouette != null)
                    {
                        spriteBatch.DrawSnapped(silhouette, centerPos, sourceRect, silhouetteColor * silhouetteFactor * (tintColor.A / 255f), 0f, origin, scale, SpriteEffects.None, 0f);
                    }
                }

                if (isFlashingWhite && silhouette != null)
                {
                    spriteBatch.DrawSnapped(silhouette, centerPos, sourceRect, Color.White * 0.8f, 0f, origin, scale, SpriteEffects.None, 0f);
                }

                // Draw Low Health Overlay
                if (lowHealthOverlay.HasValue && silhouette != null)
                {
                    spriteBatch.DrawSnapped(silhouette, centerPos, sourceRect, lowHealthOverlay.Value * (tintColor.A / 255f), 0f, origin, scale, SpriteEffects.None, 0f);
                }
            }
        }
    }
}
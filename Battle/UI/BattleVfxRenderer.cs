using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Particles;
using ProjectVagabond.Scenes;
using ProjectVagabond.Transitions;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ProjectVagabond.Battle.UI
{
    /// <summary>
    /// Handles visual effects in battle that are not strictly part of the entity or the HUD,
    /// such as floor rendering and tooltips.
    /// </summary>
    public class BattleVfxRenderer
    {
        private readonly SpriteManager _spriteManager;
        private readonly Global _global;
        private readonly Core _core;

        public BattleVfxRenderer()
        {
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _global = ServiceLocator.Get<Global>();
            _core = ServiceLocator.Get<Core>();
        }

        // CHANGED: Added alpha parameter
        public void DrawFloor(SpriteBatch spriteBatch, Vector2 slotCenter, float groundY, float scale = 1.0f, float alpha = 1.0f)
        {
            if (_spriteManager.BattleEnemyFloorSprite != null && scale > 0.01f && alpha > 0.01f)
            {
                Vector2 floorOrigin = new Vector2(_spriteManager.BattleEnemyFloorSprite.Width / 2f, _spriteManager.BattleEnemyFloorSprite.Height / 2f);
                spriteBatch.DrawSnapped(_spriteManager.BattleEnemyFloorSprite, new Vector2(slotCenter.X, groundY), null, Color.White * alpha, 0f, floorOrigin, scale, SpriteEffects.None, 0f);
            }
        }

        public void DrawStatChangeTooltip(SpriteBatch spriteBatch, BattleCombatant combatant, float alpha, bool hasInsight, Vector2 visualCenter, float barBottomY, GameTime gameTime)
        {
            var tertiaryFont = _core.TertiaryFont;
            var icons = _spriteManager.StatChangeIconsSpriteSheet;
            var iconSilhouette = _spriteManager.StatChangeIconsSpriteSheetSilhouette;
            var iconRects = _spriteManager.StatChangeIconSourceRects;

            if (icons == null || iconRects == null || iconSilhouette == null) return;

            const int width = 55;
            const int height = 28;
            const int rowHeight = 7;
            const int iconSize = 3;
            const int iconGap = 1;

            // Apply Global Left Shift (was -6, moved right by 4 -> -2)
            int xPos = (int)(visualCenter.X - width / 2) - 2;

            // Position exactly 1 pixel below the mana bar
            float yPos = barBottomY + 1;

            var bounds = new Rectangle(xPos, (int)yPos, width, height);

            string[] statLabels = { "STR", "INT", "TEN", "AGI" };
            OffensiveStatType[] statTypes = { OffensiveStatType.Strength, OffensiveStatType.Intelligence, OffensiveStatType.Tenacity, OffensiveStatType.Agility };

            // Use Palette_Sun for all labels to match the value text
            Color labelColor = _global.Palette_Sun;

            float time = (float)gameTime.TotalGameTime.TotalSeconds;

            // --- Icon Wave Animation Constants ---
            const float WAVE_SPEED = 6f; // Icons per second (Fast ripple)
            const float WAVE_WIDTH = 1f;  // Width of the wave in icons (1 full row)
            const float WAVE_HEIGHT = 1f; // Pixel height of the jump
            const float WAVE_PAUSE = 0f; // Gap in "icon units" before loop repeats (approx 0.5s pause)

            float totalSequenceLength = 24f + WAVE_PAUSE; // 4 rows * 6 icons + gap
            float waveCursor = (time * WAVE_SPEED) % totalSequenceLength;

            for (int i = 0; i < 4; i++)
            {
                int rowY = bounds.Y + (i * rowHeight);
                int effectiveValue = 0;
                switch (statTypes[i])
                {
                    case OffensiveStatType.Strength: effectiveValue = combatant.GetEffectiveStrength(); break;
                    case OffensiveStatType.Intelligence: effectiveValue = combatant.GetEffectiveIntelligence(); break;
                    case OffensiveStatType.Tenacity: effectiveValue = combatant.GetEffectiveTenacity(); break;
                    case OffensiveStatType.Agility: effectiveValue = combatant.GetEffectiveAgility(); break;
                }

                string valueText = (combatant.IsPlayerControlled || hasInsight) ? effectiveValue.ToString() : "??";
                Vector2 valueSize = tertiaryFont.MeasureString(valueText);
                float valueX = bounds.X + 14 - valueSize.X;

                // --- Stat Value Bob Animation ---
                // Stagger the bob based on the row index 'i'
                float bobSpeed = 2f;
                float bobOffset = (MathF.Sin(time * bobSpeed + (i * 0.8f)) > 0) ? -1f : 0f;

                spriteBatch.DrawStringSquareOutlinedSnapped(tertiaryFont, valueText, new Vector2(valueX, rowY + 1 + bobOffset), _global.Palette_Sun * alpha, _global.Palette_Black * alpha);
                spriteBatch.DrawStringSquareOutlinedSnapped(tertiaryFont, statLabels[i], new Vector2(bounds.X + 16, rowY + 1), labelColor * alpha, _global.Palette_Black * alpha);

                int stage = combatant.StatStages[statTypes[i]];
                int absStage = Math.Abs(stage);
                bool isPositive = stage > 0;
                int startIconX = bounds.X + 29;
                int iconY = rowY + 1;

                for (int j = 0; j < 6; j++)
                {
                    int iconIndex = 0;
                    if (j < absStage) iconIndex = isPositive ? 1 : 2;

                    // --- Calculate Wave Offset ---
                    int globalIconIndex = (i * 6) + j; // 0 to 23
                    float dist = waveCursor - globalIconIndex;
                    float iconWaveOffset = 0f;

                    if (dist > 0 && dist < WAVE_WIDTH)
                    {
                        // Sine wave hump: sin(0..pi)
                        float progress = dist / WAVE_WIDTH;
                        iconWaveOffset = -MathF.Sin(progress * MathHelper.Pi) * WAVE_HEIGHT;
                    }

                    var destRect = new Rectangle(startIconX + (j * (iconSize + iconGap)), (int)(iconY + iconWaveOffset), iconSize, iconSize);
                    var sourceRect = iconRects[iconIndex];

                    // Draw Outline
                    Color outlineColor = _global.Palette_Black * alpha;
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        for (int oy = -1; oy <= 1; oy++)
                        {
                            if (ox == 0 && oy == 0) continue;
                            spriteBatch.DrawSnapped(iconSilhouette, new Rectangle(destRect.X + ox, destRect.Y + oy, iconSize, iconSize), sourceRect, outlineColor);
                        }
                    }

                    // Draw Icon
                    spriteBatch.DrawSnapped(icons, destRect, sourceRect, Color.White * alpha);
                }
            }
        }
    }
}
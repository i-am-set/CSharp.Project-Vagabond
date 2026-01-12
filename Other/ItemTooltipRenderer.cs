using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Dice;
using ProjectVagabond.Items;
using ProjectVagabond.Particles;
using ProjectVagabond.Progression;
using ProjectVagabond.Scenes;
using ProjectVagabond.Systems;
using ProjectVagabond.Transitions;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;

namespace ProjectVagabond.UI
{
    public class ItemTooltipRenderer
    {
        private readonly Global _global;
        private readonly SpriteManager _spriteManager;
        private readonly Core _core;

        // --- TUNING ---
        private const int MIN_TOOLTIP_WIDTH = 120;
        private const int SPACE_WIDTH = 5;

        // Tunable buffer padding for width calculation
        private const int SIDE_PADDING = 6;

        // Tunable distance from the panel center to the center of each stat column
        private const float COLUMN_CENTER_OFFSET = 24f;

        // Margin to keep from the screen edge
        private const int SCREEN_EDGE_MARGIN = 10;

        // --- UNIFIED LAYOUT CONSTANTS ---
        private const int LAYOUT_SPRITE_Y = 6;
        private const int LAYOUT_TITLE_Y = 30;
        private const int LAYOUT_VARS_START_Y = 44;
        private const int LAYOUT_VAR_ROW_HEIGHT = 9; // 7px font + 2px gap
        private const int LAYOUT_CONTACT_Y = LAYOUT_VARS_START_Y + (3 * LAYOUT_VAR_ROW_HEIGHT); // Row 4
        private const int LAYOUT_DESC_START_Y = LAYOUT_CONTACT_Y + LAYOUT_VAR_ROW_HEIGHT - 3;

        public ItemTooltipRenderer()
        {
            _global = ServiceLocator.Get<Global>();
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _core = ServiceLocator.Get<Core>();
        }

        /// <summary>
        /// Draws a tooltip for the given item data.
        /// The tooltip attempts to align its internal sprite center with the anchorPosition.
        /// However, it is clamped to the screen bounds to ensure visibility.
        /// </summary>
        /// <param name="anchorPosition">The screen-space position to align the tooltip's icon center to.</param>
        public void DrawTooltip(SpriteBatch spriteBatch, object itemData, Vector2 anchorPosition, GameTime gameTime, Vector2? scale = null, float opacity = 1.0f)
        {
            if (itemData == null || opacity <= 0.01f) return;

            Vector2 drawScale = scale ?? Vector2.One;

            var font = ServiceLocator.Get<BitmapFont>();
            var secondaryFont = _core.SecondaryFont;

            // 1. Calculate Dynamic Width
            int panelWidth = CalculateDynamicWidth(itemData, font);

            // 2. Calculate Required Height
            float contentHeight = MeasureContentHeight(font, secondaryFont, itemData, panelWidth);
            int panelHeight = (int)Math.Ceiling(contentHeight) + 8 + 2;

            // 3. Determine the Y offset of the sprite center relative to the panel top.
            float spriteCenterOffsetY = GetSpriteCenterOffsetY(itemData);

            // 4. Calculate Ideal Panel Position (Perfect Alignment)
            float panelY = anchorPosition.Y - spriteCenterOffsetY;
            float panelX = anchorPosition.X - (panelWidth / 2f);

            // 5. Create Rectangle
            var infoPanelArea = new Rectangle(
                (int)MathF.Round(panelX),
                (int)MathF.Round(panelY),
                panelWidth,
                panelHeight
            );

            // 6. CLAMP TO SCREEN BOUNDS
            int screenTop = SCREEN_EDGE_MARGIN;
            int screenBottom = Global.VIRTUAL_HEIGHT - SCREEN_EDGE_MARGIN;
            int screenLeft = SCREEN_EDGE_MARGIN;
            int screenRight = Global.VIRTUAL_WIDTH - SCREEN_EDGE_MARGIN;

            if (infoPanelArea.X < screenLeft) infoPanelArea.X = screenLeft;
            if (infoPanelArea.Right > screenRight) infoPanelArea.X = screenRight - infoPanelArea.Width;
            if (infoPanelArea.Y < screenTop) infoPanelArea.Y = screenTop;
            if (infoPanelArea.Bottom > screenBottom) infoPanelArea.Y = screenBottom - infoPanelArea.Height;

            // 7. Prepare Animation Matrix
            spriteBatch.End();

            Vector2 pivotPoint = anchorPosition;

            Matrix transform = Matrix.CreateTranslation(-pivotPoint.X, -pivotPoint.Y, 0) *
                               Matrix.CreateScale(drawScale.X, drawScale.Y, 1.0f) *
                               Matrix.CreateTranslation(pivotPoint.X, pivotPoint.Y, 0);

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, transform);

            // 8. Draw Background (Rounded Bevel)
            var pixel = ServiceLocator.Get<Texture2D>();
            DrawBeveledBackground(spriteBatch, pixel, infoPanelArea, _global.Palette_Black * opacity);

            // 9. Draw Border (Rounded Bevel)
            DrawBeveledBorder(spriteBatch, pixel, infoPanelArea, _global.Palette_BlueWhite * opacity);

            // 10. Draw Content
            DrawInfoPanelContent(spriteBatch, itemData, infoPanelArea, font, secondaryFont, gameTime, opacity);

            // 11. Restore Batch
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
        }

        private float GetSpriteCenterOffsetY(object itemData)
        {
            if (itemData is MoveData)
            {
                return (LAYOUT_SPRITE_Y - 3) + 16; // 19
            }
            else
            {
                return (LAYOUT_SPRITE_Y + 2) + 8; // 16
            }
        }

        private List<string> WrapName(string name)
        {
            var words = name.Split(' ');
            var lines = new List<string>();
            string currentLine = "";

            foreach (var word in words)
            {
                if ((currentLine + (currentLine.Length > 0 ? " " : "") + word).Length > 12)
                {
                    if (!string.IsNullOrEmpty(currentLine))
                    {
                        lines.Add(currentLine);
                        currentLine = "";
                    }
                }

                if (currentLine.Length > 0) currentLine += " ";
                currentLine += word;
            }
            if (!string.IsNullOrEmpty(currentLine)) lines.Add(currentLine);

            return lines;
        }

        private int CalculateDynamicWidth(object itemData, BitmapFont font)
        {
            string name = "";
            if (itemData is MoveData m) name = m.MoveName;
            else if (itemData is WeaponData w) name = w.WeaponName;
            else if (itemData is ArmorData a) name = a.ArmorName;
            else if (itemData is RelicData r) name = r.RelicName;
            else if (itemData is ConsumableItemData c) name = c.ItemName;
            else if (itemData is MiscItemData misc) name = misc.ItemName;

            var lines = WrapName(name.ToUpper());

            float maxLineWidth = 0;
            foreach (var line in lines)
            {
                float w = font.MeasureString(line).Width;
                if (w > maxLineWidth) maxLineWidth = w;
            }

            int calculatedWidth = (int)Math.Ceiling(maxLineWidth) + (SIDE_PADDING * 2);
            return Math.Max(MIN_TOOLTIP_WIDTH, calculatedWidth);
        }

        public void DrawInfoPanelContent(SpriteBatch spriteBatch, object itemData, Rectangle bounds, BitmapFont font, BitmapFont secondaryFont, GameTime gameTime, float opacity)
        {
            // --- Draw Type and Rarity Headers ---
            var tertiaryFont = _core.TertiaryFont;
            string typeText = "ITEM";
            int rarity = 0;

            if (itemData is MoveData moveHeader)
            {
                typeText = moveHeader.MoveType == MoveType.Spell ? "SPELL" : "ACTION";
                rarity = moveHeader.Rarity;
            }
            else if (itemData is WeaponData weaponHeader)
            {
                typeText = "WEAPON";
                rarity = weaponHeader.Rarity;
            }
            else if (itemData is ArmorData armorHeader)
            {
                typeText = "ARMOR";
                rarity = armorHeader.Rarity;
            }
            else if (itemData is RelicData relicHeader)
            {
                typeText = "RELIC";
                rarity = relicHeader.Rarity;
            }
            else if (itemData is ConsumableItemData)
            {
                typeText = "CONSUMABLE";
                rarity = 0;
            }
            else if (itemData is MiscItemData miscHeader)
            {
                typeText = "ITEM";
                rarity = miscHeader.Rarity;
            }

            string rarityText = GetRarityName(rarity);
            Color rarityColor = _global.RarityColors.ContainsKey(rarity) ? _global.RarityColors[rarity] : Color.White;

            spriteBatch.DrawStringSnapped(tertiaryFont, typeText, new Vector2(bounds.X + 4, bounds.Y + 2), _global.Palette_DarkGray * opacity);
            Vector2 raritySize = tertiaryFont.MeasureString(rarityText);
            spriteBatch.DrawStringSnapped(tertiaryFont, rarityText, new Vector2(bounds.Right - 4 - raritySize.X, bounds.Y + 2), rarityColor * opacity);

            // --- Continue with specific drawing ---

            if (itemData is MoveData moveData)
            {
                string iconPath = $"Sprites/Spells/{moveData.MoveID}";
                int elementId = moveData.OffensiveElementIDs.FirstOrDefault();
                string? fallbackPath = null;
                if (BattleDataCache.Elements.TryGetValue(elementId, out var elementDef))
                {
                    string elName = elementDef.ElementName.ToLowerInvariant();
                    if (elName == "---") elName = "neutral";
                    fallbackPath = $"Sprites/Spells/default_{elName}";
                }

                var iconTexture = _spriteManager.GetItemSprite(iconPath, fallbackPath);
                var iconSilhouette = _spriteManager.GetItemSpriteSilhouette(iconPath, fallbackPath);
                Rectangle? sourceRect = _spriteManager.GetAnimatedIconSourceRect(iconTexture, gameTime);

                DrawSpellInfoPanel(spriteBatch, font, secondaryFont, moveData, iconTexture, iconSilhouette, sourceRect, null, Rectangle.Empty, Vector2.Zero, false, bounds, gameTime, opacity);
            }
            else if (itemData is WeaponData weaponData)
            {
                DrawWeaponInfoPanel(spriteBatch, font, secondaryFont, weaponData, null, null, bounds, gameTime, opacity);
            }
            else if (itemData is ArmorData armorData)
            {
                DrawArmorRelicInfoPanel(spriteBatch, font, secondaryFont, armorData.ArmorName, armorData.Description, armorData.Flavor, null, null, armorData.StatModifiers, bounds, gameTime, opacity, $"Sprites/Items/Armor/{armorData.ArmorID}");
            }
            else if (itemData is RelicData relicData)
            {
                DrawArmorRelicInfoPanel(spriteBatch, font, secondaryFont, relicData.RelicName, relicData.Description, relicData.Flavor, null, null, relicData.StatModifiers, bounds, gameTime, opacity, $"Sprites/Items/Relics/{relicData.RelicID}");
            }
            else
            {
                string name = "";
                string description = "";
                string flavor = "";
                string iconPath = "";
                Dictionary<string, int> stats = new Dictionary<string, int>();

                if (itemData is ConsumableItemData consItem) { name = consItem.ItemName; description = consItem.Description; flavor = consItem.Flavor; iconPath = consItem.ImagePath; }
                else if (itemData is MiscItemData miscItem) { name = miscItem.ItemName; description = miscItem.Description; flavor = miscItem.Flavor; iconPath = miscItem.ImagePath; }

                var iconTexture = _spriteManager.GetItemSprite(iconPath);
                var iconSilhouette = _spriteManager.GetItemSpriteSilhouette(iconPath);

                DrawGenericItemInfoPanel(spriteBatch, font, secondaryFont, name, description, flavor, iconTexture, iconSilhouette, stats, bounds, gameTime, opacity);
            }
        }

        private string GetRarityName(int rarity)
        {
            return rarity switch
            {
                -1 => "BASIC",
                0 => "COMMON",
                1 => "UNCOMMON",
                2 => "RARE",
                3 => "EPIC",
                4 => "MYTHIC",
                5 => "LEGENDARY",
                _ => "UNKNOWN"
            };
        }

        private float MeasureContentHeight(BitmapFont font, BitmapFont secondaryFont, object? data, int panelWidth)
        {
            if (data == null) return 0;

            var tertiaryFont = _core.TertiaryFont;
            const int padding = 4;
            const int lineSpacing = 1;
            float width = panelWidth - (padding * 2);

            float baseHeight = LAYOUT_DESC_START_Y;

            string description = "";
            string flavor = "";

            if (data is MoveData move)
            {
                description = move.Description;
                flavor = move.Flavor;
            }
            else if (data is WeaponData w)
            {
                description = w.Description;
                flavor = w.Flavor;
            }
            else if (data is ArmorData a)
            {
                description = a.Description;
                flavor = a.Flavor;
                // Check if stats exist
                bool hasStats = a.StatModifiers != null && a.StatModifiers.Values.Any(v => v != 0);
                if (hasStats)
                {
                    baseHeight = LAYOUT_VARS_START_Y + (2 * LAYOUT_VAR_ROW_HEIGHT) + 2;
                }
                else
                {
                    baseHeight = LAYOUT_VARS_START_Y;
                }
            }
            else if (data is RelicData r)
            {
                description = r.Description;
                flavor = r.Flavor;
                // Check if stats exist
                bool hasStats = r.StatModifiers != null && r.StatModifiers.Values.Any(v => v != 0);
                if (hasStats)
                {
                    baseHeight = LAYOUT_VARS_START_Y + (2 * LAYOUT_VAR_ROW_HEIGHT) + 2;
                }
                else
                {
                    baseHeight = LAYOUT_VARS_START_Y;
                }
            }
            else if (data is ConsumableItemData c)
            {
                description = c.Description;
                flavor = c.Flavor;
                baseHeight = LAYOUT_TITLE_Y + font.LineHeight + 4;
            }
            else if (data is MiscItemData m)
            {
                description = m.Description;
                flavor = m.Flavor;
                baseHeight = LAYOUT_TITLE_Y + font.LineHeight + 4;
            }

            float descHeight = 0f;
            if (!string.IsNullOrEmpty(description))
            {
                var lines = ParseAndWrapRichText(secondaryFont, description.ToUpper(), width, Color.White);
                descHeight = lines.Count * secondaryFont.LineHeight;
            }

            float flavorHeight = 0f;
            if (!string.IsNullOrEmpty(flavor))
            {
                var lines = ParseAndWrapRichText(tertiaryFont, flavor.ToUpper(), width, _global.Palette_DarkGray, 3);
                flavorHeight = (lines.Count * tertiaryFont.LineHeight) + ((lines.Count - 1) * lineSpacing);
                flavorHeight += 2;
            }

            return baseHeight + descHeight + flavorHeight + padding;
        }

        // --- DRAWING METHODS ---

        private void DrawItemName(SpriteBatch spriteBatch, BitmapFont font, string name, Rectangle infoPanelArea, float opacity, GameTime gameTime)
        {
            var lines = WrapName(name.ToUpper());

            // Bottom aligned to LAYOUT_TITLE_Y
            float bottomY = infoPanelArea.Y + LAYOUT_TITLE_Y;

            // Iterate backwards to draw from bottom up
            for (int i = lines.Count - 1; i >= 0; i--)
            {
                string line = lines[i];
                Vector2 lineSize = font.MeasureString(line);

                // Calculate Y offset based on line index from the bottom (0 = bottom line)
                int linesFromBottom = (lines.Count - 1) - i;
                float yPos = bottomY - (linesFromBottom * font.LineHeight);

                Vector2 namePos = new Vector2(
                    infoPanelArea.X + (infoPanelArea.Width - lineSize.X) / 2f,
                    yPos
                );
                namePos = new Vector2(MathF.Round(namePos.X), MathF.Round(namePos.Y));

                TextAnimator.DrawTextWithEffectOutlined(spriteBatch, font, line, namePos, _global.Palette_BlueWhite * opacity, _global.Palette_Black * opacity, TextEffectType.Drift, (float)gameTime.TotalGameTime.TotalSeconds);
            }
        }

        private void DrawWeaponInfoPanel(SpriteBatch spriteBatch, BitmapFont font, BitmapFont secondaryFont, WeaponData weapon, Texture2D? iconTexture, Texture2D? iconSilhouette, Rectangle infoPanelArea, GameTime gameTime, float opacity)
        {
            if (iconTexture == null)
            {
                string path = $"Sprites/Items/Weapons/{weapon.WeaponID}";
                iconTexture = _spriteManager.GetItemSprite(path);
                iconSilhouette = _spriteManager.GetItemSpriteSilhouette(path);
            }

            var tertiaryFont = _core.TertiaryFont;
            const int spriteSize = 16;

            // 1. Draw Icon
            int spriteX = infoPanelArea.X + (infoPanelArea.Width - spriteSize) / 2;
            float currentY = infoPanelArea.Y + LAYOUT_SPRITE_Y + 2;

            Vector2 staticDrawPos = new Vector2(spriteX + 8, currentY + 8);
            Vector2 iconOrigin = new Vector2(8, 8);
            float displayScale = 1.0f;

            Vector2 animOffset = GetJuicyOffset(gameTime);
            Vector2 animatedDrawPos = staticDrawPos + animOffset;

            // Draw Inventory Slot Background behind the item (Static Position)
            DrawInventorySlotBackground(spriteBatch, staticDrawPos, gameTime, opacity);

            // Draw Item (Animated Position)
            DrawIconWithSilhouette(spriteBatch, iconTexture, iconSilhouette, animatedDrawPos, iconOrigin, displayScale, opacity: opacity);

            // 2. Draw Name (Wrapped & Bottom Aligned)
            DrawItemName(spriteBatch, font, weapon.WeaponName, infoPanelArea, opacity, gameTime);

            // 3. Draw Move Stats
            currentY = infoPanelArea.Y + LAYOUT_VARS_START_Y;

            float centerX = infoPanelArea.Center.X;
            float leftCenter = centerX - COLUMN_CENTER_OFFSET;
            float rightCenter = centerX + COLUMN_CENTER_OFFSET;

            float labelOffset = 18f;
            float valueOffset = 2f;

            float leftLabelX = leftCenter - labelOffset;
            float leftValueX = leftCenter + valueOffset;
            float rightLabelX = rightCenter - labelOffset;
            float rightValueX = rightCenter + valueOffset;

            void DrawStatPair(string label, string value, float labelX, float valueX, float y, Color valColor)
            {
                float yOffset = (secondaryFont.LineHeight - tertiaryFont.LineHeight) / 2f;
                yOffset = MathF.Round(yOffset);
                spriteBatch.DrawStringSnapped(tertiaryFont, label, new Vector2(labelX, y + yOffset), _global.Palette_Gray * opacity);
                spriteBatch.DrawStringSnapped(secondaryFont, value, new Vector2(valueX, y), valColor * opacity);
            }

            string powVal = weapon.Power > 0 ? weapon.Power.ToString() : "---";
            string accVal = weapon.Accuracy >= 0 ? $"{weapon.Accuracy}%" : "---";

            DrawStatPair("POW", powVal, leftLabelX, leftValueX, currentY, _global.Palette_White);
            DrawStatPair("ACC", accVal, rightLabelX, rightValueX, currentY, _global.Palette_White);

            currentY += LAYOUT_VAR_ROW_HEIGHT;

            string targetVal = GetTargetString(weapon.Target);
            DrawStatPair("TGT", targetVal, rightLabelX, rightValueX, currentY, _global.Palette_White);
            currentY += LAYOUT_VAR_ROW_HEIGHT;

            string offStatVal = GetStatString(weapon.OffensiveStat);
            Color offColor = GetStatColor(weapon.OffensiveStat);
            string impactVal = weapon.ImpactType.ToString().ToUpper().Substring(0, Math.Min(4, weapon.ImpactType.ToString().Length));
            Color impactColor = weapon.ImpactType == ImpactType.Magical ? _global.Palette_LightBlue : (weapon.ImpactType == ImpactType.Physical ? _global.Palette_Orange : _global.Palette_Gray);

            DrawStatPair("USE", offStatVal, leftLabelX, leftValueX, currentY, offColor);
            DrawStatPair("TYP", impactVal, rightLabelX, rightValueX, currentY, impactColor);

            currentY = infoPanelArea.Y + LAYOUT_CONTACT_Y;
            if (weapon.MakesContact)
            {
                string contactText = "[MAKES CONTACT]";
                Vector2 contactSize = tertiaryFont.MeasureString(contactText);
                Vector2 contactPos = new Vector2(infoPanelArea.X + (infoPanelArea.Width - contactSize.X) / 2f, currentY);
                spriteBatch.DrawStringSnapped(tertiaryFont, contactText, contactPos, _global.Palette_Red * opacity);
            }

            float flavorHeight = DrawFlavorText(spriteBatch, infoPanelArea, weapon.Flavor, opacity);

            currentY = infoPanelArea.Y + LAYOUT_DESC_START_Y;
            DrawDescription(spriteBatch, secondaryFont, weapon.Description, infoPanelArea, currentY, flavorHeight, gameTime, opacity);
        }

        private void DrawArmorRelicInfoPanel(SpriteBatch spriteBatch, BitmapFont font, BitmapFont secondaryFont, string name, string description, string flavor, Texture2D? iconTexture, Texture2D? iconSilhouette, Dictionary<string, int> stats, Rectangle infoPanelArea, GameTime gameTime, float opacity, string? iconPath = null)
        {
            if (iconTexture == null && !string.IsNullOrEmpty(iconPath))
            {
                iconTexture = _spriteManager.GetItemSprite(iconPath);
                iconSilhouette = _spriteManager.GetItemSpriteSilhouette(iconPath);
            }

            var tertiaryFont = _core.TertiaryFont;
            const int spriteSize = 16;

            int spriteX = infoPanelArea.X + (infoPanelArea.Width - spriteSize) / 2;
            float currentY = infoPanelArea.Y + LAYOUT_SPRITE_Y + 2;
            Vector2 staticDrawPos = new Vector2(spriteX + 8, currentY + 8);
            Vector2 iconOrigin = new Vector2(8, 8);
            Vector2 animOffset = GetJuicyOffset(gameTime);
            Vector2 animatedDrawPos = staticDrawPos + animOffset;

            // NEW: Draw Inventory Slot Background behind the item (Static Position)
            DrawInventorySlotBackground(spriteBatch, staticDrawPos, gameTime, opacity);

            // Draw Item (Animated Position)
            DrawIconWithSilhouette(spriteBatch, iconTexture, iconSilhouette, animatedDrawPos, iconOrigin, 1.0f, opacity: opacity);

            // Draw Name (Wrapped & Bottom Aligned)
            DrawItemName(spriteBatch, font, name, infoPanelArea, opacity, gameTime);

            currentY = infoPanelArea.Y + LAYOUT_VARS_START_Y;

            // Check if we have any stats to display
            bool hasStats = stats != null && stats.Values.Any(v => v != 0);

            if (hasStats)
            {
                float centerX = infoPanelArea.Center.X;
                float leftCenter = centerX - COLUMN_CENTER_OFFSET;
                float rightCenter = centerX + COLUMN_CENTER_OFFSET;
                float labelOffset = 18f;
                float valueOffset = 2f;

                float leftLabelX = leftCenter - labelOffset;
                float leftValueX = leftCenter + valueOffset;
                float rightLabelX = rightCenter - labelOffset;
                float rightValueX = rightCenter + valueOffset;

                DrawStat(spriteBatch, secondaryFont, tertiaryFont, "STR", "Strength", stats, leftLabelX, leftValueX, currentY, opacity);
                DrawStat(spriteBatch, secondaryFont, tertiaryFont, "INT", "Intelligence", stats, rightLabelX, rightValueX, currentY, opacity);
                currentY += LAYOUT_VAR_ROW_HEIGHT;
                DrawStat(spriteBatch, secondaryFont, tertiaryFont, "TEN", "Tenacity", stats, leftLabelX, leftValueX, currentY, opacity);
                DrawStat(spriteBatch, secondaryFont, tertiaryFont, "AGI", "Agility", stats, rightLabelX, rightValueX, currentY, opacity);

                // Advance Y past the stat block
                currentY += LAYOUT_VAR_ROW_HEIGHT;
            }

            float flavorHeight = DrawFlavorText(spriteBatch, infoPanelArea, flavor, opacity);

            // Calculate description start Y based on whether stats were shown
            float descStartY = currentY + 2; // Add small padding

            DrawDescription(spriteBatch, secondaryFont, description, infoPanelArea, descStartY, flavorHeight, gameTime, opacity);
        }

        private void DrawSpellInfoPanel(SpriteBatch spriteBatch, BitmapFont font, BitmapFont secondaryFont, MoveData move, Texture2D? iconTexture, Texture2D? iconSilhouette, Rectangle? sourceRect, Color? iconTint, Rectangle idleFrame, Vector2 idleOrigin, bool drawBackground, Rectangle infoPanelArea, GameTime gameTime, float opacity)
        {
            var tertiaryFont = _core.TertiaryFont; // Get Tertiary Font
            const int spriteSize = 32;

            int spriteX = infoPanelArea.X + (infoPanelArea.Width - spriteSize) / 2;
            int spriteY = infoPanelArea.Y + LAYOUT_SPRITE_Y - 3;

            Vector2 iconOrigin = new Vector2(16, 16);
            Vector2 staticDrawPos = new Vector2(spriteX + 16, spriteY + 16);

            // NEW: Draw Inventory Slot Background behind the item (Static Position)
            DrawInventorySlotBackground(spriteBatch, staticDrawPos, gameTime, opacity);

            // Draw Item (Static Position for Spells)
            DrawIconWithSilhouette(spriteBatch, iconTexture, iconSilhouette, staticDrawPos, iconOrigin, 1.0f, sourceRect, iconTint, opacity);

            // Draw Name (Wrapped & Bottom Aligned)
            DrawItemName(spriteBatch, font, move.MoveName, infoPanelArea, opacity, gameTime);

            float currentY = infoPanelArea.Y + LAYOUT_VARS_START_Y;

            float centerX = infoPanelArea.Center.X;
            float leftCenter = centerX - COLUMN_CENTER_OFFSET;
            float rightCenter = centerX + COLUMN_CENTER_OFFSET;
            float labelOffset = 18f;
            float valueOffset = 2f;

            float leftLabelX = leftCenter - labelOffset;
            float leftValueX = leftCenter + valueOffset;
            float rightLabelX = rightCenter - labelOffset;
            float rightValueX = rightCenter + valueOffset;

            void DrawStatPair(string label, string value, float labelX, float valueX, float y, Color valColor)
            {
                float yOffset = (secondaryFont.LineHeight - tertiaryFont.LineHeight) / 2f;
                yOffset = MathF.Round(yOffset);
                spriteBatch.DrawStringSnapped(tertiaryFont, label, new Vector2(labelX, y + yOffset), _global.Palette_Gray * opacity);
                spriteBatch.DrawStringSnapped(secondaryFont, value, new Vector2(valueX, y), valColor * opacity);
            }

            string powVal = move.Power > 0 ? move.Power.ToString() : (move.Effects.ContainsKey("ManaDamage") ? "???" : "---");
            string accVal = move.Accuracy >= 0 ? $"{move.Accuracy}%" : "---";
            string mpVal = (move.ManaCost > 0 ? move.ManaCost.ToString() : "0") + "%";
            var manaDump = move.Abilities.OfType<ManaDumpAbility>().FirstOrDefault();
            if (manaDump != null) powVal = "???";

            DrawStatPair("POW", powVal, leftLabelX, leftValueX, currentY, _global.Palette_White);
            DrawStatPair("ACC", accVal, rightLabelX, rightValueX, currentY, _global.Palette_White);
            currentY += LAYOUT_VAR_ROW_HEIGHT;

            DrawStatPair("MANA", mpVal, leftLabelX, leftValueX, currentY, _global.Palette_White);
            DrawStatPair("TGT", GetTargetString(move.Target), rightLabelX, rightValueX, currentY, _global.Palette_White);
            currentY += LAYOUT_VAR_ROW_HEIGHT;

            string offStatVal = GetStatString(move.OffensiveStat);
            Color offColor = GetStatColor(move.OffensiveStat);
            string impactVal = move.ImpactType.ToString().ToUpper().Substring(0, Math.Min(4, move.ImpactType.ToString().Length));
            Color impactColor = move.ImpactType == ImpactType.Magical ? _global.Palette_LightBlue : (move.ImpactType == ImpactType.Physical ? _global.Palette_Orange : _global.Palette_Gray);

            DrawStatPair("USE", offStatVal, leftLabelX, leftValueX, currentY, offColor);
            DrawStatPair("TYP", impactVal, rightLabelX, rightValueX, currentY, impactColor);

            currentY = infoPanelArea.Y + LAYOUT_CONTACT_Y;
            if (move.MakesContact)
            {
                string contactText = "[ CONTACT ]";
                Vector2 contactSize = tertiaryFont.MeasureString(contactText);
                Vector2 contactPos = new Vector2(infoPanelArea.X + (infoPanelArea.Width - contactSize.X) / 2f, currentY + (secondaryFont.LineHeight - tertiaryFont.LineHeight) / 2f);
                spriteBatch.DrawStringSnapped(tertiaryFont, contactText, contactPos, _global.Palette_Red * opacity);
            }

            float flavorHeight = DrawFlavorText(spriteBatch, infoPanelArea, move.Flavor, opacity);
            currentY = infoPanelArea.Y + LAYOUT_DESC_START_Y;
            DrawDescription(spriteBatch, secondaryFont, move.Description, infoPanelArea, currentY, flavorHeight, gameTime, opacity);
        }

        private void DrawGenericItemInfoPanel(SpriteBatch spriteBatch, BitmapFont font, BitmapFont secondaryFont, string name, string description, string flavor, Texture2D? iconTexture, Texture2D? iconSilhouette, Dictionary<string, int> stats, Rectangle infoPanelArea, GameTime gameTime, float opacity)
        {
            const int spriteSize = 16;
            int spriteX = infoPanelArea.X + (infoPanelArea.Width - spriteSize) / 2;
            float currentY = infoPanelArea.Y + LAYOUT_SPRITE_Y + 2;
            Vector2 staticDrawPos = new Vector2(spriteX + 8, currentY + 8);
            Vector2 iconOrigin = new Vector2(8, 8);
            Vector2 animOffset = GetJuicyOffset(gameTime);
            Vector2 animatedDrawPos = staticDrawPos + animOffset;

            // NEW: Draw Inventory Slot Background behind the item (Static Position)
            DrawInventorySlotBackground(spriteBatch, staticDrawPos, gameTime, opacity);

            DrawIconWithSilhouette(spriteBatch, iconTexture, iconSilhouette, animatedDrawPos, iconOrigin, 1.0f, opacity: opacity);

            // Draw Name (Wrapped & Bottom Aligned)
            DrawItemName(spriteBatch, font, name, infoPanelArea, opacity, gameTime);

            float flavorHeight = DrawFlavorText(spriteBatch, infoPanelArea, flavor, opacity);
            currentY = infoPanelArea.Y + LAYOUT_TITLE_Y + font.LineHeight + 4;
            DrawDescription(spriteBatch, secondaryFont, description, infoPanelArea, currentY, flavorHeight, gameTime, opacity);
        }

        // --- Helpers ---

        private void DrawInventorySlotBackground(SpriteBatch spriteBatch, Vector2 centerPos, GameTime gameTime, float opacity)
        {
            var sheet = _spriteManager.InventorySlotIdleSpriteSheet;
            var frames = _spriteManager.InventorySlotSourceRects;

            if (sheet != null && frames != null && frames.Length > 0)
            {
                // Simulate random idle behavior using time
                // Change frame every 2 seconds
                int frameIndex = (int)(gameTime.TotalGameTime.TotalSeconds / 2.0) % frames.Length;
                var sourceRect = frames[frameIndex];
                var origin = new Vector2(sourceRect.Width / 2f, sourceRect.Height / 2f);

                spriteBatch.DrawSnapped(sheet, centerPos, sourceRect, Color.White * opacity, 0f, origin, 1.0f, SpriteEffects.None, 0f);
            }
        }

        private void DrawIconWithSilhouette(SpriteBatch spriteBatch, Texture2D? texture, Texture2D? silhouette, Vector2 pos, Vector2 origin, float scale, Rectangle? sourceRect = null, Color? tint = null, float opacity = 1.0f)
        {
            if (silhouette != null)
            {
                Color mainOutlineColor = _global.ItemOutlineColor_Idle * opacity;
                Color cornerOutlineColor = _global.ItemOutlineColor_Idle_Corner * opacity;

                spriteBatch.DrawSnapped(silhouette, pos + new Vector2(-1, -1) * scale, sourceRect, cornerOutlineColor, 0f, origin, scale, SpriteEffects.None, 0f);
                spriteBatch.DrawSnapped(silhouette, pos + new Vector2(1, -1) * scale, sourceRect, cornerOutlineColor, 0f, origin, scale, SpriteEffects.None, 0f);
                spriteBatch.DrawSnapped(silhouette, pos + new Vector2(-1, 1) * scale, sourceRect, cornerOutlineColor, 0f, origin, scale, SpriteEffects.None, 0f);
                spriteBatch.DrawSnapped(silhouette, pos + new Vector2(1, 1) * scale, sourceRect, cornerOutlineColor, 0f, origin, scale, SpriteEffects.None, 0f);

                spriteBatch.DrawSnapped(silhouette, pos + new Vector2(-1, 0) * scale, sourceRect, mainOutlineColor, 0f, origin, scale, SpriteEffects.None, 0f);
                spriteBatch.DrawSnapped(silhouette, pos + new Vector2(1, 0) * scale, sourceRect, mainOutlineColor, 0f, origin, scale, SpriteEffects.None, 0f);
                spriteBatch.DrawSnapped(silhouette, pos + new Vector2(0, -1) * scale, sourceRect, mainOutlineColor, 0f, origin, scale, SpriteEffects.None, 0f);
                spriteBatch.DrawSnapped(silhouette, pos + new Vector2(0, 1) * scale, sourceRect, mainOutlineColor, 0f, origin, scale, SpriteEffects.None, 0f);
            }

            if (texture != null)
            {
                spriteBatch.DrawSnapped(texture, pos, sourceRect, (tint ?? Color.White) * opacity, 0f, origin, scale, SpriteEffects.None, 0f);
            }
        }

        private void DrawStat(SpriteBatch spriteBatch, BitmapFont secondaryFont, BitmapFont tertiaryFont, string label, string key, Dictionary<string, int> stats, float labelX, float valueX, float y, float opacity)
        {
            float yOffset = (secondaryFont.LineHeight - tertiaryFont.LineHeight) / 2f;
            yOffset = MathF.Round(yOffset);
            spriteBatch.DrawStringSnapped(tertiaryFont, label, new Vector2(labelX, y + yOffset), _global.Palette_Gray * opacity);

            int val = 0;
            if (stats.TryGetValue(key, out int v)) val = v;

            string text;
            Color c;
            if (val == 0)
            {
                text = "+0";
                c = _global.Palette_Gray;
            }
            else
            {
                text = (val > 0 ? "+" : "") + val;
                c = val > 0 ? _global.Palette_LightGreen : _global.Palette_Red;
            }
            spriteBatch.DrawStringSnapped(secondaryFont, text, new Vector2(valueX, y), c * opacity);
        }

        private float DrawFlavorText(SpriteBatch spriteBatch, Rectangle infoPanelArea, string flavorText, float opacity)
        {
            if (string.IsNullOrEmpty(flavorText)) return 0f;

            var tertiaryFont = _core.TertiaryFont;
            const int padding = 4;
            const int spaceWidth = 3;
            const int lineSpacing = 1;

            float width = infoPanelArea.Width - (padding * 2);
            var lines = ParseAndWrapRichText(tertiaryFont, flavorText.ToUpper(), width, _global.Palette_DarkGray, spaceWidth);

            if (lines.Count == 0) return 0f;

            float lineHeight = tertiaryFont.LineHeight;
            float totalHeight = (lines.Count * lineHeight) + ((lines.Count - 1) * lineSpacing);

            float startY = infoPanelArea.Bottom - (padding + 2) - totalHeight + 3;

            foreach (var line in lines)
            {
                float lineWidth = 0;
                foreach (var segment in line)
                {
                    if (string.IsNullOrWhiteSpace(segment.Text)) lineWidth += segment.Text.Length * spaceWidth;
                    else lineWidth += tertiaryFont.MeasureString(segment.Text).Width;
                }

                float currentX = infoPanelArea.X + (infoPanelArea.Width - lineWidth) / 2f;

                foreach (var segment in line)
                {
                    float segWidth;
                    if (string.IsNullOrWhiteSpace(segment.Text))
                    {
                        segWidth = segment.Text.Length * spaceWidth;
                    }
                    else
                    {
                        segWidth = tertiaryFont.MeasureString(segment.Text).Width;
                        spriteBatch.DrawStringSnapped(tertiaryFont, segment.Text, new Vector2(currentX, startY), segment.Color * opacity);
                    }
                    currentX += segWidth;
                }
                startY += lineHeight + lineSpacing;
            }
            return totalHeight + 2 - 3;
        }

        private void DrawDescription(SpriteBatch spriteBatch, BitmapFont font, string description, Rectangle infoPanelArea, float startY, float flavorHeight, GameTime gameTime, float opacity)
        {
            if (string.IsNullOrEmpty(description)) return;

            const int padding = 4;
            float descWidth = infoPanelArea.Width - (padding * 2);
            var descLines = ParseAndWrapRichText(font, description.ToUpper(), descWidth, _global.Palette_White);

            float lineY = startY;
            foreach (var line in descLines)
            {
                if (lineY + font.LineHeight > infoPanelArea.Bottom - padding - flavorHeight) break;

                float lineWidth = 0;
                foreach (var segment in line)
                {
                    if (string.IsNullOrWhiteSpace(segment.Text)) lineWidth += segment.Text.Length * SPACE_WIDTH;
                    else lineWidth += font.MeasureString(segment.Text).Width;
                }

                float lineX = infoPanelArea.X + (infoPanelArea.Width - lineWidth) / 2;
                float currentX = lineX;

                foreach (var segment in line)
                {
                    float segWidth;
                    if (string.IsNullOrWhiteSpace(segment.Text)) segWidth = segment.Text.Length * SPACE_WIDTH;
                    else
                    {
                        segWidth = font.MeasureString(segment.Text).Width;
                        if (segment.Effect != TextEffectType.None)
                            TextAnimator.DrawTextWithEffect(spriteBatch, font, segment.Text, new Vector2(currentX, lineY), segment.Color * opacity, segment.Effect, (float)gameTime.TotalGameTime.TotalSeconds);
                        else
                            spriteBatch.DrawStringSnapped(font, segment.Text, new Vector2(currentX, lineY), segment.Color * opacity);
                    }
                    currentX += segWidth;
                }
                lineY += font.LineHeight;
            }
        }

        private Vector2 GetJuicyOffset(GameTime gameTime)
        {
            float t = (float)gameTime.TotalGameTime.TotalSeconds * 1.5f;
            float dist = 1.0f;
            float swayX = (MathF.Sin(t * 1.1f) * dist) + (MathF.Cos(t * 0.4f) * (dist * 0.5f));
            float swayY = MathF.Sin(t * 1.4f) * dist;
            return new Vector2(swayX, swayY);
        }

        private string GetTargetString(TargetType target)
        {
            return target switch
            {
                TargetType.Single => "SINGL",
                TargetType.SingleAll => "ANY",
                TargetType.Both => "BOTH",
                TargetType.Every => "MULTI",
                TargetType.All => "ALL",
                TargetType.Self => "SELF",
                TargetType.Team => "TEAM",
                TargetType.Ally => "ALLY",
                TargetType.SingleTeam => "S-TEAM",
                TargetType.RandomBoth => "R-BOTH",
                TargetType.RandomEvery => "R-EVRY",
                TargetType.RandomAll => "R-ALL",
                TargetType.None => "NONE",
                _ => "---"
            };
        }

        private string GetStatString(OffensiveStatType stat)
        {
            return stat switch
            {
                OffensiveStatType.Strength => "STR",
                OffensiveStatType.Intelligence => "INT",
                OffensiveStatType.Tenacity => "TEN",
                OffensiveStatType.Agility => "AGI",
                _ => "---"
            };
        }

        private Color GetStatColor(OffensiveStatType stat)
        {
            return stat switch
            {
                OffensiveStatType.Strength => _global.StatColor_Strength,
                OffensiveStatType.Intelligence => _global.StatColor_Intelligence,
                OffensiveStatType.Tenacity => _global.StatColor_Tenacity,
                OffensiveStatType.Agility => _global.StatColor_Agility,
                _ => _global.Palette_White
            };
        }

        private List<List<ColoredText>> ParseAndWrapRichText(BitmapFont font, string text, float maxWidth, Color defaultColor, int spaceWidth = 5)
        {
            var lines = new List<List<ColoredText>>();
            if (string.IsNullOrEmpty(text)) return lines;

            var currentLine = new List<ColoredText>();
            float currentLineWidth = 0f;
            Color currentColor = defaultColor;

            var parts = Regex.Split(text, @"(\[.*?\]|\s+)");

            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part)) continue;

                if (part.StartsWith("[") && part.EndsWith("]"))
                {
                    string tagContent = part.Substring(1, part.Length - 2).ToLowerInvariant();
                    if (tagContent == "/" || tagContent == "default")
                    {
                        currentColor = defaultColor;
                    }
                    else
                    {
                        currentColor = ParseColor(tagContent);
                    }
                }
                else if (part.Contains("\n"))
                {
                    lines.Add(currentLine);
                    currentLine = new List<ColoredText>();
                    currentLineWidth = 0f;
                }
                else
                {
                    bool isWhitespace = string.IsNullOrWhiteSpace(part);
                    float partWidth = isWhitespace ? (part.Length * spaceWidth) : font.MeasureString(part).Width;

                    if (!isWhitespace && currentLineWidth + partWidth > maxWidth && currentLineWidth > 0)
                    {
                        lines.Add(currentLine);
                        currentLine = new List<ColoredText>();
                        currentLineWidth = 0f;
                    }

                    if (isWhitespace && currentLineWidth == 0)
                    {
                        continue;
                    }

                    Color finalColor = currentColor;
                    if (currentColor != defaultColor && !isWhitespace && part.EndsWith("%"))
                    {
                        string numberPart = part.Substring(0, part.Length - 1);
                        if (int.TryParse(numberPart, out int percent))
                        {
                            float amount = Math.Clamp(percent / 100f, 0f, 1f);
                            finalColor = Color.Lerp(_global.ColorPercentageMin, _global.ColorPercentageMax, amount);
                        }
                    }

                    currentLine.Add(new ColoredText(part, finalColor));
                    currentLineWidth += partWidth;
                }
            }

            if (currentLine.Count > 0)
            {
                lines.Add(currentLine);
            }

            return lines;
        }

        private Color ParseColor(string colorName)
        {
            return _global.GetNarrationColor(colorName);
        }

        private void DrawBeveledBackground(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color color)
        {
            // 1. Top Row (Y+1): X+2 to W-4
            spriteBatch.DrawSnapped(pixel, new Rectangle(rect.X + 2, rect.Y + 1, rect.Width - 4, 1), color);

            // 2. Bottom Row (Bottom-2): X+2 to W-4
            spriteBatch.DrawSnapped(pixel, new Rectangle(rect.X + 2, rect.Bottom - 2, rect.Width - 4, 1), color);

            // 3. Middle Block (Y+2 to Bottom-3): X+1 to W-2
            spriteBatch.DrawSnapped(pixel, new Rectangle(rect.X + 1, rect.Y + 2, rect.Width - 2, rect.Height - 4), color);
        }

        private void DrawBeveledBorder(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color color)
        {
            // Top Line: X+2 to W-4
            spriteBatch.DrawSnapped(pixel, new Rectangle(rect.X + 2, rect.Y, rect.Width - 4, 1), color);

            // Bottom Line: X+2 to W-4
            spriteBatch.DrawSnapped(pixel, new Rectangle(rect.X + 2, rect.Bottom - 1, rect.Width - 4, 1), color);

            // Left Line: Y+2 to H-4
            spriteBatch.DrawSnapped(pixel, new Rectangle(rect.X, rect.Y + 2, 1, rect.Height - 4), color);

            // Right Line: Y+2 to H-4
            spriteBatch.DrawSnapped(pixel, new Rectangle(rect.Right - 1, rect.Y + 2, 1, rect.Height - 4), color);

            // Corners (1x1 pixels)
            // Top-Left
            spriteBatch.DrawSnapped(pixel, new Vector2(rect.X + 1, rect.Y + 1), color);
            // Top-Right
            spriteBatch.DrawSnapped(pixel, new Vector2(rect.Right - 2, rect.Y + 1), color);
            // Bottom-Left
            spriteBatch.DrawSnapped(pixel, new Vector2(rect.X + 1, rect.Bottom - 2), color);
            // Bottom-Right
            spriteBatch.DrawSnapped(pixel, new Vector2(rect.Right - 2, rect.Bottom - 2), color);
        }
    }
}
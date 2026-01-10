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

        private const int TOOLTIP_WIDTH = 120;
        private const int SCREEN_EDGE_MARGIN = 6;
        private const int SPACE_WIDTH = 5;

        // --- UNIFIED LAYOUT CONSTANTS (Matched to InventoryDrawer) ---
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

        public void DrawTooltip(SpriteBatch spriteBatch, object itemData, Vector2 anchorPosition, GameTime gameTime)
        {
            if (itemData == null) return;

            var font = ServiceLocator.Get<BitmapFont>();
            var secondaryFont = _core.SecondaryFont;

            // 1. Calculate Required Height
            float contentHeight = MeasureContentHeight(font, secondaryFont, itemData);

            // Added +2 to height for the bottom extension
            int panelHeight = (int)Math.Ceiling(contentHeight) + 8 + 2;

            // 2. Determine Y Position (Centered on anchor, shifted up if spell)
            float panelY = anchorPosition.Y - panelHeight / 2;
            if (itemData is MoveData) panelY -= 16;

            // 3. Create Rectangle
            int panelWidth = TOOLTIP_WIDTH;
            var infoPanelArea = new Rectangle(
                (int)(anchorPosition.X - panelWidth / 2),
                (int)panelY,
                panelWidth,
                panelHeight
            );

            // 4. Clamp to Screen
            int screenTop = SCREEN_EDGE_MARGIN;
            int screenBottom = Global.VIRTUAL_HEIGHT - SCREEN_EDGE_MARGIN;
            int screenLeft = SCREEN_EDGE_MARGIN;
            int screenRight = Global.VIRTUAL_WIDTH - SCREEN_EDGE_MARGIN;

            if (infoPanelArea.Top < screenTop) infoPanelArea.Y = screenTop;
            if (infoPanelArea.Bottom > screenBottom) infoPanelArea.Y = screenBottom - infoPanelArea.Height;
            if (infoPanelArea.Left < screenLeft) infoPanelArea.X = screenLeft;
            if (infoPanelArea.Right > screenRight) infoPanelArea.X = screenRight - infoPanelArea.Width;

            // 5. Draw Background
            var pixel = ServiceLocator.Get<Texture2D>();
            spriteBatch.DrawSnapped(pixel, infoPanelArea, _global.Palette_DarkestGray);

            // Black Outline
            DrawRectangleBorder(spriteBatch, pixel, infoPanelArea, 1, _global.Palette_BlueWhite);

            // 6. Draw Content
            DrawInfoPanelContent(spriteBatch, itemData, infoPanelArea, font, secondaryFont, gameTime);
        }

        public void DrawInfoPanelContent(SpriteBatch spriteBatch, object itemData, Rectangle bounds, BitmapFont font, BitmapFont secondaryFont, GameTime gameTime)
        {
            // --- NEW: Draw Type and Rarity Headers ---
            var tertiaryFont = _core.TertiaryFont;
            string typeText = "ITEM";
            int rarity = 0;

            // Renamed variables to avoid conflict with lower scopes
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
                rarity = 0; // Default to Common
            }
            else if (itemData is MiscItemData miscHeader)
            {
                typeText = "ITEM";
                rarity = miscHeader.Rarity;
            }

            string rarityText = GetRarityName(rarity);
            Color rarityColor = _global.RarityColors.ContainsKey(rarity) ? _global.RarityColors[rarity] : Color.White;

            // Draw Type (Top Left)
            spriteBatch.DrawStringSnapped(tertiaryFont, typeText, new Vector2(bounds.X + 4, bounds.Y + 2), _global.Palette_DarkGray);

            // Draw Rarity (Top Right)
            Vector2 raritySize = tertiaryFont.MeasureString(rarityText);
            spriteBatch.DrawStringSnapped(tertiaryFont, rarityText, new Vector2(bounds.Right - 4 - raritySize.X, bounds.Y + 2), rarityColor);

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

                DrawSpellInfoPanel(spriteBatch, font, secondaryFont, moveData, iconTexture, iconSilhouette, sourceRect, null, Rectangle.Empty, Vector2.Zero, false, bounds, gameTime);
            }
            else if (itemData is WeaponData weaponData)
            {
                DrawWeaponInfoPanel(spriteBatch, font, secondaryFont, weaponData, null, null, bounds, gameTime);
            }
            else if (itemData is ArmorData armorData)
            {
                DrawArmorRelicInfoPanel(spriteBatch, font, secondaryFont, armorData.ArmorName, armorData.Description, armorData.Flavor, null, null, armorData.StatModifiers, bounds, gameTime, $"Sprites/Items/Armor/{armorData.ArmorID}");
            }
            else if (itemData is RelicData relicData)
            {
                DrawArmorRelicInfoPanel(spriteBatch, font, secondaryFont, relicData.RelicName, relicData.Description, relicData.Flavor, null, null, relicData.StatModifiers, bounds, gameTime, $"Sprites/Items/Relics/{relicData.RelicID}");
            }
            else
            {
                string name = "";
                string description = "";
                string flavor = "";
                string iconPath = "";
                Dictionary<string, int> stats = new Dictionary<string, int>();

                // Renamed variables here as well to be safe
                if (itemData is ConsumableItemData consItem) { name = consItem.ItemName; description = consItem.Description; flavor = consItem.Flavor; iconPath = consItem.ImagePath; }
                else if (itemData is MiscItemData miscItem) { name = miscItem.ItemName; description = miscItem.Description; flavor = miscItem.Flavor; iconPath = miscItem.ImagePath; }

                var iconTexture = _spriteManager.GetItemSprite(iconPath);
                var iconSilhouette = _spriteManager.GetItemSpriteSilhouette(iconPath);

                DrawGenericItemInfoPanel(spriteBatch, font, secondaryFont, name, description, flavor, iconTexture, iconSilhouette, stats, bounds, gameTime);
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

        private float MeasureContentHeight(BitmapFont font, BitmapFont secondaryFont, object? data)
        {
            if (data == null) return 0;

            var tertiaryFont = _core.TertiaryFont;
            const int padding = 4;
            const int lineSpacing = 1;
            float width = TOOLTIP_WIDTH - (padding * 2);

            // Base height for all panels (Sprite + Title + Grid)
            // LAYOUT_DESC_START_Y is where description starts.
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
                // Armor/Relic grid is shorter (2 rows), so description starts earlier
                baseHeight = LAYOUT_VARS_START_Y + (2 * LAYOUT_VAR_ROW_HEIGHT) + 2;
            }
            else if (data is RelicData r)
            {
                description = r.Description;
                flavor = r.Flavor;
                baseHeight = LAYOUT_VARS_START_Y + (2 * LAYOUT_VAR_ROW_HEIGHT) + 2;
            }
            else if (data is ConsumableItemData c)
            {
                description = c.Description;
                flavor = c.Flavor;
                // Generic items start description right after title + sprite
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
                flavorHeight += 2; // Gap
            }

            return baseHeight + descHeight + flavorHeight + padding;
        }

        // --- DRAWING METHODS ---

        private void DrawWeaponInfoPanel(SpriteBatch spriteBatch, BitmapFont font, BitmapFont secondaryFont, WeaponData weapon, Texture2D? iconTexture, Texture2D? iconSilhouette, Rectangle infoPanelArea, GameTime gameTime)
        {
            if (iconTexture == null)
            {
                string path = $"Sprites/Items/Weapons/{weapon.WeaponID}";
                iconTexture = _spriteManager.GetItemSprite(path);
                iconSilhouette = _spriteManager.GetItemSpriteSilhouette(path);
            }

            var tertiaryFont = _core.TertiaryFont;
            const int spriteSize = 16;
            const int padding = 4;
            const int gap = 2; // Defined gap

            // 1. Draw Icon
            int spriteX = infoPanelArea.X + (infoPanelArea.Width - spriteSize) / 2;
            float currentY = infoPanelArea.Y + LAYOUT_SPRITE_Y + 2;

            Vector2 drawPos = new Vector2(spriteX + 8, currentY + 8);
            Vector2 iconOrigin = new Vector2(8, 8);
            float displayScale = 1.0f;

            // Apply Juicy Animation
            Vector2 animOffset = GetJuicyOffset(gameTime);
            drawPos += animOffset;

            DrawIconWithSilhouette(spriteBatch, iconTexture, iconSilhouette, drawPos, iconOrigin, displayScale);

            // 2. Draw Name
            string name = weapon.WeaponName.ToUpper();
            Vector2 nameSize = font.MeasureString(name);

            Vector2 namePos = new Vector2(
                infoPanelArea.X + (infoPanelArea.Width - nameSize.X) / 2f,
                infoPanelArea.Y + LAYOUT_TITLE_Y
            );

            namePos = new Vector2(MathF.Round(namePos.X), MathF.Round(namePos.Y));

            // Use DriftWave for title text
            TextAnimator.DrawTextWithEffectOutlined(spriteBatch, font, name, namePos, _global.Palette_BlueWhite, _global.Palette_Black, TextEffectType.DriftWave, (float)gameTime.TotalGameTime.TotalSeconds);

            // 3. Draw Move Stats
            currentY = infoPanelArea.Y + LAYOUT_VARS_START_Y;

            // --- TIGHTENED LAYOUT COORDINATES ---
            float leftLabelX = infoPanelArea.X + 8;
            float leftValueX = infoPanelArea.X + 28; // Fixed start for left values

            float rightLabelX = infoPanelArea.X + 62;
            float rightValueX = infoPanelArea.X + 80; // Fixed start for right values

            void DrawStatPair(string label, string value, float labelX, float valueX, float y, Color valColor)
            {
                // Center label vertically relative to value font
                float yOffset = (secondaryFont.LineHeight - tertiaryFont.LineHeight) / 2f;
                yOffset = MathF.Round(yOffset);

                spriteBatch.DrawStringSnapped(tertiaryFont, label, new Vector2(labelX, y + yOffset), _global.Palette_Gray);
                spriteBatch.DrawStringSnapped(secondaryFont, value, new Vector2(valueX, y), valColor);
            }

            // Row 1: POW / ACC
            string powVal = weapon.Power > 0 ? weapon.Power.ToString() : "---";
            string accVal = weapon.Accuracy >= 0 ? $"{weapon.Accuracy}%" : "---";

            DrawStatPair("POW", powVal, leftLabelX, leftValueX, currentY, _global.Palette_White);
            DrawStatPair("ACC", accVal, rightLabelX, rightValueX, currentY, _global.Palette_White);

            currentY += LAYOUT_VAR_ROW_HEIGHT;

            // Row 2: MP (Empty) / TGT (Right)
            string targetVal = weapon.Target switch
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

            DrawStatPair("TGT", targetVal, rightLabelX, rightValueX, currentY, _global.Palette_White);

            // No MP draw call here, leaving the left side empty.

            currentY += LAYOUT_VAR_ROW_HEIGHT;

            // Row 3: USE / TYP
            string offStatVal = weapon.OffensiveStat switch
            {
                OffensiveStatType.Strength => "STR",
                OffensiveStatType.Intelligence => "INT",
                OffensiveStatType.Tenacity => "TEN",
                OffensiveStatType.Agility => "AGI",
                _ => "---"
            };

            Color offColor = weapon.OffensiveStat switch
            {
                OffensiveStatType.Strength => _global.StatColor_Strength,
                OffensiveStatType.Intelligence => _global.StatColor_Intelligence,
                OffensiveStatType.Tenacity => _global.StatColor_Tenacity,
                OffensiveStatType.Agility => _global.StatColor_Agility,
                _ => _global.Palette_White
            };

            string impactVal = weapon.ImpactType.ToString().ToUpper().Substring(0, Math.Min(4, weapon.ImpactType.ToString().Length));
            Color impactColor = weapon.ImpactType == ImpactType.Magical ? _global.Palette_LightBlue : (weapon.ImpactType == ImpactType.Physical ? _global.Palette_Orange : _global.Palette_Gray);

            DrawStatPair("USE", offStatVal, leftLabelX, leftValueX, currentY, offColor);
            DrawStatPair("TYP", impactVal, rightLabelX, rightValueX, currentY, impactColor);

            // Row 4: Contact
            currentY = infoPanelArea.Y + LAYOUT_CONTACT_Y;
            if (weapon.MakesContact)
            {
                string contactText = "[MAKES CONTACT]";
                Vector2 contactSize = tertiaryFont.MeasureString(contactText);
                Vector2 contactPos = new Vector2(infoPanelArea.X + (infoPanelArea.Width - contactSize.X) / 2f, currentY);
                spriteBatch.DrawStringSnapped(tertiaryFont, contactText, contactPos, _global.Palette_Red);
            }

            // --- Draw Flavor Text (Bottom) ---
            float flavorHeight = DrawFlavorText(spriteBatch, infoPanelArea, weapon.Flavor);

            // --- 4. Draw Description ---
            currentY = infoPanelArea.Y + LAYOUT_DESC_START_Y;
            if (!string.IsNullOrEmpty(weapon.Description))
            {
                float descWidth = infoPanelArea.Width - (padding * 2);
                var descLines = ParseAndWrapRichText(secondaryFont, weapon.Description.ToUpper(), descWidth, _global.Palette_White);

                foreach (var line in descLines)
                {
                    // Stop drawing if we hit the flavor text area
                    if (currentY + secondaryFont.LineHeight > infoPanelArea.Bottom - padding - flavorHeight) break;

                    float lineWidth = 0;
                    foreach (var segment in line)
                    {
                        if (string.IsNullOrWhiteSpace(segment.Text)) lineWidth += segment.Text.Length * SPACE_WIDTH;
                        else lineWidth += secondaryFont.MeasureString(segment.Text).Width;
                    }

                    float lineX = infoPanelArea.X + (infoPanelArea.Width - lineWidth) / 2;
                    float currentX = lineX;

                    foreach (var segment in line)
                    {
                        float segWidth;
                        if (string.IsNullOrWhiteSpace(segment.Text)) segWidth = segment.Text.Length * SPACE_WIDTH;
                        else
                        {
                            segWidth = secondaryFont.MeasureString(segment.Text).Width;
                            // Check for effect
                            if (segment.Effect != TextEffectType.None)
                                TextAnimator.DrawTextWithEffect(spriteBatch, secondaryFont, segment.Text, new Vector2(currentX, currentY), segment.Color, segment.Effect, (float)gameTime.TotalGameTime.TotalSeconds);
                            else
                                spriteBatch.DrawStringSnapped(secondaryFont, segment.Text, new Vector2(currentX, currentY), segment.Color);
                        }
                        currentX += segWidth;
                    }
                    currentY += secondaryFont.LineHeight;
                }
            }
        }

        private void DrawArmorRelicInfoPanel(SpriteBatch spriteBatch, BitmapFont font, BitmapFont secondaryFont, string name, string description, string flavor, Texture2D? iconTexture, Texture2D? iconSilhouette, Dictionary<string, int> stats, Rectangle infoPanelArea, GameTime gameTime, string? iconPath = null)
        {
            if (iconTexture == null && !string.IsNullOrEmpty(iconPath))
            {
                iconTexture = _spriteManager.GetItemSprite(iconPath);
                iconSilhouette = _spriteManager.GetItemSpriteSilhouette(iconPath);
            }

            var tertiaryFont = _core.TertiaryFont;
            const int spriteSize = 16;

            // 1. Icon
            int spriteX = infoPanelArea.X + (infoPanelArea.Width - spriteSize) / 2;
            float currentY = infoPanelArea.Y + LAYOUT_SPRITE_Y + 2;
            Vector2 drawPos = new Vector2(spriteX + 8, currentY + 8);
            Vector2 iconOrigin = new Vector2(8, 8);
            Vector2 animOffset = GetJuicyOffset(gameTime);
            drawPos += animOffset;

            DrawIconWithSilhouette(spriteBatch, iconTexture, iconSilhouette, drawPos, iconOrigin, 1.0f);

            // 2. Name
            Vector2 nameSize = font.MeasureString(name.ToUpper());
            Vector2 namePos = new Vector2(infoPanelArea.X + (infoPanelArea.Width - nameSize.X) / 2f, infoPanelArea.Y + LAYOUT_TITLE_Y);
            namePos = new Vector2(MathF.Round(namePos.X), MathF.Round(namePos.Y));
            TextAnimator.DrawTextWithEffectOutlined(spriteBatch, font, name.ToUpper(), namePos, _global.Palette_BlueWhite, _global.Palette_Black, TextEffectType.DriftWave, (float)gameTime.TotalGameTime.TotalSeconds);

            // 3. Stat Grid
            currentY = infoPanelArea.Y + LAYOUT_VARS_START_Y;
            float leftLabelX = infoPanelArea.X + 8;
            float leftValueX = infoPanelArea.X + 28;
            float rightLabelX = infoPanelArea.X + 62;
            float rightValueX = infoPanelArea.X + 80;

            DrawStat(spriteBatch, secondaryFont, tertiaryFont, "STR", "Strength", stats, leftLabelX, leftValueX, currentY);
            DrawStat(spriteBatch, secondaryFont, tertiaryFont, "INT", "Intelligence", stats, rightLabelX, rightValueX, currentY);
            currentY += LAYOUT_VAR_ROW_HEIGHT;
            DrawStat(spriteBatch, secondaryFont, tertiaryFont, "TEN", "Tenacity", stats, leftLabelX, leftValueX, currentY);
            DrawStat(spriteBatch, secondaryFont, tertiaryFont, "AGI", "Agility", stats, rightLabelX, rightValueX, currentY);

            // Flavor
            float flavorHeight = DrawFlavorText(spriteBatch, infoPanelArea, flavor);

            // Description
            float descStartY = currentY + LAYOUT_VAR_ROW_HEIGHT + 2;
            DrawDescription(spriteBatch, secondaryFont, description, infoPanelArea, descStartY, flavorHeight, gameTime);
        }

        private void DrawSpellInfoPanel(SpriteBatch spriteBatch, BitmapFont font, BitmapFont secondaryFont, MoveData move, Texture2D? iconTexture, Texture2D? iconSilhouette, Rectangle? sourceRect, Color? iconTint, Rectangle idleFrame, Vector2 idleOrigin, bool drawBackground, Rectangle infoPanelArea, GameTime gameTime)
        {
            var tertiaryFont = _core.TertiaryFont; // Get Tertiary Font
            const int spriteSize = 32;
            const int gap = 2;

            int spriteX = infoPanelArea.X + (infoPanelArea.Width - spriteSize) / 2;
            int spriteY = infoPanelArea.Y + LAYOUT_SPRITE_Y - 3;

            Vector2 iconOrigin = new Vector2(16, 16);
            Vector2 drawPos = new Vector2(spriteX + 16, spriteY + 16);

            DrawIconWithSilhouette(spriteBatch, iconTexture, iconSilhouette, drawPos, iconOrigin, 1.0f, sourceRect, iconTint);

            string name = move.MoveName.ToUpper();
            Vector2 nameSize = font.MeasureString(name);
            Vector2 namePos = new Vector2(infoPanelArea.X + (infoPanelArea.Width - nameSize.X) / 2f, infoPanelArea.Y + LAYOUT_TITLE_Y);
            namePos = new Vector2(MathF.Round(namePos.X), MathF.Round(namePos.Y));
            TextAnimator.DrawTextWithEffectOutlined(spriteBatch, font, name, namePos, _global.Palette_BlueWhite, _global.Palette_Black, TextEffectType.DriftWave, (float)gameTime.TotalGameTime.TotalSeconds);

            float currentY = infoPanelArea.Y + LAYOUT_VARS_START_Y;
            float leftLabelX = infoPanelArea.X + 8;
            float leftValueX = infoPanelArea.X + 28;
            float rightLabelX = infoPanelArea.X + 62;
            float rightValueX = infoPanelArea.X + 80;

            string powVal = move.Power > 0 ? move.Power.ToString() : (move.Effects.ContainsKey("ManaDamage") ? "???" : "---");
            string accVal = move.Accuracy >= 0 ? $"{move.Accuracy}%" : "---";
            string mpVal = (move.ManaCost > 0 ? move.ManaCost.ToString() : "0") + "%";
            var manaDump = move.Abilities.OfType<ManaDumpAbility>().FirstOrDefault();
            if (manaDump != null) powVal = "???";

            DrawStatPair(spriteBatch, secondaryFont, tertiaryFont, "POW", powVal, leftLabelX, leftValueX, currentY, _global.Palette_White);
            DrawStatPair(spriteBatch, secondaryFont, tertiaryFont, "ACC", accVal, rightLabelX, rightValueX, currentY, _global.Palette_White);
            currentY += LAYOUT_VAR_ROW_HEIGHT;

            DrawStatPair(spriteBatch, secondaryFont, tertiaryFont, "MANA", mpVal, leftLabelX, leftValueX, currentY, _global.Palette_White);
            DrawStatPair(spriteBatch, secondaryFont, tertiaryFont, "TGT", GetTargetString(move.Target), rightLabelX, rightValueX, currentY, _global.Palette_White);
            currentY += LAYOUT_VAR_ROW_HEIGHT;

            string offStatVal = GetStatString(move.OffensiveStat);
            Color offColor = GetStatColor(move.OffensiveStat);
            string impactVal = move.ImpactType.ToString().ToUpper().Substring(0, Math.Min(4, move.ImpactType.ToString().Length));
            Color impactColor = move.ImpactType == ImpactType.Magical ? _global.Palette_LightBlue : (move.ImpactType == ImpactType.Physical ? _global.Palette_Orange : _global.Palette_Gray);

            DrawStatPair(spriteBatch, secondaryFont, tertiaryFont, "USE", offStatVal, leftLabelX, leftValueX, currentY, offColor);
            DrawStatPair(spriteBatch, secondaryFont, tertiaryFont, "TYP", impactVal, rightLabelX, rightValueX, currentY, impactColor);

            currentY = infoPanelArea.Y + LAYOUT_CONTACT_Y;
            if (move.MakesContact)
            {
                string contactText = "[ CONTACT ]";
                Vector2 contactSize = tertiaryFont.MeasureString(contactText);
                Vector2 contactPos = new Vector2(infoPanelArea.X + (infoPanelArea.Width - contactSize.X) / 2f, currentY + (secondaryFont.LineHeight - tertiaryFont.LineHeight) / 2f);
                spriteBatch.DrawStringSnapped(tertiaryFont, contactText, contactPos, _global.Palette_Red);
            }

            float flavorHeight = DrawFlavorText(spriteBatch, infoPanelArea, move.Flavor);
            currentY = infoPanelArea.Y + LAYOUT_DESC_START_Y;
            DrawDescription(spriteBatch, secondaryFont, move.Description, infoPanelArea, currentY, flavorHeight, gameTime);
        }

        private void DrawGenericItemInfoPanel(SpriteBatch spriteBatch, BitmapFont font, BitmapFont secondaryFont, string name, string description, string flavor, Texture2D? iconTexture, Texture2D? iconSilhouette, Dictionary<string, int> stats, Rectangle infoPanelArea, GameTime gameTime)
        {
            const int spriteSize = 16;
            int spriteX = infoPanelArea.X + (infoPanelArea.Width - spriteSize) / 2;
            float currentY = infoPanelArea.Y + LAYOUT_SPRITE_Y + 2;
            Vector2 drawPos = new Vector2(spriteX + 8, currentY + 8);
            Vector2 iconOrigin = new Vector2(8, 8);
            Vector2 animOffset = GetJuicyOffset(gameTime);
            drawPos += animOffset;

            DrawIconWithSilhouette(spriteBatch, iconTexture, iconSilhouette, drawPos, iconOrigin, 1.0f);

            Vector2 nameSize = font.MeasureString(name.ToUpper());
            Vector2 namePos = new Vector2(infoPanelArea.X + (infoPanelArea.Width - nameSize.X) / 2f, infoPanelArea.Y + LAYOUT_TITLE_Y);
            namePos = new Vector2(MathF.Round(namePos.X), MathF.Round(namePos.Y));
            TextAnimator.DrawTextWithEffectOutlined(spriteBatch, font, name.ToUpper(), namePos, _global.Palette_BlueWhite, _global.Palette_Black, TextEffectType.DriftWave, (float)gameTime.TotalGameTime.TotalSeconds);

            float flavorHeight = DrawFlavorText(spriteBatch, infoPanelArea, flavor);
            currentY = infoPanelArea.Y + LAYOUT_TITLE_Y + font.LineHeight + 4;
            DrawDescription(spriteBatch, secondaryFont, description, infoPanelArea, currentY, flavorHeight, gameTime);
        }

        // --- Helpers ---

        private void DrawIconWithSilhouette(SpriteBatch spriteBatch, Texture2D? texture, Texture2D? silhouette, Vector2 pos, Vector2 origin, float scale, Rectangle? sourceRect = null, Color? tint = null)
        {
            if (silhouette != null)
            {
                Color mainOutlineColor = _global.ItemOutlineColor_Idle;
                Color cornerOutlineColor = _global.ItemOutlineColor_Idle_Corner;

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
                spriteBatch.DrawSnapped(texture, pos, sourceRect, tint ?? Color.White, 0f, origin, scale, SpriteEffects.None, 0f);
            }
        }

        private void DrawStatPair(SpriteBatch spriteBatch, BitmapFont secondaryFont, BitmapFont tertiaryFont, string label, string value, float labelX, float valueX, float y, Color valColor)
        {
            float yOffset = (secondaryFont.LineHeight - tertiaryFont.LineHeight) / 2f;
            yOffset = MathF.Round(yOffset);
            spriteBatch.DrawStringSnapped(tertiaryFont, label, new Vector2(labelX, y + yOffset), _global.Palette_Gray);
            spriteBatch.DrawStringSnapped(secondaryFont, value, new Vector2(valueX, y), valColor);
        }

        private void DrawStat(SpriteBatch spriteBatch, BitmapFont secondaryFont, BitmapFont tertiaryFont, string label, string key, Dictionary<string, int> stats, float labelX, float valueX, float y)
        {
            float yOffset = (secondaryFont.LineHeight - tertiaryFont.LineHeight) / 2f;
            yOffset = MathF.Round(yOffset);
            spriteBatch.DrawStringSnapped(tertiaryFont, label, new Vector2(labelX, y + yOffset), _global.Palette_Gray);

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
            spriteBatch.DrawStringSnapped(secondaryFont, text, new Vector2(valueX, y), c);
        }

        private float DrawFlavorText(SpriteBatch spriteBatch, Rectangle infoPanelArea, string flavorText)
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

            // CHANGED: Added +2 to padding calculation to push text up from the new lower bottom
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
                        spriteBatch.DrawStringSnapped(tertiaryFont, segment.Text, new Vector2(currentX, startY), segment.Color);
                    }
                    currentX += segWidth;
                }
                startY += lineHeight + lineSpacing;
            }
            return totalHeight + 2 - 3;
        }

        private void DrawDescription(SpriteBatch spriteBatch, BitmapFont font, string description, Rectangle infoPanelArea, float startY, float flavorHeight, GameTime gameTime)
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
                            TextAnimator.DrawTextWithEffect(spriteBatch, font, segment.Text, new Vector2(currentX, lineY), segment.Color, segment.Effect, (float)gameTime.TotalGameTime.TotalSeconds);
                        else
                            spriteBatch.DrawStringSnapped(font, segment.Text, new Vector2(currentX, lineY), segment.Color);
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
                            finalColor = Color.Lerp(_global.Palette_Gray, currentColor, amount);
                        }
                    }

                    TextEffectType effect = TextEffectType.None;
                    if (Regex.IsMatch(part, @"\d+%"))
                    {
                        effect = TextEffectType.Drift;
                    }
                    currentLine.Add(new ColoredText(part, finalColor, effect));
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
            string tag = colorName.ToLowerInvariant();

            if (tag == "cstr") return _global.StatColor_Strength;
            if (tag == "cint") return _global.StatColor_Intelligence;
            if (tag == "cten") return _global.StatColor_Tenacity;
            if (tag == "cagi") return _global.StatColor_Agility;

            if (tag == "cpositive") return _global.ColorPositive;
            if (tag == "cnegative") return _global.ColorNegative;
            if (tag == "ccrit") return _global.ColorCrit;
            if (tag == "cimmune") return _global.ColorImmune;
            if (tag == "cctm") return _global.ColorConditionToMeet;
            if (tag == "cetc") return _global.Palette_DarkGray;

            if (tag == "cfire") return _global.ElementColors.GetValueOrDefault(2, Color.White);
            if (tag == "cwater") return _global.ElementColors.GetValueOrDefault(3, Color.White);
            if (tag == "carcane") return _global.ElementColors.GetValueOrDefault(4, Color.White);
            if (tag == "cearth") return _global.ElementColors.GetValueOrDefault(5, Color.White);
            if (tag == "cmetal") return _global.ElementColors.GetValueOrDefault(6, Color.White);
            if (tag == "ctoxic") return _global.ElementColors.GetValueOrDefault(7, Color.White);
            if (tag == "cwind") return _global.ElementColors.GetValueOrDefault(8, Color.White);
            if (tag == "cvoid") return _global.ElementColors.GetValueOrDefault(9, Color.White);
            if (tag == "clight") return _global.ElementColors.GetValueOrDefault(10, Color.White);
            if (tag == "celectric") return _global.ElementColors.GetValueOrDefault(11, Color.White);
            if (tag == "cice") return _global.ElementColors.GetValueOrDefault(12, Color.White);
            if (tag == "cnature") return _global.ElementColors.GetValueOrDefault(13, Color.White);

            if (tag.StartsWith("c"))
            {
                string effectName = tag.Substring(1);
                if (effectName == "poison") return _global.StatusEffectColors.GetValueOrDefault(StatusEffectType.Poison, Color.White);
                if (effectName == "stun") return _global.StatusEffectColors.GetValueOrDefault(StatusEffectType.Stun, Color.White);
                if (effectName == "regen") return _global.StatusEffectColors.GetValueOrDefault(StatusEffectType.Regen, Color.White);
                if (effectName == "dodging") return _global.StatusEffectColors.GetValueOrDefault(StatusEffectType.Dodging, Color.White);
                if (effectName == "burn") return _global.StatusEffectColors.GetValueOrDefault(StatusEffectType.Burn, Color.White);
                if (effectName == "frostbite") return _global.StatusEffectColors.GetValueOrDefault(StatusEffectType.Frostbite, Color.White);
                if (effectName == "silence") return _global.StatusEffectColors.GetValueOrDefault(StatusEffectType.Silence, Color.White);
            }

            switch (tag)
            {
                case "red": return _global.Palette_Red;
                case "blue": return _global.Palette_LightBlue;
                case "green": return _global.Palette_LightGreen;
                case "yellow": return _global.Palette_Yellow;
                case "orange": return _global.Palette_Orange;
                case "purple": return _global.Palette_LightPurple;
                case "pink": return _global.Palette_Pink;
                case "gray": return _global.Palette_Gray;
                case "white": return _global.Palette_White;
                case "BlueWhite": return _global.Palette_BlueWhite;
                case "darkgray": return _global.Palette_DarkGray;
                default: return _global.Palette_White;
            }
        }

        private (List<string> Positives, List<string> Negatives) GetStatModifierLines(Dictionary<string, int> mods)
        {
            var positives = new List<string>();
            var negatives = new List<string>();
            if (mods == null || mods.Count == 0) return (positives, negatives);

            foreach (var kvp in mods)
            {
                if (kvp.Value == 0) continue;
                string colorTag = kvp.Value > 0 ? "[cpositive]" : "[cnegative]";
                string sign = kvp.Value > 0 ? "+" : "";

                string statName = kvp.Key.ToLowerInvariant() switch
                {
                    "strength" => "STR",
                    "intelligence" => "INT",
                    "tenacity" => "TEN",
                    "agility" => "AGI",
                    "maxhp" => "HP",
                    "maxmana" => "MP",
                    _ => kvp.Key.ToUpper().Substring(0, Math.Min(3, kvp.Key.Length))
                };

                if (statName.Length < 3) statName += " ";

                string line = $"{statName} {colorTag}{sign}{kvp.Value}[/]";

                if (kvp.Value > 0) positives.Add(line);
                else negatives.Add(line);
            }
            return (positives, negatives);
        }

        private void DrawRectangleBorder(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, int thickness, Color color)
        {
            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Top, rect.Width, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Bottom - thickness, rect.Width, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Top, thickness, rect.Height), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Top, thickness, rect.Height), color);
        }
    }
}
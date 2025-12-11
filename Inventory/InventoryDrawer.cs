using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ProjectVagabond.UI
{
    public partial class SplitMapInventoryOverlay
    {
        // Fields for portrait background animation
        private Rectangle _portraitBgFrame;
        private float _portraitBgTimer;
        private float _portraitBgDuration;
        private static readonly Random _rng = new Random();
        public void DrawWorld(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            if (!IsOpen) return;
            var inventoryPosition = new Vector2(0, 200);
            var headerPosition = inventoryPosition + _inventoryPositionOffset;

            // --- PASS 1: Draw Info Panel Background (Idle Slot) BEFORE Borders ---
            if (_selectedInventoryCategory != InventoryCategory.Equip)
            {
                DrawInfoPanel(spriteBatch, font, ServiceLocator.Get<Core>().SecondaryFont, gameTime, true);
            }

            spriteBatch.DrawSnapped(_spriteManager.InventoryBorderHeader, headerPosition, Color.White);

            Texture2D selectedBorderSprite;
            if (_selectedInventoryCategory == InventoryCategory.Equip && _isEquipSubmenuOpen)
            {
                selectedBorderSprite = _spriteManager.InventoryBorderEquipSubmenu;
            }
            else
            {
                selectedBorderSprite = _selectedInventoryCategory switch
                {
                    InventoryCategory.Weapons => _spriteManager.InventoryBorderWeapons,
                    InventoryCategory.Armor => _spriteManager.InventoryBorderArmor,
                    InventoryCategory.Spells => _spriteManager.InventoryBorderSpells,
                    InventoryCategory.Relics => _spriteManager.InventoryBorderRelics,
                    InventoryCategory.Consumables => _spriteManager.InventoryBorderConsumables,
                    InventoryCategory.Misc => _spriteManager.InventoryBorderMisc,
                    InventoryCategory.Equip => _spriteManager.InventoryBorderEquip,
                    _ => _spriteManager.InventoryBorderWeapons,
                };
            }
            spriteBatch.DrawSnapped(selectedBorderSprite, inventoryPosition, Color.White);

            foreach (var button in _inventoryHeaderButtons) button.Draw(spriteBatch, font, gameTime, Matrix.Identity);

            // --- Draw Category Title ---
            if (_selectedInventoryCategory == InventoryCategory.Equip)
            {
                if (!_isEquipSubmenuOpen)
                {
                    string statsTitle = "STATS";
                    Vector2 statsTitleSize = font.MeasureString(statsTitle);
                    Vector2 statsTitlePos = new Vector2(
                        _infoPanelArea.X + (_infoPanelArea.Width - statsTitleSize.X) / 2f,
                        _infoPanelArea.Y + 5 + _inventoryPositionOffset.Y
                    );
                    spriteBatch.DrawStringSnapped(font, statsTitle, statsTitlePos, _global.Palette_DarkGray);
                }

                if (_weaponEquipButton != null)
                {
                    string equipTitle = "EQUIP";
                    Vector2 equipTitleSize = font.MeasureString(equipTitle);
                    float equipCenterX = _weaponEquipButton.Bounds.X + (_weaponEquipButton.Bounds.Width / 2f);
                    float titleY = _infoPanelArea.Y + 5 + _inventoryPositionOffset.Y;
                    Vector2 equipTitlePos = new Vector2(
                        equipCenterX - (equipTitleSize.X / 2f),
                        titleY
                    );
                    spriteBatch.DrawStringSnapped(font, equipTitle, equipTitlePos, _global.Palette_DarkGray);
                }
            }
            else
            {
                string categoryTitle = _selectedInventoryCategory.ToString().ToUpper();
                Vector2 titleSize = font.MeasureString(categoryTitle);
                Vector2 titlePos = new Vector2(
                    _infoPanelArea.X + (_infoPanelArea.Width - titleSize.X) / 2f,
                    _infoPanelArea.Y + 5 + _inventoryPositionOffset.Y
                );
                spriteBatch.DrawStringSnapped(font, categoryTitle, titlePos, _global.Palette_DarkGray);
            }

            if (_selectedInventoryCategory != InventoryCategory.Equip)
            {
                foreach (var slot in _inventorySlots) slot.Draw(spriteBatch, font, gameTime, Matrix.Identity);

                if (_totalPages > 1)
                {
                    var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
                    string pageText = $"{_currentPage + 1}/{_totalPages}";
                    var textSize = secondaryFont.MeasureString(pageText);
                    var textPos = new Vector2(
                        _inventorySlotArea.Center.X - textSize.Width / 2f,
                        _inventorySlotArea.Bottom - 2
                    );

                    var pixel = ServiceLocator.Get<Texture2D>();
                    var bgRect = new Rectangle(
                        (int)textPos.X - 1,
                        (int)textPos.Y + 2,
                        (int)Math.Ceiling(textSize.Width) + 5,
                        (int)textSize.Height
                    );
                    spriteBatch.DrawSnapped(pixel, bgRect, _global.Palette_Black);

                    spriteBatch.DrawStringSnapped(secondaryFont, pageText, textPos, _global.Palette_BrightWhite);

                    _pageLeftButton?.Draw(spriteBatch, font, gameTime, Matrix.Identity);
                    _pageRightButton?.Draw(spriteBatch, font, gameTime, Matrix.Identity);
                }

                // --- PASS 2: Draw Info Panel Content (Foreground) ---
                DrawInfoPanel(spriteBatch, font, ServiceLocator.Get<Core>().SecondaryFont, gameTime, false);
            }
            else
            {
                if (_isEquipSubmenuOpen)
                {
                    foreach (var button in _equipSubmenuButtons)
                    {
                        button.Draw(spriteBatch, font, gameTime, Matrix.Identity);
                    }

                    var arrowTexture = _spriteManager.InventoryScrollArrowsSprite;
                    var arrowRects = _spriteManager.InventoryScrollArrowRects;

                    if (arrowTexture != null && arrowRects != null && _equipSubmenuButtons.Count > 0)
                    {
                        int totalItems = 1;
                        if (_activeEquipSlotType == EquipSlotType.Weapon) totalItems += _gameState.PlayerState.Weapons.Count;
                        else if (_activeEquipSlotType == EquipSlotType.Armor) totalItems += _gameState.PlayerState.Armors.Count;
                        else if (_activeEquipSlotType == EquipSlotType.Relic1 || _activeEquipSlotType == EquipSlotType.Relic2 || _activeEquipSlotType == EquipSlotType.Relic3) totalItems += _gameState.PlayerState.Relics.Count;
                        else if (_activeEquipSlotType >= EquipSlotType.Spell1 && _activeEquipSlotType <= EquipSlotType.Spell4) totalItems += _gameState.PlayerState.Spells.Count;

                        int maxScroll = Math.Max(0, totalItems - 7);

                        if (_equipMenuScrollIndex > 0)
                        {
                            var firstButton = _equipSubmenuButtons[0];
                            var arrowPos = new Vector2(
                                firstButton.Bounds.Center.X - arrowRects[0].Width / 2f,
                                firstButton.Bounds.Top - arrowRects[0].Height
                            );
                            spriteBatch.DrawSnapped(arrowTexture, arrowPos, arrowRects[0], Color.White);
                        }

                        if (_equipMenuScrollIndex < maxScroll)
                        {
                            var lastButton = _equipSubmenuButtons.Last();
                            var arrowPos = new Vector2(
                                lastButton.Bounds.Center.X - arrowRects[1].Width / 2f,
                                lastButton.Bounds.Bottom
                            );
                            spriteBatch.DrawSnapped(arrowTexture, arrowPos, arrowRects[1], Color.White);
                        }
                    }
                }
                else
                {
                    _relicEquipButton1?.Draw(spriteBatch, font, gameTime, Matrix.Identity);
                    _relicEquipButton2?.Draw(spriteBatch, font, gameTime, Matrix.Identity);
                    _relicEquipButton3?.Draw(spriteBatch, font, gameTime, Matrix.Identity);
                    _armorEquipButton?.Draw(spriteBatch, font, gameTime, Matrix.Identity);
                    _weaponEquipButton?.Draw(spriteBatch, font, gameTime, Matrix.Identity);

                    foreach (var button in _spellEquipButtons)
                    {
                        button.Draw(spriteBatch, font, gameTime, Matrix.Identity);
                    }

                    // --- Draw "SPELL LIST" Label ---
                    if (_spellEquipButtons.Count > 0 && _weaponEquipButton != null)
                    {
                        var first = _spellEquipButtons[0];
                        var last = _spellEquipButtons[_spellEquipButtons.Count - 1];
                        float centerY = (first.Bounds.Top + last.Bounds.Bottom) / 2f;

                        float labelX = _weaponEquipButton.Bounds.X;
                        float labelWidth = 53f;

                        string[] lines = { "SPELL", "LIST" };
                        float totalHeight = lines.Length * font.LineHeight;
                        float currentY = centerY - (totalHeight / 2f);

                        foreach (var line in lines)
                        {
                            Vector2 lineSize = font.MeasureString(line);
                            float lineX = labelX + (labelWidth - lineSize.X) / 2f;
                            Vector2 linePos = new Vector2(MathF.Round(lineX), MathF.Round(currentY));
                            spriteBatch.DrawStringSnapped(font, line, linePos, _global.Palette_Gray);
                            currentY += font.LineHeight;
                        }
                    }
                }

                DrawStatsPanel(spriteBatch, font, ServiceLocator.Get<Core>().SecondaryFont, gameTime);
            }

            if (_debugButton1 != null && _debugButton1.IsEnabled) _debugButton1.Draw(spriteBatch, font, gameTime, Matrix.Identity);
            if (_debugButton2 != null && _debugButton2.IsEnabled) _debugButton2.Draw(spriteBatch, font, gameTime, Matrix.Identity);

            if (_global.ShowSplitMapGrid)
            {
                var pixel = ServiceLocator.Get<Texture2D>();
                spriteBatch.DrawSnapped(pixel, _inventorySlotArea, Color.Blue * 0.5f);

                if (_selectedInventoryCategory == InventoryCategory.Equip)
                {
                    spriteBatch.DrawSnapped(pixel, _statsPanelArea, Color.HotPink * 0.5f);

                    // Debug draw party buttons
                    if (!_isEquipSubmenuOpen)
                    {
                        foreach (var btn in _partySlotButtons)
                        {
                            if (btn.IsEnabled)
                                spriteBatch.DrawSnapped(pixel, btn.Bounds, Color.Yellow * 0.5f);
                        }
                    }
                }
                else
                {
                    spriteBatch.DrawSnapped(pixel, _infoPanelArea, Color.Cyan * 0.5f);
                }
            }

            _inventoryEquipButton?.Draw(spriteBatch, font, gameTime, Matrix.Identity);
        }

        public void DrawScreen(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            _inventoryButton?.Draw(spriteBatch, font, gameTime, transform);
        }

        private void DrawInfoPanel(SpriteBatch spriteBatch, BitmapFont font, BitmapFont secondaryFont, GameTime gameTime, bool drawBackground)
        {
            // --- Prepare Idle Slot Background ---
            var slotFrames = _spriteManager.InventorySlotSourceRects;
            Rectangle idleFrame = Rectangle.Empty;
            Vector2 idleOrigin = Vector2.Zero;
            if (slotFrames != null && slotFrames.Length > 0)
            {
                // Cycle frames slowly to simulate the idle animation
                int frameIndex = (int)(gameTime.TotalGameTime.TotalSeconds / 2.0) % slotFrames.Length;
                idleFrame = slotFrames[frameIndex];
                idleOrigin = new Vector2(idleFrame.Width / 2f, idleFrame.Height / 2f);
            }

            InventorySlot? activeSlot = _inventorySlots.FirstOrDefault(s => s.IsSelected);
            if (activeSlot == null)
            {
                activeSlot = _inventorySlots.FirstOrDefault(s => s.IsHovered);
            }

            // --- Empty State: Draw Idle Slot ---
            if (activeSlot == null || !activeSlot.HasItem || string.IsNullOrEmpty(activeSlot.ItemId))
            {
                if (drawBackground && idleFrame != Rectangle.Empty)
                {
                    // Calculate position to match the "hovered" state as closely as possible.
                    // Standardized to 6f for all categories to align them.
                    float spriteYOffset = 6f;

                    int idleSpriteX = _infoPanelArea.X + (_infoPanelArea.Width - 24) / 2;
                    float spriteY = _infoPanelArea.Y + spriteYOffset;

                    // Draw 24x24 frame at 1.0 scale
                    Vector2 itemCenter = new Vector2(idleSpriteX + 12, spriteY + 12);
                    spriteBatch.DrawSnapped(_spriteManager.InventorySlotIdleSpriteSheet, itemCenter, idleFrame, Color.White, 0f, idleOrigin, 1.0f, SpriteEffects.None, 0f);
                }
                return;
            }

            string name = activeSlot.ItemId.ToUpper();
            string description = "";
            string iconPath = activeSlot.IconPath ?? "";
            Texture2D? iconTexture = null;
            Texture2D? iconSilhouette = null;
            (List<string> Positives, List<string> Negatives) statLines = (new List<string>(), new List<string>());
            string? fallbackPath = null;
            MoveData? spellData = null;

            // --- Data Retrieval ---
            if (_selectedInventoryCategory == InventoryCategory.Relics)
            {
                var relic = BattleDataCache.Relics.Values.FirstOrDefault(r => r.RelicName.Equals(activeSlot.ItemId, StringComparison.OrdinalIgnoreCase));
                if (relic != null)
                {
                    name = relic.RelicName.ToUpper();
                    description = relic.Description.ToUpper();
                    iconPath = $"Sprites/Items/Relics/{relic.RelicID}";
                    statLines = GetStatModifierLines(relic.StatModifiers);
                }
            }
            else if (_selectedInventoryCategory == InventoryCategory.Consumables)
            {
                var item = BattleDataCache.Consumables.Values.FirstOrDefault(c => c.ItemName.Equals(activeSlot.ItemId, StringComparison.OrdinalIgnoreCase));
                if (item != null)
                {
                    name = item.ItemName.ToUpper();
                    description = item.Description.ToUpper();
                    iconPath = item.ImagePath;
                }
            }
            else if (_selectedInventoryCategory == InventoryCategory.Misc) // <--- Added
            {
                var item = BattleDataCache.MiscItems.Values.FirstOrDefault(m => m.ItemName.Equals(activeSlot.ItemId, StringComparison.OrdinalIgnoreCase));
                if (item != null)
                {
                    name = item.ItemName.ToUpper();
                    description = item.Description.ToUpper();
                    iconPath = item.ImagePath;
                }
            }
            else if (_selectedInventoryCategory == InventoryCategory.Spells)
            {
                spellData = BattleDataCache.Moves.Values.FirstOrDefault(m => m.MoveName.Equals(activeSlot.ItemId, StringComparison.OrdinalIgnoreCase));
                if (spellData != null)
                {
                    name = spellData.MoveName.ToUpper();
                    description = spellData.Description.ToUpper();
                    iconPath = $"Sprites/Items/Spells/{spellData.MoveID}";

                    int elementId = spellData.OffensiveElementIDs.FirstOrDefault();
                    if (BattleDataCache.Elements.TryGetValue(elementId, out var elementDef))
                    {
                        string elName = elementDef.ElementName.ToLowerInvariant();
                        if (elName == "---") elName = "neutral";
                        fallbackPath = $"Sprites/Items/Spells/default_{elName}";
                    }
                }
            }
            else if (_selectedInventoryCategory == InventoryCategory.Weapons)
            {
                var weapon = BattleDataCache.Weapons.Values.FirstOrDefault(w => w.WeaponName.Equals(activeSlot.ItemId, StringComparison.OrdinalIgnoreCase));
                if (weapon != null)
                {
                    name = weapon.WeaponName.ToUpper();
                    description = weapon.Description.ToUpper();
                    iconPath = $"Sprites/Items/Weapons/{weapon.WeaponID}";
                    statLines = GetStatModifierLines(weapon.StatModifiers);
                }
            }
            else if (_selectedInventoryCategory == InventoryCategory.Armor)
            {
                var armor = BattleDataCache.Armors.Values.FirstOrDefault(a => a.ArmorName.Equals(activeSlot.ItemId, StringComparison.OrdinalIgnoreCase));
                if (armor != null)
                {
                    name = armor.ArmorName.ToUpper();
                    description = armor.Description.ToUpper();
                    iconPath = $"Sprites/Items/Armor/{armor.ArmorID}";
                    statLines = GetStatModifierLines(armor.StatModifiers);
                }
            }

            if (!string.IsNullOrEmpty(iconPath))
            {
                iconTexture = _spriteManager.GetItemSprite(iconPath, fallbackPath);
                iconSilhouette = _spriteManager.GetItemSpriteSilhouette(iconPath, fallbackPath);
            }

            Rectangle? sourceRect = null;
            if (activeSlot.IsAnimated && iconTexture != null)
            {
                sourceRect = _spriteManager.GetAnimatedIconSourceRect(iconTexture, gameTime);
            }

            // --- SPECIAL RENDERING FOR SPELLS ---
            if (_selectedInventoryCategory == InventoryCategory.Spells && spellData != null)
            {
                DrawSpellInfoPanel(spriteBatch, font, secondaryFont, spellData, iconTexture, iconSilhouette, sourceRect, activeSlot.IconTint, idleFrame, idleOrigin, drawBackground);
                return;
            }

            // --- STANDARD RENDERING FOR OTHER CATEGORIES ---
            const int spriteSize = 16; // Native 16x16
            const int gap = 4;

            int maxTitleWidth = _infoPanelArea.Width - (4 * 2);
            var titleLines = ParseAndWrapRichText(font, name, maxTitleWidth, _global.Palette_BrightWhite);
            float totalTitleHeight = titleLines.Count * font.LineHeight;

            float totalDescHeight = 0f;
            List<List<ColoredText>> descLines = new List<List<ColoredText>>();
            if (!string.IsNullOrEmpty(description))
            {
                float descWidth = _infoPanelArea.Width - (4 * 2);
                descLines = ParseAndWrapRichText(secondaryFont, description, descWidth, _global.Palette_White);
                totalDescHeight = descLines.Count * secondaryFont.LineHeight;
            }

            float totalStatHeight = Math.Max(statLines.Positives.Count, statLines.Negatives.Count) * secondaryFont.LineHeight;
            float totalContentHeight = spriteSize + gap + totalTitleHeight + (totalDescHeight > 0 ? gap + totalDescHeight : 0) + (totalStatHeight > 0 ? gap + totalStatHeight : 0);

            float currentY = _infoPanelArea.Y + (_infoPanelArea.Height - totalContentHeight) / 2f;
            currentY -= 22f; // Moved up 12px (10 + 12)

            int spriteX = _infoPanelArea.X + (_infoPanelArea.Width - spriteSize) / 2;

            // Scale factor for Info Panel (1.0x for 16x16 sprites)
            float displayScale = 1.0f;
            // Scale factor for background frame (24x24 -> 24x24)
            float bgScale = 1.0f;

            if (drawBackground)
            {
                // Draw Idle Background Behind Item
                if (idleFrame != Rectangle.Empty)
                {
                    // Center the 24x24 background on the 16x16 sprite position
                    // Sprite center is (spriteX + 8, currentY + 8)
                    Vector2 itemCenter = new Vector2(spriteX + 8, currentY + 8);
                    spriteBatch.DrawSnapped(_spriteManager.InventorySlotIdleSpriteSheet, itemCenter, idleFrame, Color.White, 0f, idleOrigin, bgScale, SpriteEffects.None, 0f);
                }
                return; // Exit if only drawing background
            }

            // Calculate origin for 16x16 sprite
            Vector2 iconOrigin = new Vector2(8, 8); // Center of 16x16
            Vector2 drawPos = new Vector2(spriteX + 8, currentY + 8); // Center of 16x16 area

            if (iconSilhouette != null)
            {
                Color mainOutlineColor = _global.ItemOutlineColor_Idle;
                Color cornerOutlineColor = _global.ItemOutlineColor_Idle_Corner;

                // Draw scaled silhouette
                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(-1, -1) * displayScale, sourceRect, cornerOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(1, -1) * displayScale, sourceRect, cornerOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(-1, 1) * displayScale, sourceRect, cornerOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(1, 1) * displayScale, sourceRect, cornerOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);

                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(-1, 0) * displayScale, sourceRect, mainOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(1, 0) * displayScale, sourceRect, mainOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(0, -1) * displayScale, sourceRect, mainOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(0, 1) * displayScale, sourceRect, mainOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
            }

            if (iconTexture != null)
            {
                Color tint = activeSlot.IconTint ?? Color.White;
                spriteBatch.DrawSnapped(iconTexture, drawPos, sourceRect, tint, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
            }

            currentY += spriteSize + gap;

            foreach (var line in titleLines)
            {
                float lineWidth = 0;
                foreach (var segment in line)
                {
                    if (string.IsNullOrWhiteSpace(segment.Text))
                        lineWidth += segment.Text.Length * SPACE_WIDTH;
                    else
                        lineWidth += font.MeasureString(segment.Text).Width;
                }

                float lineX = _infoPanelArea.X + (_infoPanelArea.Width - lineWidth) / 2f;
                float currentX = lineX;

                foreach (var segment in line)
                {
                    float segWidth;
                    if (string.IsNullOrWhiteSpace(segment.Text))
                    {
                        segWidth = segment.Text.Length * SPACE_WIDTH;
                    }
                    else
                    {
                        segWidth = font.MeasureString(segment.Text).Width;
                        spriteBatch.DrawStringSnapped(font, segment.Text, new Vector2(currentX, currentY), segment.Color);
                    }
                    currentX += segWidth;
                }
                currentY += font.LineHeight;
            }

            if (descLines.Any())
            {
                currentY += gap;
                foreach (var line in descLines)
                {
                    float lineWidth = 0;
                    foreach (var segment in line)
                    {
                        if (string.IsNullOrWhiteSpace(segment.Text))
                            lineWidth += segment.Text.Length * SPACE_WIDTH;
                        else
                            lineWidth += secondaryFont.MeasureString(segment.Text).Width;
                    }

                    var lineX = _infoPanelArea.X + (_infoPanelArea.Width - lineWidth) / 2;
                    float currentX = lineX;

                    foreach (var segment in line)
                    {
                        float segWidth;
                        if (string.IsNullOrWhiteSpace(segment.Text))
                        {
                            segWidth = segment.Text.Length * SPACE_WIDTH;
                        }
                        else
                        {
                            segWidth = secondaryFont.MeasureString(segment.Text).Width;
                            spriteBatch.DrawStringSnapped(secondaryFont, segment.Text, new Vector2(currentX, currentY), segment.Color);
                        }
                        currentX += segWidth;
                    }
                    currentY += secondaryFont.LineHeight;
                }
            }

            if (statLines.Positives.Any() || statLines.Negatives.Any())
            {
                currentY += gap;
                float leftColX = _infoPanelArea.X + 8;
                float rightColX = _infoPanelArea.X + 64;

                int maxRows = Math.Max(statLines.Positives.Count, statLines.Negatives.Count);

                for (int i = 0; i < maxRows; i++)
                {
                    if (i < statLines.Positives.Count)
                    {
                        var lineParts = ParseAndWrapRichText(secondaryFont, statLines.Positives[i], _infoPanelArea.Width / 2, _global.Palette_White);
                        if (lineParts.Count > 0)
                        {
                            var segments = lineParts[0];
                            float currentX = leftColX;
                            foreach (var segment in segments)
                            {
                                float segWidth = string.IsNullOrWhiteSpace(segment.Text) ? segment.Text.Length * SPACE_WIDTH : secondaryFont.MeasureString(segment.Text).Width;
                                spriteBatch.DrawStringSnapped(secondaryFont, segment.Text, new Vector2(currentX, currentY), segment.Color);
                                currentX += segWidth;
                            }
                        }
                    }

                    if (i < statLines.Negatives.Count)
                    {
                        var lineParts = ParseAndWrapRichText(secondaryFont, statLines.Negatives[i], _infoPanelArea.Width / 2, _global.Palette_White);
                        if (lineParts.Count > 0)
                        {
                            var segments = lineParts[0];
                            float currentX = rightColX;
                            foreach (var segment in segments)
                            {
                                float segWidth = string.IsNullOrWhiteSpace(segment.Text) ? segment.Text.Length * SPACE_WIDTH : secondaryFont.MeasureString(segment.Text).Width;
                                spriteBatch.DrawStringSnapped(secondaryFont, segment.Text, new Vector2(currentX, currentY), segment.Color);
                                currentX += segWidth;
                            }
                        }
                    }
                    currentY += secondaryFont.LineHeight;
                }
            }
        }

        private void DrawSpellInfoPanel(SpriteBatch spriteBatch, BitmapFont font, BitmapFont secondaryFont, MoveData move, Texture2D? iconTexture, Texture2D? iconSilhouette, Rectangle? sourceRect, Color? iconTint, Rectangle idleFrame, Vector2 idleOrigin, bool drawBackground)
        {
            const int spriteSize = 16; // Native 16x16
            const int padding = 4;
            const int gap = 2;

            // 1. Draw Sprite (Centered Horizontally, Top of Panel)
            int spriteX = _infoPanelArea.X + (_infoPanelArea.Width - spriteSize) / 2;
            int spriteY = _infoPanelArea.Y + 2; // Moved up from 18 to 2

            // Scale factors set to 1.0 for native resolution
            float displayScale = 1.0f;
            float bgScale = 1.0f;

            if (drawBackground)
            {
                // Draw Idle Background Behind Item
                if (idleFrame != Rectangle.Empty)
                {
                    Vector2 itemCenter = new Vector2(spriteX + 8, spriteY + 8);
                    spriteBatch.DrawSnapped(_spriteManager.InventorySlotIdleSpriteSheet, itemCenter, idleFrame, Color.White, 0f, idleOrigin, bgScale, SpriteEffects.None, 0f);
                }
                return; // Exit if only drawing background
            }

            // Calculate origin for 16x16 sprite
            Vector2 iconOrigin = new Vector2(8, 8); // Center of 16x16
            Vector2 drawPos = new Vector2(spriteX + 8, spriteY + 8); // Center of 16x16 area

            if (iconSilhouette != null)
            {
                Color mainOutlineColor = _global.ItemOutlineColor_Idle;
                Color cornerOutlineColor = _global.ItemOutlineColor_Idle_Corner;

                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(-1, -1) * displayScale, sourceRect, cornerOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(1, -1) * displayScale, sourceRect, cornerOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(-1, 1) * displayScale, sourceRect, cornerOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(1, 1) * displayScale, sourceRect, cornerOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);

                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(-1, 0) * displayScale, sourceRect, mainOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(1, 0) * displayScale, sourceRect, mainOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(0, -1) * displayScale, sourceRect, mainOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(0, 1) * displayScale, sourceRect, mainOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
            }

            if (iconTexture != null)
            {
                Color tint = iconTint ?? Color.White;
                spriteBatch.DrawSnapped(iconTexture, drawPos, sourceRect, tint, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
            }

            // 2. Draw Name (Overlapping Bottom of Sprite)
            string name = move.MoveName.ToUpper();
            Vector2 nameSize = font.MeasureString(name);
            Vector2 namePos = new Vector2(
                _infoPanelArea.X + (_infoPanelArea.Width - nameSize.X) / 2f,
                spriteY + spriteSize - (font.LineHeight / 2f) + 2
            );

            // Draw outline for readability
            spriteBatch.DrawStringOutlinedSnapped(font, name, namePos, _global.Palette_BrightWhite, _global.Palette_Black);

            float currentY = namePos.Y + font.LineHeight + 2;

            // 3. Draw Stats Grid
            // Columns
            float leftLabelX = _infoPanelArea.X + 8;
            float leftValueRightX = _infoPanelArea.X + 51; // Moved left by 5

            float rightLabelX = _infoPanelArea.X + 59; // Moved left by 5
            float rightValueRightX = _infoPanelArea.X + 112;

            // Helper to draw stat pair with fixed alignment
            void DrawStatPair(string label, string value, float labelX, float valueRightX, float y, Color valColor)
            {
                // Draw Label (Left Aligned)
                spriteBatch.DrawStringSnapped(secondaryFont, label, new Vector2(labelX, y), _global.Palette_Gray);

                // Draw Value (Right Aligned)
                float valWidth = secondaryFont.MeasureString(value).Width;
                spriteBatch.DrawStringSnapped(secondaryFont, value, new Vector2(valueRightX - valWidth, y), valColor);
            }

            // Row 1: Power | Accuracy
            string powVal = move.Power > 0 ? move.Power.ToString() : "---";
            string accVal = move.Accuracy >= 0 ? $"{move.Accuracy}%" : "---";
            DrawStatPair("POW", powVal, leftLabelX, leftValueRightX, currentY, _global.Palette_White);
            DrawStatPair("ACC", accVal, rightLabelX, rightValueRightX, currentY, _global.Palette_White);
            currentY += secondaryFont.LineHeight + gap;

            // Row 2: Mana | Target
            string mpVal = move.ManaCost > 0 ? move.ManaCost.ToString() : "0";
            string targetVal = move.Target switch
            {
                TargetType.Single => "SINGL",
                TargetType.SingleAll => "ANY",
                TargetType.Both => "BOTH",
                TargetType.Every => "EVERY",
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
            DrawStatPair("MP ", mpVal, leftLabelX, leftValueRightX, currentY, _global.Palette_LightBlue);
            DrawStatPair("TGT", targetVal, rightLabelX, rightValueRightX, currentY, _global.Palette_White);
            currentY += secondaryFont.LineHeight + gap;

            // Row 3: Off. Stat | Impact
            string offStatVal = move.OffensiveStat switch
            {
                OffensiveStatType.Strength => "STR",
                OffensiveStatType.Intelligence => "INT",
                OffensiveStatType.Tenacity => "TEN",
                OffensiveStatType.Agility => "AGI",
                _ => "---"
            };

            Color offColor = move.OffensiveStat switch
            {
                OffensiveStatType.Strength => _global.StatColor_Strength,
                OffensiveStatType.Intelligence => _global.StatColor_Intelligence,
                OffensiveStatType.Tenacity => _global.StatColor_Tenacity,
                OffensiveStatType.Agility => _global.StatColor_Agility,
                _ => _global.Palette_White
            };

            string impactVal = move.ImpactType.ToString().ToUpper().Substring(0, Math.Min(4, move.ImpactType.ToString().Length)); // PHYS, MAGI, STAT
            Color impactColor = move.ImpactType == ImpactType.Magical ? _global.Palette_LightBlue : (move.ImpactType == ImpactType.Physical ? _global.Palette_Orange : _global.Palette_Gray);

            DrawStatPair("USE", offStatVal, leftLabelX, leftValueRightX, currentY, offColor);
            DrawStatPair("TYP", impactVal, rightLabelX, rightValueRightX, currentY, impactColor);
            currentY += secondaryFont.LineHeight + gap;

            // Row 4: Contact (Centered if true)
            if (move.MakesContact)
            {
                string contactText = "[MAKES CONTACT]";
                Vector2 contactSize = secondaryFont.MeasureString(contactText);
                Vector2 contactPos = new Vector2(_infoPanelArea.X + (_infoPanelArea.Width - contactSize.X) / 2f, currentY);
                spriteBatch.DrawStringSnapped(secondaryFont, contactText, contactPos, _global.Palette_Red);
                currentY += secondaryFont.LineHeight + gap;
            }

            // 4. Draw Description
            string description = move.Description.ToUpper();
            if (!string.IsNullOrEmpty(description))
            {
                float descWidth = _infoPanelArea.Width - (padding * 2);
                var descLines = ParseAndWrapRichText(secondaryFont, description, descWidth, _global.Palette_White);

                // Limit to 8 lines
                int maxLines = 8;
                if (descLines.Count > maxLines)
                {
                    descLines = descLines.Take(maxLines).ToList();
                }

                // Calculate height
                float totalDescHeight = descLines.Count * secondaryFont.LineHeight;

                // Anchor to bottom
                float bottomPadding = 8f;
                float startY = _infoPanelArea.Bottom - bottomPadding - totalDescHeight;

                float lineY = startY;
                foreach (var line in descLines)
                {
                    float lineWidth = 0;
                    foreach (var segment in line)
                    {
                        if (string.IsNullOrWhiteSpace(segment.Text))
                            lineWidth += segment.Text.Length * SPACE_WIDTH;
                        else
                            lineWidth += secondaryFont.MeasureString(segment.Text).Width;
                    }

                    float lineX = _infoPanelArea.X + (_infoPanelArea.Width - lineWidth) / 2;
                    float currentX = lineX;

                    foreach (var segment in line)
                    {
                        float segWidth;
                        if (string.IsNullOrWhiteSpace(segment.Text))
                        {
                            segWidth = segment.Text.Length * SPACE_WIDTH;
                        }
                        else
                        {
                            segWidth = secondaryFont.MeasureString(segment.Text).Width;
                            spriteBatch.DrawStringSnapped(secondaryFont, segment.Text, new Vector2(currentX, lineY), segment.Color);
                        }
                        currentX += segWidth;
                    }
                    lineY += secondaryFont.LineHeight;
                }
            }
        }

        private void DrawStatsPanel(SpriteBatch spriteBatch, BitmapFont font, BitmapFont secondaryFont, GameTime gameTime)
        {
            // --- Draw Hovered Item Details ---
            if (_hoveredItemData != null)
            {
                // --- SPECIAL HANDLING FOR SPELLS ---
                if (_hoveredItemData is MoveData move)
                {
                    string path = $"Sprites/Items/Spells/{move.MoveID}";

                    // Fallback logic for spell icons
                    int elementId = move.OffensiveElementIDs.FirstOrDefault();
                    string? fallbackPath = null;
                    if (BattleDataCache.Elements.TryGetValue(elementId, out var elementDef))
                    {
                        string elName = elementDef.ElementName.ToLowerInvariant();
                        if (elName == "---") elName = "neutral";
                        fallbackPath = $"Sprites/Items/Spells/default_{elName}";
                    }

                    var iconTexture = _spriteManager.GetItemSprite(path, fallbackPath);
                    var iconSilhouette = _spriteManager.GetItemSpriteSilhouette(path, fallbackPath);
                    var sourceRect = _spriteManager.GetAnimatedIconSourceRect(iconTexture, gameTime);

                    DrawSpellInfoPanel(spriteBatch, font, secondaryFont, move, iconTexture, iconSilhouette, sourceRect, null, Rectangle.Empty, Vector2.Zero, false);
                }
                else
                {
                    const int padding = 2;
                    const int spriteSize = 16; // Use 16px for tight fit in stats panel
                    const int gap = 4;

                    // 1. Sprite (Top Left - Centered Horizontally)
                    int spriteX = _statsPanelArea.X + (_statsPanelArea.Width - spriteSize) / 2;
                    int spriteY = _statsPanelArea.Y + 12; // Moved down 12px

                    string name = "";
                    string description = "";
                    string path = "";
                    Texture2D? itemSprite = null;
                    Texture2D? itemSilhouette = null;

                    if (_hoveredItemData is RelicData relic)
                    {
                        name = relic.RelicName.ToUpper();
                        description = relic.Description.ToUpper();
                        path = $"Sprites/Items/Relics/{relic.RelicID}";
                        itemSprite = _spriteManager.GetRelicSprite(path);
                        itemSilhouette = _spriteManager.GetRelicSpriteSilhouette(path);
                    }
                    else if (_hoveredItemData is WeaponData weapon)
                    {
                        name = weapon.WeaponName.ToUpper();
                        description = weapon.Description.ToUpper();
                        path = $"Sprites/Items/Weapons/{weapon.WeaponID}";
                        itemSprite = _spriteManager.GetItemSprite(path);
                        itemSilhouette = _spriteManager.GetItemSpriteSilhouette(path);
                    }
                    else if (_hoveredItemData is ArmorData armor)
                    {
                        name = armor.ArmorName.ToUpper();
                        description = armor.Description.ToUpper();
                        path = $"Sprites/Items/Armor/{armor.ArmorID}";
                        itemSprite = _spriteManager.GetItemSprite(path);
                        itemSilhouette = _spriteManager.GetItemSpriteSilhouette(path);
                    }

                    if (itemSilhouette != null)
                    {
                        Color mainOutlineColor = _global.ItemOutlineColor_Idle;
                        Color cornerOutlineColor = _global.ItemOutlineColor_Idle_Corner;

                        spriteBatch.DrawSnapped(itemSilhouette, new Vector2(spriteX - 1, spriteY - 1), cornerOutlineColor);
                        spriteBatch.DrawSnapped(itemSilhouette, new Vector2(spriteX + 1, spriteY - 1), cornerOutlineColor);
                        spriteBatch.DrawSnapped(itemSilhouette, new Vector2(spriteX - 1, spriteY + 1), cornerOutlineColor);
                        spriteBatch.DrawSnapped(itemSilhouette, new Vector2(spriteX + 1, spriteY + 1), cornerOutlineColor);

                        spriteBatch.DrawSnapped(itemSilhouette, new Vector2(spriteX - 1, spriteY), mainOutlineColor);
                        spriteBatch.DrawSnapped(itemSilhouette, new Vector2(spriteX + 1, spriteY), mainOutlineColor);
                        spriteBatch.DrawSnapped(itemSilhouette, new Vector2(spriteX, spriteY - 1), mainOutlineColor);
                        spriteBatch.DrawSnapped(itemSilhouette, new Vector2(spriteX, spriteY + 1), mainOutlineColor);
                    }

                    if (itemSprite != null)
                    {
                        spriteBatch.DrawSnapped(itemSprite, new Vector2(spriteX, spriteY), Color.White);
                    }

                    // 2. Title
                    // Added + 12 to move it down as requested
                    float titleBottomY = spriteY + spriteSize + gap + 12;
                    int maxTitleWidth = _statsPanelArea.Width - (4 * 2);

                    var titleLines = ParseAndWrapRichText(font, name, maxTitleWidth, _global.Palette_BrightWhite);
                    float totalTitleHeight = titleLines.Count * font.LineHeight;

                    float currentTitleY = titleBottomY - totalTitleHeight;

                    foreach (var line in titleLines)
                    {
                        float lineWidth = 0;
                        foreach (var segment in line)
                        {
                            if (string.IsNullOrWhiteSpace(segment.Text))
                                lineWidth += segment.Text.Length * SPACE_WIDTH;
                            else
                                lineWidth += font.MeasureString(segment.Text).Width;
                        }

                        float lineX = _statsPanelArea.X + (_statsPanelArea.Width - lineWidth) / 2f;
                        float currentX = lineX;

                        foreach (var segment in line)
                        {
                            float segWidth;
                            if (string.IsNullOrWhiteSpace(segment.Text))
                            {
                                segWidth = segment.Text.Length * SPACE_WIDTH;
                            }
                            else
                            {
                                segWidth = font.MeasureString(segment.Text).Width;
                                spriteBatch.DrawStringOutlinedSnapped(font, segment.Text, new Vector2(currentX, currentTitleY), segment.Color, _global.Palette_Black);
                            }
                            currentX += segWidth;
                        }
                        currentTitleY += font.LineHeight;
                    }

                    // 3. Description
                    int statsStartY = _statsPanelArea.Y + 77;
                    float descAreaTop = titleBottomY;
                    float descAreaBottom = statsStartY;
                    float descAreaHeight = descAreaBottom - descAreaTop;

                    float descWidth = _statsPanelArea.Width - (4 * 2);
                    var descLines = ParseAndWrapRichText(secondaryFont, description, descWidth, _global.Palette_White);
                    float totalDescHeight = descLines.Count * secondaryFont.LineHeight;

                    float currentDescY = descAreaTop + (descAreaHeight - totalDescHeight) / 2f;

                    foreach (var line in descLines)
                    {
                        float lineWidth = 0;
                        foreach (var segment in line)
                        {
                            if (string.IsNullOrWhiteSpace(segment.Text))
                                lineWidth += segment.Text.Length * SPACE_WIDTH;
                            else
                                lineWidth += secondaryFont.MeasureString(segment.Text).Width;
                        }

                        var lineX = _statsPanelArea.X + (_statsPanelArea.Width - lineWidth) / 2;
                        float currentX = lineX;

                        foreach (var segment in line)
                        {
                            float segWidth;
                            if (string.IsNullOrWhiteSpace(segment.Text))
                            {
                                segWidth = segment.Text.Length * SPACE_WIDTH;
                            }
                            else
                            {
                                segWidth = secondaryFont.MeasureString(segment.Text).Width;
                                spriteBatch.DrawStringSnapped(secondaryFont, segment.Text, new Vector2(currentX, currentDescY), segment.Color);
                            }
                            currentX += segWidth;
                        }
                        currentDescY += secondaryFont.LineHeight;
                    }
                }
            }
            else
            {
                // --- DRAW PARTY MEMBER LIST (Accordion Style) ---
                if (!_isEquipSubmenuOpen)
                {
                    // Layout Constants
                    const int portraitSize = 32;
                    const int buttonHeight = 8;
                    const int suiteHeight = 32; // Height reserved for the expanded suite (matches portrait)

                    // Base Y position
                    int startY = _statsPanelArea.Y + 18;

                    // Base X position for list (centered in panel, shifted left by 8px)
                    int listX = _statsPanelArea.X + (_statsPanelArea.Width - 90) / 2 - 8;

                    // 1. Calculate Y positions for all slots
                    int[] yPositions = new int[4];
                    int tempY = startY;
                    for (int i = 0; i < 4; i++)
                    {
                        yPositions[i] = tempY;
                        if (i == _currentPartyMemberIndex) tempY += suiteHeight;
                        else tempY += buttonHeight;
                    }

                    // 2. Update Background Animation State (Once per frame)
                    float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
                    _portraitBgTimer += dt;
                    if (_portraitBgTimer >= _portraitBgDuration)
                    {
                        _portraitBgTimer = 0f;
                        _portraitBgDuration = (float)(_rng.NextDouble() * (8.0 - 2.0) + 2.0);
                        var frames = _spriteManager.InventorySlotSourceRects;
                        if (frames != null && frames.Length > 0) _portraitBgFrame = frames[_rng.Next(frames.Length)];
                    }
                    if (_portraitBgFrame == Rectangle.Empty)
                    {
                        var frames = _spriteManager.InventorySlotSourceRects;
                        if (frames != null && frames.Length > 0) _portraitBgFrame = frames[_rng.Next(frames.Length)];
                    }

                    // 3. Draw Background for Selected Member (Bottom Layer)
                    // This ensures the shadow sprite is drawn behind all buttons
                    if (_currentPartyMemberIndex >= 0 && _currentPartyMemberIndex < 4)
                    {
                        int i = _currentPartyMemberIndex;
                        int portraitX = listX;
                        int portraitY = yPositions[i];
                        var destRect = new Rectangle(portraitX, portraitY, portraitSize, portraitSize);

                        if (_portraitBgFrame != Rectangle.Empty)
                        {
                            Vector2 center = destRect.Center.ToVector2();
                            Vector2 origin = new Vector2(_portraitBgFrame.Width / 2f, _portraitBgFrame.Height / 2f);
                            // Scale up the 24x24 frame to 32x32
                            float scale = 32f / 24f;
                            spriteBatch.DrawSnapped(_spriteManager.InventorySlotIdleSpriteSheet, center, _portraitBgFrame, Color.White, 0f, origin, scale, SpriteEffects.None, 0f);
                        }
                    }

                    // 4. Draw Buttons and Portrait Foregrounds
                    for (int i = 0; i < 4; i++)
                    {
                        int currentY = yPositions[i];

                        // Is this the selected member?
                        if (i == _currentPartyMemberIndex)
                        {
                            // --- DRAW INFO SUITE FOREGROUND ---
                            var member = _gameState.PlayerState.Party[i];

                            if (_spriteManager.PlayerPortraitsSpriteSheet != null && _spriteManager.PlayerPortraitSourceRects.Count > 0)
                            {
                                int portraitIndex = Math.Clamp(member.PortraitIndex, 0, _spriteManager.PlayerPortraitSourceRects.Count - 1);
                                var sourceRect = _spriteManager.PlayerPortraitSourceRects[portraitIndex];

                                // Portrait Position (Left side of the 90px area)
                                int portraitX = listX;
                                int portraitY = currentY;

                                float animSpeed = 1f;
                                int frame = (int)(gameTime.TotalGameTime.TotalSeconds * animSpeed) % 2;
                                Texture2D textureToDraw = frame == 0 ? _spriteManager.PlayerPortraitsSpriteSheet : _spriteManager.PlayerPortraitsAltSpriteSheet;
                                Texture2D silhouetteToDraw = frame == 0 ? _spriteManager.PlayerPortraitsSpriteSheetSilhouette : _spriteManager.PlayerPortraitsAltSpriteSheetSilhouette;

                                var destRect = new Rectangle(portraitX, portraitY, portraitSize, portraitSize);

                                // Outline
                                if (silhouetteToDraw != null)
                                {
                                    Color mainOutlineColor = _global.Palette_DarkGray;
                                    Color cornerOutlineColor = _global.Palette_DarkerGray;
                                    spriteBatch.DrawSnapped(silhouetteToDraw, new Rectangle(destRect.X - 1, destRect.Y, destRect.Width, destRect.Height), sourceRect, mainOutlineColor);
                                    spriteBatch.DrawSnapped(silhouetteToDraw, new Rectangle(destRect.X + 1, destRect.Y, destRect.Width, destRect.Height), sourceRect, mainOutlineColor);
                                    spriteBatch.DrawSnapped(silhouetteToDraw, new Rectangle(destRect.X, destRect.Y - 1, destRect.Width, destRect.Height), sourceRect, mainOutlineColor);
                                    spriteBatch.DrawSnapped(silhouetteToDraw, new Rectangle(destRect.X, destRect.Y + 1, destRect.Width, destRect.Height), sourceRect, mainOutlineColor);
                                }

                                spriteBatch.DrawSnapped(textureToDraw, destRect, sourceRect, Color.White);

                                // Health Bar & Text (To the right of portrait)
                                if (_spriteManager.InventoryPlayerHealthBarEmpty != null && _spriteManager.InventoryPlayerHealthBarFull != null)
                                {
                                    int barX = portraitX + portraitSize + 4;
                                    int barY = portraitY + (portraitSize - _spriteManager.InventoryPlayerHealthBarEmpty.Height) / 2;

                                    spriteBatch.DrawSnapped(_spriteManager.InventoryPlayerHealthBarEmpty, new Vector2(barX, barY), Color.White);

                                    int currentHP = member.CurrentHP;
                                    int maxHP = member.MaxHP;
                                    float hpPercent = (float)currentHP / Math.Max(1, maxHP);
                                    int fullWidth = _spriteManager.InventoryPlayerHealthBarFull.Width;
                                    int visibleWidth = (int)(fullWidth * hpPercent);

                                    var srcRect = new Rectangle(0, 0, visibleWidth, _spriteManager.InventoryPlayerHealthBarFull.Height);
                                    spriteBatch.DrawSnapped(_spriteManager.InventoryPlayerHealthBarFull, new Vector2(barX + 1, barY), srcRect, Color.White);

                                    // Name
                                    string playerName = member.Name.ToUpper();
                                    Vector2 nameSize = secondaryFont.MeasureString(playerName);
                                    Vector2 namePos = new Vector2(barX, barY - nameSize.Y - 2);
                                    spriteBatch.DrawStringSnapped(secondaryFont, playerName, namePos, _global.Palette_BrightWhite);

                                    // HP Text
                                    string hpText = $"{currentHP}/{maxHP} HP";
                                    Vector2 hpTextPos = new Vector2(barX - 2, barY + _spriteManager.InventoryPlayerHealthBarEmpty.Height);
                                    spriteBatch.DrawStringSnapped(secondaryFont, hpText, hpTextPos, _global.Palette_DarkGray);
                                }
                            }
                        }
                        else
                        {
                            // --- DRAW BUTTON ---
                            var btn = _partySlotButtons[i];

                            // Update bounds to current position
                            // Width is now 100
                            btn.Bounds = new Rectangle(listX, currentY, 100, buttonHeight);

                            // Only draw if enabled (slot is not empty)
                            if (btn.IsEnabled)
                            {
                                btn.Draw(spriteBatch, font, gameTime, Matrix.Identity);
                            }
                            else
                            {
                                btn.Draw(spriteBatch, font, gameTime, Matrix.Identity);
                            }

                            // Draw 8x8 sprite for unselected members
                            if (i < _gameState.PlayerState.Party.Count)
                            {
                                var member = _gameState.PlayerState.Party[i];

                                // Draw 8x8 Portrait
                                if (_spriteManager.PlayerPortraitsSmallSpriteSheet != null && _spriteManager.PlayerPortraitSmallSourceRects.Count > 0)
                                {
                                    int portraitIndex = Math.Clamp(member.PortraitIndex, 0, _spriteManager.PlayerPortraitSmallSourceRects.Count - 1);
                                    var sourceRect = _spriteManager.PlayerPortraitSmallSourceRects[portraitIndex];
                                    // Center vertically in button (Height 8)
                                    var pos = new Vector2(btn.Bounds.X + 1, btn.Bounds.Y);
                                    spriteBatch.DrawSnapped(_spriteManager.PlayerPortraitsSmallSpriteSheet, pos, sourceRect, Color.White);
                                }

                                // Draw Health Bar (40px wide, 2px high)
                                var pixel = ServiceLocator.Get<Texture2D>();
                                const int maxBarWidth = 40;
                                int currentHP = member.CurrentHP;
                                int maxHP = member.MaxHP;

                                float hpPercent = (float)currentHP / Math.Max(1, maxHP);
                                int currentBarWidth = (int)(maxBarWidth * hpPercent);

                                // Constraint: Min 1 pixel if alive
                                if (currentHP > 0 && currentBarWidth < 1) currentBarWidth = 1;
                                if (currentHP <= 0) currentBarWidth = 0;

                                // Position (Right aligned inside the 100px button)
                                int barX = btn.Bounds.Right - maxBarWidth - 4;
                                int barY = btn.Bounds.Y + 3; // Centered in 8px height (3 down)

                                // Draw Background
                                var bgRect = new Rectangle(barX, barY, maxBarWidth, 2);
                                spriteBatch.DrawSnapped(pixel, bgRect, _global.Palette_DarkGray);

                                // Draw Top Row
                                var topRect = new Rectangle(barX, barY, currentBarWidth, 1);
                                spriteBatch.DrawSnapped(pixel, topRect, _global.Palette_LightGreen);

                                // Draw Bottom Row
                                var botRect = new Rectangle(barX, barY + 1, currentBarWidth, 1);
                                spriteBatch.DrawSnapped(pixel, botRect, _global.Palette_DarkGreen);
                            }
                        }
                    }
                }
            }

            // --- Draw Stats ---
            if (!(_hoveredItemData is MoveData))
            {
                var member = _gameState.PlayerState.Party[_currentPartyMemberIndex];

                var stats = new List<(string Label, string StatKey)>
            {
                ("MAX HP", "MaxHP"),
                ("STRNTH", "Strength"),
                ("INTELL", "Intelligence"),
                ("TENACT", "Tenacity"),
                ("AGILTY", "Agility")
            };

                int startX = _statsPanelArea.X + 3;
                int startY = _statsPanelArea.Y + 77;
                int rowSpacing = 10;

                int val1RightX = 63;
                int arrowX = 66;
                int val2RightX = 107;

                // Helper to get modifier from currently equipped items on THIS member
                int GetEquippedModifier(int slotIndex, string statKey)
                {
                    if (slotIndex >= member.EquippedRelics.Length) return 0;
                    string? relicId = member.EquippedRelics[slotIndex];
                    if (string.IsNullOrEmpty(relicId)) return 0;
                    if (BattleDataCache.Relics.TryGetValue(relicId, out var relic)) return relic.StatModifiers.GetValueOrDefault(statKey, 0);
                    return 0;
                }

                int GetEquippedWeaponModifier(string statKey)
                {
                    if (string.IsNullOrEmpty(member.EquippedWeaponId)) return 0;
                    if (BattleDataCache.Weapons.TryGetValue(member.EquippedWeaponId, out var weapon)) return weapon.StatModifiers.GetValueOrDefault(statKey, 0);
                    return 0;
                }

                int GetEquippedArmorModifier(string statKey)
                {
                    if (string.IsNullOrEmpty(member.EquippedArmorId)) return 0;
                    if (BattleDataCache.Armors.TryGetValue(member.EquippedArmorId, out var armor)) return armor.StatModifiers.GetValueOrDefault(statKey, 0);
                    return 0;
                }

                int GetHoveredModifier(string statKey)
                {
                    if (_hoveredItemData == null) return 0;
                    if (_hoveredItemData is RelicData relic) return relic.StatModifiers.GetValueOrDefault(statKey, 0);
                    if (_hoveredItemData is WeaponData weapon) return weapon.StatModifiers.GetValueOrDefault(statKey, 0);
                    if (_hoveredItemData is ArmorData armor) return armor.StatModifiers.GetValueOrDefault(statKey, 0);
                    return 0;
                }

                for (int i = 0; i < stats.Count; i++)
                {
                    var stat = stats[i];
                    int y = startY + (i * rowSpacing);

                    // Use PlayerState helper to get base stat for specific member
                    int baseStat = _gameState.PlayerState.GetBaseStat(member, stat.StatKey);

                    int totalCurrentMod = 0;
                    for (int slot = 0; slot < member.EquippedRelics.Length; slot++) totalCurrentMod += GetEquippedModifier(slot, stat.StatKey);
                    totalCurrentMod += GetEquippedWeaponModifier(stat.StatKey);
                    totalCurrentMod += GetEquippedArmorModifier(stat.StatKey);

                    int currentVal = Math.Max(1, baseStat + totalCurrentMod);
                    int projectedVal = currentVal;
                    int diff = 0;
                    bool isComparing = _isEquipSubmenuOpen && _hoveredItemData != null;

                    if (isComparing)
                    {
                        int currentSlotMod = 0;
                        if (_activeEquipSlotType == EquipSlotType.Weapon) currentSlotMod = GetEquippedWeaponModifier(stat.StatKey);
                        else if (_activeEquipSlotType == EquipSlotType.Armor) currentSlotMod = GetEquippedArmorModifier(stat.StatKey);
                        else if (_activeEquipSlotType == EquipSlotType.Relic1) currentSlotMod = GetEquippedModifier(0, stat.StatKey);
                        else if (_activeEquipSlotType == EquipSlotType.Relic2) currentSlotMod = GetEquippedModifier(1, stat.StatKey);
                        else if (_activeEquipSlotType == EquipSlotType.Relic3) currentSlotMod = GetEquippedModifier(2, stat.StatKey);

                        int newMod = GetHoveredModifier(stat.StatKey);
                        int projectedRaw = baseStat + totalCurrentMod - currentSlotMod + newMod;
                        projectedVal = Math.Max(1, projectedRaw);
                        diff = projectedVal - currentVal;
                    }

                    string leftText;
                    Color leftColor;
                    Color labelColor;

                    if (isComparing && diff != 0)
                    {
                        labelColor = _global.Palette_BrightWhite;
                        float cyclePos = _statCycleTimer % (STAT_CYCLE_INTERVAL * 2);
                        bool showModifier = cyclePos < STAT_CYCLE_INTERVAL;

                        if (showModifier)
                        {
                            leftText = (diff > 0 ? "+" : "") + diff.ToString();
                            leftColor = diff > 0 ? _global.Palette_LightGreen : _global.Palette_Red;
                        }
                        else
                        {
                            leftText = currentVal.ToString();
                            leftColor = _global.Palette_BrightWhite;
                        }
                    }
                    else
                    {
                        labelColor = _global.Palette_Gray;
                        leftText = currentVal.ToString();
                        leftColor = _global.Palette_White;
                    }

                    spriteBatch.DrawStringSnapped(secondaryFont, stat.Label, new Vector2(startX, y + 4), labelColor);
                    Vector2 leftSize = font.MeasureString(leftText);
                    spriteBatch.DrawStringSnapped(font, leftText, new Vector2(startX + val1RightX - leftSize.X, y + 4), leftColor);

                    Color arrowColor = (isComparing && diff != 0) ? _global.Palette_BrightWhite : _global.Palette_DarkerGray;
                    Color projColor = (isComparing && diff != 0) ? (diff > 0 ? _global.Palette_LightGreen : _global.Palette_Red) : _global.Palette_DarkerGray;
                    string projStr = projectedVal.ToString();

                    spriteBatch.DrawStringSnapped(secondaryFont, ">", new Vector2(startX + arrowX, y + 4), arrowColor);
                    Vector2 projSize = font.MeasureString(projStr);
                    spriteBatch.DrawStringSnapped(font, projStr, new Vector2(startX + val2RightX - projSize.X, y + 4), projColor);
                }
            }
        }

        private List<List<ColoredText>> ParseAndWrapRichText(BitmapFont font, string text, float maxWidth, Color defaultColor)
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
                    float partWidth = isWhitespace ? (part.Length * SPACE_WIDTH) : font.MeasureString(part).Width;

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

                    currentLine.Add(new ColoredText(part, currentColor));
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
                if (effectName == "freeze") return _global.StatusEffectColors.GetValueOrDefault(StatusEffectType.Freeze, Color.White);
                if (effectName == "blind") return _global.StatusEffectColors.GetValueOrDefault(StatusEffectType.Blind, Color.White);
                if (effectName == "confuse") return _global.StatusEffectColors.GetValueOrDefault(StatusEffectType.Confuse, Color.White);
                if (effectName == "silence") return _global.StatusEffectColors.GetValueOrDefault(StatusEffectType.Silence, Color.White);
                if (effectName == "fear") return _global.StatusEffectColors.GetValueOrDefault(StatusEffectType.Fear, Color.White);
                if (effectName == "root") return _global.StatusEffectColors.GetValueOrDefault(StatusEffectType.Root, Color.White);
            }

            switch (tag)
            {
                case "teal": return _global.Palette_Teal;
                case "red": return _global.Palette_Red;
                case "blue": return _global.Palette_LightBlue;
                case "green": return _global.Palette_LightGreen;
                case "yellow": return _global.Palette_Yellow;
                case "orange": return _global.Palette_Orange;
                case "purple": return _global.Palette_LightPurple;
                case "pink": return _global.Palette_Pink;
                case "gray": return _global.Palette_Gray;
                case "white": return _global.Palette_White;
                case "brightwhite": return _global.Palette_BrightWhite;
                case "darkgray": return _global.Palette_DarkGray;
                default: return _global.Palette_White;
            }
        }
    }
}
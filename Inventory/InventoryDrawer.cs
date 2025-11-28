#nullable enable
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
        public void DrawWorld(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            if (!IsOpen) return;

            var inventoryPosition = new Vector2(0, 200);
            var headerPosition = inventoryPosition + _inventoryPositionOffset;

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
                    InventoryCategory.Equip => _spriteManager.InventoryBorderEquip,
                    _ => _spriteManager.InventoryBorderWeapons,
                };
            }
            spriteBatch.DrawSnapped(selectedBorderSprite, inventoryPosition, Color.White);

            foreach (var button in _inventoryHeaderButtons) button.Draw(spriteBatch, font, gameTime, Matrix.Identity);

            // --- NEW: Draw Category Title ---
            if (_selectedInventoryCategory == InventoryCategory.Equip)
            {
                // 1. Draw "STATS" on the right panel
                if (!_isEquipSubmenuOpen) // <--- Added check to hide STATS when submenu is open
                {
                    string statsTitle = "STATS";
                    Vector2 statsTitleSize = font.MeasureString(statsTitle);
                    Vector2 statsTitlePos = new Vector2(
                        _infoPanelArea.X + (_infoPanelArea.Width - statsTitleSize.X) / 2f,
                        _infoPanelArea.Y + 5 + _inventoryPositionOffset.Y
                    );
                    spriteBatch.DrawStringSnapped(font, statsTitle, statsTitlePos, _global.Palette_DarkGray);
                }

                // 2. Draw "EQUIP" on the left panel (centered over equip buttons)
                if (_weaponEquipButton != null)
                {
                    string equipTitle = "EQUIP";
                    Vector2 equipTitleSize = font.MeasureString(equipTitle);
                    // Center over the weapon button's width (180px)
                    float equipCenterX = _weaponEquipButton.Bounds.X + (_weaponEquipButton.Bounds.Width / 2f);
                    // Align vertically with where "STATS" would be (or is)
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
                // Standard behavior for other categories
                string categoryTitle = _selectedInventoryCategory.ToString().ToUpper();
                Vector2 titleSize = font.MeasureString(categoryTitle);
                Vector2 titlePos = new Vector2(
                    _infoPanelArea.X + (_infoPanelArea.Width - titleSize.X) / 2f,
                    _infoPanelArea.Y + 5 + _inventoryPositionOffset.Y
                );
                spriteBatch.DrawStringSnapped(font, categoryTitle, titlePos, _global.Palette_DarkGray);
            }
            // --------------------------------

            if (_selectedInventoryCategory != InventoryCategory.Equip)
            {
                foreach (var slot in _inventorySlots) slot.Draw(spriteBatch, font, gameTime, Matrix.Identity);

                // Draw Page Counter
                if (_totalPages > 1)
                {
                    var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
                    string pageText = $"{_currentPage + 1}/{_totalPages}";
                    var textSize = secondaryFont.MeasureString(pageText);
                    var textPos = new Vector2(
                        _inventorySlotArea.Center.X - textSize.Width / 2f,
                        _inventorySlotArea.Bottom - 2
                    );

                    // Draw background rectangle
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

                // Draw Info Panel
                DrawInfoPanel(spriteBatch, font, ServiceLocator.Get<Core>().SecondaryFont);
            }
            else
            {
                if (_isEquipSubmenuOpen)
                {
                    foreach (var button in _equipSubmenuButtons)
                    {
                        button.Draw(spriteBatch, font, gameTime, Matrix.Identity);
                    }

                    // Draw Scroll Arrows
                    var arrowTexture = _spriteManager.InventoryScrollArrowsSprite;
                    var arrowRects = _spriteManager.InventoryScrollArrowRects;

                    if (arrowTexture != null && arrowRects != null && _equipSubmenuButtons.Count > 0)
                    {
                        int totalItems = 1;
                        if (_activeEquipSlotType == EquipSlotType.Weapon) totalItems += _gameState.PlayerState.Weapons.Count;
                        else if (_activeEquipSlotType == EquipSlotType.Armor) totalItems += _gameState.PlayerState.Armors.Count;
                        else if (_activeEquipSlotType == EquipSlotType.Relic1 || _activeEquipSlotType == EquipSlotType.Relic2 || _activeEquipSlotType == EquipSlotType.Relic3) totalItems += _gameState.PlayerState.Relics.Count;

                        int maxScroll = Math.Max(0, totalItems - 7);

                        // Up Arrow
                        if (_equipMenuScrollIndex > 0)
                        {
                            var firstButton = _equipSubmenuButtons[0];
                            var arrowPos = new Vector2(
                                firstButton.Bounds.Center.X - arrowRects[0].Width / 2f,
                                firstButton.Bounds.Top - arrowRects[0].Height
                            );
                            spriteBatch.DrawSnapped(arrowTexture, arrowPos, arrowRects[0], Color.White);
                        }

                        // Down Arrow
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
                }

                // Draw Stats Panel (Moved here to show in both main equip view and submenu)
                DrawStatsPanel(spriteBatch, font, ServiceLocator.Get<Core>().SecondaryFont);
            }

            if (_debugButton1 != null && _debugButton1.IsEnabled) _debugButton1.Draw(spriteBatch, font, gameTime, Matrix.Identity);
            if (_debugButton2 != null && _debugButton2.IsEnabled) _debugButton2.Draw(spriteBatch, font, gameTime, Matrix.Identity);

            if (_global.ShowSplitMapGrid)
            {
                var pixel = ServiceLocator.Get<Texture2D>();
                spriteBatch.DrawSnapped(pixel, _inventorySlotArea, Color.Blue * 0.5f);

                // Debug Draw for Panels
                if (_selectedInventoryCategory == InventoryCategory.Equip)
                {
                    spriteBatch.DrawSnapped(pixel, _statsPanelArea, Color.HotPink * 0.5f);
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
            // Only draw the toggle button here
            _inventoryButton?.Draw(spriteBatch, font, gameTime, transform);
        }

        private void DrawInfoPanel(SpriteBatch spriteBatch, BitmapFont font, BitmapFont secondaryFont)
        {
            // Find the active slot (Selected takes precedence over Hovered)
            InventorySlot? activeSlot = _inventorySlots.FirstOrDefault(s => s.IsSelected);
            if (activeSlot == null)
            {
                activeSlot = _inventorySlots.FirstOrDefault(s => s.IsHovered);
            }

            if (activeSlot == null || !activeSlot.HasItem || string.IsNullOrEmpty(activeSlot.ItemId)) return;

            // Retrieve Item Data based on Category
            string name = activeSlot.ItemId.ToUpper();
            string description = "";
            string iconPath = activeSlot.IconPath ?? "";
            Texture2D? iconTexture = null;
            Texture2D? iconSilhouette = null;
            (List<string> Positives, List<string> Negatives) statLines = (new List<string>(), new List<string>());

            // Attempt to fetch rich data
            if (_selectedInventoryCategory == InventoryCategory.Relics)
            {
                // Try to find by name if ID lookup fails (since slot stores Name)
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
            else if (_selectedInventoryCategory == InventoryCategory.Spells)
            {
                var move = BattleDataCache.Moves.Values.FirstOrDefault(m => m.MoveName.Equals(activeSlot.ItemId, StringComparison.OrdinalIgnoreCase));
                if (move != null)
                {
                    name = move.MoveName.ToUpper();
                    description = move.Description.ToUpper();
                    iconPath = $"Sprites/Items/Spells/{move.MoveID}";
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

            // Load Icon
            if (!string.IsNullOrEmpty(iconPath))
            {
                iconTexture = _spriteManager.GetItemSprite(iconPath);
                iconSilhouette = _spriteManager.GetItemSpriteSilhouette(iconPath);
            }

            // --- Drawing Logic ---
            const int spriteSize = 32;
            const int gap = 4;

            // 1. Calculate Total Height for Centering
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

            // 2. Calculate Start Y
            float currentY = _infoPanelArea.Y + (_infoPanelArea.Height - totalContentHeight) / 2f;
            currentY -= 10f; // Move up 10 pixels as requested

            // 3. Draw Sprite
            int spriteX = _infoPanelArea.X + (_infoPanelArea.Width - spriteSize) / 2;

            if (iconSilhouette != null)
            {
                // Use global outline color for Idle state
                Color mainOutlineColor = _global.ItemOutlineColor_Idle;
                Color cornerOutlineColor = _global.ItemOutlineColor_Idle_Corner;

                // 1. Draw Diagonals (Corners) FIRST (Behind)
                spriteBatch.DrawSnapped(iconSilhouette, new Vector2(spriteX - 1, currentY - 1), cornerOutlineColor);
                spriteBatch.DrawSnapped(iconSilhouette, new Vector2(spriteX + 1, currentY - 1), cornerOutlineColor);
                spriteBatch.DrawSnapped(iconSilhouette, new Vector2(spriteX - 1, currentY + 1), cornerOutlineColor);
                spriteBatch.DrawSnapped(iconSilhouette, new Vector2(spriteX + 1, currentY + 1), cornerOutlineColor);

                // 2. Draw Cardinals (Main) SECOND (On Top)
                spriteBatch.DrawSnapped(iconSilhouette, new Vector2(spriteX - 1, currentY), mainOutlineColor);
                spriteBatch.DrawSnapped(iconSilhouette, new Vector2(spriteX + 1, currentY), mainOutlineColor);
                spriteBatch.DrawSnapped(iconSilhouette, new Vector2(spriteX, currentY - 1), mainOutlineColor);
                spriteBatch.DrawSnapped(iconSilhouette, new Vector2(spriteX, currentY + 1), mainOutlineColor);
            }

            if (iconTexture != null)
            {
                Color tint = activeSlot.IconTint ?? Color.White;
                spriteBatch.DrawSnapped(iconTexture, new Vector2(spriteX, currentY), tint);
            }

            currentY += spriteSize + gap;

            // 4. Draw Title
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

            // 5. Draw Description
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

            // 6. Draw Stats (Two Columns)
            if (statLines.Positives.Any() || statLines.Negatives.Any())
            {
                currentY += gap;
                float leftColX = _infoPanelArea.X + 8;
                float rightColX = _infoPanelArea.X + 64; // Roughly half way plus a bit

                int maxRows = Math.Max(statLines.Positives.Count, statLines.Negatives.Count);

                for (int i = 0; i < maxRows; i++)
                {
                    // Draw Positive Stat (Left Column)
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

                    // Draw Negative Stat (Right Column)
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

        private void DrawStatsPanel(SpriteBatch spriteBatch, BitmapFont font, BitmapFont secondaryFont)
        {
            // --- Draw Hovered Item Details ---
            if (_hoveredItemData != null)
            {
                const int padding = 2; // Reduced padding for "tightly against top"
                const int spriteSize = 32;
                const int gap = 4;

                // 1. Sprite (Top Left - Centered Horizontally)
                int spriteX = _statsPanelArea.X + (_statsPanelArea.Width - spriteSize) / 2;
                int spriteY = _statsPanelArea.Y; // Tightly against top

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
                    // Use global outline color for Idle state
                    Color mainOutlineColor = _global.ItemOutlineColor_Idle;
                    Color cornerOutlineColor = _global.ItemOutlineColor_Idle_Corner;

                    // 1. Draw Diagonals (Corners) FIRST (Behind)
                    spriteBatch.DrawSnapped(itemSilhouette, new Vector2(spriteX - 1, spriteY - 1), cornerOutlineColor);
                    spriteBatch.DrawSnapped(itemSilhouette, new Vector2(spriteX + 1, spriteY - 1), cornerOutlineColor);
                    spriteBatch.DrawSnapped(itemSilhouette, new Vector2(spriteX - 1, spriteY + 1), cornerOutlineColor);
                    spriteBatch.DrawSnapped(itemSilhouette, new Vector2(spriteX + 1, spriteY + 1), cornerOutlineColor);

                    // 2. Draw Cardinals (Main) SECOND (On Top)
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
                // Anchor: Bottom of the title block is fixed relative to the sprite bottom.
                float titleBottomY = spriteY + spriteSize + gap;

                int maxTitleWidth = _statsPanelArea.Width - (4 * 2); // 4px padding on sides

                var titleLines = ParseAndWrapRichText(font, name, maxTitleWidth, _global.Palette_BrightWhite);
                float totalTitleHeight = titleLines.Count * font.LineHeight;

                // Calculate start Y so the block ends at titleBottomY
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

                    // Center horizontally within the panel
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
                            spriteBatch.DrawStringSnapped(font, segment.Text, new Vector2(currentX, currentTitleY), segment.Color);
                        }
                        currentX += segWidth;
                    }
                    currentTitleY += font.LineHeight;
                }

                // 3. Description
                // Area: From titleBottomY to statsStartY
                int statsStartY = _statsPanelArea.Y + 77; // Defined later for stats, used here for boundary
                float descAreaTop = titleBottomY;
                float descAreaBottom = statsStartY;
                float descAreaHeight = descAreaBottom - descAreaTop;

                float descWidth = _statsPanelArea.Width - (4 * 2); // 4px padding
                var descLines = ParseAndWrapRichText(secondaryFont, description, descWidth, _global.Palette_White);
                float totalDescHeight = descLines.Count * secondaryFont.LineHeight;

                // Center vertically in the area
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

            // --- Draw Stats ---
            var playerState = _gameState.PlayerState;
            if (playerState == null) return;

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

            // Helper to get modifier from an equipped slot
            int GetEquippedModifier(int slotIndex, string statKey)
            {
                if (slotIndex >= playerState.EquippedRelics.Length) return 0;
                string? relicId = playerState.EquippedRelics[slotIndex];
                if (string.IsNullOrEmpty(relicId)) return 0;

                if (BattleDataCache.Relics.TryGetValue(relicId, out var relic))
                {
                    return relic.StatModifiers.GetValueOrDefault(statKey, 0);
                }
                return 0;
            }

            int GetEquippedWeaponModifier(string statKey)
            {
                if (string.IsNullOrEmpty(playerState.EquippedWeaponId)) return 0;
                if (BattleDataCache.Weapons.TryGetValue(playerState.EquippedWeaponId, out var weapon))
                {
                    return weapon.StatModifiers.GetValueOrDefault(statKey, 0);
                }
                return 0;
            }

            int GetEquippedArmorModifier(string statKey)
            {
                if (string.IsNullOrEmpty(playerState.EquippedArmorId)) return 0;
                if (BattleDataCache.Armors.TryGetValue(playerState.EquippedArmorId, out var armor))
                {
                    return armor.StatModifiers.GetValueOrDefault(statKey, 0);
                }
                return 0;
            }

            // Helper to get modifier from the hovered item data
            int GetHoveredModifier(string statKey)
            {
                if (_hoveredItemData == null) return 0;

                if (_hoveredItemData is RelicData relic)
                    return relic.StatModifiers.GetValueOrDefault(statKey, 0);
                if (_hoveredItemData is WeaponData weapon)
                    return weapon.StatModifiers.GetValueOrDefault(statKey, 0);
                if (_hoveredItemData is ArmorData armor)
                    return armor.StatModifiers.GetValueOrDefault(statKey, 0);

                return 0;
            }

            for (int i = 0; i < stats.Count; i++)
            {
                var stat = stats[i];
                int y = startY + (i * rowSpacing);

                // 1. Calculate Values
                // Get the base stat directly to perform accurate math
                int baseStat = playerState.GetBaseStat(stat.StatKey);

                // Calculate total current modifier from all equipped items (Relics + Weapon + Armor)
                int totalCurrentMod = 0;
                for (int slot = 0; slot < playerState.EquippedRelics.Length; slot++)
                {
                    totalCurrentMod += GetEquippedModifier(slot, stat.StatKey);
                }
                totalCurrentMod += GetEquippedWeaponModifier(stat.StatKey);
                totalCurrentMod += GetEquippedArmorModifier(stat.StatKey);

                // Current Effective Value (Clamped)
                int currentVal = Math.Max(1, baseStat + totalCurrentMod);

                // Projected Calculation
                int projectedVal = currentVal;
                int diff = 0;
                bool isComparing = _isEquipSubmenuOpen && _hoveredItemData != null;

                if (isComparing)
                {
                    int currentSlotMod = 0;

                    if (_activeEquipSlotType == EquipSlotType.Weapon)
                    {
                        currentSlotMod = GetEquippedWeaponModifier(stat.StatKey);
                    }
                    else if (_activeEquipSlotType == EquipSlotType.Armor)
                    {
                        currentSlotMod = GetEquippedArmorModifier(stat.StatKey);
                    }
                    else if (_activeEquipSlotType == EquipSlotType.Relic1)
                    {
                        currentSlotMod = GetEquippedModifier(0, stat.StatKey);
                    }
                    else if (_activeEquipSlotType == EquipSlotType.Relic2)
                    {
                        currentSlotMod = GetEquippedModifier(1, stat.StatKey);
                    }
                    else if (_activeEquipSlotType == EquipSlotType.Relic3)
                    {
                        currentSlotMod = GetEquippedModifier(2, stat.StatKey);
                    }

                    int newMod = GetHoveredModifier(stat.StatKey);

                    // Calculate projected raw value
                    int projectedRaw = baseStat + totalCurrentMod - currentSlotMod + newMod;

                    // Clamp projected value
                    projectedVal = Math.Max(1, projectedRaw);

                    // Calculate difference based on clamped values
                    diff = projectedVal - currentVal;
                }

                // 2. Determine Left Text (Modifier or Current)
                string leftText;
                Color leftColor;
                Color labelColor;

                if (isComparing && diff != 0)
                {
                    // Stat is changing: Highlight label
                    labelColor = _global.Palette_BrightWhite;

                    // Cycle logic
                    float cyclePos = _statCycleTimer % (STAT_CYCLE_INTERVAL * 2);
                    // Inverted logic: Start with Modifier (True), then Base (False)
                    bool showModifier = cyclePos < STAT_CYCLE_INTERVAL;

                    if (showModifier)
                    {
                        // Show Modifier (+5)
                        leftText = (diff > 0 ? "+" : "") + diff.ToString();
                        leftColor = diff > 0 ? _global.Palette_LightGreen : _global.Palette_Red;
                    }
                    else
                    {
                        // Show Current Value (90)
                        leftText = currentVal.ToString();
                        leftColor = _global.Palette_BrightWhite;
                    }
                }
                else
                {
                    // Stat is NOT changing or not comparing: Default label, show current value
                    labelColor = _global.Palette_Gray;
                    leftText = currentVal.ToString();
                    leftColor = _global.Palette_LightGray;
                }

                // 3. Draw Label
                spriteBatch.DrawStringSnapped(secondaryFont, stat.Label, new Vector2(startX, y + 4), labelColor);

                // 4. Draw Left Text (Current Value OR Modifier)
                Vector2 leftSize = font.MeasureString(leftText);
                spriteBatch.DrawStringSnapped(font, leftText, new Vector2(startX + val1RightX - leftSize.X, y + 4), leftColor);

                // 5. Draw Right Side (Only if comparing)
                if (isComparing)
                {
                    // Arrow
                    Color arrowColor = (diff != 0) ? _global.Palette_BrightWhite : _global.Palette_Gray;
                    spriteBatch.DrawStringSnapped(secondaryFont, ">", new Vector2(startX + arrowX, y + 4), arrowColor);

                    // Projected Value
                    string projStr = projectedVal.ToString();
                    Vector2 projSize = font.MeasureString(projStr);
                    Color projColor = _global.Palette_LightGray;
                    if (diff > 0) projColor = _global.Palette_LightGreen;
                    else if (diff < 0) projColor = _global.Palette_Red;

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

            // Split by tags OR whitespace (capturing both)
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
                    // Force a new line
                    lines.Add(currentLine);
                    currentLine = new List<ColoredText>();
                    currentLineWidth = 0f;
                }
                else
                {
                    // It's a word or spaces (but not newlines)
                    bool isWhitespace = string.IsNullOrWhiteSpace(part);
                    float partWidth = isWhitespace ? (part.Length * SPACE_WIDTH) : font.MeasureString(part).Width;

                    // If it's a word and it doesn't fit, wrap.
                    // We don't wrap on whitespace; we let it trail off the edge.
                    if (!isWhitespace && currentLineWidth + partWidth > maxWidth && currentLineWidth > 0)
                    {
                        lines.Add(currentLine);
                        currentLine = new List<ColoredText>();
                        currentLineWidth = 0f;
                    }

                    // Optimization: Don't add leading whitespace to a new line
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

            // 1. Stats
            if (tag == "cstr") return _global.StatColor_Strength;
            if (tag == "cint") return _global.StatColor_Intelligence;
            if (tag == "cten") return _global.StatColor_Tenacity;
            if (tag == "cagi") return _global.StatColor_Agility;

            // 2. General
            if (tag == "cpositive") return _global.ColorPositive;
            if (tag == "cnegative") return _global.ColorNegative;
            if (tag == "ccrit") return _global.ColorCrit;
            if (tag == "cimmune") return _global.ColorImmune;
            if (tag == "cctm") return _global.ColorConditionToMeet;
            if (tag == "cetc") return _global.Palette_DarkGray;

            // 3. Elements
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

            // 4. Status Effects
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

            // 5. Standard Palette Colors
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

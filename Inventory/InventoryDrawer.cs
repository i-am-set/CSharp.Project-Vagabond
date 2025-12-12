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
        // Fields for portrait background animation
        private int _portraitBgFrameIndex = 0; // Changed from Rectangle to int index
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
                const int statsPanelWidth = 116;
                const int statsPanelHeight = 132;
                int statsPanelX = _inventorySlotArea.Right + 4;
                int statsPanelY = _inventorySlotArea.Y - 1;
                var infoPanelArea = new Rectangle(statsPanelX, statsPanelY, statsPanelWidth, statsPanelHeight);

                DrawInfoPanel(spriteBatch, font, ServiceLocator.Get<Core>().SecondaryFont, gameTime, true, infoPanelArea);
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
            if (_selectedInventoryCategory != InventoryCategory.Equip)
            {
                const int statsPanelWidth = 116;
                const int statsPanelHeight = 132;
                int statsPanelX = _inventorySlotArea.Right + 4;
                int statsPanelY = _inventorySlotArea.Y - 1;
                var infoPanelArea = new Rectangle(statsPanelX, statsPanelY, statsPanelWidth, statsPanelHeight);

                string categoryTitle = _selectedInventoryCategory.ToString().ToUpper();
                Vector2 titleSize = font.MeasureString(categoryTitle);
                Vector2 titlePos = new Vector2(
                    infoPanelArea.X + (infoPanelArea.Width - titleSize.X) / 2f,
                    infoPanelArea.Y + 5 + _inventoryPositionOffset.Y
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
                const int statsPanelWidth = 116;
                const int statsPanelHeight = 132;
                int statsPanelX = _inventorySlotArea.Right + 4;
                int statsPanelY = _inventorySlotArea.Y - 1;
                var infoPanelArea = new Rectangle(statsPanelX, statsPanelY, statsPanelWidth, statsPanelHeight);

                DrawInfoPanel(spriteBatch, font, ServiceLocator.Get<Core>().SecondaryFont, gameTime, false, infoPanelArea);
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
                        var member = _gameState.PlayerState.Party[_currentPartyMemberIndex];
                        if (_activeEquipSlotType == EquipSlotType.Weapon) totalItems += _gameState.PlayerState.Weapons.Count;
                        else if (_activeEquipSlotType == EquipSlotType.Armor) totalItems += _gameState.PlayerState.Armors.Count;
                        else if (_activeEquipSlotType == EquipSlotType.Relic) totalItems += _gameState.PlayerState.Relics.Count;
                        else if (_activeEquipSlotType >= EquipSlotType.Spell1 && _activeEquipSlotType <= EquipSlotType.Spell4) totalItems += member.Spells.Count;

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
                    DrawPartyMemberSlots(spriteBatch, font, ServiceLocator.Get<Core>().SecondaryFont, gameTime);
                }
            }

            if (_debugButton1 != null && _debugButton1.IsEnabled) _debugButton1.Draw(spriteBatch, font, gameTime, Matrix.Identity);
            if (_debugButton2 != null && _debugButton2.IsEnabled) _debugButton2.Draw(spriteBatch, font, gameTime, Matrix.Identity);

            if (_global.ShowSplitMapGrid)
            {
                var pixel = ServiceLocator.Get<Texture2D>();

                // FIX: Only draw the inventory slot area (grid) and stats panel if NOT in Equip mode, 
                // OR if the submenu is open (since the list uses the slot area).
                if (_selectedInventoryCategory != InventoryCategory.Equip)
                {
                    spriteBatch.DrawSnapped(pixel, _inventorySlotArea, Color.Blue * 0.5f);

                    // Draw Stats Panel Debug
                    const int statsPanelWidth = 116;
                    const int statsPanelHeight = 132;
                    int statsPanelX = _inventorySlotArea.Right + 4;
                    int statsPanelY = _inventorySlotArea.Y - 1;
                    var infoPanelArea = new Rectangle(statsPanelX, statsPanelY, statsPanelWidth, statsPanelHeight);
                    spriteBatch.DrawSnapped(pixel, infoPanelArea, Color.Cyan * 0.5f);
                }
                else if (_isEquipSubmenuOpen)
                {
                    // In submenu, the list aligns with the slot area
                    spriteBatch.DrawSnapped(pixel, _inventorySlotArea, Color.Blue * 0.5f);
                }

                if (_selectedInventoryCategory == InventoryCategory.Equip)
                {
                    Color[] debugColors = { Color.Green, Color.Yellow, Color.Orange, Color.Red };
                    for (int i = 0; i < 4; i++)
                    {
                        spriteBatch.DrawSnapped(pixel, _partyMemberPanelAreas[i], debugColors[i] * 0.5f);
                    }

                    foreach (var btn in _partyEquipButtons)
                    {
                        spriteBatch.DrawSnapped(pixel, btn.Bounds, Color.Magenta * 0.5f);
                    }
                }
            }

            _inventoryEquipButton?.Draw(spriteBatch, font, gameTime, Matrix.Identity);
        }

        public void DrawScreen(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            _inventoryButton?.Draw(spriteBatch, font, gameTime, transform);
        }

        private void DrawPartyMemberSlots(SpriteBatch spriteBatch, BitmapFont font, BitmapFont secondaryFont, GameTime gameTime)
        {
            var pixel = ServiceLocator.Get<Texture2D>();

            // Update Background Animation State (Once per frame)
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _portraitBgTimer += dt;
            if (_portraitBgTimer >= _portraitBgDuration)
            {
                _portraitBgTimer = 0f;
                _portraitBgDuration = (float)(_rng.NextDouble() * (8.0 - 2.0) + 2.0);
                var frames = _spriteManager.InventorySlotLargeSourceRects;
                if (frames != null && frames.Length > 0) _portraitBgFrameIndex = _rng.Next(frames.Length);
            }

            // Determine base index for equip slot randomization
            int baseFrameIndex = _portraitBgFrameIndex;
            var slotFrames = _spriteManager.InventorySlotSourceRects;

            for (int i = 0; i < 4; i++)
            {
                var bounds = _partyMemberPanelAreas[i];
                // Check if occupied
                bool isOccupied = i < _gameState.PlayerState.Party.Count;
                var member = isOccupied ? _gameState.PlayerState.Party[i] : null;

                int centerX = bounds.Center.X;
                int currentY = bounds.Y + 4;

                // 1. Name
                string name = isOccupied ? member!.Name.ToUpper() : "EMPTY";
                Color nameColor = isOccupied ? _global.Palette_BrightWhite : _global.Palette_DarkGray;

                Vector2 nameSize = font.MeasureString(name);
                Vector2 namePos = new Vector2(centerX - nameSize.X / 2, currentY);
                currentY += (int)nameSize.Y - 2;

                // 2. Sprite
                // Draw Background Frame (Using Large Sprite Sheet)
                if (_spriteManager.InventorySlotLargeSourceRects != null && _spriteManager.InventorySlotLargeSourceRects.Length > 0)
                {
                    var largeFrame = _spriteManager.InventorySlotLargeSourceRects[_portraitBgFrameIndex];
                    Vector2 bgPos = new Vector2(centerX, currentY + 16);
                    Vector2 origin = new Vector2(largeFrame.Width / 2f, largeFrame.Height / 2f);
                    // Draw unscaled (1.0f)
                    spriteBatch.DrawSnapped(_spriteManager.InventorySlotIdleLargeSpriteSheet, bgPos, largeFrame, Color.White, 0f, origin, 1.0f, SpriteEffects.None, 0f);
                }

                // Draw Portrait (Only if occupied)
                if (isOccupied && _spriteManager.PlayerPortraitsSpriteSheet != null && _spriteManager.PlayerPortraitSourceRects.Count > 0)
                {
                    int portraitIndex = Math.Clamp(member!.PortraitIndex, 0, _spriteManager.PlayerPortraitSourceRects.Count - 1);
                    var sourceRect = _spriteManager.PlayerPortraitSourceRects[portraitIndex];

                    float animSpeed = 1f;
                    int frame = (int)(gameTime.TotalGameTime.TotalSeconds * animSpeed) % 2;
                    Texture2D textureToDraw = frame == 0 ? _spriteManager.PlayerPortraitsSpriteSheet : _spriteManager.PlayerPortraitsAltSpriteSheet;

                    var destRect = new Rectangle(centerX - 16, currentY, 32, 32);
                    spriteBatch.DrawSnapped(textureToDraw, destRect, sourceRect, Color.White);
                }

                // Draw Name
                spriteBatch.DrawStringSnapped(font, name, namePos, nameColor);

                currentY += 32 + 2 - 6; // Moved up 6 pixels

                // 3. Health Bar
                Texture2D healthBarBg = isOccupied ? _spriteManager.InventoryPlayerHealthBarEmpty : _spriteManager.InventoryPlayerHealthBarDisabled;
                if (healthBarBg != null)
                {
                    int barX = centerX - (healthBarBg.Width / 2);
                    spriteBatch.DrawSnapped(healthBarBg, new Vector2(barX, currentY), Color.White);

                    if (isOccupied && _spriteManager.InventoryPlayerHealthBarFull != null)
                    {
                        int currentHP = member!.CurrentHP;
                        int maxHP = member.MaxHP;
                        float hpPercent = (float)currentHP / Math.Max(1, maxHP);
                        int fullWidth = _spriteManager.InventoryPlayerHealthBarFull.Width;
                        int visibleWidth = (int)(fullWidth * hpPercent);

                        var srcRect = new Rectangle(0, 0, visibleWidth, _spriteManager.InventoryPlayerHealthBarFull.Height);
                        spriteBatch.DrawSnapped(_spriteManager.InventoryPlayerHealthBarFull, new Vector2(barX + 1, currentY), srcRect, Color.White);
                    }

                    string hpValText = isOccupied ? $"{member!.CurrentHP}/{member.MaxHP}" : "0/0";
                    Color hpValColor = isOccupied ? _global.Palette_BrightWhite : _global.Palette_DarkGray;
                    string hpSuffix = " HP";

                    Vector2 valSize = secondaryFont.MeasureString(hpValText);
                    Vector2 suffixSize = secondaryFont.MeasureString(hpSuffix);
                    float totalHpWidth = valSize.X + suffixSize.X;

                    float hpTextX = centerX - (totalHpWidth / 2f);
                    float hpTextY = currentY + 7;

                    spriteBatch.DrawStringSnapped(secondaryFont, hpValText, new Vector2(hpTextX, hpTextY), hpValColor);
                    spriteBatch.DrawStringSnapped(secondaryFont, hpSuffix, new Vector2(hpTextX + valSize.X, hpTextY), _global.Palette_Gray);

                    currentY += 8 + (int)valSize.Y + 4 - 3;
                }

                // 4. Equip Slots (Weapon, Armor, Relic)
                int slotSize = 16;
                int gap = 4;
                int totalEquipWidth = (slotSize * 3) + (gap * 2);
                int equipStartX = centerX - (totalEquipWidth / 2);

                // Determine random frames for slots based on the base frame index
                Rectangle weaponFrame = Rectangle.Empty;
                Rectangle armorFrame = Rectangle.Empty;
                Rectangle relicFrame = Rectangle.Empty;

                if (slotFrames != null && slotFrames.Length > 0)
                {
                    weaponFrame = slotFrames[(baseFrameIndex + 1) % slotFrames.Length];
                    armorFrame = slotFrames[(baseFrameIndex + 2) % slotFrames.Length];
                    relicFrame = slotFrames[(baseFrameIndex + 3) % slotFrames.Length];
                }

                if (isOccupied)
                {
                    // Check hover state from the invisible buttons
                    int baseBtnIndex = i * 3;
                    bool weaponHover = _partyEquipButtons[baseBtnIndex].IsHovered;
                    bool armorHover = _partyEquipButtons[baseBtnIndex + 1].IsHovered;
                    bool relicHover = _partyEquipButtons[baseBtnIndex + 2].IsHovered;

                    DrawEquipSlotIcon(spriteBatch, equipStartX, currentY, member!.EquippedWeaponId, EquipSlotType.Weapon, weaponHover, weaponFrame, i);
                    DrawEquipSlotIcon(spriteBatch, equipStartX + slotSize + gap, currentY, member.EquippedArmorId, EquipSlotType.Armor, armorHover, armorFrame, i);
                    DrawEquipSlotIcon(spriteBatch, equipStartX + (slotSize + gap) * 2, currentY, member.EquippedRelicId, EquipSlotType.Relic, relicHover, relicFrame, i);
                }
                else
                {
                    // Draw empty slot backgrounds
                    DrawEquipSlotBackground(spriteBatch, equipStartX, currentY, weaponFrame);
                    DrawEquipSlotBackground(spriteBatch, equipStartX + slotSize + gap, currentY, armorFrame);
                    DrawEquipSlotBackground(spriteBatch, equipStartX + (slotSize + gap) * 2, currentY, relicFrame);
                }

                currentY += slotSize + 6 - 5; // Moved up 5 pixels

                // 5. Stats
                string[] statLabels = { "STR", "INT", "TEN", "AGI" };
                string[] statKeys = { "Strength", "Intelligence", "Tenacity", "Agility" };

                for (int s = 0; s < 4; s++)
                {
                    int val = isOccupied ? _gameState.PlayerState.GetEffectiveStat(member!, statKeys[s]) : 0;
                    Color labelColor = isOccupied ? _global.Palette_LightGray : _global.Palette_DarkGray;

                    // Draw Label
                    spriteBatch.DrawStringSnapped(secondaryFont, statLabels[s], new Vector2(equipStartX - 3, currentY), labelColor);

                    // Draw Bar
                    Texture2D statBarBg = isOccupied ? _spriteManager.InventoryStatBarEmpty : _spriteManager.InventoryStatBarDisabled;
                    if (statBarBg != null)
                    {
                        float labelWidth = secondaryFont.MeasureString(statLabels[s]).Width;
                        float barX = equipStartX - 3 + labelWidth + 3; // 3px gap

                        // Center bar vertically with text. Text height is likely ~5-7. Bar is 3.
                        // Apply specific offsets for INT (index 1) and AGI (index 3)
                        float barYOffset = 0f;
                        if (s == 1 || s == 3) barYOffset = 0.5f;

                        float barY = currentY + (secondaryFont.LineHeight - 3) / 2f + barYOffset;

                        // Draw Empty
                        spriteBatch.DrawSnapped(statBarBg, new Vector2(barX, barY), Color.White);

                        // Draw Full (Only if occupied)
                        if (isOccupied && _spriteManager.InventoryStatBarFull != null)
                        {
                            int width = Math.Clamp(val, 1, 20) * 2;
                            var src = new Rectangle(0, 0, width, 3);
                            spriteBatch.DrawSnapped(_spriteManager.InventoryStatBarFull, new Vector2(barX, barY), src, Color.White);

                            // Draw Excess Text
                            if (val > 20)
                            {
                                int excess = val - 20;
                                string excessText = $"+{excess}";

                                Vector2 textSize = secondaryFont.MeasureString(excessText);

                                // Right align to the end of the bar (width 40)
                                float textX = (barX + 40) - textSize.X;

                                Vector2 textPos = new Vector2(textX, currentY);

                                // Draw background rectangle to hide bar underneath
                                var bgRect = new Rectangle((int)textPos.X - 1, (int)textPos.Y, (int)textSize.X + 2, (int)textSize.Y);
                                spriteBatch.DrawSnapped(pixel, bgRect, _global.Palette_Black);

                                spriteBatch.DrawStringOutlinedSnapped(secondaryFont, excessText, textPos, _global.Palette_BrightWhite, _global.Palette_Black);
                            }
                        }
                    }

                    currentY += (int)secondaryFont.LineHeight + 1;
                }

                // --- NEW: Draw Spell Slot Buttons ---
                currentY += 2; // Small gap after stats

                for (int s = 0; s < 4; s++)
                {
                    int buttonIndex = (i * 4) + s;
                    if (buttonIndex < _partySpellButtons.Count)
                    {
                        var btn = _partySpellButtons[buttonIndex];

                        // Update button state
                        if (isOccupied)
                        {
                            var spellEntry = member!.EquippedSpells[s];
                            if (spellEntry != null && BattleDataCache.Moves.TryGetValue(spellEntry.MoveID, out var moveData))
                            {
                                btn.SpellName = moveData.MoveName;
                                btn.HasSpell = true;
                            }
                            else
                            {
                                btn.SpellName = "EMPTY";
                                btn.HasSpell = false;
                            }
                            btn.IsEnabled = true;
                        }
                        else
                        {
                            btn.SpellName = "EMPTY";
                            btn.HasSpell = false;
                            btn.IsEnabled = false;
                        }

                        // Draw the button
                        btn.Draw(spriteBatch, font, gameTime, Matrix.Identity);
                    }
                }
            }
        }

        private void DrawEquipSlotBackground(SpriteBatch spriteBatch, int x, int y, Rectangle bgFrame)
        {
            if (bgFrame == Rectangle.Empty) return;

            Vector2 centerPos = new Vector2(x + 8, y + 8);
            Vector2 origin = new Vector2(12, 12); // Center of 24x24 sprite
            float scale = 1.0f;

            spriteBatch.DrawSnapped(_spriteManager.InventorySlotIdleSpriteSheet, centerPos, bgFrame, Color.White, 0f, origin, scale, SpriteEffects.None, 0f);
        }

        private void DrawEquipSlotIcon(SpriteBatch spriteBatch, int x, int y, string? itemId, EquipSlotType type, bool isHovered, Rectangle bgFrame, int memberIndex)
        {
            var pixel = ServiceLocator.Get<Texture2D>();
            var destRect = new Rectangle(x, y, 16, 16);

            // Calculate center
            Vector2 centerPos = new Vector2(x + 8, y + 8);
            Vector2 origin = new Vector2(12, 12); // Center of 24x24 sprite

            // Use 1.0f scale to draw the 24x24 sprite at full size behind the 16x16 slot
            float scale = 1.0f;

            // Draw Background (Randomized Slot Sprite)
            if (bgFrame != Rectangle.Empty)
            {
                spriteBatch.DrawSnapped(_spriteManager.InventorySlotIdleSpriteSheet, centerPos, bgFrame, Color.White, 0f, origin, scale, SpriteEffects.None, 0f);
            }

            // Check Selection State
            bool isSelected = _isEquipSubmenuOpen && _currentPartyMemberIndex == memberIndex && _activeEquipSlotType == type;

            if (isSelected || isHovered)
            {
                // Draw 1px border
                Color borderColor = _global.Palette_BrightWhite;
                // Top
                spriteBatch.DrawSnapped(pixel, new Rectangle(destRect.X, destRect.Y, destRect.Width, 1), borderColor);
                // Bottom
                spriteBatch.DrawSnapped(pixel, new Rectangle(destRect.X, destRect.Bottom - 1, destRect.Width, 1), borderColor);
                // Left
                spriteBatch.DrawSnapped(pixel, new Rectangle(destRect.X, destRect.Y, 1, destRect.Height), borderColor);
                // Right
                spriteBatch.DrawSnapped(pixel, new Rectangle(destRect.Right - 1, destRect.Y, 1, destRect.Height), borderColor);
            }

            if (!string.IsNullOrEmpty(itemId))
            {
                string path = "";
                if (type == EquipSlotType.Weapon)
                {
                    var data = GetWeaponData(itemId);
                    if (data != null) path = $"Sprites/Items/Weapons/{data.WeaponID}";
                }
                else if (type == EquipSlotType.Armor)
                {
                    var data = GetArmorData(itemId);
                    if (data != null) path = $"Sprites/Items/Armor/{data.ArmorID}";
                }
                else if (type == EquipSlotType.Relic)
                {
                    var data = GetRelicData(itemId);
                    if (data != null) path = $"Sprites/Items/Relics/{data.RelicID}";
                }

                if (!string.IsNullOrEmpty(path))
                {
                    var icon = _spriteManager.GetSmallRelicSprite(path);
                    if (icon != null)
                    {
                        spriteBatch.DrawSnapped(icon, destRect, Color.White);
                    }
                }
            }
            else
            {
                // Draw empty slot sprite if no item is equipped
                if (_spriteManager.InventoryEmptySlotSprite != null)
                {
                    spriteBatch.DrawSnapped(_spriteManager.InventoryEmptySlotSprite, destRect, Color.White);
                }
            }
        }

        private void DrawInfoPanel(SpriteBatch spriteBatch, BitmapFont font, BitmapFont secondaryFont, GameTime gameTime, bool drawBackground, Rectangle infoPanelArea)
        {
            // --- Prepare Idle Slot Background ---
            var slotFrames = _spriteManager.InventorySlotSourceRects;
            Rectangle idleFrame = Rectangle.Empty;
            Vector2 idleOrigin = Vector2.Zero;
            if (slotFrames != null && slotFrames.Length > 0)
            {
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
                    float spriteYOffset = 6f;
                    int idleSpriteX = infoPanelArea.X + (infoPanelArea.Width - 24) / 2;
                    float spriteY = infoPanelArea.Y + spriteYOffset;
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
            else if (_selectedInventoryCategory == InventoryCategory.Misc)
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
                DrawSpellInfoPanel(spriteBatch, font, secondaryFont, spellData, iconTexture, iconSilhouette, sourceRect, activeSlot.IconTint, idleFrame, idleOrigin, drawBackground, infoPanelArea);
                return;
            }

            // --- STANDARD RENDERING FOR OTHER CATEGORIES ---
            const int spriteSize = 16; // Native 16x16
            const int gap = 4;

            int maxTitleWidth = infoPanelArea.Width - (4 * 2);
            var titleLines = ParseAndWrapRichText(font, name, maxTitleWidth, _global.Palette_BrightWhite);
            float totalTitleHeight = titleLines.Count * font.LineHeight;

            float totalDescHeight = 0f;
            List<List<ColoredText>> descLines = new List<List<ColoredText>>();
            if (!string.IsNullOrEmpty(description))
            {
                float descWidth = infoPanelArea.Width - (4 * 2);
                descLines = ParseAndWrapRichText(secondaryFont, description, descWidth, _global.Palette_White);
                totalDescHeight = descLines.Count * secondaryFont.LineHeight;
            }

            float totalStatHeight = Math.Max(statLines.Positives.Count, statLines.Negatives.Count) * secondaryFont.LineHeight;
            float totalContentHeight = spriteSize + gap + totalTitleHeight + (totalDescHeight > 0 ? gap + totalDescHeight : 0) + (totalStatHeight > 0 ? gap + totalStatHeight : 0);

            float currentY = infoPanelArea.Y + (infoPanelArea.Height - totalContentHeight) / 2f;
            currentY -= 22f;

            int spriteX = infoPanelArea.X + (infoPanelArea.Width - spriteSize) / 2;

            float displayScale = 1.0f;
            float bgScale = 1.0f;

            if (drawBackground)
            {
                if (idleFrame != Rectangle.Empty)
                {
                    Vector2 itemCenter = new Vector2(spriteX + 8, currentY + 8);
                    spriteBatch.DrawSnapped(_spriteManager.InventorySlotIdleSpriteSheet, itemCenter, idleFrame, Color.White, 0f, idleOrigin, bgScale, SpriteEffects.None, 0f);
                }
                return;
            }

            Vector2 iconOrigin = new Vector2(8, 8);
            Vector2 drawPos = new Vector2(spriteX + 8, currentY + 8);

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

                float lineX = infoPanelArea.X + (infoPanelArea.Width - lineWidth) / 2f;
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

                    var lineX = infoPanelArea.X + (infoPanelArea.Width - lineWidth) / 2;
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
                float leftColX = infoPanelArea.X + 8;
                float rightColX = infoPanelArea.X + 64;

                int maxRows = Math.Max(statLines.Positives.Count, statLines.Negatives.Count);

                for (int i = 0; i < maxRows; i++)
                {
                    if (i < statLines.Positives.Count)
                    {
                        var lineParts = ParseAndWrapRichText(secondaryFont, statLines.Positives[i], infoPanelArea.Width / 2, _global.Palette_White);
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
                        var lineParts = ParseAndWrapRichText(secondaryFont, statLines.Negatives[i], infoPanelArea.Width / 2, _global.Palette_White);
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

        private void DrawSpellInfoPanel(SpriteBatch spriteBatch, BitmapFont font, BitmapFont secondaryFont, MoveData move, Texture2D? iconTexture, Texture2D? iconSilhouette, Rectangle? sourceRect, Color? iconTint, Rectangle idleFrame, Vector2 idleOrigin, bool drawBackground, Rectangle infoPanelArea)
        {
            const int spriteSize = 16;
            const int padding = 4;
            const int gap = 2;

            int spriteX = infoPanelArea.X + (infoPanelArea.Width - spriteSize) / 2;
            int spriteY = infoPanelArea.Y + 2;

            float displayScale = 1.0f;
            float bgScale = 1.0f;

            if (drawBackground)
            {
                if (idleFrame != Rectangle.Empty)
                {
                    Vector2 itemCenter = new Vector2(spriteX + 8, spriteY + 8);
                    spriteBatch.DrawSnapped(_spriteManager.InventorySlotIdleSpriteSheet, itemCenter, idleFrame, Color.White, 0f, idleOrigin, bgScale, SpriteEffects.None, 0f);
                }
                return;
            }

            Vector2 iconOrigin = new Vector2(8, 8);
            Vector2 drawPos = new Vector2(spriteX + 8, spriteY + 8);

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

            string name = move.MoveName.ToUpper();
            Vector2 nameSize = font.MeasureString(name);
            Vector2 namePos = new Vector2(
                infoPanelArea.X + (infoPanelArea.Width - nameSize.X) / 2f,
                spriteY + spriteSize - (font.LineHeight / 2f) + 2
            );

            spriteBatch.DrawStringOutlinedSnapped(font, name, namePos, _global.Palette_BrightWhite, _global.Palette_Black);

            float currentY = namePos.Y + font.LineHeight + 2;

            float leftLabelX = infoPanelArea.X + 8;
            float leftValueRightX = infoPanelArea.X + 51;

            float rightLabelX = infoPanelArea.X + 59;
            float rightValueRightX = infoPanelArea.X + 112;

            void DrawStatPair(string label, string value, float labelX, float valueRightX, float y, Color valColor)
            {
                spriteBatch.DrawStringSnapped(secondaryFont, label, new Vector2(labelX, y), _global.Palette_Gray);
                float valWidth = secondaryFont.MeasureString(value).Width;
                spriteBatch.DrawStringSnapped(secondaryFont, value, new Vector2(valueRightX - valWidth, y), valColor);
            }

            string powVal = move.Power > 0 ? move.Power.ToString() : "---";
            string accVal = move.Accuracy >= 0 ? $"{move.Accuracy}%" : "---";
            DrawStatPair("POW", powVal, leftLabelX, leftValueRightX, currentY, _global.Palette_White);
            DrawStatPair("ACC", accVal, rightLabelX, rightValueRightX, currentY, _global.Palette_White);
            currentY += secondaryFont.LineHeight + gap;

            string mpVal = move.ManaCost > 0 ? move.ManaCost.ToString() : "0";
            string targetVal = move.Target switch
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
            DrawStatPair("MP ", mpVal, leftLabelX, leftValueRightX, currentY, _global.Palette_LightBlue);
            DrawStatPair("TGT", targetVal, rightLabelX, rightValueRightX, currentY, _global.Palette_White);
            currentY += secondaryFont.LineHeight + gap;

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

            string impactVal = move.ImpactType.ToString().ToUpper().Substring(0, Math.Min(4, move.ImpactType.ToString().Length));
            Color impactColor = move.ImpactType == ImpactType.Magical ? _global.Palette_LightBlue : (move.ImpactType == ImpactType.Physical ? _global.Palette_Orange : _global.Palette_Gray);

            DrawStatPair("USE", offStatVal, leftLabelX, leftValueRightX, currentY, offColor);
            DrawStatPair("TYP", impactVal, rightLabelX, rightValueRightX, currentY, impactColor);
            currentY += secondaryFont.LineHeight + gap;

            if (move.MakesContact)
            {
                string contactText = "[MAKES CONTACT]";
                Vector2 contactSize = secondaryFont.MeasureString(contactText);
                Vector2 contactPos = new Vector2(infoPanelArea.X + (infoPanelArea.Width - contactSize.X) / 2f, currentY);
                spriteBatch.DrawStringSnapped(secondaryFont, contactText, contactPos, _global.Palette_Red);
                currentY += secondaryFont.LineHeight + gap;
            }

            string description = move.Description.ToUpper();
            if (!string.IsNullOrEmpty(description))
            {
                float descWidth = infoPanelArea.Width - (padding * 2);
                var descLines = ParseAndWrapRichText(secondaryFont, description, descWidth, _global.Palette_White);

                int maxLines = 8;
                if (descLines.Count > maxLines)
                {
                    descLines = descLines.Take(maxLines).ToList();
                }

                float totalDescHeight = descLines.Count * secondaryFont.LineHeight;
                float bottomPadding = 8f;
                float startY = infoPanelArea.Bottom - bottomPadding - totalDescHeight;

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

                    float lineX = infoPanelArea.X + (infoPanelArea.Width - lineWidth) / 2;
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
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
            // (Omitted for brevity - unchanged)
            // ...
        }

        private void DrawStatsPanel(SpriteBatch spriteBatch, BitmapFont font, BitmapFont secondaryFont, GameTime gameTime)
        {
            // --- Draw Hovered Item Details ---
            if (_hoveredItemData != null)
            {
                // (Omitted for brevity - unchanged)
                // ...
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
                            spriteBatch.DrawSnapped(_spriteManager.InventorySlotIdleSpriteSheet, center, _portraitBgFrame, Color.White, 0f, origin, 1f, SpriteEffects.None, 0f);
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
            // (Implementation omitted for brevity, same as previous)
            return new List<List<ColoredText>>();
        }
        private Color ParseColor(string colorName) { return Color.White; }
        private void DrawSpellInfoPanel(SpriteBatch spriteBatch, BitmapFont font, BitmapFont secondaryFont, MoveData move, Texture2D? iconTexture, Texture2D? iconSilhouette, Rectangle? sourceRect, Color? iconTint, Rectangle idleFrame, Vector2 idleOrigin, bool drawBackground) { }
    }
}
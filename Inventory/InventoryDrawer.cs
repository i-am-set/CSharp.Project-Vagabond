using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Dice;
using ProjectVagabond.Progression;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ProjectVagabond.UI
{
    public class InventoryDrawer
    {
        private readonly SplitMapInventoryOverlay _overlay;
        private readonly InventoryDataProcessor _dataProcessor;
        private readonly InventoryEquipSystem _equipSystem;
        private readonly ItemTooltipRenderer _tooltipRenderer;
        // Fields for portrait background animation
        private int _portraitBgFrameIndex = 0;
        private float _portraitBgTimer;
        private float _portraitBgDuration;
        private static readonly Random _rng = new Random();
        public InventoryDrawer(SplitMapInventoryOverlay overlay, InventoryDataProcessor dataProcessor, InventoryEquipSystem equipSystem)
        {
            _overlay = overlay;
            _dataProcessor = dataProcessor;
            _equipSystem = equipSystem;
            _tooltipRenderer = ServiceLocator.Get<ItemTooltipRenderer>();
        }

        public void DrawWorld(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            if (!_overlay.IsOpen) return;
            var inventoryPosition = new Vector2(0, 200);
            var headerPosition = inventoryPosition + _overlay.InventoryPositionOffset;

            // --- PASS 1: Draw Info Panel Background (Idle Slot) BEFORE Borders ---
            if (_overlay.SelectedInventoryCategory != InventoryCategory.Equip)
            {
                const int statsPanelWidth = 116;
                const int statsPanelHeight = 132;
                int statsPanelX = _overlay.InventorySlotArea.Right + 4;
                int statsPanelY = _overlay.InventorySlotArea.Y - 1;
                var infoPanelArea = new Rectangle(statsPanelX, statsPanelY, statsPanelWidth, statsPanelHeight);

                DrawInfoPanelBackground(spriteBatch, gameTime, infoPanelArea);
            }
            else if (_overlay.SelectedInventoryCategory == InventoryCategory.Equip && _overlay.HoveredItemData != null)
            {
                const int statsPanelWidth = 116;
                const int statsPanelHeight = 132;
                int statsPanelY = _overlay.InventorySlotArea.Y - 1;
                int statsPanelX = _overlay.InventorySlotArea.Right + 4;

                if (_overlay.HoveredMemberIndex == 0 || _overlay.HoveredMemberIndex == 1) statsPanelX = 194 - 24;
                else if (_overlay.HoveredMemberIndex == 2 || _overlay.HoveredMemberIndex == 3) statsPanelX = 10 + 24;

                var infoPanelArea = new Rectangle(statsPanelX, statsPanelY, statsPanelWidth, statsPanelHeight);
                DrawInfoPanelBackground(spriteBatch, gameTime, infoPanelArea);
            }

            spriteBatch.DrawSnapped(_overlay.SpriteManager.InventoryBorderHeader, headerPosition, Color.White);

            Texture2D selectedBorderSprite;
            if (_overlay.SelectedInventoryCategory == InventoryCategory.Equip && _overlay.CurrentState == InventoryState.EquipItemSelection)
            {
                selectedBorderSprite = _overlay.SpriteManager.InventoryBorderEquipSubmenu;
            }
            else
            {
                selectedBorderSprite = _overlay.SelectedInventoryCategory switch
                {
                    InventoryCategory.Weapons => _overlay.SpriteManager.InventoryBorderWeapons,
                    InventoryCategory.Armor => _overlay.SpriteManager.InventoryBorderArmor,
                    InventoryCategory.Relics => _overlay.SpriteManager.InventoryBorderRelics,
                    InventoryCategory.Consumables => _overlay.SpriteManager.InventoryBorderConsumables,
                    InventoryCategory.Misc => _overlay.SpriteManager.InventoryBorderMisc,
                    InventoryCategory.Equip => _overlay.SpriteManager.InventoryBorderEquip,
                    _ => _overlay.SpriteManager.InventoryBorderWeapons,
                };
            }
            spriteBatch.DrawSnapped(selectedBorderSprite, inventoryPosition, Color.White);

            foreach (var button in _overlay.InventoryHeaderButtons) button.Draw(spriteBatch, font, gameTime, Matrix.Identity);

            if (_overlay.SelectedInventoryCategory != InventoryCategory.Equip)
            {
                const int statsPanelWidth = 116;
                const int statsPanelHeight = 132;
                int statsPanelX = _overlay.InventorySlotArea.Right + 4;
                int statsPanelY = _overlay.InventorySlotArea.Y - 1;
                var infoPanelArea = new Rectangle(statsPanelX, statsPanelY, statsPanelWidth, statsPanelHeight);

                string categoryTitle = _overlay.SelectedInventoryCategory.ToString().ToUpper();
                Vector2 titleSize = font.MeasureString(categoryTitle);
                Vector2 titlePos = new Vector2(
                    infoPanelArea.X + (infoPanelArea.Width - titleSize.X) / 2f,
                    infoPanelArea.Y + 5 + _overlay.InventoryPositionOffset.Y
                );
                spriteBatch.DrawStringSnapped(font, categoryTitle, titlePos, _overlay.Global.Palette_DarkGray);
            }

            if (_overlay.SelectedInventoryCategory != InventoryCategory.Equip)
            {
                foreach (var slot in _overlay.InventorySlots) slot.Draw(spriteBatch, font, gameTime, Matrix.Identity);

                if (_overlay.TotalPages > 1)
                {
                    var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
                    string pageText = $"{_overlay.CurrentPage + 1}/{_overlay.TotalPages}";
                    var textSize = secondaryFont.MeasureString(pageText);
                    var textPos = new Vector2(
                        _overlay.InventorySlotArea.Center.X - textSize.Width / 2f,
                        _overlay.InventorySlotArea.Bottom - 2
                    );

                    var pixel = ServiceLocator.Get<Texture2D>();
                    var bgRect = new Rectangle(
                        (int)textPos.X - 1,
                        (int)textPos.Y + 2,
                        (int)Math.Ceiling(textSize.Width) + 5,
                        (int)textSize.Height
                    );
                    spriteBatch.DrawSnapped(pixel, bgRect, _overlay.Global.Palette_Black);

                    spriteBatch.DrawStringSnapped(secondaryFont, pageText, textPos, _overlay.Global.Palette_BlueWhite);

                    _overlay.PageLeftButton?.Draw(spriteBatch, font, gameTime, Matrix.Identity);
                    _overlay.PageRightButton?.Draw(spriteBatch, font, gameTime, Matrix.Identity);
                }

                const int statsPanelWidth = 116;
                const int statsPanelHeight = 132;
                int statsPanelX = _overlay.InventorySlotArea.Right + 4;
                int statsPanelY = _overlay.InventorySlotArea.Y - 1;
                var infoPanelArea = new Rectangle(statsPanelX, statsPanelY, statsPanelWidth, statsPanelHeight);

                // Draw Info Panel Content using the new renderer
                InventorySlot? activeSlot = _overlay.InventorySlots.FirstOrDefault(s => s.IsSelected);
                if (activeSlot == null) activeSlot = _overlay.InventorySlots.FirstOrDefault(s => s.IsHovered);

                if (activeSlot != null && activeSlot.HasItem && !string.IsNullOrEmpty(activeSlot.ItemId))
                {
                    object? itemData = _overlay.HoveredItemData;

                    if (itemData != null)
                    {
                        // Pass default scale and opacity
                        _tooltipRenderer.DrawInfoPanelContent(spriteBatch, itemData, infoPanelArea, font, ServiceLocator.Get<Core>().SecondaryFont, gameTime, 1.0f);
                    }
                }
            }
            else
            {
                if (_overlay.CurrentState == InventoryState.EquipItemSelection)
                {
                    foreach (var button in _equipSystem.EquipSubmenuButtons)
                    {
                        button.Draw(spriteBatch, font, gameTime, Matrix.Identity);
                    }

                    var arrowTexture = _overlay.SpriteManager.InventoryScrollArrowsSprite;
                    var arrowRects = _overlay.SpriteManager.InventoryScrollArrowRects;

                    if (arrowTexture != null && arrowRects != null && _equipSystem.EquipSubmenuButtons.Count > 0)
                    {
                        int totalItems = 1;
                        var member = _overlay.GameState.PlayerState.Party[_overlay.CurrentPartyMemberIndex];
                        if (_equipSystem.ActiveEquipSlotType == EquipSlotType.Weapon) totalItems += _overlay.GameState.PlayerState.Weapons.Count;
                        else if (_equipSystem.ActiveEquipSlotType == EquipSlotType.Armor) totalItems += _overlay.GameState.PlayerState.Armors.Count;
                        else if (_equipSystem.ActiveEquipSlotType == EquipSlotType.Relic) totalItems += _overlay.GameState.PlayerState.Relics.Count;

                        int maxScroll = Math.Max(0, totalItems - 7);

                        if (_equipSystem.EquipMenuScrollIndex > 0)
                        {
                            var firstButton = _equipSystem.EquipSubmenuButtons[0];
                            var arrowPos = new Vector2(
                                firstButton.Bounds.Center.X - arrowRects[0].Width / 2f,
                                firstButton.Bounds.Top - arrowRects[0].Height
                            );
                            spriteBatch.DrawSnapped(arrowTexture, arrowPos, arrowRects[0], Color.White);
                        }

                        if (_equipSystem.EquipMenuScrollIndex < maxScroll)
                        {
                            var lastButton = _equipSystem.EquipSubmenuButtons.Last();
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

                if (_overlay.HoveredItemData != null)
                {
                    Texture2D? overlayTex = null;
                    Rectangle infoPanelArea;

                    int statsPanelWidth = 116;
                    int statsPanelHeight = 132;
                    int statsPanelY = _overlay.InventorySlotArea.Y - 1;
                    int statsPanelX = _overlay.InventorySlotArea.Right + 4;

                    if (_overlay.HoveredMemberIndex == 0 || _overlay.HoveredMemberIndex == 1)
                    {
                        overlayTex = _overlay.SpriteManager.InventoryBorderEquipInfoPanelRight;
                        statsPanelX = 194 - 16;
                    }
                    else if (_overlay.HoveredMemberIndex == 2 || _overlay.HoveredMemberIndex == 3)
                    {
                        overlayTex = _overlay.SpriteManager.InventoryBorderEquipInfoPanelLeft;
                        statsPanelX = 10 + 16;
                    }

                    infoPanelArea = new Rectangle(statsPanelX, statsPanelY, statsPanelWidth, statsPanelHeight);

                    if (overlayTex != null)
                    {
                        spriteBatch.DrawSnapped(overlayTex, inventoryPosition, Color.White);
                    }

                    // Use the new renderer
                    // Pass default scale and opacity
                    _tooltipRenderer.DrawInfoPanelContent(spriteBatch, _overlay.HoveredItemData, infoPanelArea, font, ServiceLocator.Get<Core>().SecondaryFont, gameTime, 1.0f);
                }
            }

            if (_overlay.SelectedInventoryCategory != InventoryCategory.Equip)
            {
                _overlay.DebugButton1?.Draw(spriteBatch, font, gameTime, Matrix.Identity);
                _overlay.DebugButton2?.Draw(spriteBatch, font, gameTime, Matrix.Identity);
            }

            if (_overlay.Global.ShowSplitMapGrid)
            {
                var pixel = ServiceLocator.Get<Texture2D>();

                if (_overlay.SelectedInventoryCategory != InventoryCategory.Equip)
                {
                    spriteBatch.DrawSnapped(pixel, _overlay.InventorySlotArea, Color.Blue * 0.5f);
                    const int statsPanelWidth = 116;
                    const int statsPanelHeight = 132;
                    int statsPanelX = _overlay.InventorySlotArea.Right + 4;
                    int statsPanelY = _overlay.InventorySlotArea.Y - 1;
                    var infoPanelArea = new Rectangle(statsPanelX, statsPanelY, statsPanelWidth, statsPanelHeight);
                    spriteBatch.DrawSnapped(pixel, infoPanelArea, Color.Cyan * 0.5f);
                }
                else if (_overlay.CurrentState == InventoryState.EquipItemSelection)
                {
                    spriteBatch.DrawSnapped(pixel, _overlay.InventorySlotArea, Color.Blue * 0.5f);
                }

                if (_overlay.SelectedInventoryCategory == InventoryCategory.Equip)
                {
                    Color[] debugColors = { Color.Green, Color.Yellow, Color.Orange, Color.Red };
                    for (int i = 0; i < 4; i++)
                    {
                        spriteBatch.DrawSnapped(pixel, _overlay.PartyMemberPanelAreas[i], debugColors[i] * 0.5f);
                    }

                    foreach (var btn in _overlay.PartyEquipButtons)
                    {
                        spriteBatch.DrawSnapped(pixel, btn.Bounds, Color.Magenta * 0.5f);
                    }
                }
            }

            _overlay.InventoryEquipButton?.Draw(spriteBatch, font, gameTime, Matrix.Identity);
        }

        private void DrawInfoPanelBackground(SpriteBatch spriteBatch, GameTime gameTime, Rectangle infoPanelArea)
        {
            var slotFrames = _overlay.SpriteManager.InventorySlotSourceRects;
            Rectangle idleFrame = Rectangle.Empty;
            Vector2 idleOrigin = Vector2.Zero;
            if (slotFrames != null && slotFrames.Length > 0)
            {
                int frameIndex = (int)(gameTime.TotalGameTime.TotalSeconds / 2.0) % slotFrames.Length;
                idleFrame = slotFrames[frameIndex];
                idleOrigin = new Vector2(idleFrame.Width / 2f, idleFrame.Height / 2f);
            }

            if (idleFrame != Rectangle.Empty)
            {
                float spriteYOffset = 6f;
                int idleSpriteX = infoPanelArea.X + (infoPanelArea.Width - 24) / 2;
                float spriteY = infoPanelArea.Y + spriteYOffset;
                Vector2 itemCenter = new Vector2(idleSpriteX + 12, spriteY + 12);
                spriteBatch.DrawSnapped(_overlay.SpriteManager.InventorySlotIdleSpriteSheet, itemCenter, idleFrame, Color.White, 0f, idleOrigin, 1.0f, SpriteEffects.None, 0f);
            }
        }

        public void DrawScreen(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            _overlay.InventoryButton?.Draw(spriteBatch, font, gameTime, transform);
        }

        private void DrawPartyMemberSlots(SpriteBatch spriteBatch, BitmapFont font, BitmapFont secondaryFont, GameTime gameTime)
        {
            var pixel = ServiceLocator.Get<Texture2D>();
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _portraitBgTimer += dt;
            if (_portraitBgTimer >= _portraitBgDuration)
            {
                _portraitBgTimer = 0f;
                _portraitBgDuration = (float)(_rng.NextDouble() * (8.0 - 2.0) + 2.0);
                var frames = _overlay.SpriteManager.InventorySlotLargeSourceRects;
                if (frames != null && frames.Length > 0) _portraitBgFrameIndex = _rng.Next(frames.Length);
            }

            int baseFrameIndex = _portraitBgFrameIndex;
            var slotFrames = _overlay.SpriteManager.InventorySlotSourceRects;

            for (int i = 0; i < 4; i++)
            {
                var bounds = _overlay.PartyMemberPanelAreas[i];
                bool isOccupied = i < _overlay.GameState.PlayerState.Party.Count;
                var member = isOccupied ? _overlay.GameState.PlayerState.Party[i] : null;

                int centerX = bounds.Center.X;
                int currentY = bounds.Y + 4;

                string name = isOccupied ? member!.Name.ToUpper() : "EMPTY";
                Color nameColor = isOccupied ? _overlay.Global.Palette_BlueWhite : _overlay.Global.Palette_DarkGray;

                Vector2 nameSize = font.MeasureString(name);
                Vector2 namePos = new Vector2(centerX - nameSize.X / 2, currentY);

                // --- ANIMATION LOGIC ---
                // Only apply animation if the slot is occupied.
                // Empty slots should be static.
                if (isOccupied)
                {
                    var animator = _overlay.PartySlotAnimators[i];
                    var visualState = animator.GetVisualState();

                    // Apply offset to name position
                    namePos += visualState.Offset;

                    // Apply opacity to name color
                    nameColor = nameColor * visualState.Opacity;
                }

                currentY += (int)nameSize.Y - 2;

                if (_overlay.SpriteManager.InventorySlotLargeSourceRects != null && _overlay.SpriteManager.InventorySlotLargeSourceRects.Length > 0)
                {
                    var largeFrame = _overlay.SpriteManager.InventorySlotLargeSourceRects[_portraitBgFrameIndex];
                    Vector2 bgPos = new Vector2(centerX, currentY + 16);
                    Vector2 origin = new Vector2(largeFrame.Width / 2f, largeFrame.Height / 2f);
                    spriteBatch.DrawSnapped(_overlay.SpriteManager.InventorySlotIdleLargeSpriteSheet, bgPos, largeFrame, Color.White, 0f, origin, 1.0f, SpriteEffects.None, 0f);
                }

                if (isOccupied && _overlay.SpriteManager.PlayerPortraitsSpriteSheet != null && _overlay.SpriteManager.PlayerPortraitSourceRects.Count > 0)
                {
                    int portraitIndex = Math.Clamp(member!.PortraitIndex, 0, _overlay.SpriteManager.PlayerPortraitSourceRects.Count - 1);
                    var sourceRect = _overlay.SpriteManager.PlayerPortraitSourceRects[portraitIndex];

                    float animSpeed = 1f;
                    int frame = (int)(gameTime.TotalGameTime.TotalSeconds * animSpeed) % 2;
                    Texture2D textureToDraw = frame == 0 ? _overlay.SpriteManager.PlayerPortraitsSpriteSheet : _overlay.SpriteManager.PlayerPortraitsAltSpriteSheet;

                    var destRect = new Rectangle(centerX - 16, currentY, 32, 32);
                    spriteBatch.DrawSnapped(textureToDraw, destRect, sourceRect, Color.White);
                }

                // --- DRAW ANIMATED TEXT ---
                if (isOccupied)
                {
                    // Use TextAnimator with the current effect and timer
                    // Pass the raw timer value (which can be negative for delay)
                    TextAnimator.DrawTextWithEffect(
                        spriteBatch,
                        font,
                        name,
                        namePos,
                        nameColor,
                        _overlay.PartySlotTextEffects[i],
                        _overlay.PartySlotTextTimers[i] // Removed Math.Max(0, ...) to allow negative delay
                    );
                }
                else
                {
                    // Static draw for empty slots
                    spriteBatch.DrawStringSnapped(font, name, namePos, nameColor);
                }

                currentY += 32 + 2 - 6;

                Texture2D healthBarBg = isOccupied ? _overlay.SpriteManager.InventoryPlayerHealthBarEmpty : _overlay.SpriteManager.InventoryPlayerHealthBarDisabled;
                if (healthBarBg != null)
                {
                    int barX = centerX - (healthBarBg.Width / 2);
                    spriteBatch.DrawSnapped(healthBarBg, new Vector2(barX, currentY), Color.White);

                    if (isOccupied && _overlay.SpriteManager.InventoryPlayerHealthBarFull != null)
                    {
                        int currentHP = member!.CurrentHP;
                        int maxHP = member.MaxHP;
                        float hpPercent = (float)currentHP / Math.Max(1, maxHP);
                        int fullWidth = _overlay.SpriteManager.InventoryPlayerHealthBarFull.Width;
                        int visibleWidth = (int)(fullWidth * hpPercent);

                        var srcRect = new Rectangle(0, 0, visibleWidth, _overlay.SpriteManager.InventoryPlayerHealthBarFull.Height);
                        spriteBatch.DrawSnapped(_overlay.SpriteManager.InventoryPlayerHealthBarFull, new Vector2(barX + 1, currentY), srcRect, Color.White);
                    }

                    // Calculate height for layout spacing regardless of whether we draw text
                    float textHeight = secondaryFont.MeasureString("0/0").Height;

                    if (isOccupied)
                    {
                        string hpValText = $"{member!.CurrentHP}/{member.MaxHP}";
                        Color hpValColor = _overlay.Global.Palette_BlueWhite;
                        string hpSuffix = " HP";

                        Vector2 valSize = secondaryFont.MeasureString(hpValText);
                        Vector2 suffixSize = secondaryFont.MeasureString(hpSuffix);
                        float totalHpWidth = valSize.X + suffixSize.X;
                        textHeight = valSize.Y;

                        float hpTextX = centerX - (totalHpWidth / 2f);
                        float hpTextY = currentY + 7;

                        spriteBatch.DrawStringSnapped(secondaryFont, hpValText, new Vector2(hpTextX, hpTextY), hpValColor);
                        spriteBatch.DrawStringSnapped(secondaryFont, hpSuffix, new Vector2(hpTextX + valSize.X, hpTextY), _overlay.Global.Palette_Gray);
                    }

                    currentY += 8 + (int)textHeight + 4 - 3;
                }

                int slotSize = 16;
                int gap = 4;
                int totalEquipWidth = (slotSize * 3) + (gap * 2);
                int equipStartX = centerX - (totalEquipWidth / 2);

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
                    int baseBtnIndex = i * 3;
                    bool weaponHover = _overlay.PartyEquipButtons[baseBtnIndex].IsHovered;
                    bool armorHover = _overlay.PartyEquipButtons[baseBtnIndex + 1].IsHovered;
                    bool relicHover = _overlay.PartyEquipButtons[baseBtnIndex + 2].IsHovered;

                    DrawEquipSlotIcon(spriteBatch, equipStartX, currentY, member!.EquippedWeaponId, EquipSlotType.Weapon, weaponHover, weaponFrame, i);
                    DrawEquipSlotIcon(spriteBatch, equipStartX + slotSize + gap, currentY, member.EquippedArmorId, EquipSlotType.Armor, armorHover, armorFrame, i);
                    DrawEquipSlotIcon(spriteBatch, equipStartX + (slotSize + gap) * 2, currentY, member.EquippedRelicId, EquipSlotType.Relic, relicHover, relicFrame, i);
                }
                else
                {
                    DrawEquipSlotBackground(spriteBatch, equipStartX, currentY, weaponFrame);
                    DrawEquipSlotBackground(spriteBatch, equipStartX + slotSize + gap, currentY, armorFrame);
                    DrawEquipSlotBackground(spriteBatch, equipStartX + (slotSize + gap) * 2, currentY, relicFrame);
                }

                currentY += slotSize + 6 - 5;

                string[] statLabels = { "STR", "INT", "TEN", "AGI" };
                string[] statKeys = { "Strength", "Intelligence", "Tenacity", "Agility" };

                Dictionary<string, int>? hoveredItemStats = null;
                if (isOccupied && _overlay.HoveredMemberIndex == i && _overlay.HoveredItemData != null)
                {
                    if (_overlay.HoveredItemData is WeaponData w) hoveredItemStats = w.StatModifiers;
                    else if (_overlay.HoveredItemData is ArmorData a) hoveredItemStats = a.StatModifiers;
                    else if (_overlay.HoveredItemData is RelicData r) hoveredItemStats = r.StatModifiers;
                }

                for (int s = 0; s < 4; s++)
                {
                    int rawTotal = 0;
                    int baseStat = 0;
                    int totalEquipBonus = 0;

                    if (isOccupied)
                    {
                        baseStat = _overlay.GameState.PlayerState.GetBaseStat(member!, statKeys[s]);
                        int weaponBonus = 0;
                        if (!string.IsNullOrEmpty(member!.EquippedWeaponId) && BattleDataCache.Weapons.TryGetValue(member.EquippedWeaponId, out var w))
                            w.StatModifiers.TryGetValue(statKeys[s], out weaponBonus);

                        int armorBonus = 0;
                        if (!string.IsNullOrEmpty(member.EquippedArmorId) && BattleDataCache.Armors.TryGetValue(member.EquippedArmorId, out var a))
                            a.StatModifiers.TryGetValue(statKeys[s], out armorBonus);

                        int relicBonus = 0;
                        if (!string.IsNullOrEmpty(member.EquippedRelicId) && BattleDataCache.Relics.TryGetValue(member.EquippedRelicId, out var r))
                            r.StatModifiers.TryGetValue(statKeys[s], out relicBonus);

                        totalEquipBonus = weaponBonus + armorBonus + relicBonus;
                        rawTotal = baseStat + totalEquipBonus;
                    }

                    int currentEffective = Math.Max(1, rawTotal);
                    Color labelColor = isOccupied ? _overlay.Global.Palette_LightGray : _overlay.Global.Palette_DarkGray;

                    int itemBonus = 0;
                    if (hoveredItemStats != null && hoveredItemStats.TryGetValue(statKeys[s], out int bonus))
                    {
                        itemBonus = bonus;
                    }

                    int rawWithoutItem = rawTotal - itemBonus;
                    int effectiveWithoutItem = Math.Max(1, rawWithoutItem);

                    spriteBatch.DrawStringSnapped(secondaryFont, statLabels[s], new Vector2(equipStartX - 3, currentY), labelColor);

                    Texture2D statBarBg = isOccupied ? _overlay.SpriteManager.InventoryStatBarEmpty : _overlay.SpriteManager.InventoryStatBarDisabled;
                    if (statBarBg != null)
                    {
                        float labelWidth = secondaryFont.MeasureString(statLabels[s]).Width;
                        float barX = equipStartX - 3 + labelWidth + 3;
                        float barYOffset = 0f;
                        if (s == 1 || s == 3) barYOffset = 0.5f;

                        float barY = currentY + (secondaryFont.LineHeight - 3) / 2f + barYOffset;

                        spriteBatch.DrawSnapped(statBarBg, new Vector2(barX, barY), Color.White);

                        if (isOccupied && _overlay.SpriteManager.InventoryStatBarFull != null)
                        {
                            int whiteBarPoints;
                            int coloredBarPoints;
                            Color coloredBarColor;

                            if (hoveredItemStats != null)
                            {
                                // --- HOVER MODE: Show specific item impact ---
                                if (itemBonus > 0)
                                {
                                    whiteBarPoints = Math.Clamp(effectiveWithoutItem, 1, 20);
                                    int totalPoints = Math.Clamp(currentEffective, 1, 20);
                                    coloredBarPoints = totalPoints - whiteBarPoints;
                                    coloredBarColor = _overlay.Global.StatColor_Increase;
                                }
                                else if (itemBonus < 0)
                                {
                                    whiteBarPoints = Math.Clamp(currentEffective, 1, 20);
                                    int totalPoints = Math.Clamp(effectiveWithoutItem, 1, 20);
                                    coloredBarPoints = totalPoints - whiteBarPoints;
                                    coloredBarColor = _overlay.Global.StatColor_Decrease;
                                }
                                else
                                {
                                    whiteBarPoints = Math.Clamp(currentEffective, 1, 20);
                                    coloredBarPoints = 0;
                                    coloredBarColor = Color.White;
                                }
                            }
                            else
                            {
                                // --- PASSIVE MODE: Show total equipment impact (Half Intensity) ---
                                if (totalEquipBonus > 0)
                                {
                                    // Base is white, Bonus is Dim Green
                                    whiteBarPoints = Math.Clamp(baseStat, 1, 20);
                                    int totalPoints = Math.Clamp(rawTotal, 1, 20);
                                    coloredBarPoints = totalPoints - whiteBarPoints;
                                    coloredBarColor = _overlay.Global.StatColor_Increase * 0.5f; // Dim Green
                                }
                                else if (totalEquipBonus < 0)
                                {
                                    // Total is white, Penalty (up to Base) is Dim Red
                                    whiteBarPoints = Math.Clamp(rawTotal, 1, 20);
                                    int basePoints = Math.Clamp(baseStat, 1, 20);
                                    coloredBarPoints = basePoints - whiteBarPoints;
                                    coloredBarColor = _overlay.Global.StatColor_Decrease * 0.5f; // Dim Red
                                }
                                else
                                {
                                    whiteBarPoints = Math.Clamp(rawTotal, 1, 20);
                                    coloredBarPoints = 0;
                                    coloredBarColor = Color.White;
                                }
                            }

                            int whiteWidth = whiteBarPoints * 2;
                            int coloredWidth = coloredBarPoints * 2;

                            if (whiteWidth > 0)
                            {
                                var srcBase = new Rectangle(0, 0, whiteWidth, 3);
                                spriteBatch.DrawSnapped(_overlay.SpriteManager.InventoryStatBarFull, new Vector2(barX, barY), srcBase, Color.White);
                            }

                            if (coloredWidth > 0)
                            {
                                var srcColor = new Rectangle(0, 0, coloredWidth, 3);
                                spriteBatch.DrawSnapped(_overlay.SpriteManager.InventoryStatBarFull, new Vector2(barX + whiteWidth, barY), srcColor, coloredBarColor);
                            }

                            // --- EXCESS TEXT LOGIC ---
                            // Only show excess text if hovering an item, or if the total is > 20
                            if (currentEffective > 20 || effectiveWithoutItem > 20)
                            {
                                int excessValue;
                                Color textColor;

                                if (hoveredItemStats != null)
                                {
                                    if (itemBonus > 0)
                                    {
                                        excessValue = currentEffective - 20;
                                        if (effectiveWithoutItem < currentEffective) textColor = _overlay.Global.StatColor_Increase;
                                        else textColor = _overlay.Global.Palette_BlueWhite;
                                    }
                                    else if (itemBonus < 0)
                                    {
                                        excessValue = effectiveWithoutItem - 20;
                                        if (effectiveWithoutItem > currentEffective) textColor = _overlay.Global.StatColor_Decrease;
                                        else textColor = _overlay.Global.Palette_BlueWhite;
                                    }
                                    else
                                    {
                                        excessValue = currentEffective - 20;
                                        textColor = _overlay.Global.Palette_BlueWhite;
                                    }
                                }
                                else
                                {
                                    // Passive Mode Excess
                                    excessValue = currentEffective - 20;
                                    if (totalEquipBonus > 0) textColor = _overlay.Global.StatColor_Increase * 0.5f;
                                    else if (totalEquipBonus < 0) textColor = _overlay.Global.StatColor_Decrease * 0.5f;
                                    else textColor = _overlay.Global.Palette_BlueWhite;
                                }

                                if (excessValue > 0)
                                {
                                    string excessText = $"+{excessValue}";
                                    Vector2 textSize = secondaryFont.MeasureString(excessText);
                                    float textX = (barX + 40) - textSize.X;
                                    Vector2 textPos = new Vector2(textX, currentY);
                                    // Removed duplicate pixel declaration
                                    var bgRect = new Rectangle((int)textPos.X - 1, (int)textPos.Y, (int)textSize.X + 2, (int)textSize.Y);
                                    spriteBatch.DrawSnapped(pixel, bgRect, _overlay.Global.Palette_Black);
                                    spriteBatch.DrawStringOutlinedSnapped(secondaryFont, excessText, textPos, textColor, _overlay.Global.Palette_Black);
                                }
                            }
                        }
                    }
                    currentY += (int)secondaryFont.LineHeight + 1;
                }

                currentY += 2;

                for (int s = 0; s < 4; s++)
                {
                    int buttonIndex = (i * 4) + s;
                    if (buttonIndex < _overlay.PartySpellButtons.Count)
                    {
                        var btn = _overlay.PartySpellButtons[buttonIndex];
                        if (isOccupied)
                        {
                            var spellEntry = member!.Spells[s];
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
                        btn.Draw(spriteBatch, font, gameTime, Matrix.Identity);
                    }
                }
            }
        }

        private void DrawEquipSlotBackground(SpriteBatch spriteBatch, int x, int y, Rectangle bgFrame)
        {
            if (bgFrame == Rectangle.Empty) return;
            Vector2 centerPos = new Vector2(x + 8, y + 8);
            Vector2 origin = new Vector2(12, 12);
            spriteBatch.DrawSnapped(_overlay.SpriteManager.InventorySlotIdleSpriteSheet, centerPos, bgFrame, Color.White, 0f, origin, 1.0f, SpriteEffects.None, 0f);
        }

        private void DrawEquipSlotIcon(SpriteBatch spriteBatch, int x, int y, string? itemId, EquipSlotType type, bool isHovered, Rectangle bgFrame, int memberIndex)
        {
            var pixel = ServiceLocator.Get<Texture2D>();
            var destRect = new Rectangle(x, y, 16, 16);
            Vector2 centerPos = new Vector2(x + 8, y + 8);
            Vector2 origin = new Vector2(12, 12);

            if (bgFrame != Rectangle.Empty)
            {
                spriteBatch.DrawSnapped(_overlay.SpriteManager.InventorySlotIdleSpriteSheet, centerPos, bgFrame, Color.White, 0f, origin, 1.0f, SpriteEffects.None, 0f);
            }

            bool isSelected = _overlay.CurrentState == InventoryState.EquipItemSelection && _overlay.CurrentPartyMemberIndex == memberIndex && _overlay.EquipSystem.ActiveEquipSlotType == type;

            if (!string.IsNullOrEmpty(itemId))
            {
                string path = "";
                if (type == EquipSlotType.Weapon)
                {
                    var data = _dataProcessor.GetWeaponData(itemId);
                    if (data != null) path = $"Sprites/Items/Weapons/{data.WeaponID}";
                }
                else if (type == EquipSlotType.Armor)
                {
                    var data = _dataProcessor.GetArmorData(itemId);
                    if (data != null) path = $"Sprites/Items/Armor/{data.ArmorID}";
                }
                else if (type == EquipSlotType.Relic)
                {
                    var data = _dataProcessor.GetRelicData(itemId);
                    if (data != null) path = $"Sprites/Items/Relics/{data.RelicID}";
                }

                if (!string.IsNullOrEmpty(path))
                {
                    var icon = _overlay.SpriteManager.GetSmallRelicSprite(path);
                    var silhouette = _overlay.SpriteManager.GetSmallRelicSpriteSilhouette(path);

                    if (icon != null)
                    {
                        if (silhouette != null)
                        {
                            bool active = isSelected || isHovered;
                            Color mainColor = active ? _overlay.Global.Palette_BlueWhite : _overlay.Global.ItemOutlineColor_Idle;
                            Color cornerColor = active ? _overlay.Global.Palette_LightGray : _overlay.Global.ItemOutlineColor_Idle_Corner;

                            // Corners
                            spriteBatch.DrawSnapped(silhouette, new Rectangle(destRect.X - 1, destRect.Y - 1, destRect.Width, destRect.Height), cornerColor);
                            spriteBatch.DrawSnapped(silhouette, new Rectangle(destRect.X + 1, destRect.Y - 1, destRect.Width, destRect.Height), cornerColor);
                            spriteBatch.DrawSnapped(silhouette, new Rectangle(destRect.X - 1, destRect.Y + 1, destRect.Width, destRect.Height), cornerColor);
                            spriteBatch.DrawSnapped(silhouette, new Rectangle(destRect.X + 1, destRect.Y + 1, destRect.Width, destRect.Height), cornerColor);

                            // Cardinals
                            spriteBatch.DrawSnapped(silhouette, new Rectangle(destRect.X - 1, destRect.Y, destRect.Width, destRect.Height), mainColor);
                            spriteBatch.DrawSnapped(silhouette, new Rectangle(destRect.X + 1, destRect.Y, destRect.Width, destRect.Height), mainColor);
                            spriteBatch.DrawSnapped(silhouette, new Rectangle(destRect.X, destRect.Y - 1, destRect.Width, destRect.Height), mainColor);
                            spriteBatch.DrawSnapped(silhouette, new Rectangle(destRect.X, destRect.Y + 1, destRect.Width, destRect.Height), mainColor);
                        }

                        spriteBatch.DrawSnapped(icon, destRect, Color.White);
                    }
                }
            }
            else if (_overlay.SpriteManager.InventoryEmptySlotSprite != null)
            {
                Color emptyColor = (isSelected || isHovered) ? _overlay.Global.Palette_LightGray : _overlay.Global.Palette_DarkGray;
                spriteBatch.DrawSnapped(_overlay.SpriteManager.InventoryEmptySlotSprite, destRect, emptyColor);
            }
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
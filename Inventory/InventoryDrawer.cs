using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Dice;
using ProjectVagabond.Items;
using ProjectVagabond.Progression;
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
using static ProjectVagabond.Battle.Abilities.InflictStatusStunAbility;

namespace ProjectVagabond.UI
{
    public class InventoryDrawer
    {
        private readonly SplitMapInventoryOverlay _overlay;
        private readonly InventoryDataProcessor _dataProcessor;
        private readonly InventoryEquipSystem _equipSystem;
        private readonly ItemTooltipRenderer _tooltipRenderer;

        // --- ANIMATION TUNING (Character Panel Slots) ---
        private const float EQUIP_SLOT_FLOAT_SPEED = 2.5f;
        private const float EQUIP_SLOT_FLOAT_AMPLITUDE = 0.5f;
        private const float EQUIP_SLOT_ROTATION_SPEED = 2.0f;
        private const float EQUIP_SLOT_ROTATION_AMOUNT = 0.05f;

        // --- HOVER POP TUNING ---
        // Replaced local constant with Global.ItemHoverScale
        private const float HOVER_POP_SPEED = 12.0f;       // Fast spring speed

        // Track hover timers for each slot (Key: "{MemberIndex}_{SlotType}")
        private readonly Dictionary<string, float> _equipSlotHoverTimers = new Dictionary<string, float>();

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
                    InventoryCategory.Relics => _overlay.SpriteManager.InventoryBorderRelics,
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
                spriteBatch.DrawStringSnapped(font, categoryTitle, titlePos, _overlay.Global.Palette_DarkShadow);
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

                    spriteBatch.DrawStringSnapped(secondaryFont, pageText, textPos, _overlay.Global.Palette_Sun);

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

        public void DrawScreen(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            _overlay.InventoryButton?.Draw(spriteBatch, font, gameTime, transform);
        }

        private void DrawPartyMemberSlots(SpriteBatch spriteBatch, BitmapFont font, BitmapFont secondaryFont, GameTime gameTime)
        {
            var pixel = ServiceLocator.Get<Texture2D>();
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            for (int i = 0; i < 4; i++)
            {
                var bounds = _overlay.PartyMemberPanelAreas[i];
                bool isOccupied = i < _overlay.GameState.PlayerState.Party.Count;
                var member = isOccupied ? _overlay.GameState.PlayerState.Party[i] : null;

                int centerX = bounds.Center.X;
                int currentY = bounds.Y + 4;

                string name = isOccupied ? member!.Name.ToUpper() : "EMPTY";
                Color nameColor = isOccupied ? _overlay.Global.Palette_Sun : _overlay.Global.Palette_DarkShadow;

                Vector2 nameSize = font.MeasureString(name);
                Vector2 namePos = new Vector2(centerX - nameSize.X / 2, currentY);

                // --- ANIMATION LOGIC ---
                if (isOccupied)
                {
                    var animator = _overlay.PartySlotAnimators[i];
                    var visualState = animator.GetVisualState();
                    namePos += visualState.Offset;
                    nameColor = nameColor * visualState.Opacity;
                }

                currentY += (int)nameSize.Y - 2;

                if (isOccupied && _overlay.SpriteManager.PlayerMasterSpriteSheet != null)
                {
                    int portraitIndex = member!.PortraitIndex;
                    float animSpeed = 1f;
                    int frame = (int)(gameTime.TotalGameTime.TotalSeconds * animSpeed) % 2;
                    PlayerSpriteType type = frame == 0 ? PlayerSpriteType.Normal : PlayerSpriteType.Alt;

                    var sourceRect = _overlay.SpriteManager.GetPlayerSourceRect(portraitIndex, type);

                    var destRect = new Rectangle(centerX - 16, currentY, 32, 32);
                    spriteBatch.DrawSnapped(_overlay.SpriteManager.PlayerMasterSpriteSheet, destRect, sourceRect, Color.White);
                }

                // --- DRAW ANIMATED TEXT ---
                if (isOccupied)
                {
                    // Use Drift effect for occupied slots as requested
                    TextAnimator.DrawTextWithEffect(
                        spriteBatch,
                        font,
                        name,
                        namePos,
                        nameColor,
                        TextEffectType.Drift, // Forced Drift
                        _overlay.PartySlotTextTimers[i] // Timer is fine
                    );
                }
                else
                {
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

                    float textHeight = secondaryFont.MeasureString("0/0").Height;

                    if (isOccupied)
                    {
                        string hpValText = $"{member!.CurrentHP}/{member.MaxHP}";
                        Color hpValColor = _overlay.Global.Palette_DarkSun;
                        string hpSuffix = " HP";

                        Vector2 valSize = secondaryFont.MeasureString(hpValText);
                        Vector2 suffixSize = secondaryFont.MeasureString(hpSuffix);
                        float totalHpWidth = valSize.X + suffixSize.X;
                        textHeight = valSize.Y;

                        float hpTextX = centerX - (totalHpWidth / 2f);
                        float hpTextY = currentY + 7;

                        spriteBatch.DrawStringSnapped(secondaryFont, hpValText, new Vector2(hpTextX, hpTextY), hpValColor);
                        spriteBatch.DrawStringSnapped(secondaryFont, hpSuffix, new Vector2(hpTextX + valSize.X, hpTextY), _overlay.Global.Palette_DarkSun);
                    }

                    currentY += 8 + (int)textHeight + 4 - 3;
                }

                int slotSize = 16;
                int gap = 4;
                // 2 slots: Weapon, Relic
                int totalEquipWidth = (slotSize * 2) + (gap * 1);
                int equipStartX = centerX - (totalEquipWidth / 2);

                if (isOccupied)
                {
                    int baseBtnIndex = i * 2; // 2 buttons per member
                    bool weaponHover = _overlay.PartyEquipButtons[baseBtnIndex].IsHovered;
                    bool relicHover = _overlay.PartyEquipButtons[baseBtnIndex + 1].IsHovered;

                    DrawEquipSlotIcon(spriteBatch, equipStartX, currentY, member!.EquippedWeaponId, EquipSlotType.Weapon, weaponHover, i, gameTime);
                    DrawEquipSlotIcon(spriteBatch, equipStartX + slotSize + gap, currentY, member.EquippedRelicId, EquipSlotType.Relic, relicHover, i, gameTime);
                }

                currentY += slotSize + 6 - 5;

                string[] statLabels = { "STR", "INT", "TEN", "AGI" };
                string[] statKeys = { "Strength", "Intelligence", "Tenacity", "Agility" };

                // --- CENTERED STAT BLOCK LOGIC ---
                // Label (~17px) + Gap (3px) + Bar (40px) = ~60px total width.
                // CenterX - 30 puts the start 30px left of center.
                int statBlockStartX = centerX - 30;

                for (int s = 0; s < 4; s++)
                {
                    int baseStat = 0;
                    int totalEquipBonus = 0;

                    if (isOccupied)
                    {
                        baseStat = _overlay.GameState.PlayerState.GetBaseStat(member!, statKeys[s]);
                        int weaponBonus = 0;
                        if (!string.IsNullOrEmpty(member!.EquippedWeaponId) && BattleDataCache.Weapons.TryGetValue(member.EquippedWeaponId, out var w))
                            w.StatModifiers.TryGetValue(statKeys[s], out weaponBonus);

                        int relicBonus = 0;
                        if (!string.IsNullOrEmpty(member.EquippedRelicId) && BattleDataCache.Relics.TryGetValue(member.EquippedRelicId, out var r))
                            r.StatModifiers.TryGetValue(statKeys[s], out relicBonus);

                        totalEquipBonus = weaponBonus + relicBonus;
                    }

                    // --- DETERMINE HOVER CONTEXT ---
                    bool isHoveringThisMember = isOccupied && _overlay.HoveredMemberIndex == i;
                    bool isHoveringItem = isHoveringThisMember && _overlay.HoveredItemData != null;

                    // Identify the type of item being hovered
                    bool isHoveringWeapon = isHoveringItem && _overlay.HoveredItemData is WeaponData;
                    bool isHoveringRelic = isHoveringItem && _overlay.HoveredItemData is RelicData;

                    // --- CALCULATE LAYERS ---
                    // 1. Passive Bonus: Sum of equipped items EXCLUDING the slot being hovered/replaced.
                    int passiveBonus = 0;
                    if (isOccupied)
                    {
                        // Add Weapon if NOT hovering a weapon
                        if (!isHoveringWeapon && !string.IsNullOrEmpty(member!.EquippedWeaponId) && BattleDataCache.Weapons.TryGetValue(member.EquippedWeaponId, out var w))
                            if (w.StatModifiers.TryGetValue(statKeys[s], out int val)) passiveBonus += val;

                        // Add Relic if NOT hovering relic
                        if (!isHoveringRelic && !string.IsNullOrEmpty(member.EquippedRelicId) && BattleDataCache.Relics.TryGetValue(member.EquippedRelicId, out var r))
                            if (r.StatModifiers.TryGetValue(statKeys[s], out int val)) passiveBonus += val;
                    }

                    // 2. Active Bonus: Stats from the hovered item
                    int activeBonus = 0;
                    if (isHoveringItem)
                    {
                        if (isHoveringWeapon) ((WeaponData)_overlay.HoveredItemData).StatModifiers.TryGetValue(statKeys[s], out activeBonus);
                        else if (isHoveringRelic) ((RelicData)_overlay.HoveredItemData).StatModifiers.TryGetValue(statKeys[s], out activeBonus);
                    }

                    // Calculate Points for Drawing
                    int basePoints = Math.Clamp(baseStat, 1, 20);
                    int passiveTotal = Math.Clamp(baseStat + passiveBonus, 1, 20);
                    int finalTotal = Math.Clamp(baseStat + passiveBonus + activeBonus, 1, 20);

                    // Draw Label
                    Color labelColor = isOccupied ? _overlay.Global.Palette_DarkSun : _overlay.Global.Palette_DarkShadow;
                    if (isOccupied)
                    {
                        spriteBatch.DrawStringSnapped(secondaryFont, statLabels[s], new Vector2(statBlockStartX, currentY), labelColor);
                    }


                    // Draw Bar Background
                    Texture2D statBarBg = isOccupied ? _overlay.SpriteManager.InventoryStatBarEmpty : _overlay.SpriteManager.InventoryStatBarDisabled;
                    if (statBarBg != null)
                    {
                        // Fixed offset for bar to ensure vertical alignment
                        float barX = statBlockStartX + 19;
                        float barYOffset = 0f;
                        if (s == 1 || s == 3) barYOffset = 0.5f;
                        float barY = currentY + (secondaryFont.LineHeight - 3) / 2f + barYOffset;

                        spriteBatch.DrawSnapped(statBarBg, new Vector2(barX, barY), Color.White);

                        if (isOccupied && _overlay.SpriteManager.InventoryStatBarFull != null)
                        {
                            int pxPerPoint = 2;
                            int basePx = basePoints * pxPerPoint;
                            int passivePx = passiveTotal * pxPerPoint;
                            int finalPx = finalTotal * pxPerPoint;

                            // Layer 1: Base (White)
                            if (basePx > 0)
                            {
                                var srcBase = new Rectangle(0, 0, basePx, 3);
                                spriteBatch.DrawSnapped(_overlay.SpriteManager.InventoryStatBarFull, new Vector2(barX, barY), srcBase, Color.White);
                            }

                            // Layer 2: Passive Impact (Dim Colors)
                            if (passiveTotal > basePoints)
                            {
                                // Bonus: Draw Dim Green from base to passive
                                int width = passivePx - basePx;
                                var src = new Rectangle(0, 0, width, 3);
                                spriteBatch.DrawSnapped(_overlay.SpriteManager.InventoryStatBarFull, new Vector2(barX + basePx, barY), src, _overlay.Global.StatColor_Increase_Half);
                            }
                            else if (passiveTotal < basePoints)
                            {
                                // Penalty: Draw Dim Red from passive to base (overwriting white)
                                int width = basePx - passivePx;
                                var src = new Rectangle(0, 0, width, 3);
                                spriteBatch.DrawSnapped(_overlay.SpriteManager.InventoryStatBarFull, new Vector2(barX + passivePx, barY), src, _overlay.Global.StatColor_Decrease_Half);
                            }

                            // Layer 3: Active Impact (Bright Colors)
                            if (finalTotal > passiveTotal)
                            {
                                // Bonus: Draw Bright Green from passive to final
                                int width = finalPx - passivePx;
                                var src = new Rectangle(0, 0, width, 3);
                                spriteBatch.DrawSnapped(_overlay.SpriteManager.InventoryStatBarFull, new Vector2(barX + passivePx, barY), src, _overlay.Global.StatColor_Increase);
                            }
                            else if (finalTotal < passiveTotal)
                            {
                                // Penalty: Draw Bright Red from final to passive (overwriting previous layers)
                                int width = passivePx - finalPx;
                                var src = new Rectangle(0, 0, width, 3);
                                spriteBatch.DrawSnapped(_overlay.SpriteManager.InventoryStatBarFull, new Vector2(barX + finalPx, barY), src, _overlay.Global.StatColor_Decrease);
                            }

                            // --- EXCESS TEXT LOGIC ---
                            int rawFinalTotal = baseStat + passiveBonus + activeBonus;
                            if (rawFinalTotal > 20)
                            {
                                int excessValue = rawFinalTotal - 20;
                                Color textColor;

                                if (activeBonus > 0) textColor = _overlay.Global.StatColor_Increase;
                                else if (activeBonus < 0) textColor = _overlay.Global.StatColor_Decrease;
                                else if (passiveBonus > 0) textColor = _overlay.Global.StatColor_Increase * 0.5f;
                                else if (passiveBonus < 0) textColor = _overlay.Global.StatColor_Decrease * 0.5f;
                                else textColor = _overlay.Global.Palette_Sun;

                                string excessText = $"+{excessValue}";
                                Vector2 textSize = secondaryFont.MeasureString(excessText);
                                float textX = (barX + 40) - textSize.X;
                                Vector2 textPos = new Vector2(textX, currentY);
                                // Removed duplicate pixel declaration
                                spriteBatch.DrawSnapped(pixel, new Rectangle((int)textPos.X - 1, (int)textPos.Y, (int)textSize.X + 2, (int)textSize.Y), _overlay.Global.Palette_Black);
                                spriteBatch.DrawStringOutlinedSnapped(secondaryFont, excessText, textPos, textColor, _overlay.Global.Palette_Black);
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

        private int GetStatBonus(PartyMember member, string statName)
        {
            int bonus = 0;
            if (!string.IsNullOrEmpty(member.EquippedWeaponId) && BattleDataCache.Weapons.TryGetValue(member.EquippedWeaponId, out var w))
            {
                if (w.StatModifiers.TryGetValue(statName, out int val)) bonus += val;
            }

            if (!string.IsNullOrEmpty(member.EquippedRelicId) && BattleDataCache.Relics.TryGetValue(member.EquippedRelicId, out var r))
            {
                if (r.StatModifiers.TryGetValue(statName, out int val)) bonus += val;
            }

            return bonus;
        }

        private void DrawEquipSlotIcon(SpriteBatch spriteBatch, int x, int y, string? itemId, EquipSlotType type, bool isHovered, int memberIndex, GameTime gameTime)
        {
            var pixel = ServiceLocator.Get<Texture2D>();
            var destRect = new Rectangle(x, y, 16, 16);
            Vector2 centerPos = new Vector2(x + 8, y + 8);
            Vector2 origin = new Vector2(12, 12);

            bool isSelected = _overlay.CurrentState == InventoryState.EquipItemSelection && _overlay.CurrentPartyMemberIndex == memberIndex && _overlay.EquipSystem.ActiveEquipSlotType == type;

            if (!string.IsNullOrEmpty(itemId))
            {
                string path = "";
                if (type == EquipSlotType.Weapon)
                {
                    var data = _dataProcessor.GetWeaponData(itemId);
                    if (data != null) path = $"Sprites/Items/Weapons/{data.WeaponID}";
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
                        // --- JUICY FLOAT ANIMATION ---
                        // Calculate a smooth sine wave bob.
                        // Use x position as phase offset so slots don't bob in unison.
                        float time = (float)gameTime.TotalGameTime.TotalSeconds;
                        float phase = x * 0.1f;

                        // --- HOVER POP LOGIC ---
                        // Generate a unique key for this slot to track its hover state
                        string key = $"{memberIndex}_{type}";
                        if (!_equipSlotHoverTimers.ContainsKey(key)) _equipSlotHoverTimers[key] = 0f;

                        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
                        if (isHovered)
                            _equipSlotHoverTimers[key] = Math.Min(_equipSlotHoverTimers[key] + dt * HOVER_POP_SPEED, 1f);
                        else
                            _equipSlotHoverTimers[key] = Math.Max(_equipSlotHoverTimers[key] - dt * HOVER_POP_SPEED, 0f);

                        float t = _equipSlotHoverTimers[key];
                        // Use EaseOutBack for that "spring" pop
                        float popScale = 1.0f + (Global.ItemHoverScale - 1.0f) * Easing.EaseOutBack(t);

                        float floatOffset = MathF.Sin(time * EQUIP_SLOT_FLOAT_SPEED + phase) * EQUIP_SLOT_FLOAT_AMPLITUDE;
                        float rotation = MathF.Sin(time * EQUIP_SLOT_ROTATION_SPEED + phase) * EQUIP_SLOT_ROTATION_AMOUNT;

                        // Apply offset to Y
                        Vector2 drawPos = new Vector2(destRect.X, destRect.Y + floatOffset);

                        // Use DrawSnapped with rotation and origin for smooth movement
                        // Origin is center of 16x16 sprite (8,8)
                        Vector2 iconOrigin = new Vector2(8, 8);
                        Vector2 iconCenter = drawPos + iconOrigin;

                        // --- DOUBLE LAYERED OUTLINE (Always Visible) ---
                        if (silhouette != null)
                        {
                            // Determine colors based on state
                            Color mainOutlineColor, cornerOutlineColor;
                            var global = _overlay.Global;

                            if (isSelected)
                            {
                                mainOutlineColor = global.ItemOutlineColor_Selected;
                                cornerOutlineColor = global.ItemOutlineColor_Selected_Corner;
                            }
                            else if (isHovered)
                            {
                                mainOutlineColor = global.ItemOutlineColor_Hover;
                                cornerOutlineColor = global.ItemOutlineColor_Hover_Corner;
                            }
                            else
                            {
                                mainOutlineColor = global.ItemOutlineColor_Idle;
                                cornerOutlineColor = global.ItemOutlineColor_Idle_Corner;
                            }

                            // 1. Draw Diagonals (Corners) FIRST (Behind)
                            spriteBatch.DrawSnapped(silhouette, iconCenter + new Vector2(-1, -1), null, cornerOutlineColor, rotation, iconOrigin, popScale, SpriteEffects.None, 0f);
                            spriteBatch.DrawSnapped(silhouette, iconCenter + new Vector2(1, -1), null, cornerOutlineColor, rotation, iconOrigin, popScale, SpriteEffects.None, 0f);
                            spriteBatch.DrawSnapped(silhouette, iconCenter + new Vector2(-1, 1), null, cornerOutlineColor, rotation, iconOrigin, popScale, SpriteEffects.None, 0f);
                            spriteBatch.DrawSnapped(silhouette, iconCenter + new Vector2(1, 1), null, cornerOutlineColor, rotation, iconOrigin, popScale, SpriteEffects.None, 0f);

                            // 2. Draw Cardinals (Main) SECOND (On Top)
                            spriteBatch.DrawSnapped(silhouette, iconCenter + new Vector2(-1, 0), null, mainOutlineColor, rotation, iconOrigin, popScale, SpriteEffects.None, 0f);
                            spriteBatch.DrawSnapped(silhouette, iconCenter + new Vector2(1, 0), null, mainOutlineColor, rotation, iconOrigin, popScale, SpriteEffects.None, 0f);
                            spriteBatch.DrawSnapped(silhouette, iconCenter + new Vector2(0, -1), null, mainOutlineColor, rotation, iconOrigin, popScale, SpriteEffects.None, 0f);
                            spriteBatch.DrawSnapped(silhouette, iconCenter + new Vector2(0, 1), null, mainOutlineColor, rotation, iconOrigin, popScale, SpriteEffects.None, 0f);
                        }

                        spriteBatch.DrawSnapped(icon, iconCenter, null, Color.White, rotation, iconOrigin, popScale, SpriteEffects.None, 0f);
                    }
                }
            }
            else if (_overlay.SpriteManager.InventoryEmptySlotSprite != null)
            {
                Color emptyColor = (isSelected || isHovered) ? _overlay.Global.Palette_Shadow : _overlay.Global.Palette_DarkShadow;
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
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

        // Fields for portrait background animation
        private int _portraitBgFrameIndex = 0;
        private float _portraitBgTimer;
        private float _portraitBgDuration;
        private static readonly Random _rng = new Random();

        // --- UNIFIED LAYOUT CONSTANTS ---
        // All Y offsets are relative to the Info Panel's Top (Y)

        private const int LAYOUT_SPRITE_Y = 6;
        private const int LAYOUT_TITLE_Y = 30;
        private const int LAYOUT_VARS_START_Y = 44;

        private const int LAYOUT_VAR_ROW_HEIGHT = 9; // 7px font + 2px gap
        private const int LAYOUT_CONTACT_Y = LAYOUT_VARS_START_Y + (3 * LAYOUT_VAR_ROW_HEIGHT); // Row 4
        private const int LAYOUT_DESC_START_Y = LAYOUT_CONTACT_Y + LAYOUT_VAR_ROW_HEIGHT - 3;

        public InventoryDrawer(SplitMapInventoryOverlay overlay, InventoryDataProcessor dataProcessor, InventoryEquipSystem equipSystem)
        {
            _overlay = overlay;
            _dataProcessor = dataProcessor;
            _equipSystem = equipSystem;
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

                DrawInfoPanel(spriteBatch, font, ServiceLocator.Get<Core>().SecondaryFont, gameTime, true, infoPanelArea);
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

                var slotFrames = _overlay.SpriteManager.InventorySlotSourceRects;
                Rectangle idleFrame = Rectangle.Empty;
                Vector2 idleOrigin = Vector2.Zero;
                if (slotFrames != null && slotFrames.Length > 0)
                {
                    int frameIndex = (int)(gameTime.TotalGameTime.TotalSeconds / 2.0) % slotFrames.Length;
                    idleFrame = slotFrames[frameIndex];
                    idleOrigin = new Vector2(idleFrame.Width / 2f, idleFrame.Height / 2f);
                }

                int spriteSize = (_overlay.HoveredItemData is MoveData) ? 32 : 16;
                int spriteX = infoPanelArea.X + (infoPanelArea.Width - spriteSize) / 2;

                // Use Fixed Layout Y
                float spriteY = infoPanelArea.Y + LAYOUT_SPRITE_Y;

                // Apply specific offsets for background drawing to match foreground
                if (_overlay.HoveredItemData is MoveData)
                {
                    spriteY -= 3; // Move Moves UP 3px
                }
                else
                {
                    spriteY += 2; // Move Items DOWN 2px
                }

                if (spriteSize == 16) spriteY += 8; // Center 16px sprite in 32px slot

                Vector2 itemCenter = new Vector2(spriteX + spriteSize / 2f, spriteY + spriteSize / 2f);
                spriteBatch.DrawSnapped(_overlay.SpriteManager.InventorySlotIdleSpriteSheet, itemCenter, idleFrame, Color.White, 0f, idleOrigin, 1.0f, SpriteEffects.None, 0f);
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

                DrawInfoPanel(spriteBatch, font, ServiceLocator.Get<Core>().SecondaryFont, gameTime, false, infoPanelArea);
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

                    if (_overlay.HoveredItemData is MoveData moveData)
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

                        var iconTexture = _overlay.SpriteManager.GetItemSprite(iconPath, fallbackPath);
                        var iconSilhouette = _overlay.SpriteManager.GetItemSpriteSilhouette(iconPath, fallbackPath);
                        Rectangle? sourceRect = _overlay.SpriteManager.GetAnimatedIconSourceRect(iconTexture, gameTime);

                        DrawSpellInfoPanel(spriteBatch, font, ServiceLocator.Get<Core>().SecondaryFont, moveData, iconTexture, iconSilhouette, sourceRect, null, Rectangle.Empty, Vector2.Zero, false, infoPanelArea, gameTime);
                    }
                    else if (_overlay.HoveredItemData is WeaponData weaponData)
                    {
                        string iconPath = $"Sprites/Items/Weapons/{weaponData.WeaponID}";
                        var iconTexture = _overlay.SpriteManager.GetItemSprite(iconPath);
                        var iconSilhouette = _overlay.SpriteManager.GetItemSpriteSilhouette(iconPath);

                        DrawWeaponInfoPanel(spriteBatch, font, ServiceLocator.Get<Core>().SecondaryFont, weaponData, iconTexture, iconSilhouette, infoPanelArea, gameTime);
                    }
                    else
                    {
                        string name = "";
                        string description = "";
                        string iconPath = "";
                        Dictionary<string, int> stats = new Dictionary<string, int>();

                        if (_overlay.HoveredItemData is ArmorData a) { name = a.ArmorName.ToUpper(); description = a.Description; iconPath = $"Sprites/Items/Armor/{a.ArmorID}"; stats = a.StatModifiers; }
                        else if (_overlay.HoveredItemData is RelicData r) { name = r.RelicName.ToUpper(); description = r.Description; iconPath = $"Sprites/Items/Relics/{r.RelicID}"; stats = r.StatModifiers; }

                        var iconTexture = _overlay.SpriteManager.GetItemSprite(iconPath);
                        var iconSilhouette = _overlay.SpriteManager.GetItemSpriteSilhouette(iconPath);

                        DrawGenericItemInfoPanel(spriteBatch, font, ServiceLocator.Get<Core>().SecondaryFont, name, description, iconTexture, iconSilhouette, stats, infoPanelArea, gameTime);
                    }
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

                spriteBatch.DrawStringSnapped(font, name, namePos, nameColor);

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

                    string hpValText = isOccupied ? $"{member!.CurrentHP}/{member.MaxHP}" : "0/0";
                    Color hpValColor = isOccupied ? _overlay.Global.Palette_BlueWhite : _overlay.Global.Palette_DarkGray;
                    string hpSuffix = " HP";

                    Vector2 valSize = secondaryFont.MeasureString(hpValText);
                    Vector2 suffixSize = secondaryFont.MeasureString(hpSuffix);
                    float totalHpWidth = valSize.X + suffixSize.X;

                    float hpTextX = centerX - (totalHpWidth / 2f);
                    float hpTextY = currentY + 7;

                    spriteBatch.DrawStringSnapped(secondaryFont, hpValText, new Vector2(hpTextX, hpTextY), hpValColor);
                    spriteBatch.DrawStringSnapped(secondaryFont, hpSuffix, new Vector2(hpTextX + valSize.X, hpTextY), _overlay.Global.Palette_Gray);

                    currentY += 8 + (int)valSize.Y + 4 - 3;
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
                    if (isOccupied)
                    {
                        int baseStat = _overlay.GameState.PlayerState.GetBaseStat(member!, statKeys[s]);
                        int weaponBonus = 0;
                        if (!string.IsNullOrEmpty(member!.EquippedWeaponId) && BattleDataCache.Weapons.TryGetValue(member.EquippedWeaponId, out var w))
                            w.StatModifiers.TryGetValue(statKeys[s], out weaponBonus);

                        int armorBonus = 0;
                        if (!string.IsNullOrEmpty(member.EquippedArmorId) && BattleDataCache.Armors.TryGetValue(member.EquippedArmorId, out var a))
                            a.StatModifiers.TryGetValue(statKeys[s], out armorBonus);

                        int relicBonus = 0;
                        if (!string.IsNullOrEmpty(member.EquippedRelicId) && BattleDataCache.Relics.TryGetValue(member.EquippedRelicId, out var r))
                            r.StatModifiers.TryGetValue(statKeys[s], out relicBonus);

                        rawTotal = baseStat + weaponBonus + armorBonus + relicBonus;
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

                            if (currentEffective > 20 || effectiveWithoutItem > 20)
                            {
                                int excessValue;
                                Color textColor;

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

                                if (excessValue > 0)
                                {
                                    string excessText = $"+{excessValue}";
                                    Vector2 textSize = secondaryFont.MeasureString(excessText);
                                    float textX = (barX + 40) - textSize.X;
                                    Vector2 textPos = new Vector2(textX, currentY);
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

        private void DrawInfoPanel(SpriteBatch spriteBatch, BitmapFont font, BitmapFont secondaryFont, GameTime gameTime, bool drawBackground, Rectangle infoPanelArea)
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

            InventorySlot? activeSlot = _overlay.InventorySlots.FirstOrDefault(s => s.IsSelected);
            if (activeSlot == null)
            {
                activeSlot = _overlay.InventorySlots.FirstOrDefault(s => s.IsHovered);
            }

            if (activeSlot == null || !activeSlot.HasItem || string.IsNullOrEmpty(activeSlot.ItemId))
            {
                if (drawBackground && idleFrame != Rectangle.Empty)
                {
                    float spriteYOffset = 6f;
                    int idleSpriteX = infoPanelArea.X + (infoPanelArea.Width - 24) / 2;
                    float spriteY = infoPanelArea.Y + spriteYOffset;
                    Vector2 itemCenter = new Vector2(idleSpriteX + 12, spriteY + 12);
                    spriteBatch.DrawSnapped(_overlay.SpriteManager.InventorySlotIdleSpriteSheet, itemCenter, idleFrame, Color.White, 0f, idleOrigin, 1.0f, SpriteEffects.None, 0f);
                }
                return;
            }

            string name = activeSlot.ItemId.ToUpper();
            string description = "";
            string iconPath = activeSlot.IconPath ?? "";
            Texture2D? iconTexture = null;
            Texture2D? iconSilhouette = null;
            string? fallbackPath = null;
            Dictionary<string, int> stats = new Dictionary<string, int>();

            if (_overlay.SelectedInventoryCategory == InventoryCategory.Relics)
            {
                var relic = BattleDataCache.Relics.Values.FirstOrDefault(r => r.RelicName.Equals(activeSlot.ItemId, StringComparison.OrdinalIgnoreCase));
                if (relic != null)
                {
                    name = relic.RelicName.ToUpper();
                    description = relic.Description.ToUpper();
                    iconPath = $"Sprites/Items/Relics/{relic.RelicID}";
                    stats = relic.StatModifiers;
                }
            }
            else if (_overlay.SelectedInventoryCategory == InventoryCategory.Consumables)
            {
                var item = BattleDataCache.Consumables.Values.FirstOrDefault(c => c.ItemName.Equals(activeSlot.ItemId, StringComparison.OrdinalIgnoreCase));
                if (item != null)
                {
                    name = item.ItemName.ToUpper();
                    description = item.Description.ToUpper();
                    iconPath = item.ImagePath;
                }
            }
            else if (_overlay.SelectedInventoryCategory == InventoryCategory.Misc)
            {
                var item = BattleDataCache.MiscItems.Values.FirstOrDefault(m => m.ItemName.Equals(activeSlot.ItemId, StringComparison.OrdinalIgnoreCase));
                if (item != null)
                {
                    name = item.ItemName.ToUpper();
                    description = item.Description.ToUpper();
                    iconPath = item.ImagePath;
                }
            }
            else if (_overlay.SelectedInventoryCategory == InventoryCategory.Weapons)
            {
                var weapon = BattleDataCache.Weapons.Values.FirstOrDefault(w => w.WeaponName.Equals(activeSlot.ItemId, StringComparison.OrdinalIgnoreCase));
                if (weapon != null)
                {
                    // Pass WeaponData to the specialized drawer
                    if (!drawBackground)
                    {
                        DrawWeaponInfoPanel(spriteBatch, font, secondaryFont, weapon, null, null, infoPanelArea, gameTime);
                        return;
                    }
                    // For background pass, just get the icon path
                    iconPath = $"Sprites/Items/Weapons/{weapon.WeaponID}";
                }
            }
            else if (_overlay.SelectedInventoryCategory == InventoryCategory.Armor)
            {
                var armor = BattleDataCache.Armors.Values.FirstOrDefault(a => a.ArmorName.Equals(activeSlot.ItemId, StringComparison.OrdinalIgnoreCase));
                if (armor != null)
                {
                    name = armor.ArmorName.ToUpper();
                    description = armor.Description.ToUpper();
                    iconPath = $"Sprites/Items/Armor/{armor.ArmorID}";
                    stats = armor.StatModifiers;
                }
            }

            if (!string.IsNullOrEmpty(iconPath))
            {
                iconTexture = _overlay.SpriteManager.GetItemSprite(iconPath, fallbackPath);
                iconSilhouette = _overlay.SpriteManager.GetItemSpriteSilhouette(iconPath, fallbackPath);
            }

            if (!drawBackground)
            {
                DrawGenericItemInfoPanel(spriteBatch, font, secondaryFont, name, description, iconTexture, iconSilhouette, stats, infoPanelArea, gameTime);
            }
            else
            {
                const int spriteSize = 16;
                int spriteX = infoPanelArea.X + (infoPanelArea.Width - spriteSize) / 2;
                float currentY = infoPanelArea.Y + LAYOUT_SPRITE_Y;

                // Apply specific offsets for background drawing to match foreground
                if (_overlay.SelectedInventoryCategory == InventoryCategory.Weapons ||
                    _overlay.SelectedInventoryCategory == InventoryCategory.Armor ||
                    _overlay.SelectedInventoryCategory == InventoryCategory.Relics ||
                    _overlay.SelectedInventoryCategory == InventoryCategory.Consumables ||
                    _overlay.SelectedInventoryCategory == InventoryCategory.Misc)
                {
                    currentY += 2; // Move Items DOWN 2px
                }

                if (idleFrame != Rectangle.Empty)
                {
                    Vector2 itemCenter = new Vector2(spriteX + 8, currentY + 8);
                    spriteBatch.DrawSnapped(_overlay.SpriteManager.InventorySlotIdleSpriteSheet, itemCenter, idleFrame, Color.White, 0f, idleOrigin, 1.0f, SpriteEffects.None, 0f);
                }
            }
        }

        private Vector2 GetJuicyOffset(GameTime gameTime)
        {
            float t = (float)gameTime.TotalGameTime.TotalSeconds * 1.5f; // Was 3.0f
            float dist =1.0f; // Was 1.0f

            float swayX = (MathF.Sin(t * 1.1f) * dist) + (MathF.Cos(t * 0.4f) * (dist * 0.5f));
            float swayY = MathF.Sin(t * 1.4f) * dist;

            return new Vector2(swayX, swayY);
        }

        private void DrawWeaponInfoPanel(SpriteBatch spriteBatch, BitmapFont font, BitmapFont secondaryFont, WeaponData weapon, Texture2D? iconTexture, Texture2D? iconSilhouette, Rectangle infoPanelArea, GameTime gameTime)
        {
            // Load textures if not provided (e.g. from DrawInfoPanel call)
            if (iconTexture == null)
            {
                string path = $"Sprites/Items/Weapons/{weapon.WeaponID}";
                iconTexture = _overlay.SpriteManager.GetItemSprite(path);
                iconSilhouette = _overlay.SpriteManager.GetItemSpriteSilhouette(path);
            }

            var tertiaryFont = ServiceLocator.Get<Core>().TertiaryFont;
            const int spriteSize = 16;
            const int gap = 2; // CHANGED from 4 to 2 to match Spell panel
            const int padding = 4;

            // --- 1. Draw Icon ---
            int spriteX = infoPanelArea.X + (infoPanelArea.Width - spriteSize) / 2;

            // Move DOWN 2px
            float currentY = infoPanelArea.Y + LAYOUT_SPRITE_Y + 2;

            Vector2 drawPos = new Vector2(spriteX + 8, currentY + 8);
            Vector2 iconOrigin = new Vector2(8, 8);
            float displayScale = 1.0f;

            // Apply Juicy Animation
            Vector2 animOffset = GetJuicyOffset(gameTime);
            drawPos += animOffset;

            if (iconSilhouette != null)
            {
                Color mainOutlineColor = _overlay.Global.ItemOutlineColor_Idle;
                Color cornerOutlineColor = _overlay.Global.ItemOutlineColor_Idle_Corner;

                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(-1, -1) * displayScale, null, cornerOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(1, -1) * displayScale, null, cornerOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(-1, 1) * displayScale, null, cornerOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(1, 1) * displayScale, null, cornerOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);

                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(-1, 0) * displayScale, null, mainOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(1, 0) * displayScale, null, mainOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(0, -1) * displayScale, null, mainOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(0, 1) * displayScale, null, mainOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
            }

            if (iconTexture != null)
            {
                spriteBatch.DrawSnapped(iconTexture, drawPos, null, Color.White, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
            }

            // --- 2. Draw Name ---
            string name = weapon.WeaponName.ToUpper();
            Vector2 nameSize = font.MeasureString(name);

            Vector2 namePos = new Vector2(
                infoPanelArea.X + (infoPanelArea.Width - nameSize.X) / 2f,
                infoPanelArea.Y + LAYOUT_TITLE_Y
            );

            namePos = new Vector2(MathF.Round(namePos.X), MathF.Round(namePos.Y));

            // Use DriftWave for title text
            TextAnimator.DrawTextWithEffectOutlined(spriteBatch, font, name, namePos, _overlay.Global.Palette_BlueWhite, _overlay.Global.Palette_Black, TextEffectType.DriftWave, (float)gameTime.TotalGameTime.TotalSeconds);

            // --- 3. Draw Move Stats ---
            currentY = infoPanelArea.Y + LAYOUT_VARS_START_Y;

            float leftLabelX = infoPanelArea.X + 8;
            float leftValueRightX = infoPanelArea.X + 51;
            float rightLabelX = infoPanelArea.X + 59;
            float rightValueRightX = infoPanelArea.X + 112;

            void DrawStatPair(string label, string value, float labelX, float valueRightX, float y, Color valColor, float xOffset = 0f)
            {
                // Center label vertically relative to value font
                float yOffset = (secondaryFont.LineHeight - tertiaryFont.LineHeight) / 2f;
                yOffset = MathF.Round(yOffset);

                spriteBatch.DrawStringSnapped(tertiaryFont, label, new Vector2(labelX, y + yOffset), _overlay.Global.Palette_Gray);

                float effectiveRightX = valueRightX + xOffset;

                if (value.EndsWith("%"))
                {
                    string numberPart = value.Substring(0, value.Length - 1);
                    string suffix = "%";

                    Vector2 numberSize = secondaryFont.MeasureString(numberPart);
                    Vector2 suffixSize = tertiaryFont.MeasureString(suffix);

                    // Draw Suffix (%)
                    Vector2 suffixPos = new Vector2(effectiveRightX - suffixSize.X, y + yOffset);
                    spriteBatch.DrawStringSnapped(tertiaryFont, suffix, suffixPos, _overlay.Global.Palette_Gray);

                    // Draw Number
                    Vector2 numberPos = new Vector2(suffixPos.X - numberSize.X - 1, y);
                    spriteBatch.DrawStringSnapped(secondaryFont, numberPart, numberPos, valColor);
                }
                else
                {
                    float valWidth = secondaryFont.MeasureString(value).Width;
                    spriteBatch.DrawStringSnapped(secondaryFont, value, new Vector2(effectiveRightX - valWidth, y), valColor);
                }
            }

            // Row 1: POW / ACC
            string powVal = weapon.Power > 0 ? weapon.Power.ToString() : "---";
            string accVal = weapon.Accuracy >= 0 ? $"{weapon.Accuracy}%" : "---";

            // POW: No shift (0)
            DrawStatPair("POW", powVal, leftLabelX, leftValueRightX, currentY, _overlay.Global.Palette_White, 0f);

            // ACC: Shift +5 if percentage (matched to Spell panel)
            float accOffset = accVal.Contains("%") ? 5f : 0f;
            DrawStatPair("ACC", accVal, rightLabelX, rightValueRightX, currentY, _overlay.Global.Palette_White, accOffset);

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

            // TGT: No shift (0)
            DrawStatPair("TGT", targetVal, rightLabelX, rightValueRightX, currentY, _overlay.Global.Palette_White, 0f);

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
                OffensiveStatType.Strength => _overlay.Global.StatColor_Strength,
                OffensiveStatType.Intelligence => _overlay.Global.StatColor_Intelligence,
                OffensiveStatType.Tenacity => _overlay.Global.StatColor_Tenacity,
                OffensiveStatType.Agility => _overlay.Global.StatColor_Agility,
                _ => _overlay.Global.Palette_White
            };

            string impactVal = weapon.ImpactType.ToString().ToUpper().Substring(0, Math.Min(4, weapon.ImpactType.ToString().Length));
            Color impactColor = weapon.ImpactType == ImpactType.Magical ? _overlay.Global.Palette_LightBlue : (weapon.ImpactType == ImpactType.Physical ? _overlay.Global.Palette_Orange : _overlay.Global.Palette_Gray);

            // USE/TYP: No shift (0)
            DrawStatPair("USE", offStatVal, leftLabelX, leftValueRightX, currentY, offColor, 0f);
            DrawStatPair("TYP", impactVal, rightLabelX, rightValueRightX, currentY, impactColor, 0f);

            // Row 4: Contact
            currentY = infoPanelArea.Y + LAYOUT_CONTACT_Y;
            if (weapon.MakesContact)
            {
                string contactText = "[MAKES CONTACT]";
                Vector2 contactSize = tertiaryFont.MeasureString(contactText);
                Vector2 contactPos = new Vector2(infoPanelArea.X + (infoPanelArea.Width - contactSize.X) / 2f, currentY);
                spriteBatch.DrawStringSnapped(tertiaryFont, contactText, contactPos, _overlay.Global.Palette_Red);
            }

            // --- 4. Draw Description ---
            currentY = infoPanelArea.Y + LAYOUT_DESC_START_Y;
            if (!string.IsNullOrEmpty(weapon.Description))
            {
                float descWidth = infoPanelArea.Width - (padding * 2);
                var descLines = ParseAndWrapRichText(secondaryFont, weapon.Description.ToUpper(), descWidth, _overlay.Global.Palette_White);

                foreach (var line in descLines)
                {
                    float lineWidth = 0;
                    foreach (var segment in line)
                    {
                        if (string.IsNullOrWhiteSpace(segment.Text)) lineWidth += segment.Text.Length * 5;
                        else lineWidth += secondaryFont.MeasureString(segment.Text).Width;
                    }

                    float lineX = infoPanelArea.X + (infoPanelArea.Width - lineWidth) / 2;
                    float currentX = lineX;

                    foreach (var segment in line)
                    {
                        float segWidth;
                        if (string.IsNullOrWhiteSpace(segment.Text)) segWidth = segment.Text.Length * 5;
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

            // --- 5. Draw Passive Stats (if any) ---
            var statLines = GetStatModifierLines(weapon.StatModifiers);
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
                        var lineParts = ParseAndWrapRichText(secondaryFont, statLines.Positives[i], infoPanelArea.Width / 2, _overlay.Global.Palette_White);
                        if (lineParts.Count > 0)
                        {
                            var segments = lineParts[0];
                            float currentX = leftColX;
                            foreach (var segment in segments)
                            {
                                float segWidth = string.IsNullOrWhiteSpace(segment.Text) ? segment.Text.Length * 5 : secondaryFont.MeasureString(segment.Text).Width;
                                spriteBatch.DrawStringSnapped(secondaryFont, segment.Text, new Vector2(currentX, currentY), segment.Color);
                                currentX += segWidth;
                            }
                        }
                    }

                    if (i < statLines.Negatives.Count)
                    {
                        var lineParts = ParseAndWrapRichText(secondaryFont, statLines.Negatives[i], infoPanelArea.Width / 2, _overlay.Global.Palette_White);
                        if (lineParts.Count > 0)
                        {
                            var segments = lineParts[0];
                            float currentX = rightColX;
                            foreach (var segment in segments)
                            {
                                float segWidth = string.IsNullOrWhiteSpace(segment.Text) ? segment.Text.Length * 5 : secondaryFont.MeasureString(segment.Text).Width;
                                spriteBatch.DrawStringSnapped(secondaryFont, segment.Text, new Vector2(currentX, currentY), segment.Color);
                                currentX += segWidth;
                            }
                        }
                    }
                    currentY += secondaryFont.LineHeight;
                }
            }
        }

        private void DrawSpellInfoPanel(SpriteBatch spriteBatch, BitmapFont font, BitmapFont secondaryFont, MoveData move, Texture2D? iconTexture, Texture2D? iconSilhouette, Rectangle? sourceRect, Color? iconTint, Rectangle idleFrame, Vector2 idleOrigin, bool drawBackground, Rectangle infoPanelArea, GameTime gameTime)
        {
            var tertiaryFont = ServiceLocator.Get<Core>().TertiaryFont; // Get Tertiary Font
            const int spriteSize = 32;
            const int padding = 4;
            const int gap = 2;

            int spriteX = infoPanelArea.X + (infoPanelArea.Width - spriteSize) / 2;

            // Move UP 3px
            int spriteY = infoPanelArea.Y + LAYOUT_SPRITE_Y - 3;

            float displayScale = 1.0f;
            float bgScale = 1.0f;

            if (drawBackground)
            {
                if (idleFrame != Rectangle.Empty)
                {
                    Vector2 itemCenter = new Vector2(spriteX + spriteSize / 2f, spriteY + spriteSize / 2f);
                    spriteBatch.DrawSnapped(_overlay.SpriteManager.InventorySlotIdleSpriteSheet, itemCenter, idleFrame, Color.White, 0f, idleOrigin, bgScale, SpriteEffects.None, 0f);
                }
                return;
            }

            Vector2 iconOrigin = new Vector2(16, 16);
            Vector2 drawPos = new Vector2(spriteX + 16, spriteY + 16);

            // NO Juicy Animation for Spells

            if (iconSilhouette != null)
            {
                Color mainOutlineColor = _overlay.Global.ItemOutlineColor_Idle;
                Color cornerOutlineColor = _overlay.Global.ItemOutlineColor_Idle_Corner;

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
                infoPanelArea.Y + LAYOUT_TITLE_Y
            );

            namePos = new Vector2(MathF.Round(namePos.X), MathF.Round(namePos.Y));

            // Use DriftWave for title text
            TextAnimator.DrawTextWithEffectOutlined(spriteBatch, font, name, namePos, _overlay.Global.Palette_BlueWhite, _overlay.Global.Palette_Black, TextEffectType.DriftWave, (float)gameTime.TotalGameTime.TotalSeconds);

            float currentY = infoPanelArea.Y + LAYOUT_VARS_START_Y;

            float leftLabelX = infoPanelArea.X + 8;
            float leftValueRightX = infoPanelArea.X + 51;

            float rightLabelX = infoPanelArea.X + 59;
            float rightValueRightX = infoPanelArea.X + 112;

            void DrawStatPair(string label, string value, float labelX, float valueRightX, float y, Color valColor, float xOffset = 0f)
            {
                // Center label vertically relative to value font
                float yOffset = (secondaryFont.LineHeight - tertiaryFont.LineHeight) / 2f;
                yOffset = MathF.Round(yOffset);

                spriteBatch.DrawStringSnapped(tertiaryFont, label, new Vector2(labelX, y + yOffset), _overlay.Global.Palette_Gray);

                float effectiveRightX = valueRightX + xOffset;

                if (value.EndsWith("%"))
                {
                    string numberPart = value.Substring(0, value.Length - 1);
                    string suffix = "%";

                    Vector2 numberSize = secondaryFont.MeasureString(numberPart);
                    Vector2 suffixSize = tertiaryFont.MeasureString(suffix);

                    // Draw Suffix (%)
                    Vector2 suffixPos = new Vector2(effectiveRightX - suffixSize.X, y + yOffset);
                    spriteBatch.DrawStringSnapped(tertiaryFont, suffix, suffixPos, _overlay.Global.Palette_Gray);

                    // Draw Number
                    Vector2 numberPos = new Vector2(suffixPos.X - numberSize.X - 1, y);
                    spriteBatch.DrawStringSnapped(secondaryFont, numberPart, numberPos, valColor);
                }
                else
                {
                    float valWidth = secondaryFont.MeasureString(value).Width;
                    spriteBatch.DrawStringSnapped(secondaryFont, value, new Vector2(effectiveRightX - valWidth, y), valColor);
                }
            }

            string powVal = move.Power > 0 ? move.Power.ToString() : (move.Effects.ContainsKey("ManaDamage") ? "???" : "---");
            string accVal = move.Accuracy >= 0 ? $"{move.Accuracy}%" : "---";
            string mpVal = (move.ManaCost > 0 ? move.ManaCost.ToString() : "0") + "%";

            var manaDump = move.Abilities.OfType<ManaDumpAbility>().FirstOrDefault();
            if (manaDump != null && _overlay.HoveredMemberIndex != -1 && _overlay.HoveredMemberIndex < _overlay.GameState.PlayerState.Party.Count)
            {
                var member = _overlay.GameState.PlayerState.Party[_overlay.HoveredMemberIndex];
                powVal = ((int)(member.CurrentMana * manaDump.Multiplier)).ToString();
                mpVal = member.CurrentMana.ToString() + "%";
            }

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

            DrawStatPair("POW", powVal, leftLabelX, leftValueRightX, currentY, _overlay.Global.Palette_White, 0f);

            float accOffset = accVal.Contains("%") ? 5f : 0f;
            DrawStatPair("ACC", accVal, rightLabelX, rightValueRightX, currentY, _overlay.Global.Palette_White, accOffset);

            currentY += LAYOUT_VAR_ROW_HEIGHT;

            DrawStatPair("MANA ", mpVal, leftLabelX, leftValueRightX, currentY, _overlay.Global.Palette_White, 5f); // Changed to White

            DrawStatPair("TGT", targetVal, rightLabelX, rightValueRightX, currentY, _overlay.Global.Palette_White, 0f);

            currentY += LAYOUT_VAR_ROW_HEIGHT;

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
                OffensiveStatType.Strength => _overlay.Global.StatColor_Strength,
                OffensiveStatType.Intelligence => _overlay.Global.StatColor_Intelligence,
                OffensiveStatType.Tenacity => _overlay.Global.StatColor_Tenacity,
                OffensiveStatType.Agility => _overlay.Global.StatColor_Agility,
                _ => _overlay.Global.Palette_White
            };

            string impactVal = move.ImpactType.ToString().ToUpper().Substring(0, Math.Min(4, move.ImpactType.ToString().Length));
            Color impactColor = move.ImpactType == ImpactType.Magical ? _overlay.Global.Palette_LightBlue : (move.ImpactType == ImpactType.Physical ? _overlay.Global.Palette_Orange : _overlay.Global.Palette_Gray);

            // Shift USE and TYP values left by 6
            DrawStatPair("USE", offStatVal, leftLabelX, leftValueRightX, currentY, offColor, 0f);
            DrawStatPair("TYP", impactVal, rightLabelX, rightValueRightX, currentY, impactColor, 0f);

            // Row 4: Contact
            currentY = infoPanelArea.Y + LAYOUT_CONTACT_Y;
            if (move.MakesContact)
            {
                string contactText = "[MAKES CONTACT]";
                Vector2 contactSize = tertiaryFont.MeasureString(contactText);
                Vector2 contactPos = new Vector2(infoPanelArea.X + (infoPanelArea.Width - contactSize.X) / 2f, currentY);
                spriteBatch.DrawStringSnapped(tertiaryFont, contactText, contactPos, _overlay.Global.Palette_Red);
            }

            string description = move.Description.ToUpper();
            if (!string.IsNullOrEmpty(description))
            {
                float descWidth = infoPanelArea.Width - (padding * 2);
                var descLines = ParseAndWrapRichText(secondaryFont, description, descWidth, _overlay.Global.Palette_White);

                int maxLines = 8;
                if (descLines.Count > maxLines)
                {
                    descLines = descLines.Take(maxLines).ToList();
                }

                float totalDescHeight = descLines.Count * secondaryFont.LineHeight;

                float bottomPadding = 3f;
                float areaTop = infoPanelArea.Y + LAYOUT_DESC_START_Y;
                float areaBottom = infoPanelArea.Bottom - bottomPadding;
                float areaHeight = areaBottom - areaTop;

                float startY = areaTop; // Fixed top alignment

                float lineY = startY;
                foreach (var line in descLines)
                {
                    float lineWidth = 0;
                    foreach (var segment in line)
                    {
                        if (string.IsNullOrWhiteSpace(segment.Text)) lineWidth += segment.Text.Length * 5;
                        else lineWidth += secondaryFont.MeasureString(segment.Text).Width;
                    }

                    float lineX = infoPanelArea.X + (infoPanelArea.Width - lineWidth) / 2;
                    float currentX = lineX;

                    foreach (var segment in line)
                    {
                        float segWidth;
                        if (string.IsNullOrWhiteSpace(segment.Text)) segWidth = segment.Text.Length * 5;
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

        private void DrawGenericItemInfoPanel(SpriteBatch spriteBatch, BitmapFont font, BitmapFont secondaryFont, string name, string description, Texture2D? iconTexture, Texture2D? iconSilhouette, Dictionary<string, int> stats, Rectangle infoPanelArea, GameTime gameTime)
        {
            const int spriteSize = 16;
            const int gap = 4;

            var statLines = GetStatModifierLines(stats);

            // Use Fixed Layout Y
            // Move DOWN 2px
            float currentY = infoPanelArea.Y + LAYOUT_SPRITE_Y + 2;

            int spriteX = infoPanelArea.X + (infoPanelArea.Width - spriteSize) / 2;

            float displayScale = 1.0f;
            Vector2 iconOrigin = new Vector2(8, 8);
            Vector2 drawPos = new Vector2(spriteX + 8, currentY + 8);

            // Apply Juicy Animation
            Vector2 animOffset = GetJuicyOffset(gameTime);
            drawPos += animOffset;

            if (iconSilhouette != null)
            {
                Color mainOutlineColor = _overlay.Global.ItemOutlineColor_Idle;
                Color cornerOutlineColor = _overlay.Global.ItemOutlineColor_Idle_Corner;

                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(-1, -1) * displayScale, null, cornerOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(1, -1) * displayScale, null, cornerOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(-1, 1) * displayScale, null, cornerOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(1, 1) * displayScale, null, cornerOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);

                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(-1, 0) * displayScale, null, mainOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(1, 0) * displayScale, null, mainOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(0, -1) * displayScale, null, mainOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(0, 1) * displayScale, null, mainOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
            }

            if (iconTexture != null)
            {
                spriteBatch.DrawSnapped(iconTexture, drawPos, null, Color.White, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
            }

            // --- 2. Draw Name (Animated) ---
            Vector2 nameSize = font.MeasureString(name);
            Vector2 namePos = new Vector2(
                infoPanelArea.X + (infoPanelArea.Width - nameSize.X) / 2f,
                infoPanelArea.Y + LAYOUT_TITLE_Y
            );
            namePos = new Vector2(MathF.Round(namePos.X), MathF.Round(namePos.Y));

            // Use DriftWave for title text
            TextAnimator.DrawTextWithEffectOutlined(spriteBatch, font, name, namePos, _overlay.Global.Palette_BlueWhite, _overlay.Global.Palette_Black, TextEffectType.DriftWave, (float)gameTime.TotalGameTime.TotalSeconds);

            // Draw Stats in the Variables Area
            currentY = infoPanelArea.Y + LAYOUT_VARS_START_Y;
            if (statLines.Positives.Any() || statLines.Negatives.Any())
            {
                float leftColX = infoPanelArea.X + 8;
                float rightColX = infoPanelArea.X + 64;

                int maxRows = Math.Max(statLines.Positives.Count, statLines.Negatives.Count);

                for (int i = 0; i < maxRows; i++)
                {
                    if (i < statLines.Positives.Count)
                    {
                        var lineParts = ParseAndWrapRichText(secondaryFont, statLines.Positives[i], infoPanelArea.Width / 2, _overlay.Global.Palette_White);
                        if (lineParts.Count > 0)
                        {
                            var segments = lineParts[0];
                            float currentX = leftColX;
                            foreach (var segment in segments)
                            {
                                float segWidth = string.IsNullOrWhiteSpace(segment.Text) ? segment.Text.Length * 5 : secondaryFont.MeasureString(segment.Text).Width;
                                spriteBatch.DrawStringSnapped(secondaryFont, segment.Text, new Vector2(currentX, currentY), segment.Color);
                                currentX += segWidth;
                            }
                        }
                    }

                    if (i < statLines.Negatives.Count)
                    {
                        var lineParts = ParseAndWrapRichText(secondaryFont, statLines.Negatives[i], infoPanelArea.Width / 2, _overlay.Global.Palette_White);
                        if (lineParts.Count > 0)
                        {
                            var segments = lineParts[0];
                            float currentX = rightColX;
                            foreach (var segment in segments)
                            {
                                float segWidth = string.IsNullOrWhiteSpace(segment.Text) ? segment.Text.Length * 5 : secondaryFont.MeasureString(segment.Text).Width;
                                spriteBatch.DrawStringSnapped(secondaryFont, segment.Text, new Vector2(currentX, currentY), segment.Color);
                                currentX += segWidth;
                            }
                        }
                    }
                    currentY += secondaryFont.LineHeight;
                }
            }

            // Draw Description in the Description Area
            currentY = infoPanelArea.Y + LAYOUT_DESC_START_Y;
            if (!string.IsNullOrEmpty(description))
            {
                float descWidth = infoPanelArea.Width - (gap * 2);
                var descLines = ParseAndWrapRichText(secondaryFont, description, descWidth, _overlay.Global.Palette_White);

                foreach (var line in descLines)
                {
                    float lineWidth = 0;
                    foreach (var segment in line)
                    {
                        if (string.IsNullOrWhiteSpace(segment.Text)) lineWidth += segment.Text.Length * 5; // SPACE_WIDTH
                        else lineWidth += secondaryFont.MeasureString(segment.Text).Width;
                    }

                    var lineX = infoPanelArea.X + (infoPanelArea.Width - lineWidth) / 2;
                    float currentX = lineX;

                    foreach (var segment in line)
                    {
                        float segWidth;
                        if (string.IsNullOrWhiteSpace(segment.Text)) segWidth = segment.Text.Length * 5; // SPACE_WIDTH
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
                    float partWidth = isWhitespace ? (part.Length * 5) : font.MeasureString(part).Width;

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
                            finalColor = Color.Lerp(_overlay.Global.Palette_DarkGray, currentColor, amount);
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
            string tag = colorName.ToLowerInvariant();

            if (tag == "cstr") return _overlay.Global.StatColor_Strength;
            if (tag == "cint") return _overlay.Global.StatColor_Intelligence;
            if (tag == "cten") return _overlay.Global.StatColor_Tenacity;
            if (tag == "cagi") return _overlay.Global.StatColor_Agility;

            if (tag == "cpositive") return _overlay.Global.ColorPositive;
            if (tag == "cnegative") return _overlay.Global.ColorNegative;
            if (tag == "ccrit") return _overlay.Global.ColorCrit;
            if (tag == "cimmune") return _overlay.Global.ColorImmune;
            if (tag == "cctm") return _overlay.Global.ColorConditionToMeet;
            if (tag == "cetc") return _overlay.Global.Palette_DarkGray;

            if (tag == "cfire") return _overlay.Global.ElementColors.GetValueOrDefault(2, Color.White);
            if (tag == "cwater") return _overlay.Global.ElementColors.GetValueOrDefault(3, Color.White);
            if (tag == "carcane") return _overlay.Global.ElementColors.GetValueOrDefault(4, Color.White);
            if (tag == "cearth") return _overlay.Global.ElementColors.GetValueOrDefault(5, Color.White);
            if (tag == "cmetal") return _overlay.Global.ElementColors.GetValueOrDefault(6, Color.White);
            if (tag == "ctoxic") return _overlay.Global.ElementColors.GetValueOrDefault(7, Color.White);
            if (tag == "cwind") return _overlay.Global.ElementColors.GetValueOrDefault(8, Color.White);
            if (tag == "cvoid") return _overlay.Global.ElementColors.GetValueOrDefault(9, Color.White);
            if (tag == "clight") return _overlay.Global.ElementColors.GetValueOrDefault(10, Color.White);
            if (tag == "celectric") return _overlay.Global.ElementColors.GetValueOrDefault(11, Color.White);
            if (tag == "cice") return _overlay.Global.ElementColors.GetValueOrDefault(12, Color.White);
            if (tag == "cnature") return _overlay.Global.ElementColors.GetValueOrDefault(13, Color.White);

            if (tag.StartsWith("c"))
            {
                string effectName = tag.Substring(1);
                if (effectName == "poison") return _overlay.Global.StatusEffectColors.GetValueOrDefault(StatusEffectType.Poison, Color.White);
                if (effectName == "stun") return _overlay.Global.StatusEffectColors.GetValueOrDefault(StatusEffectType.Stun, Color.White);
                if (effectName == "regen") return _overlay.Global.StatusEffectColors.GetValueOrDefault(StatusEffectType.Regen, Color.White);
                if (effectName == "dodging") return _overlay.Global.StatusEffectColors.GetValueOrDefault(StatusEffectType.Dodging, Color.White);
                if (effectName == "burn") return _overlay.Global.StatusEffectColors.GetValueOrDefault(StatusEffectType.Burn, Color.White);
                if (effectName == "frostbite") return _overlay.Global.StatusEffectColors.GetValueOrDefault(StatusEffectType.Frostbite, Color.White);
                if (effectName == "silence") return _overlay.Global.StatusEffectColors.GetValueOrDefault(StatusEffectType.Silence, Color.White);
            }

            switch (tag)
            {
                case "red": return _overlay.Global.Palette_Red;
                case "blue": return _overlay.Global.Palette_LightBlue;
                case "green": return _overlay.Global.Palette_LightGreen;
                case "yellow": return _overlay.Global.Palette_Yellow;
                case "orange": return _overlay.Global.Palette_Orange;
                case "purple": return _overlay.Global.Palette_LightPurple;
                case "pink": return _overlay.Global.Palette_Pink;
                case "gray": return _overlay.Global.Palette_Gray;
                case "white": return _overlay.Global.Palette_White;
                case "BlueWhite": return _overlay.Global.Palette_BlueWhite;
                case "darkgray": return _overlay.Global.Palette_DarkGray;
                default: return _overlay.Global.Palette_White;
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

                if (statName.Length < 3)
                {
                    statName += " ";
                }

                string line = $"{statName} {colorTag}{sign}{kvp.Value}[/]";

                if (kvp.Value > 0)
                {
                    positives.Add(line);
                }
                else
                {
                    negatives.Add(line);
                }
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
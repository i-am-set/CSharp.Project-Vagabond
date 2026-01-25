using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Linq;

namespace ProjectVagabond.UI
{
    public class InventoryDrawer
    {
        private readonly PartyStatusOverlay _overlay;
        private readonly ItemTooltipRenderer _tooltipRenderer;

        public InventoryDrawer(PartyStatusOverlay overlay)
        {
            _overlay = overlay;
            _tooltipRenderer = ServiceLocator.Get<ItemTooltipRenderer>();
        }

        public void DrawWorld(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            if (!_overlay.IsOpen) return;

            var inventoryPosition = new Vector2(0, 200);

            // Draw Header Border (Generic)
            spriteBatch.DrawSnapped(_overlay.SpriteManager.InventoryBorderHeader, inventoryPosition + _overlay.InventoryPositionOffset, Color.White);

            // Draw Main Border (Using Equip border as base since it fits the party view)
            spriteBatch.DrawSnapped(_overlay.SpriteManager.InventoryBorderEquip, inventoryPosition, Color.White);

            DrawPartyMemberSlots(spriteBatch, font, ServiceLocator.Get<Core>().SecondaryFont, gameTime);

            // Draw Tooltip if hovering something
            if (_overlay.HoveredItemData != null)
            {
                Texture2D? overlayTex = null;
                Rectangle infoPanelArea;

                const int statsPanelWidth = 116;
                const int statsPanelHeight = 132;
                int statsPanelY = (int)inventoryPosition.Y + 32 + 1 - 1; // Match slot area Y
                int statsPanelX;

                // Dynamic positioning based on which member is hovered
                if (_overlay.HoveredMemberIndex == 0 || _overlay.HoveredMemberIndex == 1)
                {
                    overlayTex = _overlay.SpriteManager.InventoryBorderEquipInfoPanelRight;
                    statsPanelX = 194 - 16;
                }
                else
                {
                    overlayTex = _overlay.SpriteManager.InventoryBorderEquipInfoPanelLeft;
                    statsPanelX = 10 + 16;
                }

                infoPanelArea = new Rectangle(statsPanelX, statsPanelY, statsPanelWidth, statsPanelHeight);

                if (overlayTex != null)
                {
                    spriteBatch.DrawSnapped(overlayTex, inventoryPosition, Color.White);
                }

                _tooltipRenderer.DrawInfoPanelContent(spriteBatch, _overlay.HoveredItemData, infoPanelArea, font, ServiceLocator.Get<Core>().SecondaryFont, gameTime, 1.0f);
            }

            // Debug Grid
            if (_overlay.Global.ShowSplitMapGrid)
            {
                var pixel = ServiceLocator.Get<Texture2D>();
                Color[] debugColors = { Color.Green, Color.Yellow, Color.Orange, Color.Red };
                for (int i = 0; i < 4; i++)
                {
                    spriteBatch.DrawSnapped(pixel, _overlay.PartyMemberPanelAreas[i], debugColors[i] * 0.5f);
                }
            }
        }

        public void DrawScreen(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            // Only draw the close button if the menu is open
            if (_overlay.IsOpen)
            {
                _overlay.CloseButton?.Draw(spriteBatch, font, gameTime, transform);
            }
        }

        private void DrawPartyMemberSlots(SpriteBatch spriteBatch, BitmapFont font, BitmapFont secondaryFont, GameTime gameTime)
        {
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

                if (isOccupied)
                {
                    TextAnimator.DrawTextWithEffect(spriteBatch, font, name, namePos, nameColor, TextEffectType.Drift, _overlay.PartySlotTextTimers[i]);
                }
                else
                {
                    spriteBatch.DrawStringSnapped(font, name, namePos, nameColor);
                }

                currentY += 32 + 2 - 6;

                // Health Bar
                Texture2D healthBarBg = isOccupied ? _overlay.SpriteManager.InventoryPlayerHealthBarEmpty : _overlay.SpriteManager.InventoryPlayerHealthBarDisabled;
                if (healthBarBg != null)
                {
                    int barX = centerX - (healthBarBg.Width / 2);
                    spriteBatch.DrawSnapped(healthBarBg, new Vector2(barX, currentY), Color.White);

                    if (isOccupied && _overlay.SpriteManager.InventoryPlayerHealthBarFull != null)
                    {
                        float hpPercent = (float)member!.CurrentHP / Math.Max(1, member.MaxHP);
                        int visibleWidth = (int)(_overlay.SpriteManager.InventoryPlayerHealthBarFull.Width * hpPercent);
                        var srcRect = new Rectangle(0, 0, visibleWidth, _overlay.SpriteManager.InventoryPlayerHealthBarFull.Height);
                        spriteBatch.DrawSnapped(_overlay.SpriteManager.InventoryPlayerHealthBarFull, new Vector2(barX + 1, currentY), srcRect, Color.White);
                    }

                    if (isOccupied)
                    {
                        string hpValText = $"{member!.CurrentHP}/{member.MaxHP}";
                        string hpSuffix = " HP";
                        Vector2 valSize = secondaryFont.MeasureString(hpValText);
                        Vector2 suffixSize = secondaryFont.MeasureString(hpSuffix);
                        float totalHpWidth = valSize.X + suffixSize.X;
                        float hpTextX = centerX - (totalHpWidth / 2f);
                        float hpTextY = currentY + 7;

                        spriteBatch.DrawStringSnapped(secondaryFont, hpValText, new Vector2(hpTextX, hpTextY), _overlay.Global.Palette_DarkSun);
                        spriteBatch.DrawStringSnapped(secondaryFont, hpSuffix, new Vector2(hpTextX + valSize.X, hpTextY), _overlay.Global.Palette_Shadow);
                    }
                    currentY += 8 + (int)secondaryFont.LineHeight + 4 - 3;
                }

                // Stats - Anchored to top of Spells
                string[] statLabels = { "STR", "INT", "TEN", "AGI" };
                string[] statKeys = { "Strength", "Intelligence", "Tenacity", "Agility" };
                int statBlockStartX = centerX - 30;

                // Calculate Y position for Stats based on bottom anchor
                int bottomPadding = 4;
                int spellButtonHeight = 8;
                int numSpells = 4;
                int totalSpellHeight = numSpells * spellButtonHeight;
                int spellStartY = bounds.Bottom - bottomPadding - totalSpellHeight;

                int statGap = 2;
                int statRowHeight = (int)secondaryFont.LineHeight + 1;
                int numStats = 4;
                int totalStatHeight = numStats * statRowHeight;

                // Start drawing stats above the spells
                int statCurrentY = spellStartY - statGap - totalStatHeight;

                for (int s = 0; s < 4; s++)
                {
                    int baseStat = 0;
                    int bonus = 0;

                    if (isOccupied)
                    {
                        baseStat = _overlay.GameState.PlayerState.GetBaseStat(member!, statKeys[s]);
                        // Calculate bonus from Global Relics
                        foreach (var relicId in _overlay.GameState.PlayerState.GlobalRelics)
                        {
                            if (BattleDataCache.Relics.TryGetValue(relicId, out var r))
                                if (r.StatModifiers.TryGetValue(statKeys[s], out int val)) bonus += val;
                        }
                    }

                    Color labelColor = isOccupied ? _overlay.Global.Palette_DarkSun : _overlay.Global.Palette_DarkShadow;
                    if (isOccupied)
                    {
                        spriteBatch.DrawStringSnapped(secondaryFont, statLabels[s], new Vector2(statBlockStartX, statCurrentY), labelColor);
                    }

                    Texture2D statBarBg = isOccupied ? _overlay.SpriteManager.InventoryStatBarEmpty : _overlay.SpriteManager.InventoryStatBarDisabled;
                    if (statBarBg != null)
                    {
                        float barX = statBlockStartX + 19;
                        float barYOffset = (s == 1 || s == 3) ? 0.5f : 0f;
                        float barY = statCurrentY + (secondaryFont.LineHeight - 3) / 2f + barYOffset;

                        spriteBatch.DrawSnapped(statBarBg, new Vector2(barX, barY), Color.White);

                        if (isOccupied && _overlay.SpriteManager.InventoryStatBarFull != null)
                        {
                            int basePoints = Math.Clamp(baseStat, 1, 20);
                            int totalPoints = Math.Clamp(baseStat + bonus, 1, 20);
                            int bonusPoints = totalPoints - basePoints;

                            if (basePoints > 0)
                            {
                                var srcBase = new Rectangle(0, 0, basePoints * 2, 3);
                                spriteBatch.DrawSnapped(_overlay.SpriteManager.InventoryStatBarFull, new Vector2(barX, barY), srcBase, Color.White);
                            }

                            if (bonusPoints > 0)
                            {
                                var srcBonus = new Rectangle(0, 0, bonusPoints * 2, 3);
                                spriteBatch.DrawSnapped(_overlay.SpriteManager.InventoryStatBarFull, new Vector2(barX + basePoints * 2, barY), srcBonus, _overlay.Global.StatColor_Increase);
                            }
                            else if (bonusPoints < 0)
                            {
                                // Penalty overwrites base
                                int penaltyWidth = Math.Abs(bonusPoints) * 2;
                                var srcPenalty = new Rectangle(0, 0, penaltyWidth, 3);
                                spriteBatch.DrawSnapped(_overlay.SpriteManager.InventoryStatBarFull, new Vector2(barX + totalPoints * 2, barY), srcPenalty, _overlay.Global.StatColor_Decrease);
                            }
                        }
                    }
                    statCurrentY += statRowHeight;
                }

                // Spells (Buttons already positioned by InputHandler)
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
    }
}
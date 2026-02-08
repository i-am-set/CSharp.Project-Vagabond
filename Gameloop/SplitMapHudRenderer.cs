using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Progression;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;

namespace ProjectVagabond.UI
{
    public class SplitMapHudRenderer
    {
        private readonly Global _global;
        private readonly SpriteManager _spriteManager;
        private readonly GameState _gameState;
        private readonly Texture2D _pixel;

        private const int HUD_HEIGHT = 98;
        private const int CARD_WIDTH = 78;
        private const int START_Y = Global.VIRTUAL_HEIGHT - HUD_HEIGHT;

        // --- Hover State ---
        // Stores: Bounds, Label Text, Card Center X
        private readonly List<(Rectangle Bounds, string Label, int CardCenterX)> _activeHitboxes = new();
        private float _hoverTimer;
        private (Rectangle Bounds, string Label, int CardCenterX)? _currentHoveredItem;
        private const float HOVER_DELAY = 0.2f;
        private Vector2 _lastMousePos;
        private bool _isTooltipVisible;

        public SplitMapHudRenderer()
        {
            _global = ServiceLocator.Get<Global>();
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _gameState = ServiceLocator.Get<GameState>();
            _pixel = ServiceLocator.Get<Texture2D>();
        }

        public void Update(GameTime gameTime, Vector2 virtualMousePos)
        {
            bool found = false;
            var cursorManager = ServiceLocator.Get<CursorManager>();

            foreach (var item in _activeHitboxes)
            {
                if (item.Bounds.Contains(virtualMousePos))
                {
                    // Set cursor to Hint when hovering a move
                    cursorManager.SetState(CursorState.Hint);

                    if (_currentHoveredItem.HasValue && _currentHoveredItem.Value.Bounds == item.Bounds)
                    {
                        // If already visible, keep it visible regardless of movement within the rect
                        if (!_isTooltipVisible)
                        {
                            // Only increment timer if mouse is still
                            if (virtualMousePos == _lastMousePos)
                            {
                                _hoverTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                                if (_hoverTimer >= HOVER_DELAY)
                                {
                                    _isTooltipVisible = true;
                                }
                            }
                            else
                            {
                                // Mouse moved within the rect, reset timer
                                _hoverTimer = 0f;
                            }
                        }
                    }
                    else
                    {
                        // New item hovered
                        _currentHoveredItem = item;
                        _hoverTimer = 0f;
                        _isTooltipVisible = false;
                    }
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                _currentHoveredItem = null;
                _hoverTimer = 0f;
                _isTooltipVisible = false;
            }

            _lastMousePos = virtualMousePos;
        }

        public void Draw(SpriteBatch spriteBatch, GameTime gameTime)
        {
            _activeHitboxes.Clear();

            // Fetch fonts here to ensure they are loaded
            var core = ServiceLocator.Get<Core>();
            var defaultFont = core.DefaultFont;
            var secondaryFont = core.SecondaryFont;
            var tertiaryFont = core.TertiaryFont;

            if (defaultFont == null || secondaryFont == null || tertiaryFont == null) return;

            // Draw Background
            spriteBatch.DrawSnapped(_pixel, new Rectangle(0, START_Y, Global.VIRTUAL_WIDTH, HUD_HEIGHT), _global.Palette_Black);

            // Draw Top Border
            spriteBatch.DrawSnapped(_pixel, new Rectangle(0, START_Y, Global.VIRTUAL_WIDTH, 1), _global.Palette_DarkestPale);

            var party = _gameState.PlayerState.Party;
            int count = party.Count;

            // Calculate total width of the active cards to center them
            int totalWidth = count * CARD_WIDTH;
            int startX = (Global.VIRTUAL_WIDTH - totalWidth) / 2;

            for (int i = 0; i < count; i++)
            {
                int x = startX + (i * CARD_WIDTH);
                DrawCard(spriteBatch, gameTime, party[i], x, i, defaultFont, secondaryFont, tertiaryFont);
            }

            // Draw Tooltip
            if (_currentHoveredItem.HasValue && _isTooltipVisible)
            {
                DrawTooltip(spriteBatch, _currentHoveredItem.Value.Bounds, _currentHoveredItem.Value.CardCenterX);
            }
        }

        private void DrawCard(SpriteBatch spriteBatch, GameTime gameTime, PartyMember member, int xPosition, int index, BitmapFont defaultFont, BitmapFont secondaryFont, BitmapFont tertiaryFont)
        {
            int y = START_Y + 6;
            int centerX = xPosition + (CARD_WIDTH / 2);

            // --- 1. Name ---
            string name = member.Name.ToUpper();
            Color nameColor = _global.Palette_LightPale;
            Vector2 nameSize = defaultFont.MeasureString(name);
            spriteBatch.DrawStringSnapped(defaultFont, name, new Vector2(centerX - nameSize.X / 2, y), nameColor);

            y += (int)nameSize.Y - 4;

            // --- 2. Portrait ---
            float time = (float)gameTime.TotalGameTime.TotalSeconds;

            // Bobbing (Animation State)
            // Add index-based phase offset to create the wave effect
            float bobSpeed = 3f;
            float wavePhase = index * 1.0f;
            float sineValue = MathF.Sin(time * bobSpeed + wavePhase);
            float bobOffset = sineValue * 0.5f;

            // Floating (Spatial Movement)
            // Use index to offset the phase so they don't all float in sync
            float seed = index * 13.5f;
            float floatSpeedX = 0.1f;
            float floatSpeedY = 0.1f;
            float floatAmp = 1.5f; // 1.5 pixels float radius

            float floatX = MathF.Sin(time * floatSpeedX + seed) * floatAmp;
            float floatY = MathF.Cos(time * floatSpeedY + seed) * floatAmp;

            // Rotation
            float rotSpeed = 0.2f;
            float rotAmp = 0.02f; // Radians
            float rotation = MathF.Sin(time * rotSpeed + seed) * rotAmp;

            // Use Alt sprite when on the "upper half" of the bob (negative offset in screen coords)
            // Use Normal sprite when on the "lower half"
            PlayerSpriteType type = sineValue < 0 ? PlayerSpriteType.Alt : PlayerSpriteType.Normal;

            var sourceRect = _spriteManager.GetPlayerSourceRect(member.PortraitIndex, type);

            // Center portrait (32x32)
            // Origin is center of sprite (16, 16)
            // Position is target center on screen
            Vector2 origin = new Vector2(16, 16);
            Vector2 pos = new Vector2(centerX, y + 16) + new Vector2(floatX, bobOffset + floatY);

            spriteBatch.DrawSnapped(_spriteManager.PlayerMasterSpriteSheet, pos, sourceRect, Color.White, rotation, origin, 1.0f, SpriteEffects.None, 0f);

            y += 32 + 4;

            // --- 3. HP Bar ---
            Texture2D hpBg = _spriteManager.InventoryPlayerHealthBarEmpty;
            if (hpBg != null)
            {
                int barX = centerX - (hpBg.Width / 2);

                // HP Counter Text: "Current/Max"
                // Positioned above the bar, left-aligned with the bar
                string hpText = $"{member.CurrentHP}/{member.MaxHP}";
                // (y - 8) is the top of the bar. We subtract LineHeight to place it above.
                // Added +1 to tuck it slightly closer to the bar.
                spriteBatch.DrawStringSnapped(tertiaryFont, hpText, new Vector2(barX, (y - 9) - tertiaryFont.LineHeight + 1), _global.Palette_DarkShadow);

                spriteBatch.DrawSnapped(hpBg, new Vector2(barX, y - 8), Color.White);

                if (_spriteManager.InventoryPlayerHealthBarFull != null)
                {
                    float hpPercent = (float)member.CurrentHP / Math.Max(1, member.MaxHP);
                    int visibleWidth = (int)(_spriteManager.InventoryPlayerHealthBarFull.Width * hpPercent);
                    var srcRect = new Rectangle(0, 0, visibleWidth, _spriteManager.InventoryPlayerHealthBarFull.Height);
                    spriteBatch.DrawSnapped(_spriteManager.InventoryPlayerHealthBarFull, new Vector2(barX + 1, y - 8), srcRect, Color.White);
                }
            }

            y += 0; // Height of bar + padding

            // --- 4. Stats (STR, INT, TEN, AGI) ---
            string[] labels = { "STR", "INT", "TEN", "AGI" };
            string[] keys = { "Strength", "Intelligence", "Tenacity", "Agility" };

            // Align stats: Label at (centerX - 30), Pips at (centerX - 30 + 19)
            int statBlockStartX = centerX - 30;

            for (int s = 0; s < 4; s++)
            {
                Color labelColor = _global.Palette_DarkPale;
                spriteBatch.DrawStringSnapped(secondaryFont, labels[s], new Vector2(statBlockStartX, y), labelColor);

                Texture2D statBg = _spriteManager.InventoryStatBarEmpty;
                if (statBg != null)
                {
                    int pipX = statBlockStartX + 19;
                    // Center vertically with font (LineHeight is approx 5-6, Bar is 3)
                    int pipY = y + (int)(secondaryFont.LineHeight / 2) - (statBg.Height / 2);

                    spriteBatch.DrawSnapped(statBg, new Vector2(pipX, pipY), Color.White);

                    if (_spriteManager.InventoryStatBarFull != null)
                    {
                        int val = _gameState.PlayerState.GetBaseStat(member, keys[s]);
                        int basePoints = Math.Clamp(val, 0, 10);
                        if (basePoints > 0)
                        {
                            var srcBase = new Rectangle(0, 0, basePoints * 4, 3);
                            spriteBatch.DrawSnapped(_spriteManager.InventoryStatBarFull, new Vector2(pipX, pipY), srcBase, Color.White);
                        }
                    }
                }
                y += (int)secondaryFont.LineHeight + 1;
            }

            y += 2; // Moved up 1 pixel (was 3)

            // --- 5. Moves ---
            // Move the entirety of moves over 6 pixels to the left
            int moveStartX = statBlockStartX - 6;

            DrawMoveName(spriteBatch, member.BasicMove, "bas", moveStartX, centerX, ref y, tertiaryFont, tertiaryFont);
            DrawMoveName(spriteBatch, member.CoreMove, "cor", moveStartX, centerX, ref y, tertiaryFont, tertiaryFont);
            DrawMoveName(spriteBatch, member.AltMove, "alt", moveStartX, centerX, ref y, tertiaryFont, tertiaryFont);
        }

        private void DrawMoveName(SpriteBatch sb, MoveEntry? move, string label, int x, int centerX, ref int y, BitmapFont labelFont, BitmapFont font)
        {
            string text = "EMPTY";
            Color color = _global.Palette_DarkShadow;
            bool isMovePresent = false;

            if (move != null)
            {
                if (BattleDataCache.Moves.TryGetValue(move.MoveID, out var data))
                {
                    text = data.MoveName;

                    if (label == "bas")
                        color = _global.Palette_DarkestPale;
                    else if (label == "alt")
                        color = _global.Palette_Pale;
                    else
                        color = _global.Palette_LightPale;

                    isMovePresent = true;
                }
            }

            // Calculate Hitbox
            // Width: Entire party member card (CARD_WIDTH = 78)
            // Height: LineHeight + 4 (2px taller than previous iteration)
            // Position: Start of card X, y - 2 (1px higher than previous)
            int cardStartX = centerX - (CARD_WIDTH / 2);
            int lineHeight = (int)font.LineHeight;
            Rectangle hitRect = new Rectangle(cardStartX, y - 2, CARD_WIDTH, lineHeight + 4);

            // Check Hover
            bool isHovered = _currentHoveredItem.HasValue && _currentHoveredItem.Value.Bounds == hitRect;

            // Draw Highlight (Hollow Sun Rectangle)
            if (isHovered)
            {
                Color c = _global.Palette_Sun;
                // Top
                sb.DrawSnapped(_pixel, new Rectangle(hitRect.X, hitRect.Y, hitRect.Width, 1), c);
                // Bottom
                sb.DrawSnapped(_pixel, new Rectangle(hitRect.X, hitRect.Bottom - 1, hitRect.Width, 1), c);
                // Left
                sb.DrawSnapped(_pixel, new Rectangle(hitRect.X, hitRect.Y, 1, hitRect.Height), c);
                // Right
                sb.DrawSnapped(_pixel, new Rectangle(hitRect.Right - 1, hitRect.Y, 1, hitRect.Height), c);
            }

            if (isMovePresent)
            {
                // Draw Label (bas/cor/alt)
                sb.DrawStringSnapped(labelFont, label, new Vector2(x, y), _global.Palette_DarkShadow);

                // Calculate Centering
                // Center text between the end of the label and the end of the card
                float labelWidth = labelFont.MeasureString(label).Width;
                float labelEndX = x + labelWidth;
                int cardEndX = cardStartX + CARD_WIDTH;

                float availableWidth = cardEndX - labelEndX;
                float textWidth = font.MeasureString(text).Width;

                // Floor to snap to pixel
                float textX = MathF.Floor(labelEndX + (availableWidth - textWidth) / 2f);

                // Draw Move Name
                sb.DrawStringSnapped(font, text, new Vector2(textX, y), color);
            }
            else
            {
                // Center "EMPTY"
                Vector2 size = font.MeasureString(text);
                sb.DrawStringSnapped(font, text, new Vector2(centerX - size.X / 2, y), color);
            }

            _activeHitboxes.Add((hitRect, text, centerX));

            // Increment Y: Height (no gap)
            // Height is lineHeight + 4.
            // Next Top = hitRect.Bottom.
            // Next Y = Next Top + 2 (since Top is Y-2).
            // Next Y = hitRect.Bottom + 1 = (y - 2 + lineHeight + 4) + 1 = y + lineHeight + 3.
            // Reduced gap by 1 pixel (was +4)
            y += lineHeight + 3;
        }

        private void DrawTooltip(SpriteBatch sb, Rectangle targetRect, int cardCenterX)
        {
            int width = 86;
            int height = 64;

            // Centered horizontally with cardCenterX
            int x = cardCenterX - (width / 2);
            // Bottom aligned with Top of targetRect
            int y = targetRect.Top - height;

            // Draw Black Background
            sb.DrawSnapped(_pixel, new Rectangle(x, y, width, height), Color.Black);

            // Draw White Border (1px)
            sb.DrawSnapped(_pixel, new Rectangle(x, y, width, 1), Color.White); // Top
            sb.DrawSnapped(_pixel, new Rectangle(x, y + height - 1, width, 1), Color.White); // Bottom
            sb.DrawSnapped(_pixel, new Rectangle(x, y, 1, height), Color.White); // Left
            sb.DrawSnapped(_pixel, new Rectangle(x + width - 1, y, 1, height), Color.White); // Right

            // Draw Text "TOOLTIP"
            var core = ServiceLocator.Get<Core>();
            var font = core.TertiaryFont;
            string text = "TOOLTIP";
            Vector2 size = font.MeasureString(text);
            Vector2 pos = new Vector2(x + (width - size.X) / 2, y + (height - size.Y) / 2);

            sb.DrawStringSnapped(font, text, pos, Color.White);
        }
    }
}
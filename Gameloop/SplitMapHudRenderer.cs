using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Progression;
using ProjectVagabond.Utils;
using System;

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

        public SplitMapHudRenderer()
        {
            _global = ServiceLocator.Get<Global>();
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _gameState = ServiceLocator.Get<GameState>();
            _pixel = ServiceLocator.Get<Texture2D>();
        }

        public void Draw(SpriteBatch spriteBatch, GameTime gameTime)
        {
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
                DrawCard(spriteBatch, gameTime, party[i], x, defaultFont, secondaryFont, tertiaryFont);
            }
        }

        private void DrawCard(SpriteBatch spriteBatch, GameTime gameTime, PartyMember member, int xPosition, BitmapFont defaultFont, BitmapFont secondaryFont, BitmapFont tertiaryFont)
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
            float bobSpeed = 4f;
            float time = (float)gameTime.TotalGameTime.TotalSeconds;

            // Bobbing up and down smoothly 0.5 pixels
            float sineValue = MathF.Sin(time * bobSpeed);
            float bobOffset = sineValue * 0.5f;

            // Use Alt sprite when on the "upper half" of the bob (negative offset in screen coords)
            // Use Normal sprite when on the "lower half"
            PlayerSpriteType type = sineValue < 0 ? PlayerSpriteType.Alt : PlayerSpriteType.Normal;

            var sourceRect = _spriteManager.GetPlayerSourceRect(member.PortraitIndex, type);
            // Center portrait (32x32)
            spriteBatch.DrawSnapped(_spriteManager.PlayerMasterSpriteSheet, new Vector2(centerX - 16, y + bobOffset), sourceRect, Color.White);

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

            y += 4;

            // --- 5. Moves ---
            // Move the entirety of moves over 6 pixels to the left
            int moveStartX = statBlockStartX - 6;

            DrawMoveName(spriteBatch, member.CoreMove, "cor", moveStartX, centerX, ref y, tertiaryFont);
            DrawMoveName(spriteBatch, member.AltMove, "alt", moveStartX, centerX, ref y, tertiaryFont);
        }

        private void DrawMoveName(SpriteBatch sb, MoveEntry? move, string label, int x, int centerX, ref int y, BitmapFont font)
        {
            string text = "EMPTY";
            Color color = _global.Palette_DarkShadow;
            bool isMovePresent = false;

            if (move != null)
            {
                if (BattleDataCache.Moves.TryGetValue(move.MoveID, out var data))
                {
                    text = data.MoveName;
                    color = _global.Palette_LightPale;
                    isMovePresent = true;
                }
            }

            if (isMovePresent)
            {
                // Draw Label (cor/alt)
                sb.DrawStringSnapped(font, label, new Vector2(x, y), _global.Palette_DarkestPale);
                // Draw Move Name (offset 12)
                sb.DrawStringSnapped(font, text, new Vector2(x + 12, y), color);
            }
            else
            {
                // Center "EMPTY"
                Vector2 size = font.MeasureString(text);
                sb.DrawStringSnapped(font, text, new Vector2(centerX - size.X / 2, y), color);
            }

            y += (int)font.LineHeight + 1;
        }
    }
}
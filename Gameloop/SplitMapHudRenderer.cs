using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
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
        private const int TOTAL_WIDTH = CARD_WIDTH * 4;
        private const int START_X = (Global.VIRTUAL_WIDTH - TOTAL_WIDTH) / 2;
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
            // Fetch fonts here to ensure they are loaded (Core.LoadContent runs after Scene initialization)
            var core = ServiceLocator.Get<Core>();
            var defaultFont = core.DefaultFont;
            var secondaryFont = core.SecondaryFont;

            if (defaultFont == null || secondaryFont == null) return;

            // Draw Background
            spriteBatch.DrawSnapped(_pixel, new Rectangle(0, START_Y, Global.VIRTUAL_WIDTH, HUD_HEIGHT), _global.Palette_Black);

            // Draw Top Border
            spriteBatch.DrawSnapped(_pixel, new Rectangle(0, START_Y, Global.VIRTUAL_WIDTH, 1), _global.Palette_DarkGray);

            for (int i = 0; i < 4; i++)
            {
                DrawCard(spriteBatch, gameTime, i, defaultFont, secondaryFont);
            }
        }

        private void DrawCard(SpriteBatch spriteBatch, GameTime gameTime, int index, BitmapFont defaultFont, BitmapFont secondaryFont)
        {
            int x = START_X + (index * CARD_WIDTH);
            int y = START_Y + 6;
            int centerX = x + (CARD_WIDTH / 2);

            var party = _gameState.PlayerState.Party;
            bool isOccupied = index < party.Count;
            var member = isOccupied ? party[index] : null;

            // --- 1. Name ---
            string name = isOccupied ? member.Name.ToUpper() : "EMPTY";
            Color nameColor = isOccupied ? _global.Palette_Sun : _global.Palette_DarkShadow;
            Vector2 nameSize = defaultFont.MeasureString(name);
            spriteBatch.DrawStringSnapped(defaultFont, name, new Vector2(centerX - nameSize.X / 2, y), nameColor);

            y += (int)nameSize.Y - 4;

            // --- 2. Portrait ---
            if (isOccupied)
            {
                float animSpeed = 1f;
                int frame = (int)(gameTime.TotalGameTime.TotalSeconds * animSpeed) % 2;
                PlayerSpriteType type = frame == 0 ? PlayerSpriteType.Normal : PlayerSpriteType.Alt;

                var sourceRect = _spriteManager.GetPlayerSourceRect(member.PortraitIndex, type);
                // Center portrait (32x32)
                spriteBatch.DrawSnapped(_spriteManager.PlayerMasterSpriteSheet, new Vector2(centerX - 16, y), sourceRect, Color.White);
            }

            y += 32 + 4;

            // --- 3. HP Bar ---
            Texture2D hpBg = isOccupied ? _spriteManager.InventoryPlayerHealthBarEmpty : _spriteManager.InventoryPlayerHealthBarDisabled;
            if (hpBg != null)
            {
                int barX = centerX - (hpBg.Width / 2);
                spriteBatch.DrawSnapped(hpBg, new Vector2(barX, y - 8), Color.White);

                if (isOccupied && _spriteManager.InventoryPlayerHealthBarFull != null)
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
                Color labelColor = isOccupied ? _global.Palette_DarkSun : _global.Palette_DarkShadow;
                spriteBatch.DrawStringSnapped(secondaryFont, labels[s], new Vector2(statBlockStartX, y), labelColor);

                Texture2D statBg = isOccupied ? _spriteManager.InventoryStatBarEmpty : _spriteManager.InventoryStatBarDisabled;
                if (statBg != null)
                {
                    int pipX = statBlockStartX + 19;
                    // Center vertically with font (LineHeight is approx 5-6, Bar is 3)
                    int pipY = y + (int)(secondaryFont.LineHeight / 2) - (statBg.Height / 2);

                    spriteBatch.DrawSnapped(statBg, new Vector2(pipX, pipY), Color.White);

                    if (isOccupied && _spriteManager.InventoryStatBarFull != null)
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
            if (isOccupied)
            {
                DrawMoveName(spriteBatch, member.CoreMove, centerX, ref y, false, secondaryFont);
                DrawMoveName(spriteBatch, member.AltMove, centerX, ref y, false, secondaryFont);
            }
            else
            {
                DrawMoveName(spriteBatch, null, centerX, ref y, true, secondaryFont);
                DrawMoveName(spriteBatch, null, centerX, ref y, true, secondaryFont);
            }
        }

        private void DrawMoveName(SpriteBatch sb, MoveEntry move, int centerX, ref int y, bool forceEmpty, BitmapFont font)
        {
            string text = "EMPTY";
            Color color = _global.Palette_DarkShadow;

            if (!forceEmpty && move != null)
            {
                if (BattleDataCache.Moves.TryGetValue(move.MoveID, out var data))
                {
                    text = data.MoveName;
                    color = _global.Palette_LightGray;
                }
            }

            Vector2 size = font.MeasureString(text);
            sb.DrawStringSnapped(font, text, new Vector2(centerX - size.X / 2, y), color);
            y += (int)font.LineHeight + 1;
        }
    }
}
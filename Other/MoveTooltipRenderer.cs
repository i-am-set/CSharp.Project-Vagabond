using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;

namespace ProjectVagabond.UI
{
    public class MoveTooltipRenderer
    {
        private readonly Global _global;
        private readonly Texture2D _pixel;
        private readonly Core _core;

        public const int WIDTH = 140;
        public const int HEIGHT = 72;

        public MoveTooltipRenderer()
        {
            _global = ServiceLocator.Get<Global>();
            _pixel = ServiceLocator.Get<Texture2D>();
            _core = ServiceLocator.Get<Core>();
        }

        public void DrawFixed(SpriteBatch sb, Vector2 position, CompiledMove move, BattleCombatant actor)
        {
            DrawContainerAndContent(sb, position, move, actor);
        }

        public void DrawFloating(SpriteBatch sb, GameTime gameTime, Rectangle targetRect, int cardCenterX, CompiledMove move, BattleCombatant actor)
        {
            float x = cardCenterX - (WIDTH / 2f);
            float minX = 2f;
            float maxX = Global.VIRTUAL_WIDTH - WIDTH - 2f;

            if (x < minX) x = minX;
            if (x > maxX) x = maxX;

            float y = targetRect.Top - HEIGHT - 4;

            DrawContainerAndContent(sb, new Vector2(x, y), move, actor);
        }

        private void DrawContainerAndContent(SpriteBatch sb, Vector2 pos, CompiledMove move, BattleCombatant actor)
        {
            Vector2 size = new Vector2(WIDTH, HEIGHT);

            DrawBeveledBackground(sb, pos - new Vector2(1, 1), size + new Vector2(2, 2), _global.Palette_DarkestPale);
            DrawBeveledBackground(sb, pos, size, _global.Palette_Black);

            DrawTextContent(sb, pos, move, actor);
        }

        private void DrawTextContent(SpriteBatch sb, Vector2 boxPos, CompiledMove move, BattleCombatant actor)
        {
            var secondaryFont = _core.SecondaryFont;
            var tertiaryFont = _core.TertiaryFont;

            float currentY = boxPos.Y + 2;

            string name = move.BaseTemplate.MoveName != null ? move.BaseTemplate.MoveName.ToUpper() : "UNKNOWN";
            Vector2 nameSize = secondaryFont.MeasureString(name);
            float centeredNameX = boxPos.X + (WIDTH - nameSize.X) / 2f;
            sb.DrawStringSnapped(secondaryFont, name, new Vector2(centeredNameX, currentY), _global.Palette_Sun);

            currentY += secondaryFont.LineHeight + 4;

            Vector2 line1Size = tertiaryFont.MeasureString(move.CachedTooltipStatsLine1);
            float line1X = boxPos.X + (WIDTH - line1Size.X) / 2f;
            sb.DrawStringSnapped(tertiaryFont, move.CachedTooltipStatsLine1, new Vector2(line1X, currentY), _global.Palette_LightPale);
            currentY += tertiaryFont.LineHeight + 2;

            Vector2 line2Size = tertiaryFont.MeasureString(move.CachedTooltipStatsLine2);
            float line2X = boxPos.X + (WIDTH - line2Size.X) / 2f;
            sb.DrawStringSnapped(tertiaryFont, move.CachedTooltipStatsLine2, new Vector2(line2X, currentY), _global.Palette_LightPale);
            currentY += tertiaryFont.LineHeight + 4;

            for (int i = 0; i < move.CachedTokenLines.Count; i++)
            {
                string tokenLine = move.CachedTokenLines[i];
                Vector2 tokenSize = tertiaryFont.MeasureString(tokenLine);
                float tokenX = boxPos.X + (WIDTH - tokenSize.X) / 2f;
                sb.DrawStringSnapped(tertiaryFont, tokenLine, new Vector2(tokenX, currentY), _global.Palette_DarkPale);
                currentY += tertiaryFont.LineHeight + 2;
            }
        }

        private void DrawBeveledBackground(SpriteBatch sb, Vector2 pos, Vector2 size, Color color)
        {
            sb.DrawSnapped(_pixel, new Vector2(pos.X + 1, pos.Y), new Rectangle(0, 0, (int)size.X - 2, 1), color);
            sb.DrawSnapped(_pixel, new Vector2(pos.X + 1, pos.Y + size.Y - 1), new Rectangle(0, 0, (int)size.X - 2, 1), color);
            sb.DrawSnapped(_pixel, new Vector2(pos.X, pos.Y + 1), new Rectangle(0, 0, (int)size.X, (int)size.Y - 2), color);
        }
    }
}
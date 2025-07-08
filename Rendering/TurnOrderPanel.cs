using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using System.Collections.Generic;

namespace ProjectVagabond
{
    /// <summary>
    /// A UI panel responsible for drawing the initiative list during combat.
    /// </summary>
    public class TurnOrderPanel
    {
        /// <summary>
        /// Draws the turn order display if the game is in combat.
        /// </summary>
        /// <param name="spriteBatch">The SpriteBatch to draw with.</param>
        public void Draw(SpriteBatch spriteBatch)
        {
            var gameState = Core.CurrentGameState;
            if (!gameState.IsInCombat)
            {
                return;
            }

            var font = Global.Instance.DefaultFont;
            if (font == null) return;

            var initiativeOrder = gameState.InitiativeOrder;
            if (initiativeOrder == null || initiativeOrder.Count == 0)
            {
                return;
            }

            // --- 1. Generate Unique Display Names ---
            var displayNames = EntityNamer.GetUniqueNames(initiativeOrder);

            // --- 2. Draw the List ---
            int panelWidth = 200;
            int panelX = (Global.VIRTUAL_WIDTH - panelWidth) / 2;
            int panelY = 10;
            int lineHeight = font.LineHeight + 4;
            int padding = 5;

            // Draw background for the entire list
            var backgroundRect = new Rectangle(
                panelX,
                panelY,
                panelWidth,
                (initiativeOrder.Count * lineHeight) + (padding * 2)
            );
            spriteBatch.Draw(Core.Pixel, backgroundRect, Global.Instance.TerminalBg * 0.8f);


            for (int i = 0; i < initiativeOrder.Count; i++)
            {
                int entityId = initiativeOrder[i];
                string name = displayNames[entityId];
                Vector2 nameSize = font.MeasureString(name);

                // Determine color
                Color nameColor = (entityId == gameState.PlayerEntityId) ? Color.Yellow : Color.LightGray;

                // Calculate position
                float textX = panelX + (panelWidth - nameSize.X) / 2; // Center text horizontally
                float textY = panelY + padding + (i * lineHeight);
                var textPosition = new Vector2(textX, textY);

                // Highlight the current turn
                if (entityId == gameState.CurrentTurnEntityId)
                {
                    var highlightRect = new Rectangle(
                        panelX,
                        (int)textY - 2,
                        panelWidth,
                        lineHeight
                    );
                    spriteBatch.Draw(Core.Pixel, highlightRect, Global.Instance.Palette_Red * 0.5f);
                }

                // Draw the name
                spriteBatch.DrawString(font, name, textPosition, nameColor);
            }
        }
    }
}
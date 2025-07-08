using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond
{
    /// <summary>
    /// A UI panel that displays interactive menus for player actions during combat.
    /// </summary>
    public class ActionMenuPanel
    {
        private readonly Rectangle _bounds;
        private readonly Dictionary<string, Rectangle> _menuOptionBounds = new Dictionary<string, Rectangle>();
        private const int PADDING = 10;
        private const int BORDER_THICKNESS = 2;
        private const int LINE_SPACING = 18;

        public ActionMenuPanel(Rectangle bounds)
        {
            _bounds = bounds;
        }

        /// <summary>
        /// Draws the action menu panel, changing its content based on the current UI state.
        /// </summary>
        public void Draw(SpriteBatch spriteBatch)
        {
            var gameState = Core.CurrentGameState;
            if (!gameState.IsInCombat) return;

            _menuOptionBounds.Clear();

            // Draw border and background
            var borderRect = new Rectangle(
                _bounds.X - BORDER_THICKNESS,
                _bounds.Y - BORDER_THICKNESS,
                _bounds.Width + (BORDER_THICKNESS * 2),
                _bounds.Height + (BORDER_THICKNESS * 2)
            );
            spriteBatch.Draw(Core.Pixel, borderRect, Global.Instance.Palette_White);
            spriteBatch.Draw(Core.Pixel, _bounds, Global.Instance.TerminalBg);

            // Draw content based on state
            switch (gameState.UIState)
            {
                case CombatUIState.Default:
                    DrawDefaultMenu(spriteBatch);
                    break;
                case CombatUIState.SelectAttack:
                    DrawAttackMenu(spriteBatch);
                    break;
                case CombatUIState.SelectSkill:
                    DrawSkillMenu(spriteBatch);
                    break;
                case CombatUIState.SelectTarget:
                    DrawInstruction(spriteBatch, "Select a target...");
                    break;
                case CombatUIState.SelectMove:
                    DrawInstruction(spriteBatch, "Select a destination...");
                    break;
                case CombatUIState.Busy:
                    // Draw nothing or a "Busy" indicator
                    break;
            }
        }

        private void DrawDefaultMenu(SpriteBatch spriteBatch)
        {
            var font = Global.Instance.DefaultFont;
            var options = new List<string> { "Attack", "Skills", "Move", "Item", "End Turn" };
            for (int i = 0; i < options.Count; i++)
            {
                var position = new Vector2(_bounds.X + PADDING, _bounds.Y + PADDING + (i * LINE_SPACING));
                spriteBatch.DrawString(font, options[i], position, Global.Instance.GameTextColor);

                var bounds = new Rectangle((int)position.X, (int)position.Y, (int)font.MeasureString(options[i]).Width, font.LineHeight);
                _menuOptionBounds[options[i]] = bounds;
            }
        }

        private void DrawAttackMenu(SpriteBatch spriteBatch)
        {
            var font = Global.Instance.DefaultFont;
            var gameState = Core.CurrentGameState;
            var attacksComp = Core.ComponentStore.GetComponent<AvailableAttacksComponent>(gameState.PlayerEntityId);
            var combatStats = Core.ComponentStore.GetComponent<CombatStatsComponent>(gameState.PlayerEntityId);

            if (attacksComp == null || combatStats == null) return;

            for (int i = 0; i < attacksComp.Attacks.Count; i++)
            {
                var attack = attacksComp.Attacks[i];
                bool canAfford = combatStats.ActionPoints >= attack.ActionPointCost;
                Color textColor = canAfford ? Global.Instance.GameTextColor : Color.DarkGray;

                string text = $"{attack.Name} (Cost: {attack.ActionPointCost} AP)";
                var position = new Vector2(_bounds.X + PADDING, _bounds.Y + PADDING + (i * LINE_SPACING));
                spriteBatch.DrawString(font, text, position, textColor);

                var bounds = new Rectangle((int)position.X, (int)position.Y, (int)font.MeasureString(text).Width, font.LineHeight);
                _menuOptionBounds[attack.Name] = bounds;
            }
        }

        private void DrawSkillMenu(SpriteBatch spriteBatch)
        {
            var font = Global.Instance.DefaultFont;
            var options = new List<string> { "Block", "Power Strike" }; // Placeholder skills
            for (int i = 0; i < options.Count; i++)
            {
                var position = new Vector2(_bounds.X + PADDING, _bounds.Y + PADDING + (i * LINE_SPACING));
                spriteBatch.DrawString(font, options[i], position, Color.DarkGray); // Grayed out as they are not implemented

                var bounds = new Rectangle((int)position.X, (int)position.Y, (int)font.MeasureString(options[i]).Width, font.LineHeight);
                _menuOptionBounds[options[i]] = bounds;
            }
        }

        private void DrawInstruction(SpriteBatch spriteBatch, string text)
        {
            var font = Global.Instance.DefaultFont;
            var position = new Vector2(_bounds.X + PADDING, _bounds.Y + PADDING);
            spriteBatch.DrawString(font, text, position, Color.Yellow);
        }

        /// <summary>
        /// Handles mouse input for the action menu.
        /// </summary>
        /// <param name="mousePosition">The position of the mouse cursor.</param>
        /// <returns>The name of the action/button clicked, or null if none.</returns>
        public string HandleInput(Point mousePosition)
        {
            foreach (var option in _menuOptionBounds)
            {
                if (option.Value.Contains(mousePosition))
                {
                    // Check for affordability if in the attack selection menu
                    var gameState = Core.CurrentGameState;
                    if (gameState.UIState == CombatUIState.SelectAttack)
                    {
                        var attacksComp = Core.ComponentStore.GetComponent<AvailableAttacksComponent>(gameState.PlayerEntityId);
                        var combatStats = Core.ComponentStore.GetComponent<CombatStatsComponent>(gameState.PlayerEntityId);
                        var attack = attacksComp?.Attacks.FirstOrDefault(a => a.Name == option.Key);

                        if (attack != null && combatStats.ActionPoints < attack.ActionPointCost)
                        {
                            CombatLog.Log($"[warning]Not enough AP for {attack.Name}.");
                            return null; // Clicked an unaffordable attack, do nothing.
                        }
                    }
                    return option.Key;
                }
            }
            return null;
        }
    }
}
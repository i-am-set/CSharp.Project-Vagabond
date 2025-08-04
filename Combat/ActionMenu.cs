using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Combat.UI
{
    /// <summary>
    /// Renders a horizontal list of selectable action tiles for one hand.
    /// </summary>
    public class ActionMenu
    {
        private readonly HandType _handType;
        private readonly Global _global;
        private List<ActionData> _actions = new List<ActionData>();

        // --- TUNING CONSTANTS ---
        private const int MENU_Y_POS = 450;
        private const int TILE_WIDTH = 100;
        private const int TILE_HEIGHT = 40;
        private const int TILE_PADDING = 10;
        private static readonly Point TILE_SIZE = new Point(TILE_WIDTH, TILE_HEIGHT);
        private static readonly Color TILE_COLOR = new Color(40, 40, 60, 200);
        private static readonly Color TEXT_COLOR = Color.White;
        private static readonly Color HIGHLIGHT_COLOR = Color.Yellow;
        private static readonly Color FOCUSED_COLOR = Color.CornflowerBlue;
        private const float ANIMATION_DURATION = 0.4f;

        private Rectangle _menuArea;
        private List<Rectangle> _tileBounds = new List<Rectangle>();

        // Animation state
        private float _currentYOffset;
        private float _targetYOffset;
        private float _startYOffset;
        private float _animationTimer;
        private bool _isAnimating;
        private bool _isHidden = true;

        public IReadOnlyList<Rectangle> TileBounds => _tileBounds;
        public IReadOnlyList<ActionData> Actions => _actions;

        public ActionMenu(HandType handType)
        {
            _handType = handType;
            _global = ServiceLocator.Get<Global>();
            _currentYOffset = TILE_HEIGHT + 20; // Start off-screen
            _targetYOffset = _currentYOffset;
        }

        /// <summary>
        /// Sets the list of actions to be displayed in this menu.
        /// </summary>
        public void SetActions(IEnumerable<ActionData> actions)
        {
            _actions = actions.ToList();
            CalculateLayout();
        }

        /// <summary>
        /// Called when the combat scene is entered to trigger the intro animation.
        /// </summary>
        public void EnterScene()
        {
            _isHidden = true;
            _currentYOffset = TILE_HEIGHT + 20;
            _targetYOffset = _currentYOffset;
        }

        private void CalculateLayout()
        {
            _tileBounds.Clear();
            int totalWidth = _actions.Count * (TILE_WIDTH + TILE_PADDING) - TILE_PADDING;
            int startX;

            if (_handType == HandType.Left)
            {
                // Align to the left of the screen's center
                startX = (Global.VIRTUAL_WIDTH / 2) - totalWidth - 20;
            }
            else // Right Hand
            {
                // Align to the right of the screen's center
                startX = (Global.VIRTUAL_WIDTH / 2) + 20;
            }

            _menuArea = new Rectangle(startX, MENU_Y_POS, totalWidth, TILE_HEIGHT);

            for (int i = 0; i < _actions.Count; i++)
            {
                int x = startX + i * (TILE_WIDTH + TILE_PADDING);
                _tileBounds.Add(new Rectangle(x, MENU_Y_POS, TILE_WIDTH, TILE_HEIGHT));
            }
        }

        private void SetVisibility(bool visible)
        {
            bool shouldBeHidden = !visible;
            if (shouldBeHidden == _isHidden) return; // No change needed

            _isHidden = shouldBeHidden;
            _startYOffset = _currentYOffset;
            _targetYOffset = _isHidden ? TILE_HEIGHT + 20 : 0;
            _animationTimer = 0f;
            _isAnimating = true;
        }

        /// <summary>
        /// Updates the menu's animation state based on the combat manager.
        /// </summary>
        public void Update(GameTime gameTime, CombatManager combatManager)
        {
            // Determine if the menu should be visible
            bool isHandSelected = (_handType == HandType.Left)
                ? !string.IsNullOrEmpty(combatManager.LeftHand.SelectedActionId)
                : !string.IsNullOrEmpty(combatManager.RightHand.SelectedActionId);

            bool shouldBeVisible = !isHandSelected && combatManager.CurrentState == PlayerTurnState.Selecting;
            SetVisibility(shouldBeVisible);

            // Process animation
            if (_isAnimating)
            {
                _animationTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                float progress = Math.Min(1f, _animationTimer / ANIMATION_DURATION);
                float easedProgress = Easing.EaseOutCubic(progress);

                _currentYOffset = MathHelper.Lerp(_startYOffset, _targetYOffset, easedProgress);

                if (progress >= 1f)
                {
                    _isAnimating = false;
                }
            }
        }

        /// <summary>
        /// Draws the action menu at its current animated position.
        /// </summary>
        public void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, int selectedIndex, bool isFocused)
        {
            var pixel = ServiceLocator.Get<Texture2D>();

            for (int i = 0; i < _actions.Count; i++)
            {
                var action = _actions[i];
                var baseBounds = _tileBounds[i];
                var animatedBounds = new Rectangle(baseBounds.X, baseBounds.Y + (int)_currentYOffset, baseBounds.Width, baseBounds.Height);

                // Draw tile background
                spriteBatch.Draw(pixel, animatedBounds, TILE_COLOR);

                // Draw highlight/focus border
                if (i == selectedIndex)
                {
                    Color borderColor = isFocused ? FOCUSED_COLOR : HIGHLIGHT_COLOR;
                    DrawBorder(spriteBatch, pixel, animatedBounds, 2, borderColor);
                }

                // Draw action name
                Vector2 textSize = font.MeasureString(action.Name);
                Vector2 textPosition = new Vector2(
                    animatedBounds.X + (animatedBounds.Width - textSize.X) / 2,
                    animatedBounds.Y + (animatedBounds.Height - textSize.Y) / 2
                );
                spriteBatch.DrawString(font, action.Name, textPosition, TEXT_COLOR);
            }
        }

        private void DrawBorder(SpriteBatch spriteBatch, Texture2D texture, Rectangle rectangle, int thickness, Color color)
        {
            // Top
            spriteBatch.Draw(texture, new Rectangle(rectangle.X, rectangle.Y, rectangle.Width, thickness), color);
            // Bottom
            spriteBatch.Draw(texture, new Rectangle(rectangle.X, rectangle.Y + rectangle.Height - thickness, rectangle.Width, thickness), color);
            // Left
            spriteBatch.Draw(texture, new Rectangle(rectangle.X, rectangle.Y, thickness, rectangle.Height), color);
            // Right
            spriteBatch.Draw(texture, new Rectangle(rectangle.X + rectangle.Width - thickness, rectangle.Y, thickness, rectangle.Height), color);
        }
    }
}
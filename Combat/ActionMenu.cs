using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using MonoGame.Extended.BitmapFonts;
using MonoGame.Extended.Graphics; // CRITICAL: This using directive is required for the SpriteBatch.Draw(RectangleF) extension method.
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Combat.UI
{
    /// <summary>
    /// Renders a horizontal list of selectable action cards for one hand.
    /// </summary>
    public class ActionMenu
    {
        private readonly HandType _handType;
        private List<CombatCard> _cards = new List<CombatCard>();

        // --- TUNING CONSTANTS ---
        public static readonly Point CARD_SIZE = new Point(80, 112);
        private const int MENU_Y_POS = 400;
        private const int MENU_X_PADDING = 60; // Horizontal padding from screen edge
        private const int CARD_SPACING = -25; // Negative for overlap
        private const float SPREAD_AMOUNT = 20f; // How far cards move apart when one is hovered
        private const float CARD_TILT_RADIANS = 0.15f; // Tilt angle for unselected cards
        private const float WIGGLE_SPEED = 8f; // Speed of the hovered border shimmer
        private const float WIGGLE_ROTATION_RADIANS = 0.02f; // Rotational intensity of the border shimmer

        // State-based appearance
        public const float UNFOCUSED_SCALE = 0.8f;
        public const float DEFAULT_SCALE = 0.8f; // Size of non-hovered cards in a focused menu
        public const float HOVERED_SCALE = 1.0f; // Size of the hovered card
        public static readonly Color UNFOCUSED_TINT = new Color(150, 150, 150);
        public static readonly Color FOCUSED_TINT = Color.White;

        // Placeholder card visuals
        public static readonly Color CARD_IMAGE_AREA_COLOR = new Color(50, 50, 80);
        public static readonly Color CARD_TEXT_BG_COLOR = new Color(30, 30, 45);
        public static readonly Color TEXT_COLOR = Color.White;
        public static readonly Color BORDER_COLOR = Color.White;

        private Vector2 _menuCenterPosition;
        public Rectangle ActivationArea { get; private set; }

        public IReadOnlyList<CombatCard> Cards => _cards;

        // Animation state
        private int _hoveredIndex = -1;
        private float _wiggleTimer = 0f;

        public ActionMenu(HandType handType)
        {
            _handType = handType;
            CalculateLayout();
        }

        /// <summary>
        /// Sets the list of actions to be displayed in this menu.
        /// </summary>
        public void SetActions(IEnumerable<ActionData> actions)
        {
            // Take the first 3 actions for the hand display
            _cards = actions.Take(3).Select(a => new CombatCard(a)).ToList();
            CalculateLayout();
        }



        /// <summary>
        /// Called when the combat scene is entered to set initial positions.
        /// </summary>
        public void EnterScene()
        {
            foreach (var card in _cards)
            {
                // This method will be called in Update, so we just need to ensure state is clean.
            }
        }

        private void CalculateLayout()
        {
            // Calculate the total width of the menu when all cards are at their default, non-hovered size.
            // This provides a stable anchor point for the menu's center.
            int totalWidth = (int)(_cards.Count * (CARD_SIZE.X * DEFAULT_SCALE) + Math.Max(0, _cards.Count - 1) * CARD_SPACING);
            int startX;

            if (_handType == HandType.Left)
            {
                startX = MENU_X_PADDING;
                _menuCenterPosition = new Vector2(startX + totalWidth / 2f, MENU_Y_POS);
            }
            else // Right Hand
            {
                startX = Global.VIRTUAL_WIDTH - MENU_X_PADDING - totalWidth;
                _menuCenterPosition = new Vector2(startX + totalWidth / 2f, MENU_Y_POS);
            }

            // Define a larger area for mouse activation
            ActivationArea = new Rectangle(startX - 20, MENU_Y_POS - 40, totalWidth + 40, CARD_SIZE.Y + 80);
        }

        /// <summary>
        /// Updates the menu's animation state based on the combat manager and input.
        /// </summary>
        public void Update(GameTime gameTime, CombatInputHandler inputHandler)
        {
            _wiggleTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;

            bool isMenuFocused = inputHandler.FocusedHand == _handType;
            bool isMouseInArea = ActivationArea.Contains(inputHandler.VirtualMousePosition);
            _hoveredIndex = isMenuFocused ? inputHandler.GetSelectedIndexForHand(_handType) : -1;

            // This width is based on the original card size and is used to space out the cards' animation targets.
            float totalTargetWidth = _cards.Count * (CARD_SIZE.X + CARD_SPACING) - CARD_SPACING;
            float startX = _menuCenterPosition.X - totalTargetWidth / 2f;

            for (int i = 0; i < _cards.Count; i++)
            {
                var card = _cards[i];
                bool isHovered = i == _hoveredIndex;

                // Determine Target State
                float targetScale = DEFAULT_SCALE;
                Color targetTint = FOCUSED_TINT;

                if (!isMenuFocused && !isMouseInArea)
                {
                    targetScale = UNFOCUSED_SCALE;
                    targetTint = UNFOCUSED_TINT;
                }

                if (isHovered)
                {
                    targetScale = HOVERED_SCALE;
                }

                // Determine Target Position
                float baseCardX = startX + i * (CARD_SIZE.X + CARD_SPACING);
                float xOffset = 0;

                if (_hoveredIndex != -1)
                {
                    if (i < _hoveredIndex) xOffset = -SPREAD_AMOUNT;
                    if (i > _hoveredIndex) xOffset = SPREAD_AMOUNT;
                }

                float targetX = baseCardX + xOffset - (CARD_SIZE.X * targetScale - CARD_SIZE.X) / 2f;
                float targetY = MENU_Y_POS - (CARD_SIZE.Y * targetScale - CARD_SIZE.Y) / 2f;

                // Determine Target Rotation
                float targetRotation = 0f;
                if (!isHovered)
                {
                    // If a different card is hovered, tilt away from it.
                    if (_hoveredIndex != -1)
                    {
                        if (i < _hoveredIndex) targetRotation = -CARD_TILT_RADIANS;
                        else if (i > _hoveredIndex) targetRotation = CARD_TILT_RADIANS;
                    }
                    else // If no card is hovered, tilt based on position from center.
                    {
                        float middleIndex = (_cards.Count - 1) / 2.0f;
                        if (i < middleIndex) targetRotation = -CARD_TILT_RADIANS;
                        else if (i > middleIndex) targetRotation = CARD_TILT_RADIANS;
                    }
                }

                card.AnimateTo(new Vector2(targetX, targetY), targetScale, targetTint, targetRotation);
                card.Update(gameTime);
            }
        }

        /// <summary>
        /// Draws the action menu at its current animated position.
        /// This method uses origin-based drawing to support rotation.
        /// </summary>
        public void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            var pixel = ServiceLocator.Get<Texture2D>();

            for (int i = 0; i < _cards.Count; i++)
            {
                var card = _cards[i];
                bool isHovered = i == _hoveredIndex;

                // --- Common Properties for Drawing ---
                var cardRotation = card.CurrentRotation;
                var cardScale = card.CurrentScale;
                var cardDrawPosition = card.CurrentBounds.Center;

                // Apply rotational wiggle to the hovered card
                if (isHovered)
                {
                    float wiggle = (float)Math.Sin(_wiggleTimer * WIGGLE_SPEED) * WIGGLE_ROTATION_RADIANS;
                    cardRotation += wiggle;
                }

                // 1. Draw Border using lines for a perfect, rotatable outline
                float borderThickness = isHovered ? 3f : 2f;
                Color borderColor = BORDER_COLOR * (card.CurrentTint.A / 255f);

                // Calculate the four corners of the card relative to its origin (0,0)
                var halfSize = CARD_SIZE.ToVector2() / 2f;
                var corners = new Vector2[4]
                {
                    new Vector2(-halfSize.X, -halfSize.Y), // Top-Left
                    new Vector2( halfSize.X, -halfSize.Y), // Top-Right
                    new Vector2( halfSize.X,  halfSize.Y), // Bottom-Right
                    new Vector2(-halfSize.X,  halfSize.Y)  // Bottom-Left
                };

                // Create the transformation matrix using the potentially wiggled rotation
                var transform = Matrix.CreateScale(cardScale)
                              * Matrix.CreateRotationZ(cardRotation)
                              * Matrix.CreateTranslation(cardDrawPosition.X, cardDrawPosition.Y, 0);

                // Transform all corners
                for (int j = 0; j < corners.Length; j++)
                {
                    corners[j] = Vector2.Transform(corners[j], transform);
                }

                // Draw the lines connecting the transformed corners
                spriteBatch.DrawLine(corners[0], corners[1], borderColor, borderThickness); // Top
                spriteBatch.DrawLine(corners[1], corners[2], borderColor, borderThickness); // Right
                spriteBatch.DrawLine(corners[2], corners[3], borderColor, borderThickness); // Bottom
                spriteBatch.DrawLine(corners[3], corners[0], borderColor, borderThickness); // Left


                // 2. Draw Card Background
                var cardBaseOrigin = CARD_SIZE.ToVector2() / 2f;
                spriteBatch.Draw(pixel, cardDrawPosition, null, card.CurrentTint, cardRotation, cardBaseOrigin, cardScale, SpriteEffects.None, 0f);

                // 3. Draw placeholder image area
                var imageAreaColor = new Color(CARD_IMAGE_AREA_COLOR.ToVector3() * card.CurrentTint.ToVector3());
                var imageRect = new RectangleF(0, 0, CARD_SIZE.X, CARD_SIZE.Y * (2 / 3f));
                Vector2 imageAreaOffset = new Vector2(0, -CARD_SIZE.Y * (1 / 6f)); // Offset from card center to image area center
                Vector2 rotatedImageOffset = Vector2.Transform(imageAreaOffset * cardScale, Matrix.CreateRotationZ(cardRotation));
                Vector2 imageAreaDrawPos = cardDrawPosition + rotatedImageOffset;

                // The origin for this sub-element is its own center.
                var imageAreaOrigin = new Vector2(imageRect.Width / 2f, imageRect.Height / 2f);
                spriteBatch.Draw(pixel, imageAreaDrawPos, new Rectangle(0, 0, (int)imageRect.Width, (int)imageRect.Height), imageAreaColor, cardRotation, imageAreaOrigin, cardScale, SpriteEffects.None, 0f);

                // 4. Draw text background area
                var textBgColor = new Color(CARD_TEXT_BG_COLOR.ToVector3() * card.CurrentTint.ToVector3());
                var textBgRect = new RectangleF(0, 0, CARD_SIZE.X, CARD_SIZE.Y * (1 / 3f));
                Vector2 textBgAreaOffset = new Vector2(0, CARD_SIZE.Y * (1 / 3f)); // Offset from card center to text area center
                Vector2 rotatedTextBgOffset = Vector2.Transform(textBgAreaOffset * cardScale, Matrix.CreateRotationZ(cardRotation));
                Vector2 textBgAreaDrawPos = cardDrawPosition + rotatedTextBgOffset;

                // The origin for this sub-element is its own center.
                var textBgAreaOrigin = new Vector2(textBgRect.Width / 2f, textBgRect.Height / 2f);
                spriteBatch.Draw(pixel, textBgAreaDrawPos, new Rectangle(0, 0, (int)textBgRect.Width, (int)textBgRect.Height), textBgColor, cardRotation, textBgAreaOrigin, cardScale, SpriteEffects.None, 0f);

                // 5. Draw action name
                var textColor = new Color(TEXT_COLOR.ToVector3() * card.CurrentTint.ToVector3());
                float textDrawScale = card.CurrentScale;
                Vector2 textSize = font.MeasureString(card.Action.Name);

                spriteBatch.DrawString(font, card.Action.Name, textBgAreaDrawPos, textColor, cardRotation, textSize / 2f, textDrawScale, SpriteEffects.None, 0f);
            }
        }
    }
}
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
        public static readonly Point CARD_SIZE = new Point(120, 168);
        private const int MENU_BASE_Y_POS = 250;
        private const int MENU_X_PADDING = 20; // Horizontal padding from screen edge
        private const int CARD_SPACING = -25; // Negative for overlap
        private const float SPREAD_AMOUNT = 30f; // How far cards move apart when one is hovered
        private const float HOVER_Y_OFFSET = -40f; // How far the card moves up when hovered
        private const float SELECTED_Y_OFFSET = 80f; // How far the selected hand's menu moves down
        private const float OFFSCREEN_Y_OFFSET = 300f; // How far menus move down when both hands are selected
        private const float CARD_TILT_RADIANS = 0.15f; // Tilt angle for unselected cards
        private const float WIGGLE_SPEED = 4f; // Speed of the hovered border shimmer
        private const float WIGGLE_ROTATION_RADIANS = 0.02f; // Rotational intensity of the border shimmer
        private const float FOCUSED_Y_OFFSET = 0f; // Y position offset when this menu is focused
        private const float UNFOCUSED_Y_OFFSET = 60f; // Y position offset when this menu is NOT focused
        private const float CARD_ARCH_AMOUNT = 10f; // How much higher the middle card is than the outer cards

        // State-based appearance
        public const float UNFOCUSED_SCALE = 0.8f;
        public const float DEFAULT_SCALE = 0.8f; // Size of non-hovered cards in a focused menu
        public const float HOVERED_SCALE = 1.0f; // Size of the hovered card
        public static readonly Color UNFOCUSED_TINT = new Color(150, 150, 150);
        public static readonly Color FOCUSED_TINT = Color.White;
        private const float UNFOCUSED_ALPHA = 0.2f;
        private const float DEFAULT_ALPHA = 0.8f;
        private const float HOVERED_ALPHA = 0.95f;


        // Placeholder card visuals
        public static readonly Color CARD_IMAGE_AREA_COLOR = new Color(50, 50, 80);
        public static readonly Color CARD_TEXT_BG_COLOR = new Color(30, 30, 45);
        public static readonly Color TEXT_COLOR = Color.White;
        public static readonly Color BORDER_COLOR = Color.White;

        private Vector2 _menuCenterPosition;

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
            var core = ServiceLocator.Get<Core>();
            Rectangle actualScreenVirtualBounds = core.GetActualScreenVirtualBounds();

            // Calculate the total width of the menu when all cards are at their default, non-hovered size.
            int totalWidth = (int)(_cards.Count * (CARD_SIZE.X * DEFAULT_SCALE) + Math.Max(0, _cards.Count - 1) * CARD_SPACING);
            float menuX;

            if (_handType == HandType.Left)
            {
                // Anchor to the left edge of the actual screen's virtual bounds
                menuX = actualScreenVirtualBounds.X + MENU_X_PADDING;
            }
            else // Right Hand
            {
                // Anchor to the right edge of the actual screen's virtual bounds
                menuX = actualScreenVirtualBounds.Right - MENU_X_PADDING - totalWidth;
            }
            _menuCenterPosition = new Vector2(menuX + totalWidth / 2f, MENU_BASE_Y_POS);
        }

        /// <summary>
        /// Updates the menu's animation state based on the combat manager and input.
        /// </summary>
        public void Update(GameTime gameTime, CombatInputHandler inputHandler, CombatManager combatManager)
        {
            _wiggleTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;

            bool isMenuFocused = inputHandler.FocusedHand == _handType;
            _hoveredIndex = isMenuFocused ? inputHandler.GetSelectedIndexForHand(_handType) : -1;

            // This width is based on the original card size and is used to space out the cards' animation targets.
            float totalTargetWidth = _cards.Count * (CARD_SIZE.X + CARD_SPACING) - CARD_SPACING;
            float startX = _menuCenterPosition.X - totalTargetWidth / 2f;

            bool isThisHandSelected = (_handType == HandType.Left && !string.IsNullOrEmpty(combatManager.LeftHand.SelectedActionId))
                                   || (_handType == HandType.Right && !string.IsNullOrEmpty(combatManager.RightHand.SelectedActionId));

            for (int i = 0; i < _cards.Count; i++)
            {
                var card = _cards[i];
                bool isHovered = i == _hoveredIndex;

                // Determine Target State
                float targetScale = DEFAULT_SCALE;
                Color targetTint = FOCUSED_TINT;
                float targetAlpha = DEFAULT_ALPHA;

                if (!isMenuFocused && combatManager.CurrentState == PlayerTurnState.Selecting)
                {
                    targetScale = UNFOCUSED_SCALE;
                    targetTint = UNFOCUSED_TINT;
                    targetAlpha = UNFOCUSED_ALPHA;
                }

                if (isHovered)
                {
                    targetScale = HOVERED_SCALE;
                    targetAlpha = HOVERED_ALPHA;
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

                // --- Y-Position Logic based on Combat State ---
                float menuYOffset = isMenuFocused ? FOCUSED_Y_OFFSET : UNFOCUSED_Y_OFFSET;
                float baseY = MENU_BASE_Y_POS + menuYOffset;

                if (combatManager.CurrentState == PlayerTurnState.Confirming)
                {
                    baseY += OFFSCREEN_Y_OFFSET;
                }
                else if (isThisHandSelected && combatManager.CurrentState == PlayerTurnState.Selecting)
                {
                    baseY += SELECTED_Y_OFFSET;
                }

                // Calculate arch offset to create a fan shape
                float archYOffset = 0f;
                if (_cards.Count > 1)
                {
                    float middleIndex = (_cards.Count - 1) / 2.0f;
                    // This creates a curve where the middle card is highest. We subtract from Y to move it up.
                    archYOffset = -((1f - Math.Abs(i - middleIndex) / middleIndex) * CARD_ARCH_AMOUNT);
                }

                float targetY = baseY + archYOffset - (CARD_SIZE.Y * targetScale - CARD_SIZE.Y) / 2f;

                if (isHovered)
                {
                    targetY += HOVER_Y_OFFSET;
                }

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

                card.AnimateTo(new Vector2(targetX, targetY), targetScale, targetTint, targetRotation, targetAlpha);
                card.Update(gameTime);
            }
        }

        /// <summary>
        /// Draws the action menu using a two-pass system to ensure the hovered card is on top.
        /// </summary>
        public void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            var pixel = ServiceLocator.Get<Texture2D>();
            CombatCard hoveredCard = null;

            // First pass: Draw all non-hovered cards
            for (int i = 0; i < _cards.Count; i++)
            {
                if (i != _hoveredIndex)
                {
                    DrawCard(spriteBatch, font, gameTime, pixel, _cards[i], false);
                }
                else
                {
                    hoveredCard = _cards[i];
                }
            }

            // Second pass: Draw the hovered card on top
            if (hoveredCard != null)
            {
                DrawCard(spriteBatch, font, gameTime, pixel, hoveredCard, true);
            }
        }

        /// <summary>
        /// Helper method to draw a single card with all its visual elements.
        /// </summary>
        private void DrawCard(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Texture2D pixel, CombatCard card, bool isHovered)
        {
            // --- Common Properties for Drawing ---
            var cardRotation = card.CurrentRotation;
            var cardScale = card.CurrentScale;
            var cardDrawPosition = card.CurrentBounds.Center;
            var pixelOrigin = new Vector2(0.5f); // Origin for a 1x1 texture is its center

            // Apply rotational wiggle to the hovered card
            if (isHovered)
            {
                float wiggle = (float)Math.Sin(_wiggleTimer * WIGGLE_SPEED) * WIGGLE_ROTATION_RADIANS;
                cardRotation += wiggle;
            }

            // 1. Draw FULL Card Background
            var fullCardBgColor = new Color(CARD_TEXT_BG_COLOR.ToVector3() * card.CurrentTint.ToVector3()) * card.CurrentAlpha;
            spriteBatch.Draw(pixel, cardDrawPosition, null, fullCardBgColor, cardRotation, pixelOrigin, CARD_SIZE.ToVector2() * cardScale, SpriteEffects.None, 0f);

            // 2. Draw placeholder image area (on top of the new background)
            var imageAreaColor = new Color(CARD_IMAGE_AREA_COLOR.ToVector3() * card.CurrentTint.ToVector3()) * card.CurrentAlpha;
            var imageRectSize = new Vector2(CARD_SIZE.X, CARD_SIZE.Y * (2 / 3f));

            // Calculate the center position of the image area, accounting for card scale and rotation.
            Vector2 imageAreaOffset = new Vector2(0, -CARD_SIZE.Y * (1 / 6f)); // Offset from card center to image area center
            Vector2 rotatedImageOffset = Vector2.Transform(imageAreaOffset * cardScale, Matrix.CreateRotationZ(cardRotation));
            Vector2 imageAreaCenterPos = cardDrawPosition + rotatedImageOffset;

            spriteBatch.Draw(pixel, imageAreaCenterPos, null, imageAreaColor, cardRotation, pixelOrigin, imageRectSize * cardScale, SpriteEffects.None, 0f);

            // 3. Draw Border using lines for a perfect, rotatable outline
            float borderThickness = isHovered ? 3f : 2f;
            Color borderColor = BORDER_COLOR * card.CurrentAlpha;

            var halfSize = CARD_SIZE.ToVector2() / 2f;
            var corners = new Vector2[4]
            {
                new Vector2(-halfSize.X, -halfSize.Y), // Top-Left
                new Vector2( halfSize.X, -halfSize.Y), // Top-Right
                new Vector2( halfSize.X,  halfSize.Y), // Bottom-Right
                new Vector2(-halfSize.X,  halfSize.Y)  // Bottom-Left
            };

            var transform = Matrix.CreateScale(cardScale)
                          * Matrix.CreateRotationZ(cardRotation)
                          * Matrix.CreateTranslation(cardDrawPosition.X, cardDrawPosition.Y, 0);

            for (int j = 0; j < corners.Length; j++)
            {
                corners[j] = Vector2.Transform(corners[j], transform);
            }

            spriteBatch.DrawLine(corners[0], corners[1], borderColor, borderThickness); // Top
            spriteBatch.DrawLine(corners[1], corners[2], borderColor, borderThickness); // Right
            spriteBatch.DrawLine(corners[2], corners[3], borderColor, borderThickness); // Bottom
            spriteBatch.DrawLine(corners[3], corners[0], borderColor, borderThickness); // Left

            // 4. Draw action name
            var textColor = new Color(TEXT_COLOR.ToVector3() * card.CurrentTint.ToVector3()) * card.CurrentAlpha;
            float textDrawScale = card.CurrentScale;
            Vector2 textSize = font.MeasureString(card.Action.Name);

            // Position the text in the lower third of the card
            Vector2 textBgAreaOffset = new Vector2(0, CARD_SIZE.Y * (1 / 3f)); // Offset from card center to text area center
            Vector2 rotatedTextBgOffset = Vector2.Transform(textBgAreaOffset * cardScale, Matrix.CreateRotationZ(cardRotation));
            Vector2 textDrawPosition = cardDrawPosition + rotatedTextBgOffset;

            spriteBatch.DrawString(font, card.Action.Name, textDrawPosition, textColor, cardRotation, textSize / 2f, textDrawScale, SpriteEffects.None, 0f);
        }
    }
}
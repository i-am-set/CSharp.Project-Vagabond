using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MonoGame.Extended.BitmapFonts;
using MonoGame.Extended.Graphics;
using ProjectVagabond.Combat;
using ProjectVagabond.Combat.UI;
using ProjectVagabond.Scenes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Combat.UI
{
    /// <summary>
    /// Renders a single, centralized hand of selectable action cards.
    /// Manages its own state for appearing and hiding from the bottom of the screen.
    /// </summary>
    public class ActionHandUI
    {
        private List<CombatCard> _cards = new List<CombatCard>();

        // --- TUNING CONSTANTS ---
        public static readonly Point CARD_SIZE = new Point(120, 168);
        private const int MENU_BOTTOM_PADDING = 20; // Distance from the bottom of the visible screen.
        private const int CARD_SPACING = -45; // Negative for overlap
        private const float SPREAD_AMOUNT = 40f; // How far cards move apart when one is hovered
        private const float HOVER_Y_OFFSET = -40f; // How far the card moves up when hovered
        private const float CARD_TILT_RADIANS = 0.1f; // Tilt angle for unselected cards
        private const float CARD_ARCH_AMOUNT = 10f; // How much lower the outer cards are than the center card.
        private const float HAND_Y_ANCHOR_OFFSET = -10f; // Vertical offset for the entire hand anchor. Negative moves it up.
        private const float HAND_ANIMATION_DURATION = 0.2f;
        private const float HOVER_BOUNDS_Y_EXTENSION = 50f; // How many extra pixels to add to the bottom of the hover hitbox.

        // State-based Y positions
        private const float HIDDEN_Y_OFFSET = 250f;
        private const float PEEKING_Y_OFFSET = 110f; // Raised slightly for better default visibility
        private const float ACTIVE_Y_OFFSET = 0f;

        // Appearance
        public const float DEFAULT_SCALE = 0.9f;
        public const float HOVERED_SCALE = 1.0f;
        public const float HELD_SCALE = 1.1f; // Slightly larger than hover to show click feedback
        public static readonly Color DEFAULT_TINT = Color.White;
        private const float DEFAULT_ALPHA = 1f;
        private const float HOVERED_ALPHA = 1f;

        // Shadow properties for different states
        private const float HOVERED_SHADOW_ALPHA = 0.2f; // Shadow opacity when hovered normally
        private const float HELD_SHADOW_ALPHA = 0.3f; // Shadow opacity when held (clicked but not yet dragged)
        private const float SHADOW_SCALE_MULTIPLIER = 1.05f; // How much larger the shadow is than the card.
        private const float SHADOW_BASE_VERTICAL_OFFSET = 8f; // Base downward offset for the shadow.
        private const float SHADOW_HORIZONTAL_SHIFT_FACTOR = 0.05f; // How much the shadow shifts horizontally based on card position. 0 = no shift.
        private const float SHADOW_VERTICAL_SHIFT_FACTOR = 0.1f; // How much the shadow moves down as the card moves up.
        private const int SHADOW_BLUR_LAYERS = 5; // Number of layers for the blur effect.
        private const float SHADOW_BLUR_SPREAD = 0.01f; // How far each blur layer extends.


        // Placeholder card visuals
        public static readonly Color CARD_IMAGE_AREA_COLOR = new Color(50, 50, 80);
        public static readonly Color CARD_TEXT_BG_COLOR = new Color(30, 30, 45);
        public static readonly Color TEXT_COLOR = Color.White;
        public static readonly Color BORDER_COLOR = Color.White;

        public IReadOnlyList<CombatCard> Cards => _cards;

        // Animation state
        private int _hoveredIndex = -1;
        private float _menuYOffset;
        private float _targetMenuYOffset;
        private float _startYOffset;
        private float _yAnimationTimer;
        private bool _isYAnimating;

        public ActionHandUI() { }

        /// <summary>
        /// Sets the list of actions to be displayed in this menu.
        /// </summary>
        public void SetActions(IEnumerable<ActionData> actions)
        {
            _cards = actions.Select(a => new CombatCard(a)).ToList();
        }

        /// <summary>
        /// Removes a card from the hand display based on its action ID.
        /// </summary>
        /// <param name="actionId">The ID of the action associated with the card to remove.</param>
        public void RemoveCard(string actionId)
        {
            _cards.RemoveAll(c => c.Action.Id.Equals(actionId, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Adds a new card to the hand display, optionally animating it from a starting position.
        /// </summary>
        /// <param name="actionData">The data for the action to add as a card.</param>
        /// <param name="startPosition">Optional world position to animate the card from.</param>
        public void AddCard(ActionData actionData, Vector2? startPosition = null)
        {
            if (actionData == null || _cards.Any(c => c.Action.Id.Equals(actionData.Id, StringComparison.OrdinalIgnoreCase)))
            {
                return; // Do not add null cards or duplicates
            }

            var newCard = new CombatCard(actionData);
            if (startPosition.HasValue)
            {
                // Start the card at the source position, transparent and slightly larger, ready to animate in.
                newCard.SetInitialState(startPosition.Value - (ActionHandUI.CARD_SIZE.ToVector2() * 1.1f / 2f), 1.1f, Color.White, 0f, 0f);
            }

            _cards.Add(newCard);
            // Sort the hand to maintain a consistent order when cards are returned.
            _cards = _cards.OrderBy(c => c.Action.Name).ToList();
        }


        /// <summary>
        /// Called when the combat scene is entered to set initial positions.
        /// </summary>
        public void EnterScene()
        {
            _menuYOffset = HIDDEN_Y_OFFSET;
            _targetMenuYOffset = PEEKING_Y_OFFSET;
            _startYOffset = _menuYOffset;
            _yAnimationTimer = 0f;
            _isYAnimating = true;
        }

        /// <summary>
        /// Updates the menu's animation state based on the combat manager and input.
        /// </summary>
        public void Update(GameTime gameTime, CombatInputHandler inputHandler, CombatManager combatManager)
        {
            var core = ServiceLocator.Get<Core>();
            Rectangle actualScreenVirtualBounds = core.GetActualScreenVirtualBounds();

            // Declare layout variables once in a shared scope.
            float screenCenterX = actualScreenVirtualBounds.X + actualScreenVirtualBounds.Width / 2f;
            float cardCenterDistance = (CARD_SIZE.X * DEFAULT_SCALE) + CARD_SPACING;

            // --- State Determination ---
            bool isMouseInActiveZone;
            if (_cards.Any())
            {
                // Calculate the dynamic activation zone based on where the cards will be in their 'Active' state.
                float totalCardSpan = (_cards.Count - 1) * cardCenterDistance + (CARD_SIZE.X * HOVERED_SCALE);
                float activationWidth = totalCardSpan + (SPREAD_AMOUNT * 2); // Add spread for when a card is hovered.
                float activationX = screenCenterX - (activationWidth / 2f);

                float menuBaseCenterY_Active = actualScreenVirtualBounds.Bottom - (CARD_SIZE.Y * DEFAULT_SCALE / 2f) + ACTIVE_Y_OFFSET + HAND_Y_ANCHOR_OFFSET;

                // The highest point is the top of a hovered middle card.
                float activationTopY = menuBaseCenterY_Active + HOVER_Y_OFFSET - (CARD_SIZE.Y * HOVERED_SCALE / 2f);

                // The lowest point is now the bottom of the visible screen area to prevent jittering.
                float activationBottomY = actualScreenVirtualBounds.Bottom;

                float activationHeight = activationBottomY - activationTopY;

                var activationZone = new RectangleF(activationX, activationTopY, activationWidth, activationHeight);
                isMouseInActiveZone = activationZone.Contains(inputHandler.VirtualMousePosition);
            }
            else
            {
                isMouseInActiveZone = false;
            }

            float newTargetYOffset;

            if (combatManager.CurrentState == PlayerTurnState.Resolving)
            {
                newTargetYOffset = HIDDEN_Y_OFFSET;
            }
            else if (isMouseInActiveZone && inputHandler.DraggedCard == null)
            {
                newTargetYOffset = ACTIVE_Y_OFFSET;
            }
            else
            {
                newTargetYOffset = PEEKING_Y_OFFSET;
            }

            if (newTargetYOffset != _targetMenuYOffset)
            {
                _targetMenuYOffset = newTargetYOffset;
                _startYOffset = _menuYOffset;
                _yAnimationTimer = 0f;
                _isYAnimating = true;
            }


            // Animate the entire hand's Y position
            if (_isYAnimating)
            {
                _yAnimationTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                float progress = Math.Min(1f, _yAnimationTimer / HAND_ANIMATION_DURATION);
                _menuYOffset = MathHelper.Lerp(_startYOffset, _targetMenuYOffset, Easing.EaseOutCubic(progress));
                if (progress >= 1f) _isYAnimating = false;
            }

            // --- Card Layout Calculation ---
            _hoveredIndex = -1;
            if (newTargetYOffset == ACTIVE_Y_OFFSET) // Only allow hover when fully active
            {
                // If a card is being held on click, it takes priority for being "hovered" visually.
                if (inputHandler.HeldCard != null)
                {
                    _hoveredIndex = _cards.IndexOf(inputHandler.HeldCard);
                }
                else // Otherwise, check for mouse hover as normal.
                {
                    // Iterate backwards to prioritize cards on top
                    for (int i = _cards.Count - 1; i >= 0; i--)
                    {
                        var cardBounds = _cards[i].CurrentBounds;
                        var hoverBounds = new RectangleF(cardBounds.X, cardBounds.Y, cardBounds.Width, cardBounds.Height + HOVER_BOUNDS_Y_EXTENSION);
                        if (hoverBounds.Contains(inputHandler.VirtualMousePosition))
                        {
                            _hoveredIndex = i;
                            break;
                        }
                    }
                }
            }


            float middleCardIndex = (_cards.Count - 1) / 2.0f;
            float menuBaseCenterY = actualScreenVirtualBounds.Bottom - (CARD_SIZE.Y * DEFAULT_SCALE / 2f) + _menuYOffset + HAND_Y_ANCHOR_OFFSET;

            for (int i = 0; i < _cards.Count; i++)
            {
                var card = _cards[i];
                if (card.IsBeingDragged) continue; // Dragged cards are handled by CombatInputHandler's ForcePosition

                bool isHovered = i == _hoveredIndex;
                bool isHeldButNotDragged = (card == inputHandler.HeldCard && inputHandler.DraggedCard == null);


                // Determine Target State (Visuals)
                float targetScale = DEFAULT_SCALE;
                float targetAlpha = DEFAULT_ALPHA;
                float targetShadowAlpha = 0f; // Default to no shadow
                Vector2 calculatedTargetPosition; // Will hold the final top-left position
                float targetRotation = 0f; // Default to no rotation

                if (isHeldButNotDragged)
                {
                    targetScale = HELD_SCALE;
                    targetAlpha = HOVERED_ALPHA;
                    targetShadowAlpha = HELD_SHADOW_ALPHA;
                    // Center the card on the drag start position for "picked up" feel
                    calculatedTargetPosition = inputHandler.DragStartPosition - (CARD_SIZE.ToVector2() * targetScale / 2f);
                    targetRotation = 0f; // Held card should be straight
                }
                else // Regular hovered or default state
                {
                    if (isHovered)
                    {
                        targetScale = HOVERED_SCALE;
                        targetAlpha = HOVERED_ALPHA;
                        targetShadowAlpha = HOVERED_SHADOW_ALPHA;
                    }

                    // Determine Target Position (X-axis) based on fanning/spreading
                    float cardBaseCenterX = screenCenterX + (i - middleCardIndex) * cardCenterDistance;
                    float xOffset = 0;
                    if (_hoveredIndex != -1) // If *any* card is hovered (including if the HeldCard is also the hoveredIndex)
                    {
                        if (i < _hoveredIndex) xOffset = -SPREAD_AMOUNT;
                        if (i > _hoveredIndex) xOffset = SPREAD_AMOUNT;
                    }
                    float targetCenterX = cardBaseCenterX + xOffset;
                    float targetX = targetCenterX - (CARD_SIZE.X * targetScale) / 2f; // Initial X based on center

                    // Determine Target Position (Y-axis) based on arching/hovering
                    float archYOffset = 0f;
                    if (_cards.Count > 1 && middleCardIndex > 0)
                    {
                        float distanceFromMiddle = Math.Abs(i - middleCardIndex);
                        archYOffset = (float)Math.Pow(distanceFromMiddle / middleCardIndex, 2) * CARD_ARCH_AMOUNT;
                    }
                    float targetCenterY = menuBaseCenterY + archYOffset;
                    if (isHovered)
                    {
                        targetCenterY += HOVER_Y_OFFSET;
                    }
                    float targetY = targetCenterY - (CARD_SIZE.Y * targetScale) / 2f; // Initial Y based on center

                    calculatedTargetPosition = new Vector2(targetX, targetY);

                    // Determine Target Rotation (only if not held/dragged and part of a fanned hand)
                    if (middleCardIndex > 0)
                    {
                        if (i < middleCardIndex) targetRotation = -CARD_TILT_RADIANS * (1 - (i / middleCardIndex));
                        else if (i > middleCardIndex) targetRotation = CARD_TILT_RADIANS * ((i - middleCardIndex) / middleCardIndex);
                    }
                }

                card.AnimateTo(calculatedTargetPosition, targetScale, DEFAULT_TINT, targetRotation, targetAlpha, targetShadowAlpha);
                card.Update(gameTime);
            }
        }

        /// <summary>
        /// Draws all cards except for a specified dragged card.
        /// </summary>
        public void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, CombatCard draggedCard)
        {
            CombatCard hoveredCard = null;

            // First pass: Draw all non-hovered, non-dragged cards
            for (int i = 0; i < _cards.Count; i++)
            {
                var card = _cards[i];
                if (card == draggedCard) continue;

                if (i != _hoveredIndex)
                {
                    DrawCard(spriteBatch, font, gameTime, card, false);
                }
                else
                {
                    hoveredCard = card;
                }
            }

            // Second pass: Draw the hovered card on top
            if (hoveredCard != null)
            {
                DrawCard(spriteBatch, font, gameTime, hoveredCard, true);
            }
        }

        /// <summary>
        /// Helper method to draw a single card with all its visual elements.
        /// </summary>
        public void DrawCard(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, CombatCard card, bool isHovered)
        {
            var pixel = ServiceLocator.Get<Texture2D>();
            var core = ServiceLocator.Get<Core>();

            var cardRotation = card.CurrentRotation;
            var cardScale = card.CurrentScale;
            var cardDrawPosition = card.CurrentBounds.Center;
            var pixelOrigin = new Vector2(0.5f);

            // 0. Draw Drop Shadow if it's visible
            if (card.ShadowAlpha > 0.01f)
            {
                var actualScreenVirtualBounds = core.GetActualScreenVirtualBounds();

                // The shadow shifts horizontally away from the center, creating a pseudo-3D effect.
                float screenCenterX = actualScreenVirtualBounds.Center.X;
                float horizontalDistanceFromCenter = cardDrawPosition.X - screenCenterX;
                float dynamicShadowX = horizontalDistanceFromCenter * SHADOW_HORIZONTAL_SHIFT_FACTOR;

                // The shadow also shifts vertically based on the card's "height" from the bottom of the screen.
                float cardHeightFromBottom = actualScreenVirtualBounds.Bottom - cardDrawPosition.Y;
                float dynamicShadowY = SHADOW_BASE_VERTICAL_OFFSET + (cardHeightFromBottom * SHADOW_VERTICAL_SHIFT_FACTOR);

                // Combine all offsets.
                var dynamicOffset = new Vector2(dynamicShadowX, dynamicShadowY);
                var baseShadowPosition = cardDrawPosition + dynamicOffset + card.ShadowOffset;

                float baseAlpha = card.ShadowAlpha;
                Vector2 baseSize = CARD_SIZE.ToVector2() * cardScale * SHADOW_SCALE_MULTIPLIER;

                // Draw multiple layers to simulate a blurred edge.
                for (int i = 0; i < SHADOW_BLUR_LAYERS; i++)
                {
                    // Each layer is slightly larger and more transparent
                    float layerScale = 1.0f + (i * SHADOW_BLUR_SPREAD);
                    Vector2 layerSize = baseSize * layerScale;

                    // The alpha decreases for each layer for a smooth falloff
                    float layerAlpha = baseAlpha / (float)Math.Pow(2, i);
                    Color layerColor = Color.Black * layerAlpha;

                    spriteBatch.Draw(pixel, baseShadowPosition, null, layerColor, cardRotation, pixelOrigin, layerSize, SpriteEffects.None, 0f);
                }
            }

            // 1. Draw Card Background
            var cardBgColor = new Color(CARD_TEXT_BG_COLOR.ToVector3() * card.CurrentTint.ToVector3()) * card.CurrentAlpha;
            spriteBatch.Draw(pixel, cardDrawPosition, null, cardBgColor, cardRotation, pixelOrigin, CARD_SIZE.ToVector2() * cardScale, SpriteEffects.None, 0f);

            // 2. Draw placeholder image area
            var imageAreaColor = new Color(CARD_IMAGE_AREA_COLOR.ToVector3() * card.CurrentTint.ToVector3()) * card.CurrentAlpha;
            var imageRectSize = new Vector2(CARD_SIZE.X, CARD_SIZE.Y * (2 / 3f));
            Vector2 imageAreaOffset = new Vector2(0, -CARD_SIZE.Y * (1 / 6f));
            Vector2 rotatedImageOffset = Vector2.Transform(imageAreaOffset * cardScale, Matrix.CreateRotationZ(cardRotation));
            Vector2 imageAreaCenterPos = cardDrawPosition + rotatedImageOffset;
            spriteBatch.Draw(pixel, imageAreaCenterPos, null, imageAreaColor, cardRotation, pixelOrigin, imageRectSize * cardScale, SpriteEffects.None, 0f);

            // 3. Draw Border
            float borderThickness = isHovered || card.IsBeingDragged ? 3f : 2f;
            Color borderColor = BORDER_COLOR * card.CurrentAlpha;
            var halfSize = CARD_SIZE.ToVector2() / 2f;
            var corners = new Vector2[4]
            {
                new Vector2(-halfSize.X, -halfSize.Y), new Vector2(halfSize.X, -halfSize.Y),
                new Vector2(halfSize.X, halfSize.Y), new Vector2(-halfSize.X, halfSize.Y)
            };
            var transform = Matrix.CreateScale(cardScale) * Matrix.CreateRotationZ(cardRotation) * Matrix.CreateTranslation(cardDrawPosition.X, cardDrawPosition.Y, 0);
            for (int j = 0; j < corners.Length; j++) corners[j] = Vector2.Transform(corners[j], transform);
            spriteBatch.DrawLine(corners[0], corners[1], borderColor, borderThickness);
            spriteBatch.DrawLine(corners[1], corners[2], borderColor, borderThickness);
            spriteBatch.DrawLine(corners[2], corners[3], borderColor, borderThickness);
            spriteBatch.DrawLine(corners[3], corners[0], borderColor, borderThickness);

            // 4. Draw action name
            var textColor = new Color(TEXT_COLOR.ToVector3() * card.CurrentTint.ToVector3()) * card.CurrentAlpha;
            Vector2 textSize = font.MeasureString(card.Action.Name);
            Vector2 textBgAreaOffset = new Vector2(0, CARD_SIZE.Y * (1 / 3f));
            Vector2 rotatedTextBgOffset = Vector2.Transform(textBgAreaOffset * cardScale, Matrix.CreateRotationZ(cardRotation));
            Vector2 textDrawPosition = cardDrawPosition + rotatedTextBgOffset;
            spriteBatch.DrawString(font, card.Action.Name, textDrawPosition, textColor, cardRotation, textSize / 2f, card.CurrentScale, SpriteEffects.None, 0f);
        }
    }
}
﻿
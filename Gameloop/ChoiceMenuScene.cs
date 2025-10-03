﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Scenes;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Scenes
{
    public enum ChoiceType { Spell, Ability, Item }

    public class ChoiceMenuScene : GameScene
    {
        private readonly List<ChoiceCard> _cards = new List<ChoiceCard>();
        private static readonly Random _random = new Random();
        private readonly SceneManager _sceneManager;
        private readonly GameState _gameState;
        private readonly ChoiceGenerator _choiceGenerator;

        private enum AnimationPhase { CardIntro, RarityIntro, Idle, CardOutro, SpellTransform_PopIn, SpellTransform_BookIntro, SpellTransform_MoveOut, SpellTransform_Absorb, SpellTransform_BookMoveOut }
        private AnimationPhase _currentPhase = AnimationPhase.CardIntro;

        private Queue<ChoiceCard> _cardsToAnimateIn = new Queue<ChoiceCard>();
        private float _animationStaggerTimer = 0f;
        private const float ANIMATION_STAGGER_DELAY = 0.075f;

        private Queue<ChoiceCard> _rarityToAnimate = new Queue<ChoiceCard>();
        private float _rarityStaggerTimer = 0f;
        private const float RARITY_STAGGER_DELAY = 0.1f;

        private List<(ChoiceCard card, float delay)> _cardsToAnimateOut = new List<(ChoiceCard, float)>();

        // State for the final transform animation
        private object _selectedChoiceData;
        private Vector2 _transformAnimPosition;
        private float _transformAnimTimer;
        private const float TRANSFORM_POP_IN_DURATION = 0.5f;
        private const float TRANSFORM_BOOK_INTRO_DURATION = 0.3f;
        private const float TRANSFORM_MOVE_OUT_DURATION = 0.4f;
        private const float TRANSFORM_ABSORB_PULSE_UP_DURATION = 0.25f;
        private const float TRANSFORM_ABSORB_HANG_DURATION = 0.2f;
        private const float TRANSFORM_ABSORB_PULSE_DOWN_DURATION = 0.2f;
        private const float TOTAL_ABSORB_DURATION = TRANSFORM_ABSORB_PULSE_UP_DURATION + TRANSFORM_ABSORB_HANG_DURATION + TRANSFORM_ABSORB_PULSE_DOWN_DURATION;
        private const float TRANSFORM_BOOK_MOVE_OUT_DURATION = 0.3f;
        private const float ABSORB_PULSE_SCALE = 1.2f;
        private const float ABSORB_SHAKE_MAGNITUDE = 4f;
        private const float ABSORB_SHAKE_FREQUENCY = 40f;
        private const float ABSORB_HOP_AMOUNT = 2f;
        private Vector2 _spellbookAnimPosition;
        private float _transformInitialRotation;


        public ChoiceMenuScene()
        {
            _sceneManager = ServiceLocator.Get<SceneManager>();
            _gameState = ServiceLocator.Get<GameState>();
            _choiceGenerator = new ChoiceGenerator();
        }

        public override Rectangle GetAnimatedBounds()
        {
            return new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);
        }

        public override void Enter()
        {
            base.Enter();
        }

        public void Show(ChoiceType type, int count)
        {
            _cards.Clear();
            _cardsToAnimateIn.Clear();
            _rarityToAnimate.Clear();
            _cardsToAnimateOut.Clear();
            _animationStaggerTimer = 0f;
            _rarityStaggerTimer = 0f;
            _currentPhase = AnimationPhase.CardIntro;

            // Use the ChoiceGenerator to get a curated list of choices
            // For now, we'll assume GameStage is 1 for testing purposes.
            // In the final implementation, this value will come from the game's progression manager.
            int currentGameStage = 1; // << This should be passed in or retrieved from a ProgressionManager
            var selectedChoices = _choiceGenerator.GenerateSpellChoices(currentGameStage, count).Cast<object>().ToList();

            // Layout calculation for vertical pillars
            const int cardWidth = 95;
            const int cardGap = 5;
            const int startY = 9; // Card top is now lower
            const int cardHeight = Global.VIRTUAL_HEIGHT - 2 - 8; // Card is now shorter
            int totalWidth = (cardWidth * count) + (cardGap * (count - 1));
            int startX = (Global.VIRTUAL_WIDTH - totalWidth) / 2;

            for (int i = 0; i < selectedChoices.Count; i++)
            {
                var choice = selectedChoices[i];
                var bounds = new Rectangle(startX + i * (cardWidth + cardGap), startY, cardWidth, cardHeight);
                ChoiceCard card = null;

                if (choice is MoveData move) card = new ChoiceCard(bounds, move);
                else if (choice is AbilityData ability) card = new ChoiceCard(bounds, ability);
                else if (choice is ConsumableItemData item) card = new ChoiceCard(bounds, item);

                if (card != null)
                {
                    card.OnClick += () => { if (_currentPhase == AnimationPhase.Idle) OnCardSelected(card); };
                    _cards.Add(card);
                    _cardsToAnimateIn.Enqueue(card);
                }
            }
        }

        private void OnCardSelected(ChoiceCard selectedCard)
        {
            _currentPhase = AnimationPhase.CardOutro;
            _selectedChoiceData = selectedCard.Data;
            _transformAnimPosition = selectedCard.Bounds.Center.ToVector2();

            // The selected card's onComplete will now trigger the next phase
            selectedCard.StartOutroAnimation(true, OnSelectedCardOutroComplete);

            // Queue up the other cards to animate out with a random stagger.
            _cardsToAnimateOut.Clear();
            if (_cards.Count > 1)
            {
                foreach (var card in _cards)
                {
                    if (card != selectedCard)
                    {
                        float delay = (float)(_random.NextDouble() * 0.2); // Random delay between 0 and 0.2 seconds
                        _cardsToAnimateOut.Add((card, delay));
                    }
                }
            }
        }

        private void OnSelectedCardOutroComplete()
        {
            if (_selectedChoiceData is MoveData)
            {
                _currentPhase = AnimationPhase.SpellTransform_PopIn;
                _transformAnimTimer = 0f;
                float initialTilt = (float)(_random.NextDouble() * Math.PI) - MathHelper.PiOver2; // -90 to +90 degrees
                float spinDirection = (_random.Next(2) == 0) ? 1f : -1f;
                _transformInitialRotation = initialTilt + (spinDirection * MathHelper.TwoPi);
            }
            else
            {
                // For abilities/items, just handle the choice immediately after the card disappears
                HandleChoice(_selectedChoiceData);
            }
        }

        private void HandleChoice(object choiceData)
        {
            if (choiceData is MoveData move)
            {
                EventBus.Publish(new GameEvents.PlayerMoveSetChanged { MoveID = move.MoveID, ChangeType = GameEvents.MoveSetChangeType.Learn });
            }
            else if (choiceData is AbilityData ability)
            {
                EventBus.Publish(new GameEvents.PlayerAbilitySetChanged { AbilityID = ability.AbilityID, ChangeType = GameEvents.AbilitySetChangeType.Learn });
            }
            else if (choiceData is ConsumableItemData item)
            {
                _gameState.PlayerState.AddItem(item.ItemID);
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[palette_teal]Obtained {item.ItemName}!" });
            }

            _sceneManager.HideModal();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Always update all cards so their internal animation timers can tick.
            var currentMouseState = Mouse.GetState();
            foreach (var card in _cards)
            {
                card.Update(currentMouseState, gameTime);
            }

            // Handle the scene's animation orchestration
            switch (_currentPhase)
            {
                case AnimationPhase.CardIntro:
                    if (_cardsToAnimateIn.Any())
                    {
                        _animationStaggerTimer += deltaTime;
                        if (_animationStaggerTimer >= ANIMATION_STAGGER_DELAY)
                        {
                            _animationStaggerTimer = 0f;
                            var card = _cardsToAnimateIn.Dequeue();
                            card.StartIntroAnimation();
                        }
                    }
                    else if (_cards.All(c => !c.IsIntroAnimating))
                    {
                        // All cards have finished sliding in, transition to next phase
                        _currentPhase = AnimationPhase.RarityIntro;
                        foreach (var card in _cards)
                        {
                            _rarityToAnimate.Enqueue(card);
                        }
                    }
                    break;

                case AnimationPhase.RarityIntro:
                    if (_rarityToAnimate.Any())
                    {
                        _rarityStaggerTimer += deltaTime;
                        if (_rarityStaggerTimer >= RARITY_STAGGER_DELAY)
                        {
                            _rarityStaggerTimer = 0f;
                            var card = _rarityToAnimate.Dequeue();
                            card.StartRarityAnimation();
                        }
                    }
                    else
                    {
                        _currentPhase = AnimationPhase.Idle;
                    }
                    break;

                case AnimationPhase.CardOutro:
                    // Stagger the start of the outro animations for unselected cards.
                    for (int i = _cardsToAnimateOut.Count - 1; i >= 0; i--)
                    {
                        var entry = _cardsToAnimateOut[i];
                        entry.delay -= deltaTime;
                        if (entry.delay <= 0)
                        {
                            entry.card.StartOutroAnimation(false);
                            _cardsToAnimateOut.RemoveAt(i);
                        }
                        else
                        {
                            _cardsToAnimateOut[i] = entry; // Update the struct in the list
                        }
                    }
                    break;

                case AnimationPhase.SpellTransform_PopIn:
                    _transformAnimTimer += deltaTime;
                    if (_transformAnimTimer >= TRANSFORM_POP_IN_DURATION)
                    {
                        _currentPhase = AnimationPhase.SpellTransform_BookIntro;
                        _transformAnimTimer = 0f;
                    }
                    break;

                case AnimationPhase.SpellTransform_BookIntro:
                    _transformAnimTimer += deltaTime;
                    if (_transformAnimTimer >= TRANSFORM_BOOK_INTRO_DURATION)
                    {
                        _currentPhase = AnimationPhase.SpellTransform_MoveOut;
                        _transformAnimTimer = 0f;
                    }
                    break;

                case AnimationPhase.SpellTransform_MoveOut:
                    _transformAnimTimer += deltaTime;
                    if (_transformAnimTimer >= TRANSFORM_MOVE_OUT_DURATION)
                    {
                        _currentPhase = AnimationPhase.SpellTransform_Absorb;
                        _transformAnimTimer = 0f;
                    }
                    break;

                case AnimationPhase.SpellTransform_Absorb:
                    _transformAnimTimer += deltaTime;
                    if (_transformAnimTimer >= TOTAL_ABSORB_DURATION)
                    {
                        _currentPhase = AnimationPhase.SpellTransform_BookMoveOut;
                        _transformAnimTimer = 0f;
                    }
                    break;

                case AnimationPhase.SpellTransform_BookMoveOut:
                    _transformAnimTimer += deltaTime;
                    if (_transformAnimTimer >= TRANSFORM_BOOK_MOVE_OUT_DURATION)
                    {
                        HandleChoice(_selectedChoiceData);
                    }
                    break;

                case AnimationPhase.Idle:
                    // Input is only processed when idle.
                    if (IsInputBlocked) return;
                    // The card's own Update method (called above) handles hover/click logic.
                    break;
            }
        }

        protected override void DrawSceneContent(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            foreach (var card in _cards)
            {
                card.Draw(spriteBatch, font, gameTime, transform);
            }

            if (_currentPhase >= AnimationPhase.SpellTransform_PopIn)
            {
                var spriteManager = ServiceLocator.Get<SpriteManager>();
                var pageSprite = spriteManager.SpellbookPageSprite;
                var bookSprite = spriteManager.SpellbookClosedSprite;

                if (pageSprite != null && bookSprite != null)
                {
                    // Default values
                    float pageScale = 1f;
                    float pageAlpha = 1f;
                    Vector2 pagePos = _transformAnimPosition;
                    var pageOrigin = pageSprite.Bounds.Center.ToVector2();
                    float pageRotation = 0f;
                    Color pageColor = Color.White;

                    Vector2 bookPos = Vector2.Zero;
                    var bookOrigin = new Vector2(MathF.Round(bookSprite.Width / 2f), bookSprite.Height);
                    float bookScale = 1f;
                    Color bookFlashColor = Color.Transparent;

                    // State-based animation calculations
                    if (_currentPhase == AnimationPhase.SpellTransform_PopIn)
                    {
                        float progress = Math.Clamp(_transformAnimTimer / TRANSFORM_POP_IN_DURATION, 0f, 1f);
                        pageScale = Easing.EaseOutBackBig(progress);
                        pageRotation = MathHelper.Lerp(_transformInitialRotation, 0f, Easing.EaseOutQuint(progress));
                        pageColor = Color.Lerp(Color.White, new Color(255, 255, 255, 0), 1f - progress);
                    }
                    else if (_currentPhase == AnimationPhase.SpellTransform_BookIntro)
                    {
                        float progress = Math.Clamp(_transformAnimTimer / TRANSFORM_BOOK_INTRO_DURATION, 0f, 1f);
                        float easedProgress = Easing.EaseOutCirc(progress);
                        float bookStartY = Global.VIRTUAL_HEIGHT + bookSprite.Height;
                        float bookEndY = Global.VIRTUAL_HEIGHT - 1;
                        float bookY = MathHelper.Lerp(bookStartY, bookEndY, easedProgress);
                        bookPos = new Vector2(_transformAnimPosition.X, bookY);
                    }
                    else if (_currentPhase == AnimationPhase.SpellTransform_MoveOut)
                    {
                        float progress = Math.Clamp(_transformAnimTimer / TRANSFORM_MOVE_OUT_DURATION, 0f, 1f);
                        float easedProgress = Easing.EaseInQuint(progress);

                        float pageEndY = Global.VIRTUAL_HEIGHT - bookSprite.Height + 10;
                        pagePos.Y = MathHelper.Lerp(_transformAnimPosition.Y, pageEndY, easedProgress);

                        bookPos = new Vector2(_transformAnimPosition.X, Global.VIRTUAL_HEIGHT - 1);
                    }
                    else if (_currentPhase == AnimationPhase.SpellTransform_Absorb)
                    {
                        pageAlpha = 0f; // Page is gone
                        bookPos = new Vector2(_transformAnimPosition.X, Global.VIRTUAL_HEIGHT - 1);

                        float timer = _transformAnimTimer;

                        // --- Scale Animation ---
                        if (timer < TRANSFORM_ABSORB_PULSE_UP_DURATION)
                        {
                            float pulseProgress = timer / TRANSFORM_ABSORB_PULSE_UP_DURATION;
                            bookScale = MathHelper.Lerp(1.0f, ABSORB_PULSE_SCALE, Easing.EaseOutQuad(pulseProgress));
                            // Flash fades out during the inflation using an easing function for more impact.
                            bookFlashColor = Color.White * (1.0f - Easing.EaseInQuint(pulseProgress));
                        }
                        else if (timer < TRANSFORM_ABSORB_PULSE_UP_DURATION + TRANSFORM_ABSORB_HANG_DURATION)
                        {
                            bookScale = ABSORB_PULSE_SCALE;
                        }
                        else
                        {
                            float pulseProgress = (timer - (TRANSFORM_ABSORB_PULSE_UP_DURATION + TRANSFORM_ABSORB_HANG_DURATION)) / TRANSFORM_ABSORB_PULSE_DOWN_DURATION;
                            bookScale = MathHelper.Lerp(ABSORB_PULSE_SCALE, 1.0f, Easing.EaseInQuad(pulseProgress));
                        }

                        // --- Hop & Shake (tied to the overall progress for a smooth decay) ---
                        float progress = Math.Clamp(timer / TOTAL_ABSORB_DURATION, 0f, 1f);
                        float hopPulseProgress = MathF.Sin(progress * MathHelper.Pi); // Simple pulse for hop is fine
                        bookPos.Y -= ABSORB_HOP_AMOUNT * hopPulseProgress;

                        float shakeDecay = 1.0f - Easing.EaseOutQuad(progress);
                        float shakeOffset = MathF.Sin(progress * ABSORB_SHAKE_FREQUENCY) * ABSORB_SHAKE_MAGNITUDE * shakeDecay;
                        bookPos.X += shakeOffset;
                    }
                    else if (_currentPhase == AnimationPhase.SpellTransform_BookMoveOut)
                    {
                        pageAlpha = 0f;
                        float progress = Math.Clamp(_transformAnimTimer / TRANSFORM_BOOK_MOVE_OUT_DURATION, 0f, 1f);
                        float easedProgress = Easing.EaseInQuad(progress);
                        float bookStartY = Global.VIRTUAL_HEIGHT - 1;
                        float bookEndY = Global.VIRTUAL_HEIGHT + bookSprite.Height;
                        bookPos = new Vector2(_transformAnimPosition.X, MathHelper.Lerp(bookStartY, bookEndY, easedProgress));
                    }

                    // Draw the page first
                    if (pageAlpha > 0)
                    {
                        spriteBatch.DrawSnapped(pageSprite, pagePos, null, Color.White * pageAlpha, pageRotation, pageOrigin, pageScale, SpriteEffects.None, 0f);
                        if (_currentPhase == AnimationPhase.SpellTransform_PopIn)
                        {
                            spriteBatch.DrawSnapped(pageSprite, pagePos, null, pageColor, pageRotation, pageOrigin, pageScale, SpriteEffects.None, 0f);
                        }
                    }

                    // Draw the spellbook on top so the page goes "behind" it
                    if (_currentPhase >= AnimationPhase.SpellTransform_BookIntro)
                    {
                        spriteBatch.DrawSnapped(bookSprite, bookPos, null, Color.White, 0f, bookOrigin, bookScale, SpriteEffects.None, 0f);
                        if (_currentPhase == AnimationPhase.SpellTransform_Absorb && bookFlashColor.A > 0)
                        {
                            // End the current AlphaBlend batch to draw the additive flash
                            spriteBatch.End();
                            spriteBatch.Begin(blendState: BlendState.Additive, samplerState: SamplerState.PointClamp, transformMatrix: transform);

                            spriteBatch.DrawSnapped(bookSprite, bookPos, null, bookFlashColor, 0f, bookOrigin, bookScale, SpriteEffects.None, 0f);

                            // End the additive batch and resume the original AlphaBlend batch
                            spriteBatch.End();
                            spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp, transformMatrix: transform);
                        }
                    }
                }
            }
        }

        public override void DrawUnderlay(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            float underlayAlpha = 0.7f;
            var screenBounds = new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);
            spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            spriteBatch.Draw(ServiceLocator.Get<Texture2D>(), screenBounds, Color.Black * underlayAlpha);
            spriteBatch.End();
        }
    }
}
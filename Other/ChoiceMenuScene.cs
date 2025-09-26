using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.UI;
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

        private enum AnimationPhase { CardIntro, RarityIntro, Idle, CardOutro }
        private AnimationPhase _currentPhase = AnimationPhase.CardIntro;

        private Queue<ChoiceCard> _cardsToAnimateIn = new Queue<ChoiceCard>();
        private float _animationStaggerTimer = 0f;
        private const float ANIMATION_STAGGER_DELAY = 0.075f;

        private Queue<ChoiceCard> _rarityToAnimate = new Queue<ChoiceCard>();
        private float _rarityStaggerTimer = 0f;
        private const float RARITY_STAGGER_DELAY = 0.1f;

        private List<(ChoiceCard card, float delay)> _cardsToAnimateOut = new List<(ChoiceCard, float)>();

        public ChoiceMenuScene()
        {
            _sceneManager = ServiceLocator.Get<SceneManager>();
            _gameState = ServiceLocator.Get<GameState>();
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

            var availableChoices = GetAvailableChoices(type);
            var selectedChoices = availableChoices.OrderBy(x => _random.Next()).Take(count).ToList();

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

            // The selected card starts its animation immediately.
            // The game logic (HandleChoice) is passed as a callback to run when the animation finishes.
            selectedCard.StartOutroAnimation(true, () => HandleChoice(selectedCard.Data));

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

        private List<object> GetAvailableChoices(ChoiceType type)
        {
            switch (type)
            {
                case ChoiceType.Spell:
                    return BattleDataCache.Moves.Values.Where(m => m.MoveType == MoveType.Spell).Cast<object>().ToList();
                case ChoiceType.Ability:
                    return BattleDataCache.Abilities.Values.Cast<object>().ToList();
                case ChoiceType.Item:
                    return BattleDataCache.Consumables.Values.Cast<object>().ToList();
                default:
                    return new List<object>();
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
        }

        public override void DrawUnderlay(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            var screenBounds = new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);
            spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            spriteBatch.Draw(ServiceLocator.Get<Texture2D>(), screenBounds, Color.Black * 0.7f);
            spriteBatch.End();
        }
    }
}
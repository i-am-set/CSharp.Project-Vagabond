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

        private enum AnimationPhase { CardIntro, RarityIntro, Idle }
        private AnimationPhase _currentPhase = AnimationPhase.CardIntro;

        private Queue<ChoiceCard> _cardsToAnimate = new Queue<ChoiceCard>();
        private float _animationStaggerTimer = 0f;
        private const float ANIMATION_STAGGER_DELAY = 0.075f;

        private Queue<ChoiceCard> _rarityToAnimate = new Queue<ChoiceCard>();
        private float _rarityStaggerTimer = 0f;
        private const float RARITY_STAGGER_DELAY = 0.1f;

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
            // Animation queues are now managed by the Show() method to ensure correct initialization order.
        }

        public void Show(ChoiceType type, int count)
        {
            _cards.Clear();
            _cardsToAnimate.Clear();
            _rarityToAnimate.Clear();
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
                    card.OnClick += () => HandleChoice(choice);
                    _cards.Add(card);
                    _cardsToAnimate.Enqueue(card);
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

            switch (_currentPhase)
            {
                case AnimationPhase.CardIntro:
                    if (_cardsToAnimate.Any())
                    {
                        _animationStaggerTimer += deltaTime;
                        if (_animationStaggerTimer >= ANIMATION_STAGGER_DELAY)
                        {
                            _animationStaggerTimer = 0f;
                            var card = _cardsToAnimate.Dequeue();
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
                        // Could add a check here to wait for rarity animations to finish,
                        // but for now, we'll let input become active immediately.
                        _currentPhase = AnimationPhase.Idle;
                    }
                    break;

                case AnimationPhase.Idle:
                    if (IsInputBlocked) return;
                    var currentMouseState = Mouse.GetState();
                    foreach (var card in _cards)
                    {
                        card.Update(currentMouseState, gameTime);
                    }
                    break;
            }

            // Always update cards for their internal animation timers
            if (_currentPhase != AnimationPhase.Idle)
            {
                var currentMouseState = Mouse.GetState();
                foreach (var card in _cards)
                {
                    card.Update(currentMouseState, gameTime);
                }
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
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Combat;
using ProjectVagabond.Combat.UI;
using ProjectVagabond.Scenes;
using ProjectVagabond.Utils;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using MonoGame.Extended;
using MonoGame.Extended.Graphics;
using System.Linq;
using System;

namespace ProjectVagabond.Scenes
{
    public class CombatScene : GameScene
    {
        private CombatManager _combatManager;
        private ActionHandUI _actionHandUI;
        private CombatInputHandler _inputHandler;
        private AnimationManager _animationManager;
        private GameState _gameState;
        private ActionResolver _actionResolver;

        // Player Hands
        private HandRenderer _leftHandRenderer;
        private HandRenderer _rightHandRenderer;

        // --- TUNING CONSTANTS ---
        // Enemy Layout
        private const float ENEMY_BASE_Y = 150f; // The Y position of the CLOSEST enemy.
        private const float ENEMY_SPACING_X = 150f;
        private const float ENEMY_STAGGER_Y = 20f; // How much further UP (back) each enemy in the V-shape is.
        private const float ENEMY_STAGGER_SCALE_FACTOR = 0.05f; // How much smaller each enemy gets per step back.

        // Player Layout
        private const float PLAYER_BASE_Y_OFFSET = -80f; // Offset from the bottom of the screen.

        // Play Area
        private const float PLAY_AREA_INSET_HORIZONTAL = 50f;
        private const float PLAY_AREA_INSET_VERTICAL_TOP = 20f;
        private const float PLAY_AREA_BOTTOM_EXCLUSION_PERCENT = 0.20f; // The bottom 20% of the screen is a cancel zone.

        // Targeting Pointer
        private const float POINTER_CURVE_OFFSET = 150f;
        private const float POINTER_HORIZONTAL_PUSH = 50f;
        private const int POINTER_DOT_COUNT = 50;
        private static readonly Color POINTER_COLOR = Color.Yellow;
        private const float POINTER_DOT_SIZE = 2f;
        private const float POINTER_END_CIRCLE_RADIUS = 5f;
        private const float POINTER_ANTS_SPEED = 15f; // Dots per second
        private const int POINTER_ANTS_SPACING = 3; // Gap between dots (e.g., 3 means 1 dot drawn, 2 skipped)
        private const float SELF_CAST_INDICATOR_BUFFER = 5f;

        // A list to manage cards that are currently in their "play" animation.
        private readonly List<CombatCard> _playingCards = new List<CombatCard>();
        private readonly List<CombatEntity> _enemies = new List<CombatEntity>();
        private CombatEntity _playerEntity;
        private float _pointerAntsTimer = 0f;

        public RectangleF PlayArea { get; private set; }

        public override bool UsesLetterboxing => false;

        public override void Initialize()
        {
            base.Initialize();

            _combatManager = new CombatManager();
            _actionHandUI = new ActionHandUI();
            _inputHandler = new CombatInputHandler(_combatManager, _actionHandUI, this);
            _animationManager = ServiceLocator.Get<AnimationManager>();
            _gameState = ServiceLocator.Get<GameState>();
            _actionResolver = new ActionResolver();
            ServiceLocator.Register<ActionResolver>(_actionResolver);


            // Initialize player hands and their renderers
            _leftHandRenderer = new HandRenderer(HandType.Left);
            _rightHandRenderer = new HandRenderer(HandType.Right);

            // Register UI components with the manager so states can access them.
            _combatManager.RegisterComponents(_actionHandUI, _inputHandler, this);
        }

        public override void Enter()
        {
            base.Enter();
            EventBus.Subscribe<GameEvents.UIThemeOrResolutionChanged>(OnResolutionChanged);
            EventBus.Subscribe<GameEvents.PlayerActionConfirmed>(OnPlayerActionConfirmed);
            EventBus.Subscribe<GameEvents.CardReturnedToHand>(OnCardReturned);

            // Load content for hand renderers
            _leftHandRenderer.LoadContent();
            _rightHandRenderer.LoadContent();
            _leftHandRenderer.EnterScene();
            _rightHandRenderer.EnterScene();

            // Clear any previous combat state
            _enemies.Clear();
            _playingCards.Clear();
            _playerEntity = null;
        }

        /// <summary>
        /// Initializes the combat with a specific set of dynamically created enemies.
        /// This is called by the SceneManager after the scene has been entered.
        /// </summary>
        public void StartCombat(List<CombatEntity> enemies)
        {
            var spriteManager = ServiceLocator.Get<SpriteManager>();

            // --- Player and Enemy Setup ---
            _enemies.Clear();
            _enemies.AddRange(enemies);
            // TODO: Get real health from player stats component
            _playerEntity = new CombatEntity(_gameState.PlayerEntityId, 100, spriteManager.PlayerSprite);

            CalculateLayouts();

            // Reset the UI state before starting the FSM
            _actionHandUI.EnterCombat();
            _playingCards.Clear();
            _inputHandler.Reset();

            var combatants = new List<int> { _gameState.PlayerEntityId };
            combatants.AddRange(_enemies.Select(e => e.EntityId));
            _combatManager.StartCombat(combatants);
        }


        private void CalculateLayouts()
        {
            var core = ServiceLocator.Get<Core>();
            Rectangle actualScreenVirtualBounds = core.GetActualScreenVirtualBounds();

            // Calculate Play Area, excluding the bottom part for cancellation.
            float bottomInset = actualScreenVirtualBounds.Height * PLAY_AREA_BOTTOM_EXCLUSION_PERCENT;
            PlayArea = new RectangleF(
                actualScreenVirtualBounds.X + PLAY_AREA_INSET_HORIZONTAL,
                actualScreenVirtualBounds.Y + PLAY_AREA_INSET_VERTICAL_TOP,
                actualScreenVirtualBounds.Width - (PLAY_AREA_INSET_HORIZONTAL * 2),
                actualScreenVirtualBounds.Height - PLAY_AREA_INSET_VERTICAL_TOP - bottomInset
            );

            // Layout Player
            if (_playerEntity != null)
            {
                float playerX = actualScreenVirtualBounds.Center.X;
                float playerY = actualScreenVirtualBounds.Bottom + PLAYER_BASE_Y_OFFSET;
                _playerEntity.SetLayout(new Vector2(playerX, playerY), 1.0f);
            }

            // Calculate Enemy Layout
            LayoutEnemies();
        }

        private void LayoutEnemies()
        {
            var core = ServiceLocator.Get<Core>();
            Rectangle actualScreenVirtualBounds = core.GetActualScreenVirtualBounds();
            float screenCenterX = actualScreenVirtualBounds.X + actualScreenVirtualBounds.Width / 2f;

            int enemyCount = _enemies.Count;
            if (enemyCount == 0) return;

            // Sort by a stable property like ID before layout to prevent swapping.
            _enemies.Sort((a, b) => a.EntityId.CompareTo(b.EntityId));

            if (enemyCount <= 3)
            {
                // Simple side-by-side layout for 1-3 enemies
                float totalWidth = (enemyCount - 1) * ENEMY_SPACING_X;
                float startX = screenCenterX - totalWidth / 2f;

                for (int i = 0; i < enemyCount; i++)
                {
                    var enemy = _enemies[i];
                    float x = startX + i * ENEMY_SPACING_X;
                    enemy.SetLayout(new Vector2(x, ENEMY_BASE_Y), 1.0f);
                }
            }
            else
            {
                // Inverted Chevron (V-shape) layout for 4+ enemies
                float middleIndex = (enemyCount - 1) / 2.0f;

                for (int i = 0; i < enemyCount; i++)
                {
                    var enemy = _enemies[i];
                    float distanceFromMiddle = Math.Abs(i - middleIndex);

                    float x = screenCenterX + (i - middleIndex) * ENEMY_SPACING_X;
                    // The center enemy (distanceFromMiddle = 0) is at the highest Y (closest).
                    // Wing enemies move UP (smaller Y) as they get further from the center.
                    float y = ENEMY_BASE_Y - distanceFromMiddle * ENEMY_STAGGER_Y;
                    float scale = 1.0f - distanceFromMiddle * ENEMY_STAGGER_SCALE_FACTOR;

                    enemy.SetLayout(new Vector2(x, y), scale);
                }
            }

            // Finally, sort by Y-position for correct draw order (back to front).
            _enemies.Sort((a, b) => a.Position.Y.CompareTo(b.Position.Y));
        }


        public override void Exit()
        {
            base.Exit();
            EventBus.Unsubscribe<GameEvents.UIThemeOrResolutionChanged>(OnResolutionChanged);
            EventBus.Unsubscribe<GameEvents.PlayerActionConfirmed>(OnPlayerActionConfirmed);
            EventBus.Unsubscribe<GameEvents.CardReturnedToHand>(OnCardReturned);
        }

        private void OnPlayerActionConfirmed(GameEvents.PlayerActionConfirmed e)
        {
            var cardToPlay = _actionHandUI.Cards.FirstOrDefault(c => c.Action.Id == e.CardActionData.Id);
            if (cardToPlay != null)
            {
                _actionHandUI.RemoveCard(e.CardActionData.Id);
                _playingCards.Add(cardToPlay);

                Vector2 targetPos;
                var core = ServiceLocator.Get<Core>();
                Rectangle actualScreenVirtualBounds = core.GetActualScreenVirtualBounds();

                if (e.TargetEntityIds != null && e.TargetEntityIds.Any())
                {
                    if (e.TargetEntityIds.Count == 1)
                    {
                        // Single target
                        var targetEntity = _enemies.FirstOrDefault(enemy => enemy.EntityId == e.TargetEntityIds[0]);
                        targetPos = targetEntity?.Bounds.Center.ToVector2() ?? actualScreenVirtualBounds.Center.ToVector2();
                    }
                    else
                    {
                        // Multiple targets (AoE) - find the average position
                        Vector2 averagePos = Vector2.Zero;
                        int count = 0;
                        foreach (var targetId in e.TargetEntityIds)
                        {
                            var targetEntity = _enemies.FirstOrDefault(enemy => enemy.EntityId == targetId);
                            if (targetEntity != null)
                            {
                                averagePos += targetEntity.Bounds.Center.ToVector2();
                                count++;
                            }
                        }
                        targetPos = (count > 0) ? averagePos / count : actualScreenVirtualBounds.Center.ToVector2();
                    }
                }
                else
                {
                    // Self-cast (no targets) - animate to a spot above the hand
                    targetPos = new Vector2(actualScreenVirtualBounds.Center.X, actualScreenVirtualBounds.Bottom - 150);
                }

                cardToPlay.AnimatePlay(targetPos);
            }
        }

        private void OnCardReturned(GameEvents.CardReturnedToHand e)
        {
            // This logic might need adjustment if cards are returned from a specific enemy target
            var core = ServiceLocator.Get<Core>();
            Rectangle actualScreenVirtualBounds = core.GetActualScreenVirtualBounds();
            Vector2 startPos = actualScreenVirtualBounds.Center.ToVector2();

            _actionHandUI.AddCard(e.CardActionData, startPos);
        }


        private void OnResolutionChanged(GameEvents.UIThemeOrResolutionChanged e)
        {
            CalculateLayouts();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            _pointerAntsTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Update and clean up playing cards
            for (int i = _playingCards.Count - 1; i >= 0; i--)
            {
                var card = _playingCards[i];
                card.Update(gameTime);
                if (card.IsAnimationFinished)
                {
                    _playingCards.RemoveAt(i);
                    EventBus.Publish(new GameEvents.ActionAnimationComplete());
                }
            }

            // The CombatManager's FSM now drives the logic flow.
            _combatManager.Update(gameTime);

            // Update hand renderers
            _leftHandRenderer.Update(gameTime, _combatManager);
            _rightHandRenderer.Update(gameTime, _combatManager);

            // Enemy animations and other scene-specific visuals are updated here.
            // The FSM controls when the player can interact.
            foreach (var enemy in _enemies)
            {
                enemy.Update(gameTime);
            }
        }

        /// <summary>
        /// Called by the ActionExecutionState to trigger the visual representation of an action.
        /// </summary>
        public void ExecuteActionVisuals(CombatAction action)
        {
            // Combine all combatants into a single list for the resolver
            var allCombatants = new List<CombatEntity>(_enemies);
            if (_playerEntity != null)
            {
                allCombatants.Add(_playerEntity);
            }

            _actionResolver.Resolve(action, allCombatants);

            if (action.CasterEntityId == _gameState.PlayerEntityId)
            {
                // The visual of the player's card flying to the target has already completed.
                // This is where damage effects, sounds, and target reactions would be triggered.
                // For now, the effect is instant.
                EventBus.Publish(new GameEvents.ActionAnimationComplete());
            }
            else // AI Action
            {
                // TODO: Play AI animations (e.g., enemy sprite animation, particle effects).
                // For now, AI actions are instant.
                EventBus.Publish(new GameEvents.ActionAnimationComplete());
            }
        }

        protected override void DrawSceneContent(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            // Draw player and enemies
            _playerEntity?.Draw(spriteBatch);
            foreach (var enemy in _enemies)
            {
                enemy.Draw(spriteBatch);
            }

            // Draw player hands behind the card UI
            _leftHandRenderer.Draw(spriteBatch, font, gameTime);
            _rightHandRenderer.Draw(spriteBatch, font, gameTime);

            // Draw targeting indicators underneath the dragged card
            DrawTargetingIndicators(spriteBatch);

            // Draw the hand of cards, skipping the one being dragged
            _actionHandUI.Draw(spriteBatch, font, gameTime, _inputHandler.DraggedCard);

            // Draw cards that are currently in their "play" animation
            foreach (var card in _playingCards)
            {
                _actionHandUI.DrawCard(spriteBatch, font, gameTime, card, false);
            }

            // Draw the dragged card on top of the pointer
            if (_inputHandler.DraggedCard != null)
            {
                _actionHandUI.DrawCard(spriteBatch, font, gameTime, _inputHandler.DraggedCard, true);
            }

            // --- DEBUG: Draw the play area boundary ---
            var global = ServiceLocator.Get<Global>();
            if (global.ShowDebugOverlays)
            {
                spriteBatch.DrawRectangle(PlayArea, Color.Lime, 1f);
            }
        }

        private void DrawTargetingIndicators(SpriteBatch spriteBatch)
        {
            if (_inputHandler.DraggedCard == null) return;

            // Only draw indicators if the card is within the valid play area.
            if (!PlayArea.Contains(_inputHandler.VirtualMousePosition))
            {
                return;
            }

            var actionType = _inputHandler.DraggedCard.Action.TargetType;

            if (actionType == TargetType.Self)
            {
                DrawSelfCastIndicator(spriteBatch);
            }
            else if (actionType == TargetType.AllEnemies)
            {
                foreach (var enemy in _enemies)
                {
                    DrawPointerToTarget(spriteBatch, enemy);
                }
            }
            else if (actionType == TargetType.SingleEnemy && _inputHandler.PotentialTargetId.HasValue)
            {
                var targetEnemy = _enemies.FirstOrDefault(e => e.EntityId == _inputHandler.PotentialTargetId.Value);
                if (targetEnemy != null)
                {
                    DrawPointerToTarget(spriteBatch, targetEnemy);
                }
            }
        }

        private void DrawPointerToTarget(SpriteBatch spriteBatch, CombatEntity targetEnemy)
        {
            var pixel = ServiceLocator.Get<Texture2D>();
            Vector2 startPos = _inputHandler.DraggedCard.CurrentBounds.Center;
            Vector2 endPos = targetEnemy.Bounds.Center.ToVector2();

            var core = ServiceLocator.Get<Core>();
            Rectangle actualScreenVirtualBounds = core.GetActualScreenVirtualBounds();
            float screenCenterX = actualScreenVirtualBounds.Center.X;
            float halfScreenWidth = actualScreenVirtualBounds.Width / 2f;

            float normalizedDistFromCenter = (startPos.X - screenCenterX) / halfScreenWidth;
            float horizontalPush = -normalizedDistFromCenter * POINTER_HORIZONTAL_PUSH;

            Vector2 controlPos = new Vector2(
                (startPos.X + endPos.X) / 2 + horizontalPush,
                Math.Min(startPos.Y, endPos.Y) - POINTER_CURVE_OFFSET
            );

            int dotOffset = (int)(_pointerAntsTimer * POINTER_ANTS_SPEED);

            for (int i = 1; i < POINTER_DOT_COUNT; i++)
            {
                int effectiveIndex = i - dotOffset;
                if (((effectiveIndex % POINTER_ANTS_SPACING) + POINTER_ANTS_SPACING) % POINTER_ANTS_SPACING != 0)
                {
                    continue;
                }

                float t = (float)i / POINTER_DOT_COUNT;
                float oneMinusT = 1 - t;
                Vector2 pointOnCurve = oneMinusT * oneMinusT * startPos + 2 * oneMinusT * t * controlPos + t * t * endPos;

                spriteBatch.Draw(pixel, pointOnCurve, null, POINTER_COLOR, 0f, new Vector2(0.5f), POINTER_DOT_SIZE, SpriteEffects.None, 0f);
            }

            spriteBatch.DrawCircle(endPos, POINTER_END_CIRCLE_RADIUS, 12, POINTER_COLOR, POINTER_DOT_SIZE);
        }

        private void DrawSelfCastIndicator(SpriteBatch spriteBatch)
        {
            var cardBounds = _inputHandler.DraggedCard.CurrentBounds;

            var indicatorBounds = new RectangleF(
                cardBounds.X - SELF_CAST_INDICATOR_BUFFER,
                cardBounds.Y - SELF_CAST_INDICATOR_BUFFER,
                cardBounds.Width + SELF_CAST_INDICATOR_BUFFER * 2,
                cardBounds.Height + SELF_CAST_INDICATOR_BUFFER * 2
            );

            var topLeft = new Vector2(indicatorBounds.Left, indicatorBounds.Top);
            var topRight = new Vector2(indicatorBounds.Right, indicatorBounds.Top);
            var bottomLeft = new Vector2(indicatorBounds.Left, indicatorBounds.Bottom);
            var bottomRight = new Vector2(indicatorBounds.Right, indicatorBounds.Bottom);

            DrawMarchingLine(spriteBatch, topLeft, topRight);
            DrawMarchingLine(spriteBatch, topRight, bottomRight);
            DrawMarchingLine(spriteBatch, bottomRight, bottomLeft);
            DrawMarchingLine(spriteBatch, bottomLeft, topLeft);
        }

        private void DrawMarchingLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end)
        {
            var pixel = ServiceLocator.Get<Texture2D>();
            float length = Vector2.Distance(start, end);
            if (length < 1f) return;

            Vector2 direction = Vector2.Normalize(end - start);
            int numDots = (int)(length / POINTER_DOT_SIZE) + 1;
            int dotOffset = (int)(_pointerAntsTimer * POINTER_ANTS_SPEED);

            for (int i = 0; i < numDots; i++)
            {
                int effectiveIndex = i - dotOffset;
                if (((effectiveIndex % POINTER_ANTS_SPACING) + POINTER_ANTS_SPACING) % POINTER_ANTS_SPACING != 0)
                {
                    continue;
                }

                float distanceAlongLine = i * POINTER_DOT_SIZE;
                if (distanceAlongLine > length) break;

                Vector2 pointOnLine = start + direction * distanceAlongLine;
                spriteBatch.Draw(pixel, pointOnLine, null, POINTER_COLOR, 0f, new Vector2(0.5f), POINTER_DOT_SIZE, SpriteEffects.None, 0f);
            }
        }

        #region Public Accessors for InputHandler
        public CombatEntity FindClosestEnemyTo(Vector2 position)
        {
            return _enemies
                .OrderBy(e => Vector2.DistanceSquared(e.Bounds.Center.ToVector2(), position))
                .FirstOrDefault();
        }

        public void SetEntityTargeted(int entityId, bool isTargeted)
        {
            var entity = _enemies.FirstOrDefault(e => e.EntityId == entityId);
            if (entity != null)
            {
                entity.IsTargeted = isTargeted;
            }
        }

        public void SetAllEnemiesTargeted(bool isTargeted)
        {
            foreach (var enemy in _enemies)
            {
                enemy.IsTargeted = isTargeted;
            }
        }

        public List<int> GetAllEnemyIds()
        {
            return _enemies.Select(e => e.EntityId).ToList();
        }
        #endregion

        public override Rectangle GetAnimatedBounds()
        {
            return new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);
        }
    }
}
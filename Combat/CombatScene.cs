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
using ProjectVagabond.Combat.FSM;
using ProjectVagabond.Editor;
using ProjectVagabond.Particles;

namespace ProjectVagabond.Scenes
{
    public class CombatScene : GameScene, IAnimationPlaybackContext
    {
        private CombatManager _combatManager;
        private ActionHandUI _actionHandUI;
        private CombatInputHandler _inputHandler;
        private AnimationManager _animationManager;
        private GameState _gameState;
        private ActionResolver _actionResolver;
        private ActionAnimator _actionAnimator;
        public ActionAnimator ActionAnimator => _actionAnimator;
        private ParticleSystemManager _particleSystemManager;

        public CombatAction CurrentExecutingAction { get; set; }

        // Player Hands
        private HandRenderer _leftHandRenderer;
        private HandRenderer _rightHandRenderer;
        public HandRenderer LeftHandRenderer => _leftHandRenderer;
        public HandRenderer RightHandRenderer => _rightHandRenderer;

        // Animation Anchor Points
        public Dictionary<string, Vector2> AnimationAnchors { get; private set; }

        // --- TUNING CONSTANTS ---
        // Entity Sizing
        private static readonly Point ENEMY_BASE_SIZE = new Point(64, 96); // Base dimensions (Width, Height) for enemies.

        // Enemy Layout
        private const float ENEMY_BASE_Y = 100f; // The Y position of the CLOSEST enemy.
        private const float ENEMY_SPACING_X = 150f;
        private const float ENEMY_STAGGER_Y = 20f; // How much further UP (back) each enemy in the V-shape is.

        // Player UI
        private static readonly Point PLAYER_HEALTH_BAR_SIZE = new Point(200, 12);
        private const int PLAYER_HEALTH_BAR_Y_OFFSET = 80; // Distance from the bottom of the screen.
        private const int PLAYER_HIT_MARKER_Y_OFFSET = 10; // Distance above the health bar.

        // Play Area
        private const float PLAY_AREA_INSET_HORIZONTAL = 50f;
        private const float PLAY_AREA_INSET_VERTICAL_TOP = 20f;
        private const float PLAY_AREA_BOTTOM_EXCLUSION_PERCENT = 0.20f; // The bottom 20% of the screen is a cancel zone.

        // Targeting Indicator
        private static readonly Point TARGET_INDICATOR_SIZE = new Point(32, 32);
        private static readonly Color TARGET_INDICATOR_COLOR = Color.Yellow;
        private const float TARGET_INDICATOR_Y_OFFSET = 10f;
        private const float TARGET_INDICATOR_BOB_DISTANCE = 1f;
        private const float TARGET_INDICATOR_BOB_DURATION = 0.5f;

        // Self-Cast Indicator
        private const float SELF_CAST_INDICATOR_BUFFER = 5f;
        private const float POINTER_ANTS_SPEED = 15f; // Dots per second
        private const int POINTER_ANTS_SPACING = 3; // Gap between dots (e.g., 1 dot drawn, 2 skipped)
        private const float SELF_CAST_DOT_SIZE = 2f; // The size of each dot in the marching ants line.

        // A list to manage cards that are currently in their "play" animation.
        private readonly List<CombatCard> _playingCards = new List<CombatCard>();
        private readonly List<CombatEntity> _enemies = new List<CombatEntity>();
        private CombatEntity _playerEntity;
        private float _indicatorAnimationTimer = 0f;

        // Layout state
        private Rectangle _lastKnownBounds;

        public RectangleF PlayArea { get; private set; }

        public override bool UsesLetterboxing => false;

        public override void Initialize()
        {
            base.Initialize();

            _combatManager = new CombatManager();
            _actionHandUI = new ActionHandUI();
            _animationManager = ServiceLocator.Get<AnimationManager>();
            _gameState = ServiceLocator.Get<GameState>();
            _actionResolver = new ActionResolver();
            _particleSystemManager = ServiceLocator.Get<ParticleSystemManager>();
            ServiceLocator.Register<ActionResolver>(_actionResolver);

            // Initialize player hands and their renderers with a temporary position.
            // The correct positions will be calculated and set in CalculateLayouts.
            _leftHandRenderer = new HandRenderer(HandType.Left, Vector2.Zero);
            _rightHandRenderer = new HandRenderer(HandType.Right, Vector2.Zero);

            // Create the ActionAnimator here, after its dependencies are ready.
            _actionAnimator = new ActionAnimator(_leftHandRenderer, _rightHandRenderer);

            // Create the input handler now that all its dependencies are initialized.
            _inputHandler = new CombatInputHandler(_combatManager, _actionHandUI, this);

            // Register UI components with the manager so states can access them.
            _combatManager.RegisterComponents(_actionHandUI, _inputHandler, this);
        }

        public override void Enter()
        {
            base.Enter();
            EventBus.Subscribe<GameEvents.PlayerActionConfirmed>(OnPlayerActionConfirmed);
            EventBus.Subscribe<GameEvents.CardReturnedToHand>(OnCardReturned);

            // Load content for hand renderers
            _leftHandRenderer.LoadContent();
            _rightHandRenderer.LoadContent();

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
            _playerEntity = new CombatEntity(_gameState.PlayerEntityId, spriteManager.PlayerSprite);

            CalculateLayouts();
            _lastKnownBounds = ServiceLocator.Get<Core>().GetActualScreenVirtualBounds();

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

            // Layout Player (position only, as it is not rendered)
            if (_playerEntity != null)
            {
                float playerX = actualScreenVirtualBounds.Center.X;
                float playerY = actualScreenVirtualBounds.Bottom; // Player anchor is at the bottom
                _playerEntity.SetLayout(new Vector2(playerX, playerY), Point.Zero);
            }

            // Calculate dynamic anchor points for hands using the centralized calculator.
            AnimationAnchors = AnimationAnchorCalculator.CalculateAnchors(isEditor: false, out _);

            // Now that anchors are calculated, tell the hands to enter the scene at their correct idle positions.
            _leftHandRenderer.SetIdlePosition(AnimationAnchors["LeftHandIdle"]);
            _rightHandRenderer.SetIdlePosition(AnimationAnchors["RightHandIdle"]);
            _leftHandRenderer.SetOffscreenPosition(AnimationAnchors["LeftHandOffscreen"]);
            _rightHandRenderer.SetOffscreenPosition(AnimationAnchors["RightHandOffscreen"]);
            _leftHandRenderer.EnterScene();
            _rightHandRenderer.EnterScene();

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
                    enemy.SetLayout(new Vector2(x, ENEMY_BASE_Y), ENEMY_BASE_SIZE);
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

                    enemy.SetLayout(new Vector2(x, y), ENEMY_BASE_SIZE);
                }
            }

            // Finally, sort by Y-position for correct draw order (back to front).
            _enemies.Sort((a, b) => a.Position.Y.CompareTo(b.Position.Y));
        }


        public override void Exit()
        {
            base.Exit();
            EventBus.Unsubscribe<GameEvents.PlayerActionConfirmed>(OnPlayerActionConfirmed);
            EventBus.Unsubscribe<GameEvents.CardReturnedToHand>(OnCardReturned);

            // Unsubscribe from health events to prevent memory leaks
            _playerEntity?.UnsubscribeEvents();
            foreach (var enemy in _enemies)
            {
                enemy.UnsubscribeEvents();
            }
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

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            // Check for resolution changes to trigger layout recalculation.
            var currentBounds = ServiceLocator.Get<Core>().GetActualScreenVirtualBounds();
            if (currentBounds != _lastKnownBounds)
            {
                CalculateLayouts();
                _lastKnownBounds = currentBounds;
            }

            _indicatorAnimationTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_indicatorAnimationTimer >= TARGET_INDICATOR_BOB_DURATION)
            {
                _indicatorAnimationTimer -= TARGET_INDICATOR_BOB_DURATION;
            }

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

            // Update the action animator
            _actionAnimator.Update(gameTime);

            // The CombatManager's FSM now drives the logic flow.
            _combatManager.Update(gameTime);

            // Update hand renderers
            _leftHandRenderer.Update(gameTime);
            _rightHandRenderer.Update(gameTime);

            // Enemy animations and other scene-specific visuals are updated here.
            // The FSM controls when the player can interact.
            foreach (var enemy in _enemies)
            {
                enemy.Update(gameTime);
            }
            _playerEntity?.Update(gameTime);
        }

        public override void DrawFullscreenUI(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            var currentPose = _actionAnimator.CurrentPose;

            // --- Pass 1: Draw Hands Behind Particles ---
            spriteBatch.Begin(sortMode: SpriteSortMode.Deferred, blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp, transformMatrix: transform);
            if (currentPose != null)
            {
                if (currentPose.LeftHand.RenderLayer == RenderLayer.BehindParticles)
                {
                    _leftHandRenderer.Draw(spriteBatch, font, gameTime);
                }
                if (currentPose.RightHand.RenderLayer == RenderLayer.BehindParticles)
                {
                    _rightHandRenderer.Draw(spriteBatch, font, gameTime);
                }
            }
            spriteBatch.End();

            // --- Pass 2: Draw Particle Effects ---
            _particleSystemManager.Draw(spriteBatch, transform);

            // --- Pass 3: Draw Hands In Front of Particles ---
            spriteBatch.Begin(sortMode: SpriteSortMode.Deferred, blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp, transformMatrix: transform);
            if (currentPose != null)
            {
                if (currentPose.LeftHand.RenderLayer == RenderLayer.InFrontOfParticles)
                {
                    _leftHandRenderer.Draw(spriteBatch, font, gameTime);
                }
                if (currentPose.RightHand.RenderLayer == RenderLayer.InFrontOfParticles)
                {
                    _rightHandRenderer.Draw(spriteBatch, font, gameTime);
                }
            }
            // If no pose is active, draw both hands by default (they will be idle)
            else
            {
                _leftHandRenderer.Draw(spriteBatch, font, gameTime);
                _rightHandRenderer.Draw(spriteBatch, font, gameTime);
            }
            spriteBatch.End();


            // --- Pass 4: Draw Card UI (Manages its own batches) ---
            _actionHandUI.Draw(spriteBatch, font, gameTime, _inputHandler.DraggedCard, _playingCards, transform);

            // --- Pass 5: Draw Overlays ---
            spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp, transformMatrix: transform);
            DrawTargetingIndicators(spriteBatch);
            spriteBatch.End();
        }

        protected override void DrawSceneContent(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            // This method now only draws the in-world elements that should be letterboxed.
            foreach (var enemy in _enemies)
            {
                enemy.Draw(spriteBatch);
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
            // This block handles the targeting indicator during the ACTION SELECTION phase.
            if (_combatManager.FSM.CurrentState is ActionSelectionState && _inputHandler.DraggedCard != null)
            {
                if (!PlayArea.Contains(_inputHandler.VirtualMousePosition)) return;

                var actionType = _inputHandler.DraggedCard.Action.TargetType;
                if (actionType == TargetType.AllEnemies)
                {
                    foreach (var enemy in _enemies.Where(e => e.IsTargeted)) DrawTargetIndicator(spriteBatch, enemy);
                }
                else if (actionType == TargetType.SingleEnemy && _inputHandler.PotentialTargetId.HasValue)
                {
                    var targetEnemy = _enemies.FirstOrDefault(e => e.EntityId == _inputHandler.PotentialTargetId.Value);
                    if (targetEnemy != null) DrawTargetIndicator(spriteBatch, targetEnemy);
                }
                else if (actionType == TargetType.Self)
                {
                    DrawSelfCastIndicator(spriteBatch);
                }
            }
            // This block handles the targeting indicator during the ACTION EXECUTION phase.
            else if (_combatManager.FSM.CurrentState is ActionExecutionState && CurrentExecutingAction != null)
            {
                foreach (var targetId in CurrentExecutingAction.TargetEntityIds)
                {
                    var targetEnemy = _enemies.FirstOrDefault(e => e.EntityId == targetId);
                    if (targetEnemy != null) DrawTargetIndicator(spriteBatch, targetEnemy);
                }
            }
        }

        private void DrawTargetIndicator(SpriteBatch spriteBatch, CombatEntity target)
        {
            var pixel = ServiceLocator.Get<Texture2D>();

            // Calculate rigid bobbing offset
            float yOffset = 0;
            if (_indicatorAnimationTimer < TARGET_INDICATOR_BOB_DURATION / 2f)
            {
                yOffset = -TARGET_INDICATOR_BOB_DISTANCE;
            }

            // Calculate position above the target's sprite
            Vector2 indicatorPosition = new Vector2(
                target.Bounds.Center.X - TARGET_INDICATOR_SIZE.X / 2f,
                target.Bounds.Top - TARGET_INDICATOR_SIZE.Y - TARGET_INDICATOR_Y_OFFSET + yOffset
            );

            var indicatorRect = new Rectangle((int)indicatorPosition.X, (int)indicatorPosition.Y, TARGET_INDICATOR_SIZE.X, TARGET_INDICATOR_SIZE.Y);

            spriteBatch.Draw(pixel, indicatorRect, TARGET_INDICATOR_COLOR);
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
            int numDots = (int)(length / SELF_CAST_DOT_SIZE) + 1;
            int dotOffset = (int)(_indicatorAnimationTimer * POINTER_ANTS_SPEED);

            for (int i = 0; i < numDots; i++)
            {
                int effectiveIndex = i - dotOffset;
                if (((effectiveIndex % POINTER_ANTS_SPACING) + POINTER_ANTS_SPACING) % POINTER_ANTS_SPACING != 0)
                {
                    continue;
                }

                float distanceAlongLine = i * SELF_CAST_DOT_SIZE;
                if (distanceAlongLine > length) break;

                Vector2 pointOnLine = start + direction * distanceAlongLine;
                spriteBatch.Draw(pixel, pointOnLine, null, Color.Yellow, 0f, new Vector2(0.5f), SELF_CAST_DOT_SIZE, SpriteEffects.None, 0f);
            }
        }

        #region Public Accessors for InputHandler
        public CombatEntity FindClosestEnemyTo(Vector2 position)
        {
            var componentStore = ServiceLocator.Get<ComponentStore>();
            return _enemies
                .Where(e => componentStore.GetComponent<HealthComponent>(e.EntityId)?.CurrentHealth > 0)
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
            var componentStore = ServiceLocator.Get<ComponentStore>();
            return _enemies
                .Where(e => componentStore.GetComponent<HealthComponent>(e.EntityId)?.CurrentHealth > 0)
                .Select(e => e.EntityId)
                .ToList();
        }

        public List<CombatEntity> GetAllCombatEntities()
        {
            var allCombatants = new List<CombatEntity>(_enemies);
            if (_playerEntity != null)
            {
                allCombatants.Add(_playerEntity);
            }
            return allCombatants;
        }
        #endregion

        public override Rectangle GetAnimatedBounds()
        {
            return new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);
        }
    }
}
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Dice;
using ProjectVagabond.Particles;
using ProjectVagabond.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ProjectVagabond.Scenes
{
    public class TerminalMapScene : GameScene
    {
        private readonly GameState _coreState;
        private readonly SceneManager _sceneManager;
        private readonly ClockRenderer _clockRenderer;
        private readonly SpriteManager _spriteManager;
        private readonly WorldClockManager _worldClockManager;
        private readonly InputHandler _inputHandler;
        private readonly MapInputHandler _mapInputHandler;
        private readonly MapRenderer _mapRenderer;
        private readonly StatsRenderer _statsRenderer;
        private readonly HapticsManager _hapticsManager;
        private readonly TerminalRenderer _terminalRenderer;
        private readonly AutoCompleteManager _autoCompleteManager;
        private readonly ParticleSystemManager _particleSystemManager;
        private readonly DiceRollingSystem _diceRollingSystem;
        private WaitDialog _waitDialog;
        private ImageButton _settingsButton;
        private TurnOrderPanel _turnOrderPanel;
        private PlayerStatusPanel _playerStatusPanel;
        private EnemyDisplayPanel _enemyDisplayPanel;
        private CombatLogPanel _combatLogPanel;
        private ActionMenuPanel _actionMenuPanel;
        private PlayerCombatInputSystem _playerCombatInputSystem;
        private KeyboardState _previousKeyboardState;
        private readonly CombatUIAnimationManager _combatUIAnimationManager;
        private readonly LoadingScreen _loadingScreen;
        private bool _isInitialLoad = true;

        public TerminalMapScene()
        {
            _coreState = ServiceLocator.Get<GameState>();
            _sceneManager = ServiceLocator.Get<SceneManager>();
            _clockRenderer = ServiceLocator.Get<ClockRenderer>();
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _worldClockManager = ServiceLocator.Get<WorldClockManager>();
            _inputHandler = ServiceLocator.Get<InputHandler>();
            _mapInputHandler = ServiceLocator.Get<MapInputHandler>();
            _mapRenderer = ServiceLocator.Get<MapRenderer>();
            _statsRenderer = ServiceLocator.Get<StatsRenderer>();
            _hapticsManager = ServiceLocator.Get<HapticsManager>();
            _terminalRenderer = ServiceLocator.Get<TerminalRenderer>();
            _autoCompleteManager = ServiceLocator.Get<AutoCompleteManager>();
            _particleSystemManager = ServiceLocator.Get<ParticleSystemManager>();
            _diceRollingSystem = ServiceLocator.Get<DiceRollingSystem>();
            _combatUIAnimationManager = ServiceLocator.Get<CombatUIAnimationManager>();
            _loadingScreen = new LoadingScreen();

            EventBus.Subscribe<GameEvents.EntityTookDamage>(OnEntityTookDamage);
        }

        private void OnEntityTookDamage(GameEvents.EntityTookDamage e)
        {
            // If the player is the one taking damage, trigger a more intense shake.
            if (e.EntityId == _coreState.PlayerEntityId)
            {
                _hapticsManager.TriggerShake(10.0f, 0.35f);
            }
            else
            {
                // If an enemy takes damage, trigger the requested shake for attack feedback.
                _hapticsManager.TriggerShake(6, 0.35f);
            }
        }

        public override void Enter()
        {
            base.Enter();
            _core.IsMouseVisible = true;
            _waitDialog = new WaitDialog(this);
            _clockRenderer.OnClockClicked += ShowWaitDialog;
            _diceRollingSystem.OnRollCompleted += OnDiceRollCompleted;

            if (_isInitialLoad)
            {
                _isInitialLoad = false;
                _loadingScreen.AddTask(new DiceWarmupTask());
                _loadingScreen.Start();
            }

            if (_settingsButton == null)
            {
                var settingsIcon = _spriteManager.SettingsIconSprite;
                var buttonSize = 16;
                if (settingsIcon != null) buttonSize = Math.Max(settingsIcon.Width, settingsIcon.Height);
                _settingsButton = new ImageButton(new Rectangle(5, 5, buttonSize, buttonSize), settingsIcon);
            }
            _settingsButton.OnClick += OpenSettings;

            // --- Combat UI Panel Initialization and Layout ---
            if (_playerStatusPanel == null)
            {
                // Calculate reference bounds based on the new map layout
                int mapTotalWidth = (int)(Global.VIRTUAL_WIDTH * Global.MAP_AREA_WIDTH_PERCENT);
                int mapX = (Global.VIRTUAL_WIDTH - mapTotalWidth) / 2;
                int mapSize = Math.Min(mapTotalWidth, Global.VIRTUAL_HEIGHT - Global.MAP_TOP_PADDING - Global.TERMINAL_AREA_HEIGHT - 10);
                var mapBounds = new Rectangle(mapX, Global.MAP_TOP_PADDING, mapSize, mapSize);

                // Left Column
                int leftColumnWidth = mapBounds.X - 20; // 10px padding on each side
                var playerStatusBounds = new Rectangle(10, Global.MAP_TOP_PADDING, leftColumnWidth, 140);
                _playerStatusPanel = new PlayerStatusPanel(playerStatusBounds);

                // Right Column
                int rightColumnX = mapBounds.Right + 10;
                int rightColumnWidth = Global.VIRTUAL_WIDTH - rightColumnX - 10;
                var enemyDisplayBounds = new Rectangle(rightColumnX, Global.MAP_TOP_PADDING, rightColumnWidth, 120);
                _enemyDisplayPanel = new EnemyDisplayPanel(enemyDisplayBounds);

                var turnOrderPosition = new Vector2(rightColumnX, enemyDisplayBounds.Bottom + 10);
                _turnOrderPanel = new TurnOrderPanel(turnOrderPosition, rightColumnWidth, 10);

                // Bottom Area (Terminal Area)
                int bottomAreaY = mapBounds.Bottom + 10;
                int bottomAreaHeight = Global.VIRTUAL_HEIGHT - bottomAreaY - 10;
                int bottomAreaWidth = mapBounds.Width;
                int actionMenuWidth = (int)(bottomAreaWidth * 0.4f) - 5;
                int combatLogWidth = bottomAreaWidth - actionMenuWidth - 10;

                var actionMenuBounds = new Rectangle(mapBounds.X, bottomAreaY, actionMenuWidth, bottomAreaHeight);
                _actionMenuPanel = new ActionMenuPanel(actionMenuBounds);

                var combatLogBounds = new Rectangle(actionMenuBounds.Right + 10, bottomAreaY, combatLogWidth, bottomAreaHeight);
                _combatLogPanel = new CombatLogPanel(combatLogBounds);

                // Initialize the combat input system with the correct panels
                _playerCombatInputSystem = new PlayerCombatInputSystem(_actionMenuPanel, _turnOrderPanel, _enemyDisplayPanel, _mapRenderer);
            }
            _previousKeyboardState = Keyboard.GetState();
        }

        public override void Exit()
        {
            base.Exit();
            _clockRenderer.OnClockClicked -= ShowWaitDialog;
            if (_settingsButton != null) _settingsButton.OnClick -= OpenSettings;
            EventBus.Unsubscribe<GameEvents.EntityTookDamage>(OnEntityTookDamage);
            _diceRollingSystem.OnRollCompleted -= OnDiceRollCompleted;
        }

        private void OnDiceRollCompleted(DiceRollResult result)
        {
            foreach (var groupResult in result.ResultsByGroup)
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{groupResult.Key} dice : Sum = {groupResult.Value.Sum()}" });
            }


        }

        private void OpenSettings()
        {
            var settingsScene = _sceneManager.GetScene(GameSceneState.Settings) as SettingsScene;
            if (settingsScene != null) settingsScene.ReturnScene = GameSceneState.TerminalMap;
            _sceneManager.LastInputDevice = InputDevice.Mouse;
            _sceneManager.ChangeScene(GameSceneState.Settings);
        }

        private void ShowWaitDialog()
        {
            if (_waitDialog.IsActive || _worldClockManager.IsInterpolatingTime) return;
            _waitDialog.Show((hours, minutes, seconds) =>
            {
                double totalSeconds = (hours * 3600) + (minutes * 60) + seconds;
                if (totalSeconds > 0)
                {
                    _coreState.CancelExecutingActions();
                    // The real-world duration is proportional to the in-game duration, but capped.
                    float realDuration = Math.Clamp((float)totalSeconds * 0.1f, 0.5f, 5.0f);
                    _worldClockManager.PassTime(totalSeconds, realDuration, ActivityType.Waiting);
                }
            });
        }

        public override void Update(GameTime gameTime)
        {
            if (_loadingScreen.IsActive)
            {
                _loadingScreen.Update(gameTime);
                // The dice system needs to update for the warmup roll to simulate
                _diceRollingSystem.Update(gameTime);
                return; // Block all other updates
            }

            var currentKeyboardState = Keyboard.GetState();
            _diceRollingSystem.Update(gameTime);

            var font = ServiceLocator.Get<BitmapFont>();
            _waitDialog.Update(gameTime);
            if (_waitDialog.IsActive) return;

            // Calculate terminal bounds once for the update frame
            int mapTotalWidth = (int)(Global.VIRTUAL_WIDTH * Global.MAP_AREA_WIDTH_PERCENT);
            int mapX = (Global.VIRTUAL_WIDTH - mapTotalWidth) / 2;
            int mapSize = Math.Min(mapTotalWidth, Global.VIRTUAL_HEIGHT - Global.MAP_TOP_PADDING - Global.TERMINAL_AREA_HEIGHT - 10);
            var mapBounds = new Rectangle(mapX, Global.MAP_TOP_PADDING, mapSize, mapSize);
            int terminalY = mapBounds.Bottom + 10;
            int terminalHeight = Global.VIRTUAL_HEIGHT - terminalY - 10;
            var terminalBounds = new Rectangle(mapBounds.X, terminalY, mapBounds.Width, terminalHeight);

            // The input handler must run every frame to catch the unpause command.
            // It has its own internal logic to halt other inputs when paused.
            _inputHandler.HandleInput(gameTime, terminalBounds);

            // UI elements that should remain interactive while paused are updated here.
            var currentMouseState = Mouse.GetState();
            _settingsButton?.Update(currentMouseState);
            _clockRenderer.Update(gameTime); // Updates the clock's buttons

            // If the game is paused, halt all other scene-specific updates.
            if (_coreState.IsPaused)
            {
                _previousKeyboardState = currentKeyboardState;
                return;
            }

            if (_coreState.IsInCombat)
            {
                // These panels must always update during combat.
                _playerCombatInputSystem.ProcessInput();
                _actionMenuPanel.Update(gameTime, currentMouseState, font);
                _enemyDisplayPanel.Update(gameTime, currentMouseState);

                // Block TurnOrderPanel during any focus state.
                if (_coreState.UIState != CombatUIState.SelectTarget && _coreState.UIState != CombatUIState.SelectMove)
                {
                    _turnOrderPanel.Update(gameTime, currentMouseState, font);
                }

                // Handle MapRenderer update separately. It should NOT update during target selection.
                if (_coreState.UIState != CombatUIState.SelectTarget)
                {
                    _mapRenderer.Update(gameTime, font);
                }
            }
            else
            {
                // Standard out-of-combat updates
                _mapInputHandler.Update(gameTime);
                _mapRenderer.Update(gameTime, font);
                _statsRenderer.Update(gameTime);
            }

            _hapticsManager.Update(gameTime);
            _worldClockManager.Update(gameTime);
            _previousKeyboardState = currentKeyboardState;
        }

        public override void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            if (_loadingScreen.IsActive)
            {
                spriteBatch.Begin(samplerState: SamplerState.PointClamp);
                _loadingScreen.Draw(spriteBatch, font);
                spriteBatch.End();
                return; // Block all other drawing
            }

            var currentMouseState = Mouse.GetState();

            spriteBatch.Begin(samplerState: SamplerState.PointClamp);

            // --- Draw World/Map Content ---
            if (_coreState.CurrentMapView == MapView.Local)
            {
                _mapRenderer.DrawLocalMapBackground(spriteBatch, font, gameTime);

                spriteBatch.End();
                _particleSystemManager.Draw(spriteBatch);
                spriteBatch.Begin(samplerState: SamplerState.PointClamp);

                _mapRenderer.DrawLocalMapEntities(spriteBatch, font, gameTime);
            }
            else // World Map
            {
                _mapRenderer.DrawMap(spriteBatch, font, gameTime);
            }

            // --- Draw UI Panels ---
            if (_coreState.IsInCombat)
            {
                _enemyDisplayPanel.Draw(spriteBatch, gameTime, currentMouseState);
                _turnOrderPanel.Draw(spriteBatch, font, gameTime);
                _playerStatusPanel.Draw(spriteBatch, font);
                _actionMenuPanel.Draw(spriteBatch, font, gameTime);
                _combatLogPanel.Draw(spriteBatch, font, gameTime);

                // Draw focus effect for target or move selection
                if (_coreState.UIState == CombatUIState.SelectTarget || _coreState.UIState == CombatUIState.SelectMove)
                {
                    var pixel = ServiceLocator.Get<Texture2D>();
                    var screenBounds = new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);
                    spriteBatch.Draw(pixel, screenBounds, Color.Black * 0.6f);

                    // Redraw the important panels on top of the overlay
                    if (_coreState.UIState == CombatUIState.SelectTarget)
                    {
                        _enemyDisplayPanel.Draw(spriteBatch, gameTime, currentMouseState);

                        // If an enemy is hovered in the panel, highlight them on the map.
                        if (_enemyDisplayPanel.HoveredEnemyId.HasValue)
                        {
                            int hoveredId = _enemyDisplayPanel.HoveredEnemyId.Value;
                            var componentStore = ServiceLocator.Get<ComponentStore>();
                            var localPosComp = componentStore.GetComponent<LocalPositionComponent>(hoveredId);
                            var renderComp = componentStore.GetComponent<RenderableComponent>(hoveredId);

                            if (localPosComp != null && renderComp != null)
                            {
                                Vector2? screenPos = _mapRenderer.MapCoordsToScreen(localPosComp.LocalPosition);
                                if (screenPos.HasValue)
                                {
                                    int cellSize = _mapRenderer.CellSize;
                                    var destRect = new Rectangle((int)screenPos.Value.X, (int)screenPos.Value.Y, cellSize, cellSize);
                                    spriteBatch.Draw(renderComp.Texture ?? pixel, destRect, renderComp.Color);

                                    // Draw the pulsing red selector
                                    bool isInflated = _combatUIAnimationManager.IsPulsing("TargetSelector");
                                    var global = ServiceLocator.Get<Global>();
                                    Rectangle highlightRect = isInflated
                                        ? new Rectangle(destRect.X - 1, destRect.Y - 1, destRect.Width + 2, destRect.Height + 2)
                                        : destRect;
                                    DrawHollowRectangle(spriteBatch, highlightRect, global.Palette_Red, 1);
                                }
                            }
                        }
                    }
                    else // Must be SelectMove
                    {
                        // Redraw the map with entities to see where you're moving
                        _mapRenderer.DrawLocalMapBackground(spriteBatch, font, gameTime);
                        _mapRenderer.DrawLocalMapEntities(spriteBatch, font, gameTime);
                    }
                    _actionMenuPanel.Draw(spriteBatch, font, gameTime);
                }
            }
            else
            {
                // Calculate terminal bounds based on the map's position and size
                var mapBounds = _mapRenderer.MapScreenBounds;
                int terminalY = mapBounds.Bottom + 10; // 10px gap
                int terminalHeight = Global.VIRTUAL_HEIGHT - terminalY - 10; // 10px bottom padding
                var terminalBounds = new Rectangle(mapBounds.X, terminalY, mapBounds.Width, terminalHeight);

                _terminalRenderer.DrawTerminal(spriteBatch, font, gameTime, terminalBounds);

                // --- Side Column Layout ---
                int leftColumnWidth = mapBounds.X;
                int rightColumnX = mapBounds.Right;
                int rightColumnWidth = Global.VIRTUAL_WIDTH - rightColumnX;

                // Draw Stats in Left Column
                if (leftColumnWidth > 20) // Ensure there's space for padding
                {
                    _statsRenderer.DrawStats(spriteBatch, font, new Vector2(10, Global.MAP_TOP_PADDING), leftColumnWidth - 20);
                }

                // Draw Clock and Settings in Right Column
                if (rightColumnWidth > 20)
                {
                    // Settings Button
                    if (_settingsButton != null)
                    {
                        int settingsButtonX = rightColumnX + (rightColumnWidth - _settingsButton.Bounds.Width) / 2;
                        _settingsButton.Bounds = new Rectangle(settingsButtonX, Global.MAP_TOP_PADDING, _settingsButton.Bounds.Width, _settingsButton.Bounds.Height);
                        _settingsButton.Draw(spriteBatch, font, gameTime);
                    }

                    // Clock
                    int clockX = rightColumnX + (rightColumnWidth - 64) / 2;
                    _clockRenderer.DrawClock(spriteBatch, font, gameTime, new Vector2(clockX, Global.MAP_TOP_PADDING + 30));
                }

                // Draw Prompt/Status in Bottom-Left
                DrawPromptAndStatus(spriteBatch, font);
            }

            spriteBatch.End();

            // Draw autocomplete on top of everything else
            if (!_coreState.IsInCombat && _autoCompleteManager.ShowingAutoCompleteSuggestions)
            {
                spriteBatch.Begin(samplerState: SamplerState.PointClamp);
                _terminalRenderer.DrawAutoComplete(spriteBatch, font);
                spriteBatch.End();
            }

            _waitDialog.Draw(spriteBatch, font, gameTime);
        }

        private void DrawPromptAndStatus(SpriteBatch spriteBatch, BitmapFont font)
        {
            var global = ServiceLocator.Get<Global>();
            var statusText = _terminalRenderer.CachedStatusText;
            var promptLines = _terminalRenderer.WrappedPromptLines;

            float y = Global.VIRTUAL_HEIGHT - 10 - font.LineHeight;

            // Draw prompt lines from bottom up
            if (promptLines != null)
            {
                for (int i = promptLines.Count - 1; i >= 0; i--)
                {
                    var line = promptLines[i];
                    float x = 10;
                    foreach (var segment in line.Segments)
                    {
                        spriteBatch.DrawString(font, segment.Text, new Vector2(x, y), segment.Color);
                        x += font.MeasureString(segment.Text).Width;
                    }
                    y -= Global.PROMPT_LINE_SPACING;
                }
            }

            // Draw status text above the prompt
            if (!string.IsNullOrEmpty(statusText))
            {
                y -= (Global.TERMINAL_LINE_SPACING - Global.PROMPT_LINE_SPACING); // Adjust spacing
                spriteBatch.DrawString(font, statusText, new Vector2(10, y), global.Palette_LightGray);
            }
        }

        private void DrawHollowRectangle(SpriteBatch spriteBatch, Rectangle rect, Color color, int thickness)
        {
            var pixel = ServiceLocator.Get<Texture2D>();
            // Top
            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Top, rect.Width, thickness), color);
            // Bottom
            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Bottom - thickness, rect.Width, thickness), color);
            // Left
            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Top, thickness, rect.Height), color);
            // Right
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Top, thickness, rect.Height), color);
        }
    }
}
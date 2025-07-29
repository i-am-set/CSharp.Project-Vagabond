using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond
{
    /// <summary>
    /// Handles all player input during combat, processing menu selections and target clicks.
    /// </summary>
    public class PlayerCombatInputSystem
    {
        private readonly GameState _gameState;
        private readonly ComponentStore _componentStore;
        private readonly ArchetypeManager _archetypeManager;
        private readonly EnemyDisplayPanel _enemyDisplayPanel;
        private readonly MapRenderer _mapRenderer;

        private MouseState _previousMouseState;
        private KeyboardState _previousKeyboardState;
        private string _selectedAttackName;
        private float _previewPathCost = 0f;
        private readonly Random _random = new Random();

        public PlayerCombatInputSystem(ActionMenuPanel actionMenuPanel, TurnOrderPanel turnOrderPanel, EnemyDisplayPanel enemyDisplayPanel, MapRenderer mapRenderer)
        {
            _gameState = ServiceLocator.Get<GameState>();
            _componentStore = ServiceLocator.Get<ComponentStore>();
            _archetypeManager = ServiceLocator.Get<ArchetypeManager>();
            _enemyDisplayPanel = enemyDisplayPanel;
            _mapRenderer = mapRenderer;

            actionMenuPanel.OnActionSelected += ProcessMenuCommand;
            turnOrderPanel.OnTargetSelected += SelectTarget;
        }

        public void ProcessInput()
        {
            // Don't process any input if not in combat or not the player's turn.
            if (!_gameState.IsInCombat || _gameState.CurrentTurnEntityId != _gameState.PlayerEntityId)
            {
                _previousMouseState = Mouse.GetState();
                _previousKeyboardState = Keyboard.GetState();
                return;
            }

            var currentMouseState = Mouse.GetState();
            var currentKeyboardState = Keyboard.GetState();

            // Handle Escape key press to cancel an ongoing action or go back.
            if (currentKeyboardState.IsKeyDown(Keys.Escape) && !_previousKeyboardState.IsKeyDown(Keys.Escape))
            {
                // If a move is in progress, cancel it.
                if (_gameState.UIState == CombatUIState.Busy && _componentStore.HasComponent<InterpolationComponent>(_gameState.PlayerEntityId))
                {
                    var actionQueueComp = _componentStore.GetComponent<ActionQueueComponent>(_gameState.PlayerEntityId);
                    if (actionQueueComp != null)
                    {
                        actionQueueComp.ActionQueue.Clear();
                        _gameState.UIState = CombatUIState.Default;
                        EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = "[cancel]Movement will stop after the current step." });
                    }
                    _previousMouseState = currentMouseState;
                    _previousKeyboardState = currentKeyboardState;
                    return;
                }
                // If in a selection state, treat Escape as clicking the "Back" button.
                else if (_gameState.UIState == CombatUIState.SelectMove || _gameState.UIState == CombatUIState.SelectTarget)
                {
                    ProcessMenuCommand("Back");
                    _previousMouseState = currentMouseState;
                    _previousKeyboardState = currentKeyboardState;
                    return;
                }
            }

            // If the UI is busy and the escape key wasn't pressed, do nothing else.
            if (_gameState.UIState == CombatUIState.Busy)
            {
                _previousMouseState = currentMouseState;
                _previousKeyboardState = currentKeyboardState;
                return;
            }

            var virtualMousePos = Core.TransformMouse(currentMouseState.Position).ToPoint();

            // Universal cancel for selection modes
            bool rightClicked = currentMouseState.RightButton == ButtonState.Pressed && _previousMouseState.RightButton == ButtonState.Released;
            if (rightClicked && (_gameState.UIState == CombatUIState.SelectMove || _gameState.UIState == CombatUIState.SelectTarget))
            {
                ProcessMenuCommand("Back");
                _previousMouseState = currentMouseState;
                _previousKeyboardState = currentKeyboardState;
                return; // Exit early
            }

            if (_gameState.UIState == CombatUIState.SelectMove)
            {
                HandleMoveSelection(currentMouseState, currentKeyboardState, virtualMousePos);
            }
            else
            {
                if (_gameState.CombatMovePreviewPath.Any())
                {
                    _gameState.CombatMovePreviewPath.Clear();
                    _previewPathCost = 0f;
                }

                bool leftClicked = currentMouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released;
                if (leftClicked)
                {
                    int? clickedEntityId = _enemyDisplayPanel.GetEnemyIdAt(virtualMousePos);
                    if (clickedEntityId.HasValue)
                    {
                        SelectTarget(clickedEntityId.Value);
                    }
                    else if (_gameState.UIState == CombatUIState.SelectTarget)
                    {
                        // If in target selection mode and the click was not on a valid target,
                        // treat it as a click on the overlay to cancel the action.
                        ProcessMenuCommand("Back");
                    }
                }
            }

            _previousMouseState = currentMouseState;
            _previousKeyboardState = currentKeyboardState;
        }

        private void HandleMoveSelection(MouseState currentMouseState, KeyboardState currentKeyboardState, Point virtualMousePos)
        {
            var playerPosComp = _componentStore.GetComponent<LocalPositionComponent>(_gameState.PlayerEntityId);
            if (playerPosComp == null) return;

            var targetTile = _mapRenderer.ScreenToLocalGrid(virtualMousePos);

            var movementMode = MovementMode.Jog; // Default to Jog
            if (currentKeyboardState.IsKeyDown(Keys.LeftShift) || currentKeyboardState.IsKeyDown(Keys.RightShift))
            {
                movementMode = MovementMode.Run;
            }

            if (targetTile.X >= 0)
            {
                var path = _gameState.GetAffordablePath(_gameState.PlayerEntityId, playerPosComp.LocalPosition, targetTile, movementMode, out _previewPathCost);
                _gameState.CombatMovePreviewPath = path ?? new List<(Vector2, MovementMode)>();
            }
            else
            {
                _gameState.CombatMovePreviewPath.Clear();
                _previewPathCost = 0f;
            }

            bool leftClicked = currentMouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released;
            if (leftClicked && _gameState.CombatMovePreviewPath.Any())
            {
                var playerActionQueue = _componentStore.GetComponent<ActionQueueComponent>(_gameState.PlayerEntityId);
                if (playerActionQueue != null)
                {
                    foreach (var (step, stepMode) in _gameState.CombatMovePreviewPath)
                    {
                        playerActionQueue.ActionQueue.Enqueue(new MoveAction(_gameState.PlayerEntityId, step, stepMode));
                    }
                    _gameState.UIState = CombatUIState.Busy;
                    _gameState.CombatMovePreviewPath.Clear();
                    _previewPathCost = 0f;
                }
            }
            else if (leftClicked)
            {
                EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = "[warning]Cannot move to the selected tile." });
            }
        }

        private void SelectTarget(int targetId)
        {
            _gameState.SelectedTargetId = targetId;
            var archetypeIdComp = _componentStore.GetComponent<ArchetypeIdComponent>(targetId);
            var archetype = _archetypeManager.GetArchetypeTemplate(archetypeIdComp?.ArchetypeId ?? "Unknown");
            EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = $"Player selects target: {archetype?.Name ?? $"Entity {targetId}"}." });

            if (_gameState.UIState == CombatUIState.SelectTarget)
            {
                ExecutePlayerAttack();
            }
        }

        private void ProcessMenuCommand(string command)
        {
            var actionQueue = _componentStore.GetComponent<ActionQueueComponent>(_gameState.PlayerEntityId);
            if (actionQueue == null) return;

            switch (command)
            {
                case "Attack":
                    _selectedAttackName = "Attack"; // Generic name, logic is handled by equipment
                    _gameState.UIState = CombatUIState.SelectTarget;
                    EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = "Player prepares to attack. Now select a target." });
                    break;
                case "Move":
                    _gameState.UIState = CombatUIState.SelectMove;
                    break;
                case "End Turn":
                    actionQueue.ActionQueue.Enqueue(new EndTurnAction(_gameState.PlayerEntityId));
                    _gameState.UIState = CombatUIState.Busy;
                    break;
                case "Flee":
                    AttemptToFlee();
                    break;
                case "Back":
                    ResetToDefaultState();
                    break;
            }
        }

        private void AttemptToFlee()
        {
            var playerStats = _componentStore.GetComponent<StatsComponent>(_gameState.PlayerEntityId);
            var turnStats = _componentStore.GetComponent<TurnStatsComponent>(_gameState.PlayerEntityId);

            if (playerStats == null || turnStats == null) return;

            if (!turnStats.IsPristineForTurn)
            {
                EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = "[warning]Cannot flee after taking an action." });
                return;
            }

            var enemies = _gameState.Combatants.Where(id => id != _gameState.PlayerEntityId).ToList();
            if (!enemies.Any())
            {
                _gameState.EndCombat();
                return;
            }

            int playerAgility = playerStats.Agility;
            int maxEnemyAgility = enemies.Max(id => _componentStore.GetComponent<StatsComponent>(id)?.Agility ?? 0);

            bool success = false;
            if (playerAgility > maxEnemyAgility)
            {
                success = true;
            }
            else
            {
                float failChance = (maxEnemyAgility - playerAgility + 1) * 0.1f;
                if (_random.NextDouble() >= failChance)
                {
                    success = true;
                }
            }

            if (success)
            {
                EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = "[palette_yellow]Player successfully flees from combat!" });
                _gameState.EndCombat();
            }
            else
            {
                EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = "[warning]Player fails to flee!" });
                var actionQueue = _componentStore.GetComponent<ActionQueueComponent>(_gameState.PlayerEntityId);
                actionQueue?.ActionQueue.Enqueue(new EndTurnAction(_gameState.PlayerEntityId));
                _gameState.UIState = CombatUIState.Busy;
            }
        }

        private void ExecutePlayerAttack()
        {
            if (string.IsNullOrEmpty(_selectedAttackName) || !_gameState.SelectedTargetId.HasValue) return;

            var actionQueue = _componentStore.GetComponent<ActionQueueComponent>(_gameState.PlayerEntityId);
            var playerPos = _componentStore.GetComponent<LocalPositionComponent>(_gameState.PlayerEntityId);
            var targetPos = _componentStore.GetComponent<LocalPositionComponent>(_gameState.SelectedTargetId.Value);
            var playerCombatant = _componentStore.GetComponent<CombatantComponent>(_gameState.PlayerEntityId);
            var turnStats = _componentStore.GetComponent<TurnStatsComponent>(_gameState.PlayerEntityId);

            if (actionQueue == null || playerPos == null || targetPos == null || playerCombatant == null || turnStats == null) return;

            if (Vector2.Distance(playerPos.LocalPosition, targetPos.LocalPosition) > playerCombatant.AttackRange)
            {
                EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = $"[warning]Target is out of range!" });
                ResetToDefaultState();
                return;
            }

            actionQueue.ActionQueue.Enqueue(new AttackAction(_gameState.PlayerEntityId, _gameState.SelectedTargetId.Value, _selectedAttackName));

            // Consume the primary action for the turn.
            turnStats.HasPrimaryAction = false;

            _gameState.UIState = CombatUIState.Busy;
        }

        private void ResetToDefaultState()
        {
            _selectedAttackName = null;
            _gameState.UIState = CombatUIState.Default;
            _gameState.SelectedTargetId = null;
            _gameState.CombatMovePreviewPath.Clear();
            _previewPathCost = 0f;
        }

        // This method is no longer needed here as the refund logic is obsolete.
        // It is kept for historical reference but is not called.
        private float CalculatePathCost(IEnumerable<IAction> path, Vector2 startPosition)
        {
            if (path == null || !path.Any())
            {
                return 0f;
            }

            float totalCost = 0f;
            var playerStats = _componentStore.GetComponent<StatsComponent>(_gameState.PlayerEntityId);

            if (playerStats == null)
            {
                Console.WriteLine("[ERROR] PlayerCombatInputSystem.CalculatePathCost: Player is missing StatsComponent.");
                return 0f;
            }

            Vector2 lastKnownPosition = startPosition;

            foreach (var action in path)
            {
                if (action is MoveAction moveAction)
                {
                    Vector2 moveDir = moveAction.Destination - lastKnownPosition;
                    totalCost += _gameState.GetSecondsPassedDuringMovement(playerStats, moveAction.Mode, default, moveDir, true);
                    lastKnownPosition = moveAction.Destination; // Update for the next step in the calculation
                }
            }
            return totalCost;
        }
    }
}
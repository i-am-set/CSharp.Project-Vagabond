using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
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
        private readonly CombatTurnSystem _combatTurnSystem;
        private readonly MapRenderer _mapRenderer;

        private MouseState _previousMouseState;
        private string _selectedAttackName;

        public PlayerCombatInputSystem(ActionMenuPanel actionMenuPanel, TurnOrderPanel turnOrderPanel, MapRenderer mapRenderer)
        {
            // Acquire dependencies from ServiceLocator
            _gameState = ServiceLocator.Get<GameState>();
            _componentStore = ServiceLocator.Get<ComponentStore>();
            _archetypeManager = ServiceLocator.Get<ArchetypeManager>();
            _combatTurnSystem = ServiceLocator.Get<CombatTurnSystem>();
            _mapRenderer = mapRenderer;

            // Subscribe to the UI events
            actionMenuPanel.OnActionSelected += ProcessMenuCommand;
            turnOrderPanel.OnTargetSelected += SelectTarget;
        }

        /// <summary>
        /// Processes mouse clicks for combat actions.
        /// </summary>
        public void ProcessInput()
        {
            // Only process input if it's the player's turn and the UI is not busy.
            if (!_gameState.IsInCombat || _gameState.CurrentTurnEntityId != _gameState.PlayerEntityId || _gameState.UIState == CombatUIState.Busy)
            {
                _previousMouseState = Mouse.GetState();
                return;
            }

            var currentMouseState = Mouse.GetState();
            var virtualMousePos = Core.TransformMouse(currentMouseState.Position).ToPoint();

            if (_gameState.UIState == CombatUIState.SelectMove)
            {
                var playerPosComp = _componentStore.GetComponent<LocalPositionComponent>(_gameState.PlayerEntityId);
                if (playerPosComp != null)
                {
                    var targetTile = _mapRenderer.ScreenToLocalGrid(virtualMousePos);

                    // Update preview path continuously
                    if (targetTile.X >= 0)
                    {
                        bool isRunning = false; // Assume walking for now
                        var path = _gameState.GetAffordablePath(_gameState.PlayerEntityId, playerPosComp.LocalPosition, targetTile, isRunning, GameState.COMBAT_TURN_DURATION_SECONDS, out _);
                        _gameState.CombatMovePreviewPath = path ?? new List<Vector2>();
                    }
                    else
                    {
                        _gameState.CombatMovePreviewPath.Clear();
                    }

                    // Handle click to confirm
                    bool leftClicked = currentMouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released;
                    if (leftClicked && _gameState.CombatMovePreviewPath.Any())
                    {
                        var playerActionQueue = _componentStore.GetComponent<ActionQueueComponent>(_gameState.PlayerEntityId);
                        if (playerActionQueue != null)
                        {
                            bool isRunning = false; // Assume walking
                            foreach (var step in _gameState.CombatMovePreviewPath)
                            {
                                playerActionQueue.ActionQueue.Enqueue(new MoveAction(_gameState.PlayerEntityId, step, isRunning));
                            }

                            // Recalculate final cost for log message
                            _gameState.GetAffordablePath(_gameState.PlayerEntityId, playerPosComp.LocalPosition, targetTile, isRunning, GameState.COMBAT_TURN_DURATION_SECONDS, out float totalTimeCost);
                            EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = $"Player moves. (Time: {totalTimeCost:F1}s)" });

                            _gameState.UIState = CombatUIState.Busy;
                            _gameState.CombatMovePreviewPath.Clear();
                        }
                    }
                    else if (leftClicked) // Clicked on an invalid tile
                    {
                        EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = "[warning]Cannot move to the selected tile." });
                        ResetToDefaultState();
                    }
                }
            }
            else
            {
                // If not in SelectMove state, ensure the preview path is cleared.
                if (_gameState.CombatMovePreviewPath.Any())
                {
                    _gameState.CombatMovePreviewPath.Clear();
                }

                // Handle other input, like selecting a target
                bool leftClicked = currentMouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released;
                if (leftClicked)
                {
                    int? clickedEntityId = _mapRenderer.GetEntityIdAt(virtualMousePos);
                    if (clickedEntityId.HasValue)
                    {
                        SelectTarget(clickedEntityId.Value);
                    }
                }
            }

            _previousMouseState = currentMouseState;
        }

        private void SelectTarget(int targetId)
        {
            _gameState.SelectedTargetId = targetId;
            var archetypeIdComp = _componentStore.GetComponent<ArchetypeIdComponent>(targetId);
            var archetype = _archetypeManager.GetArchetypeTemplate(archetypeIdComp?.ArchetypeId ?? "Unknown");
            EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = $"Player selects target: {archetype?.Name ?? $"Entity {targetId}"}." });

            // If we were waiting to select a target, execute the action now
            if (_gameState.UIState == CombatUIState.SelectTarget)
            {
                ExecutePlayerAttack();
            }
        }

        private void ProcessMenuCommand(string command)
        {
            switch (command)
            {
                case "Attack":
                    _gameState.UIState = CombatUIState.SelectAttack;
                    EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = "Player opens Attack menu." });
                    break;
                case "Skills":
                    EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = "[dim]Skills are not yet implemented." });
                    break;
                case "Move":
                    _gameState.UIState = CombatUIState.SelectMove;
                    EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = "Player is selecting a destination to move." });
                    break;
                case "Item":
                    EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = "[dim]Items are not yet implemented." });
                    break;
                case "End Turn":
                    EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = "Player ends their turn." });
                    _gameState.UIState = CombatUIState.Busy;
                    _combatTurnSystem.EndCurrentTurn();
                    break;
                case "Back":
                    ResetToDefaultState();
                    break;
                default:
                    // If the command is not a main menu option, it must be an attack/skill name.
                    if (_gameState.UIState == CombatUIState.SelectAttack)
                    {
                        _selectedAttackName = command;
                        _gameState.UIState = CombatUIState.SelectTarget;
                        EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = $"Player selects '{command}'. Now select a target." });
                    }
                    break;
            }
        }

        private void ExecutePlayerAttack()
        {
            if (string.IsNullOrEmpty(_selectedAttackName) || !_gameState.SelectedTargetId.HasValue)
            {
                return;
            }

            var playerCombatant = _componentStore.GetComponent<CombatantComponent>(_gameState.PlayerEntityId);
            var playerPos = _componentStore.GetComponent<LocalPositionComponent>(_gameState.PlayerEntityId);
            var targetPos = _componentStore.GetComponent<LocalPositionComponent>(_gameState.SelectedTargetId.Value);

            if (playerCombatant == null || playerPos == null || targetPos == null)
            {
                EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = "[error]Cannot attack. Missing combat components." });
                ResetToDefaultState();
                return;
            }

            float distance = Vector2.Distance(playerPos.LocalPosition, targetPos.LocalPosition);
            if (distance > playerCombatant.AttackRange)
            {
                EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = $"[warning]Target is out of range! (Range: {playerCombatant.AttackRange}, Distance: {distance:F1})" });
                ResetToDefaultState();
                return;
            }

            var playerAttacks = _componentStore.GetComponent<AvailableAttacksComponent>(_gameState.PlayerEntityId);
            var attack = playerAttacks?.Attacks.FirstOrDefault(a => a.Name == _selectedAttackName);

            if (attack == null)
            {
                EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = $"[error]Could not find attack: {_selectedAttackName}" });
                ResetToDefaultState();
                return;
            }

            // Add the component representing the player's intent.
            var chosenAttack = new ChosenAttackComponent
            {
                TargetId = _gameState.SelectedTargetId.Value,
                AttackName = attack.Name
            };
            _componentStore.AddComponent(_gameState.PlayerEntityId, chosenAttack);

            var targetArchetypeIdComp = _componentStore.GetComponent<ArchetypeIdComponent>(chosenAttack.TargetId);
            var targetArchetype = _archetypeManager.GetArchetypeTemplate(targetArchetypeIdComp?.ArchetypeId ?? "Unknown");
            EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = $"Player decides to use {attack.Name} on {targetArchetype?.Name ?? $"Entity {chosenAttack.TargetId}"}." });

            // The player's action is now chosen. Set the UI to busy while the action is processed.
            _gameState.UIState = CombatUIState.Busy;
        }

        private void ResetToDefaultState()
        {
            _selectedAttackName = null;
            _gameState.UIState = CombatUIState.Default;
            _gameState.SelectedTargetId = null; // Also clear the selected target
            _gameState.CombatMovePreviewPath.Clear();
            EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = "[dim]Action cancelled. Returning to main menu." });
        }
    }
}
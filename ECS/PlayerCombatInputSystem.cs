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
        private readonly MapRenderer _mapRenderer;

        private MouseState _previousMouseState;
        private string _selectedAttackName;
        private float _previewPathCost = 0f;

        public PlayerCombatInputSystem(ActionMenuPanel actionMenuPanel, TurnOrderPanel turnOrderPanel, MapRenderer mapRenderer)
        {
            _gameState = ServiceLocator.Get<GameState>();
            _componentStore = ServiceLocator.Get<ComponentStore>();
            _archetypeManager = ServiceLocator.Get<ArchetypeManager>();
            _mapRenderer = mapRenderer;

            actionMenuPanel.OnActionSelected += ProcessMenuCommand;
            turnOrderPanel.OnTargetSelected += SelectTarget;
        }

        public void ProcessInput()
        {
            if (!_gameState.IsInCombat || _gameState.CurrentTurnEntityId != _gameState.PlayerEntityId || _gameState.UIState == CombatUIState.Busy)
            {
                _previousMouseState = Mouse.GetState();
                return;
            }

            var currentMouseState = Mouse.GetState();
            var currentKeyboardState = Keyboard.GetState();
            var virtualMousePos = Core.TransformMouse(currentMouseState.Position).ToPoint();

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
                    int? clickedEntityId = _mapRenderer.GetEntityIdAt(virtualMousePos);
                    if (clickedEntityId.HasValue) SelectTarget(clickedEntityId.Value);
                }
            }

            _previousMouseState = currentMouseState;
        }

        private void HandleMoveSelection(MouseState currentMouseState, KeyboardState currentKeyboardState, Point virtualMousePos)
        {
            var playerPosComp = _componentStore.GetComponent<LocalPositionComponent>(_gameState.PlayerEntityId);
            if (playerPosComp == null) return;

            var targetTile = _mapRenderer.ScreenToLocalGrid(virtualMousePos);
            bool isRunning = !(currentKeyboardState.IsKeyDown(Keys.LeftShift) || currentKeyboardState.IsKeyDown(Keys.RightShift));

            if (targetTile.X >= 0)
            {
                var path = _gameState.GetAffordablePath(_gameState.PlayerEntityId, playerPosComp.LocalPosition, targetTile, isRunning, out _previewPathCost);
                _gameState.CombatMovePreviewPath = path ?? new List<Vector2>();
                _gameState.IsCombatMovePreviewRunning = isRunning;
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
                var playerTurnStats = _componentStore.GetComponent<TurnStatsComponent>(_gameState.PlayerEntityId);

                if (playerActionQueue != null && playerTurnStats != null)
                {
                    playerTurnStats.MovementTimeUsedThisTurn += _previewPathCost;

                    foreach (var step in _gameState.CombatMovePreviewPath)
                    {
                        playerActionQueue.ActionQueue.Enqueue(new MoveAction(_gameState.PlayerEntityId, step, isRunning));
                    }
                    _gameState.UIState = CombatUIState.Busy;
                    _gameState.CombatMovePreviewPath.Clear();
                    _previewPathCost = 0f;
                }
            }
            else if (leftClicked)
            {
                EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = "[warning]Cannot move to the selected tile." });
                ResetToDefaultState();
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
                    _gameState.UIState = CombatUIState.SelectAttack;
                    break;
                case "Move":
                    _gameState.UIState = CombatUIState.SelectMove;
                    break;
                case "End Turn":
                    actionQueue.ActionQueue.Enqueue(new EndTurnAction(_gameState.PlayerEntityId));
                    _gameState.UIState = CombatUIState.Busy;
                    break;
                case "Back":
                    ResetToDefaultState();
                    break;
                default:
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
            _gameState.IsCombatMovePreviewRunning = false;
            _previewPathCost = 0f;
        }
    }
}
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
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
            bool leftClicked = currentMouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released;

            if (leftClicked)
            {
                var virtualMousePos = Core.TransformMouse(currentMouseState.Position).ToPoint();

                // Check for a click on an enemy on the map
                int? clickedEntityId = _mapRenderer.GetEntityIdAt(virtualMousePos);
                if (clickedEntityId.HasValue)
                {
                    SelectTarget(clickedEntityId.Value);
                }
            }

            _previousMouseState = currentMouseState;
        }

        private void SelectTarget(int targetId)
        {
            _gameState.SelectedTargetId = targetId;
            var archetypeIdComp = _componentStore.GetComponent<ArchetypeIdComponent>(targetId);
            var archetype = _archetypeManager.GetArchetype(archetypeIdComp?.ArchetypeId ?? "Unknown");
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
                    EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = "[dim]Moving in combat is not yet implemented." });
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

            var playerStats = _componentStore.GetComponent<CombatStatsComponent>(_gameState.PlayerEntityId);
            var playerAttacks = _componentStore.GetComponent<AvailableAttacksComponent>(_gameState.PlayerEntityId);
            var attack = playerAttacks?.Attacks.FirstOrDefault(a => a.Name == _selectedAttackName);

            if (attack == null)
            {
                EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = $"[error]Could not find attack: {_selectedAttackName}" });
                ResetToDefaultState();
                return;
            }

            if (playerStats.ActionPoints >= attack.ActionPointCost)
            {
                // Add the component representing the player's intent.
                var chosenAttack = new ChosenAttackComponent
                {
                    TargetId = _gameState.SelectedTargetId.Value,
                    AttackName = attack.Name
                };
                _componentStore.AddComponent(_gameState.PlayerEntityId, chosenAttack);

                // Deduct cost
                playerStats.ActionPoints -= attack.ActionPointCost;
                var targetArchetypeIdComp = _componentStore.GetComponent<ArchetypeIdComponent>(chosenAttack.TargetId);
                var targetArchetype = _archetypeManager.GetArchetype(targetArchetypeIdComp?.ArchetypeId ?? "Unknown");
                EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = $"Player decides to use {attack.Name} on {targetArchetype?.Name ?? $"Entity {chosenAttack.TargetId}"}." });

                // The player's action is now chosen. Set the UI to busy while the action is processed.
                _gameState.UIState = CombatUIState.Busy;
            }
            else
            {
                EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = $"[warning]Not enough Action Points to use {attack.Name}. Need {attack.ActionPointCost}, have {playerStats.ActionPoints}." });
                ResetToDefaultState();
            }
        }

        private void ResetToDefaultState()
        {
            _selectedAttackName = null;
            _gameState.UIState = CombatUIState.Default;
            EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = "[dim]Action cancelled. Returning to main menu." });
        }
    }
}
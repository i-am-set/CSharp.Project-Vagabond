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
        private readonly ActionMenuPanel _actionMenuPanel;
        private readonly MapRenderer _mapRenderer;
        private MouseState _previousMouseState;
        private string _selectedAttackName;

        public PlayerCombatInputSystem(ActionMenuPanel actionMenuPanel, MapRenderer mapRenderer)
        {
            _actionMenuPanel = actionMenuPanel;
            _mapRenderer = mapRenderer;
        }

        /// <summary>
        /// Processes mouse clicks for combat actions.
        /// </summary>
        public void ProcessInput()
        {
            var gameState = Core.CurrentGameState;
            // Only process input if it's the player's turn and the UI is not busy.
            if (!gameState.IsInCombat || gameState.CurrentTurnEntityId != gameState.PlayerEntityId || gameState.UIState == CombatUIState.Busy)
            {
                _previousMouseState = Mouse.GetState();
                return;
            }

            var currentMouseState = Mouse.GetState();
            bool leftClicked = currentMouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released;

            if (leftClicked)
            {
                var virtualMousePos = Core.TransformMouse(currentMouseState.Position).ToPoint();

                // 1. Check for a click on an enemy on the map
                int? clickedEntityId = _mapRenderer.GetEntityIdAt(virtualMousePos);
                if (clickedEntityId.HasValue)
                {
                    gameState.SelectedTargetId = clickedEntityId;
                    var archetypeIdComp = Core.ComponentStore.GetComponent<ArchetypeIdComponent>(clickedEntityId.Value);
                    var archetype = ArchetypeManager.Instance.GetArchetype(archetypeIdComp?.ArchetypeId ?? "Unknown");
                    Core.CurrentTerminalRenderer.AddCombatLog($"Player selects target: {archetype?.Name ?? $"Entity {clickedEntityId.Value}"}.");

                    // If we were waiting to select a target, execute the action now
                    if (gameState.UIState == CombatUIState.SelectTarget)
                    {
                        ExecutePlayerAttack();
                        _previousMouseState = currentMouseState;
                        return; // Action taken, end processing for this frame
                    }
                }

                // 2. Check for a click on the action menu
                string command = _actionMenuPanel.HandleInput(virtualMousePos);
                if (command != null)
                {
                    ProcessMenuCommand(command);
                }
            }

            _previousMouseState = currentMouseState;
        }

        private void ProcessMenuCommand(string command)
        {
            var gameState = Core.CurrentGameState;

            switch (command)
            {
                case "Attack":
                    gameState.UIState = CombatUIState.SelectAttack;
                    Core.CurrentTerminalRenderer.AddCombatLog("Player opens Attack menu.");
                    break;
                case "Skills":
                    // gameState.UIState = CombatUIState.SelectSkill; // For later
                    Core.CurrentTerminalRenderer.AddCombatLog("[dim]Skills are not yet implemented.");
                    break;
                case "Move":
                    // gameState.UIState = CombatUIState.SelectMove; // For later
                    Core.CurrentTerminalRenderer.AddCombatLog("[dim]Moving in combat is not yet implemented.");
                    break;
                case "Item":
                    Core.CurrentTerminalRenderer.AddCombatLog("[dim]Items are not yet implemented.");
                    break;
                case "End Turn":
                    Core.CurrentTerminalRenderer.AddCombatLog("Player ends their turn.");
                    gameState.UIState = CombatUIState.Busy;
                    Core.CombatTurnSystem.EndCurrentTurn();
                    break;
                default:
                    // If the command is not a main menu option, it must be an attack/skill name.
                    if (gameState.UIState == CombatUIState.SelectAttack)
                    {
                        _selectedAttackName = command;
                        gameState.UIState = CombatUIState.SelectTarget;
                        Core.CurrentTerminalRenderer.AddCombatLog($"Player selects '{command}'. Now select a target.");
                    }
                    break;
            }
        }

        private void ExecutePlayerAttack()
        {
            var gameState = Core.CurrentGameState;
            if (string.IsNullOrEmpty(_selectedAttackName) || !gameState.SelectedTargetId.HasValue)
            {
                return;
            }

            var playerStats = Core.ComponentStore.GetComponent<CombatStatsComponent>(gameState.PlayerEntityId);
            var playerAttacks = Core.ComponentStore.GetComponent<AvailableAttacksComponent>(gameState.PlayerEntityId);
            var attack = playerAttacks?.Attacks.FirstOrDefault(a => a.Name == _selectedAttackName);

            if (attack == null)
            {
                Core.CurrentTerminalRenderer.AddCombatLog($"[error]Could not find attack: {_selectedAttackName}");
                ResetToDefaultState();
                return;
            }

            if (playerStats.ActionPoints >= attack.ActionPointCost)
            {
                // Add the component representing the player's intent.
                var chosenAttack = new ChosenAttackComponent
                {
                    TargetId = gameState.SelectedTargetId.Value,
                    AttackName = attack.Name
                };
                Core.ComponentStore.AddComponent(gameState.PlayerEntityId, chosenAttack);

                // Deduct cost
                playerStats.ActionPoints -= attack.ActionPointCost;
                var targetArchetypeIdComp = Core.ComponentStore.GetComponent<ArchetypeIdComponent>(chosenAttack.TargetId);
                var targetArchetype = ArchetypeManager.Instance.GetArchetype(targetArchetypeIdComp?.ArchetypeId ?? "Unknown");
                Core.CurrentTerminalRenderer.AddCombatLog($"Player decides to use {attack.Name} on {targetArchetype?.Name ?? $"Entity {chosenAttack.TargetId}"}.");

                // The player's action is now chosen. Set the UI to busy while the action is processed.
                gameState.UIState = CombatUIState.Busy;
            }
            else
            {
                Core.CurrentTerminalRenderer.AddCombatLog($"[warning]Not enough Action Points to use {attack.Name}. Need {attack.ActionPointCost}, have {playerStats.ActionPoints}.");
                ResetToDefaultState();
            }
        }

        private void ResetToDefaultState()
        {
            var gameState = Core.CurrentGameState;
            _selectedAttackName = null;
            gameState.UIState = CombatUIState.Default;
            Core.CurrentTerminalRenderer.AddCombatLog("[dim]Action cancelled. Returning to main menu.");
            // We don't reset SelectedTargetId, as it's useful to keep the last-clicked enemy selected.
        }
    }
}
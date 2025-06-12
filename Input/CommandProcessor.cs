using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond
{
    public class CommandProcessor
    {
        private readonly GameState _gameState = Core.CurrentGameState;
        private Dictionary<string, Action<string[]>> _commands;

        public Dictionary<string, Action<string[]>> Commands => _commands;

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public CommandProcessor()
        {
            InitializeCommands();
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        private void AddToHistory(string message, Color? baseColor = null) => Core.CurrentTerminalRenderer.AddToHistory(message, baseColor);
        private void AddOutputToHistory(string message) => Core.CurrentTerminalRenderer.AddOutputToHistory(message);
        private void AddHelpLineToHistory(string message) => Core.CurrentTerminalRenderer.AddToHistory(message, Global.Instance.palette_LightPurple);
        
        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //
        
        private void InitializeCommands()
        {
            _commands = new Dictionary<string, Action<string[]>>();

            _commands["help"] = (args) =>
            {
                AddToHistory(" ");
                AddToHistory("Available commands:", Global.Instance.palette_DarkPurple);
                AddHelpLineToHistory(" up/down/left/right <count> [gray]- Queue a run (costs energy).");
                // AddHelpLineToHistory(" walk <dir> <count> [gray]- Queue a walk (no energy cost).");
                AddHelpLineToHistory(" rest [short|long] [gray]- Queue a rest action.");
                AddHelpLineToHistory(" look [gray]- Look around current area.");
                AddHelpLineToHistory(" move [gray]- Enable free-move mode (runs by default).");
                AddHelpLineToHistory(" clear [gray]- Clear all pending actions.");
                AddHelpLineToHistory(" pos [gray]- Show current position and queue status.");
                AddHelpLineToHistory(" exit [gray]- Exit the game.");
            };

            _commands["look"] = (args) =>
            {
                int x = (int)_gameState.PlayerWorldPos.X;
                int y = (int)_gameState.PlayerWorldPos.Y;
                float noise = _gameState.GetNoiseAt(x, y);
                string terrain = _gameState.GetTerrainDescription(noise);
                AddOutputToHistory($"You are standing on {terrain}.");
                AddOutputToHistory($"Position: ({x}, {y})");
                AddOutputToHistory($"Terrain value: {noise:F2}");
            };

            _commands["up"] = (args) => _gameState.QueueWalkMovement(new Vector2(0, -1), args);
            _commands["down"] = (args) => _gameState.QueueWalkMovement(new Vector2(0, 1), args);
            _commands["left"] = (args) => _gameState.QueueWalkMovement(new Vector2(-1, 0), args);
            _commands["right"] = (args) => _gameState.QueueWalkMovement(new Vector2(1, 0), args);

            _commands["cancel"] = (args) =>
            {
                if (_gameState.PendingActions.Count > 0)
                {
                    _gameState.CancelPendingActions();
                    AddOutputToHistory("Pending actions canceled.");
                }
                else
                {
                    AddOutputToHistory("No pending actions to cancel.");
                }
            };

            _commands["pos"] = (args) =>
            {
                AddOutputToHistory($"Current position: ({(int)_gameState.PlayerWorldPos.X}, {(int)_gameState.PlayerWorldPos.Y})");
                AddOutputToHistory($"Pending actions in queue: {_gameState.PendingActions.Count}");
                if (_gameState.IsExecutingPath)
                {
                    AddOutputToHistory($"Executing queue: action {_gameState.CurrentPathIndex + 1}/{_gameState.PendingActions.Count}");
                }
            };

            _commands["move"] = (args) =>
            {
                _gameState.ToggleIsFreeMoveMode(true);
            };

            _commands["debugallcolors"] = (args) =>
            {
                DebugAllColors();
            };

            _commands["rest"] = (args) =>
            {
                _gameState.QueueRest(args);
            };

            _commands["debug"] = (args) =>
            {
                Core.Instance.ScreenShake(intensity: 4.0f, duration: 2.2f);
            };

            _commands["exit"] = (args) =>
            {
                Core.Instance.ExitApplication();
            };
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- // 

        private void DebugAllColors()
        {
            AddOutputToHistory("[gray]Displaying all XNA Framework colors:");

            var colorProperties = typeof(Color).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .Where(p => p.PropertyType == typeof(Color))
                .OrderBy(p => p.Name);

            foreach (var property in colorProperties)
            {
                string colorName = property.Name;
                Color color = (Color)property.GetValue(null);

                AddToHistory($"[{colorName.ToLower()}]{colorName}[/]", Global.Instance.OutputTextColor);
            }

            AddOutputToHistory($"[gray]Total colors displayed: {colorProperties.Count()}");
        }
    }
}
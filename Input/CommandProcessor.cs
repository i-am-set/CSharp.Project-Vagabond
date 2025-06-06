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

        public  Dictionary<string, Action<string[]>> Commands => _commands;

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public CommandProcessor()
        {
            InitializeCommands();
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        private void AddToHistory(string message, Color? baseColor = null)
        {
            Core.CurrentTerminalRenderer.AddToHistory(message, baseColor);
        }

        private void AddOutputToHistory(string message)
        {
            Core.CurrentTerminalRenderer.AddOutputToHistory(message);
        }

        private void AddHelpLineToHistory(string message)
        {
            Core.CurrentTerminalRenderer.AddToHistory(message, Color.Violet);
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        private void InitializeCommands()
        {
            _commands = new Dictionary<string, Action<string[]>>();

            _commands["help"] = (args) =>
            {
                AddToHistory(" ");
                AddToHistory("Available commands:", Color.Plum);
                AddHelpLineToHistory(" up/down/left/right <count> [gray]- Queue movement");
                AddHelpLineToHistory(" look [gray]- Look around current area");
                AddHelpLineToHistory(" move [gray]- Queue movement with W/A/S/D or arrow keys");
                AddHelpLineToHistory(" clear [gray]- Clear pending path");
                AddHelpLineToHistory(" pos [gray]- Show current position");
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

            _commands["up"] = (args) => _gameState.QueueMovement(new Vector2(0, -1), args);
            _commands["down"] = (args) => _gameState.QueueMovement(new Vector2(0, 1), args);
            _commands["left"] = (args) => _gameState.QueueMovement(new Vector2(-1, 0), args);
            _commands["right"] = (args) => _gameState.QueueMovement(new Vector2(1, 0), args);

            _commands["clear"] = (args) =>
            {
                if (!_gameState.IsExecutingPath)
                {
                    _gameState.ClearPendingPathPreview();
                    AddOutputToHistory("Pending path cleared.");
                }
                else
                {
                    AddOutputToHistory("Cannot clear path while executing.");
                }
            };

            _commands["pos"] = (args) =>
            {
                AddOutputToHistory($"Current position: ({(int)_gameState.PlayerWorldPos.X}, {(int)_gameState.PlayerWorldPos.Y})");
                AddOutputToHistory($"Pending path steps: {_gameState.PendingPathPreview.Count}");
                if (_gameState.IsExecutingPath)
                {
                    AddOutputToHistory($"Executing path: step {_gameState.CurrentPathIndex + 1}/{_gameState.PendingPathPreview.Count}");
                }
            };

            _commands["move"] = (args) =>
            {
                _gameState.ToggleIsFreeMoveMode(true);
                AddOutputToHistory("[gold]Free move enabled.");
            };

            _commands["debugallcolors"] = (args) =>
            {
                DebugAllColors();
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
    
            var colorProperties = typeof(Color).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static) // Get all static Color properties using reflection
                .Where(p => p.PropertyType == typeof(Color))
                .OrderBy(p => p.Name);
    
            foreach (var property in colorProperties)
            {
                string colorName = property.Name;
                Color color = (Color)property.GetValue(null);
        
                AddToHistory($"[{colorName.ToLower()}]{colorName}[/]", Color.Gray); // Format as [colorname]ColorName[/] to use the color system
            }
    
            AddOutputToHistory($"[gray]Total colors displayed: {colorProperties.Count()}");
        }
    }
}

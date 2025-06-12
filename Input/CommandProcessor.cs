using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond
{
    public class CommandProcessor
    {
        private readonly GameState _gameState = Core.CurrentGameState;
        private Dictionary<string, Command> _commands;

        public Dictionary<string, Command> Commands => _commands;

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
            _commands = new Dictionary<string, Command>();

            _commands["help"] = new Command("help", (args) =>
            {
                AddToHistory(" ");
                AddToHistory("Available commands:", Color.Magenta);
                foreach (var cmd in _commands.Values)
                {
                    // Don't show help for the help command itself, or for debug commands
                    if (cmd.Name == "help" || cmd.Name.StartsWith("debug")) continue;
                    if (!string.IsNullOrEmpty(cmd.HelpText))
                    {
                        AddHelpLineToHistory(cmd.HelpText);
                    }
                }
            }, "help [gray]- Shows help for possible commands.");

            _commands["clear"] = new Command("clear", (args) =>
            {
                Core.CurrentTerminalRenderer.ClearHistory();
                AddOutputToHistory("Terminal history cleared.");
            }, "clear [gray]- Clear the terminal history.");

            _commands["look"] = new Command("look", (args) =>
            {
                int x = (int)_gameState.PlayerWorldPos.X;
                int y = (int)_gameState.PlayerWorldPos.Y;
                float noise = _gameState.GetNoiseAt(x, y);
                string terrain = _gameState.GetTerrainDescription(noise);
                AddOutputToHistory($"You are standing on {terrain}.");
                AddOutputToHistory($"Position: ({x}, {y})");
                AddOutputToHistory($"Terrain value: {noise:F2}");
            }, "look [gray]- Look around current area.");

            _commands["run"] = new Command("run", (args) =>
            {
                if (args.Length < 2)
                {
                    AddOutputToHistory("[error]No run direction given (up, down, left, right).");
                    return;
                }
                string direction = args[1].ToLower();
                string[] movementArgs = args.Skip(1).ToArray();
                switch (direction)
                {
                    case "up": _gameState.QueueWalkMovement(new Vector2(0, -1), movementArgs); break;
                    case "down": _gameState.QueueWalkMovement(new Vector2(0, 1), movementArgs); break;
                    case "left": _gameState.QueueWalkMovement(new Vector2(-1, 0), movementArgs); break;
                    case "right": _gameState.QueueWalkMovement(new Vector2(1, 0), movementArgs); break;
                    default: AddOutputToHistory($"Unknown direction for run: '{direction}'."); break;
                }
            },
            "run <dir> <count?> [gray]- Queue a run (costs energy, but quicker).",
            (args) => {
                if (args.Length == 0) return new List<string> { "up", "down", "left", "right" };
                return new List<string>();
            });

            _commands["walk"] = new Command("walk", (args) =>
            {
                if (args.Length < 2)
                {
                    AddOutputToHistory("[error]No walk direction given (up, down, left, right).");
                    return;
                }
                string direction = args[1].ToLower();
                string[] movementArgs = args.Skip(1).ToArray();
                switch (direction)
                {
                    case "up": _gameState.QueueWalkMovement(new Vector2(0, -1), movementArgs); break;
                    case "down": _gameState.QueueWalkMovement(new Vector2(0, 1), movementArgs); break;
                    case "left": _gameState.QueueWalkMovement(new Vector2(-1, 0), movementArgs); break;
                    case "right": _gameState.QueueWalkMovement(new Vector2(1, 0), movementArgs); break;
                    default: AddOutputToHistory($"Unknown direction for run: '{direction}'."); break;
                }
            },
            "walk <dir> <count?> [gray]- Queue a walk.",
            (args) => {
                if (args.Length == 0) return new List<string> { "up", "down", "left", "right" };
                return new List<string>();
            });

            _commands["up"] = new Command("up", (args) => { _gameState.QueueWalkMovement(new Vector2(0, -1), args.ToArray()); }, "walk up <count?> [gray]- Queue a walk up.");
            _commands["down"] = new Command("down", (args) => { _gameState.QueueWalkMovement(new Vector2(0, 1), args.ToArray()); }, "walk down <count?> [gray]- Queue a walk down.");
            _commands["left"] = new Command("left", (args) => { _gameState.QueueWalkMovement(new Vector2(-1, 0), args.ToArray()); }, "walk left <count?> [gray]- Queue a walk left.");
            _commands["right"] = new Command("right", (args) => { _gameState.QueueWalkMovement(new Vector2(1, 0), args.ToArray()); }, "walk right <count?> [gray]- Queue a walk right.");

            _commands["cancel"] = new Command("cancel", (args) =>
            {
                if (_gameState.PendingActions.Count > 0)
                {
                    _gameState.CancelPendingActions();
                    AddOutputToHistory("Pending actions cleared.");
                }
                else
                {
                    AddOutputToHistory("No pending actions to clear.");
                }
            }, "cancel [gray]- Cancel all pending actions.");

            _commands["pos"] = new Command("pos", (args) =>
            {
                AddOutputToHistory($"Current position: ({(int)_gameState.PlayerWorldPos.X}, {(int)_gameState.PlayerWorldPos.Y})");
                AddOutputToHistory($"Pending actions in queue: {_gameState.PendingActions.Count}");
                if (_gameState.IsExecutingPath)
                {
                    AddOutputToHistory($"Executing queue: action {_gameState.CurrentPathIndex + 1}/{_gameState.PendingActions.Count}");
                }
            }, "pos [gray]- Show current position and queue status.");

            _commands["move"] = new Command("move", (args) =>
            {
                _gameState.ToggleIsFreeMoveMode(true);
            }, "move [gray]- Enable free-move mode (W/A/S/D to queue movement).");

            _commands["debugallcolors"] = new Command("debugallcolors", (args) =>
            {
                DebugAllColors();
            }, "debugallcolors - Shows all XNA colors.");

            _commands["rest"] = new Command("rest", (args) =>
            {
                _gameState.QueueRest(args);
            },
            "rest <short|long> [gray]- Queue a rest action.",
            (args) => {
                if (args.Length == 0) return new List<string> { "short", "long" };
                return new List<string>();
            });

            _commands["debug"] = new Command("debug", (args) =>
            {
                AddToHistory("[debug]This is a debug command for testing purposes.");
                Core.Instance.ScreenShake(intensity: 4.0f, duration: 2.2f);
            }, "debug - Generic debug command");

            _commands["debug2"] = new Command("debug2", (args) =>
            {
                AddToHistory("[debug]This is a second debug command for testing purposes.");
            }, "debug - Generic debug command 2");

            _commands["exit"] = new Command("exit", (args) =>
            {
                Core.Instance.ExitApplication();
            }, "exit [gray]- Exit the game.");
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
                AddToHistory($"[{colorName.ToLower()}]{colorName}[/]", Global.Instance.OutputTextColor);
            }

            AddOutputToHistory($"[gray]Total colors displayed: {colorProperties.Count()}");
        }
    }
}
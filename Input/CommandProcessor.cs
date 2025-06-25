﻿using Microsoft.Xna.Framework;
using ProjectVagabond;
using ProjectVagabond.Scenes;
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
        private void AddHelpLineToHistory(string message) => Core.CurrentTerminalRenderer.AddToHistory(message, Global.Instance.Palette_LightPurple);

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        private void InitializeCommands()
        {
            _commands = new Dictionary<string, Command>();

            _commands["help"] = new Command("help", (args) =>
            {
                AddToHistory(" ");
                AddToHistory("Available commands:", Color.Magenta);
                foreach (var cmd in _commands.Values.OrderBy(c => c.Name))
                {
                    if (cmd.Name == "help" || cmd.Name.StartsWith("debug")) continue; // Don't show help for the help command itself, or for debug commands
                    if (!string.IsNullOrEmpty(cmd.HelpText))
                    {
                        AddHelpLineToHistory(cmd.HelpText);
                    }
                }
            }, "help [gray]- Shows help for possible commands.");

            _commands["clear"] = new Command("clear", (args) =>
            {
                Core.CurrentTerminalRenderer.ClearHistory();
                AddOutputToHistory("TERMINAL CLEARED");
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
                    AddOutputToHistory("[error]No run direction given (up, down, left, right, up-left, etc.).");
                    return;
                }
                string direction = args[1].ToLower();
                string[] movementArgs = args.Skip(1).ToArray();
                switch (direction)
                {
                    case "up": _gameState.QueueRunMovement(new Vector2(0, -1), movementArgs); break;
                    case "down": _gameState.QueueRunMovement(new Vector2(0, 1), movementArgs); break;
                    case "left": _gameState.QueueRunMovement(new Vector2(-1, 0), movementArgs); break;
                    case "right": _gameState.QueueRunMovement(new Vector2(1, 0), movementArgs); break;
                    case "up-left": _gameState.QueueRunMovement(new Vector2(-1, -1), movementArgs); break;
                    case "up-right": _gameState.QueueRunMovement(new Vector2(1, -1), movementArgs); break;
                    case "down-left": _gameState.QueueRunMovement(new Vector2(-1, 1), movementArgs); break;
                    case "down-right": _gameState.QueueRunMovement(new Vector2(1, 1), movementArgs); break;
                    default: AddOutputToHistory($"Unknown direction for run: '{direction}'."); break;
                }
            },
            "run <dir> <count?> [gray]- Queue a run (costs energy, but quicker).",
            (args) =>
            {
                if (args.Length == 0) return new List<string> { "up", "down", "left", "right", "up-left", "up-right", "down-left", "down-right" };
                return new List<string>();
            });

            _commands["walk"] = new Command("walk", (args) =>
            {
                if (args.Length < 2)
                {
                    AddOutputToHistory("[error]No walk direction given (up, down, left, right, up-left, etc.).");
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
                    case "up-left": _gameState.QueueWalkMovement(new Vector2(-1, -1), movementArgs); break;
                    case "up-right": _gameState.QueueWalkMovement(new Vector2(1, -1), movementArgs); break;
                    case "down-left": _gameState.QueueWalkMovement(new Vector2(-1, 1), movementArgs); break;
                    case "down-right": _gameState.QueueWalkMovement(new Vector2(1, 1), movementArgs); break;
                    default: AddOutputToHistory($"Unknown direction for walk: '{direction}'."); break;
                }
            },
            "walk <dir> <count?> [gray]- Queue a walk.",
            (args) =>
            {
                if (args.Length == 0) return new List<string> { "up", "down", "left", "right", "up-left", "up-right", "down-left", "down-right" };
                return new List<string>();
            });

            _commands["up"] = new Command("up", (args) =>
            {
                if (args.Length > 1)
                {
                    string secondArg = args[1].ToLower();
                    if (secondArg == "left")
                    {
                        var newArgs = new List<string> { "up-left" };
                        newArgs.AddRange(args.Skip(2));
                        _commands["up-left"].Action(newArgs.ToArray());
                        return;
                    }
                    if (secondArg == "right")
                    {
                        var newArgs = new List<string> { "up-right" };
                        newArgs.AddRange(args.Skip(2));
                        _commands["up-right"].Action(newArgs.ToArray());
                        return;
                    }
                }
                _gameState.QueueWalkMovement(new Vector2(0, -1), args);
            }, "up <count?> [gray]- Queue a walk up.");
            _commands["down"] = new Command("down", (args) =>
            {
                if (args.Length > 1)
                {
                    string secondArg = args[1].ToLower();
                    if (secondArg == "left")
                    {
                        var newArgs = new List<string> { "down-left" };
                        newArgs.AddRange(args.Skip(2));
                        _commands["down-left"].Action(newArgs.ToArray());
                        return;
                    }
                    if (secondArg == "right")
                    {
                        var newArgs = new List<string> { "down-right" };
                        newArgs.AddRange(args.Skip(2));
                        _commands["down-right"].Action(newArgs.ToArray());
                        return;
                    }
                }
                _gameState.QueueWalkMovement(new Vector2(0, 1), args);
            }, "down <count?> [gray]- Queue a walk down.");
            _commands["left"] = new Command("left", (args) =>
            {
                if (args.Length > 1)
                {
                    string secondArg = args[1].ToLower();
                    if (secondArg == "up")
                    {
                        var newArgs = new List<string> { "up-left" };
                        newArgs.AddRange(args.Skip(2));
                        _commands["up-left"].Action(newArgs.ToArray());
                        return;
                    }
                    if (secondArg == "down")
                    {
                        var newArgs = new List<string> { "down-left" };
                        newArgs.AddRange(args.Skip(2));
                        _commands["down-left"].Action(newArgs.ToArray());
                        return;
                    }
                }
                _gameState.QueueWalkMovement(new Vector2(-1, 0), args);
            }, "left <count?> [gray]- Queue a walk left.");
            _commands["right"] = new Command("right", (args) =>
            {
                if (args.Length > 1)
                {
                    string secondArg = args[1].ToLower();
                    if (secondArg == "up")
                    {
                        var newArgs = new List<string> { "up-right" };
                        newArgs.AddRange(args.Skip(2));
                        _commands["up-right"].Action(newArgs.ToArray());
                        return;
                    }
                    if (secondArg == "down")
                    {
                        var newArgs = new List<string> { "down-right" };
                        newArgs.AddRange(args.Skip(2));
                        _commands["down-right"].Action(newArgs.ToArray());
                        return;
                    }
                }
                _gameState.QueueWalkMovement(new Vector2(1, 0), args);
            }, "right <count?> [gray]- Queue a walk right.");
            _commands["up-left"] = new Command("up-left", (args) => { _gameState.QueueWalkMovement(new Vector2(-1, -1), args.ToArray()); }, "up-left <count?> [gray]- Queue a walk up-left.");
            _commands["up-right"] = new Command("up-right", (args) => { _gameState.QueueWalkMovement(new Vector2(1, -1), args.ToArray()); }, "up-right <count?> [gray]- Queue a walk up-right.");
            _commands["down-left"] = new Command("down-left", (args) => { _gameState.QueueWalkMovement(new Vector2(-1, 1), args.ToArray()); }, "down-left <count?> [gray]- Queue a walk down-left.");
            _commands["down-right"] = new Command("down-right", (args) => { _gameState.QueueWalkMovement(new Vector2(1, 1), args.ToArray()); }, "down-right <count?> [gray]- Queue a walk down-right.");

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
            "rest <short|long|full> [gray]- Queue a rest action.",
            (args) =>
            {
                if (args.Length == 0) return new List<string> { "short", "long", "full" };
                return new List<string>();
            });

            _commands["debug"] = new Command("debug", (args) =>
            {
                AddToHistory("[debug]This is a debug command for testing purposes.");
                Core.ScreenShake(intensity: 4.0f, duration: 2.2f);
            }, "debug - Generic debug command");

            _commands["debug2"] = new Command("debug2", (args) =>
            {
                AddToHistory("[debug]This is a second debug command for testing purposes.");
                Core.ScreenWobble(intensity: 4.0f, duration: 0.2f);
            }, "debug - Generic debug command 2");

            _commands["debugsettings"] = new Command("settings", (args) =>
            {
                Core.CurrentSceneManager.ChangeScene(GameSceneState.Settings);
            }, "debugsettings [gray]- Open the settings menu.");

            _commands["debugdialogue_test"] = new Command("dialogue_test", (args) =>
            {
                Core.CurrentSceneManager.ChangeScene(GameSceneState.Dialogue);
            }, "debugdialogue_test - Shows the placeholder dialogue screen.");

            _commands["debugcombat_test"] = new Command("combat_test", (args) =>
            {
                Core.CurrentSceneManager.ChangeScene(GameSceneState.Combat);
            }, "debugcombat_test - Shows the placeholder combat screen.");

            _commands["debugsetresolution"] = new Command("setresolution", (args) =>
            {
                if (args.Length != 3)
                {
                    Core.CurrentTerminalRenderer.AddOutputToHistory("[error]Invalid arguments. Usage: setres <width> <height>");
                    return;
                }
                if (!int.TryParse(args[1], out int width) || !int.TryParse(args[2], out int height))
                {
                    Core.CurrentTerminalRenderer.AddOutputToHistory("[error]Width and height must be valid integers.");
                    return;
                }
                const int minWidth = 800;
                const int minHeight = 600;
                if (width < minWidth || height < minHeight)
                {
                    Core.CurrentTerminalRenderer.AddOutputToHistory($"[error]Minimum resolution is {minWidth}x{minHeight}.");
                    return;
                }
                try
                {
                    Core.ResizeWindow(width, height);

                    Core.CurrentTerminalRenderer.AddOutputToHistory($"[green]Window resolution set to {width}x{height}.");
                }
                catch (Exception ex)
                {
                    Core.CurrentTerminalRenderer.AddOutputToHistory($"[error]Failed to set resolution: {ex.Message}");
                    Core.ResizeWindow(Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);
                }
            }, "debugsetresolution <width> <height> [gray]- Set the game resolution.");

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
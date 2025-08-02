using Microsoft.Xna.Framework;
using ProjectVagabond.Scenes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond
{
    public class CommandProcessor
    {
        // Injected Dependencies (via constructor)
        private readonly PlayerInputSystem _playerInputSystem;

        // Lazily-loaded Dependencies (via ServiceLocator)
        private GameState _gameState;
        private SceneManager _sceneManager;
        private HapticsManager _hapticsManager;
        private Global _global;
        private Core _core;
        private GraphicsDeviceManager _graphics;
        private BackgroundManager _backgroundManager;

        private Dictionary<string, Command> _commands;
        public Dictionary<string, Command> Commands => _commands;

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public CommandProcessor(PlayerInputSystem playerInputSystem)
        {
            _playerInputSystem = playerInputSystem;
            InitializeCommands();
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        private void InitializeCommands()
        {
            _commands = new Dictionary<string, Command>();

            _commands["help"] = new Command("help", (args) =>
            {
                _global ??= ServiceLocator.Get<Global>();
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = " " });
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "Available commands:", BaseColor = Color.Magenta });
                foreach (var cmd in _commands.Values.OrderBy(c => c.Name))
                {
                    if (cmd.Name == "help" || cmd.Name.StartsWith("debug")) continue;
                    if (!string.IsNullOrEmpty(cmd.HelpText))
                    {
                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = cmd.HelpText, BaseColor = _global.Palette_LightPurple });
                    }
                }
            }, "help [gray]- Shows help for possible commands.");

            _commands["clear"] = new Command("clear", (args) =>
            {
                var terminalRenderer = ServiceLocator.Get<TerminalRenderer>();
                terminalRenderer.ClearHistory();
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "TERMINAL CLEARED" });
            }, "clear [gray]- Clear the terminal history.");

            _commands["look"] = new Command("look", (args) =>
            {
                _gameState ??= ServiceLocator.Get<GameState>();
                int x = (int)_gameState.PlayerWorldPos.X;
                int y = (int)_gameState.PlayerWorldPos.Y;
                float noise = _gameState.GetNoiseAt(x, y);
                string terrain = _gameState.GetTerrainDescription(noise);
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"You are standing on {terrain}." });
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"World Position: ({x}, {y})" });
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"Terrain value: {noise:F2}" });
            }, "look [gray]- Look around current area.");

            _commands["run"] = new Command("run", (args) =>
            {
                _gameState ??= ServiceLocator.Get<GameState>();
                if (args.Length < 2)
                {
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]No run direction given (up, down, left, right, up-left, etc.)." });
                    return;
                }
                string direction = args[1].ToLower();
                string[] movementArgs = args.Skip(1).ToArray();
                switch (direction)
                {
                    case "up": _playerInputSystem.QueueRunMovement(_gameState, new Vector2(0, -1), movementArgs); break;
                    case "down": _playerInputSystem.QueueRunMovement(_gameState, new Vector2(0, 1), movementArgs); break;
                    case "left": _playerInputSystem.QueueRunMovement(_gameState, new Vector2(-1, 0), movementArgs); break;
                    case "right": _playerInputSystem.QueueRunMovement(_gameState, new Vector2(1, 0), movementArgs); break;
                    case "up-left": _playerInputSystem.QueueRunMovement(_gameState, new Vector2(-1, -1), movementArgs); break;
                    case "up-right": _playerInputSystem.QueueRunMovement(_gameState, new Vector2(1, -1), movementArgs); break;
                    case "down-left": _playerInputSystem.QueueRunMovement(_gameState, new Vector2(-1, 1), movementArgs); break;
                    case "down-right": _playerInputSystem.QueueRunMovement(_gameState, new Vector2(1, 1), movementArgs); break;
                    default: EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"Unknown direction for run: '{direction}'." }); break;
                }
            },
            "run <dir> <count?> [gray]- Queue a run.",
            (args) =>
            {
                if (args.Length == 0) return new List<string> { "up", "down", "left", "right", "up-left", "up-right", "down-left", "down-right" };
                return new List<string>();
            });

            _commands["jog"] = new Command("jog", (args) =>
            {
                _gameState ??= ServiceLocator.Get<GameState>();
                if (args.Length < 2)
                {
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]No jog direction given (up, down, left, right, up-left, etc.)." });
                    return;
                }
                string direction = args[1].ToLower();
                string[] movementArgs = args.Skip(1).ToArray();
                switch (direction)
                {
                    case "up": _playerInputSystem.QueueJogMovement(_gameState, new Vector2(0, -1), movementArgs); break;
                    case "down": _playerInputSystem.QueueJogMovement(_gameState, new Vector2(0, 1), movementArgs); break;
                    case "left": _playerInputSystem.QueueJogMovement(_gameState, new Vector2(-1, 0), movementArgs); break;
                    case "right": _playerInputSystem.QueueJogMovement(_gameState, new Vector2(1, 0), movementArgs); break;
                    case "up-left": _playerInputSystem.QueueJogMovement(_gameState, new Vector2(-1, -1), movementArgs); break;
                    case "up-right": _playerInputSystem.QueueJogMovement(_gameState, new Vector2(1, -1), movementArgs); break;
                    case "down-left": _playerInputSystem.QueueJogMovement(_gameState, new Vector2(-1, 1), movementArgs); break;
                    case "down-right": _playerInputSystem.QueueJogMovement(_gameState, new Vector2(1, 1), movementArgs); break;
                    default: EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"Unknown direction for jog: '{direction}'." }); break;
                }
            },
            "jog <dir> <count?> [gray]- Queue a jog (default speed).",
            (args) =>
            {
                if (args.Length == 0) return new List<string> { "up", "down", "left", "right", "up-left", "up-right", "down-left", "down-right" };
                return new List<string>();
            });

            _commands["walk"] = new Command("walk", (args) =>
            {
                _gameState ??= ServiceLocator.Get<GameState>();
                if (args.Length < 2)
                {
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]No walk direction given (up, down, left, right, up-left, etc.)." });
                    return;
                }
                string direction = args[1].ToLower();
                string[] movementArgs = args.Skip(1).ToArray();
                switch (direction)
                {
                    case "up": _playerInputSystem.QueueWalkMovement(_gameState, new Vector2(0, -1), movementArgs); break;
                    case "down": _playerInputSystem.QueueWalkMovement(_gameState, new Vector2(0, 1), movementArgs); break;
                    case "left": _playerInputSystem.QueueWalkMovement(_gameState, new Vector2(-1, 0), movementArgs); break;
                    case "right": _playerInputSystem.QueueWalkMovement(_gameState, new Vector2(1, 0), movementArgs); break;
                    case "up-left": _playerInputSystem.QueueWalkMovement(_gameState, new Vector2(-1, -1), movementArgs); break;
                    case "up-right": _playerInputSystem.QueueWalkMovement(_gameState, new Vector2(1, -1), movementArgs); break;
                    case "down-left": _playerInputSystem.QueueWalkMovement(_gameState, new Vector2(-1, 1), movementArgs); break;
                    case "down-right": _playerInputSystem.QueueWalkMovement(_gameState, new Vector2(1, 1), movementArgs); break;
                    default: EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"Unknown direction for walk: '{direction}'." }); break;
                }
            },
            "walk <dir> <count?> [gray]- Queue a walk (slow speed).",
            (args) =>
            {
                if (args.Length == 0) return new List<string> { "up", "down", "left", "right", "up-left", "up-right", "down-left", "down-right" };
                return new List<string>();
            });

            _commands["up"] = new Command("up", (args) => { _gameState ??= ServiceLocator.Get<GameState>(); _playerInputSystem.QueueJogMovement(_gameState, new Vector2(0, -1), args); }, "up <count?> [gray]- Queue a jog up.");
            _commands["down"] = new Command("down", (args) => { _gameState ??= ServiceLocator.Get<GameState>(); _playerInputSystem.QueueJogMovement(_gameState, new Vector2(0, 1), args); }, "down <count?> [gray]- Queue a jog down.");
            _commands["left"] = new Command("left", (args) => { _gameState ??= ServiceLocator.Get<GameState>(); _playerInputSystem.QueueJogMovement(_gameState, new Vector2(-1, 0), args); }, "left <count?> [gray]- Queue a jog left.");
            _commands["right"] = new Command("right", (args) => { _gameState ??= ServiceLocator.Get<GameState>(); _playerInputSystem.QueueJogMovement(_gameState, new Vector2(1, 0), args); }, "right <count?> [gray]- Queue a jog right.");
            _commands["up-left"] = new Command("up-left", (args) => { _gameState ??= ServiceLocator.Get<GameState>(); _playerInputSystem.QueueJogMovement(_gameState, new Vector2(-1, -1), args); }, "up-left <count?> [gray]- Queue a jog up-left.");
            _commands["up-right"] = new Command("up-right", (args) => { _gameState ??= ServiceLocator.Get<GameState>(); _playerInputSystem.QueueJogMovement(_gameState, new Vector2(1, -1), args); }, "up-right <count?> [gray]- Queue a jog up-right.");
            _commands["down-left"] = new Command("down-left", (args) => { _gameState ??= ServiceLocator.Get<GameState>(); _playerInputSystem.QueueJogMovement(_gameState, new Vector2(-1, 1), args); }, "down-left <count?> [gray]- Queue a jog down-left.");
            _commands["down-right"] = new Command("down-right", (args) => { _gameState ??= ServiceLocator.Get<GameState>(); _playerInputSystem.QueueJogMovement(_gameState, new Vector2(1, 1), args); }, "down-right <count?> [gray]- Queue a jog down-right.");

            _commands["cancel"] = new Command("cancel", (args) =>
            {
                _gameState ??= ServiceLocator.Get<GameState>();
                if (_gameState.PendingActions.Count > 0)
                {
                    _playerInputSystem.CancelPendingActions(_gameState);
                }
                else
                {
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "No pending actions to clear." });
                }
            }, "cancel [gray]- Cancel all pending actions.");

            _commands["pos"] = new Command("pos", (args) =>
            {
                _gameState ??= ServiceLocator.Get<GameState>();
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"World position: ({(int)_gameState.PlayerWorldPos.X}, {(int)_gameState.PlayerWorldPos.Y})" });
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"Pending actions in queue: {_gameState.PendingActions.Count}" });
                if (_gameState.IsExecutingActions)
                {
                    int totalActions = _gameState.InitialActionCount;
                    int completedActions = totalActions - _gameState.PendingActions.Count;
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"Executing queue: action {completedActions + 1}/{totalActions}" });
                }
            }, "pos [gray]- Show current position and queue status.");

            _commands["debugallcolors"] = new Command("debugallcolors", (args) =>
            {
                DebugAllColors();
            }, "debugallcolors - Shows all XNA colors.");

            _commands["rest"] = new Command("rest", (args) =>
            {
                _gameState ??= ServiceLocator.Get<GameState>();
                _playerInputSystem.QueueRest(_gameState, args);
            },
            "rest <short|long|full> [gray]- Queue a rest action.",
            (args) =>
            {
                if (args.Length == 0) return new List<string> { "short", "long", "full" };
                return new List<string>();
            });

            _commands["setbgscroll"] = new Command("setbgscroll", (args) =>
            {
                _backgroundManager ??= ServiceLocator.Get<BackgroundManager>();
                if (args.Length < 2)
                {
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Usage: setbgscroll <direction|stop> <speed?>" });
                    return;
                }

                string direction = args[1].ToLower();
                if (direction == "stop")
                {
                    _backgroundManager.ScrollSpeed = 0f;
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "Background scrolling stopped." });
                    return;
                }

                float speed = 10f; // default speed
                if (args.Length > 2 && float.TryParse(args[2], out float parsedSpeed))
                {
                    speed = parsedSpeed;
                }

                Vector2 dirVector = Vector2.Zero;
                switch (direction)
                {
                    case "up": dirVector = new Vector2(0, -1); break;
                    case "down": dirVector = new Vector2(0, 1); break;
                    case "left": dirVector = new Vector2(-1, 0); break;
                    case "right": dirVector = new Vector2(1, 0); break;
                    case "up-left": dirVector = new Vector2(-1, -1); break;
                    case "up-right": dirVector = new Vector2(1, -1); break;
                    case "down-left": dirVector = new Vector2(-1, 1); break;
                    case "down-right": dirVector = new Vector2(1, 1); break;
                    default:
                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[error]Unknown direction for setbgscroll: '{direction}'." });
                        return;
                }

                _backgroundManager.ScrollDirection = dirVector;
                _backgroundManager.ScrollSpeed = speed;
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"Background scrolling set to {direction} at speed {speed}." });
            },
            "setbgscroll <dir> <speed?> [gray]- Sets the background scroll.",
            (args) =>
            {
                if (args.Length == 0) return new List<string> { "stop", "up", "down", "left", "right", "up-left", "up-right", "down-left", "down-right" };
                return new List<string>();
            });

            _commands["debug"] = new Command("debug", (args) =>
            {
                _hapticsManager ??= ServiceLocator.Get<HapticsManager>();
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[debug]This is a debug command for testing purposes." });
                _hapticsManager.TriggerShake(magnitude: 4.0f, duration: 2.2f);
            }, "debug - Generic debug command");

            _commands["debug2"] = new Command("debug2", (args) =>
            {
                _hapticsManager ??= ServiceLocator.Get<HapticsManager>();
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[debug]This is a second debug command for testing purposes." });
                _hapticsManager.TriggerWobble(intensity: 4.0f, duration: 0.2f);
            }, "debug - Generic debug command 2");

            _commands["debugsettings"] = new Command("settings", (args) =>
            {
                _sceneManager ??= ServiceLocator.Get<SceneManager>();
                _sceneManager.ChangeScene(GameSceneState.Settings);
            }, "debugsettings [gray]- Open the settings menu.");

            _commands["debugdialogue_test"] = new Command("dialogue_test", (args) =>
            {
                _sceneManager ??= ServiceLocator.Get<SceneManager>();
                _sceneManager.ChangeScene(GameSceneState.Dialogue);
            }, "debugdialogue_test - Shows the placeholder dialogue screen.");

            _commands["debugencounter"] = new Command("debugencounter", (args) =>
            {
                if (args.Length < 2)
                {
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Usage: debugencounter <encounter_id>" });
                    return;
                }
                var encounterManager = ServiceLocator.Get<EncounterManager>();
                encounterManager.TriggerEncounter(args[1]);
            }, "debugencounter <id> - Triggers a game encounter by ID.");

            _commands["debugsetresolution"] = new Command("setresolution", (args) =>
            {
                _core ??= ServiceLocator.Get<Core>();
                _graphics ??= ServiceLocator.Get<GraphicsDeviceManager>();
                if (args.Length != 3)
                {
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Invalid arguments. Usage: setres <width> <height>" });
                    return;
                }
                if (!int.TryParse(args[1], out int width) || !int.TryParse(args[2], out int height))
                {
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Width and height must be valid integers." });
                    return;
                }
                const int minWidth = 800;
                const int minHeight = 600;
                if (width < minWidth || height < minHeight)
                {
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[error]Minimum resolution is {minWidth}x{minHeight}." });
                    return;
                }
                try
                {
                    _graphics.PreferredBackBufferWidth = width;
                    _graphics.PreferredBackBufferHeight = height;
                    _graphics.ApplyChanges();
                    _core.OnResize(null, null);

                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[green]Window resolution set to {width}x{height}." });
                }
                catch (Exception ex)
                {
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[error]Failed to set resolution: {ex.Message}" });
                    _graphics.PreferredBackBufferWidth = Global.VIRTUAL_WIDTH;
                    _graphics.PreferredBackBufferHeight = Global.VIRTUAL_HEIGHT;
                    _graphics.ApplyChanges();
                    _core.OnResize(null, null);
                }
            }, "debugsetresolution <width> <height> [gray]- Set the game resolution.");

            _commands["exit"] = new Command("exit", (args) =>
            {
                _core ??= ServiceLocator.Get<Core>();
                _core.ExitApplication();
            }, "exit [gray]- Exit the game.");
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- // 

        private void DebugAllColors()
        {
            _global ??= ServiceLocator.Get<Global>();
            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[gray]Displaying all XNA Framework colors:" });

            var colorProperties = typeof(Color).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .Where(p => p.PropertyType == typeof(Color))
                .OrderBy(p => p.Name);

            foreach (var property in colorProperties)
            {
                string colorName = property.Name;
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[{colorName.ToLower()}]{colorName}[/]", BaseColor = _global.OutputTextColor });
            }

            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[gray]Total colors displayed: {colorProperties.Count()}" });
        }
    }
}
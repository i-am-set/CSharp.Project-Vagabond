using Microsoft.Xna.Framework;
using ProjectVagabond;
using ProjectVagabond.Battle;
using ProjectVagabond.Scenes;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace ProjectVagabond
{
    public class CommandProcessor
    {
        // Injected Dependencies (via constructor)
        private readonly PlayerInputSystem _playerInputSystem;

        // Lazily-loaded Dependencies (via ServiceLocator)
        private GameState _gameState;
        private ComponentStore _componentStore;

        private Dictionary<string, Command> _commands;
        public Dictionary<string, Command> Commands => _commands;

        // Cache for reflected XNA colors to avoid repeated reflection calls.
        private static List<PropertyInfo> _xnaColors;
        private const int COLOR_PALETTE_GRID_WIDTH = 16;


        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public CommandProcessor(PlayerInputSystem playerInputSystem)
        {
            _playerInputSystem = playerInputSystem;
            InitializeCommands();
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        private void CacheXnaColors()
        {
            if (_xnaColors == null)
            {
                _xnaColors = typeof(Color).GetProperties(BindingFlags.Public | BindingFlags.Static)
                    .Where(prop => prop.PropertyType == typeof(Color))
                    .OrderBy(prop => prop.Name) // Order alphabetically for consistency
                    .ToList();
            }
        }

        private void InitializeCommands()
        {
            _commands = new Dictionary<string, Command>();

            _commands["help"] = new Command("help", (args) =>
            {
                var sb = new StringBuilder();
                sb.AppendLine("[palette_yellow]Available Commands:[/]");
                sb.AppendLine("  [palette_teal]Player Management[/]");
                sb.AppendLine("    stats              - Shows player's current stats.");
                sb.AppendLine("    heal <amount>      - Heals the player.");
                sb.AppendLine("    damage <amount>    - Damages the player.");
                sb.AppendLine();
                sb.AppendLine("  [palette_teal]Inventory & Moves[/]");
                sb.AppendLine("    inventory          - Shows player's current inventory.");
                sb.AppendLine("    spellbook          - Shows player's current spellbook.");
                sb.AppendLine("    give <ID> [[qty]    - Adds an item to inventory.");
                sb.AppendLine("    remove <ID> [[qty]  - Removes an item from inventory.");
                sb.AppendLine("    learn <ID>         - Teaches the player a new move.");
                sb.AppendLine("    forget <ID>        - Makes the player forget a move.");
                sb.AppendLine("    addpage            - Adds an empty spell page.");
                sb.AppendLine("    removepage         - Removes the last spell page.");
                sb.AppendLine();
                sb.AppendLine("  [palette_teal]System[/]");
                sb.AppendLine("    clear              - Clears the console history.");
                sb.AppendLine("    exit               - Exits the game.");

                string[] lines = sb.ToString().Split(new[] { Environment.NewLine }, StringSplitOptions.None);
                foreach (var line in lines)
                {
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = line });
                }
            }, "help - Shows this help message.");

            _commands["clear"] = new Command("clear", (args) =>
            {
                var debugConsole = ServiceLocator.Get<Utils.DebugConsole>();
                debugConsole.ClearHistory();
            }, "clear - Clears the console history.");

            _commands["learn"] = new Command("learn", (args) =>
            {
                if (args.Length < 2)
                {
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Usage: learn <MoveID>" });
                    return;
                }
                EventBus.Publish(new GameEvents.PlayerMoveSetChanged { MoveID = args[1], ChangeType = GameEvents.MoveSetChangeType.Learn });
            },
            "learn <MoveID> - Teach the player a new move.",
            (args) =>
            {
                if (args.Length == 0) return BattleDataCache.Moves.Keys.ToList();
                return new List<string>();
            });

            _commands["forget"] = new Command("forget", (args) =>
            {
                _gameState ??= ServiceLocator.Get<GameState>();
                if (_gameState.PlayerState == null)
                {
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Player state not initialized. Start a game first." });
                    return;
                }
                if (args.Length < 2)
                {
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Usage: forget <MoveID>" });
                    return;
                }
                EventBus.Publish(new GameEvents.PlayerMoveSetChanged { MoveID = args[1], ChangeType = GameEvents.MoveSetChangeType.Forget });
            },
            "forget <MoveID> - Make the player forget a move.",
            (args) =>
            {
                _gameState ??= ServiceLocator.Get<GameState>();
                if (args.Length == 0) return _gameState.PlayerState?.SpellbookPages.Where(p => p != null).Select(p => p.MoveID).ToList() ?? new List<string>();
                return new List<string>();
            });

            _commands["addpage"] = new Command("addpage", (args) =>
            {
                _gameState ??= ServiceLocator.Get<GameState>();
                if (_gameState.PlayerState == null)
                {
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Player state not initialized. Start a game first." });
                    return;
                }
                _gameState.PlayerState.SpellbookPages.Add(null);
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"Added a spell page. Total pages: {_gameState.PlayerState.SpellbookPages.Count}" });
            }, "addpage - Adds an empty spell page to the player's spellbook.");

            _commands["removepage"] = new Command("removepage", (args) =>
            {
                _gameState ??= ServiceLocator.Get<GameState>();
                if (_gameState.PlayerState == null)
                {
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Player state not initialized. Start a game first." });
                    return;
                }
                if (_gameState.PlayerState.SpellbookPages.Count > 0)
                {
                    _gameState.PlayerState.SpellbookPages.RemoveAt(_gameState.PlayerState.SpellbookPages.Count - 1);
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"Removed a spell page. Total pages: {_gameState.PlayerState.SpellbookPages.Count}" });
                }
                else
                {
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Cannot remove any more spell pages." });
                }
            }, "removepage - Removes the last spell page from the player's spellbook.");

            _commands["give"] = new Command("give", (args) => HandleGiveItem(args), "give <ItemID> [quantity=1] - Adds an item to inventory.",
                (args) =>
                {
                    if (args.Length == 0) return BattleDataCache.Consumables.Keys.ToList();
                    return new List<string>();
                });

            _commands["remove"] = new Command("remove", (args) => HandleRemoveItem(args), "remove <ItemID> [quantity=1] - Removes an item from inventory.",
                (args) =>
                {
                    _gameState ??= ServiceLocator.Get<GameState>();
                    if (args.Length == 0) return _gameState.PlayerState?.Inventory.Keys.ToList() ?? new List<string>();
                    return new List<string>();
                });

            _commands["inventory"] = new Command("inventory", (args) => HandleShowInventory(), "inventory - Shows player's current inventory.");
            _commands["spellbook"] = new Command("spellbook", (args) => HandleShowSpellbook(), "spellbook - Shows the player's current spellbook.");
            _commands["stats"] = new Command("stats", (args) => HandleShowStats(), "stats - Shows player's current stats.");
            _commands["heal"] = new Command("heal", (args) => HandleHeal(args), "heal <amount> - Heals the player.");
            _commands["damage"] = new Command("damage", (args) => HandleDamage(args), "damage <amount> - Damages the player.");

            _commands["exit"] = new Command("exit", (args) =>
            {
                var core = ServiceLocator.Get<Core>();
                core.ExitApplication();
            }, "exit - Exit the game.");

            _commands["debug_colorpalette"] = new Command("debug_colorpalette", (args) =>
            {
                CacheXnaColors();
                if (_xnaColors == null || !_xnaColors.Any())
                {
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Could not reflect XNA colors." });
                    return;
                }

                const int numColumns = 4;
                int maxNameLength = _xnaColors.Max(c => c.Name.Length);
                int columnWidth = maxNameLength + 4; // Add padding

                var sb = new StringBuilder();
                for (int i = 0; i < _xnaColors.Count; i++)
                {
                    string colorName = _xnaColors[i].Name;
                    string coloredText = $"[{colorName}]{colorName}[/]";
                    sb.Append(coloredText);

                    // Calculate padding based on the visible length of the name, not the tagged string
                    int padding = columnWidth - colorName.Length;
                    if (padding > 0)
                    {
                        sb.Append(new string(' ', padding));
                    }

                    if ((i + 1) % numColumns == 0 || i == _xnaColors.Count - 1)
                    {
                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = sb.ToString() });
                        sb.Clear();
                    }
                }
            }, "debug_colorpalette - Displays a list of all available XNA colors.");

            _commands["debug_particle"] = new Command(
                "debug_particle",
                (args) =>
                {
                    if (args.Length < 2)
                    {
                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Usage: debug_particle <EffectName>" });
                        return;
                    }
                    string effectName = args[1];
                    var effectNames = ParticleEffectRegistry.GetEffectNames();

                    // Case-insensitive check if the effect exists
                    if (effectNames.Any(name => name.Equals(effectName, StringComparison.OrdinalIgnoreCase)))
                    {
                        FXManager.Play(effectName, new Vector2(Global.VIRTUAL_WIDTH / 2, Global.VIRTUAL_HEIGHT / 2), 3.0f);
                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"Played particle effect: {effectName}" });
                    }
                    else
                    {
                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[error]Particle effect '{effectName}' not found." });
                    }
                },
                "debug_particle <EffectName> - Emits a specific particle effect.",
                (typedArgs) =>
                {
                    // We suggest arguments when the user is typing the first argument (typedArgs.Length == 0)
                    if (typedArgs.Length == 0)
                    {
                        return ParticleEffectRegistry.GetEffectNames();
                    }
                    // No suggestions for subsequent arguments
                    return new List<string>();
                }
            );
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- // 

        private void HandleGiveItem(string[] args)
        {
            _gameState ??= ServiceLocator.Get<GameState>();
            if (_gameState.PlayerState == null)
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Player state not initialized. Start a game first." });
                return;
            }
            if (args.Length < 2)
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Usage: give <ItemID> [quantity]" });
                return;
            }

            string itemID = args[1];
            if (!BattleDataCache.Consumables.ContainsKey(itemID))
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[error]Item '{itemID}' not found." });
                return;
            }

            int quantity = 1;
            if (args.Length > 2 && !int.TryParse(args[2], out quantity))
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Invalid quantity." });
                return;
            }

            _gameState.PlayerState.AddItem(itemID, quantity);
            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"Added {quantity}x {itemID} to inventory." });
        }

        private void HandleRemoveItem(string[] args)
        {
            _gameState ??= ServiceLocator.Get<GameState>();
            if (_gameState.PlayerState == null)
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Player state not initialized. Start a game first." });
                return;
            }
            if (args.Length < 2)
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Usage: remove <ItemID> [quantity]" });
                return;
            }

            string itemID = args[1];
            int quantity = 1;
            if (args.Length > 2 && !int.TryParse(args[2], out quantity))
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Invalid quantity." });
                return;
            }

            if (_gameState.PlayerState.RemoveItem(itemID, quantity))
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"Removed {quantity}x {itemID} from inventory." });
            }
            else
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[error]Could not remove {quantity}x {itemID}. Not enough in inventory." });
            }
        }

        private void HandleShowInventory()
        {
            _gameState ??= ServiceLocator.Get<GameState>();
            if (_gameState.PlayerState == null)
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Player state not initialized. Start a game first." });
                return;
            }
            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "Player Inventory:" });
            if (_gameState.PlayerState.Inventory.Any())
            {
                foreach (var item in _gameState.PlayerState.Inventory)
                {
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"  - {item.Key}: {item.Value}" });
                }
            }
            else
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "  (Empty)" });
            }
        }

        private void HandleShowSpellbook()
        {
            _gameState ??= ServiceLocator.Get<GameState>();
            if (_gameState.PlayerState == null)
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Player state not initialized. Start a game first." });
                return;
            }
            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "Player Spellbook:" });
            var spellbook = _gameState.PlayerState.SpellbookPages;
            if (spellbook.Any(p => p != null))
            {
                for (int i = 0; i < spellbook.Count; i++)
                {
                    var entry = spellbook[i];
                    if (entry == null)
                    {
                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"  Page {i + 1}: (Empty)" });
                    }
                    else
                    {
                        string moveName = entry.MoveID;
                        if (BattleDataCache.Moves.TryGetValue(entry.MoveID, out var moveData))
                        {
                            moveName = moveData.MoveName;
                        }
                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"  Page {i + 1}: {moveName} [dim](Used: {entry.TimesUsed}) ({entry.MoveID})[/]" });
                    }
                }
            }
            else
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "  (No pages or all pages empty)" });
            }
        }

        private void HandleShowStats()
        {
            _gameState ??= ServiceLocator.Get<GameState>();
            _componentStore ??= ServiceLocator.Get<ComponentStore>();
            var stats = _componentStore.GetComponent<CombatantStatsComponent>(_gameState.PlayerEntityId);
            if (stats == null)
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Player stats component not found." });
                return;
            }

            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "Player Stats:" });
            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"  HP: {stats.CurrentHP} / {stats.MaxHP}" });
            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"  Level: {stats.Level}" });
            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"  Strength: {stats.Strength}" });
            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"  Intelligence: {stats.Intelligence}" });
            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"  Tenacity: {stats.Tenacity}" });
            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"  Agility: {stats.Agility}" });
        }

        private void HandleHeal(string[] args)
        {
            _gameState ??= ServiceLocator.Get<GameState>();
            _componentStore ??= ServiceLocator.Get<ComponentStore>();
            if (args.Length < 2 || !int.TryParse(args[1], out int amount))
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Usage: heal <amount>" });
                return;
            }

            var stats = _componentStore.GetComponent<CombatantStatsComponent>(_gameState.PlayerEntityId);
            if (stats == null)
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Player stats component not found." });
                return;
            }

            int oldHP = stats.CurrentHP;
            stats.CurrentHP = Math.Min(stats.MaxHP, stats.CurrentHP + amount);
            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"Healed {stats.CurrentHP - oldHP} HP. Current HP: {stats.CurrentHP}/{stats.MaxHP}" });
        }

        private void HandleDamage(string[] args)
        {
            _gameState ??= ServiceLocator.Get<GameState>();
            _componentStore ??= ServiceLocator.Get<ComponentStore>();
            if (args.Length < 2 || !int.TryParse(args[1], out int amount))
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Usage: damage <amount>" });
                return;
            }

            var stats = _componentStore.GetComponent<CombatantStatsComponent>(_gameState.PlayerEntityId);
            if (stats == null)
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Player stats component not found." });
                return;
            }

            int oldHP = stats.CurrentHP;
            stats.CurrentHP = Math.Max(0, stats.CurrentHP - amount);
            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"Dealt {oldHP - stats.CurrentHP} damage. Current HP: {stats.CurrentHP}/{stats.MaxHP}" });
        }

        public void ProcessCommand(string input)
        {
            if (string.IsNullOrEmpty(input)) return;

            string[] parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return;

            string commandName = parts[0].ToLower();

            if (_commands.TryGetValue(commandName, out var command))
            {
                command.Action(parts);
            }
            else
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"Unknown command: '{commandName}'. Type 'help' for available commands." });
            }
        }
    }
}
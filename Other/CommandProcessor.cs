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
using ProjectVagabond.Progression;

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
                sb.AppendLine("    inventory          - Shows all player inventories.");
                sb.AppendLine("    spellbook          - Shows player's spellbook and equipped spells.");
                sb.AppendLine("    givespell <ID>     - Adds a spell to the spellbook.");
                sb.AppendLine("    removespell <ID>   - Removes a spell from the spellbook.");
                sb.AppendLine("    give<type> <ID> [qty] - Adds an item. Types: weapon, armor, relic, consumable.");
                sb.AppendLine("    remove<type> <ID> [qty] - Removes an item. Types: weapon, armor, relic, consumable.");
                sb.AppendLine("    equip<type> <ID> [slot] - Equips an item. Types: weapon, armor, relic.");
                sb.AppendLine("    unequip<type> [slot] - Unequips an item. Types: weapon, armor, relic.");
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

            _commands["givespell"] = new Command("givespell", (args) =>
            {
                if (args.Length < 2)
                {
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Usage: givespell <MoveID>" });
                    return;
                }
                EventBus.Publish(new GameEvents.PlayerMoveSetChanged { MoveID = args[1], ChangeType = GameEvents.MoveSetChangeType.Learn });
            },
            "givespell <MoveID> - Adds a spell to the player's spellbook.",
            (args) =>
            {
                if (args.Length == 0) return BattleDataCache.Moves.Keys.Where(id => BattleDataCache.Moves[id].MoveType == MoveType.Spell).ToList();
                return new List<string>();
            });

            _commands["removespell"] = new Command("removespell", (args) =>
            {
                _gameState ??= ServiceLocator.Get<GameState>();
                if (_gameState.PlayerState == null)
                {
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Player state not initialized." });
                    return;
                }
                if (args.Length < 2)
                {
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Usage: removespell <MoveID>" });
                    return;
                }
                EventBus.Publish(new GameEvents.PlayerMoveSetChanged { MoveID = args[1], ChangeType = GameEvents.MoveSetChangeType.Forget });
            },
            "removespell <MoveID> - Removes a spell from the player's spellbook.",
            (args) =>
            {
                _gameState ??= ServiceLocator.Get<GameState>();
                if (args.Length == 0) return _gameState.PlayerState?.SpellbookPages.Where(p => p != null).Select(p => p!.MoveID).ToList() ?? new List<string>();
                return new List<string>();
            });


            _commands["equip"] = new Command("equip", (args) => HandleEquip(args), "equip <page> <slot> - Equips a spell to a combat slot (1-4).");
            _commands["unequip"] = new Command("unequip", (args) => HandleUnequip(args), "unequip <slot> - Unequips a spell from a combat slot (1-4).");

            // --- New Inventory Commands ---
            _commands["giveconsumable"] = new Command("giveconsumable", (args) => HandleGiveItem(args, "consumable"), "giveconsumable <ItemID> [qty] - Adds a consumable.", (s) => BattleDataCache.Consumables.Keys.ToList());
            _commands["removeconsumable"] = new Command("removeconsumable", (args) => HandleRemoveItem(args, "consumable"), "removeconsumable <ItemID> [qty] - Removes a consumable.", (s) => _gameState?.PlayerState.ConsumableInventory.Keys.ToList() ?? new List<string>());
            _commands["giverelic"] = new Command("giverelic", (args) => HandleGiveItem(args, "relic"), "giverelic <AbilityID> [qty] - Adds a relic.", (s) => BattleDataCache.Abilities.Keys.ToList());
            _commands["removerelic"] = new Command("removerelic", (args) => HandleRemoveItem(args, "relic"), "removerelic <AbilityID> [qty] - Removes a relic.", (s) => _gameState?.PlayerState.RelicInventory.Keys.ToList() ?? new List<string>());
            _commands["giveweapon"] = new Command("giveweapon", (args) => HandleGiveItem(args, "weapon"), "giveweapon <WeaponID> [qty] - Adds a weapon.", (s) => new List<string>());
            _commands["removeweapon"] = new Command("removeweapon", (args) => HandleRemoveItem(args, "weapon"), "removeweapon <WeaponID> [qty] - Removes a weapon.", (s) => _gameState?.PlayerState.WeaponsInventory.Keys.ToList() ?? new List<string>());
            _commands["givearmor"] = new Command("givearmor", (args) => HandleGiveItem(args, "armor"), "givearmor <ArmorID> [qty] - Adds armor.", (s) => new List<string>());
            _commands["removearmor"] = new Command("removearmor", (args) => HandleRemoveItem(args, "armor"), "removearmor <ArmorID> [qty] - Removes armor.", (s) => _gameState?.PlayerState.ArmorsInventory.Keys.ToList() ?? new List<string>());

            // --- New Equip Commands ---
            _commands["equiprelic"] = new Command("equiprelic", HandleEquipRelic, "equiprelic <AbilityID> <slot> - Equips a relic to a slot (1-3).", (s) => _gameState?.PlayerState.RelicInventory.Keys.ToList() ?? new List<string>());
            _commands["unequiprelic"] = new Command("unequiprelic", HandleUnequipRelic, "unequiprelic <slot> - Unequips a relic from a slot (1-3).");
            _commands["equipweapon"] = new Command("equipweapon", HandleEquipWeapon, "equipweapon <WeaponID> - Equips a weapon.", (s) => _gameState?.PlayerState.WeaponsInventory.Keys.ToList() ?? new List<string>());
            _commands["unequipweapon"] = new Command("unequipweapon", HandleUnequipWeapon, "unequipweapon - Unequips the current weapon.");
            _commands["equiparmor"] = new Command("equiparmor", HandleEquipArmor, "equiparmor <ArmorID> - Equips an armor piece.", (s) => _gameState?.PlayerState.ArmorsInventory.Keys.ToList() ?? new List<string>());
            _commands["unequiparmor"] = new Command("unequiparmor", HandleUnequipArmor, "unequiparmor - Unequips the current armor.");


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

            _commands["inventory"] = new Command("inventory", (args) => HandleShowInventory(), "inventory - Shows all player inventories.");
            _commands["spellbook"] = new Command("spellbook", (args) => HandleShowSpellbook(), "spellbook - Shows the player's current spellbook.");
            _commands["stats"] = new Command("stats", (args) => HandleShowStats(), "stats - Shows player's current stats.");
            _commands["heal"] = new Command("heal", (args) => HandleHeal(args), "heal <amount> - Heals the player.");
            _commands["damage"] = new Command("damage", (args) => HandleDamage(args), "damage <amount> - Damages the player.");

            _commands["exit"] = new Command("exit", (args) =>
            {
                var core = ServiceLocator.Get<Core>();
                core.ExitApplication();
            }, "exit - Exit the game.");

            _commands["debugstartcombat"] = new Command("debugstartcombat", (args) =>
            {
                var progressionManager = ServiceLocator.Get<ProgressionManager>();
                var sceneManager = ServiceLocator.Get<SceneManager>();

                var randomEncounter = progressionManager.GetRandomBattleFromSplit("forest");

                if (randomEncounter != null && randomEncounter.Any())
                {
                    BattleSetup.EnemyArchetypes = randomEncounter;
                    BattleSetup.ReturnSceneState = GameSceneState.Split;
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"Starting debug combat with enemies: [yellow]{string.Join(", ", randomEncounter)}[/]" });
                    sceneManager.ChangeScene(GameSceneState.Battle);
                }
                else
                {
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Could not start debug combat. No 'forest' split data or no battles defined for it." });
                }
            }, "debugstartcombat - Starts a random combat encounter from the 'forest' split.");

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

        private void HandleEquip(string[] args)
        {
            _gameState ??= ServiceLocator.Get<GameState>();
            if (_gameState.PlayerState == null)
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Player state not initialized." });
                return;
            }

            if (args.Length < 3 || !int.TryParse(args[1], out int pageNumber) || !int.TryParse(args[2], out int slotNumber))
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Usage: equip <page_number> <slot_number>" });
                return;
            }

            if (slotNumber < 1 || slotNumber > 4)
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Slot number must be between 1 and 4." });
                return;
            }

            if (pageNumber < 1 || pageNumber > _gameState.PlayerState.SpellbookPages.Count)
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[error]Invalid page number. You have {_gameState.PlayerState.SpellbookPages.Count} pages." });
                return;
            }

            var spellEntry = _gameState.PlayerState.SpellbookPages[pageNumber - 1];
            if (spellEntry == null)
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[error]Spellbook page {pageNumber} is empty." });
                return;
            }

            // Check if the spell is already equipped and unequip it if so.
            for (int i = 0; i < _gameState.PlayerState.EquippedSpells.Length; i++)
            {
                if (_gameState.PlayerState.EquippedSpells[i] == spellEntry)
                {
                    _gameState.PlayerState.EquippedSpells[i] = null;
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"Unequipped {spellEntry.MoveID} from slot {i + 1}." });
                }
            }

            // Equip the spell in the new slot.
            _gameState.PlayerState.EquippedSpells[slotNumber - 1] = spellEntry;
            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"Equipped {spellEntry.MoveID} to slot {slotNumber}." });
        }

        private void HandleUnequip(string[] args)
        {
            _gameState ??= ServiceLocator.Get<GameState>();
            if (_gameState.PlayerState == null)
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Player state not initialized." });
                return;
            }

            if (args.Length < 2 || !int.TryParse(args[1], out int slotNumber))
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Usage: unequip <slot_number>" });
                return;
            }

            if (slotNumber < 1 || slotNumber > 4)
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Slot number must be between 1 and 4." });
                return;
            }

            var spellToUnequip = _gameState.PlayerState.EquippedSpells[slotNumber - 1];
            if (spellToUnequip != null)
            {
                _gameState.PlayerState.EquippedSpells[slotNumber - 1] = null;
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"Unequipped {spellToUnequip.MoveID} from slot {slotNumber}." });
            }
            else
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"Slot {slotNumber} is already empty." });
            }
        }

        private void HandleGiveItem(string[] args, string itemType)
        {
            _gameState ??= ServiceLocator.Get<GameState>();
            if (_gameState.PlayerState == null)
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Player state not initialized." });
                return;
            }
            if (args.Length < 2)
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[error]Usage: {args[0]} <ItemID> [quantity]" });
                return;
            }

            string itemID = args[1];
            int quantity = 1;
            if (args.Length > 2 && !int.TryParse(args[2], out quantity))
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Invalid quantity." });
                return;
            }

            switch (itemType)
            {
                case "consumable": _gameState.PlayerState.AddConsumable(itemID, quantity); break;
                case "relic": _gameState.PlayerState.AddRelic(itemID, quantity); break;
                case "weapon": _gameState.PlayerState.AddWeapon(itemID, quantity); break;
                case "armor": _gameState.PlayerState.AddArmor(itemID, quantity); break;
            }
            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"Added {quantity}x {itemID} to {itemType} inventory." });
        }

        private void HandleRemoveItem(string[] args, string itemType)
        {
            _gameState ??= ServiceLocator.Get<GameState>();
            if (_gameState.PlayerState == null)
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Player state not initialized." });
                return;
            }
            if (args.Length < 2)
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[error]Usage: {args[0]} <ItemID> [quantity]" });
                return;
            }

            string itemID = args[1];
            int quantity = 1;
            if (args.Length > 2 && !int.TryParse(args[2], out quantity))
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Invalid quantity." });
                return;
            }

            bool success = false;
            switch (itemType)
            {
                case "consumable": success = _gameState.PlayerState.RemoveConsumable(itemID, quantity); break;
                case "relic": success = _gameState.PlayerState.RemoveRelic(itemID, quantity); break;
                case "weapon": success = _gameState.PlayerState.RemoveWeapon(itemID, quantity); break;
                case "armor": success = _gameState.PlayerState.RemoveArmor(itemID, quantity); break;
            }

            if (success)
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"Removed up to {quantity}x {itemID} from {itemType} inventory." });
            }
            else
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[error]Item '{itemID}' not found in {itemType} inventory." });
            }
        }

        private void HandleShowInventory()
        {
            _gameState ??= ServiceLocator.Get<GameState>();
            if (_gameState.PlayerState == null)
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Player state not initialized." });
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("[yellow]-- INVENTORY --[/]");

            sb.AppendLine("[teal]Weapons:[/]");
            if (_gameState.PlayerState.WeaponsInventory.Any())
                foreach (var item in _gameState.PlayerState.WeaponsInventory) sb.AppendLine($"  - {item.Key}: {item.Value}");
            else sb.AppendLine("  (Empty)");

            sb.AppendLine("[teal]Armor:[/]");
            if (_gameState.PlayerState.ArmorsInventory.Any())
                foreach (var item in _gameState.PlayerState.ArmorsInventory) sb.AppendLine($"  - {item.Key}: {item.Value}");
            else sb.AppendLine("  (Empty)");

            sb.AppendLine("[teal]Relics:[/]");
            if (_gameState.PlayerState.RelicInventory.Any())
                foreach (var item in _gameState.PlayerState.RelicInventory) sb.AppendLine($"  - {item.Key}: {item.Value}");
            else sb.AppendLine("  (Empty)");

            sb.AppendLine("[teal]Consumables:[/]");
            if (_gameState.PlayerState.ConsumableInventory.Any())
                foreach (var item in _gameState.PlayerState.ConsumableInventory) sb.AppendLine($"  - {item.Key}: {item.Value}");
            else sb.AppendLine("  (Empty)");

            foreach (var line in sb.ToString().Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = line });
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

                        string equippedStatus = "";
                        int equippedSlot = Array.IndexOf(_gameState.PlayerState.EquippedSpells, entry);
                        if (equippedSlot != -1)
                        {
                            equippedStatus = $" [yellow][Equipped: Slot {equippedSlot + 1}][/]";
                        }

                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"  Page {i + 1}: {moveName} [dim](Used: {entry.TimesUsed}) ({entry.MoveID})[/]{equippedStatus}" });
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

        private void HandleEquipRelic(string[] args)
        {
            _gameState ??= ServiceLocator.Get<GameState>();
            if (_gameState.PlayerState == null) return;

            if (args.Length < 3 || !int.TryParse(args[2], out int slot))
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Usage: equiprelic <AbilityID> <slot>" });
                return;
            }
            if (slot < 1 || slot > 3)
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Relic slot must be between 1 and 3." });
                return;
            }

            string relicId = args[1];
            if (!_gameState.PlayerState.RelicInventory.ContainsKey(relicId))
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[error]Player does not own relic '{relicId}'." });
                return;
            }

            // Unequip from any other slot if already equipped
            for (int i = 0; i < _gameState.PlayerState.EquippedRelics.Length; i++)
            {
                if (_gameState.PlayerState.EquippedRelics[i] == relicId)
                {
                    _gameState.PlayerState.EquippedRelics[i] = null;
                }
            }

            _gameState.PlayerState.EquippedRelics[slot - 1] = relicId;
            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"Equipped '{relicId}' to relic slot {slot}." });
        }

        private void HandleUnequipRelic(string[] args)
        {
            _gameState ??= ServiceLocator.Get<GameState>();
            if (_gameState.PlayerState == null) return;

            if (args.Length < 2 || !int.TryParse(args[1], out int slot))
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Usage: unequiprelic <slot>" });
                return;
            }
            if (slot < 1 || slot > 3)
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Relic slot must be between 1 and 3." });
                return;
            }

            string? relicId = _gameState.PlayerState.EquippedRelics[slot - 1];
            if (relicId != null)
            {
                _gameState.PlayerState.EquippedRelics[slot - 1] = null;
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"Unequipped '{relicId}' from relic slot {slot}." });
            }
            else
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"Relic slot {slot} is already empty." });
            }
        }

        private void HandleEquipWeapon(string[] args)
        {
            _gameState ??= ServiceLocator.Get<GameState>();
            if (_gameState.PlayerState == null) return;

            if (args.Length < 2)
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Usage: equipweapon <WeaponID>" });
                return;
            }
            string weaponId = args[1];
            if (!_gameState.PlayerState.WeaponsInventory.ContainsKey(weaponId))
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[error]Player does not own weapon '{weaponId}'." });
                return;
            }
            _gameState.PlayerState.EquippedWeaponId = weaponId;
            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"Equipped '{weaponId}'." });
        }

        private void HandleUnequipWeapon(string[] args)
        {
            _gameState ??= ServiceLocator.Get<GameState>();
            if (_gameState.PlayerState == null) return;

            if (_gameState.PlayerState.EquippedWeaponId != null)
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"Unequipped '{_gameState.PlayerState.EquippedWeaponId}'." });
                _gameState.PlayerState.EquippedWeaponId = null;
            }
            else
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "No weapon is equipped." });
            }
        }

        private void HandleEquipArmor(string[] args)
        {
            _gameState ??= ServiceLocator.Get<GameState>();
            if (_gameState.PlayerState == null) return;

            if (args.Length < 2)
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Usage: equiparmor <ArmorID>" });
                return;
            }
            string armorId = args[1];
            if (!_gameState.PlayerState.ArmorsInventory.ContainsKey(armorId))
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[error]Player does not own armor '{armorId}'." });
                return;
            }
            _gameState.PlayerState.EquippedArmorId = armorId;
            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"Equipped '{armorId}'." });
        }

        private void HandleUnequipArmor(string[] args)
        {
            _gameState ??= ServiceLocator.Get<GameState>();
            if (_gameState.PlayerState == null) return;

            if (_gameState.PlayerState.EquippedArmorId != null)
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"Unequipped '{_gameState.PlayerState.EquippedArmorId}'." });
                _gameState.PlayerState.EquippedArmorId = null;
            }
            else
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "No armor is equipped." });
            }
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
﻿
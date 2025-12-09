using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Progression;
using ProjectVagabond.Scenes;
using ProjectVagabond.UI;
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
        private readonly PlayerInputSystem _playerInputSystem;
        private GameState _gameState;
        private Dictionary<string, Command> _commands;
        public Dictionary<string, Command> Commands => _commands;
        public CommandProcessor(PlayerInputSystem playerInputSystem)
        {
            _playerInputSystem = playerInputSystem;
            InitializeCommands();
        }

        private void InitializeCommands()
        {
            _commands = new Dictionary<string, Command>();

            _commands["help"] = new Command("help", (args) =>
            {
                var sb = new StringBuilder();
                sb.AppendLine("[palette_yellow]Available Commands:[/]");
                sb.AppendLine("  [palette_teal]System & Debug[/]");
                sb.AppendLine("    test_abilities      - Runs unit tests on ability logic.");
                sb.AppendLine("    clear               - Clears console.");
                sb.AppendLine("    exit                - Exits game.");
                sb.AppendLine("    debugcombat         - Starts a random forest combat.");
                sb.AppendLine("    combatrun           - Flees from combat.");
                sb.AppendLine("    givestatus <slot> <type> [dur] - Apply status.");
                sb.AppendLine();
                sb.AppendLine("  [palette_teal]Party & Inventory[/]");
                sb.AppendLine("    addmember <id>      - Adds a party member.");
                sb.AppendLine("    inventory           - Shows all inventories.");
                sb.AppendLine("    giveweapon <id> [n] - Adds weapon(s).");
                sb.AppendLine("    equipweapon <id>    - Equips a weapon.");
                sb.AppendLine("    unequipweapon       - Unequips current weapon.");
                sb.AppendLine("    givearmor <id> [n]  - Adds armor(s).");
                sb.AppendLine("    giverelic <id> [n]  - Adds relic(s).");
                sb.AppendLine("    giveconsumable <id> [n] - Adds consumable(s).");
                sb.AppendLine("    givespell <id>      - Adds a spell.");

                foreach (var line in sb.ToString().Split(new[] { Environment.NewLine }, StringSplitOptions.None))
                {
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = line });
                }
            }, "help - Shows this help message.");

            _commands["clear"] = new Command("clear", (args) => ServiceLocator.Get<Utils.DebugConsole>().ClearHistory(), "clear - Clears history.");

            // --- TEST COMMAND ---
            _commands["test_abilities"] = new Command("test_abilities", (args) =>
            {
                AbilityTester.RunAllTests();
            }, "test_abilities - Runs logic verification on ability classes.");

            // --- PARTY COMMANDS ---
            _commands["addmember"] = new Command("addmember", (args) =>
            {
                _gameState ??= ServiceLocator.Get<GameState>();
                if (_gameState.PlayerState == null) return;
                if (args.Length < 2) { EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Usage: addmember <MemberID>" }); return; }

                string memberId = args[1];

                var newMember = PartyMemberFactory.CreateMember(memberId);
                if (newMember != null)
                {
                    if (_gameState.PlayerState.AddPartyMember(newMember))
                    {
                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[palette_teal]Added {newMember.Name} to the party!" });
                        if (BattleDataCache.PartyMembers.TryGetValue(memberId, out var data))
                        {
                            foreach (var kvp in data.StartingEquipment)
                            {
                                if (BattleDataCache.Weapons.ContainsKey(kvp.Key)) _gameState.PlayerState.AddWeapon(kvp.Key, kvp.Value);
                                else if (BattleDataCache.Armors.ContainsKey(kvp.Key)) _gameState.PlayerState.AddArmor(kvp.Key, kvp.Value);
                            }
                        }
                    }
                    else
                    {
                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Failed to add member (Duplicate or Full)." });
                    }
                }
                else
                {
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[error]Member ID '{memberId}' not found." });
                }

            }, "addmember <id> - Adds a party member.",
            (args) => args.Length == 0 ? BattleDataCache.PartyMembers.Keys.ToList() : new List<string>());

            // --- INVENTORY COMMANDS ---
            _commands["inventory"] = new Command("inventory", (args) => HandleShowInventory(), "inventory - Shows all inventories.");

            _commands["giveweapon"] = new Command("giveweapon", (args) => HandleGiveItem(args, "Weapon"), "giveweapon <id> [n]");

            _commands["equipweapon"] = new Command("equipweapon", (args) =>
            {
                _gameState ??= ServiceLocator.Get<GameState>();
                if (_gameState.PlayerState == null) return;
                if (args.Length < 2) { EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Usage: equipweapon <WeaponID>" }); return; }

                string weaponId = args[1];
                if (!_gameState.PlayerState.Weapons.ContainsKey(weaponId))
                {
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[error]You do not have '{weaponId}' in your inventory." });
                    return;
                }

                if (!BattleDataCache.Weapons.ContainsKey(weaponId))
                {
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[error]Weapon data for '{weaponId}' not found." });
                    return;
                }

                _gameState.PlayerState.EquippedWeaponId = weaponId;
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[palette_teal]Equipped {weaponId}." });

            }, "equipweapon <id> - Equips a weapon from inventory.",
            (args) => _gameState?.PlayerState?.Weapons.Keys.ToList() ?? new List<string>());

            _commands["unequipweapon"] = new Command("unequipweapon", (args) =>
            {
                _gameState ??= ServiceLocator.Get<GameState>();
                if (_gameState.PlayerState == null) return;
                _gameState.PlayerState.EquippedWeaponId = null;
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "Unequipped weapon." });
            }, "unequipweapon - Unequips current weapon.");

            _commands["givearmor"] = new Command("givearmor", (args) => HandleGiveItem(args, "Armor"), "givearmor <id> [n]");
            _commands["giverelic"] = new Command("giverelic", (args) => HandleGiveItem(args, "Relic"), "giverelic <id> [n]",
                (args) => args.Length == 0 ? BattleDataCache.Relics.Keys.ToList() : new List<string>());
            _commands["giveconsumable"] = new Command("giveconsumable", (args) => HandleGiveItem(args, "Consumable"), "giveconsumable <id> [n]",
                (args) => args.Length == 0 ? BattleDataCache.Consumables.Keys.ToList() : new List<string>());

            _commands["removeweapon"] = new Command("removeweapon", (args) => HandleRemoveItem(args, "Weapon"), "removeweapon <id> [n]");
            _commands["removearmor"] = new Command("removearmor", (args) => HandleRemoveItem(args, "Armor"), "removearmor <id> [n]");
            _commands["removerelic"] = new Command("removerelic", (args) => HandleRemoveItem(args, "Relic"), "removerelic <id> [n]");
            _commands["removeconsumable"] = new Command("removeconsumable", (args) => HandleRemoveItem(args, "Consumable"), "removeconsumable <id> [n]");

            // --- MOVE COMMANDS ---
            _commands["givespell"] = new Command("givespell", (args) =>
            {
                if (args.Length < 2) { EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Usage: givespell <MoveID>" }); return; }
                EventBus.Publish(new GameEvents.PlayerMoveAdded { MoveID = args[1], Type = GameEvents.AcquisitionType.Add });
            }, "givespell <id> - Adds spell.", (args) => args.Length == 0 ? BattleDataCache.Moves.Values.Where(m => m.MoveType == MoveType.Spell).Select(m => m.MoveID).ToList() : new List<string>());

            _commands["removespell"] = new Command("removespell", (args) =>
            {
                if (args.Length < 2) { EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Usage: removespell <MoveID>" }); return; }
                EventBus.Publish(new GameEvents.PlayerMoveAdded { MoveID = args[1], Type = GameEvents.AcquisitionType.Remove });
            }, "removespell <id>");

            _commands["giveaction"] = new Command("giveaction", (args) =>
            {
                if (args.Length < 2) { EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Usage: giveaction <MoveID>" }); return; }
                EventBus.Publish(new GameEvents.PlayerMoveAdded { MoveID = args[1], Type = GameEvents.AcquisitionType.Add });
            }, "giveaction <id> - Adds action.", (args) => args.Length == 0 ? BattleDataCache.Moves.Values.Where(m => m.MoveType == MoveType.Action).Select(m => m.MoveID).ToList() : new List<string>());

            _commands["removeaction"] = new Command("removeaction", (args) =>
            {
                if (args.Length < 2) { EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Usage: removeaction <MoveID>" }); return; }
                EventBus.Publish(new GameEvents.PlayerMoveAdded { MoveID = args[1], Type = GameEvents.AcquisitionType.Remove });
            }, "removeaction <id>");

            // --- EQUIP SPELL ---
            _commands["equip"] = new Command("equip", (args) => HandleEquipSpell(args), "equip <moveId> <slot> - Equips a spell.");
            _commands["unequip"] = new Command("unequip", (args) => HandleUnequipSpell(args), "unequip <slot>");

            // --- DEBUG COMBAT ---
            _commands["debugcombat"] = new Command("debugcombat", (args) =>
            {
                var sceneManager = ServiceLocator.Get<SceneManager>();
                if (sceneManager.CurrentActiveScene is SplitMapScene splitScene)
                {
                    var progressionManager = ServiceLocator.Get<ProgressionManager>();
                    var encounter = progressionManager.GetRandomBattleFromSplit("Forest");

                    if (encounter != null && encounter.Any())
                    {
                        splitScene.InitiateCombat(encounter);
                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[palette_teal]Starting debug combat (Forest)..." });
                    }
                    else
                    {
                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Could not load Forest encounter data." });
                    }
                }
                else
                {
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Command only available in Split Map Scene." });
                }
            }, "debugcombat - Starts a random forest encounter (SplitMap only).");

            _commands["combatrun"] = new Command("combatrun", (args) =>
            {
                var sceneManager = ServiceLocator.Get<SceneManager>();
                if (sceneManager.CurrentActiveScene is BattleScene battleScene)
                {
                    battleScene.TriggerFlee();
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "Attempting to flee..." });
                }
                else
                {
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Not in combat." });
                }
            }, "combatrun - Flees from combat if active.");

            _commands["givestatus"] = new Command("givestatus", (args) =>
            {
                var sceneManager = ServiceLocator.Get<SceneManager>();
                if (!(sceneManager.CurrentActiveScene is BattleScene))
                {
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Not in combat." });
                    return;
                }

                if (args.Length < 3)
                {
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Usage: givestatus <slot 1-4> <StatusType> [duration]" });
                    return;
                }

                if (!int.TryParse(args[1], out int slot) || slot < 1 || slot > 4)
                {
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Invalid slot. Use 1-4 (1=P1, 2=P2, 3=E1, 4=E2)." });
                    return;
                }

                if (!Enum.TryParse<StatusEffectType>(args[2], true, out var statusType))
                {
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[error]Invalid status type '{args[2]}'." });
                    return;
                }

                int duration = 3;
                if (args.Length > 3) int.TryParse(args[3], out duration);

                var battleManager = ServiceLocator.Get<BattleManager>();
                BattleCombatant target = null;

                if (slot == 1) target = battleManager.AllCombatants.FirstOrDefault(c => c.IsPlayerControlled && c.BattleSlot == 0);
                else if (slot == 2) target = battleManager.AllCombatants.FirstOrDefault(c => c.IsPlayerControlled && c.BattleSlot == 1);
                else if (slot == 3) target = battleManager.AllCombatants.FirstOrDefault(c => !c.IsPlayerControlled && c.BattleSlot == 0);
                else if (slot == 4) target = battleManager.AllCombatants.FirstOrDefault(c => !c.IsPlayerControlled && c.BattleSlot == 1);

                if (target == null || target.IsDefeated)
                {
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[error]Slot {slot} is empty or defeated." });
                    return;
                }

                target.AddStatusEffect(new StatusEffectInstance(statusType, duration));
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[palette_teal]Applied {statusType} to {target.Name} for {duration} turns." });

            }, "givestatus <slot> <type> [dur] - Apply status in combat.",
            (args) =>
            {
                if (args.Length == 0) return new List<string> { "1", "2", "3", "4" };
                if (args.Length == 1) return Enum.GetNames(typeof(StatusEffectType)).ToList();
                if (args.Length == 2) return new List<string> { "1", "2", "3", "4", "5" };
                return new List<string>();
            });

            _commands["exit"] = new Command("exit", (args) => ServiceLocator.Get<Core>().ExitApplication(), "exit");
        }

        // --- HANDLERS ---

        private void HandleGiveItem(string[] args, string type)
        {
            _gameState ??= ServiceLocator.Get<GameState>();
            if (_gameState.PlayerState == null) return;
            if (args.Length < 2) { EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Usage: give... <id> [qty]" }); return; }

            string id = args[1];
            int qty = 1;
            if (args.Length > 2) int.TryParse(args[2], out qty);

            switch (type)
            {
                case "Weapon": _gameState.PlayerState.AddWeapon(id, qty); break;
                case "Armor": _gameState.PlayerState.AddArmor(id, qty); break;
                case "Relic": _gameState.PlayerState.AddRelic(id, qty); break;
                case "Consumable": _gameState.PlayerState.AddConsumable(id, qty); break;
            }
            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"Added {qty}x {id} to {type} inventory." });
        }

        private void HandleRemoveItem(string[] args, string type)
        {
            _gameState ??= ServiceLocator.Get<GameState>();
            if (_gameState.PlayerState == null) return;
            if (args.Length < 2) { EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Usage: remove... <id> [qty]" }); return; }

            string id = args[1];
            int qty = 1;
            if (args.Length > 2) int.TryParse(args[2], out qty);

            switch (type)
            {
                case "Weapon": _gameState.PlayerState.RemoveWeapon(id, qty); break;
                case "Armor": _gameState.PlayerState.RemoveArmor(id, qty); break;
                case "Relic": _gameState.PlayerState.RemoveRelic(id, qty); break;
                case "Consumable": _gameState.PlayerState.RemoveConsumable(id, qty); break;
            }
            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"Removed {qty}x {id} from {type} inventory." });
        }

        private void HandleShowInventory()
        {
            _gameState ??= ServiceLocator.Get<GameState>();
            if (_gameState.PlayerState == null) return;
            var ps = _gameState.PlayerState;

            if (!string.IsNullOrEmpty(ps.EquippedWeaponId))
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[palette_teal]Equipped Weapon:[/] {ps.EquippedWeaponId}" });
            }

            PrintDict(ps.Weapons, "Weapons");
            PrintDict(ps.Armors, "Armors");
            PrintDict(ps.Relics, "Relics");
            PrintDict(ps.Consumables, "Consumables");

            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[palette_teal]Spells:[/]" });
            if (ps.Spells.Any())
            {
                foreach (var spell in ps.Spells)
                {
                    string equipped = Array.IndexOf(ps.EquippedSpells, spell) != -1 ? " [yellow][Equipped][/]" : "";
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"  {spell.MoveID} (Used: {spell.TimesUsed}){equipped}" });
                }
            }
            else EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "  (Empty)" });

            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[palette_teal]Actions:[/]" });
            if (ps.Actions.Any())
            {
                foreach (var action in ps.Actions)
                {
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"  {action.MoveID} (Used: {action.TimesUsed})" });
                }
            }
            else EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "  (Empty)" });
        }

        private void PrintDict(Dictionary<string, int> dict, string title)
        {
            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[palette_teal]{title}:[/]" });
            if (!dict.Any()) EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "  (Empty)" });
            else foreach (var kvp in dict) EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"  {kvp.Key}: {kvp.Value}" });
        }

        private void HandleEquipSpell(string[] args)
        {
            _gameState ??= ServiceLocator.Get<GameState>();
            if (_gameState.PlayerState == null) return;
            if (args.Length < 3 || !int.TryParse(args[2], out int slot)) { EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Usage: equip <MoveID> <slot>" }); return; }
            string moveId = args[1];

            if (slot < 1 || slot > 4) { EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Slot must be 1-4." }); return; }

            var spellEntry = _gameState.PlayerState.Spells.FirstOrDefault(s => s.MoveID.Equals(moveId, StringComparison.OrdinalIgnoreCase));
            if (spellEntry == null) { EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[error]You don't know the spell '{moveId}'." }); return; }

            for (int i = 0; i < 4; i++) if (_gameState.PlayerState.EquippedSpells[i] == spellEntry) _gameState.PlayerState.EquippedSpells[i] = null;

            _gameState.PlayerState.EquippedSpells[slot - 1] = spellEntry;
            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"Equipped {spellEntry.MoveID} to slot {slot}." });
        }

        private void HandleUnequipSpell(string[] args)
        {
            _gameState ??= ServiceLocator.Get<GameState>();
            if (_gameState.PlayerState == null) return;
            if (args.Length < 2 || !int.TryParse(args[1], out int slot) || slot < 1 || slot > 4) return;
            _gameState.PlayerState.EquippedSpells[slot - 1] = null;
            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"Unequipped slot {slot}." });
        }

        public void ProcessCommand(string input)
        {
            if (string.IsNullOrEmpty(input)) return;
            string[] parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return;
            string cmd = parts[0].ToLower();
            if (_commands.TryGetValue(cmd, out var command)) command.Action(parts);
            else EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "Unknown command." });
        }
    }
}
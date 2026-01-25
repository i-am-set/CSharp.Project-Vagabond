using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Dice;
using ProjectVagabond.Progression;
using ProjectVagabond.Scenes;
using ProjectVagabond.Transitions;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;


namespace ProjectVagabond
{
    public class CommandProcessor
    {
        private GameState _gameState;
        private Dictionary<string, Command> _commands;
        public Dictionary<string, Command> Commands => _commands;

        // Removed PlayerInputSystem dependency
        public CommandProcessor()
        {
            InitializeCommands();
        }

        private void Log(string message)
        {
            GameLogger.Log(LogSeverity.Info, message);
            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = message });
        }

        private void InitializeCommands()
        {
            _commands = new Dictionary<string, Command>();

            _commands["help"] = new Command("help", (args) =>
            {
                var sb = new StringBuilder();
                sb.AppendLine("[Palette_DarkSun]Available Commands:[/]");
                sb.AppendLine("  [Palette_Sky]System & Debug[/]");
                sb.AppendLine("    debug_text_anims                   - Shows all text animations.");
                sb.AppendLine("    debug_colors                       - Lists all colors.");
                sb.AppendLine("    clear                              - Clears console.");
                sb.AppendLine("    exit                               - Exits game.");
                sb.AppendLine("    debug_combat                       - Starts a random forest combat.");
                sb.AppendLine("    debug_shop                         - Opens a random shop interface.");
                sb.AppendLine("    debug_rest                         - Opens the rest site interface.");
                sb.AppendLine("    debug_recruit                      - Opens the recruit interface.");
                sb.AppendLine("    debug_combatrun                    - Flees from combat.");
                sb.AppendLine("    debug_givestatus <slot> <type> {dur} - Apply status.");
                sb.AppendLine("    debug_consolefont <0|1|2>          - Sets the debug console font.");
                sb.AppendLine("    debug_damageparty <slot> <%>             - Damages member.");
                sb.AppendLine("    test_party_gen                     - Tests random move generation.");
                sb.AppendLine("    debug_passives                     - Lists party passive abilities.");
                sb.AppendLine();
                sb.AppendLine("  [Palette_Sky]Party & Inventory[/]");
                sb.AppendLine("    addmember <id>                     - Adds a party member.");
                sb.AppendLine("    inventory                          - Shows all inventories.");
                sb.AppendLine("    giveall                            - Gives 1 of every item.");
                sb.AppendLine("    givecoin <amount>                  - Adds coin.");
                sb.AppendLine("    setcoin <amount>                   - Sets coin amount.");
                sb.AppendLine("    removecoin <amount>                - Removes coin.");
                sb.AppendLine("    giverelic <id>                     - Adds relic.");
                sb.AppendLine("    givespell <id>                     - Adds a spell.");

                foreach (var line in sb.ToString().Split(new[] { Environment.NewLine }, StringSplitOptions.None))
                {
                    Log(line);
                }
            }, "help - Shows this help message.");

            _commands["clear"] = new Command("clear", (args) => ServiceLocator.Get<Utils.DebugConsole>().ClearHistory(), "clear - Clears history.");

            // --- TEXT ANIMATION DEBUG ---
            _commands["debug_text_anims"] = new Command("debug_text_anims", (args) =>
            {
                Log("--- Text Animation Showcase ---");
                Log("[wave]Wave: The quick brown fox jumps over the lazy dog.[/]");
                Log("[popwave]PopWave: The quick brown fox jumps over the lazy dog.[/]");
                Log("[pop]Pop: The quick brown fox jumps over the lazy dog.[/]");
                Log("[shake]Shake: The quick brown fox jumps over the lazy dog.[/]");
                Log("[wobble]Wobble: The quick brown fox jumps over the lazy dog.[/]");
                Log("[nervous]Nervous: The quick brown fox jumps over the lazy dog.[/]");
                Log("[rainbow]Rainbow: The quick brown fox jumps over the lazy dog.[/]");
                Log("[rainbowwave]RainbowWave: The quick brown fox jumps over the lazy dog.[/]");
                Log("[bounce]Bounce: The quick brown fox jumps over the lazy dog.[/]");
                Log("[drift]Drift: The quick brown fox jumps over the lazy dog.[/]");
                Log("[glitch]Glitch: The quick brown fox jumps over the lazy dog.[/]");
                Log("[flicker]Flicker: The quick brown fox jumps over the lazy dog.[/]");
                Log("[driftbounce]DriftBounce: The quick brown fox jumps over the lazy dog.[/]");
                Log("[driftwave]DriftWave: The quick brown fox jumps over the lazy dog.[/]");
                Log("[flickerbounce]FlickerBounce: The quick brown fox jumps over the lazy dog.[/]");
                Log("[flickerwave]FlickerWave: The quick brown fox jumps over the lazy dog.[/]");
            }, "debug_text_anims - Displays all available text animations.");

            // --- COLORS COMMAND ---
            _commands["debug_colors"] = new Command("debug_colors", (args) =>
            {
                var colorType = typeof(Color);
                var properties = colorType.GetProperties(BindingFlags.Public | BindingFlags.Static);
                var colorList = new List<(Color Color, string Name)>();

                foreach (var p in properties)
                {
                    if (p.PropertyType == typeof(Color))
                    {
                        colorList.Add(((Color)p.GetValue(null), p.Name));
                    }
                }

                // Helper to get Hue (0-360)
                float GetHue(Color c)
                {
                    float r = c.R / 255f;
                    float g = c.G / 255f;
                    float b = c.B / 255f;
                    float max = Math.Max(r, Math.Max(g, b));
                    float min = Math.Min(r, Math.Min(g, b));
                    float delta = max - min;

                    if (delta == 0) return 0;
                    if (max == r) return 60 * (((g - b) / delta) % 6);
                    if (max == g) return 60 * (((b - r) / delta) + 2);
                    return 60 * (((r - g) / delta) + 4);
                }

                // Helper to get Saturation (0-1)
                float GetSaturation(Color c)
                {
                    float r = c.R / 255f;
                    float g = c.G / 255f;
                    float b = c.B / 255f;
                    float max = Math.Max(r, Math.Max(g, b));
                    float min = Math.Min(r, Math.Min(g, b));
                    if (max == 0) return 0;
                    return (max - min) / max;
                }

                // Helper to get Brightness/Value
                float GetBrightness(Color c)
                {
                    return Math.Max(c.R, Math.Max(c.G, c.B)) / 255f;
                }

                // Sort: Rainbow (Hue) first, Grayscale last
                colorList.Sort((a, b) =>
                {
                    float satA = GetSaturation(a.Color);
                    float satB = GetSaturation(b.Color);
                    bool grayA = satA < 0.1f || (a.Color.R == a.Color.G && a.Color.G == a.Color.B);
                    bool grayB = satB < 0.1f || (b.Color.R == b.Color.G && b.Color.G == b.Color.B);

                    if (grayA && !grayB) return 1;
                    if (!grayA && grayB) return -1;

                    if (grayA && grayB)
                    {
                        return GetBrightness(b.Color).CompareTo(GetBrightness(a.Color));
                    }

                    float hueA = GetHue(a.Color);
                    float hueB = GetHue(b.Color);
                    if (Math.Abs(hueA - hueB) > 1f) return hueA.CompareTo(hueB);

                    return GetBrightness(b.Color).CompareTo(GetBrightness(a.Color));
                });

                Log("--- MonoGame Colors (Rainbow Order) ---");
                foreach (var (color, name) in colorList)
                {
                    if (color == Color.Transparent) continue;
                    Log($"[{name}]{name}[/]");
                }

            }, "colors - Lists all MonoGame colors in rainbow order.");

            // --- TEST COMMANDS ---
            _commands["test_abilities"] = new Command("test_abilities", (args) =>
            {
                AbilityTester.RunAllTests();
            }, "test_abilities - Runs logic verification on ability classes.");

            _commands["test_items"] = new Command("test_items", (args) =>
            {
                ItemIntegrityTester.RunIntegrityCheck();
            }, "test_items - Checks if all defined items can instantiate their abilities.");

            _commands["test_party_gen"] = new Command("test_party_gen", (args) =>
            {
                Log("[Palette_DarkSun]Testing Oakley Generation (10 iterations):[/]");
                for (int i = 0; i < 10; i++)
                {
                    var member = PartyMemberFactory.CreateMember("0"); // Oakley
                    if (member != null)
                    {
                        string move1 = member.Spells[0]?.MoveID ?? "Empty";
                        string moveName = "Unknown";
                        if (BattleDataCache.Moves.TryGetValue(move1, out var m)) moveName = m.MoveName;
                        Log($"  Iter {i + 1}: Slot 1 = {move1} ({moveName})");
                    }
                }
            }, "test_party_gen - Generates Oakley 10 times to verify random move slots.");

            // --- PARTY COMMANDS ---
            _commands["addmember"] = new Command("addmember", (args) =>
            {
                _gameState ??= ServiceLocator.Get<GameState>();
                if (_gameState.PlayerState == null) return;
                if (args.Length < 2) { Log("[error]Usage: addmember <MemberID>"); return; }

                string memberId = args[1];

                var newMember = PartyMemberFactory.CreateMember(memberId);
                if (newMember != null)
                {
                    if (_gameState.PlayerState.AddPartyMember(newMember))
                    {
                        Log($"[Palette_Sky]Added {newMember.Name} to the party!");
                        // Starting relics are now global, so we add them to the player state
                        if (BattleDataCache.PartyMembers.TryGetValue(memberId, out var data))
                        {
                            foreach (var kvp in data.StartingRelics) _gameState.PlayerState.AddRelic(kvp.Key);
                        }
                    }
                    else
                    {
                        Log("[error]Failed to add member (Duplicate or Full).");
                    }
                }
                else
                {
                    Log($"[error]Member ID '{memberId}' not found.");
                }

            }, "addmember <id> - Adds a party member.",
            (args) => args.Length == 0 ? BattleDataCache.PartyMembers.Keys.ToList() : new List<string>());

            _commands["debug_damagepartymember"] = new Command("damageparty", (args) =>
            {
                _gameState ??= ServiceLocator.Get<GameState>();
                if (_gameState.PlayerState == null) return;

                if (args.Length < 3)
                {
                    Log("[error]Usage: damageparty <slot 1-4> <percent>");
                    return;
                }

                if (!int.TryParse(args[1], out int slot) || slot < 1 || slot > 4)
                {
                    Log("[error]Invalid slot. Use 1-4.");
                    return;
                }

                if (!float.TryParse(args[2], out float percent))
                {
                    Log("[error]Invalid percentage.");
                    return;
                }

                int index = slot - 1;
                if (index >= _gameState.PlayerState.Party.Count)
                {
                    Log($"[error]Slot {slot} is empty.");
                    return;
                }

                var member = _gameState.PlayerState.Party[index];
                int damage = (int)(member.MaxHP * (percent / 100f));
                int oldHP = member.CurrentHP;
                member.CurrentHP = Math.Max(0, member.CurrentHP - damage);

                Log($"[Palette_Rust]Damaged {member.Name} for {damage} HP ({oldHP} -> {member.CurrentHP}).");

            }, "damageparty <slot> <percent> - Damages a party member by % of Max HP.",
            (args) => args.Length == 0 ? new List<string> { "1", "2", "3", "4" } : new List<string>());

            _commands["debug_passives"] = new Command("debug_passives", (args) =>
            {
                _gameState ??= ServiceLocator.Get<GameState>();
                if (_gameState.PlayerState == null) { Log("[error]No active game state."); return; }

                Log("[Palette_Sky]--- Party Passive Abilities ---[/]");
                int slot = 1;
                foreach (var member in _gameState.PlayerState.Party)
                {
                    Log($"[Palette_DarkSun]Slot {slot}: {member.Name}[/]");

                    // Intrinsic
                    bool hasIntrinsic = false;
                    if (member.IntrinsicAbilities != null && member.IntrinsicAbilities.Count > 0)
                    {
                        foreach (var kvp in member.IntrinsicAbilities)
                        {
                            Log($"  [cGreen]Intrinsic:[/] {kvp.Key} ({kvp.Value})");
                            hasIntrinsic = true;
                        }
                    }
                    if (!hasIntrinsic) Log("  [cGreen]Intrinsic:[/] None");
                    slot++;
                }

                Log("[Palette_Sky]--- Global Relics ---[/]");
                if (_gameState.PlayerState.GlobalRelics.Any())
                {
                    foreach (var relicId in _gameState.PlayerState.GlobalRelics)
                    {
                        if (BattleDataCache.Relics.TryGetValue(relicId, out var relic))
                            Log($"  [cBlue]Relic:[/] {relic.RelicName} - {relic.AbilityName}");
                        else
                            Log($"  [cBlue]Relic:[/] {relicId} (Unknown)");
                    }
                }
                else
                {
                    Log("  [cBlue]Relic:[/] None");
                }

            }, "debug_passives - Lists intrinsic and relic abilities.");

            // --- INVENTORY COMMANDS ---
            _commands["inventory"] = new Command("inventory", (args) => HandleShowInventory(), "inventory - Shows all inventories.");

            _commands["givecoin"] = new Command("givecoin", (args) =>
            {
                _gameState ??= ServiceLocator.Get<GameState>();
                if (_gameState.PlayerState == null) return;
                if (args.Length < 2 || !int.TryParse(args[1], out int amount)) { Log("[error]Usage: givecoin <amount>"); return; }
                _gameState.PlayerState.Coin += amount;
                Log($"[Palette_Sky]Added {amount} coin. Total: {_gameState.PlayerState.Coin}");
            }, "givecoin <amount> - Adds coin.");

            _commands["setcoin"] = new Command("setcoin", (args) =>
            {
                _gameState ??= ServiceLocator.Get<GameState>();
                if (_gameState.PlayerState == null) return;
                if (args.Length < 2 || !int.TryParse(args[1], out int amount) || amount < 0) { Log("[error]Usage: setcoin <amount >= 0>"); return; }
                _gameState.PlayerState.Coin = amount;
                Log($"[Palette_Sky]Set coin to {amount}.");
            }, "setcoin <amount> - Sets coin amount.");

            _commands["removecoin"] = new Command("removecoin", (args) =>
            {
                _gameState ??= ServiceLocator.Get<GameState>();
                if (_gameState.PlayerState == null) return;
                if (args.Length < 2 || !int.TryParse(args[1], out int amount)) { Log("[error]Usage: removecoin <amount>"); return; }
                _gameState.PlayerState.Coin -= amount;
                Log($"[Palette_Sky]Removed {amount} coin. Total: {_gameState.PlayerState.Coin}");
            }, "removecoin <amount> - Removes coin.");

            // Removed giveweapon, equipweapon, unequipweapon

            _commands["giverelic"] = new Command("giverelic", (args) => HandleGiveItem(args, "Relic"), "giverelic <id>",
                (args) => args.Length == 0 ? BattleDataCache.Relics.Keys.ToList() : new List<string>());

            _commands["giveall"] = new Command("giveall", (args) =>
            {
                _gameState ??= ServiceLocator.Get<GameState>();
                if (_gameState.PlayerState == null) return;

                int count = 0;
                foreach (var id in BattleDataCache.Relics.Keys) { _gameState.PlayerState.AddRelic(id); count++; }

                Log($"[Palette_Sky]Added {count} relics to inventory.");
            }, "giveall - Adds 1 of every relic to inventory.");

            // Removed removeweapon
            _commands["removerelic"] = new Command("removerelic", (args) => HandleRemoveItem(args, "Relic"), "removerelic <id>");

            // --- MOVE COMMANDS ---
            _commands["givespell"] = new Command("givespell", (args) =>
            {
                if (args.Length < 2) { Log("[error]Usage: givespell <MoveID>"); return; }
                EventBus.Publish(new GameEvents.PlayerMoveAdded { MoveID = args[1], Type = GameEvents.AcquisitionType.Add });
            }, "givespell <id> - Adds spell.", (args) => args.Length == 0 ? BattleDataCache.Moves.Values.Where(m => m.MoveType == MoveType.Spell).Select(m => m.MoveID).ToList() : new List<string>());

            _commands["removespell"] = new Command("removespell", (args) =>
            {
                if (args.Length < 2) { Log("[error]Usage: removespell <MoveID>"); return; }
                EventBus.Publish(new GameEvents.PlayerMoveAdded { MoveID = args[1], Type = GameEvents.AcquisitionType.Remove });
            }, "removespell <id>");

            _commands["giveaction"] = new Command("giveaction", (args) =>
            {
                if (args.Length < 2) { Log("[error]Usage: giveaction <MoveID>"); return; }
                EventBus.Publish(new GameEvents.PlayerMoveAdded { MoveID = args[1], Type = GameEvents.AcquisitionType.Add });
            }, "giveaction <id> - Adds action.", (args) => args.Length == 0 ? BattleDataCache.Moves.Values.Where(m => m.MoveType == MoveType.Action).Select(m => m.MoveID).ToList() : new List<string>());

            _commands["removeaction"] = new Command("removeaction", (args) =>
            {
                if (args.Length < 2) { Log("[error]Usage: removeaction <MoveID>"); return; }
                EventBus.Publish(new GameEvents.PlayerMoveAdded { MoveID = args[1], Type = GameEvents.AcquisitionType.Remove });
            }, "removeaction <id>");

            // --- DEBUG COMBAT ---
            _commands["debug_combat"] = new Command("debug_combat", (args) =>
            {
                var sceneManager = ServiceLocator.Get<SceneManager>();
                if (sceneManager.CurrentActiveScene is SplitMapScene splitScene)
                {
                    var progressionManager = ServiceLocator.Get<ProgressionManager>();
                    var encounter = progressionManager.GetRandomBattleFromSplit("Forest");

                    if (encounter != null && encounter.Any())
                    {
                        splitScene.InitiateCombat(encounter);
                        Log("[Palette_Leaf]Starting debug combat (Forest)...");
                    }
                    else
                    {
                        Log("[error]Could not load Forest encounter data.");
                    }
                }
                else
                {
                    Log("[error]Command only available in Split Map Scene.");
                }
            }, "debugcombat - Starts a random forest encounter (SplitMap only).");

            // --- DEBUG SHOP ---
            _commands["debug_shop"] = new Command("debug_shop", (args) =>
            {
                var sceneManager = ServiceLocator.Get<SceneManager>();
                if (sceneManager.CurrentActiveScene is SplitMapScene splitScene)
                {
                    splitScene.DebugTriggerShop();
                    Log("[Palette_Sky]Opening debug shop...");
                }
                else
                {
                    Log("[error]Command only available in Split Map Scene.");
                }
            }, "debug_shop - Opens a random shop interface.");

            // --- DEBUG REST ---
            _commands["debug_rest"] = new Command("debug_rest", (args) =>
            {
                var sceneManager = ServiceLocator.Get<SceneManager>();
                if (sceneManager.CurrentActiveScene is SplitMapScene splitScene)
                {
                    splitScene.DebugTriggerRest();
                    Log("[Palette_Sky]Opening debug rest site...");
                }
                else
                {
                    Log("[error]Command only available in Split Map Scene.");
                }
            }, "debug_rest - Opens the rest site interface.");

            // --- DEBUG RECRUIT ---
            _commands["debug_recruit"] = new Command("debug_recruit", (args) =>
            {
                var sceneManager = ServiceLocator.Get<SceneManager>();
                if (sceneManager.CurrentActiveScene is SplitMapScene splitScene)
                {
                    splitScene.DebugTriggerRecruit();
                    Log("[Palette_Sky]Opening debug recruit menu...");
                }
                else
                {
                    Log("[error]Command only available in Split Map Scene.");
                }
            }, "debug_recruit - Opens the recruit interface.");

            _commands["debug_combatrun"] = new Command("debug_combatrun", (args) =>
            {
                var sceneManager = ServiceLocator.Get<SceneManager>();
                if (sceneManager.CurrentActiveScene is BattleScene battleScene)
                {
                    battleScene.TriggerFlee();
                    Log("Attempting to flee...");
                }
                else
                {
                    Log("[error]Not in combat.");
                }
            }, "debug_combatrun - Flees from combat if active.");

            _commands["debug_givestatus"] = new Command("debug_givestatus", (args) =>
            {
                var sceneManager = ServiceLocator.Get<SceneManager>();
                if (!(sceneManager.CurrentActiveScene is BattleScene))
                {
                    Log("[error]Command only available in combat.");
                    return;
                }

                if (args.Length < 3)
                {
                    Log("[error]Usage: debug_givestatus <party_slot_1-4> <StatusType> [duration]");
                    return;
                }

                // 1. Parse Slot
                if (!int.TryParse(args[1], out int slot) || slot < 1 || slot > 4)
                {
                    Log("[error]Invalid slot. Use 1-4.");
                    return;
                }

                var gameState = ServiceLocator.Get<GameState>();
                if (slot > gameState.PlayerState.Party.Count)
                {
                    Log($"[error]Slot {slot} is empty.");
                    return;
                }

                var partyMember = gameState.PlayerState.Party[slot - 1];
                var battleManager = ServiceLocator.Get<BattleManager>();
                var combatant = battleManager.AllCombatants.FirstOrDefault(c => c.Name == partyMember.Name && c.IsPlayerControlled);

                if (combatant == null)
                {
                    Log($"[error]Combatant for {partyMember.Name} not found in battle (maybe defeated/removed?).");
                    return;
                }

                // 2. Parse Status Type
                if (!Enum.TryParse<StatusEffectType>(args[2], true, out var statusType))
                {
                    Log($"[error]Invalid status type '{args[2]}'.");
                    return;
                }

                // 3. Check Temp/Perm and Duration
                bool isPerm = statusType == StatusEffectType.Poison ||
                              statusType == StatusEffectType.Burn ||
                              statusType == StatusEffectType.Frostbite;

                int duration = -1;

                if (!isPerm)
                {
                    if (args.Length < 4)
                    {
                        Log($"[error]Status '{statusType}' is temporary. Duration argument required.");
                        return;
                    }

                    if (!int.TryParse(args[3], out duration) || duration <= 0)
                    {
                        Log("[error]Duration must be an integer > 0.");
                        return;
                    }
                }

                // 4. Apply
                combatant.AddStatusEffect(new StatusEffectInstance(statusType, duration));
                string durText = isPerm ? "Permanent" : $"{duration} turns";
                Log($"[Palette_Sky]Applied {statusType} to {combatant.Name} ({durText}).");

            }, "debug_givestatus <slot> <type> [dur] - Apply status to party member in combat.",
            (args) =>
            {
                if (args.Length == 0) return new List<string> { "1", "2", "3", "4" };
                if (args.Length == 1) return Enum.GetNames(typeof(StatusEffectType)).ToList();
                if (args.Length == 2) return new List<string> { "1", "2", "3", "4", "5" };
                return new List<string>();
            });

            _commands["debug_consolefont"] = new Command("debug_consolefont", (args) =>
            {
                if (args.Length < 2 || !int.TryParse(args[1], out int index))
                {
                    Log("[error]Usage: debug_consolefont <0|1|2>");
                    return;
                }
                ServiceLocator.Get<DebugConsole>().SetFontIndex(index);
                Log($"[Palette_Sky]Debug Console Font set to index {index}.");
            }, "debug_consolefont <0|1|2> - Sets the debug console font.");

            _commands["exit"] = new Command("exit", (args) => ServiceLocator.Get<Core>().ExitApplication(), "exit");
        }

        // --- HANDLERS ---

        private void HandleGiveItem(string[] args, string type)
        {
            _gameState ??= ServiceLocator.Get<GameState>();
            if (_gameState.PlayerState == null) return;
            if (args.Length < 2) { Log("[error]Usage: give... <id>"); return; }

            string id = args[1];

            switch (type)
            {
                case "Relic": _gameState.PlayerState.AddRelic(id); break;
            }
            Log($"Added {id} to {type} inventory.");
        }

        private void HandleRemoveItem(string[] args, string type)
        {
            _gameState ??= ServiceLocator.Get<GameState>();
            if (_gameState.PlayerState == null) return;
            if (args.Length < 2) { Log("[error]Usage: remove... <id>"); return; }

            string id = args[1];

            switch (type)
            {
                case "Relic": _gameState.PlayerState.RemoveRelic(id); break;
            }
            Log($"Removed {id} from {type} inventory.");
        }

        private void HandleShowInventory()
        {
            _gameState ??= ServiceLocator.Get<GameState>();
            if (_gameState.PlayerState == null) return;
            var ps = _gameState.PlayerState;

            Log($"[Palette_Sky]Coin:[/] {ps.Coin}");

            Log($"[Palette_Sky]Global Relics:[/]");
            if (!ps.GlobalRelics.Any()) Log("  (Empty)");
            else foreach (var r in ps.GlobalRelics) Log($"  {r}");

            Log("[Palette_Sky]Spells:[/]");
            if (ps.Spells.Any(s => s != null))
            {
                for (int i = 0; i < ps.Spells.Length; i++)
                {
                    var spell = ps.Spells[i];
                    if (spell != null)
                    {
                        Log($"  Slot {i + 1}: {spell.MoveID} (Used: {spell.TimesUsed})");
                    }
                    else
                    {
                        Log($"  Slot {i + 1}: (Empty)");
                    }
                }
            }
            else Log("  (Empty)");

            Log("[Palette_Sky]Actions:[/]");
            if (ps.Actions.Any())
            {
                foreach (var action in ps.Actions)
                {
                    Log($"  {action.MoveID} (Used: {action.TimesUsed})");
                }
            }
            else Log("  (Empty)");
        }

        private void PrintDict(Dictionary<string, int> dict, string title)
        {
            Log($"[Palette_Sky]{title}:[/]");
            if (!dict.Any()) Log("  (Empty)");
            else foreach (var kvp in dict) Log($"  {kvp.Key}: {kvp.Value}");
        }

        public void ProcessCommand(string input)
        {
            if (string.IsNullOrEmpty(input)) return;
            string[] parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return;
            string cmd = parts[0].ToLower();
            if (_commands.TryGetValue(cmd, out var command)) command.Action(parts);
            else Log("Unknown command.");
        }
    }
}
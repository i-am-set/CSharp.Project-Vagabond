using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond;
using ProjectVagabond.Battle;
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
                sb.AppendLine("    debug_ticket                       - Spawns a blank ticket in the arena.");
                sb.AppendLine("    simulate_economy <days> <win_rate> - Simulates the economy headless.");
                sb.AppendLine("    clear                              - Clears console.");
                sb.AppendLine("    exit                               - Exits game.");
                sb.AppendLine("    fps                                - Toggles FPS counter.");
                sb.AppendLine("    debug_consolefont <0|1|2>          - Sets the debug console font.");

                foreach (var line in sb.ToString().Split(new[] { Environment.NewLine }, StringSplitOptions.None))
                {
                    Log(line);
                }
            }, "help - Shows this help message.");

            _commands["clear"] = new Command("clear", (args) => ServiceLocator.Get<Utils.DebugConsole>().ClearHistory(), "clear - Clears history.");

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

                float GetBrightness(Color c)
                {
                    return Math.Max(c.R, Math.Max(c.G, c.B)) / 255f;
                }

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

            _commands["debug_ticket"] = new Command("debug_ticket", (args) =>
            {
                var sceneManager = ServiceLocator.Get<SceneManager>();
                if (sceneManager.CurrentActiveScene is ArenaScene arena)
                {
                    arena.DebugPrintTicket();
                    Log("[Palette_Sky]Spawned debug ticket.[/]");
                }
                else
                {
                    Log("[error]Must be in the Arena scene to spawn a ticket.[/]");
                }
            }, "debug_ticket - Spawns a blank ticket in the arena for physics testing.");

            _commands["simulate_economy"] = new Command("simulate_economy", (args) =>
            {
                int days = 8;
                float winRate = 0.5f;

                if (args.Length > 1 && int.TryParse(args[1], out int parsedDays)) days = parsedDays;
                if (args.Length > 2 && float.TryParse(args[2], out float parsedRate)) winRate = parsedRate;

                Log($"--- Simulating Economy for {days} Days (Win Rate: {winRate:P0}) ---");

                int currentGold = ServiceLocator.Get<Global>().StartingGold;
                int currentDay = 1;
                Random rng = new Random();

                for (int i = 0; i < days; i++)
                {
                    int entryFee = 10 + (currentDay * 5);
                    currentGold -= entryFee;

                    if (currentGold < 0)
                    {
                        Log($"[Palette_Rust]BANKRUPT on Day {currentDay}! Short by {Math.Abs(currentGold)}G.[/]");
                        break;
                    }

                    bool isWin = rng.NextDouble() <= winRate;
                    int payout = 0;

                    if (isWin)
                    {
                        payout += (int)(entryFee * 1.2f);
                        payout += (int)(entryFee * 0.4f);
                        payout += (int)(entryFee * 0.4f);
                        payout += (int)(60f * (entryFee * 0.01f));
                    }
                    else
                    {
                        payout += (int)(entryFee * 0.2f);
                        payout += (int)(entryFee * 0.2f);
                        payout += (int)(30f * (entryFee * 0.01f));
                    }

                    currentGold += payout;
                    Log($"Day {currentDay} | Fee: {entryFee}G | Payout: {payout}G | Bank: {currentGold}G");

                    currentDay++;
                }

                if (currentGold >= 0)
                {
                    Log($"[Palette_Leaf]Simulation Complete. Final Bankroll: {currentGold}G[/]");
                }

            }, "simulate_economy <days> <win_rate> - Simulates the economy headless.");

            _commands["fps"] = new Command("fps", (args) =>
            {
                Global.Instance.ShowFPS = !Global.Instance.ShowFPS;
                Log($"[Palette_Sky]FPS Display: {(Global.Instance.ShowFPS ? "ON" : "OFF")}[/]");
            }, "fps - Toggles the FPS counter.");

            _commands["exit"] = new Command("exit", (args) => ServiceLocator.Get<Core>().ExitApplication(), "exit");
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
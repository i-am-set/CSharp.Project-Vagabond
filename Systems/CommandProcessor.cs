using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ProjectVagabond
{
    public class CommandProcessor
    {
        private readonly Dictionary<string, string> commandMap = new()
        {
            { "help", "help1" }, { "help1", "help1" },
            { "help2", "help2" },
            { "freemove", "freemove" }, { "free", "freemove" }, { "move", "freemove" },
            { "inspect", "inspect" }, { "i", "inspect" },
            { "iup", "inspectup" }, { "inspectup", "inspectup" }, { "iu", "inspectup" },
            { "ileft", "inspectleft" }, { "inspectleft", "inspectleft" }, { "il", "inspectleft" },
            { "idown", "inspectdown" }, { "inspectdown", "inspectdown" }, { "id", "inspectdown" },
            { "iright", "inspectright" }, { "inspectright", "inspectright" }, { "ir", "inspectright" },
            { "exit", "exit" }, { "quit", "exit" },
            { "rest", "rest" }, { "sleep", "rest" },
            { "clear", "clear" }, { "cls", "clear" },
            { "debug", "debug" },
            { "debugdamage", "debugdamage" },
            { "debugshownoise", "debugshownoise" }, { "noise", "debugshownoise" },
            { "debugshowterrain", "debugshowterrain" },
            { "debugshowlushness", "debugshowlushness" },
            { "debugshowtemp", "debugshowtemperature" }, { "showtemperature", "debugshowtemperature" },
            { "debugshowhumidity", "debugshowhumidity" },
            { "debugshowresources", "debugshowresources" },
            { "debugshowdifficulty", "debugshowdifficulty" },
            { "debughidenoise", "debughidenoise" }, { "debugnormalnoise", "debughidenoise" },
        };

        public void ProcessCommand(string rawInput, GameState gameState, MovementSystem movementSystem)
        {
            if (string.IsNullOrWhiteSpace(rawInput))
                return;

            string input = rawInput.ToLower().Replace(" ", "");
            string[] parts = rawInput.ToLower().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            // Check for movement commands first
            var movements = ParseMovementCommands(rawInput);
            if (movements.Any())
            {
                if (gameState.IsFreeMoving)
                {
                    foreach (var move in movements)
                    {
                        gameState.AddOutput($"Queued move: {move.direction.ToUpper()}");
                        gameState.MovePlayer(move.direction, move.steps);
                    }
                }
                else
                {
                    foreach (var move in movements)
                    {
                        movementSystem.OptimizePendingMoves(gameState.QueuedMoves, move.direction, move.steps);
                    }
            
                    // Update preview path
                    gameState.PreviewPath = movementSystem.GetPreviewPath(new List<(string, int)>(gameState.QueuedMoves));
                }
                return;
            }

            // Handle ENTER to execute queued moves
            if (input == "" && gameState.QueuedMoves.Any())
            {
                movementSystem.ExecuteMoves(gameState.QueuedMoves);
                gameState.PreviewPath.Clear();
                return;
            }

            // Check for mapped commands
            if (commandMap.TryGetValue(input, out string normalized))
            {
                ExecuteCommand(normalized, gameState);
                return;
            }

            gameState.AddOutput($"'{rawInput}' is invalid... try again.");
        }

        private List<(string direction, int steps)> ParseMovementCommands(string input)
        {
            var commands = new List<(string direction, int steps)>();
            var matches = Regex.Matches(input.ToLower(), @"(up|down|left|right)\s*(\d*)");

            foreach (Match match in matches)
            {
                string dir = match.Groups[1].Value;
                int steps = 1;

                if (!string.IsNullOrEmpty(match.Groups[2].Value) && int.TryParse(match.Groups[2].Value, out int parsed))
                {
                    steps = parsed;
                }

                commands.Add((dir, steps));
            }

            return commands;
        }

        private void ProcessQueuedMoves(GameState gameState)
        {
            foreach (var move in gameState.QueuedMoves)
            {
                if (gameState.AP <= 0)
                {
                    gameState.AddOutput($"Cannot move {move.direction.ToUpper()} {move.steps} time(s)... not enough energy.");
                    break;
                }
                gameState.MovePlayer(move.direction, move.steps);
            }
            gameState.QueuedMoves.Clear();
        }

        private void ExecuteCommand(string command, GameState gameState)
        {
            switch (command)
            {
                case "help1":
                    ShowHelp1(gameState);
                    break;
                case "help2":
                    ShowHelp2(gameState);
                    break;
                case "freemove":
                    gameState.IsFreeMoving = !gameState.IsFreeMoving;
                    gameState.AddOutput(gameState.IsFreeMoving ? 
                        "You are FREE MOVING! Use (W/A/S/D) to queue moves." : 
                        "Free move disabled.");
                    break;
                case "exit":
                    Environment.Exit(0);
                    break;
                case "inspect":
                    gameState.AddOutput("No inspect direction given please include (up/left/right/down)");
                    break;
                case "inspectup":
                    InspectDirection("up", gameState);
                    break;
                case "inspectleft":
                    InspectDirection("left", gameState);
                    break;
                case "inspectdown":
                    InspectDirection("down", gameState);
                    break;
                case "inspectright":
                    InspectDirection("right", gameState);
                    break;
                case "rest":
                    gameState.Rest();
                    break;
                case "clear":
                    gameState.OutputHistory.Clear();
                    gameState.AddOutput("Console cleared.");
                    break;
                case "debug":
                    gameState.AddOutput("[DEBUG] Debug console output.");
                    break;
                case "debugdamage":
                    gameState.TakeDamage(10);
                    break;
                case "debugshowterrain":
                    gameState.CurrentDisplayMode = DisplayMode.NoiseMap;
                    gameState.NoiseMapType = "terrain";
                    gameState.AddOutput("Showing TERRAIN HEIGHT noise map.");
                    break;
                case "debugshowlushness":
                    gameState.CurrentDisplayMode = DisplayMode.NoiseMap;
                    gameState.NoiseMapType = "lushness";
                    gameState.AddOutput("Showing LUSHNESS noise map.");
                    break;
                case "debugshowtemperature":
                    gameState.CurrentDisplayMode = DisplayMode.NoiseMap;
                    gameState.NoiseMapType = "temperature";
                    gameState.AddOutput("Showing TEMPERATURE noise map.");
                    break;
                case "debugshowhumidity":
                    gameState.CurrentDisplayMode = DisplayMode.NoiseMap;
                    gameState.NoiseMapType = "humidity";
                    gameState.AddOutput("Showing HUMIDITY noise map.");
                    break;
                case "debugshowresources":
                    gameState.CurrentDisplayMode = DisplayMode.NoiseMap;
                    gameState.NoiseMapType = "resources";
                    gameState.AddOutput("Showing RESOURCES noise map.");
                    break;
                case "debugshowdifficulty":
                    gameState.CurrentDisplayMode = DisplayMode.NoiseMap;
                    gameState.NoiseMapType = "difficulty";
                    gameState.AddOutput("Showing DIFFICULTY noise map.");
                    break;
            }
        }

        private void ShowHelp1(GameState gameState)
        {
            gameState.AddOutput("...");
            gameState.AddOutput("...");
            gameState.AddOutput("...");
            gameState.AddOutput("...");
            gameState.AddOutput("<> HELP OUTPUT <>");
            gameState.AddOutput("#1: Move by inputting (up/down/left/right) into the console.");
            gameState.AddOutput("#2: Move multiple spaces by adding a number after the move command (ex: down 7).");
            gameState.AddOutput("#3: Move freely by inputting (free) or (move) into the console.");
            gameState.AddOutput("#4: Inspect by inputting (inspect up/down/left/right) into the console.");
            gameState.AddOutput("...");
            gameState.AddOutput("...");
            gameState.AddOutput("...");
            gameState.AddOutput("<Page 1/2>");
            gameState.AddOutput("...");
            gameState.AddOutput("<> INPUT 'help2' FOR MORE HELP <>");
        }

        private void ShowHelp2(GameState gameState)
        {
            gameState.AddOutput("...");
            gameState.AddOutput("...");
            gameState.AddOutput("...");
            gameState.AddOutput("...");
            gameState.AddOutput("<> HELP OUTPUT <>");
            gameState.AddOutput("...");
            gameState.AddOutput("...");
            gameState.AddOutput("...");
            gameState.AddOutput("...");
            gameState.AddOutput("...");
            gameState.AddOutput("...");
            gameState.AddOutput("...");
            gameState.AddOutput("...");
            gameState.AddOutput("...");
            gameState.AddOutput("<Page 2/2>");
            gameState.AddOutput("...");
            gameState.AddOutput("<> INPUT 'help1' FOR BASIC HELP <>");
        }

        private void InspectDirection(string direction, GameState gameState)
        {
            var terrain = gameState.GetTerrainInDirection(direction);
            
            gameState.AddOutput("-----------------------------------");
            gameState.AddOutput($"Inspected {direction.ToUpper()}");
            gameState.AddOutput("-----------------------------------");
            gameState.AddOutput($"Terrain Type: {terrain.TerrainType}");
            gameState.AddOutput($"Vegetation  : {terrain.VegetationType}");
            gameState.AddOutput($"Difficulty  : {terrain.Difficulty:F2}");
            gameState.AddOutput($"Energy Cost : {terrain.EnergyCost}");
            gameState.AddOutput("-----------------------------------");
        }
    }
}

using System;
using System.Collections.Generic;

namespace ProjectVagabond
{
    public class MovementSystem
    {
        private readonly GameState gameState;

        public MovementSystem(GameState gameState)
        {
            this.gameState = gameState;
        }

        public void OptimizePendingMoves(List<(string direction, int steps)> pendingMoves, string newDirection, int newSteps)
        {
            if (pendingMoves.Count > 0)
            {
                var lastMove = pendingMoves[pendingMoves.Count - 1];
                string oppositeDirection = GetOppositeDirection(newDirection);
            
                if (lastMove.direction == oppositeDirection)
                {
                    int cancelAmount = Math.Min(lastMove.steps, newSteps);
                    int remainingLast = lastMove.steps - cancelAmount;
                    int remainingNew = newSteps - cancelAmount;
                
                    pendingMoves.RemoveAt(pendingMoves.Count - 1);
                
                    if (remainingLast > 0)
                    {
                        pendingMoves.Add((lastMove.direction, remainingLast));
                    }
                
                    if (remainingNew > 0)
                    {
                        pendingMoves.Add((newDirection, remainingNew));
                    }
                
                    if (cancelAmount > 0)
                    {
                        gameState.AddOutput($"Backtracked {cancelAmount} time(s) {newDirection.ToUpper()}.");
                    }
                
                    return;
                }
                else if (lastMove.direction == newDirection)
                {
                    pendingMoves[pendingMoves.Count - 1] = (lastMove.direction, lastMove.steps + newSteps);
                    return;
                }
            }
        
            pendingMoves.Add((newDirection, newSteps));
        }

        public List<(int x, int y)> GetPreviewPath(List<(string direction, int steps)> moves)
        {
            var path = new List<(int x, int y)>();
            int tempX = gameState.PlayerWorldX;
            int tempY = gameState.PlayerWorldY;
            HashSet<string> warnedDirections = new HashSet<string>();
            int remainingEnergy = gameState.AP;

            var validMoves = new List<(string direction, int steps)>();

            foreach (var (dir, steps) in moves)
            {
                int validSteps = 0;

                for (int i = 0; i < steps; i++)
                {
                    int nextX = tempX;
                    int nextY = tempY;

                    switch (dir)
                    {
                        case "up": nextY--; break;
                        case "down": nextY++; break;
                        case "left": nextX--; break;
                        case "right": nextX++; break;
                    }

                    if (IsPositionBlocked(nextX, nextY))
                    {
                        if (!warnedDirections.Contains(dir))
                        {
                            if (validSteps > 0)
                            {
                                gameState.AddOutput($"Cannot queue move {dir.ToUpper()} {validSteps+1} times... blocked by obstacle.");
                            }
                            else
                            {
                                gameState.AddOutput($"Cannot queue move {dir.ToUpper()}... blocked by obstacle.");
                            }
                            warnedDirections.Add(dir);
                        }
                        break;
                    }

                    var terrainData = gameState.GetTerrainDataAt(nextX, nextY);
                    int energyCost = terrainData.EnergyCost;
                    if (remainingEnergy < energyCost)
                    {
                        if (!warnedDirections.Contains($"{dir}_energy"))
                        {
                            string notEnoughEnergyPrompt = $"Cannot move {dir.ToUpper()} {steps} time(s)... not enough energy.";
                            if (validSteps > 0)
                            {
                                gameState.AddOutput(notEnoughEnergyPrompt);
                                gameState.AddOutput($"Queued {dir.ToUpper()} {validSteps} time(s).");
                            }
                            else
                            {
                                gameState.AddOutput(notEnoughEnergyPrompt);
                            }
                            warnedDirections.Add($"{dir}_energy");
                        }
                        break;
                    }

                    tempX = nextX;
                    tempY = nextY;
                    path.Add((tempX, tempY));
                    remainingEnergy -= energyCost;
                    validSteps++;
                }

                if (validSteps > 0)
                {
                    validMoves.Add((dir, validSteps));
                }
            }

            moves.Clear();
            moves.AddRange(validMoves);

            return path;
        }

        public int CalculatePreviewEnergyUsage(List<(int x, int y)> path)
        {
            int totalEnergyUsage = 0;
        
            foreach (var (x, y) in path)
            {
                var terrainData = gameState.GetTerrainDataAt(x, y);
                int energyCost = terrainData.EnergyCost;
                totalEnergyUsage += energyCost;
            }
        
            return totalEnergyUsage;
        }

        public void ExecuteMoves(List<(string direction, int steps)> pendingMoves)
        {
            var previewPath = GetPreviewPath(pendingMoves);
            int previewEnergyUsage = CalculatePreviewEnergyUsage(previewPath);
        
            if (previewEnergyUsage > gameState.AP)
            {
                gameState.AddOutput($"Not enough energy! Path requires {previewEnergyUsage} energy, but you only have {gameState.AP}.");
                pendingMoves.Clear();
                return;
            }
        
            int stepsMoved = ExecutePath(previewPath);

            if (gameState.AP <= 0) 
            { 
                gameState.AddOutput($"Out of energy!"); 
            }
            gameState.AddOutput($"Moved {stepsMoved} time(s).");
        
            pendingMoves.Clear();
        }

        private int ExecutePath(List<(int x, int y)> path)
        {
            int stepsMoved = 0;
            foreach (var (x, y) in path)
            {
                var terrainData = gameState.GetTerrainDataAt(x, y);
                int energyCost = terrainData.EnergyCost;
            
                if (gameState.AP < energyCost)
                {
                    gameState.AddOutput("Not enough energy for this terrain!");
                    break;
                }

                gameState.PlayerWorldX = x;
                gameState.PlayerWorldY = y;
                gameState.CameraX = gameState.PlayerWorldX - 7;
                gameState.CameraY = gameState.PlayerWorldY - 7;
            
                gameState.AP -= energyCost;
                if (gameState.AP < 0) gameState.AP = 0;
            
                stepsMoved++;
            }
            return stepsMoved;
        }

        private bool IsPositionBlocked(int x, int y)
        {
            char terrain = gameState.GetTerrainAt(x, y);
            return terrain == '^' || terrain == '~'; // Mountains or water are blocked
        }

        private static string GetOppositeDirection(string direction)
        {
            return direction switch
            {
                "up" => "down",
                "down" => "up",
                "left" => "right",
                "right" => "left",
                _ => ""
            };
        }

        public static bool IsDirection(string direction)
        {
            return direction switch
            {
                "up" or "down" or "left" or "right" => true,
                _ => false
            };
        }
    }
}

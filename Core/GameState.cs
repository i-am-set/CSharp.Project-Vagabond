using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace ProjectVagabond
{
    public class GameState
    {
        private Vector2 _playerWorldPos;
        private NoiseMapManager _noiseManager;
        private PlayerStats _playerStats;
        private List<Vector2> _pendingPathPreview = new List<Vector2>();
        private float _moveTimer = 0f;
        private bool _isExecutingPath = false;
        private int _currentPathIndex = 0;
        private bool _isFreeMoveMode = false;

        public Vector2 PlayerWorldPos => _playerWorldPos;
        public List<Vector2> PendingPathPreview => _pendingPathPreview;
        public bool IsExecutingPath => _isExecutingPath;
        public bool IsFreeMoveMode => _isFreeMoveMode;
        public int CurrentPathIndex => _currentPathIndex;
        public NoiseMapManager NoiseManager => _noiseManager;
        public PlayerStats PlayerStats => _playerStats;
        public int PendingPathEnergyCost => CalculatePathEnergyCost(_pendingPathPreview);

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public GameState()
        {
            _playerWorldPos = new Vector2(0, 0); // Start at world origin

            int masterSeed = RandomNumberGenerator.GetInt32(1, 99999) + Environment.TickCount;
            _noiseManager = new NoiseMapManager(masterSeed);
            _playerStats = new PlayerStats(5, 5, 5, 5, 5);
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public void ClearPendingPathPreview()
        {
            _pendingPathPreview.Clear();
        }

        public void ToggleExecutingPath(bool toggle)
        {
            ToggleIsFreeMoveMode(false);
            _isExecutingPath = toggle;
        }

        public void ToggleIsFreeMoveMode(bool toggle)
        {
            _isFreeMoveMode = toggle;

            if (toggle)
            {
                Core.CurrentTerminalRenderer.AddOutputToHistory("[gold]Free move enabled.");
            }
            else
            {
                Core.CurrentTerminalRenderer.AddOutputToHistory("[gold]Free move disabled.");
            }
        }

        public void SetCurrentPathIndex(int index)
        {
            _currentPathIndex = index;
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public bool IsPositionPassable(Vector2 position)
        {
            var mapData = GetMapDataAt((int)position.X, (int)position.Y);
            string terrainType = mapData.TerrainType;

            return terrainType != "WATER" && terrainType != "PEAKS";
        }

        public int GetMovementEnergyCost(Vector2 position)
        {
            var mapData = GetMapDataAt((int)position.X, (int)position.Y);
            string terrainType = mapData.TerrainType;

            return terrainType switch
            {
                "FLATLANDS" => 1,
                "HILLS" => 2,
                "MOUNTAINS" => 3,
                "WATER" => 1, // Impassable, but no cost if somehow reached
                "PEAKS" => 1, // Impassable, but no cost if somehow reached
                _ => 1 // Default to flatlands cost
            };
        }

        public int CalculatePathEnergyCost(List<Vector2> path)
        {
            int totalCost = 0;
            foreach (var position in path)
            {
                totalCost += GetMovementEnergyCost(position);
            }
            return totalCost;
        }

        public void QueueMovement(Vector2 direction, string[] args)
        {
            if (_isExecutingPath)
            {
                Core.CurrentTerminalRenderer.AddOutputToHistory("Cannot queue movements while executing a path.");
                return;
            }

            int count = 1;
            if (args.Length > 1 && int.TryParse(args[1], out int parsedCount))
            {
                count = Math.Max(1, Math.Min(Global.MAX_SINGLE_MOVE_LIMIT, parsedCount));
            }

            Vector2 oppositeDirection = -direction; // Check for backtracking from the end of the path
            int removedSteps = 0;

            while (_pendingPathPreview.Count > 0 && removedSteps < count) // Remove steps from the end that match the opposite direction
            {
                Vector2 lastStep = _pendingPathPreview.Last();
                Vector2 prevPos = _pendingPathPreview.Count > 1 ? _pendingPathPreview[_pendingPathPreview.Count - 2] : _playerWorldPos;
                Vector2 lastDirection = lastStep - prevPos;

                if (lastDirection == oppositeDirection)
                {
                    _pendingPathPreview.RemoveAt(_pendingPathPreview.Count - 1);
                    removedSteps++;
                }
                else
                {
                    break;
                }
            }

            int remainingSteps = count - removedSteps; // Add remaining forward steps
            if (remainingSteps > 0)
            {
                Vector2 currentPos = _pendingPathPreview.Count > 0 ? _pendingPathPreview.Last() : _playerWorldPos;
                int validSteps = 0;

                for (int i = 0; i < remainingSteps; i++)
                {
                    Vector2 nextPos = currentPos + direction;

                    if (!IsPositionPassable(nextPos))
                    {
                        var mapData = GetMapDataAt((int)nextPos.X, (int)nextPos.Y);
                        Core.CurrentTerminalRenderer.AddOutputToHistory($"[crimson]Cannot move here... terrain is impassable! <{mapData.TerrainType.ToLower()}>");
                        break;
                    }

                    // Create a temporary path including this new position to check energy cost
                    var tempPath = new List<Vector2>(_pendingPathPreview) { nextPos };
                    int totalEnergyCost = CalculatePathEnergyCost(tempPath);

                    if (totalEnergyCost > _playerStats.CurrentEnergyPoints)
                    {
                        var mapData = GetMapDataAt((int)nextPos.X, (int)nextPos.Y);
                        int stepCost = GetMovementEnergyCost(nextPos);
                        Core.CurrentTerminalRenderer.AddOutputToHistory($"[crimson]Cannot move here... Not enough energy! <Requires {stepCost} EP>");
                        break;
                    }

                    currentPos = nextPos;
                    _pendingPathPreview.Add(currentPos);
                    validSteps++;
                }

                if (validSteps > 0)
                {
                    int pathCost = CalculatePathEnergyCost(_pendingPathPreview);
                    if (removedSteps > 0)
                    {
                        Core.CurrentTerminalRenderer.AddOutputToHistory($"[teal]Backtracked {removedSteps} time(s), added {validSteps} move(s) (Path cost: {pathCost} energy)");
                    }
                    else
                    {
                        Core.CurrentTerminalRenderer.AddOutputToHistory($"Queued {validSteps} move(s) {args[0].ToLower()} (Path cost: {pathCost} energy)");
                    }
                }
                else if (removedSteps > 0)
                {
                    int pathCost = CalculatePathEnergyCost(_pendingPathPreview);
                    Core.CurrentTerminalRenderer.AddOutputToHistory($"[teal]Backtracked {removedSteps} time(s) (Path cost: {pathCost} energy)");
                }
            }
            else if (removedSteps > 0)
            {
                int pathCost = CalculatePathEnergyCost(_pendingPathPreview);
                Core.CurrentTerminalRenderer.AddOutputToHistory($"[teal]Backtracked {removedSteps} time(s) (Path cost: {pathCost} energy)");
            }
        }

        public void UpdateMovement(GameTime gameTime)
        {
            if (_isExecutingPath && _pendingPathPreview.Count > 0)
            {
                _moveTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;

                if (_moveTimer >= Global.MOVE_DELAY_SECONDS)
                {
                    if (_currentPathIndex < _pendingPathPreview.Count)
                    {
                        Vector2 nextPosition = _pendingPathPreview[_currentPathIndex];

                        if (!IsPositionPassable(nextPosition))
                        {
                            var mapData = GetMapDataAt((int)nextPosition.X, (int)nextPosition.Y);
                            Core.CurrentTerminalRenderer.AddOutputToHistory($"[crimson]Path blocked at ({(int)nextPosition.X}, {(int)nextPosition.Y}) - {mapData.TerrainType.ToLower()} is impassable!");
                            CancelPathExecution();
                            return;
                        }

                        // Check if player has enough energy for this step
                        int energyCost = GetMovementEnergyCost(nextPosition);
                        if (!_playerStats.CanExertEnergy(energyCost))
                        {
                            var mapData = GetMapDataAt((int)nextPosition.X, (int)nextPosition.Y);
                            Core.CurrentTerminalRenderer.AddOutputToHistory($"[crimson]Not enough energy to continue! Need {energyCost} for {mapData.TerrainType.ToLower()}, have {_playerStats.CurrentEnergyPoints}");
                            CancelPathExecution();
                            return;
                        }

                        // Execute the move
                        _playerWorldPos = nextPosition;
                        _playerStats.ExertEnergy(energyCost);

                        var currentMapData = GetMapDataAt((int)nextPosition.X, (int)nextPosition.Y);
                        Core.CurrentTerminalRenderer.AddOutputToHistory($"[darkgray]Moved to ({(int)nextPosition.X}, {(int)nextPosition.Y}) - {currentMapData.TerrainType.ToLower()} (-{energyCost} energy, {_playerStats.CurrentEnergyPoints} remaining)");

                        _currentPathIndex++;
                        _moveTimer = 0f;

                        if (_currentPathIndex >= _pendingPathPreview.Count) // Check if path was completed
                        {
                            _isExecutingPath = false;
                            _pendingPathPreview.Clear();
                            _currentPathIndex = 0;
                            ToggleExecutingPath(false);
                            Core.CurrentTerminalRenderer.AddOutputToHistory("Path execution completed.");
                        }
                    }
                }
            }
        }

        public void CancelPathExecution()
        {
            if (_isExecutingPath)
            {
                _isExecutingPath = false;
                _pendingPathPreview.Clear();
                _currentPathIndex = 0;
                ToggleExecutingPath(false);
                ToggleIsFreeMoveMode(false);
                Core.CurrentTerminalRenderer.AddOutputToHistory("[crimson]Path execution cancelled.");
            }
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public MapData GetMapDataAt(int x, int y)
        {
            return _noiseManager.GetMapData(x, y);
        }

        public float GetNoiseAt(int x, int y)
        {
            return _noiseManager.GetNoiseValue(NoiseMapType.TerrainHeight, x, y);
        }

        public string GetTerrainDescription(float noise)
        {
            if (noise < Global.Instance.WaterLevel) return "deep water";
            if (noise < Global.Instance.FlatlandsLevel) return "flatlands";
            if (noise < Global.Instance.HillsLevel) return "hills";
            if (noise < Global.Instance.MountainsLevel) return "mountains";
            return "peaks";
        }

        public string GetTerrainDescription(int x, int y)
        {
            var mapData = GetMapDataAt(x, y);
            return mapData.TerrainType.ToLower();
        }

        public Texture2D GetTerrainTexture(float noise)
        {
            if (noise < Global.Instance.WaterLevel) return Core.CurrentSpriteManager.WaterSprite;
            if (noise < Global.Instance.FlatlandsLevel) return Core.CurrentSpriteManager.FlatlandSprite;
            if (noise < Global.Instance.HillsLevel) return Core.CurrentSpriteManager.HillSprite;
            if (noise < Global.Instance.MountainsLevel) return Core.CurrentSpriteManager.MountainSprite;
            return Core.CurrentSpriteManager.PeakSprite;
        }

        public Texture2D GetTerrainTexture(int x, int y)
        {
            float noise = GetNoiseAt(x, y);
            return GetTerrainTexture(noise);
        }
    }
}
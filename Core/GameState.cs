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

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public GameState()
        {
            _playerWorldPos = new Vector2(0, 0); // Start at world origin
            
            int masterSeed = RandomNumberGenerator.GetInt32(1, 99999) + Environment.TickCount;
            _noiseManager = new NoiseMapManager(masterSeed);
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public void ClearPendingPathPreview()
        {
            _pendingPathPreview.Clear();
        }

        public void ToggleExecutingPath(bool toggle)
        {
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

                for (int i = 0; i < remainingSteps; i++)
                {
                    currentPos += direction;
                    _pendingPathPreview.Add(currentPos);
                }
            }

            if (removedSteps > 0)
            {
                Core.CurrentTerminalRenderer.AddOutputToHistory($"[teal]Backtracked {removedSteps} time(s), added {remainingSteps} move(s)");
            }
            else
            {
                Core.CurrentTerminalRenderer.AddOutputToHistory($"Queued {count} move(s) [dimgray]{args[0].ToUpper()}");
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
                        _playerWorldPos = _pendingPathPreview[_currentPathIndex];
                        _currentPathIndex++;
                        _moveTimer = 0f;

                        if (_currentPathIndex >= _pendingPathPreview.Count) // Check if we've completed the path
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
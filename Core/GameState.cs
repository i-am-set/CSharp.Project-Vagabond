using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Dice;
using ProjectVagabond.Particles;
using ProjectVagabond.Progression;
using ProjectVagabond.Scenes;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace ProjectVagabond
{
    public enum MapView
    {
        World
    }
    public class GameState
    {
        private readonly NoiseMapManager _noiseManager;
        private readonly ComponentStore _componentStore;
        private readonly ChunkManager _chunkManager;
        private readonly Global _global;
        private readonly SpriteManager _spriteManager;

        private ActionExecutionSystem _actionExecutionSystem;

        private bool _isExecutingActions = false;
        private bool _isPaused = false;
        private readonly Random _random = new Random();
        private static readonly Queue<IAction> _emptyActionQueue = new Queue<IAction>();

        public MapView PathExecutionMapView { get; private set; }

        public int PlayerEntityId { get; private set; }
        public PlayerState PlayerState { get; private set; }
        public Vector2 PlayerWorldPos
        {
            get
            {
                var posComp = _componentStore.GetComponent<PositionComponent>(PlayerEntityId);
                return posComp != null ? posComp.WorldPosition : Vector2.Zero;
            }
        }
        public Queue<IAction> PendingActions
        {
            get
            {
                var actionQueueComp = _componentStore.GetComponent<ActionQueueComponent>(PlayerEntityId);
                return actionQueueComp?.ActionQueue ?? _emptyActionQueue;
            }
        }
        public bool IsExecutingActions => _isExecutingActions;
        public bool IsPausedByConsole { get; set; } = false;
        public bool IsPaused => _isPaused || IsPausedByConsole;
        public NoiseMapManager NoiseManager => _noiseManager;
        public List<int> ActiveEntities { get; private set; } = new List<int>();
        public int InitialActionCount { get; private set; }
        public bool IsActionQueueDirty { get; set; } = true;

        public HashSet<Point> ExploredCells { get; private set; } = new HashSet<Point>();
        private const int FOG_OF_WAR_RADIUS = 40;

        public string LastRunKiller { get; set; } = "Unknown";

        public GameState(NoiseMapManager noiseManager, ComponentStore componentStore, ChunkManager chunkManager, Global global, SpriteManager spriteManager)
        {
            _noiseManager = noiseManager;
            _componentStore = componentStore;
            _chunkManager = chunkManager;
            _global = global;
            _spriteManager = spriteManager;

            EventBus.Subscribe<GameEvents.ActionQueueChanged>(e => IsActionQueueDirty = true);
            EventBus.Subscribe<GameEvents.PlayerMoved>(OnPlayerMoved);
        }

        private void OnPlayerMoved(GameEvents.PlayerMoved e)
        {
            UpdateExploration(e.NewPosition);
        }

        public void InitializeWorld()
        {
            // 1. Create PlayerState container
            PlayerState = new PlayerState();
            PlayerState.Party.Clear(); // Remove default dummy

            // 2. Create "Oakley" (The Main Character) using ID "0"
            var oakley = PartyMemberFactory.CreateMember("0");
            if (oakley == null)
            {
                throw new Exception("CRITICAL: Could not load 'Oakley' (ID: 0) from PartyMembers.json");
            }
            PlayerState.Party.Add(oakley);

            // 3. Add Oakley's starting gear to the shared inventory
            if (BattleDataCache.PartyMembers.TryGetValue("0", out var oakleyData))
            {
                foreach (var kvp in oakleyData.StartingWeapons)
                {
                    if (BattleDataCache.Weapons.ContainsKey(kvp.Key)) PlayerState.AddWeapon(kvp.Key, kvp.Value);
                }
                foreach (var kvp in oakleyData.StartingArmor)
                {
                    if (BattleDataCache.Armors.ContainsKey(kvp.Key)) PlayerState.AddArmor(kvp.Key, kvp.Value);
                }
                foreach (var kvp in oakleyData.StartingRelics)
                {
                    if (BattleDataCache.Relics.ContainsKey(kvp.Key)) PlayerState.AddRelic(kvp.Key, kvp.Value);
                }
            }

            // 4. Spawn the Entity in the world
            PlayerEntityId = Spawner.Spawn("player", worldPosition: new Vector2(0, 0));

            // 5. Sync Entity Components with Oakley's Stats
            var liveStats = new CombatantStatsComponent
            {
                Level = oakley.Level,
                MaxHP = oakley.MaxHP,
                CurrentHP = oakley.MaxHP,
                MaxMana = oakley.MaxMana,
                CurrentMana = oakley.MaxMana,
                Strength = oakley.Strength,
                Intelligence = oakley.Intelligence,
                Tenacity = oakley.Tenacity,
                Agility = oakley.Agility,
                DefensiveElementIDs = new List<int>(oakley.DefensiveElementIDs),
                // FIX: Filter out nulls from the Spells array before selecting MoveID
                AvailableMoveIDs = oakley.Spells
                    .Where(m => m != null)
                    .Select(m => m!.MoveID)
                    .Concat(oakley.Actions.Select(m => m.MoveID))
                    .ToList()
            };
            _componentStore.AddComponent(PlayerEntityId, liveStats);

            var posComp = _componentStore.GetComponent<PositionComponent>(PlayerEntityId);
            if (posComp != null)
            {
                _componentStore.AddComponent(PlayerEntityId, new RenderPositionComponent { WorldPosition = posComp.WorldPosition });
            }

            UpdateExploration(PlayerWorldPos);
        }

        public void Reset()
        {
            PlayerEntityId = 0;
            PlayerState = null;
            ActiveEntities.Clear();
            ExploredCells.Clear();
            IsActionQueueDirty = true;
            _isExecutingActions = false;
            _isPaused = false;
            IsPausedByConsole = false;
            LastRunKiller = "Unknown";
        }

        public void UpdateExploration(Vector2 centerPosition)
        {
            int radius = FOG_OF_WAR_RADIUS;
            float radiusSquared = radius * radius;
            int centerX = (int)centerPosition.X;
            int centerY = (int)centerPosition.Y;

            for (int y = centerY - radius; y <= centerY + radius; y++)
            {
                for (int x = centerX - radius; x <= centerX + radius; x++)
                {
                    var cellPoint = new Point(x, y);
                    var cellCenter = new Vector2(x, y);
                    if (Vector2.DistanceSquared(centerPosition, cellCenter) <= radiusSquared)
                    {
                        ExploredCells.Add(cellPoint);
                    }
                }
            }
        }

        public void InitializeRenderableEntities()
        {
            var allEntitiesWithRenderable = _componentStore.GetAllEntitiesWithComponent<RenderableComponent>().ToList();
            foreach (var entityId in allEntitiesWithRenderable)
            {
                var renderable = _componentStore.GetComponent<RenderableComponent>(entityId);

                if (renderable.Texture != null)
                {
                    continue;
                }

                if (entityId == PlayerEntityId)
                {
                    renderable.Texture = _spriteManager.PlayerSprite;
                }
                else
                {
                    renderable.Texture = ServiceLocator.Get<Texture2D>();
                }
            }
        }

        public void UpdateActiveEntities()
        {
            var playerPosComp = _componentStore.GetComponent<PositionComponent>(PlayerEntityId);
            if (playerPosComp == null)
            {
                ActiveEntities.Clear();
                return;
            }

            var playerChunkCoords = ChunkManager.WorldToChunkCoords(playerPosComp.WorldPosition);
            var tetheredEntities = _chunkManager.GetEntitiesInTetherRange(playerChunkCoords);
            var highImportanceEntities = _componentStore.GetAllEntitiesWithComponent<HighImportanceComponent>();
            ActiveEntities = tetheredEntities.Union(highImportanceEntities).ToList();
        }

        public void TogglePause()
        {
            if (_isExecutingActions)
            {
                _isPaused = !_isPaused;
            }
        }

        public void ToggleExecutingActions(bool toggle)
        {
            _actionExecutionSystem ??= ServiceLocator.Get<ActionExecutionSystem>();
            if (toggle)
            {
                InitialActionCount = PendingActions.Count;
                PathExecutionMapView = MapView.World;
                _actionExecutionSystem.StartExecution();
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"Executing queue of[undo] {PendingActions.Count}[gray] action(s)..." });
            }
            else
            {
                InitialActionCount = 0;
                _actionExecutionSystem.StopExecution();
            }
            _isExecutingActions = toggle;
            IsActionQueueDirty = true;
        }

        public bool IsPositionPassable(Vector2 position, MapView view, int pathfindingEntityId, Vector2 targetDestination, out MapData mapData)
        {
            mapData = default;

            mapData = GetMapDataAt((int)position.X, (int)position.Y);
            if (!(mapData.TerrainHeight >= _global.WaterLevel && mapData.TerrainHeight < _global.MountainsLevel))
                return false;

            if (position == targetDestination)
            {
                return true;
            }

            if (IsTileOccupied(position, pathfindingEntityId))
            {
                return false;
            }

            return true;
        }

        public bool IsPositionPassable(Vector2 position, MapView view, out MapData mapData)
        {
            return IsPositionPassable(position, view, -1, new Vector2(-1, -1), out mapData);
        }

        public bool IsPositionPassable(Vector2 position, MapView view)
        {
            return IsPositionPassable(position, view, out _);
        }

        public bool IsTileOccupied(Vector2 position, int askingEntityId)
        {
            var entitiesToCheck = ActiveEntities;
            foreach (var entityId in entitiesToCheck)
            {
                if (entityId == askingEntityId) continue;

                var worldPosComp = _componentStore.GetComponent<PositionComponent>(entityId);
                if (worldPosComp != null && worldPosComp.WorldPosition == position)
                {
                    if (_componentStore.HasComponent<PlayerTagComponent>(entityId))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public int GetMovementTickCost(MovementMode mode, MapData mapData)
        {
            int ticks = mode switch
            {
                MovementMode.Walk => 2,
                MovementMode.Jog => 1,
                MovementMode.Run => 1,
                _ => 2
            };

            ticks += mapData.TerrainHeight switch
            {
                var height when height < _global.FlatlandsLevel => 0,
                var height when height < _global.HillsLevel => 1,
                var height when height < _global.MountainsLevel => 2,
                _ => 99
            };

            return ticks;
        }

        public void ExecuteActions()
        {
            ToggleExecutingActions(false);
        }

        public void ClearPendingActions()
        {
            PendingActions.Clear();
        }

        public bool CancelExecutingActions(bool interrupted = false)
        {
            if (_isExecutingActions)
            {
                _actionExecutionSystem ??= ServiceLocator.Get<ActionExecutionSystem>();
                _actionExecutionSystem.HandleInterruption();
                ToggleExecutingActions(false);
                _isPaused = false;
                PendingActions.Clear();

                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = interrupted ? "[cancel]Action queue interrupted." : "[cancel]Action queue cancelled." });
                return true;
            }
            return false;
        }

        public void ApplyNarrativeOutcome(NarrativeOutcome outcome)
        {
            if (outcome == null) return;

            switch (outcome.OutcomeType)
            {
                case "GiveItem":
                    PlayerState.AddConsumable(outcome.Value);
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[palette_teal]Obtained {outcome.Value}!" });
                    break;
                case "AddBuff":
                    if (Enum.TryParse<StatusEffectType>(outcome.Value, true, out var effectType))
                    {
                        var buffsComp = _componentStore.GetComponent<TemporaryBuffsComponent>(PlayerEntityId);
                        if (buffsComp == null)
                        {
                            buffsComp = new TemporaryBuffsComponent();
                            _componentStore.AddComponent(PlayerEntityId, buffsComp);
                        }
                        var existingBuff = buffsComp.Buffs.FirstOrDefault(b => b.EffectType == effectType);
                        if (existingBuff != null)
                        {
                            existingBuff.RemainingBattles += outcome.Duration;
                        }
                        else
                        {
                            buffsComp.Buffs.Add(new TemporaryBuff { EffectType = effectType, RemainingBattles = outcome.Duration });
                        }
                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[palette_teal]Gained a temporary buff: {outcome.Value}!" });
                    }
                    break;
                case "AddStat":
                    var statsComp = _componentStore.GetComponent<CombatantStatsComponent>(PlayerEntityId);
                    if (statsComp == null) return;

                    var parts = outcome.Value.Split(',');
                    if (parts.Length == 2 && int.TryParse(parts[1], out int amount))
                    {
                        string statName = parts[0].Trim().ToLowerInvariant();
                        string feedback = "";
                        switch (statName)
                        {
                            case "strength": statsComp.Strength += amount; feedback = $"Strength increased by {amount}!"; break;
                            case "intelligence": statsComp.Intelligence += amount; feedback = $"Intelligence increased by {amount}!"; break;
                            case "tenacity": statsComp.Tenacity += amount; feedback = $"Tenacity increased by {amount}!"; break;
                            case "agility": statsComp.Agility += amount; feedback = $"Agility increased by {amount}!"; break;
                            case "maxhp": statsComp.MaxHP += amount; statsComp.CurrentHP += amount; feedback = $"Max HP increased by {amount}!"; break;
                        }
                        if (!string.IsNullOrEmpty(feedback))
                        {
                            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[palette_teal]{feedback}" });
                        }
                    }
                    break;
            }
        }

        public MapData GetMapDataAt(int x, int y) => _noiseManager.GetMapData(x, y);
        public float GetNoiseAt(int x, int y) => _noiseManager.GetNoiseValue(NoiseMapType.TerrainHeight, x, y);

        public string GetTerrainDescription(float noise)
        {
            if (noise < _global.WaterLevel) return "deep water";
            if (noise < _global.FlatlandsLevel) return "flatlands";
            if (noise < _global.HillsLevel) return "hills";
            if (noise < _global.MountainsLevel) return "mountains";
            return "peaks";
        }

        public string GetTerrainDescription(MapData data) => GetTerrainDescription(data.TerrainHeight);
        public string GetTerrainDescription(int x, int y) => GetTerrainDescription(GetMapDataAt(x, y).TerrainHeight);

        public Texture2D GetTerrainTexture(float noise)
        {
            if (noise < _global.WaterLevel) return _spriteManager.WaterSprite;
            if (noise < _global.FlatlandsLevel) return _spriteManager.FlatlandSprite;
            if (noise < _global.HillsLevel) return _spriteManager.HillSprite;
            if (noise < _global.MountainsLevel) return _spriteManager.MountainSprite;
            return _spriteManager.PeakSprite;
        }

        public Texture2D GetTerrainTexture(int x, int y) => GetTerrainTexture(GetNoiseAt(x, y));

        public List<int> GetEntitiesAtGridPos(Vector2 gridPos)
        {
            var entitiesOnTile = new List<int>();
            var entitiesToCheck = ActiveEntities;
            foreach (var entityId in entitiesToCheck)
            {
                var worldPosComp = _componentStore.GetComponent<PositionComponent>(entityId);
                if (worldPosComp != null && worldPosComp.WorldPosition == gridPos)
                {
                    entitiesOnTile.Add(entityId);
                }
            }
            return entitiesOnTile;
        }
    }
}
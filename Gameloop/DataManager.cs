using ProjectVagabond.Battle;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjectVagabond
{
    /// <summary>
    /// Manages the loading and retrieval of static game data, replacing the ECS ArchetypeManager.
    /// </summary>
    public class DataManager
    {
        private readonly Dictionary<string, EnemyData> _enemies = new Dictionary<string, EnemyData>(StringComparer.OrdinalIgnoreCase);

        public DataManager() { }

        public void LoadData(string contentRoot)
        {
            LoadEnemies(Path.Combine(contentRoot, "Data", "Archetypes.json"));
        }

        private void LoadEnemies(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Debug.WriteLine($"[DataManager] [ERROR] Enemy data file not found at {filePath}");
                return;
            }

            try
            {
                string json = File.ReadAllText(filePath);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    Converters = { new JsonStringEnumConverter() }
                };

                var enemyList = JsonSerializer.Deserialize<List<EnemyData>>(json, options);

                if (enemyList != null)
                {
                    foreach (var enemy in enemyList)
                    {
                        if (!string.IsNullOrEmpty(enemy.Id))
                        {
                            _enemies[enemy.Id] = enemy;
                        }
                    }
                    Debug.WriteLine($"[DataManager] Loaded {_enemies.Count} enemy definitions.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DataManager] [ERROR] Failed to load enemy data: {ex.Message}");
            }
        }

        public EnemyData GetEnemyData(string id)
        {
            if (_enemies.TryGetValue(id, out var data))
            {
                return data;
            }
            Debug.WriteLine($"[DataManager] [WARNING] Enemy ID '{id}' not found.");
            return null;
        }
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ProjectVagabond.Combat
{
    /// <summary>
    /// Manages loading, storing, and providing access to all combat actions (spells, items, etc.)
    /// from data files. This class is registered with the ServiceLocator for global access.
    /// </summary>
    public class ActionManager
    {
        private readonly Dictionary<string, ActionData> _actions = new Dictionary<string, ActionData>(StringComparer.OrdinalIgnoreCase);

        public ActionManager() { }

        /// <summary>
        /// Creates a default set of actions for testing if none are loaded from files.
        /// </summary>
        private List<ActionData> CreateDefaultActions()
        {
            return new List<ActionData>
            {
                new ActionData
                {
                    Id = "spell_fireball",
                    Name = "Fireball",
                    Priority = 0,
                    Combinations = new List<SynergyData>
                    {
                        new SynergyData { PairedWith = "spell_wind_gust", CombinesToBecome = "spell_firestorm" }
                    }
                },
                new ActionData
                {
                    Id = "spell_ice_shard",
                    Name = "Ice Shard",
                    Priority = 0
                },
                new ActionData
                {
                    Id = "spell_wind_gust",
                    Name = "Wind Gust",
                    Priority = 1
                },
                new ActionData
                {
                    Id = "spell_firestorm",
                    Name = "Firestorm",
                    Priority = 0
                },
                new ActionData
                {
                    Id = "action_pass",
                    Name = "Pass",
                    Priority = -10
                }
            };
        }

        /// <summary>
        /// Loads all .json files from a specified directory, deserializes them into
        /// ActionData objects, and stores them for later use. If no files are found,
        /// it populates the manager with a default set of actions.
        /// </summary>
        /// <param name="directoryPath">The path to the directory containing action JSON files.</param>
        public void LoadActions(string directoryPath)
        {
            if (Directory.Exists(directoryPath))
            {
                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                };

                string[] actionFiles = Directory.GetFiles(directoryPath, "*.json", SearchOption.AllDirectories);

                foreach (var file in actionFiles)
                {
                    try
                    {
                        string jsonContent = File.ReadAllText(file);
                        var actionData = JsonSerializer.Deserialize<ActionData>(jsonContent, jsonOptions);

                        if (actionData != null && !string.IsNullOrEmpty(actionData.Id))
                        {
                            if (!_actions.TryAdd(actionData.Id, actionData))
                            {
                                Console.WriteLine($"[WARNING] Duplicate action ID '{actionData.Id}' found in '{file}'. Overwriting previous entry.");
                                _actions[actionData.Id] = actionData;
                            }
                        }
                        else
                        {
                            Console.WriteLine($"[WARNING] Could not load action from {file}. Invalid format or missing ID.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] Failed to load or parse action file {file}: {ex.Message}");
                    }
                }
            }

            // If after loading, no actions exist, create default ones for testing.
            if (_actions.Count == 0)
            {
                Console.WriteLine("[INFO] No action files found or loaded. Creating default actions for testing.");
                var defaultActions = CreateDefaultActions();
                foreach (var action in defaultActions)
                {
                    _actions[action.Id] = action;
                }
            }
        }

        /// <summary>
        /// Retrieves a loaded action by its unique ID.
        /// </summary>
        /// <param name="id">The ID of the action to retrieve.</param>
        /// <returns>The ActionData object, or null if not found.</returns>
        public ActionData GetAction(string id)
        {
            _actions.TryGetValue(id, out var action);
            return action;
        }

        /// <summary>
        /// Retrieves a collection of all loaded actions.
        /// </summary>
        /// <returns>An enumerable collection of all ActionData objects.</returns>
        public IEnumerable<ActionData> GetAllActions()
        {
            return _actions.Values.ToList();
        }
    }
}
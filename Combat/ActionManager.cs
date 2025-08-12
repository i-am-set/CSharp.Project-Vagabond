using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using ProjectVagabond.Combat;
using ProjectVagabond.Combat.UI;
using ProjectVagabond.Scenes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

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
        /// Loads all .json files from a specified directory, deserializes them into
        //  ActionData objects, and stores them for later use.
        /// </summary>
        /// <param name="directoryPath">The path to the directory containing action JSON files.</param>
        public void LoadActions(string directoryPath)
        {
            Debug.WriteLine($"[ActionManager] --- Loading Actions from: {Path.GetFullPath(directoryPath)} ---");

            if (!Directory.Exists(directoryPath))
            {
                Debug.WriteLine($"[ActionManager] [ERROR] Action directory not found. No actions will be loaded.");
                return;
            }

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            };

            string[] actionFiles = Directory.GetFiles(directoryPath, "*.json", SearchOption.AllDirectories);
            Debug.WriteLine($"[ActionManager] Found {actionFiles.Length} JSON files to process.");

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
                            Debug.WriteLine($"[ActionManager] [WARNING] Duplicate action ID '{actionData.Id}' found in '{file}'. Overwriting.");
                            _actions[actionData.Id] = actionData;
                        }
                        else
                        {
                            Debug.WriteLine($"[ActionManager] Successfully loaded Action: '{actionData.Id}'");
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"[ActionManager] [WARNING] Could not load action from {file}. Invalid format or missing ID.");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ActionManager] [ERROR] Failed to load or parse action file {file}: {ex.Message}");
                }
            }

            Debug.WriteLine($"[ActionManager] --- Finished loading. Total actions loaded: {_actions.Count} ---");

            // --- FAILSAFE ---
            // Ensure the essential generic attack action exists to prevent crashes.
            if (!_actions.ContainsKey("action_attack"))
            {
                Debug.WriteLine("[ActionManager] [CRITICAL FAILURE] 'action_attack.json' not found or failed to load. Creating failsafe version.");
                _actions["action_attack"] = new ActionData
                {
                    Id = "action_attack",
                    Name = "Attack",
                    TargetType = TargetType.SingleEnemy,
                    Priority = 0
                };
            }
        }

        /// <summary>
        /// Retrieves a loaded action by its unique ID. It will also check the CombatManager's
        /// temporary action cache for dynamically generated actions.
        /// </summary>
        /// <param name="id">The ID of the action to retrieve.</param>
        /// <returns>The ActionData object, or null if not found.</returns>
        public ActionData GetAction(string id)
        {
            // First, try to get a permanent, loaded action.
            if (_actions.TryGetValue(id, out var action))
            {
                return action;
            }

            // If not found, check the combat manager for a temporary action for the current turn.
            var combatManager = ServiceLocator.Get<CombatManager>();
            return combatManager?.GetTemporaryAction(id);
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
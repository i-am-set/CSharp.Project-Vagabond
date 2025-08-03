using ProjectVagabond.Encounters;
using ProjectVagabond.Scenes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ProjectVagabond
{
    /// <summary>
    /// Manages loading, storing, and triggering game encounters from data files.
    /// </summary>
    public class EncounterManager
    {
        private readonly Dictionary<string, EncounterData> _encounters = new Dictionary<string, EncounterData>(StringComparer.OrdinalIgnoreCase);
        private SceneManager _sceneManager; // Lazy loaded

        public EncounterManager() { }

        /// <summary>
        /// Loads all .json files from a specified directory and its subdirectories,
        /// deserializes them into EncounterData objects, and stores them for later use.
        /// </summary>
        /// <param name="directoryPath">The path to the root directory containing encounter JSON files.</param>
        public void LoadEncounters(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                Console.WriteLine($"[ERROR] Encounter directory not found: {directoryPath}");
                return;
            }

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            };

            string[] encounterFiles = Directory.GetFiles(directoryPath, "*.json", SearchOption.AllDirectories);

            foreach (var file in encounterFiles)
            {
                try
                {
                    string jsonContent = File.ReadAllText(file);
                    var encounterData = JsonSerializer.Deserialize<EncounterData>(jsonContent, jsonOptions);

                    if (encounterData != null && !string.IsNullOrEmpty(encounterData.Id))
                    {
                        if (!_encounters.TryAdd(encounterData.Id, encounterData))
                        {
                            Console.WriteLine($"[WARNING] Duplicate encounter ID '{encounterData.Id}' found in '{file}'. Overwriting previous entry.");
                            _encounters[encounterData.Id] = encounterData;
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[WARNING] Could not load encounter from {file}. Invalid format or missing ID.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Failed to load or parse encounter file {file}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Retrieves a list of all loaded encounters that are flagged as random encounters.
        /// </summary>
        /// <returns>An enumerable collection of random EncounterData objects.</returns>
        public IEnumerable<EncounterData> GetRandomEncounters()
        {
            return _encounters.Values.Where(e => e.IsRandom);
        }

        /// <summary>
        /// Finds an encounter by its ID, interrupts any active player path, and displays it as a modal dialog.
        /// </summary>
        /// <param name="encounterId">The unique ID of the encounter to trigger.</param>
        public void TriggerEncounter(string encounterId)
        {
            _sceneManager ??= ServiceLocator.Get<SceneManager>();
            var gameState = ServiceLocator.Get<GameState>();

            if (gameState.IsExecutingActions)
            {
                gameState.CancelExecutingActions(interrupted: true);
            }

            if (_encounters.TryGetValue(encounterId, out var encounterData))
            {
                if (_sceneManager.CurrentActiveScene is GameMapScene terminalMapScene)
                {
                    terminalMapScene.ShowEncounter(encounterData);
                }
                else
                {
                    Console.WriteLine($"[WARNING] EncounterManager: Tried to trigger encounter '{encounterId}' but the active scene is not TerminalMapScene.");
                }
            }
            else
            {
                Console.WriteLine($"[ERROR] EncounterManager: Encounter with ID '{encounterId}' not found.");
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[error]Encounter '{encounterId}' not found." });
            }
        }

        /// <summary>
        /// Processes a list of outcomes by executing their corresponding registered actions.
        /// </summary>
        /// <param name="outcomes">The list of outcomes to process.</param>
        public void ProcessOutcomes(List<EncounterOutcomeData> outcomes)
        {
            if (outcomes == null) return;

            foreach (var outcome in outcomes)
            {
                EncounterActionRegistry.ExecuteAction(outcome.Type, outcome.Value);
            }
        }
    }
}
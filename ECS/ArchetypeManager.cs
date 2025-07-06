using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ProjectVagabond
{
    /// <summary>
    /// A singleton manager responsible for loading, storing, and providing
    /// access to all entity archetypes from JSON files.
    /// </summary>
    public class ArchetypeManager
    {
        public static readonly ArchetypeManager Instance = new ArchetypeManager();
        private readonly Dictionary<string, Archetype> _archetypes = new Dictionary<string, Archetype>();

        private ArchetypeManager() { }

        /// <summary>
        /// Loads all .json files from a specified directory, deserializes them
        /// into Archetype objects, and stores them for later use.
        /// </summary>
        /// <param name="directoryPath">The path to the directory containing archetype JSON files.</param>
        public void LoadArchetypes(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                Console.WriteLine($"[ERROR] Archetype directory not found: {directoryPath}");
                return;
            }

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            };

            string[] archetypeFiles = Directory.GetFiles(directoryPath, "*.json");

            foreach (var file in archetypeFiles)
            {
                try
                {
                    string jsonContent = File.ReadAllText(file);
                    var archetype = JsonSerializer.Deserialize<Archetype>(jsonContent, jsonOptions);

                    if (archetype != null && !string.IsNullOrEmpty(archetype.Id))
                    {
                        _archetypes[archetype.Id] = archetype;
                        Console.WriteLine($"[INFO] Loaded archetype: {archetype.Id}");
                    }
                    else
                    {
                        Console.WriteLine($"[WARNING] Could not load archetype from {file}. Invalid format or missing ID.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Failed to load or parse archetype file {file}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Retrieves a loaded archetype by its unique ID.
        /// </summary>
        /// <param name="id">The ID of the archetype to retrieve.</param>
        /// <returns>The Archetype object, or null if not found.</returns>
        public Archetype GetArchetype(string id)
        {
            _archetypes.TryGetValue(id, out var archetype);
            return archetype;
        }
    }
}
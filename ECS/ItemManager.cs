using ProjectVagabond;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjectVagabond
{
    /// <summary>
    /// Manages the loading and retrieval of all item data, such as weapons, from JSON files.
    /// </summary>
    public class ItemManager
    {
        private readonly Dictionary<string, Weapon> _weapons = new Dictionary<string, Weapon>(StringComparer.OrdinalIgnoreCase);

        public ItemManager() { }

        /// <summary>
        /// Creates a default set of weapons for testing if none are loaded from files.
        /// </summary>
        private List<Weapon> CreateDefaultWeapons()
        {
            return new List<Weapon>
            {
                new Weapon
                {
                    Id = "weapon_unarmed_punch",
                    Name = "Punch",
                    GrantedActionIds = new List<string> { "action_punch" }
                },
                new Weapon
                {
                    Id = "weapon_unarmed_claw",
                    Name = "Claw",
                    GrantedActionIds = new List<string> { "action_claw" }
                }
            };
        }

        /// <summary>
        /// Loads all weapon JSON files from a specified directory and stores them.
        /// </summary>
        /// <param name="directoryPath">The path to the directory containing weapon JSON files.</param>
        public void LoadWeapons(string directoryPath)
        {
            if (Directory.Exists(directoryPath))
            {
                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                };

                string[] weaponFiles = Directory.GetFiles(directoryPath, "*.json");

                foreach (var file in weaponFiles)
                {
                    try
                    {
                        string jsonContent = File.ReadAllText(file);
                        var weapon = JsonSerializer.Deserialize<Weapon>(jsonContent, jsonOptions);

                        if (weapon != null && !string.IsNullOrEmpty(weapon.Id))
                        {
                            _weapons[weapon.Id] = weapon;
                        }
                        else
                        {
                            Console.WriteLine($"[WARNING] Could not load weapon from {file}. Invalid format or missing ID.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] Failed to load or parse weapon file {file}: {ex.Message}");
                    }
                }
            }

            // If after loading, no weapons exist, create default ones for testing.
            if (_weapons.Count == 0)
            {
                Console.WriteLine("[INFO] No weapon files found or loaded. Creating default weapons for testing.");
                var defaultWeapons = CreateDefaultWeapons();
                foreach (var weapon in defaultWeapons)
                {
                    _weapons[weapon.Id] = weapon;
                }
            }
        }

        /// <summary>
        /// Retrieves a loaded weapon by its unique ID.
        /// </summary>
        /// <param name="id">The ID of the weapon to retrieve.</param>
        /// <returns>The Weapon object, or null if not found.</returns>
        public Weapon GetWeapon(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }
            _weapons.TryGetValue(id, out var weapon);
            return weapon;
        }
    }
}

using ProjectVagabond.Combat;
using ProjectVagabond.Combat.Effects;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
        /// Loads all weapon JSON files from a specified directory and stores them.
        /// </summary>
        /// <param name="directoryPath">The path to the directory containing weapon JSON files.</param>
        public void LoadWeapons(string directoryPath)
        {
            Debug.WriteLine($"[ItemManager] --- Loading Weapons from: {Path.GetFullPath(directoryPath)} ---");

            if (!Directory.Exists(directoryPath))
            {
                Debug.WriteLine($"[ItemManager] [ERROR] Weapon directory not found. No weapons will be loaded.");
            }
            else
            {
                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new JsonStringEnumConverter() }
                };

                string[] weaponFiles = Directory.GetFiles(directoryPath, "*.json", SearchOption.AllDirectories);
                Debug.WriteLine($"[ItemManager] Found {weaponFiles.Length} JSON files to process.");

                foreach (var file in weaponFiles)
                {
                    try
                    {
                        string jsonContent = File.ReadAllText(file);
                        var weapon = JsonSerializer.Deserialize<Weapon>(jsonContent, jsonOptions);

                        if (weapon != null && !string.IsNullOrEmpty(weapon.Id))
                        {
                            _weapons[weapon.Id] = weapon;
                            Debug.WriteLine($"[ItemManager] Successfully loaded Weapon: '{weapon.Id}'");
                        }
                        else
                        {
                            Debug.WriteLine($"[ItemManager] [WARNING] Could not load weapon from {file}. Invalid format or missing ID.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[ItemManager] [ERROR] Failed to load or parse weapon file {file}: {ex.Message}");
                    }
                }
            }

            Debug.WriteLine($"[ItemManager] --- Finished loading. Total weapons loaded: {_weapons.Count} ---");

            // --- FAILSAFE ---
            // Ensure essential unarmed weapons exist to prevent crashes.
            if (!_weapons.ContainsKey("weapon_unarmed_punch"))
            {
                Debug.WriteLine("[ItemManager] [CRITICAL FAILURE] 'weapon_unarmed_punch.json' not found or failed to load. Creating failsafe version.");
                _weapons["weapon_unarmed_punch"] = new Weapon
                {
                    Id = "weapon_unarmed_punch",
                    Name = "Unarmed Strike",
                    PrimaryAttack = new ActionData
                    {
                        Name = "Punch",
                        TargetType = TargetType.SingleEnemy,
                        Effects = new List<EffectDefinition> { new EffectDefinition { Type = "DealDamage", Amount = "3", DamageType = DamageType.Blunt } }
                    }
                };
            }
            if (!_weapons.ContainsKey("weapon_unarmed_claw"))
            {
                Debug.WriteLine("[ItemManager] [CRITICAL FAILURE] 'weapon_unarmed_claw.json' not found or failed to load. Creating failsafe version.");
                _weapons["weapon_unarmed_claw"] = new Weapon
                {
                    Id = "weapon_unarmed_claw",
                    Name = "Unarmed Strike",
                    PrimaryAttack = new ActionData
                    {
                        Name = "Claw",
                        TargetType = TargetType.SingleEnemy,
                        Effects = new List<EffectDefinition> { new EffectDefinition { Type = "DealDamage", Amount = "3", DamageType = DamageType.Slashing } }
                    }
                };
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
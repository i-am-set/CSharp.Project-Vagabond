using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace ProjectVagabond
{
    /// <summary>
    /// A manager responsible for loading, storing, and providing
    /// access to all entity archetypes from JSON files. This class is
    /// registered with the ServiceLocator.
    /// </summary>
    public class ArchetypeManager
    {
        private readonly Dictionary<string, ArchetypeTemplate> _archetypes = new Dictionary<string, ArchetypeTemplate>();

        public ArchetypeManager() { }

        /// <summary>
        /// Creates a default, failsafe player archetype if the JSON file fails to load.
        /// </summary>
        private void CreateFailsafePlayerArchetype()
        {
            var playerComponents = new List<IComponent>
            {
                new ArchetypeIdComponent { ArchetypeId = "player" },
                new PositionComponent(),
                new StatsComponent { Strength = 5, Agility = 5, Tenacity = 5, Intelligence = 5, Charm = 5, SecondsPerEnergyPoint = 5.0f },
                new ActionQueueComponent(),
                new PlayerTagComponent(),
                new HighImportanceComponent(),
                new RenderableComponent { Color = ServiceLocator.Get<Global>().PlayerColor },
                new HealthComponent { MaxHealth = 100, CurrentHealth = 100 },
                new EquipmentComponent(),
                new ActiveStatusEffectComponent(),
                new EnergyRegenComponent(),
            };
            _archetypes["player"] = new ArchetypeTemplate("player", "Player", playerComponents);
        }

        /// <summary>
        /// Loads all .json files from a specified directory, "bakes" them
        /// into ArchetypeTemplate objects, and stores them for later use.
        /// This process uses reflection and should only be done at load time.
        /// </summary>
        /// <param name="directoryPath">The path to the directory containing archetype JSON files.</param>
        public void LoadArchetypes(string directoryPath)
        {
            if (Directory.Exists(directoryPath))
            {
                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                };

                string[] archetypeFiles = Directory.GetFiles(directoryPath, "*.json", SearchOption.AllDirectories);

                foreach (var file in archetypeFiles)
                {
                    try
                    {
                        string jsonContent = File.ReadAllText(file);
                        var archetypeDto = JsonSerializer.Deserialize<Archetype>(jsonContent, jsonOptions);

                        if (archetypeDto != null && !string.IsNullOrEmpty(archetypeDto.Id))
                        {
                            var templateComponents = new List<IComponent>();

                            // Add the ArchetypeIdComponent to the template so it gets cloned with the rest.
                            templateComponents.Add(new ArchetypeIdComponent { ArchetypeId = archetypeDto.Id });

                            // Process components from the JSON file using reflection.
                            foreach (var componentDef in archetypeDto.Components)
                            {
                                string typeName = componentDef["Type"].ToString();
                                Type componentType = Type.GetType(typeName, throwOnError: true);
                                object componentInstance = Activator.CreateInstance(componentType);

                                if (componentDef.TryGetValue("Properties", out object props) && props is JsonElement propertiesElement)
                                {
                                    PopulateComponentProperties(componentInstance, propertiesElement);
                                }

                                if (componentInstance is IInitializableComponent initializable)
                                {
                                    initializable.Initialize();
                                }

                                if (componentInstance is ICloneableComponent cloneableComponent)
                                {
                                    templateComponents.Add(cloneableComponent);
                                }
                                else
                                {
                                    Console.WriteLine($"[WARNING] Component '{componentType.Name}' in archetype '{archetypeDto.Id}' does not implement ICloneableComponent and will not be added to the template.");
                                }
                            }

                            // Create and store the final baked template.
                            var template = new ArchetypeTemplate(archetypeDto.Id, archetypeDto.Name, templateComponents);
                            _archetypes[template.Id] = template;
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

            // --- FAILSAFE ---
            // The player archetype is essential for the game to run. If it's not loaded, create a default one.
            if (!_archetypes.ContainsKey("player"))
            {
                Debug.WriteLine("[ArchetypeManager] [CRITICAL FAILURE] 'player.json' not found or failed to load. Creating a failsafe player archetype. Please ensure the file exists and its 'Copy to Output Directory' property is set to 'Copy if newer'.");
                CreateFailsafePlayerArchetype();
            }
        }

        /// <summary>
        /// Retrieves a loaded and baked archetype template by its unique ID.
        /// </summary>
        /// <param name="id">The ID of the archetype template to retrieve.</param>
        /// <returns>The ArchetypeTemplate object, or null if not found.</returns>
        public ArchetypeTemplate GetArchetypeTemplate(string id)
        {
            _archetypes.TryGetValue(id, out var archetype);
            return archetype;
        }

        /// <summary>
        /// Uses reflection to set properties on a component instance from JSON data.
        /// </summary>
        private static void PopulateComponentProperties(object component, JsonElement properties)
        {
            foreach (JsonProperty property in properties.EnumerateObject())
            {
                PropertyInfo propInfo = component.GetType().GetProperty(property.Name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

                if (propInfo != null && propInfo.CanWrite)
                {
                    try
                    {
                        object value = ConvertJsonElement(property.Value, propInfo.PropertyType);
                        propInfo.SetValue(component, value);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[ArchetypeManager] [CRITICAL FAILURE] Could not set property '{property.Name}' on component '{component.GetType().Name}'. Reason: {ex.Message}. Check the JSON definition.");
                    }
                }
                else
                {
                    Debug.WriteLine($"[ArchetypeManager] [WARNING] Property '{property.Name}' not found or cannot be written to on component '{component.GetType().Name}'.");
                }
            }
        }

        /// <summary>
        /// Converts a JsonElement to the target C# type.
        /// </summary>
        private static object ConvertJsonElement(JsonElement element, Type targetType)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    string stringValue = element.GetString();
                    if (targetType.IsEnum)
                    {
                        return Enum.Parse(targetType, stringValue, true);
                    }
                    // Handle special string-to-color conversion for RenderableComponent
                    if (targetType == typeof(Color))
                    {
                        var globalInstance = ServiceLocator.Get<Global>();
                        var colorProp = typeof(Global).GetProperty(stringValue, BindingFlags.Public | BindingFlags.Instance);
                        if (colorProp != null && colorProp.PropertyType == typeof(Color))
                        {
                            return colorProp.GetValue(globalInstance);
                        }
                        // Fallback to standard MonoGame colors
                        var mgColorProp = typeof(Color).GetProperty(stringValue, BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase);
                        if (mgColorProp != null)
                        {
                            return mgColorProp.GetValue(null);
                        }
                    }
                    return stringValue;

                case JsonValueKind.Number:
                    if (targetType == typeof(int)) return element.GetInt32();
                    if (targetType == typeof(float)) return element.GetSingle();
                    if (targetType == typeof(double)) return element.GetDouble();
                    if (targetType == typeof(decimal)) return element.GetDecimal();
                    break;

                case JsonValueKind.True:
                    return true;

                case JsonValueKind.False:
                    return false;

                case JsonValueKind.Object:
                case JsonValueKind.Array:
                    // Use the built-in deserializer to handle complex objects and arrays.
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    return JsonSerializer.Deserialize(element.GetRawText(), targetType, options);
            }

            return Convert.ChangeType(element.ToString(), targetType);
        }
    }
}
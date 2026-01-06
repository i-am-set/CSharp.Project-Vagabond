using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Dice;
using ProjectVagabond.Particles;
using ProjectVagabond.Scenes;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace ProjectVagabond
{
    /// <summary>
    /// A manager responsible for loading, storing, and providing
    /// access to all entity archetypes from a single JSON file.
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
                new PlayerTagComponent(),
                new HighImportanceComponent(),
                new RenderableComponent { Color = ServiceLocator.Get<Global>().PlayerColor },
                new TemporaryBuffsComponent()
            };
            _archetypes["player"] = new ArchetypeTemplate("player", "Player", playerComponents);
        }

        /// <summary>
        /// Loads the master Archetypes.json file, "bakes" the definitions
        /// into ArchetypeTemplate objects, and stores them for later use.
        /// </summary>
        /// <param name="filePath">The path to the Archetypes.json file.</param>
        public void LoadArchetypes(string filePath)
        {
            // Robust path handling:
            // 1. If it's a directory, look for Archetypes.json inside.
            // 2. If it's a file path, use it directly.
            // 3. If it doesn't exist, try appending .json if missing.

            string finalPath = filePath;

            if (Directory.Exists(filePath))
            {
                finalPath = Path.Combine(filePath, "Archetypes.json");
            }
            else if (!File.Exists(filePath) && !filePath.EndsWith(".json"))
            {
                finalPath = filePath + ".json";
            }

            if (File.Exists(finalPath))
            {
                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                };

                try
                {
                    string jsonContent = File.ReadAllText(finalPath);
                    var archetypeList = JsonSerializer.Deserialize<List<Archetype>>(jsonContent, jsonOptions);

                    if (archetypeList != null)
                    {
                        foreach (var archetypeDto in archetypeList)
                        {
                            if (!string.IsNullOrEmpty(archetypeDto.Id))
                            {
                                var templateComponents = new List<IComponent>();

                                // Add the ArchetypeIdComponent to the template so it gets cloned with the rest.
                                templateComponents.Add(new ArchetypeIdComponent { ArchetypeId = archetypeDto.Id });

                                // Process components from the JSON file using reflection.
                                foreach (var componentDef in archetypeDto.Components)
                                {
                                    string typeName = componentDef["Type"].ToString();

                                    // Use throwOnError: false to handle cases where a component class was deleted (like ActionQueueComponent)
                                    Type componentType = Type.GetType(typeName, throwOnError: false);

                                    if (componentType == null)
                                    {
                                        Debug.WriteLine($"[ArchetypeManager] Warning: Component type '{typeName}' not found. Skipping.");
                                        continue;
                                    }

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
                                    else if (componentInstance is IComponent)
                                    {
                                        bool hasProperties = componentDef.ContainsKey("Properties");
                                        if (!hasProperties)
                                        {
                                            templateComponents.Add((IComponent)componentInstance);
                                        }
                                        else
                                        {
                                            Console.WriteLine($"[WARNING] Stateful component '{componentType.Name}' in archetype '{archetypeDto.Id}' does not implement ICloneableComponent and will not be added to spawned entities correctly.");
                                        }
                                    }
                                }

                                // Create and store the final baked template.
                                var template = new ArchetypeTemplate(archetypeDto.Id, archetypeDto.Name, templateComponents);
                                _archetypes[template.Id] = template;
                            }
                        }
                        Debug.WriteLine($"[ArchetypeManager] Successfully loaded {_archetypes.Count} archetypes from {finalPath}.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Failed to load or parse archetypes file {finalPath}: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine($"[WARNING] Archetypes file not found at {finalPath} (Original input: {filePath}).");
            }

            // --- FAILSAFE ---
            if (!_archetypes.ContainsKey("player"))
            {
                Debug.WriteLine("[ArchetypeManager] [CRITICAL FAILURE] 'player' archetype not found. Creating failsafe.");
                CreateFailsafePlayerArchetype();
            }
        }

        public ArchetypeTemplate GetArchetypeTemplate(string id)
        {
            _archetypes.TryGetValue(id, out var archetype);
            return archetype;
        }

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
                        Debug.WriteLine($"[ArchetypeManager] [CRITICAL FAILURE] Could not set property '{property.Name}' on component '{component.GetType().Name}'. Reason: {ex.Message}.");
                    }
                }
            }
        }

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
                    if (targetType == typeof(Color))
                    {
                        var globalInstance = ServiceLocator.Get<Global>();
                        var colorProp = typeof(Global).GetProperty(stringValue, BindingFlags.Public | BindingFlags.Instance);
                        if (colorProp != null && colorProp.PropertyType == typeof(Color))
                        {
                            return colorProp.GetValue(globalInstance);
                        }
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

                case JsonValueKind.True: return true;
                case JsonValueKind.False: return false;

                case JsonValueKind.Object:
                case JsonValueKind.Array:
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    return JsonSerializer.Deserialize(element.GetRawText(), targetType, options);
            }

            return Convert.ChangeType(element.ToString(), targetType);
        }
    }
}
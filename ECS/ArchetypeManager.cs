using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

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
        private static readonly Dictionary<string, Type> _componentNameMap = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

        public ArchetypeManager()
        {
            // This static constructor will only run once to build the component type map.
            if (_componentNameMap.Count == 0)
            {
                BuildComponentTypeMap();
            }
        }

        private static void BuildComponentTypeMap()
        {
            Debug.WriteLine("[ArchetypeManager] Building component type map...");
            var componentType = typeof(IComponent);
            var types = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(s => s.GetTypes())
                .Where(p => componentType.IsAssignableFrom(p) && !p.IsInterface);

            foreach (var type in types)
            {
                if (!_componentNameMap.TryAdd(type.Name, type))
                {
                    Debug.WriteLine($"[ArchetypeManager] [WARNING] Duplicate component name found: {type.Name}. Only the first one was registered.");
                }
            }
            Debug.WriteLine($"[ArchetypeManager] Found and mapped {_componentNameMap.Count} component types.");
        }


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
                new CombatantComponent { DefaultWeaponId = "weapon_unarmed_punch", InnateActionIds = new List<string> { "spell_fireball", "spell_ice_shard", "spell_wind_gust", "spell_heal" } },
                new EquipmentComponent(),
                new ActiveStatusEffectComponent(),
                new EnergyRegenComponent(),
                new CombatDeckComponent()
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
                    Converters = { new JsonStringEnumConverter() }
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

                            templateComponents.Add(new ArchetypeIdComponent { ArchetypeId = archetypeDto.Id });

                            foreach (var componentDef in archetypeDto.Components)
                            {
                                string typeName = componentDef["Type"].ToString();
                                if (!_componentNameMap.TryGetValue(typeName, out Type componentType))
                                {
                                    throw new TypeLoadException($"Component type '{typeName}' in archetype '{archetypeDto.Id}' could not be found. Make sure it exists and implements IComponent.");
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
                                else
                                {
                                    Console.WriteLine($"[WARNING] Component '{componentType.Name}' in archetype '{archetypeDto.Id}' does not implement ICloneableComponent and will not be added to the template.");
                                }
                            }

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

            if (!_archetypes.ContainsKey("player"))
            {
                Debug.WriteLine("[ArchetypeManager] [CRITICAL FAILURE] 'player.json' not found or failed to load. Creating a failsafe player archetype. Please ensure the file exists and its 'Copy to Output Directory' property is set to 'Copy if newer'.");
                CreateFailsafePlayerArchetype();
            }
        }

        public ArchetypeTemplate GetArchetypeTemplate(string id)
        {
            _archetypes.TryGetValue(id, out var archetype);
            return archetype;
        }

        public IEnumerable<ArchetypeTemplate> GetAllArchetypeTemplates()
        {
            return _archetypes.Values;
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
                        Debug.WriteLine($"[ArchetypeManager] [CRITICAL FAILURE] Could not set property '{property.Name}' on component '{component.GetType().Name}'. Reason: {ex.Message}. Check the JSON definition.");
                    }
                }
                else
                {
                    Debug.WriteLine($"[ArchetypeManager] [WARNING] Property '{property.Name}' not found or cannot be written to on component '{component.GetType().Name}'.");
                }
            }
        }

        private static object ConvertJsonElement(JsonElement element, Type targetType)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            };

            if (targetType == typeof(Color) && element.ValueKind == JsonValueKind.String)
            {
                string stringValue = element.GetString();
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

            return JsonSerializer.Deserialize(element.GetRawText(), targetType, options);
        }
    }
}
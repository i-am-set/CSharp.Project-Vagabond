using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;

namespace ProjectVagabond
{
    /// <summary>
    /// A static helper class responsible for creating entities from archetypes.
    /// </summary>
    public static class Spawner
    {
        /// <summary>
        /// Spawns a new entity based on a specified archetype at a given position.
        /// </summary>
        /// <param name="archetypeId">The ID of the archetype to spawn (e.g., "player", "wanderer_npc").</param>
        /// <param name="worldPosition">The world position where the entity should be spawned.</param>
        /// <param name="localPosition">The local position where the entity should be spawned within its world chunk.</param>
        /// <returns>The entity ID of the newly spawned entity, or -1 if spawning fails.</returns>
        public static int Spawn(string archetypeId, Vector2 worldPosition, Vector2 localPosition)
        {
            var archetype = ArchetypeManager.Instance.GetArchetype(archetypeId);
            if (archetype == null)
            {
                Console.WriteLine($"[ERROR] Failed to spawn entity. Archetype '{archetypeId}' not found.");
                return -1;
            }

            int entityId = Core.EntityManager.CreateEntity();

            foreach (var componentDef in archetype.Components)
            {
                try
                {
                    string typeName = componentDef["Type"].ToString();
                    Type componentType = Type.GetType(typeName);

                    if (componentType == null)
                    {
                        Console.WriteLine($"[ERROR] Could not find component type '{typeName}' for archetype '{archetypeId}'.");
                        continue;
                    }

                    // Create an instance of the component using its parameterless constructor
                    object componentInstance = Activator.CreateInstance(componentType);

                    // Set properties from the JSON definition
                    if (componentDef.TryGetValue("Properties", out object props) && props is JsonElement propertiesElement)
                    {
                        PopulateComponentProperties(componentInstance, propertiesElement);
                    }

                    // If the component needs post-property-setting logic, run it now.
                    if (componentInstance is IInitializableComponent initializable)
                    {
                        initializable.Initialize();
                    }

                    // Add the fully populated component to the store
                    // We use reflection to call the generic AddComponent method.
                    MethodInfo addComponentMethod = typeof(ComponentStore).GetMethod("AddComponent").MakeGenericMethod(componentType);
                    addComponentMethod.Invoke(Core.ComponentStore, new object[] { entityId, componentInstance });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Failed to create or add component for archetype '{archetypeId}': {ex.Message}");
                }
            }

            // After all components are added, set the specific spawn positions
            var posComp = Core.ComponentStore.GetComponent<PositionComponent>(entityId);
            if (posComp != null)
            {
                posComp.WorldPosition = worldPosition;
            }

            var localPosComp = Core.ComponentStore.GetComponent<LocalPositionComponent>(entityId);
            if (localPosComp != null)
            {
                localPosComp.LocalPosition = localPosition;
            }

            // Register the new entity with the spatial partitioning system
            Core.ChunkManager.RegisterEntity(entityId, worldPosition);

            return entityId;
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
                        Console.WriteLine($"[WARNING] Could not set property '{property.Name}' on component '{component.GetType().Name}': {ex.Message}");
                    }
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
                        var colorProp = typeof(Global).GetProperty(stringValue, BindingFlags.Public | BindingFlags.Instance);
                        if (colorProp != null && colorProp.PropertyType == typeof(Color))
                        {
                            return colorProp.GetValue(Global.Instance);
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

                case JsonValueKind.Array:
                    // Use the built-in deserializer to handle arrays of complex objects.
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    return JsonSerializer.Deserialize(element.GetRawText(), targetType, options);
            }

            return Convert.ChangeType(element.ToString(), targetType);
        }
    }
}
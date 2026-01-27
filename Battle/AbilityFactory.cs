using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Progression;
using ProjectVagabond.Scenes;
using ProjectVagabond.Transitions;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace ProjectVagabond.Battle.Abilities
{
    public static class AbilityFactory
    {
        // Cache: Maps "Thorns" -> typeof(ThornsAbility)
        private static readonly Dictionary<string, Type> _abilityTypeCache;

        static AbilityFactory()
        {
            _abilityTypeCache = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
            RegisterAbilityTypes();
        }

        /// <summary>
        /// Scans the assembly for classes implementing IAbility and caches them by name (stripping "Ability" suffix).
        /// </summary>
        private static void RegisterAbilityTypes()
        {
            var abilityType = typeof(IAbility);
            var types = Assembly.GetExecutingAssembly().GetTypes()
                .Where(t => abilityType.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

            foreach (var type in types)
            {
                // Map "ElementalDamageBonusAbility" -> "ElementalDamageBonus"
                string key = type.Name;
                if (key.EndsWith("Ability"))
                {
                    key = key.Substring(0, key.Length - "Ability".Length);
                }

                if (!_abilityTypeCache.ContainsKey(key))
                {
                    _abilityTypeCache[key] = type;
                }
            }

            Debug.WriteLine($"[AbilityFactory] Cached {_abilityTypeCache.Count} ability types via Reflection.");
        }

        public static List<IAbility> CreateAbilitiesFromData(Dictionary<string, string> effects, Dictionary<string, int> statModifiers)
        {
            var abilities = new List<IAbility>();

            // 1. Handle Stat Modifiers
            // Removed FlatStatBonusAbility logic as relics are gone. 
            // If Party Members need stat mods later, we can re-implement a specific ability for it.

            if (effects == null) return abilities;

            // 2. Handle Named Effects via Reflection
            foreach (var kvp in effects)
            {
                try
                {
                    IAbility ability = CreateAbility(kvp.Key, kvp.Value);
                    if (ability != null) abilities.Add(ability);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AbilityFactory] Error creating ability '{kvp.Key}' with params '{kvp.Value}': {ex.Message}");
                }
            }

            return abilities;
        }

        private static IAbility CreateAbility(string key, string valueString)
        {
            // 1. Find the type
            if (!_abilityTypeCache.TryGetValue(key, out Type type))
            {
                Debug.WriteLine($"[AbilityFactory] Warning: No ability class found for key '{key}'. Expected class '{key}Ability'.");
                return null;
            }

            // 2. Get the constructor (Assume the first public one is the target)
            var constructors = type.GetConstructors();
            if (constructors.Length == 0) return null;
            var ctor = constructors[0];
            var parameters = ctor.GetParameters();

            // 3. Parse arguments
            object[] args;

            // Special Case: Constructor takes a single List/IEnumerable (e.g., StatusImmunityAbility)
            // and the input string is a comma-separated list of those items.
            if (parameters.Length == 1 && IsCollectionType(parameters[0].ParameterType))
            {
                args = new object[] { ParseCollection(valueString, parameters[0].ParameterType) };
            }
            // Special Case: Constructor takes 0 arguments (Flag abilities like PainFuel)
            else if (parameters.Length == 0)
            {
                args = Array.Empty<object>();
            }
            // Standard Case: Comma-separated values map 1-to-1 to parameters
            else
            {
                string[] parts = valueString.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                // Validate arg count (allow optional params if we have fewer parts)
                if (parts.Length > parameters.Length)
                {
                    Debug.WriteLine($"[AbilityFactory] Warning: Too many arguments for '{key}'. Expected {parameters.Length}, got {parts.Length}. Truncating.");
                }

                args = new object[parameters.Length];

                for (int i = 0; i < parameters.Length; i++)
                {
                    var paramType = parameters[i].ParameterType;

                    if (i < parts.Length)
                    {
                        // Parse the string part to the target type
                        args[i] = ParseValue(parts[i].Trim(), paramType);
                    }
                    else if (parameters[i].HasDefaultValue)
                    {
                        // Use default value if provided in C#
                        args[i] = parameters[i].DefaultValue;
                    }
                    else
                    {
                        throw new ArgumentException($"Missing required argument for parameter '{parameters[i].Name}' in '{key}'.");
                    }
                }
            }

            // 4. Instantiate
            return (IAbility)Activator.CreateInstance(type, args);
        }

        private static object ParseValue(string input, Type targetType)
        {
            if (targetType == typeof(string)) return input;
            if (targetType == typeof(int)) return int.Parse(input, CultureInfo.InvariantCulture);
            if (targetType == typeof(float)) return float.Parse(input, CultureInfo.InvariantCulture);
            if (targetType == typeof(double)) return double.Parse(input, CultureInfo.InvariantCulture);
            if (targetType == typeof(bool)) return bool.Parse(input);
            if (targetType.IsEnum) return Enum.Parse(targetType, input, true);

            throw new InvalidOperationException($"[AbilityFactory] Unsupported parameter type: {targetType.Name}");
        }

        private static bool IsCollectionType(Type type)
        {
            return type != typeof(string) && (type.IsArray || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>)) || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)));
        }

        private static object ParseCollection(string input, Type collectionType)
        {
            // Determine the element type (e.g., StatusEffectType inside IEnumerable<StatusEffectType>)
            Type elementType = collectionType.IsArray
                ? collectionType.GetElementType()
                : collectionType.GetGenericArguments()[0];

            string[] parts = input.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            // Create a generic list to hold the parsed items
            var listType = typeof(List<>).MakeGenericType(elementType);
            var list = (IList)Activator.CreateInstance(listType);

            foreach (var part in parts)
            {
                list.Add(ParseValue(part.Trim(), elementType));
            }

            // If the constructor wants an Array, convert it
            if (collectionType.IsArray)
            {
                var array = Array.CreateInstance(elementType, list.Count);
                list.CopyTo(array, 0);
                return array;
            }

            return list;
        }
    }
}

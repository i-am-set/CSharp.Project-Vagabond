using Microsoft.Xna.Framework;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
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
        private static readonly Dictionary<string, Type> _abilityTypeCache;

        static AbilityFactory()
        {
            _abilityTypeCache = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
            RegisterAbilityTypes();
        }

        private static void RegisterAbilityTypes()
        {
            var abilityType = typeof(IAbility);
            var types = Assembly.GetExecutingAssembly().GetTypes()
                .Where(t => abilityType.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

            foreach (var type in types)
            {
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

        public static List<IAbility> GetAbilitiesForStatus(StatusEffectInstance instance)
        {
            var list = new List<IAbility>();

            switch (instance.EffectType)
            {
                case StatusEffectType.Poison:
                    list.Add(new PoisonLogicAbility(instance));
                    break;
                case StatusEffectType.Regen:
                    list.Add(new RegenLogicAbility(instance));
                    break;
                case StatusEffectType.Stun:
                    list.Add(new StunLogicAbility(instance));
                    break;
                case StatusEffectType.Burn:
                    list.Add(new BurnLogicAbility(instance));
                    break;
                case StatusEffectType.Provoked:
                    list.Add(new ProvokeLogicAbility(instance));
                    break;
                case StatusEffectType.Silence:
                    list.Add(new SilenceLogicAbility(instance));
                    break;
                case StatusEffectType.Frostbite:
                    list.Add(new FrostbiteLogicAbility(instance));
                    break;
                case StatusEffectType.Dodging:
                    list.Add(new DodgingLogicAbility(instance));
                    break;
                case StatusEffectType.Empowered:
                    list.Add(new EmpoweredLogicAbility(instance));
                    break;
                case StatusEffectType.Protected:
                    list.Add(new ProtectedLogicAbility(instance));
                    break;
                default:
                    break;
            }

            return list;
        }

        public static List<IAbility> CreateAbilitiesFromData(MoveData? moveData, Dictionary<string, string> effects, Dictionary<string, int> statModifiers)
        {
            var abilities = new List<IAbility>();

            if (effects == null) return abilities;

            foreach (var kvp in effects)
            {
                try
                {
                    IAbility ability = CreateAbility(kvp.Key, kvp.Value);
                    if (ability != null)
                    {
                        abilities.Add(ability);

                        if (moveData != null)
                        {
                            if (ability is RecoilAbility || ability is DamageRecoilAbility)
                            {
                                moveData.Tags.Add("Effect.Recoil");
                            }
                            if (ability is ManaDumpAbility)
                            {
                                moveData.Tags.Add("Effect.ManaDump");
                            }
                            if (ability is ManaBurnOnHitAbility || ability is ManaDamageAbility || ability is RestoreManaAbility)
                            {
                                moveData.Tags.Add("Effect.ManaMod");
                            }
                            if (ability is PercentageDamageAbility)
                            {
                                moveData.Tags.Add("Effect.FixedDamage");
                            }
                        }
                    }
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
            if (!_abilityTypeCache.TryGetValue(key, out Type type))
            {
                Debug.WriteLine($"[AbilityFactory] Warning: No ability class found for key '{key}'. Expected class '{key}Ability'.");
                return null;
            }

            var constructors = type.GetConstructors();
            if (constructors.Length == 0) return null;
            var ctor = constructors[0];
            var parameters = ctor.GetParameters();

            object[] args;

            if (parameters.Length == 1 && IsCollectionType(parameters[0].ParameterType))
            {
                args = new object[] { ParseCollection(valueString, parameters[0].ParameterType) };
            }
            else if (parameters.Length == 0)
            {
                args = Array.Empty<object>();
            }
            else
            {
                string[] parts = valueString.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

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
                        args[i] = ParseValue(parts[i].Trim(), paramType);
                    }
                    else if (parameters[i].HasDefaultValue)
                    {
                        args[i] = parameters[i].DefaultValue;
                    }
                    else
                    {
                        throw new ArgumentException($"Missing required argument for parameter '{parameters[i].Name}' in '{key}'.");
                    }
                }
            }

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
            Type elementType = collectionType.IsArray
                ? collectionType.GetElementType()
                : collectionType.GetGenericArguments()[0];

            string[] parts = input.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            var listType = typeof(List<>).MakeGenericType(elementType);
            var list = (IList)Activator.CreateInstance(listType);

            foreach (var part in parts)
            {
                list.Add(ParseValue(part.Trim(), elementType));
            }

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
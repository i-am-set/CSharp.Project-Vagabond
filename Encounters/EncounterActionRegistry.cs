using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ProjectVagabond.Encounters
{
    /// <summary>
    /// A static registry that discovers and stores mappings from string identifiers to C# methods.
    /// This allows encounter JSON files to trigger game logic in a decoupled, data-driven way.
    /// </summary>
    public static class EncounterActionRegistry
    {
        private static readonly Dictionary<string, Action<string>> _actions = new Dictionary<string, Action<string>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Uses reflection to scan the current assembly for static methods marked with the [EncounterAction] attribute
        /// and registers them in the action dictionary. This should be called once at startup.
        /// </summary>
        public static void RegisterActions()
        {
            var methods = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .SelectMany(type => type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                .Where(method => method.GetCustomAttribute<EncounterActionAttribute>() != null);

            foreach (var method in methods)
            {
                var attribute = method.GetCustomAttribute<EncounterActionAttribute>();
                var parameters = method.GetParameters();

                // Validate the method signature: must be a static void method with a single string parameter.
                if (method.ReturnType == typeof(void) && parameters.Length == 1 && parameters[0].ParameterType == typeof(string))
                {
                    var action = (Action<string>)Delegate.CreateDelegate(typeof(Action<string>), method);
                    if (!_actions.TryAdd(attribute.ActionName, action))
                    {
                        Console.WriteLine($"[WARNING] EncounterActionRegistry: Duplicate action name '{attribute.ActionName}' found. Overwriting.");
                        _actions[attribute.ActionName] = action;
                    }
                }
                else
                {
                    Console.WriteLine($"[WARNING] EncounterActionRegistry: Method '{method.DeclaringType.Name}.{method.Name}' has [EncounterAction] attribute but an invalid signature. Expected 'static void Method(string)'.");
                }
            }
        }

        /// <summary>
        /// Executes a registered action by its name.
        /// </summary>
        /// <param name="actionName">The name of the action to execute (from JSON).</param>
        /// <param name="value">The value parameter to pass to the action (from JSON).</param>
        public static void ExecuteAction(string actionName, string value)
        {
            if (_actions.TryGetValue(actionName, out var action))
            {
                action?.Invoke(value);
            }
            else
            {
                Console.WriteLine($"[ERROR] EncounterActionRegistry: Action '{actionName}' not found.");
            }
        }
    }
}
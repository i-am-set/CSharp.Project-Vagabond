using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Scenes;
using ProjectVagabond.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ProjectVagabond
{
    /// <summary>
    /// A simple static Service Locator pattern to provide global access to major systems and managers
    /// without requiring a complex dependency injection framework. This allows for loose coupling
    /// by centralizing service resolution.
    /// </summary>
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

        /// <summary>
        /// Registers a service instance with the locator. If a service of the same type
        /// already exists, it will be overwritten and a detailed warning will be logged
        /// to the Debug output.
        /// </summary>
        /// <typeparam name="T">The type of the service to register.</typeparam>
        /// <param name="service">The instance of the service.</param>
        public static void Register<T>(T service)
        {
            var type = typeof(T);
            if (_services.ContainsKey(type))
            {
                // This is a more flexible approach. It allows overwriting but logs a detailed
                // warning to the debug console, which is visible during development.
                var stackTrace = new StackTrace();
                var callingFrame = stackTrace.GetFrame(1); // Get the frame that called this method
                var callingMethod = callingFrame?.GetMethod();
                var callingType = callingMethod?.DeclaringType;

                string warningMessage =
                    $"[ServiceLocator WARNING] Service of type '{type.Name}' is being overwritten. " +
                    $"The new registration was called from '{callingType?.Name}.{callingMethod?.Name}'. " +
                    "This may be intentional, but can lead to unexpected behavior if not.";

                Debug.WriteLine(warningMessage);
            }
            _services[type] = service;
        }

        /// <summary>
        /// Retrieves a registered service instance.
        /// </summary>
        /// <typeparam name="T">The type of the service to retrieve.</typeparam>
        /// <returns>The registered instance of the service.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the service has not been registered.</exception>
        public static T Get<T>()
        {
            var type = typeof(T);
            if (!_services.TryGetValue(type, out var service))
            {
                // Enhanced exception with stack trace information
                var stackTrace = new StackTrace();
                var callingFrame = stackTrace.GetFrame(1); // Get the frame that called this method
                var callingMethod = callingFrame?.GetMethod();
                var callingType = callingMethod?.DeclaringType;
                throw new InvalidOperationException($"Service of type '{type.Name}' has not been registered. " +
                                                    $"This was requested by '{callingType?.Name}.{callingMethod?.Name}'. " +
                                                    $"Please ensure the service is registered in Core.cs before it is requested.");
            }
            return (T)service;
        }

        /// <summary>
        /// Unregisters a service of a specific type. Useful for scene-scoped services.
        /// </summary>
        /// <typeparam name="T">The type of the service to unregister.</typeparam>
        public static void Unregister<T>()
        {
            var type = typeof(T);
            _services.Remove(type);
        }


        /// <summary>
        /// Clears all registered services. Useful for teardown or resetting state.
        /// </summary>
        public static void Clear()
        {
            _services.Clear();
        }
    }
}

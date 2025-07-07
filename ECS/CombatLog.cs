using System;

namespace ProjectVagabond
{
    /// <summary>
    /// A centralized, static class for logging combat-related messages.
    /// </summary>
    public static class CombatLog
    {
        /// <summary>
        /// Fired whenever a new message is added to the combat log.
        /// </summary>
        public static event Action<string> OnMessageLogged;

        /// <summary>
        /// Logs a new message and notifies any listeners.
        /// </summary>
        /// <param name="message">The combat message to log.</param>
        public static void Log(string message)
        {
            OnMessageLogged?.Invoke(message);
        }
    }
}
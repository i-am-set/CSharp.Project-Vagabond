using System;

namespace ProjectVagabond.Encounters
{
    /// <summary>
    /// A custom attribute to mark static methods as invokable encounter actions.
    /// This allows the EncounterActionRegistry to discover and register them at runtime.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class EncounterActionAttribute : Attribute
    {
        public string ActionName { get; }

        /// <summary>
        /// Marks a method as an encounter action.
        /// </summary>
        /// <param name="actionName">The unique string identifier used in encounter JSON files to call this method.</param>
        public EncounterActionAttribute(string actionName)
        {
            ActionName = actionName;
        }
    }
}
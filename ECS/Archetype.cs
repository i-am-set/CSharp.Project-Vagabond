using System.Collections.Generic;

namespace ProjectVagabond
{
    /// <summary>
    /// Represents a template for creating an entity, loaded from a JSON file.
    /// It defines the set of components that an entity of this type should have.
    /// </summary>
    public class Archetype
    {
        /// <summary>
        /// The unique identifier for this archetype (e.g., "player", "wanderer_npc").
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// A user-friendly name for the archetype (e.g., "Player", "Wanderer").
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// A list of component definitions. Each dictionary represents a single component,
        /// specifying its type and initial property values.
        /// </summary>
        public List<Dictionary<string, object>> Components { get; set; }
    }
}
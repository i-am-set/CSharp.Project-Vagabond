namespace ProjectVagabond.Battle
{
    /// <summary>
    /// Represents a single element definition, loaded from a data file.
    /// </summary>
    public class ElementDefinition
    {
        /// <summary>
        /// The unique integer key for the element.
        /// </summary>
        public int ElementID { get; set; }

        /// <summary>
        /// The display name of the element (e.g., "Fire").
        /// </summary>
        public string ElementName { get; set; }
    }
}
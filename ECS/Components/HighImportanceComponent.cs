namespace ProjectVagabond
{
    /// <summary>
    /// A marker component for entities that should always be processed by systems,
    /// regardless of their distance from the player (e.g., quest-critical NPCs,
    /// long-running environmental effects).
    /// </summary>
    public class HighImportanceComponent : IComponent
    {
    }
}
namespace ProjectVagabond.Encounters
{
    /// <summary>
    /// A static class containing all concrete implementations of encounter outcomes.
    /// Methods in this class are discovered by the EncounterActionRegistry via the [EncounterAction] attribute.
    /// </summary>
    public static class EncounterActions
    {
        /// <summary>
        /// A simple test action that publishes a message to the terminal.
        /// </summary>
        /// <param name="value">The message to be published.</param>
        [EncounterAction("LogMessage")]
        public static void LogMessage(string value)
        {
            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = value });
        }
    }
}
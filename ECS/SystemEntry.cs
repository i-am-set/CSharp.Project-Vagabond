namespace ProjectVagabond
{
    /// <summary>
    /// A data-holding class that pairs a system with its update frequency information.
    /// Used by the SystemManager to orchestrate updates.
    /// </summary>
    public class SystemEntry
    {
        /// <summary>
        /// The system instance.
        /// </summary>
        public ISystem System { get; }

        /// <summary>
        /// The desired time interval between updates, in seconds.
        /// A value of 0 means the system updates every frame.
        /// </summary>
        public float UpdateInterval { get; }

        /// <summary>
        /// Accumulates the elapsed time since the last update.
        /// </summary>
        public float Accumulator { get; set; }

        /// <summary>
        /// Initializes a new instance of the SystemEntry class.
        /// </summary>
        /// <param name="system">The system to be managed.</param>
        /// <param name="updateInterval">The update interval in seconds.</param>
        public SystemEntry(ISystem system, float updateInterval)
        {
            System = system;
            UpdateInterval = updateInterval;
            Accumulator = 0f;
        }
    }
}
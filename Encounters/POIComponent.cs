namespace ProjectVagabond.Encounters
{
    /// <summary>
    /// A component that marks an entity as a Point of Interest (POI).
    /// It contains the necessary data to trigger a game encounter when the player interacts with it.
    /// </summary>
    public class POIComponent : IComponent, ICloneableComponent, IInitializableComponent
    {
        /// <summary>
        /// The unique ID of the encounter to trigger from the EncounterManager.
        /// </summary>
        public string EncounterId { get; set; }

        /// <summary>
        /// If false, the POI entity will be destroyed after its encounter is triggered.
        /// If true, it will remain.
        /// </summary>
        public bool IsPersistent { get; set; } = true;

        /// <summary>
        /// Data for a timed encounter associated with this POI.
        /// </summary>
        public TimedEncounterData TimedEvent { get; set; }

        /// <summary>
        /// A runtime timer for the timed encounter. Not loaded from JSON.
        /// </summary>
        public float TimedEventTimer { get; set; }

        public void Initialize()
        {
            // Set the initial timer when the component is created.
            if (TimedEvent != null)
            {
                TimedEventTimer = TimedEvent.Duration;
            }
        }

        public IComponent Clone()
        {
            var clone = (POIComponent)this.MemberwiseClone();
            // Perform a deep copy of the TimedEncounterData if it exists, as it's a reference type.
            if (this.TimedEvent != null)
            {
                clone.TimedEvent = new TimedEncounterData
                {
                    EventType = this.TimedEvent.EventType,
                    Duration = this.TimedEvent.Duration
                };
            }
            // The timer is runtime state and should be reset, which Initialize() will handle.
            clone.TimedEventTimer = 0f;
            return clone;
        }
    }
}
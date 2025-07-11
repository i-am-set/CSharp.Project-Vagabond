namespace ProjectVagabond
{
    /// <summary>
    /// Holds the dynamic stats for a single combat turn.
    /// </summary>
    public class CombatStatsComponent : IComponent, ICloneableComponent
    {
        /// <summary>
        /// The "currency" an entity spends to perform attacks during a turn.
        /// </summary>
        public int ActionPoints { get; set; }

        public IComponent Clone()
        {
            return (IComponent)this.MemberwiseClone();
        }
    }
}
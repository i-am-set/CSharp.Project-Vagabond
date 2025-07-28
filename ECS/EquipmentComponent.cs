namespace ProjectVagabond
{
    /// <summary>
    /// A component that manages an entity's equipped items.
    /// </summary>
    public class EquipmentComponent : IComponent, ICloneableComponent
    {
        /// <summary>
        /// The ID of the weapon currently equipped by the entity.
        /// A null or empty value indicates the entity is unarmed.
        /// </summary>
        public string EquippedWeaponId { get; set; }

        public IComponent Clone()
        {
            return (IComponent)this.MemberwiseClone();
        }
    }
}
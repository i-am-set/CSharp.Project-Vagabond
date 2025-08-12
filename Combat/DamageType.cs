namespace ProjectVagabond.Combat
{
    /// <summary>
    /// Defines the various types of damage and effects in the game.
    /// </summary>
    public enum DamageType
    {
        // Physical
        Slashing,
        Blunt,

        // Elemental
        Fire,
        Water,
        Air,
        Electric,
        Earth,
        Toxic,

        // Metaphysical
        Life,
        Decay,
        Light,
        Shadow,
        Void,
        Arcane, // Raw magic
        Blood,
        Entropy, // Chaos magic
        Fabric // Meta-magic
    }
}
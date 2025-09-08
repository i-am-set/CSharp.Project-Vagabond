using System.Collections.Generic;

namespace ProjectVagabond.Battle
{
    /// <summary>
    /// A static class to hold configuration data for the next battle to be initiated.
    /// This allows for decoupling the battle setup from the scene transition logic.
    /// </summary>
    public static class BattleSetup
    {
        /// <summary>
        /// A list of enemy archetype IDs to spawn in the next battle.
        /// If null or empty, the BattleScene will use its default encounter.
        /// </summary>
        public static List<string> EnemyArchetypes { get; set; }
    }
}
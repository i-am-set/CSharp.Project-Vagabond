using Microsoft.Xna.Framework.Input;
using ProjectVagabond;
using ProjectVagabond.Battle;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ProjectVagabond.Battle
{
    /// <summary>
    /// Holds all core attributes for a combatant, forming the fundamental inputs for combat calculations.
    /// </summary>
    public class CombatantStats
    {
        /// <summary>
        /// A combatant's maximum vitality.
        /// </summary>
        public int MaxHP { get; set; }

        /// <summary>
        /// A combatant's current vitality. When reduced to 0, they are defeated.
        /// </summary>
        public int CurrentHP { get; set; }

        /// <summary>
        /// Governs the potency of Physical moves.
        /// </summary>
        public int Strength { get; set; }

        /// <summary>
        /// Governs the potency of Magical moves.
        /// </summary>
        public int Intelligence { get; set; }

        /// <summary>
        /// A combatant's universal resilience against all incoming damage.
        /// </summary>
        public int Tenacity { get; set; }

        /// <summary>
        /// A combatant's speed and reaction time, determining action order.
        /// </summary>
        public int Agility { get; set; }
    }
}
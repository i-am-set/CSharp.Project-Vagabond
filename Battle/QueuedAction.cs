#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond;
using ProjectVagabond.Battle;
using ProjectVagabond.Scenes;
using ProjectVagabond.UI;
using System;
using System.Collections.Generic;

namespace ProjectVagabond.Battle
{
    /// <summary>
    /// Represents a single, chosen action waiting to be executed in the action queue.
    /// It captures the state of the actor and target at the moment the action is chosen.
    /// </summary>
    public class QueuedAction
    {
        /// <summary>
        /// The combatant performing the action.
        /// </summary>
        public BattleCombatant Actor { get; set; } = default!;

        /// <summary>
        /// The move that was selected. Null if an item was used.
        /// </summary>
        public MoveData? ChosenMove { get; set; }

        /// <summary>
        /// The item that was used. Null if a move was used.
        /// </summary>
        public ConsumableItemData? ChosenItem { get; set; }

        /// <summary>
        /// The combatant being targeted by the move. Can be null for non-targeted moves.
        /// </summary>
        public BattleCombatant? Target { get; set; }

        /// <summary>
        /// A copy of the move's priority, used for sorting the action queue.
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// A copy of the actor's agility, used for tie-breaking in the action queue.
        /// </summary>
        public int ActorAgility { get; set; }
    }
}
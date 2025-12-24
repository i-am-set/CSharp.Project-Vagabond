#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Scenes;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ProjectVagabond.Battle
{
    public enum QueuedActionType
    {
        Move,
        Item,
        Charging,
        Switch
    }

    public class QueuedAction
    {
        public QueuedActionType Type { get; set; }
        public BattleCombatant Actor { get; set; } = default!;
        public MoveData? ChosenMove { get; set; }

        /// <summary>
        /// The specific MoveEntry used for this action, if applicable.
        /// </summary>
        public MoveEntry? SpellbookEntry { get; set; }

        public ConsumableItemData? ChosenItem { get; set; }

        /// <summary>
        /// For Moves/Items: The target of the action.
        /// For Switch: The benched combatant to swap in.
        /// </summary>
        public BattleCombatant? Target { get; set; }

        public int Priority { get; set; }
        public int ActorAgility { get; set; }
        public bool IsLastActionInRound { get; set; } = false;
    }
}

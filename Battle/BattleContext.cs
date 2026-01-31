using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;

namespace ProjectVagabond.Battle.Abilities
{
    public class BattleContext
    {
        public BattleCombatant Actor { get; set; }
        public BattleCombatant Target { get; set; }
        public MoveData Move { get; set; }
        public QueuedAction Action { get; set; }
        public bool IsSimulation { get; set; }

        public void ResetMultipliers()
        {
            Actor = null;
            Target = null;
            Move = null;
            Action = null;
            IsSimulation = false;
        }
    }
}
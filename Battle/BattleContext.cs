using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;

namespace ProjectVagabond.Battle.Abilities
{
    public enum VarianceMode { Random, Median, Min, Max }

    public class BattleContext
    {
        public BattleCombatant Actor { get; set; }
        public BattleCombatant Target { get; set; }
        public CompiledMove Move { get; set; }
        public QueuedAction Action { get; set; }
        public bool IsSimulation { get; set; }
        public VarianceMode SimulationVariance { get; set; } = VarianceMode.Random;
        public IEnumerable<BattleCombatant> AllCombatants { get; set; }

        public void ResetMultipliers()
        {
            Actor = null;
            Target = null;
            Move = null;
            Action = null;
            IsSimulation = false;
            SimulationVariance = VarianceMode.Random;
        }
    }
}
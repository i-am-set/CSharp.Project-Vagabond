using ProjectVagabond.Battle;
using System.Collections.Generic;

namespace ProjectVagabond.Battle.Abilities
{
    public class CombatTriggerContext
    {
        // --- Immutable Context ---
        public BattleCombatant Actor { get; set; }
        public BattleCombatant Target { get; set; }
        public MoveData Move { get; set; }
        public QueuedAction Action { get; set; }

        // --- Mutable Data ---
        public float DamageMultiplier { get; set; } = 1.0f;
        public float FlatDamageBonus { get; set; } = 0f;

        public float BasePower { get; set; }
        public float BaseDamage { get; set; } // Added: Used for logs/calculations
        public int FinalDamage { get; set; } // Added: Used for OnHit/OnDamaged

        public float AccumulatedLifestealPercent { get; set; } = 0f; // Added: For Lifesteal

        public OffensiveStatType StatType { get; set; }
        public float StatValue { get; set; } // Used for Stats, Accuracy, Crit Chance

        public StatusEffectType StatusType { get; set; }
        public int StatusDuration { get; set; }
        public bool IsCancelled { get; set; } = false;
        public bool IsCritical { get; set; }
        public bool IsGraze { get; set; }

        public List<int> Weaknesses { get; set; }
        public List<int> Resistances { get; set; }

        public string LockedMoveID { get; set; }

        public bool IsSimulation { get; set; } = false;

        public void ResetMultipliers()
        {
            DamageMultiplier = 1.0f;
            FlatDamageBonus = 0f;
            IsCancelled = false;
        }
    }
}
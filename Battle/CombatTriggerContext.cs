using ProjectVagabond;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;

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
        public float BaseDamage { get; set; } // Used for logs/calculations
        public int FinalDamage { get; set; } // Used for OnHit/OnDamaged

        public float AccumulatedLifestealPercent { get; set; } = 0f;

        public OffensiveStatType StatType { get; set; }
        public float StatValue { get; set; } // Used for Stats, Accuracy, Crit Chance

        public StatusEffectType StatusType { get; set; }
        public int StatusDuration { get; set; }
        public bool IsCancelled { get; set; } = false;
        public bool IsCritical { get; set; }
        public bool IsGraze { get; set; }

        public string LockedMoveID { get; set; }

        // --- Rule-Breaking Flags ---
        public bool BreakGuard { get; set; }
        public bool IgnoreEvasion { get; set; }
        public bool IsLethal { get; set; }

        // --- Generic Data ---
        public Dictionary<string, object> Tags { get; set; } = new Dictionary<string, object>();

        public void ResetMultipliers()
        {
            DamageMultiplier = 1.0f;
            FlatDamageBonus = 0f;
            IsCancelled = false;
            BreakGuard = false;
            IgnoreEvasion = false;
            IsLethal = false;
            Tags.Clear();
        }
    }
}
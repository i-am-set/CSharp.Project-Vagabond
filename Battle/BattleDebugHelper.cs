using ProjectVagabond.Battle;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProjectVagabond.Utils
{
    public static class BattleDebugHelper
    {
        public static void RunDamageCalculationTestSuite()
        {
            Debug.WriteLine("\n--- RUNNING ELEMENTAL DAMAGE TEST SUITE ---");

            if (BattleDataCache.Elements == null || !BattleDataCache.Elements.Any())
            {
                Debug.WriteLine("-> FATAL ERROR: BattleDataCache.Elements is null or empty. Elements.json likely failed to load or is empty.");
                Debug.WriteLine("--- TEST SUITE ABORTED ---");
                return;
            }

            // No longer checking matrix coverage as the matrix is gone.
            Debug.WriteLine($"-> Found {BattleDataCache.Elements.Count} total elements defined in Elements.json.");

            var dummyMove = new MoveData();
            var dummyTarget = new BattleCombatant();

            // Test a few basic interactions
            var elements = BattleDataCache.Elements.Values.ToList();
            if (elements.Count > 0)
            {
                int testElementId = elements[0].ElementID;

                // Test Weakness
                dummyMove.OffensiveElementIDs = new List<int> { testElementId };
                dummyTarget.WeaknessElementIDs = new List<int> { testElementId };
                dummyTarget.ResistanceElementIDs = new List<int>();

                float mult = DamageCalculator.GetElementalMultiplier(dummyMove, dummyTarget);
                Debug.WriteLine($"Test Weakness (ID {testElementId}): Expected 2.0, Got {mult}");

                // Test Resistance
                dummyTarget.WeaknessElementIDs = new List<int>();
                dummyTarget.ResistanceElementIDs = new List<int> { testElementId };
                mult = DamageCalculator.GetElementalMultiplier(dummyMove, dummyTarget);
                Debug.WriteLine($"Test Resistance (ID {testElementId}): Expected 0.5, Got {mult}");

                // Test Neutral
                dummyTarget.ResistanceElementIDs = new List<int>();
                mult = DamageCalculator.GetElementalMultiplier(dummyMove, dummyTarget);
                Debug.WriteLine($"Test Neutral (ID {testElementId}): Expected 1.0, Got {mult}");
            }

            Debug.WriteLine("\n--- TEST SUITE COMPLETE ---");
        }
    }
}
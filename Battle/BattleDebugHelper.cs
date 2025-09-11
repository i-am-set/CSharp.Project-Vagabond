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

            var offensiveElements = BattleDataCache.Elements.Values.Where(e => e.Type == ElementType.Offensive).ToList();
            var defensiveElements = BattleDataCache.Elements.Values.Where(e => e.Type == ElementType.Defensive).ToList();

            Debug.WriteLine($"-> Found {BattleDataCache.Elements.Count} total elements defined in Elements.json.");
            Debug.WriteLine($"-> Found {offensiveElements.Count} OFFENSIVE elements.");
            Debug.WriteLine($"-> Found {defensiveElements.Count} DEFENSIVE elements.");

            if (!offensiveElements.Any() || !defensiveElements.Any())
            {
                Debug.WriteLine("-> ERROR: No offensive or defensive elements were found after filtering. Check the 'Type' property in Elements.json for each element.");
                Debug.WriteLine("--- TEST SUITE ABORTED ---");
                return;
            }

            VerifyMatrixCoverage(offensiveElements, defensiveElements);

            var dummyMove = new MoveData();
            var dummyTarget = new BattleCombatant();

            foreach (var offElement in offensiveElements.OrderBy(e => e.ElementID))
            {
                dummyMove.OffensiveElementIDs = new List<int> { offElement.ElementID };
                Debug.WriteLine($"\n--- Attacking with: {offElement.ElementName} (ID: {offElement.ElementID}) ---");

                foreach (var defElement in defensiveElements.OrderBy(e => e.ElementID))
                {
                    dummyTarget.DefensiveElementIDs = new List<int> { defElement.ElementID };

                    float multiplier = DamageCalculator.GetElementalMultiplier(dummyMove, dummyTarget);

                    string effectiveness;
                    if (multiplier > 1.0f) effectiveness = "WEAKNESS";
                    else if (multiplier > 0.0f && multiplier < 1.0f) effectiveness = "RESISTANCE";
                    else if (multiplier == 0.0f) effectiveness = "IMMUNITY";
                    else effectiveness = "NEUTRAL";

                    Debug.WriteLine($"    -> vs {defElement.ElementName,-10} (ID: {defElement.ElementID,-3}) | Multiplier: {multiplier:F1} ({effectiveness})");
                }
            }

            Debug.WriteLine("\n--- TEST SUITE COMPLETE ---");
        }

        private static void VerifyMatrixCoverage(List<ElementDefinition> offensive, List<ElementDefinition> defensive)
        {
            Debug.WriteLine("\n--- Verifying Matrix Coverage ---");
            bool allCovered = true;

            // Check for missing attacking elements (rows)
            foreach (var offElement in offensive)
            {
                if (!BattleDataCache.InteractionMatrix.ContainsKey(offElement.ElementID))
                {
                    Debug.WriteLine($"-> WARNING: Attacking Element '{offElement.ElementName}' (ID: {offElement.ElementID}) is MISSING from the CSV matrix rows.");
                    allCovered = false;
                }
            }

            // Check for missing defending elements (columns)
            if (BattleDataCache.InteractionMatrix.Any())
            {
                var firstRow = BattleDataCache.InteractionMatrix.First().Value;
                foreach (var defElement in defensive)
                {
                    if (!firstRow.ContainsKey(defElement.ElementID))
                    {
                        Debug.WriteLine($"-> WARNING: Defending Element '{defElement.ElementName}' (ID: {defElement.ElementID}) is MISSING from the CSV matrix columns.");
                        allCovered = false;
                    }
                }
            }
            else
            {
                Debug.WriteLine("-> WARNING: Interaction Matrix is empty. Cannot verify defending elements.");
                allCovered = false;
            }


            if (allCovered)
            {
                Debug.WriteLine("-> SUCCESS: All defined elements are present in the interaction matrix.");
            }
            else
            {
                Debug.WriteLine("-> ACTION REQUIRED: Please check your ElementalInteractionMatrix.csv file against Elements.json.");
            }
        }
    }
}
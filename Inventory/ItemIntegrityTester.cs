using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectVagabond;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ProjectVagabond.Utils
{
    public static class ItemIntegrityTester
    {
        private static int _passed = 0;
        private static int _failed = 0;
        private static int _skipped = 0;

        public static void RunIntegrityCheck()
        {
            _passed = 0;
            _failed = 0;
            _skipped = 0;

            LogHeader("=== STARTING ITEM INTEGRITY CHECK ===");

            // 1. Test Weapons
            if (BattleDataCache.Weapons.Count == 0) LogFail("WARNING: Weapon Cache is empty!");
            TestCollection(
                "WEAPONS",
                BattleDataCache.Weapons.Values,
                w => w.WeaponName,
                w => w.Effects
            );

            // 2. Test Relics
            if (BattleDataCache.Relics.Count == 0) LogFail("WARNING: Relic Cache is empty!");
            TestCollection(
                "RELICS",
                BattleDataCache.Relics.Values,
                r => r.RelicName,
                r => r.Effects
            );

            string resultColor = _failed == 0 ? "[palette_lightgreen]" : "[palette_red]";
            string msg = $"=== CHECK COMPLETE: {resultColor}{_passed} PASSED[/], [palette_red]{_failed} FAILED[/], [palette_yellow]{_skipped} SKIPPED[/] ===";

            // Log final summary to both
            Debug.WriteLine($"[ItemIntegrityTester] {msg.Replace("[palette_lightgreen]", "").Replace("[palette_red]", "").Replace("[palette_yellow]", "").Replace("[/]", "")}");
            EventBus.Publish(new GameEvents.TerminalMessagePublished
            {
                Message = msg,
                BaseColor = _failed == 0 ? Color.Lime : Color.Red
            });
        }

        private static void TestCollection<T>(
            string categoryName,
            IEnumerable<T> items,
            Func<T, string> nameSelector,
            Func<T, Dictionary<string, string>> effectsSelector)
        {
            LogHeader($"--- Testing {categoryName} ---");

            foreach (var item in items)
            {
                string itemName = nameSelector(item);
                var effects = effectsSelector(item);

                // 1. Handle Skipped Items (No Effects)
                if (effects == null || effects.Count == 0)
                {
                    _skipped++;
                    LogSkip($"SKIP: {itemName} (No passive effects)");
                    continue;
                }

                // 2. Test Each Effect on the Item
                foreach (var kvp in effects)
                {
                    string abilityKey = kvp.Key;
                    string abilityParams = kvp.Value;

                    // Create a temporary dictionary for the factory to isolate this specific effect
                    var singleEffectDict = new Dictionary<string, string> { { abilityKey, abilityParams } };

                    try
                    {
                        // Attempt to create the ability via the Factory
                        // We pass an empty stat dictionary because we are only testing the Effect string parsing here
                        var abilities = AbilityFactory.CreateAbilitiesFromData(singleEffectDict, new Dictionary<string, int>());

                        if (abilities.Count == 1)
                        {
                            _passed++;
                            LogSuccess($"PASS: {itemName} -> [{abilityKey}] args:({abilityParams})");
                        }
                        else
                        {
                            _failed++;
                            LogFail($"FAIL: {itemName} -> [{abilityKey}] args:({abilityParams})");
                            LogFail($"      Reason: Factory returned 0 abilities. Check if '{abilityKey}Ability' class exists.");
                        }
                    }
                    catch (Exception ex)
                    {
                        _failed++;
                        LogFail($"CRASH: {itemName} -> [{abilityKey}] args:({abilityParams})");
                        LogFail($"       Error: {ex.Message}");
                        if (ex.InnerException != null)
                        {
                            LogFail($"       Inner: {ex.InnerException.Message}");
                        }
                    }
                }
            }
        }

        // --- Logging Helpers (Dual Output) ---

        private static void LogHeader(string message)
        {
            string tagged = $"[palette_blue]{message}[/]";
            Debug.WriteLine(tagged);
            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = tagged });
        }

        private static void LogSuccess(string message)
        {
            string tagged = $"[palette_lightgreen]{message}[/]";
            Debug.WriteLine(tagged);
            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = tagged });
        }

        private static void LogFail(string message)
        {
            string tagged = $"[palette_red]{message}[/]";
            Debug.WriteLine(tagged);
            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = tagged });
        }

        private static void LogSkip(string message)
        {
            string tagged = $"[palette_yellow]{message}[/]";
            Debug.WriteLine(tagged);
            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = tagged });
        }
    }
}

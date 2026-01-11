using Microsoft.Xna.Framework;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Battle.UI; // Added for BattleAnimationManager
using System;
using System.Collections.Generic;

namespace ProjectVagabond.Utils
{
    public static class AbilityTester
    {
        private static int _passed = 0;
        private static int _failed = 0;
        private static int _skipped = 0;
        public static void RunAllTests()
        {
            _passed = 0;
            _failed = 0;
            _skipped = 0;

            LogHeader("=== STARTING ABILITY LOGIC TESTS ===");

            // --- MOCK BATTLE ENVIRONMENT SETUP ---
            var mockPlayer = CreateDummy(100, 100);
            mockPlayer.IsPlayerControlled = true;
            var mockEnemy = CreateDummy(100, 100);
            mockEnemy.IsPlayerControlled = false;

            // Register a temporary BattleManager
            var mockBattleManager = new BattleManager(
                new List<BattleCombatant> { mockPlayer },
                new List<BattleCombatant> { mockEnemy },
                new BattleAnimationManager()
            );
            ServiceLocator.Register(mockBattleManager);

            try
            {
                LogHeader("Testing Stat Modifiers...");
                TestStatModifiers(mockPlayer, mockEnemy);
            }
            catch (Exception ex)
            {
                LogFail($"CRITICAL TEST SUITE FAILURE: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"TEST FAILURE: {ex}");
            }
            finally
            {
                // Cleanup: Remove the mock manager so it doesn't interfere with the real game
                ServiceLocator.Unregister<BattleManager>();
            }

            string resultColor = _failed == 0 ? "[palette_lightgreen]" : "[palette_red]";
            string msg = $"--- TESTS COMPLETE: {resultColor}{_passed} PASSED[/], [palette_red]{_failed} FAILED[/], [palette_yellow]{_skipped} SKIPPED[/] ---";

            // Log to GameLogger directly to ensure tags are parsed by DebugConsole
            GameLogger.Log(LogSeverity.Info, msg);

            // Also publish to EventBus for the in-game terminal (if active)
            EventBus.Publish(new GameEvents.TerminalMessagePublished
            {
                Message = msg,
                BaseColor = _failed == 0 ? Color.Lime : Color.Red
            });
        }

        private static void TestStatModifiers(BattleCombatant player, BattleCombatant enemy)
        {
            // 1. FlatStatBonusAbility
            var mods = new Dictionary<string, int> { { "Strength", 10 } };
            var ability = new FlatStatBonusAbility(mods);
            int result = ability.ModifyStat(OffensiveStatType.Strength, 10, new BattleCombatant());
            Assert(result == 20, "FlatStatBonus (Strength +10)");

            // 2. CorneredAnimalAbility (HP Threshold)
            // Updated Signature: Threshold (33%), Stat (Agility), Amount (1 stage = +50%)
            var ca = new CorneredAnimalAbility(33f, OffensiveStatType.Agility, 1);

            player.Stats.CurrentHP = 10; player.Stats.MaxHP = 100; // Low HP (10%)
            int lowResult = ca.ModifyStat(OffensiveStatType.Agility, 10, player);

            player.Stats.CurrentHP = 100; // High HP (100%)
            int highResult = ca.ModifyStat(OffensiveStatType.Agility, 10, player);

            // Expected: 10 * (1.0 + 0.5 * 1) = 15
            Assert(lowResult == 15, "CorneredAnimal (Low HP Trigger)");
            Assert(highResult == 10, "CorneredAnimal (High HP Ignore)");
        }

        // --- Helpers ---

        private static BattleCombatant CreateDummy(int currentHp, int maxHp)
        {
            var c = new BattleCombatant
            {
                Name = "Dummy",
                Stats = new CombatantStats { CurrentHP = currentHp, MaxHP = maxHp },
                BattleSlot = 0
            };
            // IsActiveOnField is read-only and derived from BattleSlot.
            // Setting BattleSlot = 0 makes IsActiveOnField true.
            return c;
        }

        private static void Assert(bool condition, string testName)
        {
            if (condition)
            {
                _passed++;
                string msg = $"  [palette_lightgreen]PASS:[/] {testName}";
                GameLogger.Log(LogSeverity.Info, msg);
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = msg });
            }
            else
            {
                _failed++;
                string msg = $"  [palette_red]FAIL:[/] {testName}";
                GameLogger.Log(LogSeverity.Error, msg);
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = msg });
            }
        }

        private static void LogInfo(string message)
        {
            string tagged = $"[palette_blue]{message}[/]";
            GameLogger.Log(LogSeverity.Info, tagged);
            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = tagged });
        }

        private static void LogHeader(string message)
        {
            string tagged = $"[palette_blue]{message}[/]";
            GameLogger.Log(LogSeverity.Info, tagged);
            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = tagged });
        }

        private static void LogFail(string message)
        {
            string tagged = $"[palette_red]{message}[/]";
            GameLogger.Log(LogSeverity.Error, tagged);
            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = tagged });
        }

        private static void LogSkipped(string message)
        {
            _skipped++;
            string tagged = $"  [palette_yellow]SKIPPED:[/] {message}";
            GameLogger.Log(LogSeverity.Warning, tagged);
            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = tagged });
        }
    }
}
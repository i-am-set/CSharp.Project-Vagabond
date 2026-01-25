using Microsoft.Xna.Framework;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Battle.UI;
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

            var mockPlayer = CreateDummy(100, 100);
            mockPlayer.IsPlayerControlled = true;
            var mockEnemy = CreateDummy(100, 100);
            mockEnemy.IsPlayerControlled = false;

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
                ServiceLocator.Unregister<BattleManager>();
            }

            string resultColor = _failed == 0 ? "[Palette_Leaf]" : "[Palette_Rust]";
            string msg = $"--- TESTS COMPLETE: {resultColor}{_passed} PASSED[/], [Palette_Rust]{_failed} FAILED[/], [Palette_DarkSun]{_skipped} SKIPPED[/] ---";

            GameLogger.Log(LogSeverity.Info, msg);
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

            // Manually trigger event
            var ctx = new CombatTriggerContext { Actor = player, StatType = OffensiveStatType.Strength, StatValue = 10 };
            ability.OnCombatEvent(CombatEventType.CalculateStat, ctx);

            Assert(ctx.StatValue == 20, "FlatStatBonus (Strength +10)");

            // 2. CorneredAnimalAbility (HP Threshold)
            var ca = new CorneredAnimalAbility(33f, OffensiveStatType.Agility, 1);

            player.Stats.CurrentHP = 10; player.Stats.MaxHP = 100; // Low HP (10%)
            ctx = new CombatTriggerContext { Actor = player, StatType = OffensiveStatType.Agility, StatValue = 10 };
            ca.OnCombatEvent(CombatEventType.CalculateStat, ctx);
            int lowResult = (int)ctx.StatValue;

            player.Stats.CurrentHP = 100; // High HP (100%)
            ctx = new CombatTriggerContext { Actor = player, StatType = OffensiveStatType.Agility, StatValue = 10 };
            ca.OnCombatEvent(CombatEventType.CalculateStat, ctx);
            int highResult = (int)ctx.StatValue;

            Assert(lowResult == 15, "CorneredAnimal (Low HP Trigger)");
            Assert(highResult == 10, "CorneredAnimal (High HP Ignore)");
        }

        private static BattleCombatant CreateDummy(int currentHp, int maxHp)
        {
            var c = new BattleCombatant
            {
                Name = "Dummy",
                Stats = new CombatantStats { CurrentHP = currentHp, MaxHP = maxHp },
                BattleSlot = 0
            };
            return c;
        }

        private static void Assert(bool condition, string testName)
        {
            if (condition)
            {
                _passed++;
                string msg = $"  [Palette_Leaf]PASS:[/] {testName}";
                GameLogger.Log(LogSeverity.Info, msg);
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = msg });
            }
            else
            {
                _failed++;
                string msg = $"  [Palette_Rust]FAIL:[/] {testName}";
                GameLogger.Log(LogSeverity.Error, msg);
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = msg });
            }
        }

        private static void LogInfo(string message)
        {
            string tagged = $"[Palette_Sky]{message}[/]";
            GameLogger.Log(LogSeverity.Info, tagged);
            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = tagged });
        }

        private static void LogHeader(string message)
        {
            string tagged = $"[Palette_Sky]{message}[/]";
            GameLogger.Log(LogSeverity.Info, tagged);
            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = tagged });
        }

        private static void LogFail(string message)
        {
            string tagged = $"[Palette_Rust]{message}[/]";
            GameLogger.Log(LogSeverity.Error, tagged);
            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = tagged });
        }

        private static void LogSkipped(string message)
        {
            _skipped++;
            string tagged = $"  [Palette_DarkSun]SKIPPED:[/] {message}";
            GameLogger.Log(LogSeverity.Warning, tagged);
            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = tagged });
        }
    }
}
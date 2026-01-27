using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Progression;
using ProjectVagabond.Scenes;
using ProjectVagabond.Transitions;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

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
                // Relic abilities removed, skipping stat modifier tests for now
                LogSkipped("Stat Modifier tests skipped (Relics removed)");
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

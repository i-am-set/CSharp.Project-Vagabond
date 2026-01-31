using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Utils;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System;

namespace ProjectVagabond.Battle.UI
{
    public class BattleLogManager
    {
        private struct LogEntry
        {
            public string Text;
            public Color Color;
        }

        private readonly List<LogEntry> _logs = new List<LogEntry>();
        private const int MAX_LOGS = 15;
        private readonly Global _global;
        private readonly Core _core;

        public BattleLogManager()
        {
            _global = ServiceLocator.Get<Global>();
            _core = ServiceLocator.Get<Core>();
            // Constructor no longer auto-subscribes to prevent double-subscription issues
            // BattleScene.Enter will handle subscription.
        }

        public void Reset()
        {
            _logs.Clear();
        }

        public void Unsubscribe()
        {
            EventBus.Unsubscribe<GameEvents.ActionDeclared>(OnActionDeclared);
            EventBus.Unsubscribe<GameEvents.BattleActionExecuted>(OnActionExecuted);
            EventBus.Unsubscribe<GameEvents.CombatantDefeated>(OnCombatantDefeated);
            EventBus.Unsubscribe<GameEvents.ActionFailed>(OnActionFailed);
            EventBus.Unsubscribe<GameEvents.CombatantHealed>(OnCombatantHealed);
            EventBus.Unsubscribe<GameEvents.StatusEffectTriggered>(OnStatusEffectTriggered);
            EventBus.Unsubscribe<GameEvents.CombatantManaRestored>(OnCombatantManaRestored);
            EventBus.Unsubscribe<GameEvents.CombatantRecoiled>(OnCombatantRecoiled);
            EventBus.Unsubscribe<GameEvents.NextEnemyApproaches>(OnNextEnemyApproaches);
        }

        public void Subscribe()
        {
            // Ensure we don't double subscribe by unsubscribing first
            Unsubscribe();

            EventBus.Subscribe<GameEvents.ActionDeclared>(OnActionDeclared);
            EventBus.Subscribe<GameEvents.BattleActionExecuted>(OnActionExecuted);
            EventBus.Subscribe<GameEvents.CombatantDefeated>(OnCombatantDefeated);
            EventBus.Subscribe<GameEvents.ActionFailed>(OnActionFailed);
            EventBus.Subscribe<GameEvents.CombatantHealed>(OnCombatantHealed);
            EventBus.Subscribe<GameEvents.StatusEffectTriggered>(OnStatusEffectTriggered);
            EventBus.Subscribe<GameEvents.CombatantManaRestored>(OnCombatantManaRestored);
            EventBus.Subscribe<GameEvents.CombatantRecoiled>(OnCombatantRecoiled);
            EventBus.Subscribe<GameEvents.NextEnemyApproaches>(OnNextEnemyApproaches);
        }

        private void AddLog(string text, Color color)
        {
            // Newest on top
            _logs.Insert(0, new LogEntry { Text = text.ToUpper(), Color = color });

            if (_logs.Count > MAX_LOGS)
            {
                _logs.RemoveAt(_logs.Count - 1);
            }
        }

        private void OnActionDeclared(GameEvents.ActionDeclared e)
        {
            // No log for declaration, only execution
        }

        private void OnActionExecuted(GameEvents.BattleActionExecuted e)
        {
            string moveName = e.ChosenMove.MoveName.ToUpper();
            string actorName = e.Actor.Name.ToUpper();

            Color logColor = _global.Palette_DarkestPale;

            for (int i = 0; i < e.Targets.Count; i++)
            {
                var target = e.Targets[i];
                var result = e.DamageResults[i];
                string targetName = target.Name.ToUpper();

                string verb = "HIT";

                if (result.WasGraze)
                {
                    verb = "GRAZED";
                }
                else if (result.WasCritical)
                {
                    verb = "CRITICALLY HIT";
                }
                else if (result.WasProtected)
                {
                    verb = "WAS BLOCKED BY";
                }

                string logLine = $"{actorName} {verb} {targetName} WITH {moveName}";
                AddLog(logLine, logColor);
            }
        }

        private void OnCombatantDefeated(GameEvents.CombatantDefeated e)
        {
            // Color: Palette_DarkRust
            Color logColor = _global.Palette_DarkRust;
            AddLog($"{e.DefeatedCombatant.Name} WAS DEFEATED", logColor);
        }

        private void OnActionFailed(GameEvents.ActionFailed e)
        {
            // Color: Palette_DarkShadow
            Color logColor = _global.Palette_DarkShadow;
            string moveName = !string.IsNullOrEmpty(e.MoveName) ? e.MoveName.ToUpper() : "ACTION";
            AddLog($"{e.Actor.Name.ToUpper()} FAILED {moveName}", logColor);
        }

        private void OnCombatantHealed(GameEvents.CombatantHealed e)
        {
            // Color: Palette_DarkShadow (Consistent with other status updates)
            Color logColor = _global.Palette_DarkShadow;
            AddLog($"{e.Target.Name} RECOVERED {e.HealAmount} HP", logColor);
        }

        private void OnStatusEffectTriggered(GameEvents.StatusEffectTriggered e)
        {
            if (e.Damage > 0)
            {
                // Color: Palette_DarkShadow
                Color logColor = _global.Palette_DarkShadow;
                AddLog($"{e.Combatant.Name} TOOK {e.Damage} {e.EffectType.ToString().ToUpper()} DMG", logColor);
            }
        }

        private void OnCombatantManaRestored(GameEvents.CombatantManaRestored e)
        {
            // Color: Palette_DarkShadow
            Color logColor = _global.Palette_DarkShadow;
            AddLog($"{e.Target.Name} RESTORED {e.AmountRestored} MANA", logColor);
        }

        private void OnCombatantRecoiled(GameEvents.CombatantRecoiled e)
        {
            // Color: Palette_DarkShadow
            Color logColor = _global.Palette_DarkShadow;
            AddLog($"{e.Actor.Name} TOOK {e.RecoilDamage} RECOIL DAMAGE", logColor);
        }

        private void OnNextEnemyApproaches(GameEvents.NextEnemyApproaches e)
        {
            // Color: Palette_DarkestPale
            Color logColor = _global.Palette_DarkestPale;
            AddLog("ANOTHER ENEMY APPROACHES", logColor);
        }

        public void Draw(SpriteBatch spriteBatch, bool forceFullVisibility = false)
        {
            if (_logs.Count == 0) return;

            var font = _core.TertiaryFont;
            int lineHeight = font.LineHeight;
            int spacing = 1; // 1 pixel gap
            int startY = 2; // Top padding
            int rightMargin = 2; // Right padding
            float iterationFadeAmount = 0.2f;

            for (int i = 0; i < _logs.Count; i++)
            {
                var log = _logs[i];
                Vector2 size = font.MeasureString(log.Text);

                // Right Align: ScreenWidth - Margin - TextWidth
                float x = Global.VIRTUAL_WIDTH - rightMargin - size.X;
                float y = startY + (i * (lineHeight + spacing));
                Vector2 pos = new Vector2(x, y);

                // --- FADE LOGIC ---
                // Newest (i=0) is 1.0.
                // Each subsequent line fades by iterationFadeAmount
                float alpha = 1.0f;
                if (!forceFullVisibility)
                {
                    alpha = MathHelper.Clamp(1.0f - (i * iterationFadeAmount), 0f, 1f);
                }

                // Optimization: Stop drawing if invisible
                if (alpha <= 0f) break;

                // Draw with specific log color
                // Using outline for readability over the map
                spriteBatch.DrawStringSquareOutlinedSnapped(font, log.Text, pos, log.Color * alpha, _global.Palette_Black * alpha);
            }
        }
    }
}
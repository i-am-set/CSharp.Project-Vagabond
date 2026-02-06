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
            _logs.Insert(0, new LogEntry { Text = text.ToUpper(), Color = color });

            if (_logs.Count > MAX_LOGS)
            {
                _logs.RemoveAt(_logs.Count - 1);
            }
        }

        private void OnActionDeclared(GameEvents.ActionDeclared e) { }

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

                if (result.WasGraze) verb = "GRAZED";
                else if (result.WasCritical) verb = "CRITICALLY HIT";
                else if (result.WasProtected) verb = "WAS BLOCKED BY";

                string logLine = $"{actorName} {verb} {targetName} WITH {moveName}";
                AddLog(logLine, logColor);
            }
        }

        private void OnCombatantDefeated(GameEvents.CombatantDefeated e)
        {
            AddLog($"{e.DefeatedCombatant.Name} WAS DEFEATED", _global.Palette_DarkRust);
        }

        private void OnActionFailed(GameEvents.ActionFailed e)
        {
            string moveName = !string.IsNullOrEmpty(e.MoveName) ? e.MoveName.ToUpper() : "ACTION";
            AddLog($"{e.Actor.Name.ToUpper()} FAILED {moveName}", _global.Palette_DarkShadow);
        }

        private void OnCombatantHealed(GameEvents.CombatantHealed e)
        {
            AddLog($"{e.Target.Name} RECOVERED {e.HealAmount} HP", _global.Palette_DarkShadow);
        }

        private void OnStatusEffectTriggered(GameEvents.StatusEffectTriggered e)
        {
            if (e.Damage > 0)
            {
                AddLog($"{e.Combatant.Name} TOOK {e.Damage} {e.EffectType.ToString().ToUpper()} DMG", _global.Palette_DarkShadow);
            }
        }

        private void OnCombatantManaRestored(GameEvents.CombatantManaRestored e)
        {
            AddLog($"{e.Target.Name} RESTORED {e.AmountRestored} MANA", _global.Palette_DarkShadow);
        }

        private void OnCombatantRecoiled(GameEvents.CombatantRecoiled e)
        {
            AddLog($"{e.Actor.Name} TOOK {e.RecoilDamage} RECOIL DAMAGE", _global.Palette_DarkShadow);
        }

        private void OnNextEnemyApproaches(GameEvents.NextEnemyApproaches e)
        {
            AddLog("ANOTHER ENEMY APPROACHES", _global.Palette_DarkestPale);
        }

        public void Draw(SpriteBatch spriteBatch, bool forceFullVisibility = false)
        {
            if (_logs.Count == 0) return;

            var font = _core.TertiaryFont;
            int lineHeight = font.LineHeight;
            int spacing = 1;
            int startY = 2;
            int rightMargin = 2;

            // If hovering, draw all. If not, draw max 2 lines (Recent + "...")
            int drawCount = forceFullVisibility ? _logs.Count : Math.Min(_logs.Count, 2);

            for (int i = 0; i < drawCount; i++)
            {
                string text = _logs[i].Text;
                Color color = _logs[i].Color;

                // If not hovering and this is the second line, override with ellipsis
                if (!forceFullVisibility && i == 1)
                {
                    text = "...";
                    color = _global.Palette_Gray; // Use a neutral color for the indicator
                }

                Vector2 size = font.MeasureString(text);
                float x = Global.VIRTUAL_WIDTH - rightMargin - size.X;
                float y = startY + (i * (lineHeight + spacing));
                Vector2 pos = new Vector2(x, y);

                // Always draw at full opacity in this new style
                spriteBatch.DrawStringSquareOutlinedSnapped(font, text, pos, color, _global.Palette_Black);
            }
        }
    }
}
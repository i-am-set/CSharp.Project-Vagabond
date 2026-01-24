using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Utils;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ProjectVagabond.Battle.UI
{
    public class BattleLogManager
    {
        private struct LogEntry
        {
            public string Text;
        }

        private readonly List<LogEntry> _logs = new List<LogEntry>();
        private const int MAX_LOGS = 10;
        private readonly Global _global;
        private readonly Core _core;

        public BattleLogManager()
        {
            _global = ServiceLocator.Get<Global>();
            _core = ServiceLocator.Get<Core>();
            Subscribe();
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
        }

        private void Subscribe()
        {
            EventBus.Subscribe<GameEvents.ActionDeclared>(OnActionDeclared);
            EventBus.Subscribe<GameEvents.BattleActionExecuted>(OnActionExecuted);
            EventBus.Subscribe<GameEvents.CombatantDefeated>(OnCombatantDefeated);
            EventBus.Subscribe<GameEvents.ActionFailed>(OnActionFailed);
            EventBus.Subscribe<GameEvents.CombatantHealed>(OnCombatantHealed);
            EventBus.Subscribe<GameEvents.StatusEffectTriggered>(OnStatusEffectTriggered);
        }

        private void AddLog(string text)
        {
            // Newest on top
            _logs.Insert(0, new LogEntry { Text = text.ToUpper() });

            if (_logs.Count > MAX_LOGS)
            {
                _logs.RemoveAt(_logs.Count - 1);
            }
        }

        private void OnActionDeclared(GameEvents.ActionDeclared e)
        {
            if (e.Type == QueuedActionType.Move && e.Move != null)
            {
                string moveName = e.Move.MoveName;
                AddLog($"{e.Actor.Name} USED {moveName}");
            }
            else if (e.Type == QueuedActionType.Switch)
            {
                AddLog($"{e.Actor.Name} IS SWITCHING");
            }
        }

        private void OnActionExecuted(GameEvents.BattleActionExecuted e)
        {
            // Iterate through results and bake details into single lines
            for (int i = 0; i < e.Targets.Count; i++)
            {
                var target = e.Targets[i];
                var result = e.DamageResults[i];
                var sb = new StringBuilder();

                // Base Action
                if (result.WasGraze)
                {
                    sb.Append($"{target.Name} WAS GRAZED");
                }
                else if (result.WasProtected)
                {
                    sb.Append($"{target.Name} PROTECTED ITSELF");
                }
                else
                {
                    sb.Append($"{target.Name} WAS HIT");
                }

                // Baked Details
                if (result.WasCritical)
                {
                    sb.Append(". CRITICAL HIT!");
                }

                if (result.Effectiveness == DamageCalculator.ElementalEffectiveness.Effective)
                {
                    sb.Append(". SUPER EFFECTIVE!");
                }
                else if (result.Effectiveness == DamageCalculator.ElementalEffectiveness.Resisted)
                {
                    sb.Append(". NOT VERY EFFECTIVE.");
                }
                else if (result.Effectiveness == DamageCalculator.ElementalEffectiveness.Immune)
                {
                    sb.Append(". IT HAD NO EFFECT.");
                }

                AddLog(sb.ToString());
            }
        }

        private void OnCombatantDefeated(GameEvents.CombatantDefeated e)
        {
            AddLog($"{e.DefeatedCombatant.Name} WAS DEFEATED!");
        }

        private void OnActionFailed(GameEvents.ActionFailed e)
        {
            AddLog($"{e.Actor.Name} FAILED: {e.Reason.ToUpper()}");
        }

        private void OnCombatantHealed(GameEvents.CombatantHealed e)
        {
            AddLog($"{e.Target.Name} RECOVERED {e.HealAmount} HP");
        }

        private void OnStatusEffectTriggered(GameEvents.StatusEffectTriggered e)
        {
            if (e.Damage > 0)
            {
                AddLog($"{e.Combatant.Name} TOOK {e.Damage} {e.EffectType.ToString().ToUpper()} DMG");
            }
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            if (_logs.Count == 0) return;

            var font = _core.TertiaryFont;
            int lineHeight = font.LineHeight;
            int spacing = 1; // 1 pixel gap
            int startY = 2; // Top padding
            int rightMargin = 2; // Right padding

            for (int i = 0; i < _logs.Count; i++)
            {
                var log = _logs[i];
                Vector2 size = font.MeasureString(log.Text);

                // Right Align: ScreenWidth - Margin - TextWidth
                float x = Global.VIRTUAL_WIDTH - rightMargin - size.X;
                float y = startY + (i * (lineHeight + spacing));
                Vector2 pos = new Vector2(x, y);

                // Fade out older logs
                float alpha = 1.0f - ((float)i / MAX_LOGS);
                // Ensure the top log is always fully visible, bottom ones fade
                alpha = MathHelper.Clamp(alpha + 0.2f, 0f, 1f);

                // Draw with Palette_DarkShadow as requested
                // Using outline for readability over the map
                spriteBatch.DrawStringOutlinedSnapped(font, log.Text, pos, _global.Palette_DarkShadow * alpha, _global.Palette_Black * alpha);
            }
        }
    }
}
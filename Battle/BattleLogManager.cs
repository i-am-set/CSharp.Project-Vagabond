using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Utils;
using System.Collections.Generic;
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
        private const int MAX_LOGS = 25;
        private readonly Global _global;
        private readonly Core _core;

        // --- UI State ---
        private float _slideProgress = 0f; // 0.0 = Closed, 1.0 = Open
        private const float SLIDE_SPEED = 10f;

        // Increased from 6 to 7 to extend 1 pixel lower
        private const int TAB_HEIGHT = 7;

        // Layout
        private Rectangle _tabBounds;

        public BattleLogManager()
        {
            _global = ServiceLocator.Get<Global>();
            _core = ServiceLocator.Get<Core>();

            // Full width tab anchored at (0,0)
            _tabBounds = new Rectangle(0, 0, Global.VIRTUAL_WIDTH, TAB_HEIGHT);
        }

        public void Reset()
        {
            _logs.Clear();
            _slideProgress = 0f;
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

        private void AddLog(string text, Color color)
        {
            _logs.Insert(0, new LogEntry { Text = text.ToUpper(), Color = color });
            if (_logs.Count > MAX_LOGS) _logs.RemoveAt(_logs.Count - 1);
        }

        // --- Event Handlers ---
        private void OnActionDeclared(GameEvents.ActionDeclared e) { }
        private void OnActionExecuted(GameEvents.BattleActionExecuted e)
        {
            string moveName = e.ChosenMove.MoveName.ToUpper();
            string actorName = e.Actor.Name.ToUpper();
            for (int i = 0; i < e.Targets.Count; i++)
            {
                var target = e.Targets[i];
                var result = e.DamageResults[i];
                string verb = result.WasGraze ? "GRAZED" : (result.WasCritical ? "CRITICALLY HIT" : (result.WasProtected ? "WAS BLOCKED BY" : "HIT"));
                AddLog($"{actorName} {verb} {target.Name.ToUpper()} WITH {moveName}", _global.Palette_DarkestPale);
            }
        }
        private void OnCombatantDefeated(GameEvents.CombatantDefeated e) => AddLog($"{e.DefeatedCombatant.Name} WAS DEFEATED", _global.Palette_DarkRust);
        private void OnActionFailed(GameEvents.ActionFailed e) => AddLog($"{e.Actor.Name.ToUpper()} FAILED {(e.MoveName ?? "ACTION").ToUpper()}", _global.Palette_DarkShadow);
        private void OnCombatantHealed(GameEvents.CombatantHealed e) => AddLog($"{e.Target.Name} RECOVERED {e.HealAmount} HP", _global.Palette_DarkShadow);
        private void OnStatusEffectTriggered(GameEvents.StatusEffectTriggered e) { if (e.Damage > 0) AddLog($"{e.Combatant.Name} TOOK {e.Damage} {e.EffectType.ToString().ToUpper()} DMG", _global.Palette_DarkShadow); }
        private void OnCombatantManaRestored(GameEvents.CombatantManaRestored e) => AddLog($"{e.Target.Name} RESTORED {e.AmountRestored} MANA", _global.Palette_DarkShadow);
        private void OnCombatantRecoiled(GameEvents.CombatantRecoiled e) => AddLog($"{e.Actor.Name} TOOK {e.RecoilDamage} RECOIL DAMAGE", _global.Palette_DarkShadow);
        private void OnNextEnemyApproaches(GameEvents.NextEnemyApproaches e) => AddLog("ANOTHER ENEMY APPROACHES", _global.Palette_DarkestPale);

        public void Update(GameTime gameTime)
        {
            var mousePos = Core.TransformMouse(Mouse.GetState().Position);
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Check if mouse is hovering the tab
            bool isHoveringTab = _tabBounds.Contains(mousePos);

            float target = isHoveringTab ? 1.0f : 0.0f;
            _slideProgress = MathHelper.Lerp(_slideProgress, target, dt * SLIDE_SPEED);
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            var pixel = ServiceLocator.Get<Texture2D>();
            var font = _core.TertiaryFont;
            var spriteManager = ServiceLocator.Get<SpriteManager>();

            // Calculate dynamic height
            float maxExpansion = Global.VIRTUAL_HEIGHT - TAB_HEIGHT;
            float currentExpansion = maxExpansion * _slideProgress;
            float totalHeight = TAB_HEIGHT + currentExpansion;

            // Calculate Fade Alpha
            // We multiply by 5 so it becomes fully opaque quickly (at 20% open)
            // This prevents it from looking like a faint ghost while sliding
            float fadeAlpha = Math.Clamp(_slideProgress * 5f, 0f, 1f);

            // 1. Draw Background (Unified Rectangle)
            Rectangle bgRect = new Rectangle(0, 0, Global.VIRTUAL_WIDTH, (int)totalHeight);

            // Apply fadeAlpha to the 95% opacity
            if (fadeAlpha > 0f)
            {
                spriteBatch.DrawSnapped(pixel, bgRect, _global.Palette_Black * 0.95f * fadeAlpha);
            }

            // 2. Draw Bottom Border Line
            // Apply fadeAlpha so it hides when closed
            if (fadeAlpha > 0f)
            {
                spriteBatch.DrawSnapped(pixel, new Rectangle(0, bgRect.Bottom - 1, Global.VIRTUAL_WIDTH, 1), _global.Palette_DarkShadow * fadeAlpha);
            }

            // 3. Draw Arrow Sprite (Centered in the Tab area)
            // The arrow remains visible (opaque) so the user knows where to hover
            var arrow = spriteManager.DownArrowSprite;
            if (arrow != null)
            {
                Vector2 arrowPos = new Vector2(
                    _tabBounds.Center.X - (arrow.Width / 2f),
                    _tabBounds.Center.Y - (arrow.Height / 2f)
                );
                spriteBatch.DrawSnapped(arrow, arrowPos, _global.Palette_DarkShadow);
            }

            // 4. Draw Text (Only if expanded enough to see)
            if (_slideProgress > 0.01f)
            {
                int startY = TAB_HEIGHT + 4;
                int spacing = 3;
                int lineHeight = font.LineHeight + spacing;

                for (int i = 0; i < _logs.Count; i++)
                {
                    float y = startY + (i * lineHeight);

                    // Stop drawing if we hit the bottom line
                    if (y + lineHeight > bgRect.Bottom) break;

                    string text = _logs[i].Text;
                    // Fade text in with the menu
                    Color color = _logs[i].Color * fadeAlpha;
                    string number = $"{i + 1}.";

                    Vector2 numSize = font.MeasureString(number);
                    Vector2 textSize = font.MeasureString(text);
                    float gap = 4f;
                    float totalWidth = numSize.X + gap + textSize.X;

                    float startX = (Global.VIRTUAL_WIDTH - totalWidth) / 2f;

                    spriteBatch.DrawStringSnapped(font, number, new Vector2(startX, y), _global.Palette_DarkShadow * fadeAlpha);
                    spriteBatch.DrawStringSnapped(font, text, new Vector2(startX + numSize.X + gap, y), color);
                }
            }
        }
    }
}
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Utils;
using System.Collections.Generic;
using System;
using System.Linq; // Added for List manipulation

namespace ProjectVagabond.Battle.UI
{
    public class BattleLogManager
    {
        private struct LogEntry
        {
            public string Text;
            public Color Color;
            public int RoundNumber;
            public int RoundIndex;
        }

        private readonly List<LogEntry> _logs = new List<LogEntry>();
        private const int MAX_LOGS = 25;
        private readonly Global _global;
        private readonly Core _core;

        // --- UI State ---
        private float _slideProgress = 0f;
        private const float SLIDE_SPEED = 10f;
        private const int TAB_HEIGHT = 7;

        // Round Tracking
        private int _lastSeenRound = 0;
        private int _currentRoundLogIndex = 0;

        // Layout
        private Rectangle _tabBounds;

        public BattleLogManager()
        {
            _global = ServiceLocator.Get<Global>();
            _core = ServiceLocator.Get<Core>();
            _tabBounds = new Rectangle(0, 0, Global.VIRTUAL_WIDTH, TAB_HEIGHT);
        }

        public void Reset()
        {
            _logs.Clear();
            _slideProgress = 0f;
            _lastSeenRound = 0;
            _currentRoundLogIndex = 0;
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
            EventBus.Unsubscribe<GameEvents.CombatantRecoiled>(OnCombatantRecoiled);
            EventBus.Unsubscribe<GameEvents.NextEnemyApproaches>(OnNextEnemyApproaches);
        }

        private void AddLog(string text, Color color)
        {
            var battleManager = ServiceLocator.Get<BattleManager>();
            int currentRound = battleManager != null ? battleManager.RoundNumber : 1;

            if (currentRound > _lastSeenRound)
            {
                _currentRoundLogIndex = 0;
                _lastSeenRound = currentRound;
            }

            _currentRoundLogIndex++;

            _logs.Insert(0, new LogEntry
            {
                Text = text.ToUpper(),
                Color = color,
                RoundNumber = currentRound,
                RoundIndex = _currentRoundLogIndex
            });

            if (_logs.Count > MAX_LOGS) _logs.RemoveAt(_logs.Count - 1);
        }

        // --- Event Handlers ---
        private void OnActionDeclared(GameEvents.ActionDeclared e) { }

        private void OnActionExecuted(GameEvents.BattleActionExecuted e)
        {
            string moveName = e.ChosenMove.MoveName.ToUpper();
            string actorName = e.Actor.Name.ToUpper();

            // 1. Group targets by the specific outcome verb
            // This ensures we don't say "HIT WOLF AND TOAD" if Wolf was HIT but Toad was CRITICALLY HIT.
            var outcomeGroups = new Dictionary<string, List<string>>();

            for (int i = 0; i < e.Targets.Count; i++)
            {
                var target = e.Targets[i];
                var result = e.DamageResults[i];

                string verb = result.WasGraze ? "GRAZED" :
                              (result.WasCritical ? "CRITICALLY HIT" :
                              (result.WasProtected ? "WAS BLOCKED BY" : "HIT"));

                if (!outcomeGroups.ContainsKey(verb))
                {
                    outcomeGroups[verb] = new List<string>();
                }
                outcomeGroups[verb].Add(target.Name.ToUpper());
            }

            // 2. Generate a log line for each group
            foreach (var kvp in outcomeGroups)
            {
                string verb = kvp.Key;
                List<string> names = kvp.Value;
                string targetString = FormatNameList(names);

                AddLog($"{actorName} {verb} {targetString} WITH {moveName}", _global.Palette_Pale);
            }
        }

        private string FormatNameList(List<string> names)
        {
            if (names.Count == 0) return "";
            if (names.Count == 1) return names[0];
            if (names.Count == 2) return $"{names[0]} AND {names[1]}";

            // Oxford comma style for 3+: "A, B, AND C"
            string joined = string.Join(", ", names.Take(names.Count - 1));
            return $"{joined}, AND {names.Last()}";
        }

        private void OnCombatantDefeated(GameEvents.CombatantDefeated e) => AddLog($"{e.DefeatedCombatant.Name} WAS DEFEATED", _global.Palette_DarkRust);
        private void OnActionFailed(GameEvents.ActionFailed e) => AddLog($"{e.Actor.Name.ToUpper()} FAILED {(e.MoveName ?? "ACTION").ToUpper()}", _global.Palette_Rust);
        private void OnCombatantHealed(GameEvents.CombatantHealed e) => AddLog($"{e.Target.Name} RECOVERED {e.HealAmount} HP", _global.Palette_Leaf);
        private void OnStatusEffectTriggered(GameEvents.StatusEffectTriggered e) { if (e.Damage > 0) AddLog($"{e.Combatant.Name} TOOK {e.Damage} {e.EffectType.ToString().ToUpper()} DMG", _global.Palette_Shadow); }
        private void OnCombatantRecoiled(GameEvents.CombatantRecoiled e) => AddLog($"{e.Actor.Name} TOOK {e.RecoilDamage} RECOIL DAMAGE", _global.Palette_Fruit);
        private void OnNextEnemyApproaches(GameEvents.NextEnemyApproaches e) => AddLog("ANOTHER ENEMY APPROACHES", _global.Palette_LightPale);

        public void Update(GameTime gameTime)
        {
            var mousePos = Core.TransformMouse(Mouse.GetState().Position);
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            bool isHoveringTab = _tabBounds.Contains(mousePos);
            float target = isHoveringTab ? 1.0f : 0.0f;
            _slideProgress = MathHelper.Lerp(_slideProgress, target, dt * SLIDE_SPEED);
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            var pixel = ServiceLocator.Get<Texture2D>();
            var font = _core.TertiaryFont;
            var spriteManager = ServiceLocator.Get<SpriteManager>();

            float maxExpansion = Global.VIRTUAL_HEIGHT - TAB_HEIGHT;
            float currentExpansion = maxExpansion * _slideProgress;
            float totalHeight = TAB_HEIGHT + currentExpansion;
            float fadeAlpha = Math.Clamp(_slideProgress * 5f, 0f, 1f);

            Rectangle bgRect = new Rectangle(0, 0, Global.VIRTUAL_WIDTH, (int)totalHeight);

            if (fadeAlpha > 0f)
            {
                spriteBatch.DrawSnapped(pixel, bgRect, _global.Palette_Black * 0.75f * fadeAlpha);
                spriteBatch.DrawSnapped(pixel, new Rectangle(0, bgRect.Bottom - 1, Global.VIRTUAL_WIDTH, 1), _global.Palette_DarkShadow * fadeAlpha);
            }

            var arrow = spriteManager.DownArrowSprite;
            if (arrow != null)
            {
                Vector2 arrowPos = new Vector2(
                    _tabBounds.Center.X - (arrow.Width / 2f),
                    _tabBounds.Center.Y - (arrow.Height / 2f)
                );
                spriteBatch.DrawSnapped(arrow, arrowPos, _global.Palette_DarkestPale);
            }

            if (_slideProgress > 0.01f)
            {
                int startY = TAB_HEIGHT + 4;
                int spacing = 3;
                int lineHeight = font.LineHeight + spacing;
                float currentY = startY;

                float logStartX = 100f;
                float gap = 4f;

                for (int i = 0; i < _logs.Count; i++)
                {
                    if (currentY + lineHeight > bgRect.Bottom) break;

                    var entry = _logs[i];
                    string text = entry.Text;
                    Color color = entry.Color * fadeAlpha;

                    string number = $"{entry.RoundIndex}.";

                    string roundHeader = "";
                    if (entry.RoundIndex == 1)
                    {
                        roundHeader = $"ROUND {entry.RoundNumber}";
                    }

                    Vector2 textPos = new Vector2(logStartX, currentY);

                    Vector2 numSize = font.MeasureString(number);
                    float numberX = logStartX - gap - numSize.X;
                    Vector2 numberPos = new Vector2(numberX, currentY);

                    if (!string.IsNullOrEmpty(roundHeader))
                    {
                        Vector2 headerSize = font.MeasureString(roundHeader);
                        float headerX = numberX - gap - headerSize.X;
                        spriteBatch.DrawStringSnapped(font, roundHeader, new Vector2(headerX, currentY), _global.Palette_Sky * fadeAlpha);
                    }

                    spriteBatch.DrawStringSnapped(font, number, numberPos, _global.Palette_DarkPale * fadeAlpha);
                    spriteBatch.DrawStringSnapped(font, text, textPos, color);

                    currentY += lineHeight;

                    if (i < _logs.Count - 1)
                    {
                        if (_logs[i + 1].RoundNumber != entry.RoundNumber)
                        {
                            currentY += 2;
                        }
                    }
                }
            }
        }
    }
}
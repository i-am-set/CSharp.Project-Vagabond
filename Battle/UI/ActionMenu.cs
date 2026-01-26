using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

namespace ProjectVagabond.Battle.UI
{
    public class ActionMenu
    {
        // Events
        public event Action<int, QueuedAction>? OnActionSelected;
        public event Action<int>? OnSwitchMenuRequested;
        public event Action<int>? OnCancelRequested; // New: Request to unlock a slot
        public event Action? OnFleeRequested;

        // State
        private bool _isVisible;
        public bool IsVisible => _isVisible;

        private List<CombatantPanel> _panels = new List<CombatantPanel>();

        // Tooltip / Hover State
        public MoveData? HoveredMove { get; private set; }
        public int HoveredSlotIndex { get; private set; } = -1;
        public MoveEntry? SelectedSpellbookEntry { get; private set; }

        // Layout
        private const int PANEL_WIDTH = 80;
        private const int PANEL_Y = 130;

        public ActionMenu() { }

        public void Reset()
        {
            _isVisible = false;
            _panels.Clear();
            HoveredMove = null;
            HoveredSlotIndex = -1;
        }

        public void Show(BattleCombatant anyPlayer, List<BattleCombatant> allCombatants)
        {
            _panels.Clear();
            var party = allCombatants.Where(c => c.IsPlayerControlled && c.IsActiveOnField).OrderBy(c => c.BattleSlot).ToList();

            foreach (var member in party)
            {
                if (member.IsDefeated) continue;
                var panel = new CombatantPanel(member, allCombatants);
                panel.OnActionSelected += (action) => OnActionSelected?.Invoke(member.BattleSlot, action);
                panel.OnSwitchRequested += () => OnSwitchMenuRequested?.Invoke(member.BattleSlot);
                panel.OnCancelRequested += () => OnCancelRequested?.Invoke(member.BattleSlot);
                _panels.Add(panel);
            }

            UpdateLayout();
            _isVisible = true;
        }

        public void Hide() => _isVisible = false;

        public void GoBack()
        {
            // Not used in parallel mode usually
        }

        private void UpdateLayout()
        {
            int screenWidth = Global.VIRTUAL_WIDTH;
            foreach (var panel in _panels)
            {
                int x = (panel.SlotIndex == 0) ? 10 : (screenWidth - PANEL_WIDTH - 10);
                panel.SetPosition(new Vector2(x, PANEL_Y));
            }
        }

        public void Update(MouseState mouse, GameTime gameTime, bool isInputBlocked)
        {
            if (!_isVisible) return;

            HoveredMove = null;
            HoveredSlotIndex = -1;

            var battleManager = ServiceLocator.Get<BattleManager>();

            foreach (var panel in _panels)
            {
                bool isLocked = battleManager.IsActionPending(panel.SlotIndex);
                panel.Update(mouse, gameTime, isInputBlocked, isLocked);

                if (panel.HoveredMove != null)
                {
                    HoveredMove = panel.HoveredMove;
                    HoveredSlotIndex = panel.SlotIndex;
                }
            }
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform, Vector2 offset)
        {
            if (!_isVisible) return;
            foreach (var panel in _panels)
            {
                panel.Draw(spriteBatch, font, gameTime, transform, offset);
            }
        }

        // --- Nested Panel Class ---
        private class CombatantPanel
        {
            public int SlotIndex { get; }
            public BattleCombatant Combatant { get; }
            public MoveData? HoveredMove { get; private set; }

            public event Action<QueuedAction>? OnActionSelected;
            public event Action? OnSwitchRequested;
            public event Action? OnCancelRequested;

            private readonly List<Button> _buttons = new List<Button>();
            private Vector2 _position;
            private Rectangle _panelBounds;

            // Layout
            private const int MOVE_BTN_HEIGHT = 6;
            private const int ACTION_BTN_HEIGHT = 6;
            private const int SPACING = 1;

            public CombatantPanel(BattleCombatant combatant, List<BattleCombatant> allCombatants)
            {
                Combatant = combatant;
                SlotIndex = combatant.BattleSlot;
                InitializeButtons(allCombatants);
            }

            public void SetPosition(Vector2 pos)
            {
                _position = pos;
                LayoutButtons();
            }

            private void InitializeButtons(List<BattleCombatant> allCombatants)
            {
                var tertiaryFont = ServiceLocator.Get<Core>().TertiaryFont;
                var global = ServiceLocator.Get<Global>();

                // 1. Spells
                for (int i = 0; i < 4; i++)
                {
                    var entry = Combatant.Spells[i];
                    string label = "---";
                    bool enabled = false;
                    MoveData? moveData = null;

                    if (entry != null && BattleDataCache.Moves.TryGetValue(entry.MoveID, out var move))
                    {
                        label = move.MoveName.ToUpper();
                        enabled = true;
                        moveData = move;
                    }

                    var btn = new Button(Rectangle.Empty, label, font: tertiaryFont, enableHoverSway: false)
                    {
                        CustomDefaultTextColor = enabled ? global.Palette_Sun : global.Palette_DarkShadow,
                        CustomHoverTextColor = global.ButtonHoverColor,
                        IsEnabled = enabled
                    };

                    var capturedMove = moveData;
                    var capturedEntry = entry;

                    btn.OnClick += () =>
                    {
                        if (capturedMove != null)
                        {
                            var action = new QueuedAction
                            {
                                Actor = Combatant,
                                ChosenMove = capturedMove,
                                SpellbookEntry = capturedEntry,
                                Type = QueuedActionType.Move,
                                Priority = capturedMove.Priority,
                                ActorAgility = Combatant.GetEffectiveAgility()
                            };
                            OnActionSelected?.Invoke(action);
                        }
                    };
                    _buttons.Add(btn);
                }

                // 2. Actions
                var strikeBtn = new Button(Rectangle.Empty, "STRIKE", font: tertiaryFont, enableHoverSway: false);
                strikeBtn.OnClick += () =>
                {
                    var strikeMove = Combatant.StrikeMove;
                    if (strikeMove != null)
                    {
                        var action = new QueuedAction
                        {
                            Actor = Combatant,
                            ChosenMove = strikeMove,
                            Type = QueuedActionType.Move,
                            Priority = strikeMove.Priority,
                            ActorAgility = Combatant.GetEffectiveAgility()
                        };
                        OnActionSelected?.Invoke(action);
                    }
                };
                _buttons.Add(strikeBtn);

                var stallBtn = new Button(Rectangle.Empty, "STALL", font: tertiaryFont, enableHoverSway: false);
                stallBtn.OnClick += () =>
                {
                    if (BattleDataCache.Moves.TryGetValue("6", out var stallMove))
                    {
                        var action = new QueuedAction
                        {
                            Actor = Combatant,
                            ChosenMove = stallMove,
                            Target = Combatant,
                            Type = QueuedActionType.Move,
                            Priority = 0,
                            ActorAgility = Combatant.GetEffectiveAgility()
                        };
                        OnActionSelected?.Invoke(action);
                    }
                };
                _buttons.Add(stallBtn);

                var switchBtn = new Button(Rectangle.Empty, "SWITCH", font: tertiaryFont, enableHoverSway: false);
                switchBtn.OnClick += () => OnSwitchRequested?.Invoke();
                bool hasBench = allCombatants.Any(c => c.IsPlayerControlled && !c.IsDefeated && c.BattleSlot >= 2);
                switchBtn.IsEnabled = hasBench;
                _buttons.Add(switchBtn);
            }

            private void LayoutButtons()
            {
                int x = (int)_position.X;
                int y = (int)_position.Y;
                int startY = y;

                for (int i = 0; i < 4; i++)
                {
                    _buttons[i].Bounds = new Rectangle(x, y, PANEL_WIDTH, MOVE_BTN_HEIGHT);
                    y += MOVE_BTN_HEIGHT + SPACING;
                }

                int strikeW = 30;
                int stallW = 25;
                int switchW = 25;

                _buttons[4].Bounds = new Rectangle(x, y, strikeW, ACTION_BTN_HEIGHT);
                _buttons[5].Bounds = new Rectangle(x + strikeW, y, stallW, ACTION_BTN_HEIGHT);
                _buttons[6].Bounds = new Rectangle(x + strikeW + stallW, y, switchW, ACTION_BTN_HEIGHT);

                int totalHeight = (y + ACTION_BTN_HEIGHT) - startY;
                _panelBounds = new Rectangle(x, startY, PANEL_WIDTH, totalHeight);
            }

            public void Update(MouseState mouse, GameTime gameTime, bool isInputBlocked, bool isLocked)
            {
                HoveredMove = null;

                // Handle Right-Click Cancellation
                if (isLocked && !isInputBlocked)
                {
                    if (mouse.RightButton == ButtonState.Pressed && _panelBounds.Contains(mouse.Position))
                    {
                        // We need to detect just-pressed, but we don't have previous state here easily.
                        // However, BattleUIManager calls this every frame.
                        // Let's rely on the fact that if it's pressed, we request cancel.
                        // The manager can debounce if needed, or we can just fire it.
                        // Actually, firing every frame while held is bad.
                        // But since cancelling unlocks it, `isLocked` becomes false immediately next frame.
                        OnCancelRequested?.Invoke();
                    }
                }

                foreach (var btn in _buttons)
                {
                    if (isInputBlocked)
                    {
                        btn.IsHovered = false;
                        continue;
                    }

                    btn.Update(mouse);

                    if (btn.IsHovered && btn.IsEnabled)
                    {
                        int index = _buttons.IndexOf(btn);
                        if (index < 4) // Spell
                        {
                            var entry = Combatant.Spells[index];
                            if (entry != null && BattleDataCache.Moves.TryGetValue(entry.MoveID, out var m))
                                HoveredMove = m;
                        }
                        else if (index == 4) // Strike
                        {
                            HoveredMove = Combatant.StrikeMove;
                        }
                        else if (index == 5) // Stall
                        {
                            if (BattleDataCache.Moves.TryGetValue("6", out var m)) HoveredMove = m;
                        }
                    }
                }
            }

            public void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform, Vector2 offset)
            {
                var pixel = ServiceLocator.Get<Texture2D>();
                var global = ServiceLocator.Get<Global>();
                var battleManager = ServiceLocator.Get<BattleManager>();
                bool isLocked = battleManager.IsActionPending(SlotIndex);

                foreach (var btn in _buttons)
                {
                    var rect = btn.Bounds;
                    rect.Y += (int)offset.Y;

                    Color bgColor = global.Palette_DarkShadow;
                    if (isLocked) bgColor = global.Palette_DarkGray;
                    if (btn.IsHovered && btn.IsEnabled) bgColor = global.Palette_Rust;
                    else if (!btn.IsEnabled) bgColor = global.Palette_Black;

                    spriteBatch.DrawSnapped(pixel, rect, bgColor);

                    var textSize = btn.Font.MeasureString(btn.Text);
                    var textPos = new Vector2(
                        rect.X + (rect.Width - textSize.Width) / 2f,
                        rect.Y + (rect.Height - textSize.Height) / 2f
                    );
                    textPos = new Vector2(MathF.Round(textPos.X), MathF.Round(textPos.Y));

                    Color textColor = btn.CustomDefaultTextColor ?? global.Palette_Sun;
                    if (isLocked) textColor = global.Palette_LightGray;
                    if (btn.IsHovered && btn.IsEnabled) textColor = global.ButtonHoverColor;
                    if (!btn.IsEnabled) textColor = global.ButtonDisableColor;

                    spriteBatch.DrawStringSnapped(btn.Font, btn.Text, textPos, textColor);
                }

                if (isLocked)
                {
                    var checkRect = new Rectangle((int)_position.X + PANEL_WIDTH - 8, (int)_position.Y - 8, 8, 8);
                    spriteBatch.DrawSnapped(pixel, checkRect, global.Palette_Leaf);
                }
            }
        }
    }
}
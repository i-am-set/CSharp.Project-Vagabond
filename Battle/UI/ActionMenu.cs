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
        public event Action<int>? OnCancelRequested;
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

        public void GoBack() { }

        private void UpdateLayout()
        {
            foreach (var panel in _panels)
            {
                // Get the designated area for this slot (Left or Right)
                var area = BattleLayout.GetActionMenuArea(panel.SlotIndex);

                // Calculate the panel's total height to center it vertically
                // 4 Moves * 7px (6+1) + 1 Action Row * 7px (6+1) = 35px total height
                int panelHeight = 35;

                // Center the panel within the area
                int x = area.Center.X - (PANEL_WIDTH / 2);
                int y = area.Center.Y - (panelHeight / 2);

                panel.SetPosition(new Vector2(x, y));
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

        public void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform, Vector2 offset, int? hiddenSlotIndex = null)
        {
            if (!_isVisible) return;
            foreach (var panel in _panels)
            {
                if (hiddenSlotIndex.HasValue && panel.SlotIndex == hiddenSlotIndex.Value)
                {
                    continue;
                }
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
            // We add 1px to the hitbox height to create the seamless gap
            private const int HITBOX_PADDING = 1;

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

                // Layout Moves (Vertical Stack)
                for (int i = 0; i < 4; i++)
                {
                    // Hitbox is 1px taller than visual height to bridge the gap
                    _buttons[i].Bounds = new Rectangle(x, y, PANEL_WIDTH, MOVE_BTN_HEIGHT + HITBOX_PADDING);
                    y += MOVE_BTN_HEIGHT + HITBOX_PADDING;
                }

                // Layout Actions (Horizontal Row)
                int strikeW = 30;
                int stallW = 25;
                int switchW = 25;

                // Hitbox is 1px taller here too for consistency
                _buttons[4].Bounds = new Rectangle(x, y, strikeW, ACTION_BTN_HEIGHT + HITBOX_PADDING);
                _buttons[5].Bounds = new Rectangle(x + strikeW, y, stallW, ACTION_BTN_HEIGHT + HITBOX_PADDING);
                _buttons[6].Bounds = new Rectangle(x + strikeW + stallW, y, switchW, ACTION_BTN_HEIGHT + HITBOX_PADDING);

                int totalHeight = (y + ACTION_BTN_HEIGHT + HITBOX_PADDING) - startY;
                _panelBounds = new Rectangle(x, startY, PANEL_WIDTH, totalHeight);
            }

            public void Update(MouseState mouse, GameTime gameTime, bool isInputBlocked, bool isLocked)
            {
                HoveredMove = null;

                if (isLocked && !isInputBlocked)
                {
                    if (mouse.RightButton == ButtonState.Pressed && _panelBounds.Contains(mouse.Position))
                    {
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

                    // VISUAL ADJUSTMENT:
                    // The hitbox is 1px taller than the visual button.
                    // We subtract 1 from the height to draw the background, creating the visual gap.
                    var visualRect = new Rectangle(rect.X, rect.Y, rect.Width, rect.Height - HITBOX_PADDING);

                    Color bgColor = global.Palette_DarkShadow;
                    if (isLocked) bgColor = global.Palette_DarkGray;
                    if (btn.IsHovered && btn.IsEnabled) bgColor = global.Palette_Rust;
                    else if (!btn.IsEnabled) bgColor = global.Palette_Black;

                    // Draw Beveled Background using the smaller visual rect
                    DrawBeveledBackground(spriteBatch, pixel, visualRect, bgColor);

                    var textSize = btn.Font.MeasureString(btn.Text);

                    // Center text within the VISUAL rect, not the hitbox
                    var textPos = new Vector2(
                        visualRect.X + (visualRect.Width - textSize.Width) / 2f,
                        visualRect.Y + (visualRect.Height - textSize.Height) / 2f
                    );
                    textPos = new Vector2(MathF.Round(textPos.X), MathF.Round(textPos.Y));

                    Color textColor = btn.CustomDefaultTextColor ?? global.Palette_Sun;
                    if (isLocked) textColor = global.Palette_LightGray;
                    if (btn.IsHovered && btn.IsEnabled) textColor = global.ButtonHoverColor;
                    if (!btn.IsEnabled) textColor = global.ButtonDisableColor;

                    spriteBatch.DrawStringSnapped(btn.Font, btn.Text, textPos, textColor);

                    // --- DEBUG DRAWING ---
                    if (global.ShowSplitMapGrid)
                    {
                        // Draw the full hitbox in yellow to verify seamlessness
                        spriteBatch.DrawSnapped(pixel, rect, Color.Yellow * 0.2f);
                        spriteBatch.DrawLineSnapped(new Vector2(rect.Left, rect.Top), new Vector2(rect.Right, rect.Top), Color.Yellow);
                        spriteBatch.DrawLineSnapped(new Vector2(rect.Left, rect.Bottom), new Vector2(rect.Right, rect.Bottom), Color.Yellow);
                        spriteBatch.DrawLineSnapped(new Vector2(rect.Left, rect.Top), new Vector2(rect.Left, rect.Bottom), Color.Yellow);
                        spriteBatch.DrawLineSnapped(new Vector2(rect.Right, rect.Top), new Vector2(rect.Right, rect.Bottom), Color.Yellow);
                    }
                }

                if (isLocked)
                {
                    var checkRect = new Rectangle((int)_position.X + PANEL_WIDTH - 8, (int)_position.Y - 8, 8, 8);
                    spriteBatch.DrawSnapped(pixel, checkRect, global.Palette_Leaf);
                }
            }

            private void DrawBeveledBackground(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color color)
            {
                // 1. Top Row (Y+1): X+2 to W-4
                spriteBatch.DrawSnapped(pixel, new Rectangle(rect.X + 2, rect.Y, rect.Width - 4, 1), color);

                // 2. Bottom Row (Bottom-2): X+2 to W-4
                spriteBatch.DrawSnapped(pixel, new Rectangle(rect.X + 2, rect.Bottom - 1, rect.Width - 4, 1), color);

                // 3. Middle Block (Y+2 to Bottom-3): X+1 to W-2
                spriteBatch.DrawSnapped(pixel, new Rectangle(rect.X + 1, rect.Y + 1, rect.Width - 2, rect.Height - 2), color);
            }
        }
    }
}
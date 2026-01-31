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
        private const int PANEL_WIDTH = 140;

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
                var area = BattleLayout.GetActionMenuArea(panel.SlotIndex);
                // 4 Moves * 10px (9+1) + 1 Action Row * 9px (8+1) = 40 + 9 = 49
                int panelHeight = 49;
                int x = area.Center.X - (PANEL_WIDTH / 2);
                int y = area.Center.Y - (panelHeight / 2);
                panel.SetPosition(new Vector2(x, y));
            }
        }

        public void Update(MouseState mouse, GameTime gameTime, bool isInputBlocked, int? switchingSlotIndex = null)
        {
            if (!_isVisible) return;

            HoveredMove = null;
            HoveredSlotIndex = -1;

            var battleManager = ServiceLocator.Get<BattleManager>();

            foreach (var panel in _panels)
            {
                // If input is globally blocked, just reset hover and continue
                if (isInputBlocked)
                {
                    panel.Update(mouse, gameTime, true, false);
                    continue;
                }

                // If a specific slot is switching
                if (switchingSlotIndex.HasValue)
                {
                    // If this is the switching panel, block it completely
                    if (panel.SlotIndex == switchingSlotIndex.Value)
                    {
                        panel.Update(mouse, gameTime, true, false);
                        continue;
                    }

                    // If this is another panel, update it but disable switch button
                    panel.SetSwitchButtonAllowed(false);
                }
                else
                {
                    // Normal state, ensure switch button is enabled (if valid logic allows)
                    panel.SetSwitchButtonAllowed(true);
                }

                bool isLocked = battleManager.IsActionPending(panel.SlotIndex);
                panel.Update(mouse, gameTime, false, isLocked);

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
            private Button _cancelButton;
            private Vector2 _position;
            private Rectangle _panelBounds;
            private bool _hasBench;

            // Layout
            private const int MOVE_BTN_HEIGHT = 9;
            private const int ACTION_BTN_HEIGHT = 8;
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

            public void SetSwitchButtonAllowed(bool allowed)
            {
                // Button 6 is Switch
                if (_buttons.Count > 6)
                {
                    _buttons[6].IsEnabled = allowed && _hasBench;
                }
            }

            private void InitializeButtons(List<BattleCombatant> allCombatants)
            {
                var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
                var tertiaryFont = ServiceLocator.Get<Core>().TertiaryFont;
                var global = ServiceLocator.Get<Global>();
                var spriteManager = ServiceLocator.Get<SpriteManager>();
                var icons = spriteManager.ActionIconsSpriteSheet;
                var iconRects = spriteManager.ActionIconSourceRects;

                // Helper to get icon rect based on move type
                Rectangle GetMoveIcon(MoveData m)
                {
                    if (m.ImpactType == ImpactType.Physical) return iconRects[2];
                    if (m.ImpactType == ImpactType.Magical) return iconRects[3];
                    return iconRects[4]; // Status
                }

                // 1. Spells
                for (int i = 0; i < 4; i++)
                {
                    var entry = Combatant.Spells[i];
                    string label = "---";
                    bool enabled = false;
                    MoveData? moveData = null;
                    Rectangle? iconRect = null;

                    if (entry != null && BattleDataCache.Moves.TryGetValue(entry.MoveID, out var move))
                    {
                        label = move.MoveName.ToUpper();
                        enabled = true;
                        moveData = move;
                        iconRect = GetMoveIcon(move);
                    }

                    // Use MoveButton for spells to handle mana checks and scrolling
                    var btn = new MoveButton(
                        Combatant, // Pass the owner
                        moveData ?? new MoveData { MoveName = "---" }, // Dummy data if null
                        entry ?? new MoveEntry(),
                        moveData?.Power ?? 0,
                        secondaryFont,
                        null, // No background sprite (ActionMenu draws container)
                        enabled ? icons : null,
                        iconRect,
                        true
                    )
                    {
                        IsEnabled = enabled,
                        // MoveButton handles its own text/icon rendering logic
                    };

                    var capturedMove = moveData;
                    var capturedEntry = entry;

                    btn.OnClick += () =>
                    {
                        if (capturedMove != null)
                        {
                            // Check Mana before firing
                            bool canAfford = false;
                            var manaDump = capturedMove.Abilities.OfType<ManaDumpAbility>().FirstOrDefault();
                            if (manaDump != null)
                            {
                                canAfford = Combatant.Stats.CurrentMana > 0;
                            }
                            else
                            {
                                canAfford = Combatant.Stats.CurrentMana >= capturedMove.ManaCost;
                            }

                            if (canAfford)
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
                            else
                            {
                                ServiceLocator.Get<HapticsManager>().TriggerShake(2f, 0.1f);
                            }
                        }
                    };
                    _buttons.Add(btn);
                }

                // 2. Actions (Using Tertiary Font)
                // Strike
                var strikeMove = Combatant.StrikeMove;
                Rectangle strikeIcon = (strikeMove != null) ? GetMoveIcon(strikeMove) : iconRects[2];
                var strikeBtn = new TextOverImageButton(Rectangle.Empty, "STRIKE", null, font: tertiaryFont, enableHoverSway: false, iconTexture: icons, iconSourceRect: strikeIcon)
                {
                    AlignLeft = false, // Center aligned for bottom row
                    IconColorMatchesText = true,
                    CustomDefaultTextColor = global.GameTextColor
                };
                strikeBtn.OnClick += () =>
                {
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

                // Stall
                var stallBtn = new TextOverImageButton(Rectangle.Empty, "STALL", null, font: tertiaryFont, enableHoverSway: false, iconTexture: icons, iconSourceRect: iconRects[0])
                {
                    AlignLeft = false,
                    IconColorMatchesText = true,
                    CustomDefaultTextColor = global.GameTextColor
                };
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

                // Switch
                var switchBtn = new TextOverImageButton(Rectangle.Empty, "SWITCH", null, font: tertiaryFont, enableHoverSway: false, iconTexture: icons, iconSourceRect: iconRects[1])
                {
                    AlignLeft = false,
                    IconColorMatchesText = true,
                    CustomDefaultTextColor = global.GameTextColor
                };
                switchBtn.OnClick += () => OnSwitchRequested?.Invoke();
                _hasBench = allCombatants.Any(c => c.IsPlayerControlled && !c.IsDefeated && c.BattleSlot >= 2);
                switchBtn.IsEnabled = _hasBench;
                _buttons.Add(switchBtn);

                // 3. Cancel Button
                _cancelButton = new Button(Rectangle.Empty, "CANCEL", font: secondaryFont, enableHoverSway: false)
                {
                    CustomDefaultTextColor = global.Palette_Rust,
                    CustomHoverTextColor = global.ButtonHoverColor
                };
                _cancelButton.OnClick += () => OnCancelRequested?.Invoke();
            }

            private void LayoutButtons()
            {
                int x = (int)_position.X;
                int y = (int)_position.Y;
                int startY = y;

                // Layout Moves
                for (int i = 0; i < 4; i++)
                {
                    _buttons[i].Bounds = new Rectangle(x, y, PANEL_WIDTH, MOVE_BTN_HEIGHT + HITBOX_PADDING);
                    y += MOVE_BTN_HEIGHT + HITBOX_PADDING;
                }

                // Layout Actions (Total Width 140)
                int strikeW = 50;
                int stallW = 45;
                int switchW = 45;

                _buttons[4].Bounds = new Rectangle(x, y, strikeW, ACTION_BTN_HEIGHT + HITBOX_PADDING);
                _buttons[5].Bounds = new Rectangle(x + strikeW, y, stallW, ACTION_BTN_HEIGHT + HITBOX_PADDING);
                _buttons[6].Bounds = new Rectangle(x + strikeW + stallW, y, switchW, ACTION_BTN_HEIGHT + HITBOX_PADDING);

                int totalHeight = (y + ACTION_BTN_HEIGHT + HITBOX_PADDING) - startY;
                _panelBounds = new Rectangle(x, startY, PANEL_WIDTH, totalHeight);

                // Layout Cancel Button (Centered in the slot's designated area)
                var area = BattleLayout.GetActionMenuArea(SlotIndex);
                int cancelW = 50;
                int cancelH = 15;
                _cancelButton.Bounds = new Rectangle(
                    area.Center.X - cancelW / 2,
                    area.Center.Y - cancelH / 2,
                    cancelW,
                    cancelH
                );
            }

            public void Update(MouseState mouse, GameTime gameTime, bool isInputBlocked, bool isLocked)
            {
                HoveredMove = null;

                if (isInputBlocked)
                {
                    foreach (var btn in _buttons) btn.IsHovered = false;
                    _cancelButton.IsHovered = false;
                    return;
                }

                if (isLocked)
                {
                    _cancelButton.Update(mouse);
                }
                else
                {
                    foreach (var btn in _buttons)
                    {
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
            }

            public void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform, Vector2 offset)
            {
                var pixel = ServiceLocator.Get<Texture2D>();
                var global = ServiceLocator.Get<Global>();
                var battleManager = ServiceLocator.Get<BattleManager>();
                bool isLocked = battleManager.IsActionPending(SlotIndex);

                if (isLocked)
                {
                    // Draw Cancel Button
                    var rect = _cancelButton.Bounds;
                    rect.Y += (int)offset.Y;

                    var textSize = _cancelButton.Font.MeasureString(_cancelButton.Text);
                    var textPos = new Vector2(
                        rect.X + (rect.Width - textSize.Width) / 2f,
                        rect.Y + (rect.Height - textSize.Height) / 2f
                    );
                    textPos = new Vector2(MathF.Round(textPos.X), MathF.Round(textPos.Y));

                    Color textColor = _cancelButton.IsHovered ? global.ButtonHoverColor : global.Palette_Sun;
                    spriteBatch.DrawStringSnapped(_cancelButton.Font, _cancelButton.Text, textPos, textColor);
                }
                else
                {
                    // Draw Normal Buttons
                    for (int i = 0; i < _buttons.Count; i++)
                    {
                        var btn = _buttons[i];
                        var rect = btn.Bounds;
                        rect.Y += (int)offset.Y;
                        var visualRect = new Rectangle(rect.X, rect.Y, rect.Width, rect.Height - HITBOX_PADDING);

                        // Check if it's a MoveButton and if it can afford the cost
                        var moveBtn = btn as MoveButton;
                        bool isEffectiveDisabled = !btn.IsEnabled || (moveBtn != null && !moveBtn.CanAfford);

                        Color bgColor;
                        if (isEffectiveDisabled)
                        {
                            bgColor = global.Palette_Black;
                        }
                        else if (btn.IsHovered)
                        {
                            bgColor = global.Palette_Rust;
                        }
                        else
                        {
                            // Moves (0-3) use DarkestPale, Actions (4-6) use DarkShadow
                            bgColor = (i < 4) ? global.Palette_DarkestPale : global.Palette_DarkShadow;
                        }

                        DrawBeveledBackground(spriteBatch, pixel, visualRect, bgColor);

                        // Use TextOverImageButton's Draw to handle icon + text
                        btn.Draw(spriteBatch, btn.Font, gameTime, transform, false, 0f, offset.Y);
                    }
                }

                // Debug Drawing
                if (global.ShowSplitMapGrid)
                {
                    var area = BattleLayout.GetActionMenuArea(SlotIndex);
                    spriteBatch.DrawSnapped(pixel, area, (SlotIndex == 0 ? Color.Cyan : Color.Magenta) * 0.2f);
                    spriteBatch.DrawLineSnapped(new Vector2(area.Right, area.Top), new Vector2(area.Right, area.Bottom), (SlotIndex == 0 ? Color.Cyan : Color.Magenta));
                }
            }

            private void DrawBeveledBackground(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color color)
            {
                spriteBatch.DrawSnapped(pixel, new Rectangle(rect.X + 2, rect.Y, rect.Width - 4, 1), color);
                spriteBatch.DrawSnapped(pixel, new Rectangle(rect.X + 2, rect.Bottom - 1, rect.Width - 4, 1), color);
                spriteBatch.DrawSnapped(pixel, new Rectangle(rect.X + 1, rect.Y + 1, rect.Width - 2, rect.Height - 2), color);
            }
        }
    }
}

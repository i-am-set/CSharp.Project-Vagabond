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
        // 48 * 3 = 144 + 2px spacing * 2 = 148
        private const int PANEL_WIDTH = 148;

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
                // Center the panel within the slot area
                // Panel height is determined by button height (10px)
                int panelHeight = 10;
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
                if (isInputBlocked)
                {
                    panel.Update(mouse, gameTime, true, false);
                    continue;
                }

                if (switchingSlotIndex.HasValue)
                {
                    if (panel.SlotIndex == switchingSlotIndex.Value)
                    {
                        panel.Update(mouse, gameTime, true, false);
                        continue;
                    }
                    panel.SetSwitchButtonAllowed(false);
                }
                else
                {
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
            private bool _hasBench;
            private Rectangle[] _iconRects;

            // Layout
            private const int BUTTON_WIDTH = 48;
            private const int BUTTON_HEIGHT = 10; // Compact height
            private const int BUTTON_SPACING = 2;

            public CombatantPanel(BattleCombatant combatant, List<BattleCombatant> allCombatants)
            {
                Combatant = combatant;
                SlotIndex = combatant.BattleSlot;

                var spriteManager = ServiceLocator.Get<SpriteManager>();
                _iconRects = spriteManager.ActionIconSourceRects;

                InitializeButtons(allCombatants);
            }

            public void SetPosition(Vector2 pos)
            {
                _position = pos;
                LayoutButtons();
            }

            public void SetSwitchButtonAllowed(bool allowed)
            {
                // Button 2 is Switch
                if (_buttons.Count > 2)
                {
                    _buttons[2].IsEnabled = allowed && _hasBench;
                }
            }

            private void InitializeButtons(List<BattleCombatant> allCombatants)
            {
                var tertiaryFont = ServiceLocator.Get<Core>().TertiaryFont;
                var global = ServiceLocator.Get<Global>();
                var spriteManager = ServiceLocator.Get<SpriteManager>();
                var icons = spriteManager.ActionIconsSpriteSheet;

                // 1. Attack Button (Left)
                // Icon: Physical (Index 2)
                AddActionButton("ATTACK", Combatant.AttackMove, icons, _iconRects[2], tertiaryFont, global);

                // 2. Special Button (Middle)
                // Icon: Magical (Index 3)
                AddActionButton("SPECIAL", Combatant.SpecialMove, icons, _iconRects[3], tertiaryFont, global);

                // 3. Switch Button (Right)
                // Icon: Switch (Index 1)
                var switchBtn = new TextOverImageButton(Rectangle.Empty, "SWITCH", null, font: tertiaryFont, enableHoverSway: false, iconTexture: icons, iconSourceRect: _iconRects[1])
                {
                    AlignLeft = true,
                    IconColorMatchesText = true,
                    CustomDefaultTextColor = global.GameTextColor,
                    TextRenderOffset = new Vector2(0, -1),
                    IconRenderOffset = new Vector2(0, -1)
                };
                switchBtn.OnClick += () => OnSwitchRequested?.Invoke();
                _hasBench = allCombatants.Any(c => c.IsPlayerControlled && !c.IsDefeated && c.BattleSlot >= 2);
                switchBtn.IsEnabled = _hasBench;
                _buttons.Add(switchBtn);

                // 4. Cancel Button
                _cancelButton = new Button(Rectangle.Empty, "CANCEL", font: tertiaryFont, enableHoverSway: false)
                {
                    CustomDefaultTextColor = global.Palette_Rust,
                    CustomHoverTextColor = global.ButtonHoverColor
                };
                _cancelButton.OnClick += () => OnCancelRequested?.Invoke();
            }

            private void AddActionButton(string label, MoveEntry? entry, Texture2D icons, Rectangle iconRect, BitmapFont font, Global global)
            {
                bool moveExists = entry != null && BattleDataCache.Moves.ContainsKey(entry.MoveID);

                var btn = new TextOverImageButton(Rectangle.Empty, label, null, font: font, enableHoverSway: false, iconTexture: icons, iconSourceRect: iconRect)
                {
                    AlignLeft = true,
                    IconColorMatchesText = true,
                    CustomDefaultTextColor = moveExists ? global.GameTextColor : global.Palette_DarkShadow,
                    CustomHoverTextColor = global.ButtonHoverColor,
                    TextRenderOffset = new Vector2(0, -1),
                    IconRenderOffset = new Vector2(0, -1),
                    IsEnabled = moveExists
                };

                if (moveExists)
                {
                    var moveData = BattleDataCache.Moves[entry!.MoveID];
                    btn.OnClick += () =>
                    {
                        // Check Affordability
                        bool canAfford = false;
                        var manaDump = moveData.Abilities.OfType<ManaDumpAbility>().FirstOrDefault();
                        if (manaDump != null)
                        {
                            canAfford = Combatant.Stats.CurrentMana > 0;
                        }
                        else
                        {
                            canAfford = Combatant.Stats.CurrentMana >= moveData.ManaCost;
                        }

                        if (canAfford)
                        {
                            var action = new QueuedAction
                            {
                                Actor = Combatant,
                                ChosenMove = moveData,
                                SpellbookEntry = entry,
                                Type = QueuedActionType.Move,
                                Priority = moveData.Priority,
                                ActorAgility = Combatant.GetEffectiveAgility()
                            };
                            OnActionSelected?.Invoke(action);
                        }
                        else
                        {
                            ServiceLocator.Get<HapticsManager>().TriggerShake(2f, 0.1f);
                        }
                    };
                }

                _buttons.Add(btn);
            }

            private void LayoutButtons()
            {
                int x = (int)_position.X;
                int y = (int)_position.Y;

                // Horizontal Layout: Attack | Special | Switch
                for (int i = 0; i < 3; i++)
                {
                    _buttons[i].Bounds = new Rectangle(x, y, BUTTON_WIDTH, BUTTON_HEIGHT);
                    x += BUTTON_WIDTH + BUTTON_SPACING;
                }

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
                            if (index == 0) // Attack
                            {
                                var entry = Combatant.AttackMove;
                                if (entry != null && BattleDataCache.Moves.TryGetValue(entry.MoveID, out var m))
                                    HoveredMove = m;
                            }
                            else if (index == 1) // Special
                            {
                                var entry = Combatant.SpecialMove;
                                if (entry != null && BattleDataCache.Moves.TryGetValue(entry.MoveID, out var m))
                                    HoveredMove = m;
                            }
                            // Index 2 is Switch, no move data
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
                    var rect = _cancelButton.Bounds;
                    int snappedOffsetY = (int)offset.Y;
                    rect.Y += snappedOffsetY;

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
                    for (int i = 0; i < _buttons.Count; i++)
                    {
                        var btn = _buttons[i];
                        var rect = btn.Bounds;
                        int snappedOffsetY = (int)offset.Y;
                        rect.Y += snappedOffsetY;

                        // Visual rect is the full button bounds minus 1px height for bevel effect
                        var visualRect = new Rectangle(rect.X, rect.Y, rect.Width, rect.Height - 1);

                        Color bgColor;
                        bool canAfford = true;

                        // Check affordability for Attack (0) and Special (1)
                        if (i < 2)
                        {
                            MoveEntry? entry = (i == 0) ? Combatant.AttackMove : Combatant.SpecialMove;
                            if (entry != null && BattleDataCache.Moves.TryGetValue(entry.MoveID, out var moveData))
                            {
                                var manaDump = moveData.Abilities.OfType<ManaDumpAbility>().FirstOrDefault();
                                if (manaDump != null) canAfford = Combatant.Stats.CurrentMana > 0;
                                else canAfford = Combatant.Stats.CurrentMana >= moveData.ManaCost;
                            }
                        }

                        if (!btn.IsEnabled || !canAfford)
                        {
                            bgColor = global.Palette_Black;
                        }
                        else if (btn.IsHovered)
                        {
                            bgColor = global.Palette_Rust;
                        }
                        else
                        {
                            bgColor = global.Palette_DarkestPale;
                        }

                        DrawBeveledBackground(spriteBatch, pixel, visualRect, bgColor);

                        // Pass tint override if disabled/cant afford to gray out text/icon
                        Color? tint = (!btn.IsEnabled || !canAfford) ? global.Palette_DarkShadow : (Color?)null;
                        btn.Draw(spriteBatch, btn.Font, gameTime, transform, false, 0f, snappedOffsetY, tint);
                    }
                }

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
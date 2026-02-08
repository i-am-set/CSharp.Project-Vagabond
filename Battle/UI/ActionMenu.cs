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
        public MoveEntry? SelectedMoveEntry { get; private set; }

        // Layout
        private const int PANEL_WIDTH = 146;

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
            var battleManager = ServiceLocator.Get<BattleManager>();
            var activePlayers = battleManager.AllCombatants.Where(c => c.IsPlayerControlled && c.IsActiveOnField).ToList();
            bool isCentered = activePlayers.Count == 1;

            foreach (var panel in _panels)
            {
                var area = BattleLayout.GetActionMenuArea(panel.SlotIndex, isCentered);
                int x = area.Center.X - (PANEL_WIDTH / 2);
                int y = area.Y;
                panel.SetPosition(new Vector2(x, y));
            }
        }

        public void UpdatePositions(BattleRenderer renderer)
        {
            var battleManager = ServiceLocator.Get<BattleManager>();
            var activePlayers = battleManager.AllCombatants.Where(c => c.IsPlayerControlled && c.IsActiveOnField).ToList();
            bool isCentered = activePlayers.Count == 1;

            foreach (var panel in _panels)
            {
                float visualX = renderer.GetCombatantVisualX(panel.Combatant);
                float x = visualX - (PANEL_WIDTH / 2f);
                float y = BattleLayout.GetActionMenuArea(panel.SlotIndex, isCentered).Y;
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
            private readonly MoveTooltipRenderer _tooltipRenderer;

            // --- Button Layout Configuration ---
            private const int BUTTON_WIDTH = 45;
            private const int BUTTON_HEIGHT = 9;
            private const int BUTTON_SPACING = 1;

            // --- Info Box Configuration ---
            private const int INFO_BOX_OFFSET_Y = 12; // Below the buttons

            public CombatantPanel(BattleCombatant combatant, List<BattleCombatant> allCombatants)
            {
                Combatant = combatant;
                SlotIndex = combatant.BattleSlot;

                var spriteManager = ServiceLocator.Get<SpriteManager>();
                _iconRects = spriteManager.ActionIconSourceRects;
                _tooltipRenderer = new MoveTooltipRenderer();

                InitializeButtons(allCombatants);
            }

            public void SetPosition(Vector2 pos)
            {
                _position = pos;
                LayoutButtons();
            }

            public void SetSwitchButtonAllowed(bool allowed)
            {
                // Switch button is now at index 4
                if (_buttons.Count > 4)
                {
                    _buttons[4].IsEnabled = allowed && _hasBench;
                }
            }

            private void InitializeButtons(List<BattleCombatant> allCombatants)
            {
                var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
                var tertiaryFont = ServiceLocator.Get<Core>().TertiaryFont;
                var global = ServiceLocator.Get<Global>();

                // 1. Basic (Index 0) - DarkestPale - Width 32 - Text "BSC"
                AddActionButton("BAS", Combatant.BasicMove, tertiaryFont, global, global.Palette_DarkestPale, 32);

                // 2. Core (Index 1) - Pale - Width 45
                AddActionButton("CORE", Combatant.CoreMove, secondaryFont, global, global.Palette_DarkPale, 45);

                // 3. Alt (Index 2) - DarkPale - Width 32
                AddActionButton("ALT", Combatant.AltMove, tertiaryFont, global, global.Palette_DarkestPale, 32);

                // --- SECONDARY ROW ---

                // 4. GUARD (Index 3)
                AddSecondaryButton("GUARD", "7");

                // 5. Switch (Index 4)
                var switchMove = new MoveData
                {
                    MoveID = "SWITCH",
                    MoveName = "SWITCH",
                    Description = "Switch to a reserve member.",
                    Target = TargetType.None
                };
                AddSecondaryButton("SWITCH", null, switchMove, () => OnSwitchRequested?.Invoke());

                // 6. Stall (Index 5)
                AddSecondaryButton("STALL", "6");

                // Check bench for Switch enablement
                _hasBench = allCombatants.Any(c => c.IsPlayerControlled && !c.IsDefeated && c.BattleSlot >= 2);
                if (_buttons.Count > 4) _buttons[4].IsEnabled = _hasBench;

                _cancelButton = new Button(Rectangle.Empty, "CANCEL", font: tertiaryFont, enableHoverSway: false)
                {
                    CustomDefaultTextColor = global.Palette_Rust,
                    CustomHoverTextColor = global.ButtonHoverColor
                };
                _cancelButton.OnClick += () => OnCancelRequested?.Invoke();
            }

            private void AddActionButton(string label, MoveEntry? entry, BitmapFont font, Global global, Color backgroundColor, int width)
            {
                bool moveExists = entry != null && BattleDataCache.Moves.ContainsKey(entry.MoveID);
                MoveData? moveData = moveExists ? BattleDataCache.Moves[entry!.MoveID] : null;

                Rectangle? iconRect = null;
                Color iconColor = Color.White;

                if (moveData != null)
                {
                    int iconIndex = -1;
                    switch (moveData.ImpactType)
                    {
                        case ImpactType.Physical: iconIndex = 3; break;
                        case ImpactType.Magical: iconIndex = 4; break;
                        case ImpactType.Status: iconIndex = 5; break;
                    }

                    if (iconIndex >= 0 && _iconRects != null && iconIndex < _iconRects.Length)
                    {
                        iconRect = _iconRects[iconIndex];
                    }

                    if (label == "CORE") iconColor = global.Palette_DarkestPale;
                    else iconColor = global.Palette_DarkShadow;
                }

                var btn = new MoveButton(Combatant, moveData, entry, font)
                {
                    DrawSystemBackground = true,
                    BackgroundColor = backgroundColor,
                    EnableHoverSway = false,
                    Text = label,
                    CustomDefaultTextColor = global.Palette_Black,
                    CustomHoverTextColor = global.Palette_Sun,
                    VisualWidthOverride = width,
                    TextRenderOffset = (label == "BAS" || label == "ALT") ? new Vector2(0, 1) : Vector2.Zero,
                    ActionIconRect = iconRect,
                    ActionIconColor = iconColor,
                    ActionIconHoverColor = global.Palette_DarkSun
                };

                if (moveExists)
                {
                    btn.OnClick += () =>
                    {
                        if (btn.CanAfford)
                        {
                            var action = new QueuedAction
                            {
                                Actor = Combatant,
                                ChosenMove = moveData!,
                                SpellbookEntry = entry!,
                                Type = QueuedActionType.Move,
                                Priority = moveData!.Priority,
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

            private void AddSecondaryButton(string label, string moveId, MoveData? manualData = null, Action? customAction = null)
            {
                var global = ServiceLocator.Get<Global>();
                var font = ServiceLocator.Get<Core>().TertiaryFont;

                MoveData? moveData = manualData;
                if (moveData == null && !string.IsNullOrEmpty(moveId) && BattleDataCache.Moves.ContainsKey(moveId))
                {
                    moveData = BattleDataCache.Moves[moveId];
                }

                var btn = new MoveButton(Combatant, moveData, null, font)
                {
                    DrawSystemBackground = true,
                    BackgroundColor = global.Palette_DarkShadow,
                    EnableHoverSway = false,
                    Text = label,
                    CustomDefaultTextColor = global.Palette_Black,
                    CustomHoverTextColor = global.Palette_Sun,
                    VisualWidthOverride = 37,
                    TextRenderOffset = new Vector2(0, 1)
                };

                if (customAction != null)
                {
                    btn.OnClick += customAction;
                }
                else if (moveData != null)
                {
                    btn.OnClick += () =>
                    {
                        if (btn.CanAfford)
                        {
                            var action = new QueuedAction
                            {
                                Actor = Combatant,
                                ChosenMove = moveData,
                                SpellbookEntry = null,
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
                // --- Top Row (Indices 0, 1, 2) ---
                int[] widths = { 32, 45, 32 };
                int totalButtonsWidth = widths.Sum() + (BUTTON_SPACING * 2);

                float panelCenterX = _position.X + (PANEL_WIDTH / 2f);
                int startX = (int)(panelCenterX - (totalButtonsWidth / 2f));
                int y = (int)_position.Y + 1;

                for (int i = 0; i < 3; i++)
                {
                    int visualHeight = (i == 1) ? 9 : 7; // Core is 9, Basic/Alt are 7
                    int hitboxHeight = 13;               // Requested hitbox size

                    // Calculate padding to center the visual within the 13px hitbox
                    // Core: (13 - 9) / 2 = 2px padding
                    // Basic/Alt: (13 - 7) / 2 = 3px padding
                    int paddingY = (hitboxHeight - visualHeight) / 2;

                    int yOffset = (i == 1) ? 0 : 1; // Original visual offset logic
                    int width = widths[i];

                    // Shift Bounds.Y up by paddingY so the visual (drawn at center) stays in the original spot
                    _buttons[i].Bounds = new Rectangle(startX, (y + yOffset) - paddingY, width + 1, hitboxHeight);

                    // Apply the visual override so it doesn't stretch
                    if (_buttons[i] is MoveButton mb)
                    {
                        mb.VisualHeightOverride = visualHeight;
                    }

                    startX += width + BUTTON_SPACING;
                }

                // --- Secondary Row (Indices 3, 4, 5) ---
                int[] secWidths = { 37, 37, 37 };
                int secTotalWidth = secWidths.Sum() + (BUTTON_SPACING * 2);
                int secStartX = (int)(panelCenterX - (secTotalWidth / 2f));
                int secY = (int)_position.Y + 12; // Same Y as InfoBox (they overlap if InfoBox is active)

                for (int i = 3; i < 6; i++)
                {
                    if (i >= _buttons.Count) break;
                    int width = secWidths[i - 3];
                    _buttons[i].Bounds = new Rectangle(secStartX, secY, width + 1, 7);
                    secStartX += width + BUTTON_SPACING;
                }

                // --- Cancel Button ---
                int cancelW = 50;
                int cancelH = 15;
                float panelCenterY = _position.Y + (BattleLayout.ACTION_MENU_HEIGHT / 2f);

                _cancelButton.Bounds = new Rectangle(
                    (int)(panelCenterX - cancelW / 2),
                    (int)(panelCenterY - cancelH / 2),
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
                            // Only show Info Box for the top row (0, 1, 2)
                            // Secondary row (3, 4, 5) does not trigger the large info box to avoid overlap
                            if (index >= 0 && index <= 2)
                            {
                                if (index == 0) // Basic
                                {
                                    var entry = Combatant.BasicMove;
                                    if (entry != null && BattleDataCache.Moves.TryGetValue(entry.MoveID, out var m))
                                        HoveredMove = m;
                                }
                                else if (index == 1) // Core
                                {
                                    var entry = Combatant.CoreMove;
                                    if (entry != null && BattleDataCache.Moves.TryGetValue(entry.MoveID, out var m))
                                        HoveredMove = m;
                                }
                                else if (index == 2) // Alt
                                {
                                    var entry = Combatant.AltMove;
                                    if (entry != null && BattleDataCache.Moves.TryGetValue(entry.MoveID, out var m))
                                        HoveredMove = m;
                                }
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

                int snappedOffsetY = (int)offset.Y;

                if (isLocked)
                {
                    var rect = _cancelButton.Bounds;
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
                    // 1. Draw Secondary Row (Indices 3, 4, 5) first (Background Layer)
                    for (int i = 3; i < 6; i++)
                    {
                        if (i < _buttons.Count)
                            DrawButton(spriteBatch, _buttons[i], pixel, global, snappedOffsetY, gameTime, transform, i);
                    }

                    // 2. Draw Info Box (Only if populated, covers secondary row)
                    if (HoveredMove != null)
                    {
                        float panelCenterX = _position.X + (PANEL_WIDTH / 2f);

                        // Calculate position for shared renderer
                        Vector2 tooltipPos = new Vector2(
                            panelCenterX - (MoveTooltipRenderer.WIDTH / 2f),
                            _position.Y + INFO_BOX_OFFSET_Y + snappedOffsetY
                        );

                        // Call shared renderer
                        _tooltipRenderer.DrawFixed(spriteBatch, tooltipPos, HoveredMove);
                    }

                    // 3. Draw Top Row (Indices 0, 1, 2) (Foreground Layer)
                    for (int i = 0; i < 3; i++)
                    {
                        DrawButton(spriteBatch, _buttons[i], pixel, global, snappedOffsetY, gameTime, transform, i);
                    }
                }

                if (global.ShowSplitMapGrid)
                {
                    var activePlayers = battleManager.AllCombatants.Where(c => c.IsPlayerControlled && c.IsActiveOnField).ToList();
                    bool isCentered = activePlayers.Count == 1;
                    var area = BattleLayout.GetActionMenuArea(SlotIndex, isCentered);
                    spriteBatch.DrawSnapped(pixel, area, (SlotIndex == 0 ? Color.Cyan : Color.Magenta) * 0.2f);
                    spriteBatch.DrawLineSnapped(new Vector2(area.Right, area.Top), new Vector2(area.Right, area.Bottom), (SlotIndex == 0 ? Color.Cyan : Color.Magenta));
                }
            }

            private void DrawButton(SpriteBatch spriteBatch, Button btn, Texture2D pixel, Global global, int snappedOffsetY, GameTime gameTime, Matrix transform, int moveIndex = -1)
            {
                var rect = btn.Bounds;
                rect.Y += snappedOffsetY;

                Color? tint = null;
                if (btn is MoveButton moveBtn)
                {
                    if (!moveBtn.IsEnabled || !moveBtn.CanAfford) tint = global.Palette_DarkShadow;
                }
                else
                {
                    if (!btn.IsEnabled) tint = global.Palette_DarkShadow;
                }

                btn.Draw(spriteBatch, btn.Font, gameTime, transform, false, 0f, snappedOffsetY, tint);
            }

            private void DrawBeveledBackground(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color color)
            {
                // Top and Bottom edges (inset by 1px on each side)
                spriteBatch.DrawSnapped(pixel, new Rectangle(rect.X + 1, rect.Y, rect.Width - 2, 1), color);
                spriteBatch.DrawSnapped(pixel, new Rectangle(rect.X + 1, rect.Bottom - 1, rect.Width - 2, 1), color);

                // Middle block (full height minus top/bottom edges)
                spriteBatch.DrawSnapped(pixel, new Rectangle(rect.X, rect.Y + 1, rect.Width, rect.Height - 2), color);
            }
        }
    }
}
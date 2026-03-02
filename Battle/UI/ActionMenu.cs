using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Battle.UI
{
    public class ActionMenu
    {
        public event Action<int, QueuedAction>? OnActionSelected;
        public event Action<int>? OnSwitchMenuRequested;
        public event Action<int>? OnCancelRequested;
        public event Action? OnFleeRequested;

        private bool _isVisible;
        public bool IsVisible => _isVisible;

        private List<CombatantPanel> _panels = new List<CombatantPanel>();

        public MoveData? HoveredMove { get; private set; }
        public int HoveredSlotIndex { get; private set; } = -1;
        public MoveEntry? SelectedMoveEntry { get; private set; }

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
            if (_isVisible && _panels.Count > 0 && _panels[0].Combatant == anyPlayer) return;

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
            TriggerButtonEntrance();
            _isVisible = true;
        }

        public void HideButtons() { foreach (var panel in _panels) panel.HideButtons(); }
        public void TriggerButtonEntrance() { foreach (var panel in _panels) panel.PopButtons(); }
        public void Hide() => _isVisible = false;
        public void GoBack() { }

        private void UpdateLayout()
        {
            foreach (var panel in _panels)
            {
                var area = BattleLayout.GetActionMenuArea(panel.SlotIndex);
                int x = area.Center.X - (PANEL_WIDTH / 2);
                int y = area.Y;
                panel.SetPosition(new Vector2(x, y));
            }
        }

        public void UpdatePositions(BattleRenderer renderer) => UpdateLayout();

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

        public void DrawButtons(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform, Vector2 offset, int? hiddenSlotIndex = null)
        {
            if (!_isVisible) return;
            foreach (var panel in _panels)
            {
                if (hiddenSlotIndex.HasValue && panel.SlotIndex == hiddenSlotIndex.Value) continue;
                panel.DrawButtons(spriteBatch, font, gameTime, transform, offset);
            }
        }

        public void DrawTooltips(SpriteBatch spriteBatch, Vector2 offset, int? hiddenSlotIndex = null)
        {
            if (!_isVisible) return;
            foreach (var panel in _panels)
            {
                if (hiddenSlotIndex.HasValue && panel.SlotIndex == hiddenSlotIndex.Value) continue;
                panel.DrawTooltip(spriteBatch, offset);
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

            private const int PANEL_WIDTH = 146;

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
                if (_buttons.Count > 3) _buttons[3].IsEnabled = allowed && _hasBench;
            }

            public void HideButtons() { foreach (var btn in _buttons) btn.SetHiddenForEntrance(); }

            public void PopButtons()
            {
                var random = new Random();
                var indices = Enumerable.Range(0, _buttons.Count).ToList();
                int n = indices.Count;
                while (n > 1) { n--; int k = random.Next(n + 1); int value = indices[k]; indices[k] = indices[n]; indices[n] = value; }
                for (int i = 0; i < indices.Count; i++) { _buttons[indices[i]].PlayEntrance(i * 0.05f); }
            }

            private void InitializeButtons(List<BattleCombatant> allCombatants)
            {
                var defaultFont = ServiceLocator.Get<Core>().DefaultFont;
                var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
                var tertiaryFont = ServiceLocator.Get<Core>().TertiaryFont;
                var global = ServiceLocator.Get<Global>();

                MoveData? GetMoveData(MoveEntry? entry) => entry != null && BattleDataCache.Moves.TryGetValue(entry.MoveID, out var m) ? m : null;
                BitmapFont GetFontForLabel(string text) => text.Length >= 12 ? tertiaryFont : (text.Length > 8 ? secondaryFont : defaultFont);

                // 0: Basic
                AddActionButton("BASC", Combatant.BasicMove, defaultFont, global, global.Palette_DarkestPale, 44, 28, new Vector2(0, 1), Vector2.Zero, false);

                // 1: Spell 1
                var sp1Data = GetMoveData(Combatant.Spell1);
                string sp1Label = sp1Data?.MoveName.ToUpper() ?? "---";
                AddActionButton(sp1Label, Combatant.Spell1, GetFontForLabel(sp1Label), global, global.Palette_DarkPale, 89, 13, Vector2.Zero, new Vector2(0, 1), true);

                // 2: Spell 2
                var sp2Data = GetMoveData(Combatant.Spell2);
                string sp2Label = sp2Data?.MoveName.ToUpper() ?? "---";
                AddActionButton(sp2Label, Combatant.Spell2, GetFontForLabel(sp2Label), global, global.Palette_DarkestPale, 89, 13, Vector2.Zero, new Vector2(0, 1), true);

                // 3: Switch
                var switchMove = new MoveData { MoveID = "SWITCH", MoveName = "SWITCH", Description = "Switch to a reserve member.", Target = TargetType.None };
                AddSecondaryButton("SWITCH", null, switchMove, () => OnSwitchRequested?.Invoke());

                // 4: Spell 3
                var sp3Data = GetMoveData(Combatant.Spell3);
                string sp3Label = sp3Data?.MoveName.ToUpper() ?? "---";
                AddActionButton(sp3Label, Combatant.Spell3, GetFontForLabel(sp3Label), global, global.Palette_DarkPale, 89, 13, Vector2.Zero, new Vector2(0, 1), true);

                _hasBench = allCombatants.Any(c => c.IsPlayerControlled && !c.IsDefeated && c.BattleSlot >= 2);
                if (_buttons.Count > 3) _buttons[3].IsEnabled = _hasBench;

                _cancelButton = new Button(Rectangle.Empty, "CANCEL", font: tertiaryFont)
                {
                    CustomDefaultTextColor = global.Palette_Rust,
                    CustomHoverTextColor = global.ButtonHoverColor
                };
                _cancelButton.OnClick += () => OnCancelRequested?.Invoke();
            }

            private void LayoutButtons()
            {
                int primaryTotalWidth = 133;
                float panelCenterX = _position.X + (PANEL_WIDTH / 2f);
                int startX = (int)(panelCenterX - (primaryTotalWidth / 2f));
                int y = (int)_position.Y + 1;

                // Basic
                if (_buttons.Count > 0) { _buttons[0].Bounds = InflateRect(startX - 1, y - 1, 44, 28); ((MoveButton)_buttons[0]).VisualWidthOverride = 44; ((MoveButton)_buttons[0]).VisualHeightOverride = 28; }
                // Spell 1
                if (_buttons.Count > 1) { _buttons[1].Bounds = InflateRect(startX + 45, y - 1, 89, 13); ((MoveButton)_buttons[1]).VisualWidthOverride = 89; }
                // Spell 2
                if (_buttons.Count > 2) { _buttons[2].Bounds = InflateRect(startX + 45, y + 14, 89, 13); ((MoveButton)_buttons[2]).VisualWidthOverride = 89; }
                // Switch (Under Basic)
                if (_buttons.Count > 3) { _buttons[3].Bounds = InflateRect(startX - 1, y + 29, 44, 13); ((MoveButton)_buttons[3]).VisualWidthOverride = 44; ((MoveButton)_buttons[3]).VisualHeightOverride = 13; }
                // Spell 3 (Under Spell 2)
                if (_buttons.Count > 4) { _buttons[4].Bounds = InflateRect(startX + 45, y + 29, 89, 13); ((MoveButton)_buttons[4]).VisualWidthOverride = 89; }

                _cancelButton.Bounds = new Rectangle((int)(panelCenterX - 25), (int)(_position.Y + (BattleLayout.ACTION_MENU_HEIGHT / 2f) - 7), 50, 15);
            }

            private Rectangle InflateRect(int x, int y, int w, int h) { var r = new Rectangle(x, y, w, h); r.Inflate(1, 1); return r; }

            private void AddActionButton(string label, MoveEntry? entry, BitmapFont font, Global global, Color defaultBackgroundColor, int width, int height, Vector2 iconOffset, Vector2 textOffset, bool showCooldown)
            {
                bool moveExists = entry != null && BattleDataCache.Moves.ContainsKey(entry.MoveID);
                MoveData? moveData = moveExists ? BattleDataCache.Moves[entry!.MoveID] : null;

                Color backgroundColor = defaultBackgroundColor;
                Rectangle? iconRect = null;
                Color iconColor = Color.White;
                bool forceDisabled = false;

                if (moveData != null)
                {
                    switch (moveData.ImpactType) { case ImpactType.Physical: backgroundColor = global.Palette_DarkRust; break; case ImpactType.Magical: backgroundColor = global.Palette_Sea; break; case ImpactType.Status: backgroundColor = global.Palette_DarkPale; break; }
                    int iconIndex = moveData.ImpactType switch { ImpactType.Physical => 3, ImpactType.Magical => 4, ImpactType.Status => 5, _ => -1 };
                    if (iconIndex >= 0 && _iconRects != null && iconIndex < _iconRects.Length) iconRect = _iconRects[iconIndex];
                    iconColor = global.Palette_Black;
                    if (moveData.Abilities.OfType<CounterAbility>().Any() && Combatant.HasUsedFirstAttack) forceDisabled = true;
                }

                bool isOnCooldown = entry != null && entry.TurnsUntilReady > 0;

                var btn = new MoveButton(Combatant, moveData, entry, font)
                {
                    DrawSystemBackground = true,
                    BackgroundColor = backgroundColor,
                    EnableHoverSway = false,
                    Text = label,
                    CustomDefaultTextColor = global.Palette_Black,
                    CustomHoverTextColor = global.Palette_DarkestPale,
                    VisualWidthOverride = width,
                    VisualHeightOverride = height,
                    TextRenderOffset = textOffset,
                    ActionIconRect = iconRect,
                    ActionIconColor = iconColor,
                    ActionIconHoverColor = global.Palette_DarkestPale,
                    IconRenderOffset = iconOffset,
                    IsEnabled = !forceDisabled && !isOnCooldown,
                    ShowCooldown = showCooldown,
                    CurrentCooldown = entry?.TurnsUntilReady ?? 0,
                    MaxCooldown = moveData?.Cooldown ?? 0
                };

                if (moveExists && !forceDisabled && !isOnCooldown)
                {
                    btn.OnClick += () => OnActionSelected?.Invoke(new QueuedAction { Actor = Combatant, ChosenMove = moveData!, SpellbookEntry = entry!, Type = QueuedActionType.Move, Priority = moveData!.Priority, ActorAgility = Combatant.GetEffectiveAgility() });
                }
                _buttons.Add(btn);
            }

            private void AddSecondaryButton(string label, string moveId, MoveData? manualData = null, Action? customAction = null)
            {
                var global = ServiceLocator.Get<Global>();
                var font = ServiceLocator.Get<Core>().SecondaryFont;
                MoveData? moveData = manualData ?? (!string.IsNullOrEmpty(moveId) && BattleDataCache.Moves.TryGetValue(moveId, out var m) ? m : null);

                var btn = new MoveButton(Combatant, moveData, null, font)
                {
                    DrawSystemBackground = true,
                    BackgroundColor = global.Palette_DarkestPale,
                    EnableHoverSway = false,
                    Text = label,
                    CustomDefaultTextColor = global.Palette_Black,
                    CustomHoverTextColor = global.Palette_DarkestPale,
                    VisualWidthOverride = 44,
                    VisualHeightOverride = 13,
                    TextRenderOffset = Vector2.Zero
                };

                if (customAction != null) btn.OnClick += customAction;
                else if (moveData != null) btn.OnClick += () => OnActionSelected?.Invoke(new QueuedAction { Actor = Combatant, ChosenMove = moveData, Type = QueuedActionType.Move, Priority = moveData.Priority, ActorAgility = Combatant.GetEffectiveAgility() });
                _buttons.Add(btn);
            }

            public void Update(MouseState mouse, GameTime gameTime, bool isInputBlocked, bool isLocked)
            {
                HoveredMove = null;
                if (isInputBlocked) { foreach (var btn in _buttons) btn.IsHovered = false; _cancelButton.IsHovered = false; return; }

                if (isLocked) _cancelButton.Update(mouse);
                else
                {
                    Button? bestBtn = null;
                    float bestDist = float.MaxValue;
                    Vector2 mousePos = Core.TransformMouse(mouse.Position);

                    foreach (var btn in _buttons)
                    {
                        if (btn.Bounds.Contains(mousePos))
                        {
                            float dist = Vector2.Distance(btn.Bounds.Center.ToVector2(), mousePos);
                            if (dist < bestDist) { bestDist = dist; bestBtn = btn; }
                        }
                    }

                    foreach (var btn in _buttons)
                    {
                        btn.Update(mouse);
                        if (btn != bestBtn) btn.IsHovered = false;

                        if (btn.IsHovered && btn.IsEnabled)
                        {
                            int index = _buttons.IndexOf(btn);
                            if (index == 0) HoveredMove = GetMove(Combatant.BasicMove);
                            else if (index == 1) HoveredMove = GetMove(Combatant.Spell1);
                            else if (index == 2) HoveredMove = GetMove(Combatant.Spell2);
                            else if (index == 4) HoveredMove = GetMove(Combatant.Spell3);
                        }
                    }
                }
            }

            private MoveData? GetMove(MoveEntry? entry) => entry != null && BattleDataCache.Moves.TryGetValue(entry.MoveID, out var m) ? m : null;

            public void DrawButtons(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform, Vector2 offset)
            {
                var pixel = ServiceLocator.Get<Texture2D>();
                var global = ServiceLocator.Get<Global>();
                bool isLocked = ServiceLocator.Get<BattleManager>().IsActionPending(SlotIndex);
                float snappedOffsetY = offset.Y;

                if (isLocked) _cancelButton.Draw(spriteBatch, _cancelButton.Font, gameTime, transform, false, 0f, snappedOffsetY);
                else foreach (var btn in _buttons) DrawButton(spriteBatch, btn, pixel, global, snappedOffsetY, gameTime, transform);

                if (global.ShowSplitMapGrid)
                {
                    var area = BattleLayout.GetActionMenuArea(SlotIndex);
                    spriteBatch.DrawSnapped(pixel, area, (SlotIndex == 0 ? Color.Cyan : Color.Magenta) * 0.2f);
                    spriteBatch.DrawLineSnapped(new Vector2(area.Right, area.Top), new Vector2(area.Right, area.Bottom), (SlotIndex == 0 ? Color.Cyan : Color.Magenta));
                }
            }

            public void DrawTooltip(SpriteBatch spriteBatch, Vector2 offset)
            {
                if (HoveredMove == null) return;
                int targetSlot = (SlotIndex == 0) ? 1 : 0;
                var targetArea = BattleLayout.GetActionMenuArea(targetSlot);
                Vector2 tooltipPos = new Vector2(targetArea.Center.X - (MoveTooltipRenderer.WIDTH / 2f), targetArea.Center.Y - 23f + offset.Y);
                _tooltipRenderer.DrawFixed(spriteBatch, tooltipPos, HoveredMove);
            }

            private void DrawButton(SpriteBatch spriteBatch, Button btn, Texture2D pixel, Global global, float snappedOffsetY, GameTime gameTime, Matrix transform)
            {
                Color? tint = null;
                if (btn is MoveButton moveBtn)
                {
                    moveBtn.Opacity = Combatant.HudVisualAlpha;
                    if (!moveBtn.IsEnabled) tint = global.Palette_DarkShadow;
                }
                else if (!btn.IsEnabled) tint = global.Palette_DarkShadow;

                btn.Draw(spriteBatch, btn.Font, gameTime, transform, false, 0f, snappedOffsetY, tint);
            }
        }
    }
}
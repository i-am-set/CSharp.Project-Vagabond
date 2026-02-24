using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Particles;
using ProjectVagabond.Progression;
using ProjectVagabond.Scenes;
using ProjectVagabond.Transitions;
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
            if (_isVisible && _panels.Count > 0 && _panels[0].Combatant == anyPlayer)
            {
                return;
            }

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

            // Trigger entrance animation when showing the menu
            TriggerButtonEntrance();

            _isVisible = true;
        }

        public void HideButtons()
        {
            foreach (var panel in _panels)
            {
                panel.HideButtons();
            }
        }

        public void TriggerButtonEntrance()
        {
            foreach (var panel in _panels)
            {
                panel.PopButtons();
            }
        }

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

        public void UpdatePositions(BattleRenderer renderer)
        {
            UpdateLayout();
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

        // Split Draw into DrawButtons and DrawTooltips to allow layering with SwitchMenu
        public void DrawButtons(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform, Vector2 offset, int? hiddenSlotIndex = null)
        {
            if (!_isVisible) return;

            foreach (var panel in _panels)
            {
                if (hiddenSlotIndex.HasValue && panel.SlotIndex == hiddenSlotIndex.Value)
                {
                    continue;
                }
                panel.DrawButtons(spriteBatch, font, gameTime, transform, offset);
            }
        }

        public void DrawTooltips(SpriteBatch spriteBatch, Vector2 offset, int? hiddenSlotIndex = null)
        {
            if (!_isVisible) return;

            foreach (var panel in _panels)
            {
                if (hiddenSlotIndex.HasValue && panel.SlotIndex == hiddenSlotIndex.Value)
                {
                    continue;
                }
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

            private const int HITBOX_WIDTH = 45;
            private const int VISUAL_WIDTH = 44;
            private const int INFO_BOX_OFFSET_Y = 23;

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
                if (_buttons.Count > 4)
                {
                    _buttons[4].IsEnabled = allowed && _hasBench;
                }
            }

            public void HideButtons()
            {
                foreach (var btn in _buttons) btn.SetHiddenForEntrance();
            }

            public void PopButtons()
            {
                var random = new Random();
                var indices = Enumerable.Range(0, _buttons.Count).ToList();
                int n = indices.Count;
                while (n > 1)
                {
                    n--;
                    int k = random.Next(n + 1);
                    int value = indices[k];
                    indices[k] = indices[n];
                    indices[n] = value;
                }

                for (int i = 0; i < indices.Count; i++)
                {
                    int btnIndex = indices[i];
                    float delay = i * 0.05f;
                    _buttons[btnIndex].PlayEntrance(delay);
                }
            }

            private void InitializeButtons(List<BattleCombatant> allCombatants)
            {
                var defaultFont = ServiceLocator.Get<Core>().DefaultFont;
                var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
                var tertiaryFont = ServiceLocator.Get<Core>().TertiaryFont;
                var global = ServiceLocator.Get<Global>();

                MoveData? GetMoveData(MoveEntry? entry)
                {
                    if (entry != null && BattleDataCache.Moves.TryGetValue(entry.MoveID, out var m))
                        return m;
                    return null;
                }

                BitmapFont GetFontForLabel(string text)
                {
                    if (text.Length >= 12) return tertiaryFont;
                    if (text.Length > 8) return secondaryFont;
                    return defaultFont;
                }

                // 1. Basic (Index 0)
                AddActionButton("BASC", Combatant.BasicMove, defaultFont, global, global.Palette_DarkestPale, VISUAL_WIDTH, 27, new Vector2(0, 1), Vector2.Zero);

                // 2. Core (Index 1)
                var coreData = GetMoveData(Combatant.CoreMove);
                string coreLabel = coreData?.MoveName.ToUpper() ?? "---";
                var coreFont = GetFontForLabel(coreLabel);
                AddActionButton(coreLabel, Combatant.CoreMove, coreFont, global, global.Palette_DarkPale, 88, 13, Vector2.Zero, new Vector2(0, 1));

                // 3. Alt (Index 2)
                var altData = GetMoveData(Combatant.AltMove);
                string altLabel = altData?.MoveName.ToUpper() ?? "---";
                var altFont = GetFontForLabel(altLabel);
                AddActionButton(altLabel, Combatant.AltMove, altFont, global, global.Palette_DarkestPale, 88, 13, Vector2.Zero, new Vector2(0, 1));

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

                _hasBench = allCombatants.Any(c => c.IsPlayerControlled && !c.IsDefeated && c.BattleSlot >= 2);
                if (_buttons.Count > 4) _buttons[4].IsEnabled = _hasBench;

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

                if (_buttons.Count > 0)
                {
                    var visualRect = new Rectangle(startX - 1, y - 1, 44, 28);
                    var hitbox = visualRect;
                    hitbox.Inflate(1, 1);
                    _buttons[0].Bounds = hitbox;

                    if (_buttons[0] is MoveButton mb)
                    {
                        mb.VisualHeightOverride = 28;
                        mb.VisualWidthOverride = 44;
                    }
                }

                if (_buttons.Count > 1)
                {
                    var visualRect = new Rectangle(startX + 45, y - 1, 89, 13);
                    var hitbox = visualRect;
                    hitbox.Inflate(1, 1);
                    _buttons[1].Bounds = hitbox;

                    if (_buttons[1] is MoveButton mb)
                    {
                        mb.VisualWidthOverride = 89;
                    }
                }

                if (_buttons.Count > 2)
                {
                    var visualRect = new Rectangle(startX + 45, y + 14, 89, 13);
                    var hitbox = visualRect;
                    hitbox.Inflate(1, 1);
                    _buttons[2].Bounds = hitbox;

                    if (_buttons[2] is MoveButton mb)
                    {
                        mb.VisualWidthOverride = 89;
                    }
                }

                int originalTotalButtonsWidth = (HITBOX_WIDTH * 3);
                int secStartX = (int)(panelCenterX - (originalTotalButtonsWidth / 2f));
                int secY = y + 29;

                for (int i = 3; i < 6; i++)
                {
                    if (i >= _buttons.Count) break;

                    int visualHeight = 12;
                    int hitboxHeight = 16;

                    int currentX = secStartX;
                    int currentWidth = HITBOX_WIDTH;
                    int currentVisualWidth = VISUAL_WIDTH;

                    if (i == 4)
                    {
                        currentX += 1;
                        currentWidth -= 1;
                        currentVisualWidth -= 1;
                    }

                    if (i == 5)
                    {
                        currentX += 1;
                    }

                    var rect = new Rectangle(currentX, secY, currentWidth, hitboxHeight);
                    rect.Inflate(1, 1);
                    _buttons[i].Bounds = rect;

                    if (_buttons[i] is MoveButton mb)
                    {
                        mb.VisualHeightOverride = visualHeight;
                        mb.VisualWidthOverride = currentVisualWidth;
                    }

                    secStartX += HITBOX_WIDTH;
                }

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

            private void AddActionButton(string label, MoveEntry? entry, BitmapFont font, Global global, Color defaultBackgroundColor, int width, int height, Vector2 iconOffset, Vector2 textOffset)
            {
                bool moveExists = entry != null && BattleDataCache.Moves.ContainsKey(entry.MoveID);
                MoveData? moveData = moveExists ? BattleDataCache.Moves[entry!.MoveID] : null;

                Color backgroundColor = defaultBackgroundColor;
                Rectangle? iconRect = null;
                Color iconColor = Color.White;

                bool forceDisabled = false;

                if (moveData != null)
                {
                    switch (moveData.ImpactType)
                    {
                        case ImpactType.Physical: backgroundColor = global.Palette_DarkRust; break;
                        case ImpactType.Magical: backgroundColor = global.Palette_Sea; break;
                        case ImpactType.Status: backgroundColor = global.Palette_DarkPale; break;
                    }

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

                    iconColor = global.Palette_Black;

                    bool isCounter = moveData.Abilities.OfType<CounterAbility>().Any();
                    if (isCounter && Combatant.HasUsedFirstAttack)
                    {
                        forceDisabled = true;
                    }
                }

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
                    IsEnabled = !forceDisabled
                };

                if (moveExists && !forceDisabled)
                {
                    btn.OnClick += () =>
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
                    };
                }

                _buttons.Add(btn);
            }

            private void AddSecondaryButton(string label, string moveId, MoveData? manualData = null, Action? customAction = null)
            {
                var global = ServiceLocator.Get<Global>();
                var font = ServiceLocator.Get<Core>().SecondaryFont;

                MoveData? moveData = manualData;
                if (moveData == null && !string.IsNullOrEmpty(moveId) && BattleDataCache.Moves.ContainsKey(moveId))
                {
                    moveData = BattleDataCache.Moves[moveId];
                }

                var btn = new MoveButton(Combatant, moveData, null, font)
                {
                    DrawSystemBackground = true,
                    BackgroundColor = global.Palette_DarkestPale,
                    EnableHoverSway = false,
                    Text = label,
                    CustomDefaultTextColor = global.Palette_Black,
                    CustomHoverTextColor = global.Palette_DarkestPale,
                    VisualWidthOverride = VISUAL_WIDTH,
                    VisualHeightOverride = 20,
                    TextRenderOffset = Vector2.Zero
                };

                if (customAction != null)
                {
                    btn.OnClick += customAction;
                }
                else if (moveData != null)
                {
                    btn.OnClick += () =>
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
                    };
                }

                _buttons.Add(btn);
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
                    Button? bestBtn = null;
                    float bestDist = float.MaxValue;
                    Vector2 mousePos = Core.TransformMouse(mouse.Position);

                    foreach (var btn in _buttons)
                    {
                        if (btn.Bounds.Contains(mousePos))
                        {
                            float dist = Vector2.Distance(btn.Bounds.Center.ToVector2(), mousePos);
                            if (dist < bestDist)
                            {
                                bestDist = dist;
                                bestBtn = btn;
                            }
                        }
                    }

                    foreach (var btn in _buttons)
                    {
                        btn.Update(mouse);
                        if (btn != bestBtn) btn.IsHovered = false;

                        if (btn.IsHovered && btn.IsEnabled)
                        {
                            int index = _buttons.IndexOf(btn);
                            if (index >= 0 && index <= 2)
                            {
                                if (index == 0)
                                {
                                    var entry = Combatant.BasicMove;
                                    if (entry != null && BattleDataCache.Moves.TryGetValue(entry.MoveID, out var m))
                                        HoveredMove = m;
                                }
                                else if (index == 1)
                                {
                                    var entry = Combatant.CoreMove;
                                    if (entry != null && BattleDataCache.Moves.TryGetValue(entry.MoveID, out var m))
                                        HoveredMove = m;
                                }
                                else if (index == 2)
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

            public void DrawButtons(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform, Vector2 offset)
            {
                var pixel = ServiceLocator.Get<Texture2D>();
                var global = ServiceLocator.Get<Global>();
                var battleManager = ServiceLocator.Get<BattleManager>();
                bool isLocked = battleManager.IsActionPending(SlotIndex);

                float snappedOffsetY = offset.Y;

                if (isLocked)
                {
                    _cancelButton.Draw(spriteBatch, _cancelButton.Font, gameTime, transform, false, 0f, snappedOffsetY);
                }
                else
                {
                    for (int i = 3; i < 6; i++)
                    {
                        if (i < _buttons.Count)
                            DrawButton(spriteBatch, _buttons[i], pixel, global, snappedOffsetY, gameTime, transform, i);
                    }

                    for (int i = 0; i < 3; i++)
                    {
                        DrawButton(spriteBatch, _buttons[i], pixel, global, snappedOffsetY, gameTime, transform, i);
                    }
                }

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

                float snappedOffsetY = offset.Y;
                Vector2 tooltipPos = new Vector2(
                    targetArea.Center.X - (MoveTooltipRenderer.WIDTH / 2f),
                    targetArea.Center.Y - (23f) + snappedOffsetY
                );

                _tooltipRenderer.DrawFixed(spriteBatch, tooltipPos, HoveredMove);
            }

            private void DrawButton(SpriteBatch spriteBatch, Button btn, Texture2D pixel, Global global, float snappedOffsetY, GameTime gameTime, Matrix transform, int moveIndex = -1)
            {
                Color? tint = null;
                if (btn is MoveButton moveBtn)
                {
                    // Apply the combatant's HUD alpha to the button opacity
                    moveBtn.Opacity = Combatant.HudVisualAlpha;

                    if (!moveBtn.IsEnabled) tint = global.Palette_DarkShadow;
                }
                else
                {
                    if (!btn.IsEnabled) tint = global.Palette_DarkShadow;
                }

                btn.Draw(spriteBatch, btn.Font, gameTime, transform, false, 0f, snappedOffsetY, tint);
            }
        }
    }
}

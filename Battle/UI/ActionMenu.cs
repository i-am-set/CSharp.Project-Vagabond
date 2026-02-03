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

            // --- Button Layout Configuration ---
            private const int BUTTON_WIDTH = 45;
            private const int BUTTON_HEIGHT = 9;
            private const int BUTTON_SPACING = 1;

            // --- Info Box Configuration ---
            private const int INFO_BOX_WIDTH = 139;
            private const int INFO_BOX_HEIGHT = 34;
            private const int INFO_BOX_OFFSET_Y = 12; // Below the buttons

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
                if (_buttons.Count > 3)
                {
                    _buttons[3].IsEnabled = allowed && _hasBench;
                }
            }

            private void InitializeButtons(List<BattleCombatant> allCombatants)
            {
                var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
                var tertiaryFont = ServiceLocator.Get<Core>().TertiaryFont;
                var global = ServiceLocator.Get<Global>();
                var spriteManager = ServiceLocator.Get<SpriteManager>();
                var icons = spriteManager.ActionIconsSpriteSheet;

                // 1. Basic (Index 0)
                AddActionButton("BASIC", Combatant.BasicMove, secondaryFont, global);

                // 2. Core (Index 1)
                AddActionButton("CORE", Combatant.CoreMove, secondaryFont, global);

                // 3. Alt (Index 2)
                AddActionButton("ALT", Combatant.AltMove, secondaryFont, global);

                // 4. Switch (Index 3)
                var switchBtn = new TextOverImageButton(Rectangle.Empty, "SWITCH", null, font: tertiaryFont, enableHoverSway: false, iconTexture: icons, iconSourceRect: _iconRects[1])
                {
                    AlignLeft = true,
                    IconColorMatchesText = true,
                    CustomDefaultTextColor = global.GameTextColor,
                    TextRenderOffset = new Vector2(0, -1),
                    IconRenderOffset = new Vector2(0, -1),
                    EnableHoverRotation = false
                };
                switchBtn.OnClick += () => OnSwitchRequested?.Invoke();
                _hasBench = allCombatants.Any(c => c.IsPlayerControlled && !c.IsDefeated && c.BattleSlot >= 2);
                switchBtn.IsEnabled = _hasBench;
                _buttons.Add(switchBtn);

                _cancelButton = new Button(Rectangle.Empty, "CANCEL", font: tertiaryFont, enableHoverSway: false)
                {
                    CustomDefaultTextColor = global.Palette_Rust,
                    CustomHoverTextColor = global.ButtonHoverColor
                };
                _cancelButton.OnClick += () => OnCancelRequested?.Invoke();
            }

            private void AddActionButton(string label, MoveEntry? entry, BitmapFont font, Global global)
            {
                bool moveExists = entry != null && BattleDataCache.Moves.ContainsKey(entry.MoveID);

                // Standard Button (No Icon, Centered Text)
                var btn = new Button(Rectangle.Empty, label, font: font, enableHoverSway: false)
                {
                    CustomDefaultTextColor = moveExists ? global.Palette_Black : global.Palette_DarkShadow,
                    CustomHoverTextColor = global.Palette_Black, // Hover color is handled by bg change, text stays black
                    IsEnabled = moveExists,
                    // NEW SETTINGS
                    HoverAnimation = HoverAnimationType.None, // Disable scale
                    WaveEffectType = TextEffectType.Drift,    // Change text effect
                    EnableTextWave = true
                };

                if (moveExists)
                {
                    var moveData = BattleDataCache.Moves[entry!.MoveID];
                    btn.OnClick += () =>
                    {
                        bool canAfford = false;
                        var manaDump = moveData.Abilities.OfType<ManaDumpAbility>().FirstOrDefault();
                        if (manaDump != null) canAfford = Combatant.Stats.CurrentMana > 0;
                        else canAfford = Combatant.Stats.CurrentMana >= moveData.ManaCost;

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
                // Calculate total width of the 3 buttons + spacing
                int totalButtonsWidth = (BUTTON_WIDTH * 3) + (BUTTON_SPACING * 2);

                // Center the group within the panel
                float panelCenterX = _position.X + (PANEL_WIDTH / 2f);
                int startX = (int)(panelCenterX - (totalButtonsWidth / 2f));
                int y = (int)_position.Y + 1;

                // Layout the 3 move buttons in the top row
                for (int i = 0; i < 3; i++)
                {
                    // Hitbox is 1px wider to fill the gap
                    _buttons[i].Bounds = new Rectangle(startX, y, BUTTON_WIDTH + 1, BUTTON_HEIGHT);
                    startX += BUTTON_WIDTH + BUTTON_SPACING;
                }

                // Layout the Switch button below the middle button (Core)
                int switchW = 50;
                int switchH = 15;
                int switchY = (int)_position.Y + INFO_BOX_OFFSET_Y + 2;

                _buttons[3].Bounds = new Rectangle(
                    (int)(panelCenterX - switchW / 2),
                    switchY,
                    switchW,
                    switchH
                );

                // Layout Cancel Button
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
                            // Index 3 is Switch, which does not populate HoveredMove
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
                    // 1. Draw Switch Button (Index 3) first so it is underneath the info panel if it appears
                    DrawButton(spriteBatch, _buttons[3], pixel, global, snappedOffsetY, gameTime, transform);

                    // 2. Draw Info Box (Only if populated)
                    if (HoveredMove != null)
                    {
                        float panelCenterX = _position.X + (PANEL_WIDTH / 2f);
                        var infoBoxRect = new Rectangle(
                            (int)(panelCenterX - (INFO_BOX_WIDTH / 2f)),
                            (int)_position.Y + INFO_BOX_OFFSET_Y + snappedOffsetY,
                            INFO_BOX_WIDTH,
                            INFO_BOX_HEIGHT
                        );

                        // Opaque background to cover the switch button
                        DrawBeveledBackground(spriteBatch, pixel, infoBoxRect, global.Palette_DarkShadow);

                        // Inner description area background
                        var descBgRect = new Rectangle(
                            infoBoxRect.X + 1,
                            infoBoxRect.Bottom - 1 - 18,
                            infoBoxRect.Width - 2,
                            18
                        );
                        DrawBeveledBackground(spriteBatch, pixel, descBgRect, global.Palette_Black);

                        DrawInfoBoxContent(spriteBatch, infoBoxRect, HoveredMove, global);
                    }

                    // 3. Draw Move Buttons (Indices 0, 1, 2)
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

                // Visual rect uses fixed dimensions to ensure 9px height and 45px width, ignoring the 1px hitbox extension
                var visualRect = new Rectangle(rect.X, rect.Y, BUTTON_WIDTH, BUTTON_HEIGHT);
                Color bgColor;
                bool canAfford = true;

                if (moveIndex != -1)
                {
                    MoveEntry? entry = null;
                    if (moveIndex == 0) entry = Combatant.BasicMove;
                    else if (moveIndex == 1) entry = Combatant.CoreMove;
                    else if (moveIndex == 2) entry = Combatant.AltMove;

                    if (entry != null && BattleDataCache.Moves.TryGetValue(entry.MoveID, out var moveData))
                    {
                        var manaDump = moveData.Abilities.OfType<ManaDumpAbility>().FirstOrDefault();
                        if (manaDump != null) canAfford = Combatant.Stats.CurrentMana > 0;
                        else canAfford = Combatant.Stats.CurrentMana >= moveData.ManaCost;
                    }
                }

                if (!btn.IsEnabled || !canAfford) bgColor = global.Palette_Black;
                else if (btn.IsHovered) bgColor = global.Palette_Rust;
                else
                {
                    switch (moveIndex)
                    {
                        case 0: bgColor = global.Palette_DarkestPale; break;
                        case 1: bgColor = global.Palette_Pale; break;
                        case 2: bgColor = global.Palette_DarkPale; break;
                        default: bgColor = global.Palette_DarkestPale; break;
                    }
                }

                DrawBeveledBackground(spriteBatch, pixel, visualRect, bgColor);
                Color? tint = (!btn.IsEnabled || !canAfford) ? global.Palette_DarkShadow : (Color?)null;
                btn.Draw(spriteBatch, btn.Font, gameTime, transform, false, 0f, snappedOffsetY, tint);
            }

            private void DrawInfoBoxContent(SpriteBatch spriteBatch, Rectangle bounds, MoveData move, Global global)
            {
                var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
                var tertiaryFont = ServiceLocator.Get<Core>().TertiaryFont;

                int startX = bounds.X + 4;
                int startY = bounds.Y + 1;

                // Layout Tuning
                int rowSpacing = 8;
                int pairSpacing = 5;
                int labelValueGap = (int)tertiaryFont.MeasureString(" ").Width;

                int currentX = startX;
                int currentY = startY;

                string name = move.MoveName.ToUpper();
                string desc = move.Description;
                string powTxt = move.Power > 0 ? move.Power.ToString() : "--";
                string accTxt = move.Accuracy > 0 ? $"{move.Accuracy}%" : "--";
                string mnaTxt = move.ManaCost.ToString();
                string useTxt = GetStatShortName(move.OffensiveStat);

                void DrawPair(string label, string val)
                {
                    spriteBatch.DrawStringSnapped(tertiaryFont, label, new Vector2(currentX, currentY + 1), global.Palette_Fruit);
                    currentX += (int)tertiaryFont.MeasureString(label).Width + labelValueGap;

                    spriteBatch.DrawStringSnapped(secondaryFont, val, new Vector2(currentX, currentY), global.Palette_DarkSun);
                    currentX += (int)secondaryFont.MeasureString(val).Width + pairSpacing;
                }

                DrawPair("POW", powTxt);
                DrawPair("ACC", accTxt);
                DrawPair("MNA", mnaTxt);
                DrawPair("USE", useTxt);

                // Name
                currentX = startX;
                currentY += (rowSpacing - 2);

                spriteBatch.DrawStringSnapped(secondaryFont, name, new Vector2(currentX, currentY), global.Palette_Sun);

                // Description
                currentY += rowSpacing;
                float maxWidth = bounds.Width - 8;

                // --- Centering Logic ---
                var lines = new List<List<(string Text, Color Color)>>();
                var currentLine = new List<(string Text, Color Color)>();
                float currentLineWidth = 0f;
                lines.Add(currentLine);

                var parts = Regex.Split(desc, @"(\[.*?\]|\s+)");
                Color currentColor = global.Palette_Sun;

                foreach (var part in parts)
                {
                    if (string.IsNullOrEmpty(part)) continue;

                    if (part.StartsWith("[") && part.EndsWith("]"))
                    {
                        string tag = part.Substring(1, part.Length - 2);
                        if (tag == "/" || tag.Equals("default", StringComparison.OrdinalIgnoreCase))
                            currentColor = global.Palette_Sun;
                        else
                            currentColor = global.GetNarrationColor(tag);
                    }
                    else
                    {
                        string textPart = part.ToUpper();
                        Vector2 size = tertiaryFont.MeasureString(textPart);

                        if (string.IsNullOrWhiteSpace(textPart))
                        {
                            if (textPart.Contains("\n"))
                            {
                                lines.Add(new List<(string, Color)>());
                                currentLine = lines.Last();
                                currentLineWidth = 0;
                            }
                            else
                            {
                                if (currentLineWidth + size.X > maxWidth)
                                {
                                    lines.Add(new List<(string, Color)>());
                                    currentLine = lines.Last();
                                    currentLineWidth = 0;
                                }
                                else
                                {
                                    currentLine.Add((textPart, currentColor));
                                    currentLineWidth += size.X;
                                }
                            }
                            continue;
                        }

                        if (currentLineWidth + size.X > maxWidth)
                        {
                            lines.Add(new List<(string, Color)>());
                            currentLine = lines.Last();
                            currentLineWidth = 0;
                        }

                        currentLine.Add((textPart, currentColor));
                        currentLineWidth += size.X;
                    }
                }

                // Calculate Vertical Center
                int totalHeight = lines.Count * tertiaryFont.LineHeight;
                int availableHeight = bounds.Bottom - currentY - 2;
                int startDrawY = currentY + (availableHeight - totalHeight) / 2;

                if (startDrawY < currentY) startDrawY = currentY;

                foreach (var line in lines)
                {
                    if (startDrawY + tertiaryFont.LineHeight > bounds.Bottom) break;

                    float lineWidth = 0;
                    foreach (var item in line) lineWidth += tertiaryFont.MeasureString(item.Text).Width;

                    float lineX = startX + (maxWidth - lineWidth) / 2f;

                    foreach (var item in line)
                    {
                        spriteBatch.DrawStringSnapped(tertiaryFont, item.Text, new Vector2(lineX, startDrawY), item.Color);
                        lineX += tertiaryFont.MeasureString(item.Text).Width;
                    }
                    startDrawY += tertiaryFont.LineHeight;
                }
            }

            private string GetStatShortName(OffensiveStatType stat)
            {
                return stat switch
                {
                    OffensiveStatType.Strength => "STR",
                    OffensiveStatType.Intelligence => "INT",
                    OffensiveStatType.Tenacity => "TEN",
                    OffensiveStatType.Agility => "AGI",
                    _ => "---"
                };
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
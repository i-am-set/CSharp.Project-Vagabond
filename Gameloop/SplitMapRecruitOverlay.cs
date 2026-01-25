using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Scenes;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;


namespace ProjectVagabond.UI
{
    public class SplitMapRecruitOverlay
    {
        public bool IsOpen { get; private set; } = false;
        public event Action? OnRecruitComplete; // Fired when a choice is made or skipped

        private readonly Global _global;
        private readonly SpriteManager _spriteManager;
        private readonly Core _core;
        private readonly GameState _gameState;
        private readonly HapticsManager _hapticsManager;
        private readonly ItemTooltipRenderer _tooltipRenderer;

        private Button _selectButton;
        private Button _skipButton;
        private ConfirmationDialog _confirmationDialog;

        // Candidates
        private List<PartyMember> _candidates = new List<PartyMember>();
        private List<Button> _candidateButtons = new List<Button>();
        private List<Rectangle> _candidatePanelAreas = new List<Rectangle>();
        private int _selectedCandidateIndex = -1;

        // Hover State for Sub-elements
        private int _hoveredInternalCandidateIndex = -1;
        private int _hoveredEquipSlotIndex = -1; // 0=Weapon, 1=Relic
        private int _hoveredSpellSlotIndex = -1; // 0-3
        private object? _hoveredItemData; // Data for the info panel

        // Tooltip State
        private float _tooltipTimer = 0f;
        private object? _lastHoveredItemData = null;

        // Tooltip Animation
        private UIAnimator _tooltipAnimator;
        private const float TOOLTIP_ANIM_DURATION_IN = 0.25f; // Slower, smoother entry
        private const float TOOLTIP_ANIM_DURATION_OUT = 0.0f; // Instant exit (No animation)

        // Layout Constants
        private const float WORLD_Y_OFFSET = 600f;
        private const int BUTTON_HEIGHT = 15;
        private const int PANEL_WIDTH = 76;
        private const int PANEL_HEIGHT = 135;
        private const int SPACE_WIDTH = 5;
        private const int TOOLTIP_WIDTH = 120; // Fixed width for the tooltip
        private const int SCREEN_EDGE_MARGIN = 6; // Minimum distance from screen edges

        // --- ANIMATION TUNING ---
        private const float EQUIP_FLOAT_SPEED = 2.5f;
        private const float EQUIP_FLOAT_AMPLITUDE = 0.5f;
        private const float EQUIP_ROTATION_SPEED = 2.0f;
        private const float EQUIP_ROTATION_AMOUNT = 0.05f;
        private const float HOVER_POP_SPEED = 12.0f;

        // Track hover timers for each slot (Key: "{CandidateIndex}_{SlotType}")
        private readonly Dictionary<string, float> _equipSlotHoverTimers = new Dictionary<string, float>();

        // Tuning
        private Color TOOLTIP_BG_COLOR;

        private static readonly Random _rng = new Random();

        // Hop Animation Controllers (One per candidate)
        private readonly List<SpriteHopAnimationController> _hopControllers = new List<SpriteHopAnimationController>();

        public SplitMapRecruitOverlay(GameScene parentScene)
        {
            _core = ServiceLocator.Get<Core>();
            _global = ServiceLocator.Get<Global>();
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _gameState = ServiceLocator.Get<GameState>();
            _hapticsManager = ServiceLocator.Get<HapticsManager>();
            _tooltipRenderer = ServiceLocator.Get<ItemTooltipRenderer>();

            // Initialize Tooltip Animator
            _tooltipAnimator = new UIAnimator
            {
                EntryStyle = EntryExitStyle.Zoom, // Use Zoom for smooth scaling
                DurationIn = TOOLTIP_ANIM_DURATION_IN,
                DurationOut = TOOLTIP_ANIM_DURATION_OUT
            };

            // Initialize Tunable Colors
            TOOLTIP_BG_COLOR = _global.Palette_DarkShadow;

            _confirmationDialog = new ConfirmationDialog(parentScene);

            // Select Button (Primary)
            _selectButton = new Button(Rectangle.Empty, "SELECT", font: _core.SecondaryFont)
            {
                CustomDefaultTextColor = _global.GameTextColor,
                CustomHoverTextColor = _global.ButtonHoverColor,
                UseScreenCoordinates = true
            };
            _selectButton.OnClick += ConfirmSelection;

            // Skip Button (Secondary)
            _skipButton = new Button(Rectangle.Empty, "SKIP", font: _core.TertiaryFont)
            {
                CustomDefaultTextColor = _global.GameTextColor,
                CustomHoverTextColor = _global.ButtonHoverColor,
                UseScreenCoordinates = true
            };
            _skipButton.OnClick += RequestSkip;
        }

        public void GenerateNewCandidates()
        {
            _selectedCandidateIndex = -1;
            _candidates.Clear();
            _equipSlotHoverTimers.Clear(); // Reset animation timers

            // 1. Get all potential members
            var allMemberIds = BattleDataCache.PartyMembers.Keys.ToList();

            // 2. Filter out current party members and past members
            var validIds = allMemberIds.Where(id =>
                !_gameState.PlayerState.PastMemberIds.Contains(id) &&
                !_gameState.PlayerState.Party.Any(m => m.Name == BattleDataCache.PartyMembers[id].Name)
            ).ToList();

            // 3. Always try to select 3
            int count = 3;
            count = Math.Min(count, validIds.Count); // Cap at available

            if (count > 0)
            {
                // Shuffle
                var shuffled = validIds.OrderBy(x => _rng.Next()).Take(count).ToList();
                foreach (var id in shuffled)
                {
                    var member = PartyMemberFactory.CreateMember(id);
                    if (member != null)
                    {
                        _candidates.Add(member);
                    }
                }
            }

            // Reset animations for the new set of candidates
            _hopControllers.Clear();
            for (int i = 0; i < _candidates.Count; i++)
            {
                _hopControllers.Add(new SpriteHopAnimationController());
            }
        }

        public void Show()
        {
            IsOpen = true;
            _tooltipTimer = 0f;
            _lastHoveredItemData = null;
            _tooltipAnimator.Reset();
            RebuildLayout();
        }

        public void Hide()
        {
            IsOpen = false;
            _confirmationDialog.Hide();
            _tooltipAnimator.Reset();
        }

        private void RebuildLayout()
        {
            _candidateButtons.Clear();
            _candidatePanelAreas.Clear();

            int screenBottom = (int)WORLD_Y_OFFSET + Global.VIRTUAL_HEIGHT;

            // Select Button - Centered
            var selectFont = _selectButton.Font ?? _core.SecondaryFont;
            var selectSize = selectFont.MeasureString("SELECT");
            int selectWidth = (int)selectSize.Width + 16;

            int buttonY = screenBottom - BUTTON_HEIGHT - 2;

            int selectX = (Global.VIRTUAL_WIDTH - selectWidth) / 2;

            _selectButton.Bounds = new Rectangle(selectX, buttonY, selectWidth, BUTTON_HEIGHT);

            // Skip Button - Bottom Right
            var skipFont = _skipButton.Font ?? _core.TertiaryFont;
            var skipSize = skipFont.MeasureString("SKIP");
            int skipWidth = (int)skipSize.Width + 16;
            int skipX = Global.VIRTUAL_WIDTH - skipWidth - 10;

            _skipButton.Bounds = new Rectangle(skipX, buttonY, skipWidth, BUTTON_HEIGHT);

            // Panels
            int count = _candidates.Count;
            if (count == 0) return;

            int totalWidth = (count * PANEL_WIDTH); // Tightly packed
            int startX = (Global.VIRTUAL_WIDTH - totalWidth) / 2;

            int panelY = (int)WORLD_Y_OFFSET + 24;

            for (int i = 0; i < count; i++)
            {
                var rect = new Rectangle(startX + (i * PANEL_WIDTH), panelY, PANEL_WIDTH, PANEL_HEIGHT);
                _candidatePanelAreas.Add(rect);

                // Create invisible button over the entire panel for selection
                var btn = new Button(rect, "")
                {
                    UseScreenCoordinates = true,
                    EnableHoverSway = false
                };

                int index = i; // Capture for lambda
                btn.OnClick += () => SelectCandidate(index);

                _candidateButtons.Add(btn);
            }
        }

        private void SelectCandidate(int index)
        {
            // Prevent selection if party is full
            if (_gameState.PlayerState.Party.Count >= 4)
            {
                EventBus.Publish(new GameEvents.AlertPublished { Message = "PARTY FULL" });
                _hapticsManager.TriggerShake(5f, 0.1f);
                return;
            }

            if (_selectedCandidateIndex == index)
            {
                // Deselect if clicking same
                _selectedCandidateIndex = -1;
            }
            else
            {
                _selectedCandidateIndex = index;
                // Trigger hop for visual feedback ONLY on selection
                if (index >= 0 && index < _hopControllers.Count)
                {
                    _hopControllers[index].Trigger();
                }
            }
        }

        private void ConfirmSelection()
        {
            if (_selectedCandidateIndex < 0 || _selectedCandidateIndex >= _candidates.Count) return;

            var candidate = _candidates[_selectedCandidateIndex];

            if (_gameState.PlayerState.AddPartyMember(candidate))
            {
                _hapticsManager.TriggerUICompoundShake(_global.ButtonHapticStrength);
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[Palette_Sky]Recruited {candidate.Name}!" });
                OnRecruitComplete?.Invoke();
            }
            else
            {
                EventBus.Publish(new GameEvents.AlertPublished { Message = "PARTY FULL" });
            }
        }

        private void RequestSkip()
        {
            _confirmationDialog.Show(
                "Skip recruitment?",
                new List<Tuple<string, Action>>
                {
                    Tuple.Create("[SKIP", new Action(() => { OnRecruitComplete?.Invoke(); _confirmationDialog.Hide(); })),
                    Tuple.Create("[chighlight]CANCEL", new Action(() => _confirmationDialog.Hide()))
                }
            );
        }

        public void Update(GameTime gameTime, MouseState mouseState, Matrix cameraTransform)
        {
            if (!IsOpen) return;

            if (_confirmationDialog.IsActive)
            {
                _confirmationDialog.Update(gameTime);
                return;
            }

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            foreach (var controller in _hopControllers) controller.Update(gameTime);

            var virtualMousePos = Core.TransformMouse(mouseState.Position);
            var mouseInWorldSpace = Vector2.Transform(virtualMousePos, Matrix.Invert(cameraTransform));
            var worldMouseState = new MouseState((int)mouseInWorldSpace.X, (int)mouseInWorldSpace.Y, mouseState.ScrollWheelValue, mouseState.LeftButton, mouseState.MiddleButton, mouseState.RightButton, mouseState.XButton1, mouseState.XButton2);

            _hoveredInternalCandidateIndex = -1;
            _hoveredSpellSlotIndex = -1;
            _hoveredItemData = null;

            var defaultFont = ServiceLocator.Get<BitmapFont>();
            var secondaryFont = _core.SecondaryFont;

            for (int i = 0; i < _candidates.Count; i++)
            {
                var bounds = _candidatePanelAreas[i];
                if (bounds.Contains(mouseInWorldSpace))
                {
                    _hoveredInternalCandidateIndex = i;
                    var candidate = _candidates[i];

                    int centerX = bounds.Center.X;
                    int currentY = bounds.Y + 4;

                    currentY += (int)defaultFont.MeasureString(candidate.Name.ToUpper()).Height - 2;
                    currentY += 32 + 2 - 6;
                    currentY += 8 + (int)secondaryFont.MeasureString($"{candidate.MaxHP}/{candidate.MaxHP}").Height + 4 - 3;

                    currentY += ((int)secondaryFont.LineHeight + 1) * 4;
                    currentY += 2;

                    int spellButtonWidth = 64;
                    int spellButtonHeight = 8;
                    int spellButtonX = centerX - (spellButtonWidth / 2);

                    for (int s = 0; s < 4; s++)
                    {
                        Rectangle spellRect = new Rectangle(spellButtonX, currentY, spellButtonWidth, spellButtonHeight);
                        if (spellRect.Contains(mouseInWorldSpace))
                        {
                            _hoveredSpellSlotIndex = s;
                            var spell = candidate.Spells[s];
                            if (spell != null)
                                _hoveredItemData = BattleDataCache.Moves.GetValueOrDefault(spell.MoveID);
                        }
                        currentY += spellButtonHeight;
                    }
                }
            }

            if (_hoveredItemData != _lastHoveredItemData)
            {
                _tooltipTimer = 0f;
                _tooltipAnimator.Reset();
            }

            if (_hoveredItemData != null)
            {
                _tooltipTimer += dt;
                ServiceLocator.Get<CursorManager>().SetState(CursorState.Hint);

                if (_tooltipTimer >= ItemTooltipRenderer.TOOLTIP_DELAY)
                {
                    if (!_tooltipAnimator.IsVisible) _tooltipAnimator.Show();
                }
            }
            else
            {
                _tooltipTimer = 0f;
                if (_lastHoveredItemData != null && _tooltipAnimator.IsVisible) _tooltipAnimator.Hide();
            }

            _lastHoveredItemData = _hoveredItemData;
            _tooltipAnimator.Update(dt);

            for (int i = 0; i < _candidateButtons.Count; i++)
            {
                var btn = _candidateButtons[i];
                btn.Update(worldMouseState);
            }

            _selectButton.IsEnabled = _selectedCandidateIndex != -1;
            _selectButton.Update(worldMouseState);
            _skipButton.Update(worldMouseState);
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            if (!IsOpen) return;

            var pixel = ServiceLocator.Get<Texture2D>();
            var secondaryFont = _core.SecondaryFont;
            var defaultFont = ServiceLocator.Get<BitmapFont>();

            // Draw Background
            var bgRect = new Rectangle(0, (int)WORLD_Y_OFFSET, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);
            spriteBatch.DrawSnapped(pixel, bgRect, _global.GameBg);

            // Draw Border
            if (_spriteManager.RestBorderMain != null)
            {
                spriteBatch.DrawSnapped(_spriteManager.RestBorderMain, new Vector2(0, WORLD_Y_OFFSET), Color.White);
            }

            // Title
            string title = "RECRUIT";
            Vector2 titleSize = font.MeasureString(title);
            Vector2 titlePos = new Vector2((Global.VIRTUAL_WIDTH - titleSize.X) / 2, WORLD_Y_OFFSET + 10);
            spriteBatch.DrawStringSnapped(font, title, titlePos, _global.Palette_Sun);

            if (_candidates.Count == 0)
            {
                string emptyText = "NO ONE FOUND...";
                Vector2 emptySize = secondaryFont.MeasureString(emptyText);
                Vector2 emptyPos = new Vector2((Global.VIRTUAL_WIDTH - emptySize.X) / 2, (int)WORLD_Y_OFFSET + 80);
                spriteBatch.DrawStringSnapped(secondaryFont, emptyText, emptyPos, _global.Palette_Shadow);
            }
            else
            {
                for (int i = 0; i < _candidates.Count; i++)
                {
                    DrawRecruitSlot(spriteBatch, i, defaultFont, secondaryFont, gameTime);
                }
            }

            _selectButton.Draw(spriteBatch, secondaryFont, gameTime, Matrix.Identity);

            // Force Tertiary font usage here visually, even though Button handles it
            var tertiaryFont = _core.TertiaryFont;
            _skipButton.Draw(spriteBatch, tertiaryFont, gameTime, Matrix.Identity);

            // --- Draw Info Panel if Hovered AND Timer Met ---
            if (_hoveredItemData != null && _tooltipAnimator.GetVisualState().IsVisible)
            {
                // 1. Calculate Center of Hovered Slot in World Space
                Vector2 worldSlotCenter = GetHoveredSlotCenter();

                // 2. Transform to Screen Space
                Vector2 screenSlotCenter = Vector2.Transform(worldSlotCenter, transform);

                // 3. Get Animation State
                var state = _tooltipAnimator.GetVisualState();

                // 4. Draw Tooltip using the new renderer
                // Pass animated scale and opacity
                _tooltipRenderer.DrawTooltip(spriteBatch, _hoveredItemData, screenSlotCenter, gameTime, state.Scale, state.Opacity);
            }
        }

        private Vector2 GetHoveredSlotCenter()
        {
            if (_hoveredInternalCandidateIndex == -1) return Vector2.Zero;

            var panelRect = _candidatePanelAreas[_hoveredInternalCandidateIndex];
            int centerX = panelRect.Center.X;
            int currentY = panelRect.Y + 4;

            var defaultFont = ServiceLocator.Get<BitmapFont>();
            var secondaryFont = _core.SecondaryFont;

            // Re-simulate layout to find Y
            currentY += defaultFont.LineHeight - 2; // Name
            currentY += 32 + 2 - 6; // Portrait
            currentY += 8 + secondaryFont.LineHeight + 4 - 3; // Health Bar

            currentY += (secondaryFont.LineHeight + 1) * 4; // Stats
            currentY += 2; // Gap

            if (_hoveredSpellSlotIndex != -1)
            {
                int spellButtonHeight = 8;
                int y = currentY + (_hoveredSpellSlotIndex * spellButtonHeight);
                return new Vector2(centerX, y + spellButtonHeight / 2);
            }

            return Vector2.Zero;
        }

        private void DrawRecruitSlot(SpriteBatch spriteBatch, int index, BitmapFont font, BitmapFont secondaryFont, GameTime gameTime)
        {
            var member = _candidates[index];
            var bounds = _candidatePanelAreas[index];
            var button = _candidateButtons[index];
            var hopController = _hopControllers[index];

            int centerX = bounds.Center.X;
            int currentY = bounds.Y + 4;

            bool isSelected = index == _selectedCandidateIndex;

            // Highlight Background if selected or hovered
            if (isSelected)
            {
                var pixel = ServiceLocator.Get<Texture2D>();
                DrawBeveledBackground(spriteBatch, pixel, bounds, _global.Palette_Shadow);
                DrawBeveledBorder(spriteBatch, pixel, bounds, _global.Palette_DarkSun);
            }
            else if (button.IsHovered && button.IsEnabled)
            {
                var pixel = ServiceLocator.Get<Texture2D>();
                DrawBeveledBackground(spriteBatch, pixel, bounds, _global.Palette_Black);
                DrawBeveledBorder(spriteBatch, pixel, bounds, _global.ButtonHoverColor);
            }

            // 1. Name
            string name = member.Name.ToUpper();
            Vector2 nameSize = font.MeasureString(name);
            Vector2 namePos = new Vector2(centerX - nameSize.X / 2, currentY);
            currentY += (int)nameSize.Y - 2;

            // 2. Portrait
            if (_spriteManager.PlayerMasterSpriteSheet != null)
            {
                int portraitIndex = member.PortraitIndex;
                PlayerSpriteType type = (isSelected || (button.IsHovered && button.IsEnabled))
                    ? PlayerSpriteType.Alt
                    : PlayerSpriteType.Normal;

                var sourceRect = _spriteManager.GetPlayerSourceRect(portraitIndex, type);
                float hopOffset = hopController.GetOffset(true);

                float bobSpeed = 2.5f;
                float bobAmp = 0.5f;

                if (isSelected)
                {
                    bobSpeed = 5.0f;
                    bobAmp = 1.0f;
                }
                else if (button.IsHovered && button.IsEnabled)
                {
                    bobSpeed = 5.0f;
                    bobAmp = 0.5f;
                }

                float phase = index * 0.7f;
                float bob = MathF.Sin((float)gameTime.TotalGameTime.TotalSeconds * bobSpeed + phase) * bobAmp;
                hopOffset += bob;

                Vector2 portraitPos = new Vector2(centerX - 16, currentY + hopOffset);
                spriteBatch.DrawSnapped(_spriteManager.PlayerMasterSpriteSheet, portraitPos, sourceRect, Color.White);
            }

            spriteBatch.DrawStringSnapped(font, name, namePos, isSelected ? _global.Palette_DarkSun : _global.Palette_Sun);

            currentY += 32 + 2 - 6;

            // 3. Health Bar
            if (_spriteManager.InventoryPlayerHealthBarEmpty != null)
            {
                int barX = centerX - (_spriteManager.InventoryPlayerHealthBarEmpty.Width / 2);
                spriteBatch.DrawSnapped(_spriteManager.InventoryPlayerHealthBarEmpty, new Vector2(barX, currentY), Color.White);

                if (_spriteManager.InventoryPlayerHealthBarFull != null)
                {
                    spriteBatch.DrawSnapped(_spriteManager.InventoryPlayerHealthBarFull, new Vector2(barX + 1, currentY), Color.White);
                }

                string hpValText = $"{member.MaxHP}/{member.MaxHP}";
                string hpSuffix = " HP";
                Vector2 valSize = secondaryFont.MeasureString(hpValText);
                float totalHpWidth = valSize.X + secondaryFont.MeasureString(hpSuffix).Width;
                float hpTextX = centerX - (totalHpWidth / 2f);
                float hpTextY = currentY + 7;

                spriteBatch.DrawStringSnapped(secondaryFont, hpValText, new Vector2(hpTextX, hpTextY), _global.Palette_Sun);
                spriteBatch.DrawStringSnapped(secondaryFont, hpSuffix, new Vector2(hpTextX + valSize.X, hpTextY), _global.Palette_DarkSun);

                currentY += 8 + (int)valSize.Y + 4 - 3;
            }

            // 4. Stats
            string[] statLabels = { "STR", "INT", "TEN", "AGI" };
            string[] statKeys = { "Strength", "Intelligence", "Tenacity", "Agility" };
            int statBlockStartX = centerX - 30;

            for (int s = 0; s < 4; s++)
            {
                int baseStat = member.GetType().GetProperty(statKeys[s])?.GetValue(member) as int? ?? 0;
                int bonus = GetStatBonus(member, statKeys[s]);
                int rawTotal = baseStat + bonus;

                spriteBatch.DrawStringSnapped(secondaryFont, statLabels[s], new Vector2(statBlockStartX, currentY), _global.Palette_DarkSun);

                if (_spriteManager.InventoryStatBarEmpty != null)
                {
                    float barX = statBlockStartX + 19;
                    float barYOffset = (s == 1 || s == 3) ? 0.5f : 0f;
                    float barY = currentY + (secondaryFont.LineHeight - 3) / 2f + barYOffset;

                    spriteBatch.DrawSnapped(_spriteManager.InventoryStatBarEmpty, new Vector2(barX, barY), Color.White);

                    if (_spriteManager.InventoryStatBarFull != null)
                    {
                        int whiteBarPoints;
                        int coloredBarPoints;
                        Color coloredBarColor;

                        // UPDATED: Scale for 10-unit max
                        if (bonus > 0)
                        {
                            whiteBarPoints = Math.Clamp(baseStat, 1, 10);
                            int totalPoints = Math.Clamp(rawTotal, 1, 10);
                            coloredBarPoints = totalPoints - whiteBarPoints;
                            coloredBarColor = _global.StatColor_Increase * 0.5f;
                        }
                        else if (bonus < 0)
                        {
                            whiteBarPoints = Math.Clamp(rawTotal, 1, 10);
                            int basePoints = Math.Clamp(baseStat, 1, 10);
                            coloredBarPoints = basePoints - whiteBarPoints;
                            coloredBarColor = _global.StatColor_Decrease * 0.5f;
                        }
                        else
                        {
                            whiteBarPoints = Math.Clamp(rawTotal, 1, 10);
                            coloredBarPoints = 0;
                            coloredBarColor = Color.White;
                        }

                        // Multiply width by 4 instead of 2
                        if (whiteBarPoints > 0)
                        {
                            var srcBase = new Rectangle(0, 0, whiteBarPoints * 4, 3);
                            spriteBatch.DrawSnapped(_spriteManager.InventoryStatBarFull, new Vector2(barX, barY), srcBase, Color.White);
                        }

                        if (coloredBarPoints > 0)
                        {
                            var srcColor = new Rectangle(0, 0, coloredBarPoints * 4, 3);
                            spriteBatch.DrawSnapped(_spriteManager.InventoryStatBarFull, new Vector2(barX + whiteBarPoints * 4, barY), srcColor, coloredBarColor);
                        }

                        if (rawTotal > 10) // Was 20
                        {
                            int excessValue = rawTotal - 10;
                            Color textColor = bonus > 0 ? _global.StatColor_Increase * 0.5f : (bonus < 0 ? _global.StatColor_Decrease * 0.5f : _global.Palette_Sun);
                            string excessText = $"+{excessValue}";
                            Vector2 textSize = secondaryFont.MeasureString(excessText);
                            float textX = (barX + 40) - textSize.X;
                            Vector2 textPos = new Vector2(textX, currentY);
                            spriteBatch.DrawSnapped(ServiceLocator.Get<Texture2D>(), new Rectangle((int)textPos.X - 1, (int)textPos.Y, (int)textSize.X + 2, (int)textSize.Y), _global.Palette_Black);
                            spriteBatch.DrawStringOutlinedSnapped(secondaryFont, excessText, textPos, textColor, _global.Palette_Black);
                        }
                    }
                }
                currentY += (int)secondaryFont.LineHeight + 1;
            }

            // 5. Spells
            currentY += 2;
            int spellButtonWidth = 64;
            int spellButtonHeight = 8;
            int spellButtonX = centerX - (spellButtonWidth / 2);

            for (int s = 0; s < 4; s++)
            {
                Rectangle spellRect = new Rectangle(spellButtonX, currentY, spellButtonWidth, spellButtonHeight);
                if (spellRect.Contains(Core.TransformMouse(Mouse.GetState().Position)))
                {
                    _hoveredSpellSlotIndex = s;
                    var spell = member.Spells[s];
                    if (spell != null)
                        _hoveredItemData = BattleDataCache.Moves.GetValueOrDefault(spell.MoveID);
                }
                currentY += spellButtonHeight;
            }
        }

        private int GetStatBonus(PartyMember member, string statName)
        {
            // Calculate bonus from Global Relics
            int bonus = 0;
            foreach (var relicId in _gameState.PlayerState.GlobalRelics)
            {
                if (BattleDataCache.Relics.TryGetValue(relicId, out var r))
                {
                    if (r.StatModifiers.TryGetValue(statName, out int val)) bonus += val;
                }
            }
            return bonus;
        }

        public void DrawDialogOverlay(SpriteBatch spriteBatch)
        {
            if (_confirmationDialog.IsActive)
            {
                _confirmationDialog.DrawOverlay(spriteBatch);
            }
        }

        public void DrawDialogContent(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            if (_confirmationDialog.IsActive)
            {
                // Draw in screen space (Matrix.Identity)
                _confirmationDialog.DrawContent(spriteBatch, font, gameTime, Matrix.Identity);
            }

            // Draw Narrator if active
            // (Recruit overlay doesn't use narrator currently, but kept for consistency)
        }

        private void DrawBeveledBorder(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color color)
        {
            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Top, rect.Width, 1), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Bottom - 1, rect.Width, 1), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Top, 1, rect.Height), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - 1, rect.Top, 1, rect.Height), color);

            // Corners (1x1 pixels)
            // Top-Left
            spriteBatch.DrawSnapped(pixel, new Vector2(rect.X + 1, rect.Y + 1), color);
            // Top-Right
            spriteBatch.DrawSnapped(pixel, new Vector2(rect.Right - 2, rect.Y + 1), color);
            // Bottom-Left
            spriteBatch.DrawSnapped(pixel, new Vector2(rect.X + 1, rect.Bottom - 2), color);
            // Bottom-Right
            spriteBatch.DrawSnapped(pixel, new Vector2(rect.Right - 2, rect.Bottom - 2), color);
        }

        private void DrawBeveledBackground(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color color)
        {
            // 1. Top Row (Y+1): X+2 to W-4
            spriteBatch.DrawSnapped(pixel, new Rectangle(rect.X + 2, rect.Y + 1, rect.Width - 4, 1), color);

            // 2. Bottom Row (Bottom-2): X+2 to W-4
            spriteBatch.DrawSnapped(pixel, new Rectangle(rect.X + 2, rect.Bottom - 2, rect.Width - 4, 1), color);

            // 3. Middle Block (Y+2 to Bottom-3): X+1 to W-2
            spriteBatch.DrawSnapped(pixel, new Rectangle(rect.X + 1, rect.Y + 2, rect.Width - 2, rect.Height - 4), color);
        }
    }
}

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
using static ProjectVagabond.Battle.Abilities.InflictStatusStunAbility;

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
        private const float TOOLTIP_DELAY = 0.0f; // Tunable delay

        // Tooltip Animation
        private UIAnimator _tooltipAnimator;
        private const float TOOLTIP_ANIM_DURATION_IN = 0.05f; // Match LootScreen
        private const float TOOLTIP_ANIM_DURATION_OUT = 0.1f;

        // Layout Constants
        private const float WORLD_Y_OFFSET = 600f;
        private const int BUTTON_HEIGHT = 15;
        private const int PANEL_WIDTH = 76;
        private const int PANEL_HEIGHT = 135;
        private const int SPACE_WIDTH = 5;
        private const int TOOLTIP_WIDTH = 120; // Fixed width for the tooltip
        private const int SCREEN_EDGE_MARGIN = 6; // Minimum distance from screen edges

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
                EntryStyle = EntryExitStyle.Pop,
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
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[palette_blue]Recruited {candidate.Name}!" });
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

            // Update Hops
            foreach (var controller in _hopControllers) controller.Update(gameTime);

            // Transform mouse
            var virtualMousePos = Core.TransformMouse(mouseState.Position);
            var mouseInWorldSpace = Vector2.Transform(virtualMousePos, Matrix.Invert(cameraTransform));
            var worldMouseState = new MouseState((int)mouseInWorldSpace.X, (int)mouseInWorldSpace.Y, mouseState.ScrollWheelValue, mouseState.LeftButton, mouseState.MiddleButton, mouseState.RightButton, mouseState.XButton1, mouseState.XButton2);

            // --- HIT TEST LOGIC FOR SUB-ELEMENTS ---
            _hoveredInternalCandidateIndex = -1;
            _hoveredEquipSlotIndex = -1;
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

                    // Name
                    string name = candidate.Name.ToUpper();
                    Vector2 nameSize = defaultFont.MeasureString(name);
                    currentY += (int)nameSize.Y - 2;

                    // Portrait
                    currentY += 32 + 2 - 6;

                    // Health Bar
                    string hpValText = $"{candidate.MaxHP}/{candidate.MaxHP}";
                    Vector2 valSize = secondaryFont.MeasureString(hpValText);
                    currentY += 8 + (int)valSize.Y + 4 - 3;

                    // Equip Slots
                    int slotSize = 16;
                    int gap = 4;
                    // 2 slots: Weapon, Relic
                    int equipStartX = centerX - ((slotSize * 2 + gap * 1) / 2);

                    // Check Weapon Slot
                    Rectangle weaponRect = new Rectangle(equipStartX, currentY, slotSize, slotSize);
                    if (weaponRect.Contains(mouseInWorldSpace))
                    {
                        // Only allow hover if item exists
                        if (!string.IsNullOrEmpty(candidate.EquippedWeaponId))
                        {
                            _hoveredEquipSlotIndex = 0;
                            _hoveredItemData = BattleDataCache.Weapons.GetValueOrDefault(candidate.EquippedWeaponId);
                        }
                    }

                    // Check Relic Slot
                    Rectangle relicRect = new Rectangle(equipStartX + slotSize + gap, currentY, slotSize, slotSize);
                    if (relicRect.Contains(mouseInWorldSpace))
                    {
                        // Only allow hover if item exists
                        if (!string.IsNullOrEmpty(candidate.EquippedRelicId))
                        {
                            _hoveredEquipSlotIndex = 1;
                            _hoveredItemData = BattleDataCache.Relics.GetValueOrDefault(candidate.EquippedRelicId);
                        }
                    }

                    currentY += slotSize + 6 - 5;

                    // Stats (4 lines)
                    currentY += ((int)secondaryFont.LineHeight + 1) * 4;

                    // Spells
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

            // --- TOOLTIP ANIMATION LOGIC ---
            if (_hoveredItemData != _lastHoveredItemData)
            {
                _tooltipTimer = 0f;
            }

            if (_hoveredItemData != null)
            {
                _tooltipTimer += dt;
                // Use Hint cursor for non-clickable info elements
                ServiceLocator.Get<CursorManager>().SetState(CursorState.Hint);

                if (_tooltipTimer >= TOOLTIP_DELAY)
                {
                    if (_hoveredItemData != _lastHoveredItemData)
                    {
                        _tooltipAnimator.Reset();
                        _tooltipAnimator.Show();
                    }
                    else if (!_tooltipAnimator.IsVisible)
                    {
                        _tooltipAnimator.Show();
                    }
                }
            }
            else
            {
                _tooltipTimer = 0f;
                if (_lastHoveredItemData != null && _tooltipAnimator.IsVisible)
                {
                    _tooltipAnimator.Hide();
                }
            }

            _lastHoveredItemData = _hoveredItemData;
            _tooltipAnimator.Update(dt);

            // Update Buttons
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
                // Pass animated scale
                _tooltipRenderer.DrawTooltip(spriteBatch, _hoveredItemData, screenSlotCenter, gameTime, state.Scale, 1.0f);
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

            if (_hoveredEquipSlotIndex != -1)
            {
                int slotSize = 16;
                int gap = 4;
                // 2 slots: Weapon, Relic
                int equipStartX = centerX - ((slotSize * 2 + gap * 1) / 2);
                int x = equipStartX + (_hoveredEquipSlotIndex * (slotSize + gap));
                return new Vector2(x + slotSize / 2, currentY + slotSize / 2);
            }

            currentY += 16 + 6 - 5; // Equip Slots
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
                // Darker background for selection
                spriteBatch.DrawSnapped(pixel, bounds, _global.Palette_Shadow);
                // Selection Border (Yellow)
                DrawRectangleBorder(spriteBatch, pixel, bounds, 1, _global.Palette_Yellow);
            }
            else if (button.IsHovered && button.IsEnabled)
            {
                var pixel = ServiceLocator.Get<Texture2D>();
                spriteBatch.DrawSnapped(pixel, bounds, _global.Palette_DarkShadow);
                // Hover Border (White)
                DrawRectangleBorder(spriteBatch, pixel, bounds, 1, Color.White);
            }

            // 1. Name
            string name = member.Name.ToUpper();
            Vector2 nameSize = font.MeasureString(name);
            Vector2 namePos = new Vector2(centerX - nameSize.X / 2, currentY);
            currentY += (int)nameSize.Y - 2;

            // 2. Portrait
            if (_spriteManager.PlayerPortraitsSpriteSheet != null && _spriteManager.PlayerPortraitSourceRects.Count > 0)
            {
                int portraitIndex = Math.Clamp(member.PortraitIndex, 0, _spriteManager.PlayerPortraitSourceRects.Count - 1);
                var sourceRect = _spriteManager.PlayerPortraitSourceRects[portraitIndex];

                // Apply Hop
                float hopOffset = hopController.GetOffset(true); // Up

                // Add continuous bob if selected
                if (isSelected)
                {
                    float bob = MathF.Sin((float)gameTime.TotalGameTime.TotalSeconds * 5f) * 1.5f;
                    hopOffset += bob;
                }

                var destRect = new Rectangle(centerX - 16, (int)(currentY + hopOffset), 32, 32);

                // Determine Texture: Use Alt if Selected or Hovered
                Texture2D portraitTexture = (isSelected || (button.IsHovered && button.IsEnabled))
                    ? _spriteManager.PlayerPortraitsAltSpriteSheet
                    : _spriteManager.PlayerPortraitsSpriteSheet;

                spriteBatch.DrawSnapped(portraitTexture, destRect, sourceRect, Color.White);
            }

            // Draw Name on top
            Color nameColor = isSelected ? _global.Palette_Yellow : _global.Palette_Sun;
            spriteBatch.DrawStringSnapped(font, name, namePos, nameColor);

            currentY += 32 + 2 - 6;

            // 3. Health Bar (Full)
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
                Vector2 suffixSize = secondaryFont.MeasureString(hpSuffix);
                float totalHpWidth = valSize.X + suffixSize.X;
                float hpTextX = centerX - (totalHpWidth / 2f);
                float hpTextY = currentY + 7;

                spriteBatch.DrawStringSnapped(secondaryFont, hpValText, new Vector2(hpTextX, hpTextY), _global.Palette_Sun);
                // CHANGED: Use DarkSun for HP suffix
                spriteBatch.DrawStringSnapped(secondaryFont, hpSuffix, new Vector2(hpTextX + valSize.X, hpTextY), _global.Palette_DarkSun);

                currentY += 8 + (int)valSize.Y + 4 - 3;
            }

            // 4. Equipment Slots
            int slotSize = 16;
            int gap = 4;
            // 2 slots: Weapon, Relic
            int totalEquipWidth = (slotSize * 2) + (gap * 1);
            int equipStartX = centerX - (totalEquipWidth / 2);

            // Pass hover states based on internal hover calculation
            bool hoveringThisCandidate = _hoveredInternalCandidateIndex == index;
            bool hoverWeapon = hoveringThisCandidate && _hoveredEquipSlotIndex == 0;
            bool hoverRelic = hoveringThisCandidate && _hoveredEquipSlotIndex == 1;

            DrawEquipSlot(spriteBatch, equipStartX, currentY, member.EquippedWeaponId, "Weapon", hoverWeapon);
            DrawEquipSlot(spriteBatch, equipStartX + slotSize + gap, currentY, member.EquippedRelicId, "Relic", hoverRelic);

            currentY += slotSize + 6 - 5;

            // 5. Stats
            string[] statLabels = { "STR", "INT", "TEN", "AGI" };
            string[] statKeys = { "Strength", "Intelligence", "Tenacity", "Agility" };

            // --- CENTERED STAT BLOCK LOGIC ---
            // Label (~17px) + Gap (3px) + Bar (40px) = ~60px total width.
            // CenterX - 30 puts the start 30px left of center.
            int statBlockStartX = centerX - 30;

            for (int s = 0; s < 4; s++)
            {
                // Calculate Base + Bonus
                int baseStat = member.GetType().GetProperty(statKeys[s])?.GetValue(member) as int? ?? 0;
                int bonus = GetStatBonus(member, statKeys[s]);
                int rawTotal = baseStat + bonus;

                // Draw Label
                // CHANGED: Use DarkSun for stat labels
                Color labelColor = _global.Palette_DarkSun;
                spriteBatch.DrawStringSnapped(secondaryFont, statLabels[s], new Vector2(statBlockStartX, currentY), labelColor);

                // Draw Bar Background
                if (_spriteManager.InventoryStatBarEmpty != null)
                {
                    // Fixed offset for bar to ensure vertical alignment
                    float barX = statBlockStartX + 19;
                    float barYOffset = 0f;
                    if (s == 1 || s == 3) barYOffset = 0.5f;
                    float barY = currentY + (secondaryFont.LineHeight - 3) / 2f + barYOffset;

                    spriteBatch.DrawSnapped(_spriteManager.InventoryStatBarEmpty, new Vector2(barX, barY), Color.White);

                    if (_spriteManager.InventoryStatBarFull != null)
                    {
                        int whiteBarPoints;
                        int coloredBarPoints;
                        Color coloredBarColor;

                        if (bonus > 0)
                        {
                            // Base is white, Bonus is Dim Green
                            whiteBarPoints = Math.Clamp(baseStat, 1, 20);
                            int totalPoints = Math.Clamp(rawTotal, 1, 20);
                            coloredBarPoints = totalPoints - whiteBarPoints;
                            coloredBarColor = _global.StatColor_Increase * 0.5f; // Dim Green
                        }
                        else if (bonus < 0)
                        {
                            // Total is white, Penalty (up to Base) is Dim Red
                            whiteBarPoints = Math.Clamp(rawTotal, 1, 20);
                            int basePoints = Math.Clamp(baseStat, 1, 20);
                            coloredBarPoints = basePoints - whiteBarPoints;
                            coloredBarColor = _global.StatColor_Decrease * 0.5f; // Dim Red
                        }
                        else
                        {
                            whiteBarPoints = Math.Clamp(rawTotal, 1, 20);
                            coloredBarPoints = 0;
                            coloredBarColor = Color.White;
                        }

                        int whiteWidth = whiteBarPoints * 2;
                        int coloredWidth = coloredBarPoints * 2;

                        if (whiteWidth > 0)
                        {
                            var srcBase = new Rectangle(0, 0, whiteWidth, 3);
                            spriteBatch.DrawSnapped(_spriteManager.InventoryStatBarFull, new Vector2(barX, barY), srcBase, Color.White);
                        }

                        if (coloredWidth > 0)
                        {
                            var srcColor = new Rectangle(0, 0, coloredWidth, 3);
                            spriteBatch.DrawSnapped(_spriteManager.InventoryStatBarFull, new Vector2(barX + whiteWidth, barY), srcColor, coloredBarColor);
                        }

                        // --- EXCESS TEXT LOGIC ---
                        if (rawTotal > 20)
                        {
                            int excessValue = rawTotal - 20;
                            Color textColor;

                            if (bonus > 0) textColor = _global.StatColor_Increase * 0.5f;
                            else if (bonus < 0) textColor = _global.StatColor_Decrease * 0.5f;
                            else textColor = _global.Palette_Sun;

                            if (excessValue > 0)
                            {
                                string excessText = $"+{excessValue}";
                                Vector2 textSize = secondaryFont.MeasureString(excessText);
                                float textX = (barX + 40) - textSize.X;
                                Vector2 textPos = new Vector2(textX, currentY);
                                var pixel = ServiceLocator.Get<Texture2D>();
                                var bgRect = new Rectangle((int)textPos.X - 1, (int)textPos.Y, (int)textSize.X + 2, (int)textSize.Y);
                                spriteBatch.DrawSnapped(pixel, bgRect, _global.Palette_Black);
                                spriteBatch.DrawStringOutlinedSnapped(secondaryFont, excessText, textPos, textColor, _global.Palette_Black);
                            }
                        }
                    }
                }
                currentY += (int)secondaryFont.LineHeight + 1;
            }

            // 6. Spells
            currentY += 2;
            var tertiaryFont = ServiceLocator.Get<Core>().TertiaryFont;
            int spellButtonWidth = 64;
            int spellButtonHeight = 8;
            int spellButtonX = centerX - (spellButtonWidth / 2);

            for (int s = 0; s < 4; s++)
            {
                var spellEntry = member.Spells[s];
                bool hasSpell = spellEntry != null && !string.IsNullOrEmpty(spellEntry.MoveID);
                string spellName = "EMPTY";
                int frameIndex = 0;
                if (hasSpell)
                {
                    frameIndex = 1;
                    if (BattleDataCache.Moves.TryGetValue(spellEntry.MoveID, out var moveData))
                    {
                        spellName = moveData.MoveName.ToUpper();
                    }
                }

                bool isSpellHovered = hoveringThisCandidate && _hoveredSpellSlotIndex == s;

                // Draw Sprite
                if (_spriteManager.InventorySpellSlotButtonSpriteSheet != null && _spriteManager.InventorySpellSlotButtonSourceRects != null)
                {
                    // Frame 2 is Hover state in sprite sheet
                    int drawFrame = isSpellHovered ? 2 : frameIndex;
                    var sourceRect = _spriteManager.InventorySpellSlotButtonSourceRects[drawFrame];

                    var destRect = new Rectangle(spellButtonX, (int)currentY, spellButtonWidth, spellButtonHeight);
                    spriteBatch.DrawSnapped(_spriteManager.InventorySpellSlotButtonSpriteSheet, destRect, sourceRect, Color.White);

                    if (hasSpell || isSpellHovered)
                    {
                        Vector2 textSize = tertiaryFont.MeasureString(spellName);
                        Vector2 textPos = new Vector2(
                            destRect.X + (destRect.Width - textSize.X) / 2f,
                            destRect.Y + (destRect.Height - textSize.Y) / 2f
                        );
                        textPos = new Vector2(MathF.Round(textPos.X), MathF.Round(textPos.Y));

                        Color textColor = isSpellHovered ? _global.ButtonHoverColor : _global.Palette_Sun;

                        // Use Square Outline
                        spriteBatch.DrawStringSquareOutlinedSnapped(tertiaryFont, spellName, textPos, textColor, _global.Palette_Black);
                    }
                }
                currentY += spellButtonHeight;
            }
        }

        private int GetStatBonus(PartyMember member, string statName)
        {
            int bonus = 0;
            if (!string.IsNullOrEmpty(member.EquippedWeaponId) && BattleDataCache.Weapons.TryGetValue(member.EquippedWeaponId, out var w))
            {
                if (w.StatModifiers.TryGetValue(statName, out int val)) bonus += val;
            }

            if (!string.IsNullOrEmpty(member.EquippedRelicId) && BattleDataCache.Relics.TryGetValue(member.EquippedRelicId, out var r))
            {
                if (r.StatModifiers.TryGetValue(statName, out int val)) bonus += val;
            }

            return bonus;
        }

        private void DrawEquipSlot(SpriteBatch spriteBatch, int x, int y, string? itemId, string type, bool isHovered)
        {
            var destRect = new Rectangle(x, y, 16, 16);
            Vector2 centerPos = new Vector2(x + 8, y + 8);
            Vector2 origin = new Vector2(12, 12);

            if (isHovered)
            {
                var pixel = ServiceLocator.Get<Texture2D>();
                DrawRectangleBorder(spriteBatch, pixel, destRect, 1, Color.White);
            }

            if (!string.IsNullOrEmpty(itemId))
            {
                string path = "";
                if (type == "Weapon" && BattleDataCache.Weapons.TryGetValue(itemId, out var w)) path = $"Sprites/Items/Weapons/{w.WeaponID}";
                else if (type == "Relic" && BattleDataCache.Relics.TryGetValue(itemId, out var r)) path = $"Sprites/Items/Relics/{r.RelicID}";

                if (!string.IsNullOrEmpty(path))
                {
                    var icon = _spriteManager.GetSmallRelicSprite(path);
                    if (icon != null) spriteBatch.DrawSnapped(icon, destRect, Color.White);
                }
            }
            else if (_spriteManager.InventoryEmptySlotSprite != null)
            {
                spriteBatch.DrawSnapped(_spriteManager.InventoryEmptySlotSprite, destRect, Color.White);
            }
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
                _confirmationDialog.DrawContent(spriteBatch, font, gameTime, Matrix.Identity);
            }
        }

        private void DrawRectangleBorder(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, int thickness, Color color)
        {
            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Top, rect.Width, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Bottom - thickness, rect.Width, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Top, thickness, rect.Height), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Top, thickness, rect.Height), color);
        }
    }
}

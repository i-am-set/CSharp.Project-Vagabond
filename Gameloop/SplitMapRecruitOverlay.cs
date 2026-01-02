using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Scenes;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
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
        private int _hoveredEquipSlotIndex = -1; // 0=Weapon, 1=Armor, 2=Relic
        private int _hoveredSpellSlotIndex = -1; // 0-3
        private object? _hoveredItemData; // Data for the info panel

        // Tooltip State
        private float _tooltipTimer = 0f;
        private object? _lastHoveredItemData = null;
        private const float TOOLTIP_DELAY = 0.4f; // Tunable delay

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

        // Animation
        private int _portraitBgFrameIndex = 0;
        private float _portraitBgTimer;
        private float _portraitBgDuration;
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

            // Initialize Tunable Colors
            TOOLTIP_BG_COLOR = _global.Palette_DarkestGray;

            _confirmationDialog = new ConfirmationDialog(parentScene);

            // Select Button (Primary)
            _selectButton = new Button(Rectangle.Empty, "SELECT", font: _core.SecondaryFont)
            {
                CustomDefaultTextColor = _global.Palette_BrightWhite,
                CustomHoverTextColor = _global.Palette_Yellow,
                UseScreenCoordinates = true
            };
            _selectButton.OnClick += ConfirmSelection;

            // Skip Button (Secondary)
            _skipButton = new Button(Rectangle.Empty, "SKIP", font: _core.TertiaryFont)
            {
                CustomDefaultTextColor = _global.Palette_LightGray,
                CustomHoverTextColor = _global.Palette_Red,
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

            // 3. Randomly select 1 to 3
            int count = _rng.Next(1, 4); // 1, 2, or 3
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
            RebuildLayout();
        }

        public void Hide()
        {
            IsOpen = false;
            _confirmationDialog.Hide();
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

                // Disable if party is full
                if (_gameState.PlayerState.Party.Count >= 4)
                {
                    btn.IsEnabled = false;
                }

                _candidateButtons.Add(btn);
            }
        }

        private void SelectCandidate(int index)
        {
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
                _hapticsManager.TriggerCompoundShake(0.75f);
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[palette_teal]Recruited {candidate.Name}!" });
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
                    Tuple.Create("[red]SKIP", new Action(() => { OnRecruitComplete?.Invoke(); _confirmationDialog.Hide(); })),
                    Tuple.Create("CANCEL", new Action(() => _confirmationDialog.Hide()))
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

            // Update Background Animation
            _portraitBgTimer += dt;
            if (_portraitBgTimer >= _portraitBgDuration)
            {
                _portraitBgTimer = 0f;
                _portraitBgDuration = (float)(_rng.NextDouble() * (8.0 - 2.0) + 2.0);
                var frames = _spriteManager.InventorySlotLargeSourceRects;
                if (frames != null && frames.Length > 0) _portraitBgFrameIndex = _rng.Next(frames.Length);
            }

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
                    int equipStartX = centerX - ((slotSize * 3 + gap * 2) / 2);

                    // Check Weapon Slot
                    Rectangle weaponRect = new Rectangle(equipStartX, currentY, slotSize, slotSize);
                    if (weaponRect.Contains(mouseInWorldSpace))
                    {
                        _hoveredEquipSlotIndex = 0;
                        if (!string.IsNullOrEmpty(candidate.EquippedWeaponId))
                            _hoveredItemData = BattleDataCache.Weapons.GetValueOrDefault(candidate.EquippedWeaponId);
                    }

                    // Check Armor Slot
                    Rectangle armorRect = new Rectangle(equipStartX + slotSize + gap, currentY, slotSize, slotSize);
                    if (armorRect.Contains(mouseInWorldSpace))
                    {
                        _hoveredEquipSlotIndex = 1;
                        if (!string.IsNullOrEmpty(candidate.EquippedArmorId))
                            _hoveredItemData = BattleDataCache.Armors.GetValueOrDefault(candidate.EquippedArmorId);
                    }

                    // Check Relic Slot
                    Rectangle relicRect = new Rectangle(equipStartX + (slotSize + gap) * 2, currentY, slotSize, slotSize);
                    if (relicRect.Contains(mouseInWorldSpace))
                    {
                        _hoveredEquipSlotIndex = 2;
                        if (!string.IsNullOrEmpty(candidate.EquippedRelicId))
                            _hoveredItemData = BattleDataCache.Relics.GetValueOrDefault(candidate.EquippedRelicId);
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

            // --- TOOLTIP TIMER LOGIC ---
            if (_hoveredItemData != _lastHoveredItemData)
            {
                _tooltipTimer = 0f;
                _lastHoveredItemData = _hoveredItemData;
            }

            if (_hoveredItemData != null)
            {
                _tooltipTimer += dt;
            }
            else
            {
                _tooltipTimer = 0f;
            }

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

        public void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
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
            spriteBatch.DrawStringSnapped(font, title, titlePos, _global.Palette_BrightWhite);

            if (_candidates.Count == 0)
            {
                string emptyText = "NO ONE FOUND...";
                Vector2 emptySize = secondaryFont.MeasureString(emptyText);
                Vector2 emptyPos = new Vector2((Global.VIRTUAL_WIDTH - emptySize.X) / 2, (int)WORLD_Y_OFFSET + 80);
                spriteBatch.DrawStringSnapped(secondaryFont, emptyText, emptyPos, _global.Palette_Gray);
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
            if (_hoveredItemData != null && _tooltipTimer >= TOOLTIP_DELAY)
            {
                DrawDynamicTooltip(spriteBatch, defaultFont, secondaryFont, gameTime);
            }
        }

        private void DrawDynamicTooltip(SpriteBatch spriteBatch, BitmapFont font, BitmapFont secondaryFont, GameTime gameTime)
        {
            // 1. Calculate Center of Hovered Slot
            Vector2 slotCenter = GetHoveredSlotCenter();

            // 2. Calculate Required Height
            float contentHeight = MeasureContentHeight(font, secondaryFont, _hoveredItemData);
            int panelHeight = (int)Math.Ceiling(contentHeight) + 8; // Add padding

            // 3. Determine Y Position
            // Default: Centered on slot
            float panelY = slotCenter.Y - panelHeight / 2;

            // If Spell, shift up by 16 pixels
            if (_hoveredItemData is MoveData)
            {
                panelY -= 16;
            }

            // 4. Create Rectangle
            int panelWidth = TOOLTIP_WIDTH;
            var infoPanelArea = new Rectangle(
                (int)(slotCenter.X - panelWidth / 2),
                (int)panelY,
                panelWidth,
                panelHeight
            );

            // 5. Clamp to Screen (with Margin)
            // Screen bounds relative to the overlay's world position
            int screenTop = (int)WORLD_Y_OFFSET + SCREEN_EDGE_MARGIN;
            int screenBottom = (int)WORLD_Y_OFFSET + Global.VIRTUAL_HEIGHT - SCREEN_EDGE_MARGIN;
            int screenLeft = SCREEN_EDGE_MARGIN;
            int screenRight = Global.VIRTUAL_WIDTH - SCREEN_EDGE_MARGIN;

            if (infoPanelArea.Top < screenTop) infoPanelArea.Y = screenTop;
            if (infoPanelArea.Bottom > screenBottom) infoPanelArea.Y = screenBottom - infoPanelArea.Height;
            if (infoPanelArea.Left < screenLeft) infoPanelArea.X = screenLeft;
            if (infoPanelArea.Right > screenRight) infoPanelArea.X = screenRight - infoPanelArea.Width;

            // 6. Draw Background (Opaque Darker Gray)
            var pixel = ServiceLocator.Get<Texture2D>();
            spriteBatch.DrawSnapped(pixel, infoPanelArea, TOOLTIP_BG_COLOR);
            DrawRectangleBorder(spriteBatch, pixel, infoPanelArea, 1, Color.White);

            // 7. Draw Content
            if (_hoveredItemData is MoveData moveData)
            {
                string iconPath = $"Sprites/Spells/{moveData.MoveID}";
                int elementId = moveData.OffensiveElementIDs.FirstOrDefault();
                string? fallbackPath = null;
                if (BattleDataCache.Elements.TryGetValue(elementId, out var elementDef))
                {
                    string elName = elementDef.ElementName.ToLowerInvariant();
                    if (elName == "---") elName = "neutral";
                    fallbackPath = $"Sprites/Spells/default_{elName}";
                }

                var iconTexture = _spriteManager.GetItemSprite(iconPath, fallbackPath);
                var iconSilhouette = _spriteManager.GetItemSpriteSilhouette(iconPath, fallbackPath);
                Rectangle? sourceRect = _spriteManager.GetAnimatedIconSourceRect(iconTexture, gameTime);

                DrawSpellInfoPanel(spriteBatch, font, secondaryFont, moveData, iconTexture, iconSilhouette, sourceRect, null, Rectangle.Empty, Vector2.Zero, false, infoPanelArea);
            }
            else
            {
                string name = "";
                string description = "";
                string iconPath = "";
                Dictionary<string, int> stats = new Dictionary<string, int>();

                if (_hoveredItemData is WeaponData w) { name = w.WeaponName; description = w.Description; iconPath = $"Sprites/Items/Weapons/{w.WeaponID}"; stats = w.StatModifiers; }
                else if (_hoveredItemData is ArmorData a) { name = a.ArmorName; description = a.Description; iconPath = $"Sprites/Items/Armor/{a.ArmorID}"; stats = a.StatModifiers; }
                else if (_hoveredItemData is RelicData r) { name = r.RelicName; description = r.Description; iconPath = $"Sprites/Items/Relics/{r.RelicID}"; stats = r.StatModifiers; }

                var iconTexture = _spriteManager.GetItemSprite(iconPath);
                var iconSilhouette = _spriteManager.GetItemSpriteSilhouette(iconPath);

                DrawGenericItemInfoPanel(spriteBatch, font, secondaryFont, name, description, iconTexture, iconSilhouette, stats, infoPanelArea);
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
                int equipStartX = centerX - ((slotSize * 3 + gap * 2) / 2);
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

        private float MeasureContentHeight(BitmapFont font, BitmapFont secondaryFont, object? data)
        {
            if (data == null) return 0;

            const int spriteSize = 16; // Generic
            const int spellSpriteSize = 32; // Spell
            const int gap = 4;
            const int padding = 4;
            float width = TOOLTIP_WIDTH - (padding * 2);

            if (data is MoveData move)
            {
                float h = 2; // Top padding
                h += spellSpriteSize;
                h += font.LineHeight + 2; // Name
                h += (secondaryFont.LineHeight * 3) + (gap * 2); // Stats (3 lines)
                if (move.MakesContact) h += secondaryFont.LineHeight + gap;
                if (!string.IsNullOrEmpty(move.Description))
                {
                    var lines = ParseAndWrapRichText(secondaryFont, move.Description.ToUpper(), width, Color.White);
                    h += lines.Count * secondaryFont.LineHeight;
                }
                return h + padding;
            }
            else
            {
                string name = "";
                string description = "";
                Dictionary<string, int> stats = new Dictionary<string, int>();

                if (data is WeaponData w) { name = w.WeaponName; description = w.Description; stats = w.StatModifiers; }
                else if (data is ArmorData a) { name = a.ArmorName; description = a.Description; stats = a.StatModifiers; }
                else if (data is RelicData r) { name = r.RelicName; description = r.Description; stats = r.StatModifiers; }

                float h = 0;
                h += spriteSize + gap;

                var titleLines = ParseAndWrapRichText(font, name.ToUpper(), width, Color.White);
                h += titleLines.Count * font.LineHeight;

                if (!string.IsNullOrEmpty(description))
                {
                    h += gap;
                    var descLines = ParseAndWrapRichText(secondaryFont, description.ToUpper(), width, Color.White);
                    h += descLines.Count * secondaryFont.LineHeight;
                }

                var (pos, neg) = GetStatModifierLines(stats);
                if (pos.Any() || neg.Any())
                {
                    h += gap;
                    int rows = Math.Max(pos.Count, neg.Count);
                    h += rows * secondaryFont.LineHeight;
                }
                return h + padding;
            }
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
                spriteBatch.DrawSnapped(pixel, bounds, _global.Palette_DarkGray * 0.5f);
                // Selection Border (Yellow)
                DrawRectangleBorder(spriteBatch, pixel, bounds, 1, _global.Palette_Yellow);
            }
            else if (button.IsHovered && button.IsEnabled)
            {
                var pixel = ServiceLocator.Get<Texture2D>();
                spriteBatch.DrawSnapped(pixel, bounds, _global.Palette_DarkGray * 0.2f);
                // Hover Border (White)
                DrawRectangleBorder(spriteBatch, pixel, bounds, 1, Color.White);
            }

            // 1. Name
            string name = member.Name.ToUpper();
            Vector2 nameSize = font.MeasureString(name);
            Vector2 namePos = new Vector2(centerX - nameSize.X / 2, currentY);
            currentY += (int)nameSize.Y - 2;

            // 2. Portrait Background
            if (_spriteManager.InventorySlotLargeSourceRects != null && _spriteManager.InventorySlotLargeSourceRects.Length > 0)
            {
                var largeFrame = _spriteManager.InventorySlotLargeSourceRects[_portraitBgFrameIndex];
                Vector2 bgPos = new Vector2(centerX, currentY + 16);
                Vector2 origin = new Vector2(largeFrame.Width / 2f, largeFrame.Height / 2f);
                spriteBatch.DrawSnapped(_spriteManager.InventorySlotIdleLargeSpriteSheet, bgPos, largeFrame, Color.White, 0f, origin, 1.0f, SpriteEffects.None, 0f);
            }

            // 3. Portrait
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
            Color nameColor = isSelected ? _global.Palette_Yellow : _global.Palette_BrightWhite;
            spriteBatch.DrawStringSnapped(font, name, namePos, nameColor);

            currentY += 32 + 2 - 6;

            // 4. Health Bar (Full)
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

                spriteBatch.DrawStringSnapped(secondaryFont, hpValText, new Vector2(hpTextX, hpTextY), _global.Palette_BrightWhite);
                spriteBatch.DrawStringSnapped(secondaryFont, hpSuffix, new Vector2(hpTextX + valSize.X, hpTextY), _global.Palette_Gray);

                currentY += 8 + (int)valSize.Y + 4 - 3;
            }

            // 5. Equipment Slots
            int slotSize = 16;
            int gap = 4;
            int totalEquipWidth = (slotSize * 3) + (gap * 2);
            int equipStartX = centerX - (totalEquipWidth / 2);

            var slotFrames = _spriteManager.InventorySlotSourceRects;
            Rectangle weaponFrame = Rectangle.Empty;
            Rectangle armorFrame = Rectangle.Empty;
            Rectangle relicFrame = Rectangle.Empty;

            if (slotFrames != null && slotFrames.Length > 0)
            {
                weaponFrame = slotFrames[(_portraitBgFrameIndex + 1) % slotFrames.Length];
                armorFrame = slotFrames[(_portraitBgFrameIndex + 2) % slotFrames.Length];
                relicFrame = slotFrames[(_portraitBgFrameIndex + 3) % slotFrames.Length];
            }

            // Pass hover states based on internal hover calculation
            bool hoveringThisCandidate = _hoveredInternalCandidateIndex == index;
            bool hoverWeapon = hoveringThisCandidate && _hoveredEquipSlotIndex == 0;
            bool hoverArmor = hoveringThisCandidate && _hoveredEquipSlotIndex == 1;
            bool hoverRelic = hoveringThisCandidate && _hoveredEquipSlotIndex == 2;

            DrawEquipSlot(spriteBatch, equipStartX, currentY, member.EquippedWeaponId, "Weapon", weaponFrame, hoverWeapon);
            DrawEquipSlot(spriteBatch, equipStartX + slotSize + gap, currentY, member.EquippedArmorId, "Armor", armorFrame, hoverArmor);
            DrawEquipSlot(spriteBatch, equipStartX + (slotSize + gap) * 2, currentY, member.EquippedRelicId, "Relic", relicFrame, hoverRelic);

            currentY += slotSize + 6 - 5;

            // 6. Stats
            string[] statLabels = { "STR", "INT", "TEN", "AGI" };
            int[] statValues = { member.Strength, member.Intelligence, member.Tenacity, member.Agility };
            int statBarStartX = centerX - ((16 * 3 + 8) / 2);

            for (int s = 0; s < 4; s++)
            {
                spriteBatch.DrawStringSnapped(secondaryFont, statLabels[s], new Vector2(statBarStartX - 3, currentY), _global.Palette_LightGray);

                float labelWidth = secondaryFont.MeasureString(statLabels[s]).Width;
                float barX = statBarStartX - 3 + labelWidth + 3;
                float barY = currentY + (secondaryFont.LineHeight - 3) / 2f;
                if (s == 1 || s == 3) barY += 0.5f;

                if (_spriteManager.InventoryStatBarEmpty != null)
                {
                    spriteBatch.DrawSnapped(_spriteManager.InventoryStatBarEmpty, new Vector2(barX, barY), Color.White);
                    int points = Math.Clamp(statValues[s], 1, 20);
                    int width = points * 2;
                    if (_spriteManager.InventoryStatBarFull != null && width > 0)
                    {
                        var src = new Rectangle(0, 0, width, 3);
                        spriteBatch.DrawSnapped(_spriteManager.InventoryStatBarFull, new Vector2(barX, barY), src, Color.White);
                    }
                }
                currentY += (int)secondaryFont.LineHeight + 1;
            }

            // 7. Spells
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

                        Color textColor = isSpellHovered ? _global.ButtonHoverColor : _global.Palette_BrightWhite;

                        // Use Square Outline
                        spriteBatch.DrawStringSquareOutlinedSnapped(tertiaryFont, spellName, textPos, textColor, _global.Palette_Black);
                    }
                }
                currentY += spellButtonHeight;
            }
        }

        private void DrawEquipSlot(SpriteBatch spriteBatch, int x, int y, string? itemId, string type, Rectangle bgFrame, bool isHovered)
        {
            var destRect = new Rectangle(x, y, 16, 16);
            Vector2 centerPos = new Vector2(x + 8, y + 8);
            Vector2 origin = new Vector2(12, 12);

            if (bgFrame != Rectangle.Empty)
            {
                spriteBatch.DrawSnapped(_spriteManager.InventorySlotIdleSpriteSheet, centerPos, bgFrame, Color.White, 0f, origin, 1.0f, SpriteEffects.None, 0f);
            }

            if (isHovered)
            {
                var pixel = ServiceLocator.Get<Texture2D>();
                DrawRectangleBorder(spriteBatch, pixel, destRect, 1, Color.White);
            }

            if (!string.IsNullOrEmpty(itemId))
            {
                string path = "";
                if (type == "Weapon" && BattleDataCache.Weapons.TryGetValue(itemId, out var w)) path = $"Sprites/Items/Weapons/{w.WeaponID}";
                else if (type == "Armor" && BattleDataCache.Armors.TryGetValue(itemId, out var a)) path = $"Sprites/Items/Armor/{a.ArmorID}";
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

        // --- DUPLICATED HELPER METHODS FROM INVENTORY OVERLAY ---

        private void DrawSpellInfoPanel(SpriteBatch spriteBatch, BitmapFont font, BitmapFont secondaryFont, MoveData move, Texture2D? iconTexture, Texture2D? iconSilhouette, Rectangle? sourceRect, Color? iconTint, Rectangle idleFrame, Vector2 idleOrigin, bool drawBackground, Rectangle infoPanelArea)
        {
            const int spriteSize = 32;
            const int padding = 4;
            const int gap = 2;

            int spriteX = infoPanelArea.X + (infoPanelArea.Width - spriteSize) / 2;
            int spriteY = infoPanelArea.Y + 2;

            float displayScale = 1.0f;
            float bgScale = 1.0f;

            if (drawBackground)
            {
                if (idleFrame != Rectangle.Empty)
                {
                    Vector2 itemCenter = new Vector2(spriteX + spriteSize / 2f, spriteY + spriteSize / 2f);
                    spriteBatch.DrawSnapped(_spriteManager.InventorySlotIdleSpriteSheet, itemCenter, idleFrame, Color.White, 0f, idleOrigin, bgScale, SpriteEffects.None, 0f);
                }
                return;
            }

            Vector2 iconOrigin = new Vector2(16, 16);
            Vector2 drawPos = new Vector2(spriteX + 16, spriteY + 16);

            if (iconSilhouette != null)
            {
                Color mainOutlineColor = _global.ItemOutlineColor_Idle;
                Color cornerOutlineColor = _global.ItemOutlineColor_Idle_Corner;

                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(-1, -1) * displayScale, sourceRect, cornerOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(1, -1) * displayScale, sourceRect, cornerOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(-1, 1) * displayScale, sourceRect, cornerOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(1, 1) * displayScale, sourceRect, cornerOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);

                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(-1, 0) * displayScale, sourceRect, mainOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(1, 0) * displayScale, sourceRect, mainOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(0, -1) * displayScale, sourceRect, mainOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(0, 1) * displayScale, sourceRect, mainOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
            }

            if (iconTexture != null)
            {
                Color tint = iconTint ?? Color.White;
                spriteBatch.DrawSnapped(iconTexture, drawPos, sourceRect, tint, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
            }

            string name = move.MoveName.ToUpper();
            Vector2 nameSize = font.MeasureString(name);

            Vector2 namePos = new Vector2(
                infoPanelArea.X + (infoPanelArea.Width - nameSize.X) / 2f,
                spriteY + spriteSize - (font.LineHeight / 2f) - 2
            );

            spriteBatch.DrawStringOutlinedSnapped(font, name, namePos, _global.Palette_BrightWhite, _global.Palette_Black);

            float currentY = namePos.Y + font.LineHeight + 2;

            float leftLabelX = infoPanelArea.X + 8;
            float leftValueRightX = infoPanelArea.X + 51;

            float rightLabelX = infoPanelArea.X + 59;
            float rightValueRightX = infoPanelArea.X + 112;

            void DrawStatPair(string label, string value, float labelX, float valueRightX, float y, Color valColor)
            {
                spriteBatch.DrawStringSnapped(secondaryFont, label, new Vector2(labelX, y), _global.Palette_Gray);
                float valWidth = secondaryFont.MeasureString(value).Width;
                spriteBatch.DrawStringSnapped(secondaryFont, value, new Vector2(valueRightX - valWidth, y), valColor);
            }

            string powVal = move.Power > 0 ? move.Power.ToString() : "---";
            string accVal = move.Accuracy >= 0 ? $"{move.Accuracy}%" : "---";
            DrawStatPair("POW", powVal, leftLabelX, leftValueRightX, currentY, _global.Palette_White);
            DrawStatPair("ACC", accVal, rightLabelX, rightValueRightX, currentY, _global.Palette_White);
            currentY += secondaryFont.LineHeight + gap;

            string mpVal = move.ManaCost > 0 ? move.ManaCost.ToString() : "0";
            string targetVal = move.Target switch
            {
                TargetType.Single => "SINGL",
                TargetType.SingleAll => "ANY",
                TargetType.Both => "BOTH",
                TargetType.Every => "MULTI",
                TargetType.All => "ALL",
                TargetType.Self => "SELF",
                TargetType.Team => "TEAM",
                TargetType.Ally => "ALLY",
                TargetType.SingleTeam => "S-TEAM",
                TargetType.RandomBoth => "R-BOTH",
                TargetType.RandomEvery => "R-EVRY",
                TargetType.RandomAll => "R-ALL",
                TargetType.None => "NONE",
                _ => "---"
            };
            DrawStatPair("MP ", mpVal, leftLabelX, leftValueRightX, currentY, _global.Palette_LightBlue);
            DrawStatPair("TGT", targetVal, rightLabelX, rightValueRightX, currentY, _global.Palette_White);
            currentY += secondaryFont.LineHeight + gap;

            string offStatVal = move.OffensiveStat switch
            {
                OffensiveStatType.Strength => "STR",
                OffensiveStatType.Intelligence => "INT",
                OffensiveStatType.Tenacity => "TEN",
                OffensiveStatType.Agility => "AGI",
                _ => "---"
            };

            Color offColor = move.OffensiveStat switch
            {
                OffensiveStatType.Strength => _global.StatColor_Strength,
                OffensiveStatType.Intelligence => _global.StatColor_Intelligence,
                OffensiveStatType.Tenacity => _global.StatColor_Tenacity,
                OffensiveStatType.Agility => _global.StatColor_Agility,
                _ => _global.Palette_White
            };

            string impactVal = move.ImpactType.ToString().ToUpper().Substring(0, Math.Min(4, move.ImpactType.ToString().Length));
            Color impactColor = move.ImpactType == ImpactType.Magical ? _global.Palette_LightBlue : (move.ImpactType == ImpactType.Physical ? _global.Palette_Orange : _global.Palette_Gray);

            DrawStatPair("USE", offStatVal, leftLabelX, leftValueRightX, currentY, offColor);
            DrawStatPair("TYP", impactVal, rightLabelX, rightValueRightX, currentY, impactColor);
            currentY += secondaryFont.LineHeight + gap;

            if (move.MakesContact)
            {
                string contactText = "[MAKES CONTACT]";
                Vector2 contactSize = secondaryFont.MeasureString(contactText);
                Vector2 contactPos = new Vector2(infoPanelArea.X + (infoPanelArea.Width - contactSize.X) / 2f, currentY);
                spriteBatch.DrawStringSnapped(secondaryFont, contactText, contactPos, _global.Palette_Red);
                currentY += secondaryFont.LineHeight + gap;
            }

            string description = move.Description.ToUpper();
            if (!string.IsNullOrEmpty(description))
            {
                float descWidth = infoPanelArea.Width - (padding * 2);
                var descLines = ParseAndWrapRichText(secondaryFont, description, descWidth, _global.Palette_White);

                int maxLines = 8;
                if (descLines.Count > maxLines)
                {
                    descLines = descLines.Take(maxLines).ToList();
                }

                float totalDescHeight = descLines.Count * secondaryFont.LineHeight;

                float bottomPadding = 3f;
                float areaTop = currentY;
                float areaBottom = infoPanelArea.Bottom - bottomPadding;
                float areaHeight = areaBottom - areaTop;

                float startY = areaTop + (areaHeight - totalDescHeight) / 2f;
                if (startY < areaTop) startY = areaTop;

                float lineY = startY;
                foreach (var line in descLines)
                {
                    float lineWidth = 0;
                    foreach (var segment in line)
                    {
                        if (string.IsNullOrWhiteSpace(segment.Text))
                            lineWidth += segment.Text.Length * SPACE_WIDTH;
                        else
                            lineWidth += secondaryFont.MeasureString(segment.Text).Width;
                    }

                    float lineX = infoPanelArea.X + (infoPanelArea.Width - lineWidth) / 2;
                    float currentX = lineX;

                    foreach (var segment in line)
                    {
                        float segWidth;
                        if (string.IsNullOrWhiteSpace(segment.Text))
                        {
                            segWidth = segment.Text.Length * SPACE_WIDTH;
                        }
                        else
                        {
                            segWidth = secondaryFont.MeasureString(segment.Text).Width;
                            spriteBatch.DrawStringSnapped(secondaryFont, segment.Text, new Vector2(currentX, lineY), segment.Color);
                        }
                        currentX += segWidth;
                    }
                    lineY += secondaryFont.LineHeight;
                }
            }
        }

        private void DrawGenericItemInfoPanel(SpriteBatch spriteBatch, BitmapFont font, BitmapFont secondaryFont, string name, string description, Texture2D? iconTexture, Texture2D? iconSilhouette, Dictionary<string, int> stats, Rectangle infoPanelArea)
        {
            const int spriteSize = 16;
            const int gap = 4;

            var statLines = GetStatModifierLines(stats);

            int maxTitleWidth = infoPanelArea.Width - (4 * 2);
            var titleLines = ParseAndWrapRichText(font, name, maxTitleWidth, _global.Palette_BrightWhite);
            float totalTitleHeight = titleLines.Count * font.LineHeight;

            float totalDescHeight = 0f;
            List<List<ColoredText>> descLines = new List<List<ColoredText>>();
            if (!string.IsNullOrEmpty(description))
            {
                float descWidth = infoPanelArea.Width - (4 * 2);
                descLines = ParseAndWrapRichText(secondaryFont, description, descWidth, _global.Palette_White);
                totalDescHeight = descLines.Count * secondaryFont.LineHeight;
            }

            float totalStatHeight = Math.Max(statLines.Positives.Count, statLines.Negatives.Count) * secondaryFont.LineHeight;
            float totalContentHeight = spriteSize + gap + totalTitleHeight + (totalDescHeight > 0 ? gap + totalDescHeight : 0) + (totalStatHeight > 0 ? gap + totalStatHeight : 0);

            float currentY = infoPanelArea.Y + (infoPanelArea.Height - totalContentHeight) / 2f;
            currentY -= 22f;

            int spriteX = infoPanelArea.X + (infoPanelArea.Width - spriteSize) / 2;

            float displayScale = 1.0f;
            Vector2 iconOrigin = new Vector2(8, 8);
            Vector2 drawPos = new Vector2(spriteX + 8, currentY + 8);

            if (iconSilhouette != null)
            {
                Color mainOutlineColor = _global.ItemOutlineColor_Idle;
                Color cornerOutlineColor = _global.ItemOutlineColor_Idle_Corner;

                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(-1, -1) * displayScale, null, cornerOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(1, -1) * displayScale, null, cornerOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(-1, 1) * displayScale, null, cornerOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(1, 1) * displayScale, null, cornerOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);

                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(-1, 0) * displayScale, null, mainOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(1, 0) * displayScale, null, mainOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(0, -1) * displayScale, null, mainOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
                spriteBatch.DrawSnapped(iconSilhouette, drawPos + new Vector2(0, 1) * displayScale, null, mainOutlineColor, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
            }

            if (iconTexture != null)
            {
                spriteBatch.DrawSnapped(iconTexture, drawPos, null, Color.White, 0f, iconOrigin, displayScale, SpriteEffects.None, 0f);
            }

            currentY += spriteSize + gap;

            foreach (var line in titleLines)
            {
                float lineWidth = 0;
                foreach (var segment in line)
                {
                    if (string.IsNullOrWhiteSpace(segment.Text))
                        lineWidth += segment.Text.Length * SPACE_WIDTH;
                    else
                        lineWidth += font.MeasureString(segment.Text).Width;
                }

                float lineX = infoPanelArea.X + (infoPanelArea.Width - lineWidth) / 2f;
                float currentX = lineX;

                foreach (var segment in line)
                {
                    float segWidth;
                    if (string.IsNullOrWhiteSpace(segment.Text))
                    {
                        segWidth = segment.Text.Length * SPACE_WIDTH;
                    }
                    else
                    {
                        segWidth = font.MeasureString(segment.Text).Width;
                        spriteBatch.DrawStringSnapped(font, segment.Text, new Vector2(currentX, currentY), segment.Color);
                    }
                    currentX += segWidth;
                }
                currentY += font.LineHeight;
            }

            if (descLines.Any())
            {
                currentY += gap;
                foreach (var line in descLines)
                {
                    float lineWidth = 0;
                    foreach (var segment in line)
                    {
                        if (string.IsNullOrWhiteSpace(segment.Text))
                            lineWidth += segment.Text.Length * SPACE_WIDTH;
                        else
                            lineWidth += secondaryFont.MeasureString(segment.Text).Width;
                    }

                    var lineX = infoPanelArea.X + (infoPanelArea.Width - lineWidth) / 2;
                    float currentX = lineX;

                    foreach (var segment in line)
                    {
                        float segWidth;
                        if (string.IsNullOrWhiteSpace(segment.Text))
                        {
                            segWidth = segment.Text.Length * SPACE_WIDTH;
                        }
                        else
                        {
                            segWidth = secondaryFont.MeasureString(segment.Text).Width;
                            spriteBatch.DrawStringSnapped(secondaryFont, segment.Text, new Vector2(currentX, currentY), segment.Color);
                        }
                        currentX += segWidth;
                    }
                    currentY += secondaryFont.LineHeight;
                }
            }

            if (statLines.Positives.Any() || statLines.Negatives.Any())
            {
                currentY += gap;
                float leftColX = infoPanelArea.X + 8;
                float rightColX = infoPanelArea.X + 64;

                int maxRows = Math.Max(statLines.Positives.Count, statLines.Negatives.Count);

                for (int i = 0; i < maxRows; i++)
                {
                    if (i < statLines.Positives.Count)
                    {
                        var lineParts = ParseAndWrapRichText(secondaryFont, statLines.Positives[i], infoPanelArea.Width / 2, _global.Palette_White);
                        if (lineParts.Count > 0)
                        {
                            var segments = lineParts[0];
                            float currentX = leftColX;
                            foreach (var segment in segments)
                            {
                                float segWidth = string.IsNullOrWhiteSpace(segment.Text) ? segment.Text.Length * SPACE_WIDTH : secondaryFont.MeasureString(segment.Text).Width;
                                spriteBatch.DrawStringSnapped(secondaryFont, segment.Text, new Vector2(currentX, currentY), segment.Color);
                                currentX += segWidth;
                            }
                        }
                    }

                    if (i < statLines.Negatives.Count)
                    {
                        var lineParts = ParseAndWrapRichText(secondaryFont, statLines.Negatives[i], infoPanelArea.Width / 2, _global.Palette_White);
                        if (lineParts.Count > 0)
                        {
                            var segments = lineParts[0];
                            float currentX = rightColX;
                            foreach (var segment in segments)
                            {
                                float segWidth = string.IsNullOrWhiteSpace(segment.Text) ? segment.Text.Length * SPACE_WIDTH : secondaryFont.MeasureString(segment.Text).Width;
                                spriteBatch.DrawStringSnapped(secondaryFont, segment.Text, new Vector2(currentX, currentY), segment.Color);
                                currentX += segWidth;
                            }
                        }
                    }
                    currentY += secondaryFont.LineHeight;
                }
            }
        }

        private List<List<ColoredText>> ParseAndWrapRichText(BitmapFont font, string text, float maxWidth, Color defaultColor)
        {
            var lines = new List<List<ColoredText>>();
            if (string.IsNullOrEmpty(text)) return lines;

            var currentLine = new List<ColoredText>();
            float currentLineWidth = 0f;
            Color currentColor = defaultColor;

            var parts = Regex.Split(text, @"(\[.*?\]|\s+)");

            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part)) continue;

                if (part.StartsWith("[") && part.EndsWith("]"))
                {
                    string tagContent = part.Substring(1, part.Length - 2).ToLowerInvariant();
                    if (tagContent == "/" || tagContent == "default")
                    {
                        currentColor = defaultColor;
                    }
                    else
                    {
                        currentColor = ParseColor(tagContent);
                    }
                }
                else if (part.Contains("\n"))
                {
                    lines.Add(currentLine);
                    currentLine = new List<ColoredText>();
                    currentLineWidth = 0f;
                }
                else
                {
                    bool isWhitespace = string.IsNullOrWhiteSpace(part);
                    float partWidth = isWhitespace ? (part.Length * SPACE_WIDTH) : font.MeasureString(part).Width;

                    if (!isWhitespace && currentLineWidth + partWidth > maxWidth && currentLineWidth > 0)
                    {
                        lines.Add(currentLine);
                        currentLine = new List<ColoredText>();
                        currentLineWidth = 0f;
                    }

                    if (isWhitespace && currentLineWidth == 0)
                    {
                        continue;
                    }

                    currentLine.Add(new ColoredText(part, currentColor));
                    currentLineWidth += partWidth;
                }
            }

            if (currentLine.Count > 0)
            {
                lines.Add(currentLine);
            }

            return lines;
        }

        private Color ParseColor(string colorName)
        {
            string tag = colorName.ToLowerInvariant();

            if (tag == "cstr") return _global.StatColor_Strength;
            if (tag == "cint") return _global.StatColor_Intelligence;
            if (tag == "cten") return _global.StatColor_Tenacity;
            if (tag == "cagi") return _global.StatColor_Agility;

            if (tag == "cpositive") return _global.ColorPositive;
            if (tag == "cnegative") return _global.ColorNegative;
            if (tag == "ccrit") return _global.ColorCrit;
            if (tag == "cimmune") return _global.ColorImmune;
            if (tag == "cctm") return _global.ColorConditionToMeet;
            if (tag == "cetc") return _global.Palette_DarkGray;

            if (tag == "cfire") return _global.ElementColors.GetValueOrDefault(2, Color.White);
            if (tag == "cwater") return _global.ElementColors.GetValueOrDefault(3, Color.White);
            if (tag == "carcane") return _global.ElementColors.GetValueOrDefault(4, Color.White);
            if (tag == "cearth") return _global.ElementColors.GetValueOrDefault(5, Color.White);
            if (tag == "cmetal") return _global.ElementColors.GetValueOrDefault(6, Color.White);
            if (tag == "ctoxic") return _global.ElementColors.GetValueOrDefault(7, Color.White);
            if (tag == "cwind") return _global.ElementColors.GetValueOrDefault(8, Color.White);
            if (tag == "cvoid") return _global.ElementColors.GetValueOrDefault(9, Color.White);
            if (tag == "clight") return _global.ElementColors.GetValueOrDefault(10, Color.White);
            if (tag == "celectric") return _global.ElementColors.GetValueOrDefault(11, Color.White);
            if (tag == "cice") return _global.ElementColors.GetValueOrDefault(12, Color.White);
            if (tag == "cnature") return _global.ElementColors.GetValueOrDefault(13, Color.White);

            if (tag.StartsWith("c"))
            {
                string effectName = tag.Substring(1);
                if (effectName == "poison") return _global.StatusEffectColors.GetValueOrDefault(StatusEffectType.Poison, Color.White);
                if (effectName == "stun") return _global.StatusEffectColors.GetValueOrDefault(StatusEffectType.Stun, Color.White);
                if (effectName == "regen") return _global.StatusEffectColors.GetValueOrDefault(StatusEffectType.Regen, Color.White);
                if (effectName == "dodging") return _global.StatusEffectColors.GetValueOrDefault(StatusEffectType.Dodging, Color.White);
                if (effectName == "burn") return _global.StatusEffectColors.GetValueOrDefault(StatusEffectType.Burn, Color.White);
                if (effectName == "frostbite") return _global.StatusEffectColors.GetValueOrDefault(StatusEffectType.Frostbite, Color.White);
                if (effectName == "silence") return _global.StatusEffectColors.GetValueOrDefault(StatusEffectType.Silence, Color.White);
            }

            switch (tag)
            {
                case "teal": return _global.Palette_Teal;
                case "red": return _global.Palette_Red;
                case "blue": return _global.Palette_LightBlue;
                case "green": return _global.Palette_LightGreen;
                case "yellow": return _global.Palette_Yellow;
                case "orange": return _global.Palette_Orange;
                case "purple": return _global.Palette_LightPurple;
                case "pink": return _global.Palette_Pink;
                case "gray": return _global.Palette_Gray;
                case "white": return _global.Palette_White;
                case "brightwhite": return _global.Palette_BrightWhite;
                case "darkgray": return _global.Palette_DarkGray;
                default: return _global.Palette_White;
            }
        }

        private (List<string> Positives, List<string> Negatives) GetStatModifierLines(Dictionary<string, int> mods)
        {
            var positives = new List<string>();
            var negatives = new List<string>();
            if (mods == null || mods.Count == 0) return (positives, negatives);

            foreach (var kvp in mods)
            {
                if (kvp.Value == 0) continue;
                string colorTag = kvp.Value > 0 ? "[cpositive]" : "[cnegative]";
                string sign = kvp.Value > 0 ? "+" : "";

                // Map full names to abbreviations
                string statName = kvp.Key.ToLowerInvariant() switch
                {
                    "strength" => "STR",
                    "intelligence" => "INT",
                    "tenacity" => "TEN",
                    "agility" => "AGI",
                    "maxhp" => "HP",
                    "maxmana" => "MP",
                    _ => kvp.Key.ToUpper().Substring(0, Math.Min(3, kvp.Key.Length)) // Fallback
                };

                // Pad short names to 3 characters
                if (statName.Length < 3)
                {
                    statName += " ";
                }

                string line = $"{statName} {colorTag}{sign}{kvp.Value}[/]";

                if (kvp.Value > 0)
                {
                    positives.Add(line);
                }
                else
                {
                    negatives.Add(line);
                }
            }
            return (positives, negatives);
        }
    }
}
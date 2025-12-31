using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Particles;
using ProjectVagabond.Transitions;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ProjectVagabond.Battle.UI
{
    public class ActionMenu
    {
        public event Action<MoveData, MoveEntry, BattleCombatant>? OnMoveSelected;
        public event Action? OnItemMenuRequested;
        public event Action? OnSwitchMenuRequested;
        public event Action? OnMovesMenuOpened;
        public event Action? OnMainMenuOpened;
        public event Action? OnFleeRequested;
        public event Action? OnSlot2BackRequested;

        private bool _isVisible;
        public bool IsVisible => _isVisible;

        private BattleCombatant? _player;
        private List<BattleCombatant>? _allCombatants;
        private List<BattleCombatant>? _allTargets;
        private List<Button> _actionButtons = new List<Button>();
        private readonly MoveButton?[] _moveButtons = new MoveButton?[4];
        private List<Button> _secondaryActionButtons = new List<Button>();
        private Button _backButton;
        private Button _slot2BackButton;
        private readonly Global _global;

        public enum MenuState { Main, Moves, Targeting, Tooltip }
        private MenuState _currentState;
        public MenuState CurrentMenuState => _currentState;
        private MoveData? _selectedMove;
        private MoveEntry? _selectedSpellbookEntry;
        public MoveData? SelectedMove => _selectedMove;
        public MoveEntry? SelectedSpellbookEntry => _selectedSpellbookEntry;
        private MoveData? _tooltipMove;
        private bool _useSimpleTooltip;
        public MoveData? HoveredMove { get; private set; }
        private MoveEntry? _hoveredSpellbookEntry;
        public Button? HoveredButton { get; private set; }

        private float _targetingTextAnimTimer = 0f;
        private bool _buttonsInitialized = false;
        private MouseState _previousMouseState;

        // Tooltip Title Scrolling State
        private bool _isTooltipScrollingInitialized = false;
        private float _tooltipScrollPosition = 0f;
        private float _tooltipScrollWaitTimer = 0f;
        private float _tooltipMaxScrollToShowEnd = 0f;
        private enum TooltipScrollState { PausedAtStart, ScrollingToEnd, PausedAtEnd }
        private TooltipScrollState _tooltipScrollState = TooltipScrollState.PausedAtStart;

        // Hover Info Title Scrolling State
        private bool _isHoverInfoScrollingInitialized = false;
        private float _hoverInfoScrollPosition = 0f;
        private float _hoverInfoScrollWaitTimer = 0f;
        private float _hoverInfoMaxScrollToShowEnd = 0f;
        private enum HoverInfoScrollState { PausedAtStart, ScrollingToEnd, PausedAtEnd }
        private HoverInfoScrollState _hoverInfoScrollState = HoverInfoScrollState.PausedAtStart;
        private MoveData? _lastHoveredMoveForScrolling;


        // Scrolling Tuning
        private const float SCROLL_SPEED = 25f;
        private const float SCROLL_PAUSE_DURATION = 1.5f;
        private const int EXTRA_SCROLL_SPACES = 1;
        private static readonly RasterizerState _clipRasterizerState = new RasterizerState { ScissorTestEnable = true };

        // Spam Click Prevention State
        private readonly Queue<float> _clickTimestamps = new Queue<float>();
        private bool _isSpamming = false;
        private float _spamCooldownTimer = 0f;
        private const int SPAM_CLICKS = 3;
        private const float SPAM_WINDOW_SECONDS = 1f;
        private const float SPAM_COOLDOWN_SECONDS = 0.25f;

        // Shared animation timer for main action buttons
        public float SharedSwayTimer { get; private set; } = 0f;
        private bool _wasAnyActionHoveredLastFrame = false;

        // Hover Box Animation
        private MoveButton? _hoveredMoveButton;
        private bool _shouldAttuneButtonPulse = false;

        // --- Moves Menu Layout Constants ---
        private const int MOVE_BUTTON_WIDTH = 116;
        private const int MOVE_BUTTON_HEIGHT = 9;
        private const int MOVE_ROW_SPACING = 1;
        private const int MOVE_ROWS = 4;
        private const int MOVE_BLOCK_HEIGHT = MOVE_ROWS * (MOVE_BUTTON_HEIGHT + MOVE_ROW_SPACING) - MOVE_ROW_SPACING;

        // Debug Bounds
        private Rectangle _tooltipBounds;
        private Rectangle _moveInfoPanelBounds;

        // Text Formatting Tuning
        private const int SPACE_WIDTH = 5;

        public ActionMenu()
        {
            _global = ServiceLocator.Get<Global>();
            _backButton = new Button(Rectangle.Empty, "BACK", enableHoverSway: false) { CustomDefaultTextColor = _global.Palette_Gray };
            _backButton.OnClick += () => {
                if (_currentState == MenuState.Targeting || _currentState == MenuState.Tooltip)
                {
                    SetState(MenuState.Moves);
                }
                else if (_currentState == MenuState.Moves)
                {
                    SetState(MenuState.Main);
                }
            };
            _previousMouseState = Mouse.GetState();
        }

        private void InitializeButtons()
        {
            if (_buttonsInitialized) return;

            var spriteManager = ServiceLocator.Get<SpriteManager>();
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            var defaultFont = ServiceLocator.Get<BitmapFont>(); // Use Default Font for Main Buttons
            var actionIconsSheet = spriteManager.ActionIconsSpriteSheet;
            var actionIconRects = spriteManager.ActionIconSourceRects;

            // Main Menu Buttons - Converted to standard Buttons for 128x12 layout
            // Disabled hover sway, set custom hover text color to White.
            // TextRenderOffset set to (0, 1) to move text down 1 pixel.
            // Font set to defaultFont.
            var actButton = new Button(Rectangle.Empty, "ACT", function: "Act", font: defaultFont, enableHoverSway: false) { CustomHoverTextColor = Color.White, TextRenderOffset = new Vector2(0, 1) };
            var itemButton = new Button(Rectangle.Empty, "ITEM", function: "Item", font: defaultFont, enableHoverSway: false) { CustomHoverTextColor = Color.White, TextRenderOffset = new Vector2(0, 1) };
            var switchButton = new Button(Rectangle.Empty, "SWITCH", function: "Switch", font: defaultFont, enableHoverSway: false) { CustomHoverTextColor = Color.White, TextRenderOffset = new Vector2(0, 1) };

            actButton.OnClick += () => {
                if (_isSpamming) { actButton.TriggerShake(); EventBus.Publish(new GameEvents.AlertPublished { Message = "Spam Prevention" }); return; }
                SetState(MenuState.Moves);
            };
            itemButton.OnClick += () => {
                if (_isSpamming) { itemButton.TriggerShake(); EventBus.Publish(new GameEvents.AlertPublished { Message = "Spam Prevention" }); return; }
                OnItemMenuRequested?.Invoke();
            };

            // Switch Logic
            switchButton.OnClick += () => {
                if (_isSpamming) { switchButton.TriggerShake(); EventBus.Publish(new GameEvents.AlertPublished { Message = "Spam Prevention" }); return; }
                OnSwitchMenuRequested?.Invoke();
            };

            _actionButtons.Add(actButton);
            _actionButtons.Add(itemButton);
            _actionButtons.Add(switchButton);

            // Secondary Action Buttons (Strike, Attune, Stall) - Keep Secondary Font
            var strikeButton = new TextOverImageButton(Rectangle.Empty, "STRIKE", null, font: secondaryFont, iconTexture: actionIconsSheet, iconSourceRect: actionIconRects[0], enableHoverSway: false, customHoverTextColor: Color.White)
            {
                HasRightClickHint = false,
                HasMiddleClickHint = true, // Updated to Middle Click
                TintBackgroundOnHover = false,
                DrawBorderOnHover = false,
                HoverBorderColor = _global.Palette_Red
            };
            strikeButton.OnClick += () => {
                if (_player != null && !string.IsNullOrEmpty(_player.DefaultStrikeMoveID) && BattleDataCache.Moves.TryGetValue(_player.DefaultStrikeMoveID, out var strikeMove))
                {
                    SelectMove(strikeMove, null);
                }
            };
            _secondaryActionButtons.Add(strikeButton);

            var attuneButton = new TextOverImageButton(Rectangle.Empty, "ATTUNE", null, font: secondaryFont, iconTexture: actionIconsSheet, iconSourceRect: actionIconRects[3], enableHoverSway: false, customHoverTextColor: Color.White)
            {
                HasRightClickHint = false,
                HasMiddleClickHint = true, // Updated to Middle Click
                TintBackgroundOnHover = false,
                DrawBorderOnHover = false,
                HoverBorderColor = _global.Palette_Red
            };
            attuneButton.OnClick += () => {
                if (BattleDataCache.Moves.TryGetValue("7", out var attuneMove))
                {
                    SelectMove(attuneMove, null);
                }
            };
            _secondaryActionButtons.Add(attuneButton);

            var stallButton = new TextOverImageButton(Rectangle.Empty, "STALL", null, font: secondaryFont, iconTexture: actionIconsSheet, iconSourceRect: actionIconRects[2], enableHoverSway: false, customHoverTextColor: Color.White)
            {
                HasRightClickHint = false,
                HasMiddleClickHint = true, // Updated to Middle Click
                TintBackgroundOnHover = false,
                DrawBorderOnHover = false,
                HoverBorderColor = _global.Palette_Red
            };
            stallButton.OnClick += () => {
                if (BattleDataCache.Moves.TryGetValue("6", out var stallMove))
                {
                    SelectMove(stallMove, null);
                }
            };
            _secondaryActionButtons.Add(stallButton);

            _backButton.Font = secondaryFont;

            // Initialize Slot 2 Back Button
            _slot2BackButton = new Button(Rectangle.Empty, "BACK", enableHoverSway: false) { CustomDefaultTextColor = _global.Palette_Gray };
            _slot2BackButton.OnClick += () => OnSlot2BackRequested?.Invoke();
            _slot2BackButton.Font = secondaryFont;

            _buttonsInitialized = true;
        }

        public void GoBack()
        {
            if (!_isVisible) return;

            if (_currentState == MenuState.Tooltip || _currentState == MenuState.Moves || _currentState == MenuState.Targeting)
            {
                _backButton.TriggerClick();
            }
        }

        public void Reset()
        {
            SetState(MenuState.Main);
            ResetAnimationState();
            _selectedMove = null;
            _selectedSpellbookEntry = null;
            _tooltipMove = null;
            _clickTimestamps.Clear();
            _isSpamming = false;
        }

        public void ResetAnimationState()
        {
            foreach (var button in _actionButtons)
            {
                button.ResetAnimationState();
            }
            foreach (var button in _moveButtons)
            {
                button?.ResetAnimationState();
            }
            foreach (var button in _secondaryActionButtons)
            {
                button.ResetAnimationState();
            }
            _backButton.ResetAnimationState();
            _slot2BackButton?.ResetAnimationState();
        }

        public void Show(BattleCombatant player, List<BattleCombatant> allCombatants)
        {
            _isVisible = true;
            _player = player;
            _allCombatants = allCombatants;
            _allTargets = allCombatants.Where(c => !c.IsPlayerControlled && !c.IsDefeated).ToList();

            // Ensure buttons are initialized and update their state immediately based on the new combatant list.
            // This prevents the UI from animating in with stale state from a previous battle.
            InitializeButtons();
            UpdateSwitchButtonState();

            SetState(MenuState.Main);
        }

        public void Hide()
        {
            _isVisible = false;
        }

        public void SetState(MenuState newState)
        {
            var oldState = _currentState;
            _currentState = newState;
            HoveredMove = null;

            if (oldState == MenuState.Moves && newState != MenuState.Moves)
            {
                _isHoverInfoScrollingInitialized = false;
            }

            if (newState == MenuState.Main)
            {
                OnMainMenuOpened?.Invoke();
                foreach (var button in _actionButtons)
                {
                    button.ResetAnimationState();
                }
            }
            else if (newState == MenuState.Moves)
            {
                OnMovesMenuOpened?.Invoke();
                PopulateMoveButtons();
            }
            else if (newState == MenuState.Targeting)
            {
                _targetingTextAnimTimer = 0f;
            }
            else if (newState == MenuState.Tooltip)
            {
                _isTooltipScrollingInitialized = false;
                _tooltipScrollPosition = 0f;
                _tooltipScrollState = TooltipScrollState.PausedAtStart;
                _tooltipScrollWaitTimer = SCROLL_PAUSE_DURATION;
            }
        }

        private void PopulateMoveButtons()
        {
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            var spriteManager = ServiceLocator.Get<SpriteManager>();

            var spells = _player.Spells;
            if (spells == null) return;

            for (int i = 0; i < spells.Length; i++)
            {
                var entry = spells[i];
                if (entry == null || !BattleDataCache.Moves.TryGetValue(entry.MoveID, out var move))
                {
                    _moveButtons[i] = null;
                    continue;
                }

                int effectivePower = DamageCalculator.GetEffectiveMovePower(_player, move);
                // Pass null for background to make it transparent
                var moveButton = CreateMoveButton(move, entry, effectivePower, secondaryFont, null, true);
                _moveButtons[i] = moveButton;
                moveButton.ShowInstantly();
            }
        }

        private MoveButton CreateMoveButton(MoveData move, MoveEntry entry, int displayPower, BitmapFont font, Texture2D? background, bool startVisible)
        {
            var spriteManager = ServiceLocator.Get<SpriteManager>();
            int elementId = move.OffensiveElementIDs.FirstOrDefault();
            if (elementId == 0)
            {
                elementId = 1;
            }
            Rectangle? sourceRect = null;
            if (spriteManager.ElementIconSourceRects.TryGetValue(elementId, out var rect))
            {
                sourceRect = rect;
            }

            var moveButton = new MoveButton(move, entry, displayPower, font, background, spriteManager.ElementIconsSpriteSheet, sourceRect, startVisible);
            moveButton.OnClick += () => HandleMoveButtonClick(move, entry, moveButton);
            return moveButton;
        }

        private void HandleMoveButtonClick(MoveData move, MoveEntry? entry, MoveButton button)
        {
            // --- MANA DUMP LOGIC ---
            var manaDump = move.Abilities.OfType<ManaDumpAbility>().FirstOrDefault();
            bool canAfford;
            if (manaDump != null)
            {
                canAfford = _player != null && _player.Stats.CurrentMana > 0;
            }
            else
            {
                canAfford = _player != null && _player.Stats.CurrentMana >= move.ManaCost;
            }

            if (!canAfford)
            {
                EventBus.Publish(new GameEvents.AlertPublished { Message = "NOT ENOUGH MANA" });
                var attuneButton = _secondaryActionButtons.FirstOrDefault(b => b.Text == "ATTUNE");
                if (attuneButton != null)
                {
                    attuneButton.TriggerShake();
                    attuneButton.TriggerFlash(_global.Palette_Red, 0.4f);
                }
                return;
            }

            SelectMove(move, entry);
        }

        private void SelectMove(MoveData move, MoveEntry? entry)
        {
            _selectedMove = move;
            _selectedSpellbookEntry = entry;

            // Force targeting menu for everything except None
            if (move.Target == TargetType.None)
            {
                OnMoveSelected?.Invoke(move, entry, null);
            }
            else
            {
                SetState(MenuState.Targeting);
            }
        }

        private void UpdateTooltipScrolling(GameTime gameTime)
        {
            if (!_isTooltipScrollingInitialized) return;

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            switch (_tooltipScrollState)
            {
                case TooltipScrollState.PausedAtStart:
                    _tooltipScrollWaitTimer -= dt;
                    if (_tooltipScrollWaitTimer <= 0)
                    {
                        _tooltipScrollState = TooltipScrollState.ScrollingToEnd;
                    }
                    break;

                case TooltipScrollState.ScrollingToEnd:
                    _tooltipScrollPosition += SCROLL_SPEED * dt;
                    if (_tooltipScrollPosition >= _tooltipMaxScrollToShowEnd)
                    {
                        _tooltipScrollPosition = _tooltipMaxScrollToShowEnd;
                        _tooltipScrollState = TooltipScrollState.PausedAtEnd;
                        _tooltipScrollWaitTimer = SCROLL_PAUSE_DURATION;
                    }
                    break;

                case TooltipScrollState.PausedAtEnd:
                    _tooltipScrollWaitTimer -= dt;
                    if (_tooltipScrollWaitTimer <= 0)
                    {
                        _tooltipScrollPosition = 0;
                        _tooltipScrollState = TooltipScrollState.PausedAtStart;
                        _tooltipScrollWaitTimer = SCROLL_PAUSE_DURATION;
                    }
                    break;
            }
        }

        private void UpdateHoverInfoScrolling(GameTime gameTime)
        {
            if (!_isHoverInfoScrollingInitialized) return;

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            switch (_hoverInfoScrollState)
            {
                case HoverInfoScrollState.PausedAtStart:
                    _hoverInfoScrollWaitTimer -= dt;
                    if (_hoverInfoScrollWaitTimer <= 0)
                    {
                        _hoverInfoScrollState = HoverInfoScrollState.ScrollingToEnd;
                    }
                    break;

                case HoverInfoScrollState.ScrollingToEnd:
                    _hoverInfoScrollPosition += SCROLL_SPEED * dt;
                    if (_hoverInfoScrollPosition >= _hoverInfoMaxScrollToShowEnd)
                    {
                        _hoverInfoScrollPosition = _hoverInfoMaxScrollToShowEnd;
                        _hoverInfoScrollState = HoverInfoScrollState.PausedAtEnd;
                        _hoverInfoScrollWaitTimer = SCROLL_PAUSE_DURATION;
                    }
                    break;

                case HoverInfoScrollState.PausedAtEnd:
                    _hoverInfoScrollWaitTimer -= dt;
                    if (_hoverInfoScrollWaitTimer <= 0)
                    {
                        _hoverInfoScrollPosition = 0;
                        _hoverInfoScrollState = HoverInfoScrollState.PausedAtStart;
                        _hoverInfoScrollWaitTimer = SCROLL_PAUSE_DURATION;
                    }
                    break;
            }
        }

        private void UpdateSpamDetection(GameTime gameTime, MouseState currentMouseState)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            float now = (float)gameTime.TotalGameTime.TotalSeconds;

            if (_spamCooldownTimer > 0)
            {
                _spamCooldownTimer -= dt;
                if (_spamCooldownTimer <= 0)
                {
                    _isSpamming = false;
                    _clickTimestamps.Clear();
                }
            }

            if (_currentState == MenuState.Main && currentMouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released)
            {
                _clickTimestamps.Enqueue(now);
                _spamCooldownTimer = SPAM_COOLDOWN_SECONDS;

                while (_clickTimestamps.Count > 0 && now - _clickTimestamps.Peek() > SPAM_WINDOW_SECONDS)
                {
                    _clickTimestamps.Dequeue();
                }

                if (_clickTimestamps.Count >= SPAM_CLICKS)
                {
                    _isSpamming = true;
                }
            }
        }

        private void UpdateLayout()
        {
            switch (_currentState)
            {
                case MenuState.Moves:
                    const int secButtonWidth = 60;
                    const int secButtonHeight = 13;
                    const int secRowSpacing = 0;
                    const int secRows = 3;
                    const int secBlockHeight = secRows * (secButtonHeight + secRowSpacing) - secRowSpacing;
                    const int infoPanelWidth = 120;
                    const int gap = 0;

                    // Calculate total width of the three columns
                    int totalWidth = secButtonWidth + gap + MOVE_BUTTON_WIDTH + gap + infoPanelWidth;

                    // Center the entire block on screen
                    int startX = (Global.VIRTUAL_WIDTH - totalWidth) / 2;

                    // Column 1: Secondary Actions (Left)
                    int secGridStartX = startX;
                    int moveGridStartY = 128;
                    int secGridStartY = moveGridStartY + (MOVE_BLOCK_HEIGHT / 2) - (secBlockHeight / 2);

                    for (int i = 0; i < _secondaryActionButtons.Count; i++)
                    {
                        var button = _secondaryActionButtons[i];
                        int yPos = secGridStartY + i * (secButtonHeight + secRowSpacing);
                        button.Bounds = new Rectangle(secGridStartX, yPos, secButtonWidth, secButtonHeight);
                    }

                    // Column 2: Move Buttons (Center)
                    int moveGridStartX = secGridStartX + secButtonWidth + gap;
                    for (int i = 0; i < _moveButtons.Length; i++)
                    {
                        var button = _moveButtons[i];
                        if (button == null) continue;

                        int rowStep = MOVE_BUTTON_HEIGHT + MOVE_ROW_SPACING;
                        button.Bounds = new Rectangle(
                            moveGridStartX,
                            moveGridStartY + i * rowStep,
                            MOVE_BUTTON_WIDTH,
                            rowStep
                        );
                    }
                    break;
            }
        }

        private void UpdateSwitchButtonState()
        {
            // Update Switch Button Enable State
            if (_allCombatants != null && _actionButtons.Count > 2)
            {
                // Check if there are any player-controlled combatants on the bench (Slot >= 2)
                bool hasBench = _allCombatants.Any(c => c.IsPlayerControlled && !c.IsDefeated && c.BattleSlot >= 2);
                // The Switch button is the 3rd button (index 2)
                _actionButtons[2].IsEnabled = hasBench;
            }
        }

        public void Update(MouseState currentMouseState, GameTime gameTime, bool isInputBlocked = false)
        {
            InitializeButtons();
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (!_isVisible)
            {
                _previousMouseState = currentMouseState;
                return;
            }

            // If input is blocked, use a dummy mouse state to prevent hover/click logic
            // but still allow internal state updates (like timers)
            MouseState effectiveMouseState = isInputBlocked
                ? new MouseState(-10000, -10000, currentMouseState.ScrollWheelValue, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released)
                : currentMouseState;

            UpdateLayout();
            UpdateSpamDetection(gameTime, effectiveMouseState);

            HoveredButton = null;

            switch (_currentState)
            {
                case MenuState.Main:
                    bool isAnyActionHovered = false;

                    UpdateSwitchButtonState();

                    foreach (var button in _actionButtons)
                    {
                        button.Update(effectiveMouseState);
                        if (button.IsHovered)
                        {
                            isAnyActionHovered = true;
                            HoveredButton = button;
                        }
                    }

                    // Update Slot 2 Back Button
                    if (_player != null && _player.BattleSlot == 1)
                    {
                        _slot2BackButton.Update(effectiveMouseState);
                        if (_slot2BackButton.IsHovered) HoveredButton = _slot2BackButton;

                        if (effectiveMouseState.RightButton == ButtonState.Pressed && _previousMouseState.RightButton == ButtonState.Released)
                        {
                            OnSlot2BackRequested?.Invoke();
                        }
                    }

                    if (isAnyActionHovered)
                    {
                        SharedSwayTimer += dt;
                    }
                    else if (_wasAnyActionHoveredLastFrame)
                    {
                        SharedSwayTimer = 0f;
                    }
                    _wasAnyActionHoveredLastFrame = isAnyActionHovered;
                    break;
                case MenuState.Moves:
                    HoveredMove = null;
                    _hoveredSpellbookEntry = null;
                    _hoveredMoveButton = null;
                    _shouldAttuneButtonPulse = false;

                    bool middleClickHeldOnAButton = false;
                    MoveData? moveForTooltip = null;
                    bool simpleTooltip = false;

                    // Check for Silence Status
                    bool isSilenced = _player != null && _player.HasStatusEffect(StatusEffectType.Silence);

                    foreach (var button in _moveButtons)
                    {
                        if (button == null) continue;

                        // --- SILENCE LOGIC ---
                        // If player is silenced and this move is a Spell, disable the button.
                        if (isSilenced && button.Move.MoveType == MoveType.Spell)
                        {
                            button.IsEnabled = false;
                        }
                        else
                        {
                            button.IsEnabled = true;
                        }

                        button.Update(effectiveMouseState);
                        if (button.IsHovered)
                        {
                            HoveredMove = button.Move;
                            _hoveredSpellbookEntry = button.Entry;
                            HoveredButton = button;
                            _hoveredMoveButton = button;

                            if (_player != null && _player.Stats.CurrentMana < button.Move.ManaCost)
                            {
                                _shouldAttuneButtonPulse = true;
                            }

                            if (effectiveMouseState.MiddleButton == ButtonState.Pressed)
                            {
                                middleClickHeldOnAButton = true;
                                moveForTooltip = button.Move;
                                simpleTooltip = false;
                            }
                        }
                    }

                    foreach (var button in _secondaryActionButtons)
                    {
                        // --- SILENCE LOGIC FOR SECONDARY BUTTONS ---
                        // Check if the underlying move is a spell (e.g. Attune)
                        string? moveId = button.Text switch
                        {
                            "STRIKE" => _player?.DefaultStrikeMoveID,
                            "ATTUNE" => "7",
                            "STALL" => "6",
                            _ => null
                        };

                        if (!string.IsNullOrEmpty(moveId) && BattleDataCache.Moves.TryGetValue(moveId, out var moveData))
                        {
                            if (isSilenced && moveData.MoveType == MoveType.Spell)
                            {
                                button.IsEnabled = false;
                            }
                            else
                            {
                                button.IsEnabled = true;
                            }
                        }

                        button.Update(effectiveMouseState);
                        if (button.IsHovered)
                        {
                            HoveredButton = button;

                            if (!string.IsNullOrEmpty(moveId) && BattleDataCache.Moves.TryGetValue(moveId, out var move))
                            {
                                HoveredMove = move;
                                _hoveredSpellbookEntry = null;

                                if (effectiveMouseState.MiddleButton == ButtonState.Pressed)
                                {
                                    middleClickHeldOnAButton = true;
                                    moveForTooltip = move;
                                    simpleTooltip = false;
                                }
                            }
                        }
                    }

                    if (middleClickHeldOnAButton)
                    {
                        _tooltipMove = moveForTooltip;
                        _useSimpleTooltip = simpleTooltip;
                        SetState(MenuState.Tooltip);
                    }

                    // --- Right Click to Back Logic ---
                    if (effectiveMouseState.RightButton == ButtonState.Pressed && _previousMouseState.RightButton == ButtonState.Released)
                    {
                        GoBack();
                    }

                    if (HoveredMove != _lastHoveredMoveForScrolling)
                    {
                        _isHoverInfoScrollingInitialized = false;
                        _lastHoveredMoveForScrolling = HoveredMove;
                    }

                    UpdateHoverInfoScrolling(gameTime);

                    _backButton.Update(effectiveMouseState);
                    if (_backButton.IsHovered) HoveredButton = _backButton;
                    break;
                case MenuState.Targeting:
                    _targetingTextAnimTimer += dt;
                    _backButton.Update(effectiveMouseState);
                    if (_backButton.IsHovered) HoveredButton = _backButton;
                    break;
                case MenuState.Tooltip:
                    if (effectiveMouseState.MiddleButton == ButtonState.Released)
                    {
                        SetState(MenuState.Moves);
                    }
                    UpdateTooltipScrolling(gameTime);
                    _backButton.Update(effectiveMouseState);
                    if (_backButton.IsHovered) HoveredButton = _backButton;
                    break;
            }

            _previousMouseState = currentMouseState;
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform, Vector2 offset)
        {
            InitializeButtons();

            var spriteManager = ServiceLocator.Get<SpriteManager>();
            var pixel = ServiceLocator.Get<Texture2D>();
            var bgColor = _global.Palette_Black; // Changed to fully opaque black
            const int dividerY = 123;
            var bgRect = new Rectangle(0, dividerY + (int)offset.Y, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT - dividerY);
            spriteBatch.DrawSnapped(pixel, bgRect, bgColor);

            if (!_isVisible)
            {
                // Draw the combat border when the menu is hidden (e.g. during narration/animations)
                // Apply offset to the border position
                spriteBatch.DrawSnapped(spriteManager.BattleBorderCombat, offset, Color.White);
                return;
            }

            // Draw the specific border based on state
            if (_currentState == MenuState.Main)
            {
                // --- DYNAMIC BORDER SELECTION ---
                // If the acting player is in Slot 1 (the second slot), use the alternate border.
                var border = (_player != null && _player.BattleSlot == 1) ? spriteManager.BattleBorderMain2 : spriteManager.BattleBorderMain;
                spriteBatch.DrawSnapped(border, offset, Color.White);
            }
            else if (_currentState == MenuState.Targeting)
            {
                spriteBatch.DrawSnapped(spriteManager.BattleBorderTarget, offset, Color.White);
            }
            else if (_currentState == MenuState.Moves || _currentState == MenuState.Tooltip)
            {
                spriteBatch.DrawSnapped(spriteManager.BattleBorderAction, offset, Color.White);
            }

            switch (_currentState)
            {
                case MenuState.Main:
                    {
                        const int buttonWidth = 128;
                        const int buttonHeight = 14; // Increased from 13
                        const int buttonSpacing = 0; // No gap

                        int totalHeight = (buttonHeight * _actionButtons.Count) + (buttonSpacing * (_actionButtons.Count - 1));
                        int availableHeight = Global.VIRTUAL_HEIGHT - dividerY;

                        // Moved up 3 pixels (-3)
                        int startY = dividerY + (availableHeight - totalHeight) / 2 - 3;

                        // --- DYNAMIC BUTTON POSITIONING ---
                        // Default (Slot 0): Left side (-80 offset)
                        // Slot 1: Right side (+80 offset)
                        int xOffset = -80;
                        if (_player != null && _player.BattleSlot == 1)
                        {
                            xOffset = 80;
                        }

                        int startX = (Global.VIRTUAL_WIDTH - buttonWidth) / 2 + xOffset;

                        int currentY = startY;
                        foreach (var button in _actionButtons)
                        {
                            button.Bounds = new Rectangle(startX, currentY, buttonWidth, buttonHeight);

                            // Draw manual border for the button (Inset by 1 pixel)
                            Color borderColor = _global.Palette_Gray; // Default enabled
                            if (!button.IsEnabled) borderColor = _global.Palette_DarkestGray;
                            else if (button.IsHovered) borderColor = _global.ButtonHoverColor;

                            // --- MODIFIED: Increase height by 1 pixel downward ---
                            // Original: button.Bounds.Height - 2
                            // New: button.Bounds.Height - 1
                            var borderRect = new Rectangle(button.Bounds.X + 1, button.Bounds.Y + 1, button.Bounds.Width - 2, button.Bounds.Height - 1);

                            // Apply offset to border drawing
                            spriteBatch.DrawSnapped(pixel, new Rectangle(borderRect.Left, borderRect.Top + (int)offset.Y, borderRect.Width, 1), borderColor); // Top
                            spriteBatch.DrawSnapped(pixel, new Rectangle(borderRect.Left, borderRect.Bottom - 1 + (int)offset.Y, borderRect.Width, 1), borderColor); // Bottom
                            spriteBatch.DrawSnapped(pixel, new Rectangle(borderRect.Left, borderRect.Top + (int)offset.Y, 1, borderRect.Height), borderColor); // Left
                            spriteBatch.DrawSnapped(pixel, new Rectangle(borderRect.Right - 1, borderRect.Top + (int)offset.Y, 1, borderRect.Height), borderColor); // Right

                            // Pass offset to button draw
                            button.Draw(spriteBatch, font, gameTime, transform, false, null, offset.Y);
                            currentY += buttonHeight + buttonSpacing;
                        }

                        if (_player != null && _player.BattleSlot == 1)
                        {
                            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
                            // Ensure font is set
                            if (_slot2BackButton.Font == null) _slot2BackButton.Font = secondaryFont;

                            int backButtonHeight = 9; // Reduced from 15
                            int backButtonY = 168;    // Moved down from 165

                            var backSize = secondaryFont.MeasureString(_slot2BackButton.Text);
                            int backWidth = (int)backSize.Width + 16;
                            // Center relative to the button column
                            int backX = startX + (buttonWidth - backWidth) / 2 + 2; // Added +2 pixels

                            _slot2BackButton.Bounds = new Rectangle(backX, backButtonY, backWidth, backButtonHeight);
                            _slot2BackButton.Draw(spriteBatch, font, gameTime, transform, false, null, offset.Y);
                        }
                        break;
                    }
                case MenuState.Moves:
                    {
                        DrawMovesMenu(spriteBatch, font, gameTime, transform, offset);
                        break;
                    }
                case MenuState.Targeting:
                    {
                        const int backButtonPadding = 8;
                        const int backButtonHeight = 15; // Increased from 7 to match ItemMenu
                        const int backButtonTopMargin = 1;
                        const int horizontalPadding = 10;
                        const int verticalPadding = 2;
                        int availableWidth = Global.VIRTUAL_WIDTH - (horizontalPadding * 2);
                        int availableHeight = Global.VIRTUAL_HEIGHT - dividerY - (verticalPadding * 2);
                        int gridAreaHeight = availableHeight - backButtonHeight - backButtonTopMargin;

                        int backButtonWidth = (int)(_backButton.Font ?? font).MeasureString(_backButton.Text).Width + backButtonPadding * 2;
                        _backButton.Bounds = new Rectangle(
                            horizontalPadding + (availableWidth - backButtonWidth) / 2 + 1, // Added +1 to X
                            165,
                            backButtonWidth,
                            backButtonHeight
                        );
                        _backButton.Draw(spriteBatch, font, gameTime, transform, false, null, offset.Y);
                        break;
                    }
                case MenuState.Tooltip:
                    {
                        if (_useSimpleTooltip)
                        {
                            DrawSimpleTooltip(spriteBatch, font, gameTime, transform, offset);
                        }
                        else
                        {
                            DrawComplexTooltip(spriteBatch, font, gameTime, transform, offset);
                        }
                        break;
                    }
            }

            // --- DEBUG DRAWING (F1) ---
            if (_global.ShowSplitMapGrid)
            {
                // Main Menu Buttons
                if (_currentState == MenuState.Main)
                {
                    foreach (var button in _actionButtons)
                    {
                        var debugRect = button.Bounds;
                        debugRect.Y += (int)offset.Y;
                        spriteBatch.DrawSnapped(pixel, debugRect, Color.Green * 0.5f);
                    }
                    if (_player != null && _player.BattleSlot == 1)
                    {
                        var debugRect = _slot2BackButton.Bounds;
                        debugRect.Y += (int)offset.Y;
                        spriteBatch.DrawSnapped(pixel, debugRect, Color.Red * 0.5f);
                    }
                }

                // Moves Menu
                if (_currentState == MenuState.Moves)
                {
                    foreach (var button in _moveButtons)
                    {
                        if (button != null)
                        {
                            var debugRect = button.Bounds;
                            debugRect.Y += (int)offset.Y;
                            spriteBatch.DrawSnapped(pixel, debugRect, Color.Yellow * 0.5f);
                        }
                    }
                    foreach (var button in _secondaryActionButtons)
                    {
                        var debugRect = button.Bounds;
                        debugRect.Y += (int)offset.Y;
                        spriteBatch.DrawSnapped(pixel, debugRect, Color.Orange * 0.5f);
                    }
                    var backDebugRect = _backButton.Bounds;
                    backDebugRect.Y += (int)offset.Y;
                    spriteBatch.DrawSnapped(pixel, backDebugRect, Color.Red * 0.5f);

                    var infoDebugRect = _moveInfoPanelBounds;
                    infoDebugRect.Y += (int)offset.Y;
                    spriteBatch.DrawSnapped(pixel, infoDebugRect, Color.Magenta * 0.5f);
                }

                // Tooltip
                if (_currentState == MenuState.Tooltip)
                {
                    var debugRect = _tooltipBounds;
                    debugRect.Y += (int)offset.Y;
                    spriteBatch.DrawSnapped(pixel, debugRect, Color.Magenta * 0.5f);

                    var backDebugRect = _backButton.Bounds;
                    backDebugRect.Y += (int)offset.Y;
                    spriteBatch.DrawSnapped(pixel, backDebugRect, Color.Red * 0.5f);
                }

                // Targeting
                if (_currentState == MenuState.Targeting)
                {
                    var backDebugRect = _backButton.Bounds;
                    backDebugRect.Y += (int)offset.Y;
                    spriteBatch.DrawSnapped(pixel, backDebugRect, Color.Red * 0.5f);
                }
            }
        }

        private void DrawSimpleTooltip(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform, Vector2 offset)
        {
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            var pixel = ServiceLocator.Get<Texture2D>();

            const int boxWidth = 294;
            const int boxHeight = 47;
            const int boxY = 113; // Moved up from 117 to fit larger back button
            int boxX = (Global.VIRTUAL_WIDTH - boxWidth) / 2;
            var tooltipBounds = new Rectangle(boxX, boxY + (int)offset.Y, boxWidth, boxHeight);
            _tooltipBounds = tooltipBounds; // Store for debug

            // Draw opaque black background
            spriteBatch.DrawSnapped(pixel, tooltipBounds, _global.Palette_Black);

            var borderColor = _global.Palette_White;
            spriteBatch.DrawSnapped(pixel, new Rectangle(tooltipBounds.Left, tooltipBounds.Top, tooltipBounds.Width, 1), borderColor);
            spriteBatch.DrawSnapped(pixel, new Rectangle(tooltipBounds.Left, tooltipBounds.Bottom - 1, tooltipBounds.Width, 1), borderColor);
            spriteBatch.DrawSnapped(pixel, new Rectangle(tooltipBounds.Left, tooltipBounds.Top, 1, tooltipBounds.Height), borderColor);
            spriteBatch.DrawSnapped(pixel, new Rectangle(tooltipBounds.Right - 1, tooltipBounds.Top, 1, tooltipBounds.Height), borderColor);

            if (_tooltipMove != null)
            {
                var moveName = _tooltipMove.MoveName.ToUpper();
                var nameSize = font.MeasureString(moveName);
                var namePos = new Vector2(
                    tooltipBounds.Center.X - nameSize.Width / 2,
                    tooltipBounds.Y + 8
                );
                spriteBatch.DrawStringSnapped(font, moveName, namePos, _global.Palette_BrightWhite);

                if (!string.IsNullOrEmpty(_tooltipMove.Description))
                {
                    float availableWidth = tooltipBounds.Width - 20;
                    var wrappedLines = ParseAndWrapRichText(secondaryFont, _tooltipMove.Description.ToUpper(), availableWidth, _global.Palette_White);
                    float currentY = namePos.Y + nameSize.Height + 6;

                    foreach (var line in wrappedLines)
                    {
                        float lineWidth = 0;
                        foreach (var segment in line)
                        {
                            if (string.IsNullOrWhiteSpace(segment.Text))
                                lineWidth += segment.Text.Length * SPACE_WIDTH;
                            else
                                lineWidth += secondaryFont.MeasureString(segment.Text).Width;
                        }

                        float lineX = tooltipBounds.Center.X - lineWidth / 2f;
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
            }

            int backButtonY = 113 + boxHeight + 5; // Use base Y + height + padding
            var backSize = (_backButton.Font ?? font).MeasureString(_backButton.Text);
            int backWidth = (int)backSize.Width + 16;
            _backButton.Bounds = new Rectangle(
                (Global.VIRTUAL_WIDTH - backWidth) / 2 + 1, // Added +1 to X
                backButtonY,
                backWidth,
                15 // Increased from 7 to match ItemMenu
            );
            _backButton.Draw(spriteBatch, font, gameTime, transform, false, null, offset.Y);
        }

        private void DrawComplexTooltip(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform, Vector2 offset)
        {
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            var pixel = ServiceLocator.Get<Texture2D>(); // Added pixel retrieval

            const int dividerY = 114;
            const int moveButtonWidth = 157;
            const int columns = 2;
            const int columnSpacing = 0;

            int totalGridWidth = (moveButtonWidth * columns) + columnSpacing;
            const int gridHeight = 40;
            int gridStartX = (Global.VIRTUAL_WIDTH - totalGridWidth) / 2;
            int gridStartY = 123; // Adjusted from 128 to fit larger back button

            var spriteManager = ServiceLocator.Get<SpriteManager>();
            var tooltipBg = spriteManager.ActionTooltipBackgroundSprite;
            var tooltipBgRect = new Rectangle(gridStartX, gridStartY + (int)offset.Y, totalGridWidth, gridHeight);
            _tooltipBounds = tooltipBgRect; // Store for debug

            // Draw opaque black background
            spriteBatch.DrawSnapped(pixel, tooltipBgRect, _global.Palette_Black);

            spriteBatch.DrawSnapped(tooltipBg, tooltipBgRect, Color.White);

            if (_tooltipMove != null)
            {
                DrawMoveInfoPanelContent(spriteBatch, _tooltipMove, tooltipBgRect, font, secondaryFont, transform, true);
            }

            const int backButtonTopMargin = 0;
            int backButtonY = 123 + gridHeight + backButtonTopMargin + 2; // Use base Y
            var backSize = (_backButton.Font ?? font).MeasureString(_backButton.Text);
            int backWidth = (int)backSize.Width + 16;
            _backButton.Bounds = new Rectangle(
                (Global.VIRTUAL_WIDTH - backWidth) / 2 + 1, // Added +1 to X
                backButtonY,
                backWidth,
                15 // Increased from 7 to match ItemMenu
            );
            _backButton.Draw(spriteBatch, font, gameTime, transform, false, null, offset.Y);
        }

        private void DrawMovesMenu(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform, Vector2 offset)
        {
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            var pixel = ServiceLocator.Get<Texture2D>();

            const int secButtonWidth = 60;

            const int borderWidth = 120;
            const int borderHeight = 37;
            const int layoutGap = 5; // Increased gap for spacing

            int moveGridStartY = 128;

            // Calculate total width of the three columns
            int totalWidth = secButtonWidth + layoutGap + MOVE_BUTTON_WIDTH + layoutGap + borderWidth;

            // Center the entire block on screen
            int startX = (Global.VIRTUAL_WIDTH - totalWidth) / 2;

            // Column 1: Secondary Actions (Left)
            int secGridStartX = startX;

            // Column 2: Move Buttons (Center)
            int moveGridStartX = secGridStartX + secButtonWidth + layoutGap;

            // Column 3: Info Panel (Right)
            int borderX = moveGridStartX + MOVE_BUTTON_WIDTH + layoutGap;
            int borderY = moveGridStartY + (MOVE_BLOCK_HEIGHT / 2) - (borderHeight / 2);

            var borderRect = new Rectangle(borderX, borderY + (int)offset.Y, borderWidth, borderHeight);
            _moveInfoPanelBounds = borderRect; // Store for debug

            // Removed manual border drawing here as it's now handled by the background sprite

            DrawMoveInfoPanelContent(spriteBatch, HoveredMove, borderRect, font, secondaryFont, transform, false);


            for (int i = 0; i < _moveButtons.Length; i++)
            {
                var button = _moveButtons[i];
                int row = i;
                int rowStep = MOVE_BUTTON_HEIGHT + MOVE_ROW_SPACING;

                var visualBounds = new Rectangle(
                    moveGridStartX,
                    moveGridStartY + row * rowStep,
                    MOVE_BUTTON_WIDTH,
                    MOVE_BUTTON_HEIGHT
                );

                if (button == null)
                {
                    var placeholderFillColor = Color.Transparent; // Changed from Black to Transparent
                    // Apply offset to placeholder
                    var offsetBounds = new Rectangle(visualBounds.X, visualBounds.Y + (int)offset.Y, visualBounds.Width, visualBounds.Height);
                    spriteBatch.DrawSnapped(pixel, offsetBounds, placeholderFillColor);
                    var placeholderBorderColor = _global.Palette_DarkerGray;

                    const int dashLength = 1;
                    const int gapLength = 3;
                    const int patternLength = dashLength + gapLength;

                    int lineY = offsetBounds.Center.Y;
                    int lineStartX = offsetBounds.Left + 3;
                    int lineEndX = offsetBounds.Right - 1;

                    for (int x = lineStartX; x < lineEndX; x += patternLength)
                    {
                        int width = Math.Min(dashLength, lineEndX - x);
                        spriteBatch.DrawSnapped(pixel, new Rectangle(x, lineY, width, 1), placeholderBorderColor);
                    }
                }
                else
                {
                    var buttonBorderColor = _global.Palette_DarkerGray;
                    if (_player != null && _player.Stats.CurrentMana < button.Move.ManaCost)
                    {
                        buttonBorderColor = Color.Transparent;
                    }
                    // Apply offset to border drawing
                    var offsetBounds = new Rectangle(visualBounds.X, visualBounds.Y + (int)offset.Y, visualBounds.Width, visualBounds.Height);

                    spriteBatch.DrawSnapped(pixel, new Rectangle(offsetBounds.Left, offsetBounds.Top, offsetBounds.Width, 1), buttonBorderColor); // Top
                    spriteBatch.DrawSnapped(pixel, new Rectangle(offsetBounds.Left, offsetBounds.Bottom - 1, offsetBounds.Width, 1), buttonBorderColor); // Bottom
                    spriteBatch.DrawSnapped(pixel, new Rectangle(offsetBounds.Left, offsetBounds.Top, 1, offsetBounds.Height), buttonBorderColor); // Left
                    spriteBatch.DrawSnapped(pixel, new Rectangle(offsetBounds.Right - 1, offsetBounds.Top, 1, offsetBounds.Height), buttonBorderColor); // Right

                    if (button == _hoveredMoveButton && button.IsEnabled)
                    {
                        var hoverBgRect = new Rectangle(offsetBounds.X + 1, offsetBounds.Y + 1, offsetBounds.Width - 2, offsetBounds.Height - 2);
                        spriteBatch.DrawSnapped(pixel, hoverBgRect, _global.Palette_DarkerGray);
                    }

                    var originalBounds = button.Bounds;
                    button.Bounds = visualBounds;
                    button.Draw(spriteBatch, font, gameTime, transform, false, null, offset.Y);
                    button.Bounds = originalBounds;
                }
            }

            Color? attunePulseColor = null;
            if (_shouldAttuneButtonPulse)
            {
                float pulse = (MathF.Sin((float)gameTime.TotalGameTime.TotalSeconds * 8f) + 1f) / 2f;
                attunePulseColor = Color.Lerp(Color.White, _global.Palette_LightBlue, pulse);
            }

            for (int i = 0; i < _secondaryActionButtons.Count; i++)
            {
                var button = _secondaryActionButtons[i];

                // --- MANUAL BORDER DRAWING FOR SECONDARY BUTTONS ---
                var borderColor = button.IsHovered ? _global.ButtonHoverColor : _global.Palette_DarkerGray;
                // 1 pixel in from normal border. Normal border is usually the bounds edges.
                // So inset by 1.
                var buttonBorderRect = new Rectangle(button.Bounds.X + 1, button.Bounds.Y + 1, button.Bounds.Width - 2, button.Bounds.Height - 2);

                // Apply offset to border drawing
                spriteBatch.DrawSnapped(pixel, new Rectangle(buttonBorderRect.Left, buttonBorderRect.Top + (int)offset.Y, buttonBorderRect.Width, 1), borderColor); // Top
                spriteBatch.DrawSnapped(pixel, new Rectangle(buttonBorderRect.Left, buttonBorderRect.Bottom - 1 + (int)offset.Y, buttonBorderRect.Width, 1), borderColor); // Bottom
                spriteBatch.DrawSnapped(pixel, new Rectangle(buttonBorderRect.Left, buttonBorderRect.Top + (int)offset.Y, 1, buttonBorderRect.Height), borderColor); // Left
                spriteBatch.DrawSnapped(pixel, new Rectangle(buttonBorderRect.Right - 1, buttonBorderRect.Top + (int)offset.Y, 1, buttonBorderRect.Height), borderColor); // Right

                button.Draw(spriteBatch, font, gameTime, transform, false, null, offset.Y, button.Text == "ATTUNE" ? attunePulseColor : null);
            }


            int layoutBottomY = Math.Max(borderRect.Bottom - (int)offset.Y, moveGridStartY + MOVE_BLOCK_HEIGHT);
            int backButtonY = layoutBottomY - 2;
            var backSize = (_backButton.Font ?? font).MeasureString(_backButton.Text);
            int backWidth = (int)backSize.Width + 16;
            _backButton.Bounds = new Rectangle(
                (Global.VIRTUAL_WIDTH - backWidth) / 2 + 1,
                backButtonY,
                backWidth,
                15
            );
            _backButton.Draw(spriteBatch, font, gameTime, transform, false, null, offset.Y);
        }

        private void DrawMoveInfoPanelContent(SpriteBatch spriteBatch, MoveData? move, Rectangle bounds, BitmapFont font, BitmapFont secondaryFont, Matrix transform, bool isForTooltip)
        {
            var tertiaryFont = ServiceLocator.Get<Core>().TertiaryFont; // Get Tertiary Font
            const int horizontalPadding = 4;
            const int verticalPadding = 3;
            float currentY = bounds.Y + verticalPadding;

            if (isForTooltip)
            {
                if (move == null) return;

                var moveName = move.MoveName.ToUpper();
                var nameSize = font.MeasureString(moveName);
                var namePos = new Vector2(bounds.X + horizontalPadding, currentY);

                var statsSegments = new List<(string Text, Color Color)>();
                string moveTypeText = move.MoveType.ToString().ToUpper();
                Color moveTypeColor = move.MoveType switch
                {
                    MoveType.Spell => _global.Palette_LightBlue,
                    MoveType.Action => _global.Palette_Orange,
                    _ => _global.Palette_White
                };
                statsSegments.Add((moveTypeText, moveTypeColor));

                if (move.ImpactType != ImpactType.Status)
                {
                    string separator = " / ";
                    string accuracyText = move.Accuracy >= 0 ? $"ACC: {move.Accuracy}%" : "ACC: ---";
                    string powerText = move.Power > 0 ? $"POW: {move.Power}" : (move.Effects.ContainsKey("ManaDamage") ? "POW: ???" : "POW: ---");

                    // --- MANA DUMP LOGIC ---
                    var manaDump = move.Abilities.OfType<ManaDumpAbility>().FirstOrDefault();
                    if (manaDump != null && _player != null)
                    {
                        powerText = $"POW: {(int)(_player.Stats.CurrentMana * manaDump.Multiplier)}";
                    }

                    Color accColor = _global.Palette_BrightWhite;
                    if (move.Accuracy >= 0)
                    {
                        float t = Math.Clamp((100f - move.Accuracy) / 50f, 0f, 1f);
                        accColor = Color.Lerp(_global.Palette_BrightWhite, _global.Palette_Red, t);
                    }

                    statsSegments.Insert(0, (separator, _global.Palette_DarkGray));
                    statsSegments.Insert(0, (accuracyText, accColor));
                    statsSegments.Insert(0, (separator, _global.Palette_DarkGray));
                    statsSegments.Insert(0, (powerText, _global.Palette_BrightWhite));
                }

                // --- NEW: Contact Text for Tooltip ---
                float contactWidth = 0f;
                if (move.MakesContact)
                {
                    string contactText = "[ CONTACT ]";
                    float textW = tertiaryFont.MeasureString(contactText).Width;
                    contactWidth = textW + 4; // Text + Gap

                    // Insert "CONTACT" at the beginning of the segments list
                    statsSegments.Insert(0, (contactText, _global.Palette_Red));
                    statsSegments.Insert(1, (" ", Color.Transparent)); // Spacer
                }

                float totalStatsWidth = statsSegments.Sum(s =>
                    s.Text == "[ CONTACT ]" ? tertiaryFont.MeasureString(s.Text).Width : secondaryFont.MeasureString(s.Text).Width
                );

                float statsY = currentY + (nameSize.Height - secondaryFont.LineHeight) / 2;
                float statsStartX = bounds.Right - horizontalPadding - totalStatsWidth;

                float currentX = statsStartX;
                foreach (var segment in statsSegments)
                {
                    if (segment.Text == "[ CONTACT ]")
                    {
                        // Center vertically relative to statsY (secondary font baseline)
                        float contactY = statsY + (secondaryFont.LineHeight - tertiaryFont.LineHeight) / 2f;
                        spriteBatch.DrawStringSnapped(tertiaryFont, segment.Text, new Vector2(currentX, contactY), segment.Color);
                        currentX += tertiaryFont.MeasureString(segment.Text).Width;
                    }
                    else
                    {
                        spriteBatch.DrawStringSnapped(secondaryFont, segment.Text, new Vector2(currentX, statsY), segment.Color);
                        currentX += secondaryFont.MeasureString(segment.Text).Width;
                    }
                }

                float textAvailableWidth = statsStartX - namePos.X - 4;
                bool needsScrolling = nameSize.Width > textAvailableWidth;
                if (needsScrolling)
                {
                    if (!_isTooltipScrollingInitialized)
                    {
                        _isTooltipScrollingInitialized = true;
                        float spaceWidth = font.MeasureString(" ").Width;
                        _tooltipMaxScrollToShowEnd = nameSize.Width - textAvailableWidth + (spaceWidth * EXTRA_SCROLL_SPACES);
                        _tooltipScrollWaitTimer = SCROLL_PAUSE_DURATION;
                        _tooltipScrollState = TooltipScrollState.PausedAtStart;
                        _tooltipScrollPosition = 0;
                    }

                    var originalRasterizerState = spriteBatch.GraphicsDevice.RasterizerState;
                    var originalScissorRect = spriteBatch.GraphicsDevice.ScissorRectangle;
                    spriteBatch.End();

                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, _clipRasterizerState, null, transform);
                    var clipRect = new Rectangle((int)namePos.X, (int)namePos.Y, (int)textAvailableWidth, (int)nameSize.Height);
                    spriteBatch.GraphicsDevice.ScissorRectangle = clipRect;

                    var scrollingTextPosition = new Vector2(namePos.X - _tooltipScrollPosition, namePos.Y);
                    spriteBatch.DrawStringSnapped(font, move.MoveName.ToUpper(), scrollingTextPosition, _global.Palette_BrightWhite);

                    spriteBatch.End();
                    spriteBatch.GraphicsDevice.ScissorRectangle = originalScissorRect;
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, originalRasterizerState, null, transform);
                }
                else
                {
                    _isTooltipScrollingInitialized = false;
                    spriteBatch.DrawStringSnapped(font, moveName, namePos, _global.Palette_BrightWhite);
                }
                currentY += nameSize.Height + 1;

                var underlineStart = new Vector2(bounds.X + horizontalPadding, currentY);
                var underlineEnd = new Vector2(bounds.Right - horizontalPadding, currentY);
                spriteBatch.DrawLineSnapped(underlineStart, underlineEnd, _global.Palette_DarkGray);
                currentY += 3;

                if (!string.IsNullOrEmpty(move.Description))
                {
                    float availableWidth = bounds.Width - (horizontalPadding * 2);
                    var wrappedLines = ParseAndWrapRichText(secondaryFont, move.Description.ToUpper(), availableWidth, _global.Palette_White);
                    foreach (var line in wrappedLines)
                    {
                        if (currentY + secondaryFont.LineHeight > bounds.Bottom - verticalPadding) break;

                        float lineWidth = 0;
                        foreach (var segment in line)
                        {
                            if (string.IsNullOrWhiteSpace(segment.Text))
                                lineWidth += segment.Text.Length * SPACE_WIDTH;
                            else
                                lineWidth += secondaryFont.MeasureString(segment.Text).Width;
                        }

                        float lineX = bounds.X + horizontalPadding;
                        float lineCurrentX = lineX;

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
                                spriteBatch.DrawStringSnapped(secondaryFont, segment.Text, new Vector2(lineCurrentX, currentY), segment.Color);
                            }
                            lineCurrentX += segWidth;
                        }
                        currentY += secondaryFont.LineHeight;
                    }
                }
            }
            else
            {
                float statsY = currentY;
                Color valueColor = _global.Palette_BrightWhite;
                Color labelColor = _global.Palette_DarkGray;

                string powerLabel = "POWE:";
                string accLabel = "ACCU:";
                string manaLabel = "MANA:";

                string powerValue, accValue, manaValue, impactValue, moveTypeValue;

                if (move != null)
                {
                    powerValue = move.Power > 0 ? move.Power.ToString() : (move.Effects.ContainsKey("ManaDamage") ? "???" : "---");
                    accValue = move.Accuracy >= 0 ? $"{move.Accuracy}%" : "---";
                    manaValue = move.ManaCost > 0 ? $"{move.ManaCost}%" : "---";
                    impactValue = move.ImpactType.ToString().ToUpper();
                    moveTypeValue = move.MoveType.ToString().ToUpper();

                    // --- MANA DUMP LOGIC ---
                    var manaDump = move.Abilities.OfType<ManaDumpAbility>().FirstOrDefault();
                    if (manaDump != null && _player != null)
                    {
                        powerValue = ((int)(_player.Stats.CurrentMana * manaDump.Multiplier)).ToString();
                        manaValue = _player.Stats.CurrentMana.ToString() + "%"; // Added %
                    }
                }
                else
                {
                    powerValue = accValue = manaValue = impactValue = moveTypeValue = "";
                    valueColor = labelColor;
                }

                spriteBatch.DrawStringSnapped(secondaryFont, powerLabel, new Vector2(bounds.X + horizontalPadding, statsY), labelColor);
                var powerValueSize = secondaryFont.MeasureString(powerValue);
                var powerValuePos = new Vector2(bounds.Center.X - 9 - powerValueSize.Width, statsY);
                spriteBatch.DrawStringSnapped(secondaryFont, powerValue, powerValuePos, valueColor);

                var manaLabelPos = new Vector2(bounds.Center.X + 2, statsY);
                spriteBatch.DrawStringSnapped(secondaryFont, manaLabel, manaLabelPos, labelColor);
                var manaValueSize = secondaryFont.MeasureString(manaValue);
                var manaValuePos = new Vector2(bounds.Right - horizontalPadding - manaValueSize.Width, statsY);
                if (!manaValue.Contains("%"))
                {
                    manaValuePos.X -= 5;
                }
                spriteBatch.DrawStringSnapped(secondaryFont, manaValue, manaValuePos, valueColor);

                currentY += secondaryFont.LineHeight + 2;

                var accLabelPos = new Vector2(bounds.X + horizontalPadding, currentY);
                spriteBatch.DrawStringSnapped(secondaryFont, accLabel, accLabelPos, labelColor);
                var accValueSize = secondaryFont.MeasureString(accValue);
                var accValuePos = new Vector2(bounds.Center.X - 9 - accValueSize.Width, currentY);
                if (!accValue.Contains("%"))
                {
                    accValuePos.X -= 6;
                }
                accValuePos.X += 6;

                Color accColor = valueColor;
                if (move != null && move.Accuracy >= 0)
                {
                    float t = Math.Clamp((100f - move.Accuracy) / 50f, 0f, 1f);
                    accColor = Color.Lerp(valueColor, _global.Palette_Red, t);
                }

                spriteBatch.DrawStringSnapped(secondaryFont, accValue, accValuePos, accColor);

                string offStatVal = move?.OffensiveStat switch
                {
                    OffensiveStatType.Strength => "STR",
                    OffensiveStatType.Intelligence => "INT",
                    OffensiveStatType.Tenacity => "TEN",
                    OffensiveStatType.Agility => "AGI",
                    _ => "---"
                };

                Color offColor = move?.OffensiveStat switch
                {
                    OffensiveStatType.Strength => _global.StatColor_Strength,
                    OffensiveStatType.Intelligence => _global.StatColor_Intelligence,
                    OffensiveStatType.Tenacity => _global.StatColor_Tenacity,
                    OffensiveStatType.Agility => _global.StatColor_Agility,
                    _ => _global.Palette_BrightWhite
                };

                if (move != null && move.ImpactType != ImpactType.Status)
                {
                    spriteBatch.DrawStringSnapped(secondaryFont, "USE", new Vector2(bounds.Center.X + 2, currentY), labelColor);
                    var offStatSize = secondaryFont.MeasureString(offStatVal);
                    var offStatPos = new Vector2(bounds.Right - horizontalPadding - offStatSize.Width, currentY);
                    offStatPos.X -= 6;
                    spriteBatch.DrawStringSnapped(secondaryFont, offStatVal, offStatPos, offColor);
                }

                string targetValue = "";
                if (move != null)
                {
                    targetValue = move.Target switch
                    {
                        TargetType.Single => "SINGLE",
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
                        TargetType.SingleAll => "ANY",
                        TargetType.None => "NONE",
                        _ => ""
                    };
                }

                var impactSize = secondaryFont.MeasureString(impactValue);
                var moveTypeSize = secondaryFont.MeasureString(moveTypeValue);
                const int typeGap = 5;
                float typeY = bounds.Bottom - verticalPadding - secondaryFont.LineHeight;
                float gapCenter = bounds.Center.X + 5;

                if (!string.IsNullOrEmpty(targetValue))
                {
                    var targetValueSize = secondaryFont.MeasureString(targetValue);
                    float yAfterRow2 = currentY + secondaryFont.LineHeight;
                    float availableSpace = typeY - yAfterRow2;
                    var targetValuePos = new Vector2(
                        bounds.X + (bounds.Width - targetValueSize.Width) / 2f,
                        yAfterRow2 + (availableSpace - targetValueSize.Height) / 2f
                    );
                    spriteBatch.DrawStringSnapped(secondaryFont, targetValue, targetValuePos, _global.Palette_DarkGray);
                }

                float impactX = gapCenter - (typeGap / 2f) - impactSize.Width;
                var impactPos = new Vector2(impactX, typeY);

                float moveTypeX = gapCenter + (typeGap / 2f);
                var moveTypePos = new Vector2(moveTypeX, typeY);

                Color impactColor = valueColor;
                Color moveTypeColor = valueColor;
                if (move != null)
                {
                    switch (move.ImpactType)
                    {
                        case ImpactType.Physical:
                            impactColor = _global.Palette_Orange;
                            break;
                        case ImpactType.Magical:
                            impactColor = _global.Palette_LightBlue;
                            break;
                    }
                    switch (move.MoveType)
                    {
                        case MoveType.Spell:
                            moveTypeColor = _global.Palette_LightBlue;
                            break;
                        case MoveType.Action:
                            moveTypeColor = _global.Palette_Orange;
                            break;
                    }
                }

                spriteBatch.DrawStringSnapped(secondaryFont, impactValue, impactPos, impactColor);
                spriteBatch.DrawStringSnapped(secondaryFont, moveTypeValue, moveTypePos, moveTypeColor);

                // --- NEW: Draw "CON" if Contact (Side Panel) ---
                if (move != null && move.MakesContact)
                {
                    string conText = "[ CONTACT ]";
                    var conSize = tertiaryFont.MeasureString(conText);

                    // Position below the last row (USE/TYP)
                    // typeY is the Y of the last row.
                    float conY = typeY + secondaryFont.LineHeight + 2;

                    // Center horizontally in the panel
                    var conPos = new Vector2(
                        bounds.Center.X - conSize.Width / 2f,
                        conY
                    );

                    spriteBatch.DrawStringSnapped(tertiaryFont, conText, conPos, _global.Palette_Red);
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

                    Color finalColor = currentColor;
                    if (currentColor != defaultColor && !isWhitespace && part.EndsWith("%"))
                    {
                        // Try to parse the number before the %
                        string numberPart = part.Substring(0, part.Length - 1);
                        if (int.TryParse(numberPart, out int percent))
                        {
                            float amount = Math.Clamp(percent / 100f, 0f, 1f);
                            finalColor = Color.Lerp(_global.Palette_DarkGray, currentColor, amount);
                        }
                    }

                    currentLine.Add(new ColoredText(part, finalColor));
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
    }
}
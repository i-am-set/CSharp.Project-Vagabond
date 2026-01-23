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
using static ProjectVagabond.Battle.Abilities.InflictStatusStunAbility;

namespace ProjectVagabond.Battle.UI
{
    public class ActionMenu
    {
        public event Action<MoveData, MoveEntry, BattleCombatant>? OnMoveSelected;
        public event Action? OnSwitchMenuRequested;
        public event Action? OnMovesMenuOpened;
        public event Action? OnMainMenuOpened;
        public event Action? OnFleeRequested;
        public event Action? OnSlot2BackRequested;

        public event Action<BattleCombatant>? OnStrikeRequested;

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

        private bool _buttonsInitialized = false;
        private MouseState _previousMouseState;

        private bool _isTooltipScrollingInitialized = false;
        private float _tooltipScrollPosition = 0f;
        private float _tooltipScrollWaitTimer = 0f;
        private float _tooltipMaxScrollToShowEnd = 0f;
        private enum TooltipScrollState { PausedAtStart, ScrollingToEnd, PausedAtEnd }
        private TooltipScrollState _tooltipScrollState = TooltipScrollState.PausedAtStart;

        private bool _isHoverInfoScrollingInitialized = false;
        private float _hoverInfoScrollPosition = 0f;
        private float _hoverInfoScrollWaitTimer = 0f;
        private float _hoverInfoMaxScrollToShowEnd = 0f;
        private enum HoverInfoScrollState { PausedAtStart, ScrollingToEnd, PausedAtEnd }
        private HoverInfoScrollState _hoverInfoScrollState = HoverInfoScrollState.PausedAtStart;
        private MoveData? _lastHoveredMoveForScrolling;

        private const float SCROLL_SPEED = 25f;
        private const float SCROLL_PAUSE_DURATION = 1.5f;
        private const int EXTRA_SCROLL_SPACES = 1;
        private static readonly RasterizerState _clipRasterizerState = new RasterizerState { ScissorTestEnable = true };

        private readonly Queue<float> _clickTimestamps = new Queue<float>();
        private bool _isSpamming = false;
        private float _spamCooldownTimer = 0f;
        private const int SPAM_CLICKS = 3;
        private const float SPAM_WINDOW_SECONDS = 1f;
        private const float SPAM_COOLDOWN_SECONDS = 0.25f;

        public float SharedSwayTimer { get; private set; } = 0f;
        private bool _wasAnyActionHoveredLastFrame = false;

        private MoveButton? _hoveredMoveButton;
        private bool _shouldAttuneButtonPulse = false;

        private const int MOVE_BUTTON_WIDTH = 116;
        private const int MOVE_BUTTON_HEIGHT = 13;

        private const int MOVE_ROW_SPACING = 0;
        private const int MOVE_COL_SPACING = 0;

        private const int MOVE_ROWS = 2;
        private const int MOVE_BLOCK_HEIGHT = MOVE_ROWS * (MOVE_BUTTON_HEIGHT + MOVE_ROW_SPACING) - MOVE_ROW_SPACING;

        private Rectangle _tooltipBounds;

        private const int SPACE_WIDTH = 5;

        public ActionMenu()
        {
            _global = ServiceLocator.Get<Global>();
            _backButton = new Button(Rectangle.Empty, "BACK", enableHoverSway: false) { CustomDefaultTextColor = _global.DullTextColor };
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
            var defaultFont = ServiceLocator.Get<BitmapFont>();
            var actionIconsSheet = spriteManager.ActionIconsSpriteSheet;
            var actionIconRects = spriteManager.ActionIconSourceRects;

            var actButton = new Button(Rectangle.Empty, "ACT", function: "Act", font: defaultFont, enableHoverSway: false)
            {
                CustomHoverTextColor = _global.ButtonHoverColor,
                TextRenderOffset = new Vector2(0, 1),
                EnableTextWave = true,
                WaveEffectType = TextEffectType.DriftWave
            };

            var switchButton = new Button(Rectangle.Empty, "SWITCH", function: "Switch", font: defaultFont, enableHoverSway: false)
            {
                CustomHoverTextColor = _global.ButtonHoverColor,
                TextRenderOffset = new Vector2(0, 1),
                EnableTextWave = true,
                WaveEffectType = TextEffectType.DriftWave
            };

            actButton.OnClick += () => {
                if (_isSpamming) { actButton.TriggerShake(); EventBus.Publish(new GameEvents.AlertPublished { Message = "Spam Prevention" }); return; }
                SetState(MenuState.Moves);
            };

            switchButton.OnClick += () => {
                if (_isSpamming) { switchButton.TriggerShake(); EventBus.Publish(new GameEvents.AlertPublished { Message = "Spam Prevention" }); return; }
                OnSwitchMenuRequested?.Invoke();
            };

            _actionButtons.Add(actButton);
            _actionButtons.Add(switchButton);

            var strikeButton = new TextOverImageButton(Rectangle.Empty, "STRIKE", null, font: secondaryFont, iconTexture: actionIconsSheet, iconSourceRect: actionIconRects[0], enableHoverSway: false, customHoverTextColor: _global.GameTextColor, alignLeft: true)
            {
                HasRightClickHint = false,
                HasMiddleClickHint = true,
                TintBackgroundOnHover = false,
                DrawBorderOnHover = false,
                HoverBorderColor = _global.ButtonHoverColor,
                EnableTextWave = true,
                WaveEffectType = TextEffectType.LeftAlignedSmallWave,
                TintIconOnHover = false,
                IconColorMatchesText = true, // Force icon to match text color
                ContentXOffset = 3f,
                CustomDefaultTextColor = _global.GameTextColor
            };
            strikeButton.OnClick += () => {
                OnStrikeRequested?.Invoke(_player);
            };
            _secondaryActionButtons.Add(strikeButton);

            var stallButton = new TextOverImageButton(Rectangle.Empty, "STALL", null, font: secondaryFont, iconTexture: actionIconsSheet, iconSourceRect: actionIconRects[2], enableHoverSway: false, customHoverTextColor: _global.GameTextColor, alignLeft: true)
            {
                HasRightClickHint = false,
                HasMiddleClickHint = true,
                TintBackgroundOnHover = false,
                DrawBorderOnHover = false,
                HoverBorderColor = _global.ButtonHoverColor,
                EnableTextWave = true,
                WaveEffectType = TextEffectType.LeftAlignedSmallWave,
                TintIconOnHover = false,
                IconColorMatchesText = true, // Force icon to match text color
                ContentXOffset = 3f,
                CustomDefaultTextColor = _global.GameTextColor
            };
            stallButton.OnClick += () => {
                if (BattleDataCache.Moves.TryGetValue("6", out var stallMove))
                {
                    SelectMove(stallMove, null);
                }
            };
            _secondaryActionButtons.Add(stallButton);

            _backButton.Font = secondaryFont;

            _slot2BackButton = new Button(Rectangle.Empty, "BACK", enableHoverSway: false) { CustomDefaultTextColor = _global.DullTextColor };
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

            _previousMouseState = Mouse.GetState();

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
                return;
            }

            SelectMove(move, entry);
        }

        public void SelectMoveExternal(MoveData move, MoveEntry? entry)
        {
            SelectMove(move, entry);
        }

        private void SelectMove(MoveData move, MoveEntry? entry)
        {
            _selectedMove = move;
            _selectedSpellbookEntry = entry;

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
                case MenuState.Main:
                    {
                        const int buttonWidth = 128;
                        const int buttonHeight = 13;
                        const int buttonSpacing = 0; // --- CHANGED: Set to 0 for stacking ---

                        int totalHeight = (buttonHeight * _actionButtons.Count) + (buttonSpacing * (_actionButtons.Count - 1));
                        int availableHeight = Global.VIRTUAL_HEIGHT - 123; // DIVIDER_Y

                        int startY = 123 + (availableHeight - totalHeight) / 2 - 3;

                        int xOffset = -80;
                        if (_player != null && _player.BattleSlot == 1)
                        {
                            xOffset = 80;
                        }

                        int startX = (Global.VIRTUAL_WIDTH - buttonWidth) / 2 + xOffset;

                        int currentY = startY;
                        foreach (var button in _actionButtons)
                        {
                            // Unified offset to 9 to ensure alignment and prevent overlap
                            int specificYOffset = 9;

                            button.Bounds = new Rectangle(startX, currentY + specificYOffset, buttonWidth, buttonHeight);
                            currentY += buttonHeight + buttonSpacing;
                        }
                    }
                    break;
                case MenuState.Moves:
                    const int secButtonWidth = 60;
                    const int secButtonHeight = 13;
                    const int secRowSpacing = 0;
                    const int secRows = 2;
                    const int secBlockHeight = secRows * (secButtonHeight + secRowSpacing) - secRowSpacing;

                    const int gap = 3;

                    int totalWidth = secButtonWidth + gap + (MOVE_BUTTON_WIDTH * 2) + MOVE_COL_SPACING;
                    int startXMoves = (Global.VIRTUAL_WIDTH - totalWidth) / 2;

                    int secGridStartX = startXMoves;
                    int moveGridStartY = 144;
                    int secGridStartY = moveGridStartY + (MOVE_BLOCK_HEIGHT / 2) - (secBlockHeight / 2);

                    for (int i = 0; i < _secondaryActionButtons.Count; i++)
                    {
                        var button = _secondaryActionButtons[i];
                        int yPos = secGridStartY + i * (secButtonHeight + secRowSpacing);

                        button.Bounds = new Rectangle(secGridStartX - 3, yPos, secButtonWidth + 6, secButtonHeight);
                    }

                    int moveGridStartX = secGridStartX + secButtonWidth + gap;

                    for (int i = 0; i < _moveButtons.Length; i++)
                    {
                        var button = _moveButtons[i];
                        if (button == null) continue;

                        int col = i % 2;
                        int row = i / 2;

                        int xPos = moveGridStartX + (col * (MOVE_BUTTON_WIDTH + MOVE_COL_SPACING));
                        int yPos = moveGridStartY + (row * (MOVE_BUTTON_HEIGHT + MOVE_ROW_SPACING));

                        button.Bounds = new Rectangle(xPos, yPos, MOVE_BUTTON_WIDTH, MOVE_BUTTON_HEIGHT);
                    }
                    break;
            }
        }

        private void UpdateSwitchButtonState()
        {
            if (_allCombatants != null && _actionButtons.Count > 1)
            {
                bool hasBench = _allCombatants.Any(c => c.IsPlayerControlled && !c.IsDefeated && c.BattleSlot >= 2);
                _actionButtons[1].IsEnabled = hasBench;
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

            MouseState effectiveMouseState = isInputBlocked
                ? new MouseState(-10000, -10000, currentMouseState.ScrollWheelValue, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released)
                : currentMouseState;

            UpdateLayout();
            UpdateSpamDetection(gameTime, effectiveMouseState);

            HoveredButton = null;

            // --- CHECK FOR MOVE LOCK ---
            string lockedMoveID = null;
            if (_player != null)
            {
                foreach (var ability in _player.Abilities)
                {
                    if (ability is IMoveLockAbility lockAbility)
                    {
                        lockedMoveID = lockAbility.GetLockedMoveID();
                        if (lockedMoveID != null) break;
                    }
                }
            }

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

                    bool isSilenced = _player != null && _player.HasStatusEffect(StatusEffectType.Silence);

                    foreach (var button in _moveButtons)
                    {
                        if (button == null) continue;

                        // --- LOCK LOGIC ---
                        if (lockedMoveID != null && button.Move.MoveID != lockedMoveID)
                        {
                            button.IsEnabled = false;
                        }
                        // --- SILENCE LOGIC ---
                        else if (isSilenced && button.Move.MoveType == MoveType.Spell)
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
                        string? moveId = button.Text switch
                        {
                            "STRIKE" => _player?.DefaultStrikeMoveID,
                            "ATTUNE" => "7",
                            "STALL" => "6",
                            _ => null
                        };

                        MoveData? moveData = null;
                        if (button.Text == "STRIKE" && _player != null)
                        {
                            moveData = _player.StrikeMove;
                        }
                        else if (!string.IsNullOrEmpty(moveId) && BattleDataCache.Moves.TryGetValue(moveId, out var cachedMove))
                        {
                            moveData = cachedMove;
                        }

                        if (moveData != null)
                        {
                            // --- LOCK LOGIC ---
                            if (lockedMoveID != null && moveData.MoveID != lockedMoveID)
                            {
                                button.IsEnabled = false;
                            }
                            // --- SILENCE LOGIC ---
                            else if (isSilenced && moveData.MoveType == MoveType.Spell)
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

                            if (moveData != null)
                            {
                                HoveredMove = moveData;
                                _hoveredSpellbookEntry = null;

                                if (effectiveMouseState.MiddleButton == ButtonState.Pressed)
                                {
                                    middleClickHeldOnAButton = true;
                                    moveForTooltip = moveData;
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
            var bgColor = _global.Palette_Black;

            const int dividerY = 140;

            var bgRect = new Rectangle(0, dividerY + (int)offset.Y, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT - dividerY);
            spriteBatch.DrawSnapped(pixel, bgRect, bgColor);

            if (!_isVisible)
            {
                spriteBatch.DrawSnapped(spriteManager.BattleBorderCombat, offset, Color.White);
                return;
            }

            if (_currentState == MenuState.Main)
            {
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
                        foreach (var button in _actionButtons)
                        {
                            // --- CHANGED: Use exact bounds for hover rect ---
                            // Shrunk by 1 pixel on top and bottom as requested
                            var hoverRect = new Rectangle(
                                button.Bounds.X,
                                button.Bounds.Y + 1 + (int)offset.Y,
                                button.Bounds.Width,
                                button.Bounds.Height - 2
                            );

                            if (button.IsEnabled)
                            {
                                float rotation = button.IsHovered ? button.CurrentHoverRotation : 0f;
                                Color buttonBgColor = button.IsHovered ? _global.Palette_DarkestPale : _global.Palette_DarkShadow;
                                DrawBeveledBackground(spriteBatch, pixel, hoverRect, buttonBgColor, rotation);
                            }

                            button.Draw(spriteBatch, font, gameTime, transform, false, null, offset.Y);
                        }

                        if (_player != null && _player.BattleSlot == 1)
                        {
                            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
                            if (_slot2BackButton.Font == null) _slot2BackButton.Font = secondaryFont;

                            int backButtonHeight = 9;
                            int backButtonY = 169; // +1 pixel

                            var backSize = secondaryFont.MeasureString(_slot2BackButton.Text);
                            int backWidth = (int)backSize.Width + 10; // +10 padding
                            int backX = _actionButtons[0].Bounds.X + (_actionButtons[0].Bounds.Width - backWidth) / 2 + 2;

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
                        const int backButtonPadding = 4; // Reduced padding
                        const int backButtonHeight = 15;
                        const int backButtonTopMargin = 1;
                        const int horizontalPadding = 10;
                        const int verticalPadding = 2;
                        int availableWidth = Global.VIRTUAL_WIDTH - (horizontalPadding * 2);
                        int availableHeight = Global.VIRTUAL_HEIGHT - dividerY - (verticalPadding * 2);
                        int gridAreaHeight = availableHeight - backButtonHeight - backButtonTopMargin;

                        int backButtonWidth = (int)(_backButton.Font ?? font).MeasureString(_backButton.Text).Width + backButtonPadding * 2;
                        _backButton.Bounds = new Rectangle(
                            horizontalPadding + (availableWidth - backButtonWidth) / 2 + 1,
                            170, // +4 pixels
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

            if (_global.ShowSplitMapGrid)
            {
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
                }

                if (_currentState == MenuState.Tooltip)
                {
                    var debugRect = _tooltipBounds;
                    debugRect.Y += (int)offset.Y;
                    spriteBatch.DrawSnapped(pixel, debugRect, Color.Magenta * 0.5f);

                    var backDebugRect = _backButton.Bounds;
                    backDebugRect.Y += (int)offset.Y;
                    spriteBatch.DrawSnapped(pixel, backDebugRect, Color.Red * 0.5f);
                }

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
            const int boxY = 113;
            int boxX = (Global.VIRTUAL_WIDTH - boxWidth) / 2;
            var tooltipBounds = new Rectangle(boxX, boxY + (int)offset.Y, boxWidth, boxHeight);
            _tooltipBounds = tooltipBounds;

            DrawBeveledBackground(spriteBatch, pixel, tooltipBounds, _global.Palette_Black);
            DrawBeveledBorder(spriteBatch, pixel, tooltipBounds, _global.Palette_Sun);

            if (_tooltipMove != null)
            {
                var moveName = _tooltipMove.MoveName.ToUpper();
                var nameSize = font.MeasureString(moveName);
                var namePos = new Vector2(
                    tooltipBounds.Center.X - nameSize.Width / 2,
                    tooltipBounds.Y + 8
                );
                spriteBatch.DrawStringSnapped(font, moveName, namePos, _global.GameTextColor);

                if (!string.IsNullOrEmpty(_tooltipMove.Description))
                {
                    float availableWidth = tooltipBounds.Width - 20;
                    var wrappedLines = ParseAndWrapRichText(secondaryFont, _tooltipMove.Description.ToUpper(), availableWidth, _global.GameTextColor);
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

            int backButtonY = 113 + boxHeight + 10; // +4 pixels
            var backSize = (_backButton.Font ?? font).MeasureString(_backButton.Text);
            int backWidth = (int)backSize.Width; // Exact width
            int backHeight = (int)backSize.Height; // Exact height
            _backButton.Bounds = new Rectangle(
                (Global.VIRTUAL_WIDTH - backWidth) / 2, // Exact center
                backButtonY,
                backWidth,
                backHeight
            );
            _backButton.Draw(spriteBatch, font, gameTime, transform, false, null, offset.Y);
        }

        private void DrawComplexTooltip(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform, Vector2 offset)
        {
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            var pixel = ServiceLocator.Get<Texture2D>();

            const int moveButtonWidth = 157;
            const int columns = 2;
            const int columnSpacing = 0;

            int totalGridWidth = (moveButtonWidth * columns) + columnSpacing;
            const int gridHeight = 40;
            int gridStartX = (Global.VIRTUAL_WIDTH - totalGridWidth) / 2;
            int gridStartY = 123;

            var spriteManager = ServiceLocator.Get<SpriteManager>();
            var tooltipBg = spriteManager.ActionTooltipBackgroundSprite;
            var tooltipBgRect = new Rectangle(gridStartX, gridStartY + (int)offset.Y, totalGridWidth, gridHeight);
            _tooltipBounds = tooltipBgRect;

            DrawBeveledBackground(spriteBatch, pixel, tooltipBgRect, _global.Palette_Black);
            DrawBeveledBorder(spriteBatch, pixel, tooltipBgRect, _global.Palette_Sun);

            if (_tooltipMove != null)
            {
                DrawMoveInfoPanelContent(spriteBatch, _tooltipMove, tooltipBgRect, font, secondaryFont, transform, true, gameTime);
            }

            const int backButtonTopMargin = 0;
            int backButtonY = 123 + gridHeight + backButtonTopMargin + 7; // +4 pixels
            var backSize = (_backButton.Font ?? font).MeasureString(_backButton.Text);
            int backWidth = (int)backSize.Width; // Exact width
            int backHeight = (int)backSize.Height; // Exact height
            _backButton.Bounds = new Rectangle(
                (Global.VIRTUAL_WIDTH - backWidth) / 2, // Exact center
                backButtonY,
                backWidth,
                backHeight
            );
            _backButton.Draw(spriteBatch, font, gameTime, transform, false, null, offset.Y);
        }

        private void DrawMovesMenu(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform, Vector2 offset)
        {
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            var pixel = ServiceLocator.Get<Texture2D>();

            const int secButtonWidth = 60;
            const int layoutGap = 3; // --- CHANGED: Gap set to 3 ---

            // --- MOVED UP 5 PIXELS (149 -> 144) ---
            int moveGridStartY = 144; // Was 149
            // Total width = SecButton + Gap + (Move Button * 2) + Gap
            int totalWidth = secButtonWidth + layoutGap + (MOVE_BUTTON_WIDTH * 2) + MOVE_COL_SPACING;
            int startX = (Global.VIRTUAL_WIDTH - totalWidth) / 2;

            int secGridStartX = startX;
            int moveGridStartX = secGridStartX + secButtonWidth + layoutGap;

            // Draw Secondary Buttons (Strike, Stall)
            for (int i = 0; i < _secondaryActionButtons.Count; i++)
            {
                var button = _secondaryActionButtons[i];
                var buttonRect = new Rectangle(button.Bounds.X, button.Bounds.Y + (int)offset.Y, button.Bounds.Width, button.Bounds.Height);

                // --- CHANGE START ---
                // Calculate visual bounds by shrinking the expanded hitbox back to original size
                // Hitbox was expanded by 3 on left and 3 on right.
                // So visual X is hitbox X + 3. Visual Width is hitbox Width - 6.
                var visualRect = new Rectangle(buttonRect.X + 3, buttonRect.Y, buttonRect.Width - 6, buttonRect.Height);

                // Shift background IN by 2 pixels on sides, 1 pixel on top/bottom relative to VISUAL rect
                var hoverRect = new Rectangle(visualRect.X + 2, visualRect.Y + 1, visualRect.Width - 4, visualRect.Height - 2);
                // --- CHANGE END ---

                bool isHovered = (button is Button btn && btn.IsHovered && btn.IsEnabled);
                Color secBgColor = isHovered ? _global.Palette_DarkestPale : _global.Palette_DarkShadow;
                float rotation = isHovered ? ((Button)button).CurrentHoverRotation : 0f;

                // Always draw background (DarkShadow)
                DrawBeveledBackground(spriteBatch, pixel, hoverRect, secBgColor, rotation);

                button.Draw(spriteBatch, font, gameTime, transform, false, null, offset.Y, button.Text == "ATTUNE" ? (Color?)null : null);
            }

            // Draw Move Buttons (2x2 Grid)
            for (int i = 0; i < _moveButtons.Length; i++)
            {
                var button = _moveButtons[i];

                // Calculate grid position again to be safe (though UpdateLayout handles it)
                int col = i % 2;
                int row = i / 2;
                int xPos = moveGridStartX + (col * (MOVE_BUTTON_WIDTH + MOVE_COL_SPACING));
                int yPos = moveGridStartY + (row * (MOVE_BUTTON_HEIGHT + MOVE_ROW_SPACING));

                var visualBounds = new Rectangle(xPos, yPos, MOVE_BUTTON_WIDTH, MOVE_BUTTON_HEIGHT);

                if (button == null)
                {
                    // --- CHANGED: Placeholder background is now DarkShadow ---
                    var placeholderFillColor = _global.Palette_DarkShadow; // Was Transparent
                    var offsetBounds = new Rectangle(visualBounds.X, visualBounds.Y + (int)offset.Y, visualBounds.Width, visualBounds.Height);

                    // --- CHANGE START ---
                    // Shift placeholder background IN by 1 pixel on all sides
                    var bgRect = new Rectangle(offsetBounds.X + 1, offsetBounds.Y + 1, offsetBounds.Width - 2, offsetBounds.Height - 2);
                    DrawBeveledBackground(spriteBatch, pixel, bgRect, placeholderFillColor, 0f);
                    // --- CHANGE END ---
                }
                else
                {
                    var offsetBounds = new Rectangle(visualBounds.X, visualBounds.Y + (int)offset.Y, visualBounds.Width, visualBounds.Height);

                    // --- CHANGE START ---
                    // Always draw the background plate
                    // Shift background IN by 1 pixel on all sides
                    var bgRect = new Rectangle(offsetBounds.X + 1, offsetBounds.Y + 1, offsetBounds.Width - 2, offsetBounds.Height - 2);
                    bool isHovered = (button.IsHovered && button.IsEnabled);
                    Color moveBgColor = isHovered ? _global.Palette_DarkestPale : _global.Palette_DarkShadow;
                    float rotation = isHovered ? button.CurrentHoverRotation : 0f;
                    DrawBeveledBackground(spriteBatch, pixel, bgRect, moveBgColor, rotation);
                    // --- CHANGE END ---

                    var originalBounds = button.Bounds;
                    button.Bounds = visualBounds;
                    button.Draw(spriteBatch, font, gameTime, transform, false, null, offset.Y);
                    button.Bounds = originalBounds;
                }
            }

            // --- FIXED BACK BUTTON POSITION ---
            int backButtonY = 170; // +4 pixels
            var backSize = (_backButton.Font ?? font).MeasureString(_backButton.Text);
            int backWidth = (int)backSize.Width; // Exact width
            int backHeight = (int)backSize.Height; // Exact height
            _backButton.Bounds = new Rectangle(
                (Global.VIRTUAL_WIDTH - backWidth) / 2, // Exact center
                backButtonY,
                backWidth,
                backHeight
            );
            _backButton.Draw(spriteBatch, font, gameTime, transform, false, null, offset.Y);
        }

        private void DrawBeveledBackground(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color color, float rotation = 0f)
        {
            Vector2 center = rect.Center.ToVector2();
            float cos = MathF.Cos(rotation);
            float sin = MathF.Sin(rotation);

            void DrawRotatedStrip(int relX, int relY, int w, int h)
            {
                float stripCenterX = relX + w / 2f;
                float stripCenterY = relY + h / 2f;
                float rotX = stripCenterX * cos - stripCenterY * sin;
                float rotY = stripCenterX * sin + stripCenterY * cos;
                Vector2 drawPos = center + new Vector2(rotX, rotY);
                Vector2 origin = new Vector2(0.5f, 0.5f);
                spriteBatch.DrawSnapped(pixel, drawPos, null, color, rotation, origin, new Vector2(w, h), SpriteEffects.None, 0f);
            }

            float topY = rect.Y - center.Y;
            float leftX = rect.X - center.X;
            float w = rect.Width;
            float h = rect.Height;

            DrawRotatedStrip((int)(leftX + 2), (int)(topY), (int)(w - 4), 1);
            DrawRotatedStrip((int)(leftX + 1), (int)(topY + 1), (int)(w - 2), 1);
            DrawRotatedStrip((int)(leftX), (int)(topY + 2), (int)(w), (int)(h - 4));
            DrawRotatedStrip((int)(leftX + 1), (int)(topY + h - 2), (int)(w - 2), 1);
            DrawRotatedStrip((int)(leftX + 2), (int)(topY + h - 1), (int)(w - 4), 1);
        }

        private void DrawBeveledBorder(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color color)
        {
            spriteBatch.DrawSnapped(pixel, new Rectangle(rect.X + 2, rect.Y, rect.Width - 4, 1), color);
            spriteBatch.DrawSnapped(pixel, new Rectangle(rect.X + 2, rect.Bottom - 1, rect.Width - 4, 1), color);
            spriteBatch.DrawSnapped(pixel, new Rectangle(rect.X, rect.Y + 2, 1, rect.Height - 4), color);
            spriteBatch.DrawSnapped(pixel, new Rectangle(rect.Right - 1, rect.Y + 2, 1, rect.Height - 4), color);
            spriteBatch.DrawSnapped(pixel, new Vector2(rect.X + 1, rect.Y + 1), color);
            spriteBatch.DrawSnapped(pixel, new Vector2(rect.Right - 2, rect.Y + 1), color);
            spriteBatch.DrawSnapped(pixel, new Vector2(rect.X + 1, rect.Bottom - 2), color);
            spriteBatch.DrawSnapped(pixel, new Vector2(rect.Right - 2, rect.Bottom - 2), color);
        }

        private void DrawMoveInfoPanelContent(SpriteBatch spriteBatch, MoveData? move, Rectangle bounds, BitmapFont font, BitmapFont secondaryFont, Matrix transform, bool isForTooltip, GameTime gameTime, float opacity = 1.0f)
        {
            var tertiaryFont = ServiceLocator.Get<Core>().TertiaryFont;
            const int horizontalPadding = 4;
            const int verticalPadding = 3;
            float currentY = bounds.Y + verticalPadding;

            // Shared Target Value Logic
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

            if (isForTooltip)
            {
                if (move == null) return;

                var moveName = move.MoveName.ToUpper();
                var nameSize = font.MeasureString(moveName);
                var namePos = new Vector2(bounds.X + horizontalPadding, currentY);

                string moveTypeText = move.MoveType.ToString().ToUpper();
                Color moveTypeColor = move.MoveType switch
                {
                    MoveType.Spell => _global.ColorNarration_Spell,
                    MoveType.Action => _global.ColorNarration_Action,
                    _ => _global.ColorNarration_Status
                };

                string impactTypeText = move.ImpactType.ToString().ToUpper();
                Color impactTypeColor = move.ImpactType switch
                {
                    ImpactType.Physical => _global.ColorNarration_Action,
                    ImpactType.Magical => _global.ColorNarration_Spell,
                    _ => _global.ColorNarration_Status
                };

                var statsSegments = new List<(string Text, Color Color, BitmapFont Font)>();

                if (move.ImpactType != ImpactType.Status)
                {
                    string accuracyText = move.Accuracy >= 0 ? $"{move.Accuracy}%" : "---";
                    string powerText = move.Power > 0 ? $"{move.Power}" : (move.Effects.ContainsKey("ManaDamage") ? "???" : "---");

                    var manaDump = move.Abilities.OfType<ManaDumpAbility>().FirstOrDefault();
                    if (manaDump != null && _player != null)
                    {
                        powerText = $"{(int)(_player.Stats.CurrentMana * manaDump.Multiplier)}";
                    }

                    // POW Couple
                    statsSegments.Add(("POW ", _global.DullTextColor, tertiaryFont));
                    statsSegments.Add((powerText, _global.GameTextColor, secondaryFont));

                    // Gap
                    statsSegments.Add(("  ", Color.Transparent, secondaryFont));

                    // ACC Couple
                    statsSegments.Add(("ACC ", _global.DullTextColor, tertiaryFont));
                    statsSegments.Add((accuracyText, _global.GameTextColor, secondaryFont));

                    // MANA
                    string manaText = move.ManaCost > 0 ? $"{move.ManaCost}%" : "---";
                    statsSegments.Add(("  ", Color.Transparent, secondaryFont));
                    statsSegments.Add(("MANA ", _global.DullTextColor, tertiaryFont));
                    statsSegments.Add((manaText, _global.GameTextColor, secondaryFont));

                    // USE (Already inside !Status check)
                    string offStatVal = move.OffensiveStat switch
                    {
                        OffensiveStatType.Strength => "STR",
                        OffensiveStatType.Intelligence => "INT",
                        OffensiveStatType.Tenacity => "TEN",
                        OffensiveStatType.Agility => "AGI",
                        _ => "---"
                    };

                    Color offColor = _global.GameTextColor; // Unified color

                    statsSegments.Add(("  ", Color.Transparent, secondaryFont));
                    statsSegments.Add(("USE ", _global.DullTextColor, tertiaryFont));
                    statsSegments.Add((offStatVal, offColor, secondaryFont));
                    statsSegments.Add((" ", Color.Transparent, secondaryFont)); // The requested space
                }

                float totalStatsWidth = 0f;
                foreach (var segment in statsSegments)
                {
                    totalStatsWidth += segment.Font.MeasureString(segment.Text).Width;
                }

                float moveTypeWidth = tertiaryFont.MeasureString(moveTypeText).Width;
                float impactTypeWidth = tertiaryFont.MeasureString(impactTypeText).Width;
                float stackWidth = Math.Max(moveTypeWidth, impactTypeWidth);

                float totalContentWidth = totalStatsWidth + stackWidth;

                float statsY = (currentY + (nameSize.Height - secondaryFont.LineHeight) / 2) - 1;
                float startX = bounds.Right - horizontalPadding - totalContentWidth;

                float currentX = startX;
                foreach (var segment in statsSegments)
                {
                    float yOffset = (secondaryFont.LineHeight - segment.Font.LineHeight) / 2f;
                    yOffset = MathF.Round(yOffset);

                    spriteBatch.DrawStringSnapped(segment.Font, segment.Text, new Vector2(currentX, statsY + yOffset), segment.Color);
                    currentX += segment.Font.MeasureString(segment.Text).Width;
                }

                float tertiaryYOffset = (secondaryFont.LineHeight - tertiaryFont.LineHeight) / 2f;
                tertiaryYOffset = MathF.Round(tertiaryYOffset);
                float stackBaseY = statsY + tertiaryYOffset;

                spriteBatch.DrawStringSnapped(tertiaryFont, moveTypeText, new Vector2(currentX, stackBaseY + 3), moveTypeColor);
                spriteBatch.DrawStringSnapped(tertiaryFont, impactTypeText, new Vector2(currentX, stackBaseY - 2), impactTypeColor);

                float textAvailableWidth = startX - namePos.X - 4;
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
                    spriteBatch.DrawStringSnapped(font, move.MoveName.ToUpper(), scrollingTextPosition, _global.GameTextColor);

                    spriteBatch.End();
                    spriteBatch.GraphicsDevice.ScissorRectangle = originalScissorRect;
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, originalRasterizerState, null, transform);
                }
                else
                {
                    _isTooltipScrollingInitialized = false;
                    spriteBatch.DrawStringSnapped(font, moveName, namePos, _global.GameTextColor);
                }
                currentY += nameSize.Height + 1;

                var underlineStart = new Vector2(bounds.X + horizontalPadding, currentY);
                var underlineEnd = new Vector2(bounds.Right - horizontalPadding, currentY);
                spriteBatch.DrawLineSnapped(underlineStart, underlineEnd, _global.DullTextColor);
                currentY += 3;

                // --- BOTTOM LEFT INFO (Target & Contact) ---
                float bottomTextY = bounds.Bottom - verticalPadding - tertiaryFont.LineHeight + 1;
                float leftTextX = bounds.X + horizontalPadding;

                if (!string.IsNullOrEmpty(targetValue))
                {
                    spriteBatch.DrawStringSnapped(tertiaryFont, targetValue, new Vector2(leftTextX, bottomTextY), _global.DullTextColor);
                }

                if (move.MakesContact)
                {
                    string contactText = "[CONTACT]";
                    float contactWidth = tertiaryFont.MeasureString(contactText).Width;
                    float rightTextX = bounds.Right - horizontalPadding - contactWidth;
                    spriteBatch.DrawStringSnapped(tertiaryFont, contactText, new Vector2(rightTextX, bottomTextY), _global.Palette_Rust);
                }

                if (!string.IsNullOrEmpty(move.Description))
                {
                    float availableWidth = bounds.Width - (horizontalPadding * 2);
                    var wrappedLines = ParseAndWrapRichText(secondaryFont, move.Description.ToUpper(), availableWidth, _global.GameTextColor);

                    float totalTextHeight = wrappedLines.Count * secondaryFont.LineHeight;
                    // Adjust available height to avoid overlapping bottom text
                    float availableHeight = (bounds.Bottom - verticalPadding - (tertiaryFont.LineHeight + 2)) - currentY;

                    float startY = currentY + (availableHeight - totalTextHeight) / 2f;
                    if (startY < currentY) startY = currentY;

                    float drawY = startY;

                    foreach (var line in wrappedLines)
                    {
                        if (drawY + secondaryFont.LineHeight > bounds.Bottom - verticalPadding) break;

                        float lineWidth = 0;
                        foreach (var segment in line)
                        {
                            if (string.IsNullOrWhiteSpace(segment.Text))
                                lineWidth += segment.Text.Length * SPACE_WIDTH;
                            else
                                lineWidth += secondaryFont.MeasureString(segment.Text).Width;
                        }

                        float lineX = bounds.Center.X - (lineWidth / 2f);
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
                                if (segment.Effect != TextEffectType.None)
                                    TextAnimator.DrawTextWithEffect(spriteBatch, secondaryFont, segment.Text, new Vector2(lineCurrentX, drawY), segment.Color * opacity, segment.Effect, (float)gameTime.TotalGameTime.TotalSeconds);
                                else
                                    spriteBatch.DrawStringSnapped(secondaryFont, segment.Text, new Vector2(lineCurrentX, drawY), segment.Color * opacity);
                            }
                            lineCurrentX += segWidth;
                        }
                        drawY += secondaryFont.LineHeight;
                    }
                }
            }
            else
            {
                // This block was for the old side panel, which is now removed.
                // Keeping it empty or removing it entirely is fine.
            }
        }

        private List<List<ColoredText>> ParseAndWrapRichText(BitmapFont font, string text, float maxWidth, Color defaultColor, int spaceWidth = 5)
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
                    float partWidth = isWhitespace ? (part.Length * spaceWidth) : font.MeasureString(part).Width;

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
                    // Removed percentage color lerping logic here.
                    // Just use the current color (which defaults to defaultColor or the last tag).

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

            if (tag == "cstr") return _global.GameTextColor;
            if (tag == "cint") return _global.GameTextColor;
            if (tag == "cten") return _global.GameTextColor;
            if (tag == "cagi") return _global.GameTextColor;

            if (tag == "cpositive") return _global.ColorPositive;
            if (tag == "cnegative") return _global.ColorNegative;
            if (tag == "ccrit") return _global.ColorCrit;
            if (tag == "cimmune") return _global.ColorImmune;
            if (tag == "cctm") return _global.ColorConditionToMeet;
            if (tag == "cetc") return _global.Palette_DarkShadow;

            if (tag == "cfire") return _global.ElementColors.GetValueOrDefault(2, Color.White);
            if (tag == "cwater") return _global.ElementColors.GetValueOrDefault(3, Color.White);
            if (tag == "carcane") return _global.ElementColors.GetValueOrDefault(4, Color.White);
            if (tag == "cnature") return _global.ElementColors.GetValueOrDefault(5, Color.White);
            if (tag == "cblight") return _global.ElementColors.GetValueOrDefault(9, Color.White);
            if (tag == "cdivine") return _global.ElementColors.GetValueOrDefault(10, Color.White);
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
                default: return Color.Magenta;
            }
        }
    }
}

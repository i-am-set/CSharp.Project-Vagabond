#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace ProjectVagabond.Battle.UI
{
    public class ActionMenu
    {
        public event Action<MoveData, MoveEntry, BattleCombatant>? OnMoveSelected;
        public event Action? OnItemMenuRequested;
        public event Action? OnMovesMenuOpened;
        public event Action? OnMainMenuOpened;
        public event Action? OnFleeRequested;

        private bool _isVisible;
        private BattleCombatant? _player;
        private List<BattleCombatant>? _allCombatants;
        private List<BattleCombatant>? _allTargets;
        private List<Button> _actionButtons = new List<Button>();
        private readonly MoveButton?[] _moveButtons = new MoveButton?[4];
        private List<Button> _secondaryActionButtons = new List<Button>();
        private Button _backButton;
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

        private Queue<ImageButton> _actionButtonsToAnimate = new Queue<ImageButton>();
        private float _actionButtonAnimationDelayTimer = 0f;
        private const float ACTION_BUTTON_SEQUENTIAL_ANIMATION_DELAY = 0.05f;

        private Queue<TextOverImageButton> _secondaryButtonsToAnimate = new Queue<TextOverImageButton>();
        private float _secondaryButtonAnimationDelayTimer = 0f;

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
        private const int MOVE_BUTTON_WIDTH = 118; // Reduced from 128
        private const int MOVE_BUTTON_HEIGHT = 9;
        private const int MOVE_ROW_SPACING = 1;
        private const int MOVE_ROWS = 4;
        private const int MOVE_BLOCK_HEIGHT = MOVE_ROWS * (MOVE_BUTTON_HEIGHT + MOVE_ROW_SPACING) - MOVE_ROW_SPACING;


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
            var actionSheet = spriteManager.ActionButtonsSpriteSheet;
            var rects = spriteManager.ActionButtonSourceRects;
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            var secondaryButtonBg = spriteManager.ActionButtonTemplateSecondarySprite;
            var actionIconsSheet = spriteManager.ActionIconsSpriteSheet;
            var actionIconRects = spriteManager.ActionIconSourceRects;

            // Main Menu Buttons
            var actButton = new ImageButton(Rectangle.Empty, actionSheet, rects[0], rects[1], null, function: "Act", startVisible: false, debugColor: new Color(100, 0, 0, 150));
            var itemButton = new ImageButton(Rectangle.Empty, actionSheet, rects[2], rects[3], null, function: "Item", startVisible: false, debugColor: new Color(0, 100, 0, 150));
            var fleeButton = new ImageButton(Rectangle.Empty, actionSheet, rects[4], rects[5], null, function: "Flee", startVisible: false, debugColor: new Color(0, 0, 100, 150));

            actButton.OnClick += () => {
                if (_isSpamming) { actButton.TriggerShake(); EventBus.Publish(new GameEvents.AlertPublished { Message = "Spam Prevention" }); return; }
                SetState(MenuState.Moves);
            };
            itemButton.OnClick += () => {
                if (_isSpamming) { itemButton.TriggerShake(); EventBus.Publish(new GameEvents.AlertPublished { Message = "Spam Prevention" }); return; }
                OnItemMenuRequested?.Invoke();
            };
            fleeButton.OnClick += () => {
                if (_isSpamming) { fleeButton.TriggerShake(); EventBus.Publish(new GameEvents.AlertPublished { Message = "Spam Prevention" }); return; }
                OnFleeRequested?.Invoke();
            };

            _actionButtons.Add(actButton);
            _actionButtons.Add(itemButton);
            _actionButtons.Add(fleeButton);

            // Secondary Action Buttons
            var strikeButton = new TextOverImageButton(Rectangle.Empty, "STRIKE", secondaryButtonBg, font: secondaryFont, iconTexture: actionIconsSheet, iconSourceRect: actionIconRects[0], enableHoverSway: false, customHoverTextColor: Color.White)
            {
                HasRightClickHint = true,
                TintBackgroundOnHover = false,
                DrawBorderOnHover = true,
                HoverBorderColor = _global.Palette_Red
            };
            strikeButton.OnClick += () => {
                if (_player != null && !string.IsNullOrEmpty(_player.DefaultStrikeMoveID) && BattleDataCache.Moves.TryGetValue(_player.DefaultStrikeMoveID, out var strikeMove))
                {
                    SelectMove(strikeMove, null);
                }
            };
            _secondaryActionButtons.Add(strikeButton);

            var attuneButton = new TextOverImageButton(Rectangle.Empty, "ATTUNE", secondaryButtonBg, font: secondaryFont, iconTexture: actionIconsSheet, iconSourceRect: actionIconRects[3], enableHoverSway: false, customHoverTextColor: Color.White)
            {
                HasRightClickHint = true,
                TintBackgroundOnHover = false,
                DrawBorderOnHover = true,
                HoverBorderColor = _global.Palette_Red
            };
            attuneButton.OnClick += () => {
                if (BattleDataCache.Moves.TryGetValue("Attune", out var attuneMove))
                {
                    SelectMove(attuneMove, null);
                }
            };
            _secondaryActionButtons.Add(attuneButton);

            var stallButton = new TextOverImageButton(Rectangle.Empty, "STALL", secondaryButtonBg, font: secondaryFont, iconTexture: actionIconsSheet, iconSourceRect: actionIconRects[2], enableHoverSway: false, customHoverTextColor: Color.White)
            {
                HasRightClickHint = true,
                TintBackgroundOnHover = false,
                DrawBorderOnHover = true,
                HoverBorderColor = _global.Palette_Red
            };
            stallButton.OnClick += () => {
                if (BattleDataCache.Moves.TryGetValue("Stall", out var stallMove))
                {
                    SelectMove(stallMove, null);
                }
            };
            _secondaryActionButtons.Add(stallButton);

            _backButton.Font = secondaryFont;

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
        }

        public void Show(BattleCombatant player, List<BattleCombatant> allCombatants)
        {
            _isVisible = true;
            _player = player;
            _allCombatants = allCombatants;
            _allTargets = allCombatants.Where(c => !c.IsPlayerControlled && !c.IsDefeated).ToList();
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
                _actionButtonsToAnimate.Clear();
                _actionButtonAnimationDelayTimer = 0f;
                foreach (var button in _actionButtons)
                {
                    if (button is ImageButton imageButton)
                    {
                        imageButton.HideForAnimation();
                        _actionButtonsToAnimate.Enqueue(imageButton);
                    }
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

            var equippedSpells = _player.EquippedSpells;
            if (equippedSpells == null) return;

            for (int i = 0; i < equippedSpells.Length; i++)
            {
                var entry = equippedSpells[i];
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

            var moveButton = new MoveButton(move, entry, displayPower, font, background, spriteManager.ElementIconsSpriteSheet, sourceRect, startVisible) { HasRightClickHint = true };
            moveButton.OnClick += () => HandleMoveButtonClick(move, entry, moveButton);
            return moveButton;
        }

        private void HandleMoveButtonClick(MoveData move, MoveEntry? entry, MoveButton button)
        {
            if (_player.Stats.CurrentMana < move.ManaCost)
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

            switch (move.Target)
            {
                case TargetType.Self:
                    OnMoveSelected?.Invoke(move, entry, _player);
                    break;

                case TargetType.None:
                case TargetType.Every:
                case TargetType.EveryAll:
                    OnMoveSelected?.Invoke(move, entry, null);
                    break;

                case TargetType.Single:
                    var enemies = _allTargets.Where(c => !c.IsDefeated).ToList();
                    if (enemies.Count == 1)
                    {
                        OnMoveSelected?.Invoke(move, entry, enemies[0]);
                    }
                    else
                    {
                        SetState(MenuState.Targeting);
                    }
                    break;

                case TargetType.SingleAll:
                    var allValidTargets = _allCombatants.Where(c => !c.IsDefeated).ToList();
                    if (allValidTargets.Count == 1)
                    {
                        OnMoveSelected?.Invoke(move, entry, allValidTargets[0]);
                    }
                    else
                    {
                        SetState(MenuState.Targeting);
                    }
                    break;
            }
        }

        // ... [Rest of class remains unchanged] ...

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
                    const int borderWidth = 120;
                    const int layoutGap = 2;
                    int moveGridStartY = 128;
                    int moveGridStartX = 75; // Shifted right by 10
                    int secGridStartX = moveGridStartX - layoutGap - secButtonWidth;
                    int secGridStartY = moveGridStartY + (MOVE_BLOCK_HEIGHT / 2) - (secBlockHeight / 2);

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
                    for (int i = 0; i < _secondaryActionButtons.Count; i++)
                    {
                        var button = _secondaryActionButtons[i];
                        int yPos = secGridStartY + i * (secButtonHeight + secRowSpacing);
                        button.Bounds = new Rectangle(secGridStartX, yPos, secButtonWidth, secButtonHeight);
                    }
                    break;
            }
        }

        public void Update(MouseState currentMouseState, GameTime gameTime)
        {
            InitializeButtons();
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (!_isVisible)
            {
                _previousMouseState = currentMouseState;
                return;
            }

            UpdateLayout();
            UpdateSpamDetection(gameTime, currentMouseState);

            if (_actionButtonsToAnimate.Any())
            {
                _actionButtonAnimationDelayTimer += dt;
                if (_actionButtonAnimationDelayTimer >= ACTION_BUTTON_SEQUENTIAL_ANIMATION_DELAY)
                {
                    _actionButtonAnimationDelayTimer = 0f;
                    var buttonToAnimate = _actionButtonsToAnimate.Dequeue();
                    buttonToAnimate.TriggerAppearAnimation();
                }
            }

            HoveredButton = null;

            switch (_currentState)
            {
                case MenuState.Main:
                    bool isAnyActionHovered = false;
                    foreach (var button in _actionButtons)
                    {
                        button.Update(currentMouseState);
                        if (button.IsHovered)
                        {
                            isAnyActionHovered = true;
                            HoveredButton = button;
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

                    bool rightClickHeldOnAButton = false;
                    MoveData? moveForTooltip = null;
                    bool simpleTooltip = false;

                    foreach (var button in _moveButtons)
                    {
                        if (button == null) continue;
                        button.Update(currentMouseState);
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

                            if (currentMouseState.RightButton == ButtonState.Pressed)
                            {
                                rightClickHeldOnAButton = true;
                                moveForTooltip = button.Move;
                                simpleTooltip = false;
                            }
                        }
                    }

                    foreach (var button in _secondaryActionButtons)
                    {
                        button.Update(currentMouseState);
                        if (button.IsHovered)
                        {
                            HoveredButton = button;
                            string? moveId = button.Text switch
                            {
                                "STRIKE" => _player?.DefaultStrikeMoveID,
                                "ATTUNE" => "Attune",
                                "STALL" => "Stall",
                                _ => null
                            };

                            if (!string.IsNullOrEmpty(moveId) && BattleDataCache.Moves.TryGetValue(moveId, out var move))
                            {
                                HoveredMove = move;
                                _hoveredSpellbookEntry = null;

                                if (currentMouseState.RightButton == ButtonState.Pressed)
                                {
                                    rightClickHeldOnAButton = true;
                                    moveForTooltip = move;
                                    simpleTooltip = false;
                                }
                            }
                        }
                    }

                    if (rightClickHeldOnAButton)
                    {
                        _tooltipMove = moveForTooltip;
                        _useSimpleTooltip = simpleTooltip;
                        SetState(MenuState.Tooltip);
                    }

                    // --- Right Click to Back Logic ---
                    if (currentMouseState.RightButton == ButtonState.Pressed && _previousMouseState.RightButton == ButtonState.Released)
                    {
                        if (!rightClickHeldOnAButton)
                        {
                            GoBack();
                        }
                    }

                    if (HoveredMove != _lastHoveredMoveForScrolling)
                    {
                        _isHoverInfoScrollingInitialized = false;
                        _lastHoveredMoveForScrolling = HoveredMove;
                    }

                    UpdateHoverInfoScrolling(gameTime);

                    _backButton.Update(currentMouseState);
                    if (_backButton.IsHovered) HoveredButton = _backButton;
                    break;
                case MenuState.Targeting:
                    _targetingTextAnimTimer += dt;
                    _backButton.Update(currentMouseState);
                    if (_backButton.IsHovered) HoveredButton = _backButton;
                    break;
                case MenuState.Tooltip:
                    if (currentMouseState.RightButton == ButtonState.Released)
                    {
                        SetState(MenuState.Moves);
                    }
                    UpdateTooltipScrolling(gameTime);
                    _backButton.Update(currentMouseState);
                    if (_backButton.IsHovered) HoveredButton = _backButton;
                    break;
            }

            _previousMouseState = currentMouseState;
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            InitializeButtons();

            var spriteManager = ServiceLocator.Get<SpriteManager>();
            var pixel = ServiceLocator.Get<Texture2D>();
            var bgColor = _global.Palette_Black; // Changed to fully opaque black
            const int dividerY = 123;
            var bgRect = new Rectangle(0, dividerY, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT - dividerY);
            spriteBatch.DrawSnapped(pixel, bgRect, bgColor);

            if (!_isVisible) return;

            // Draw the specific border based on state
            if (_currentState == MenuState.Main)
            {
                spriteBatch.DrawSnapped(spriteManager.BattleBorderMain, Vector2.Zero, Color.White);
            }
            else if (_currentState == MenuState.Moves || _currentState == MenuState.Targeting || _currentState == MenuState.Tooltip)
            {
                spriteBatch.DrawSnapped(spriteManager.BattleBorderAction, Vector2.Zero, Color.White);
            }

            switch (_currentState)
            {
                case MenuState.Main:
                    {
                        const int buttonWidth = 96;
                        const int buttonHeight = 43;
                        const int buttonSpacing = 5;

                        int totalWidth = (buttonWidth * _actionButtons.Count) + (buttonSpacing * (_actionButtons.Count - 1));
                        int startX = (Global.VIRTUAL_WIDTH - totalWidth) / 2;

                        int availableHeight = Global.VIRTUAL_HEIGHT - dividerY;
                        int startY = dividerY + (availableHeight - buttonHeight) / 2 - 4;

                        int currentX = startX;
                        foreach (var button in _actionButtons)
                        {
                            button.Bounds = new Rectangle(currentX, startY, buttonWidth, buttonHeight);
                            if (button is ImageButton imageButton)
                            {
                                imageButton.Draw(spriteBatch, font, gameTime, transform, false);
                            }
                            else
                            {
                                button.Draw(spriteBatch, font, gameTime, transform);
                            }
                            currentX += buttonWidth + buttonSpacing;
                        }
                        break;
                    }
                case MenuState.Moves:
                    {
                        DrawMovesMenu(spriteBatch, font, gameTime, transform);
                        break;
                    }
                case MenuState.Targeting:
                    {
                        const int backButtonPadding = 8;
                        const int backButtonHeight = 7; // Increased from 5
                        const int backButtonTopMargin = 1;
                        const int horizontalPadding = 10;
                        const int verticalPadding = 2;
                        int availableWidth = Global.VIRTUAL_WIDTH - (horizontalPadding * 2);
                        int availableHeight = Global.VIRTUAL_HEIGHT - dividerY - (verticalPadding * 2);
                        int gridAreaHeight = availableHeight - backButtonHeight - backButtonTopMargin;
                        int gridStartY = dividerY + verticalPadding + 4;

                        string text = "CHOOSE A TARGET";
                        Vector2 textSize = font.MeasureString(text);

                        float animX = MathF.Sin(_targetingTextAnimTimer * 4f) * 1f;
                        float animY = MathF.Sin(_targetingTextAnimTimer * 8f) * 1f;
                        Vector2 animOffset = new Vector2(animX, animY);

                        Vector2 textPos = new Vector2(
                            horizontalPadding + (availableWidth - textSize.X) / 2,
                            gridStartY + (gridAreaHeight - textSize.Y) / 2 - 10
                        ) + animOffset;
                        spriteBatch.DrawStringSnapped(font, text, textPos, Color.Red);

                        int backButtonWidth = (int)(_backButton.Font ?? font).MeasureString(_backButton.Text).Width + backButtonPadding * 2;
                        _backButton.Bounds = new Rectangle(
                            horizontalPadding + (availableWidth - backButtonWidth) / 2 + 1, // Added +1 to X
                            gridStartY + gridAreaHeight + backButtonTopMargin - 9, // Moved up by 1 pixel (was -8)
                            backButtonWidth,
                            backButtonHeight
                        );
                        _backButton.Draw(spriteBatch, font, gameTime, transform);
                        break;
                    }
                case MenuState.Tooltip:
                    {
                        if (_useSimpleTooltip)
                        {
                            DrawSimpleTooltip(spriteBatch, font, gameTime, transform);
                        }
                        else
                        {
                            DrawComplexTooltip(spriteBatch, font, gameTime, transform);
                        }
                        break;
                    }
            }
        }

        private void DrawSimpleTooltip(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            var pixel = ServiceLocator.Get<Texture2D>();

            const int boxWidth = 294;
            const int boxHeight = 47;
            const int boxY = 117;
            int boxX = (Global.VIRTUAL_WIDTH - boxWidth) / 2;
            var tooltipBounds = new Rectangle(boxX, boxY, boxWidth, boxHeight);

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
                    var wrappedLines = WrapText(_tooltipMove.Description.ToUpper(), availableWidth, secondaryFont);
                    float currentY = namePos.Y + nameSize.Height + 6;

                    foreach (var line in wrappedLines)
                    {
                        var lineSize = secondaryFont.MeasureString(line);
                        var linePos = new Vector2(
                            tooltipBounds.Center.X - lineSize.Width / 2,
                            currentY
                        );
                        spriteBatch.DrawStringSnapped(secondaryFont, line, linePos, _global.Palette_White);
                        currentY += secondaryFont.LineHeight;
                    }
                }
            }

            int backButtonY = tooltipBounds.Bottom + 5; // Moved down by 2 pixels (was +3)
            var backSize = (_backButton.Font ?? font).MeasureString(_backButton.Text);
            int backWidth = (int)backSize.Width + 16;
            _backButton.Bounds = new Rectangle(
                (Global.VIRTUAL_WIDTH - backWidth) / 2 + 1, // Added +1 to X
                backButtonY,
                backWidth,
                7 // Increased from 5
            );
            _backButton.Draw(spriteBatch, font, gameTime, transform);
        }

        private void DrawComplexTooltip(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
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
            int gridStartY = dividerY + 2 + 12;

            var spriteManager = ServiceLocator.Get<SpriteManager>();
            var tooltipBg = spriteManager.ActionTooltipBackgroundSprite;
            var tooltipBgRect = new Rectangle(gridStartX, gridStartY, totalGridWidth, gridHeight);

            // Draw opaque black background
            spriteBatch.DrawSnapped(pixel, tooltipBgRect, _global.Palette_Black);

            spriteBatch.DrawSnapped(tooltipBg, tooltipBgRect, Color.White);

            if (_tooltipMove != null)
            {
                DrawMoveInfoPanelContent(spriteBatch, _tooltipMove, tooltipBgRect, font, secondaryFont, transform, true);
            }

            const int backButtonTopMargin = 0;
            int backButtonY = gridStartY + gridHeight + backButtonTopMargin + 2; // Moved down by 2 pixels (was +0)
            var backSize = (_backButton.Font ?? font).MeasureString(_backButton.Text);
            int backWidth = (int)backSize.Width + 16;
            _backButton.Bounds = new Rectangle(
                (Global.VIRTUAL_WIDTH - backWidth) / 2 + 1, // Added +1 to X
                backButtonY,
                backWidth,
                7 // Increased from 5
            );
            _backButton.Draw(spriteBatch, font, gameTime, transform);
        }

        private void DrawMovesMenu(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            var pixel = ServiceLocator.Get<Texture2D>();

            const int secButtonWidth = 60;

            const int borderWidth = 120;
            const int borderHeight = 37;
            const int layoutGap = 2;

            int moveGridStartY = 128;
            int moveGridStartX = 75; // Shifted right by 10
            int borderX = moveGridStartX + MOVE_BUTTON_WIDTH + layoutGap;
            int borderY = moveGridStartY + (MOVE_BLOCK_HEIGHT / 2) - (borderHeight / 2);


            var borderRect = new Rectangle(borderX, borderY, borderWidth, borderHeight);
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
                    spriteBatch.DrawSnapped(pixel, visualBounds, placeholderFillColor);
                    var placeholderBorderColor = _global.Palette_DarkGray;

                    const int dashLength = 1;
                    const int gapLength = 3;
                    const int patternLength = dashLength + gapLength;

                    int lineY = visualBounds.Center.Y;
                    int lineStartX = visualBounds.Left + 3;
                    int lineEndX = visualBounds.Right - 1;

                    for (int x = lineStartX; x < lineEndX; x += patternLength)
                    {
                        int width = Math.Min(dashLength, lineEndX - x);
                        spriteBatch.DrawSnapped(pixel, new Rectangle(x, lineY, width, 1), placeholderBorderColor);
                    }
                }
                else
                {
                    var buttonBorderColor = _global.Palette_DarkGray;
                    if (_player != null && _player.Stats.CurrentMana < button.Move.ManaCost)
                    {
                        buttonBorderColor = Color.Transparent;
                    }
                    spriteBatch.DrawSnapped(pixel, new Rectangle(visualBounds.Left, visualBounds.Top, visualBounds.Width, 1), buttonBorderColor); // Top
                    spriteBatch.DrawSnapped(pixel, new Rectangle(visualBounds.Left, visualBounds.Bottom - 1, visualBounds.Width, 1), buttonBorderColor); // Bottom
                    spriteBatch.DrawSnapped(pixel, new Rectangle(visualBounds.Left, visualBounds.Top, 1, visualBounds.Height), buttonBorderColor); // Left
                    spriteBatch.DrawSnapped(pixel, new Rectangle(visualBounds.Right - 1, visualBounds.Top, 1, visualBounds.Height), buttonBorderColor); // Right

                    if (button == _hoveredMoveButton && button.IsEnabled)
                    {
                        var hoverBgRect = new Rectangle(visualBounds.X + 1, visualBounds.Y + 1, visualBounds.Width - 2, visualBounds.Height - 2);
                        spriteBatch.DrawSnapped(pixel, hoverBgRect, _global.Palette_DarkGray);
                    }

                    var originalBounds = button.Bounds;
                    button.Bounds = visualBounds;
                    button.Draw(spriteBatch, font, gameTime, transform);
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
                button.Draw(spriteBatch, font, gameTime, transform, false, null, null, button.Text == "ATTUNE" ? attunePulseColor : null);
            }


            int layoutBottomY = Math.Max(borderRect.Bottom, moveGridStartY + MOVE_BLOCK_HEIGHT);
            int backButtonY = layoutBottomY + 3; // Moved up by 1 pixel (was +4)
            var backSize = (_backButton.Font ?? font).MeasureString(_backButton.Text);
            int backWidth = (int)backSize.Width + 16;
            _backButton.Bounds = new Rectangle(
                (Global.VIRTUAL_WIDTH - backWidth) / 2 + 1, // Added +1 to X
                backButtonY,
                backWidth,
                7 // Increased from 5
            );
            _backButton.Draw(spriteBatch, font, gameTime, transform);
        }

        private void DrawMoveInfoPanelContent(SpriteBatch spriteBatch, MoveData? move, Rectangle bounds, BitmapFont font, BitmapFont secondaryFont, Matrix transform, bool isForTooltip)
        {
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
                    string powerText = move.Power > 0 ? $"POW: {move.Power}" : "POW: ---";

                    statsSegments.Insert(0, (separator, _global.Palette_DarkGray));
                    statsSegments.Insert(0, (accuracyText, _global.Palette_White));
                    statsSegments.Insert(0, (separator, _global.Palette_DarkGray));
                    statsSegments.Insert(0, (powerText, _global.Palette_White));
                }

                float totalStatsWidth = statsSegments.Sum(s => secondaryFont.MeasureString(s.Text).Width);
                float statsY = currentY + (nameSize.Height - secondaryFont.LineHeight) / 2;
                float statsStartX = bounds.Right - horizontalPadding - totalStatsWidth;

                float currentX = statsStartX;
                foreach (var segment in statsSegments)
                {
                    spriteBatch.DrawStringSnapped(secondaryFont, segment.Text, new Vector2(currentX, statsY), segment.Color);
                    currentX += secondaryFont.MeasureString(segment.Text).Width;
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
                    var wrappedLines = WrapText(move.Description.ToUpper(), availableWidth, secondaryFont);
                    foreach (var line in wrappedLines)
                    {
                        if (currentY + secondaryFont.LineHeight > bounds.Bottom - verticalPadding) break;
                        var descPos = new Vector2(bounds.X + horizontalPadding, currentY);
                        spriteBatch.DrawStringSnapped(secondaryFont, line, descPos, _global.Palette_White);
                        currentY += secondaryFont.LineHeight;
                    }
                }
            }
            else
            {
                float statsY = currentY;
                Color valueColor = _global.Palette_White;
                Color labelColor = _global.Palette_DarkGray;

                string powerLabel = "POWE:";
                string accLabel = "ACCU:";
                string manaLabel = "MANA:";

                string powerValue, accValue, manaValue, impactValue, moveTypeValue;

                if (move != null)
                {
                    powerValue = move.Power > 0 ? move.Power.ToString() : "---";
                    accValue = move.Accuracy >= 0 ? $"{move.Accuracy}%" : "---";
                    manaValue = move.ManaCost > 0 ? $"{move.ManaCost}%" : "---";
                    impactValue = move.ImpactType.ToString().ToUpper();
                    moveTypeValue = move.MoveType.ToString().ToUpper();
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

                var accLabelPos = new Vector2(bounds.Center.X + 2, statsY);
                spriteBatch.DrawStringSnapped(secondaryFont, accLabel, accLabelPos, labelColor);
                var accValueSize = secondaryFont.MeasureString(accValue);
                var accValuePos = new Vector2(bounds.Right - horizontalPadding - accValueSize.Width, statsY);
                if (!accValue.Contains("%"))
                {
                    accValuePos.X -= 5;
                }
                spriteBatch.DrawStringSnapped(secondaryFont, accValue, accValuePos, valueColor);

                currentY += secondaryFont.LineHeight + 2;

                var manaLabelPos = new Vector2(bounds.X + horizontalPadding, currentY);
                spriteBatch.DrawStringSnapped(secondaryFont, manaLabel, manaLabelPos, labelColor);
                var manaValueSize = secondaryFont.MeasureString(manaValue);
                var manaValuePos = new Vector2(bounds.Center.X - 3 - manaValueSize.Width, currentY);
                if (!manaValue.Contains("%"))
                {
                    manaValuePos.X -= 6;
                }
                spriteBatch.DrawStringSnapped(secondaryFont, manaValue, manaValuePos, valueColor);

                string targetValue = "";
                if (move != null)
                {
                    targetValue = move.Target switch
                    {
                        TargetType.Single => "SINGLE",
                        TargetType.Every => "MULTI",
                        TargetType.SingleAll => "ANY",
                        TargetType.EveryAll => "ALL",
                        TargetType.Self => "SELF",
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
                    float yAfterMana = currentY + secondaryFont.LineHeight;
                    float availableSpace = typeY - yAfterMana;
                    var targetValuePos = new Vector2(
                        bounds.X + (bounds.Width - targetValueSize.Width) / 2f,
                        yAfterMana + (availableSpace - targetValueSize.Height) / 2f
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
            }
        }


        private List<string> WrapText(string text, float maxLineWidth, BitmapFont font)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(text)) return lines;

            var words = text.Split(' ');
            var currentLine = new StringBuilder();

            foreach (var word in words)
            {
                var testLine = currentLine.Length > 0 ? currentLine.ToString() + " " + word : word;
                if (font.MeasureString(testLine).Width > maxLineWidth)
                {
                    lines.Add(currentLine.ToString());
                    currentLine.Clear();
                    currentLine.Append(word);
                }
                else
                {
                    if (currentLine.Length > 0)
                        currentLine.Append(" ");
                    currentLine.Append(word);
                }
            }

            if (currentLine.Length > 0)
                lines.Add(currentLine.ToString());

            return lines;
        }
    }
}
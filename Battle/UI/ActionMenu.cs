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
        public event Action<MoveData, SpellbookEntry, BattleCombatant>? OnMoveSelected;
        public event Action? OnItemMenuRequested;
        public event Action? OnMovesMenuOpened;
        public event Action? OnMainMenuOpened;
        public event Action? OnFleeRequested;

        private bool _isVisible;
        private BattleCombatant? _player;
        private List<BattleCombatant>? _allCombatants;
        private List<BattleCombatant>? _allTargets;
        private List<Button> _actionButtons = new List<Button>();
        private List<MoveButton> _moveButtons = new List<MoveButton>();
        private List<Button> _secondaryActionButtons = new List<Button>();
        private Button _backButton;
        private readonly Global _global;

        public enum MenuState { Main, Moves, Targeting, Tooltip, AnimatingHandDiscard }
        private MenuState _currentState;
        public MenuState CurrentMenuState => _currentState;
        private MoveData? _selectedMove;
        private SpellbookEntry? _selectedSpellbookEntry;
        public MoveData? SelectedMove => _selectedMove;
        public SpellbookEntry? SelectedSpellbookEntry => _selectedSpellbookEntry;
        private MoveData? _tooltipMove;
        private bool _useSimpleTooltip;
        public MoveData? HoveredMove { get; private set; }
        private SpellbookEntry? _hoveredSpellbookEntry;
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

        // State for animation
        private readonly HashSet<SpellbookEntry> _newlyDrawnEntries = new HashSet<SpellbookEntry>();
        private enum ButtonAnimationPhase { Hidden, AnimatingNewText, AnimatingButton, Idle }
        private readonly Dictionary<MoveButton, (ButtonAnimationPhase phase, float timer)> _buttonAnimationStates = new();
        private readonly Queue<MoveButton> _pendingButtonAnimations = new Queue<MoveButton>();
        private float _animationDelayTimer = 0f;
        private const float SEQUENTIAL_ANIMATION_DELAY = 0.05f;

        // "NEW!" Text Animation Tuning
        private const float NEW_TEXT_FADE_IN_DURATION = 0.0375f;
        private const float NEW_TEXT_HOLD_DURATION = 0.05f;
        private const float NEW_TEXT_FADE_OUT_DURATION = 0.05f;
        private const float NEW_TEXT_TOTAL_DURATION = NEW_TEXT_FADE_IN_DURATION + NEW_TEXT_HOLD_DURATION + NEW_TEXT_FADE_OUT_DURATION;

        private Queue<ImageButton> _actionButtonsToAnimate = new Queue<ImageButton>();
        private float _actionButtonAnimationDelayTimer = 0f;
        private const float ACTION_BUTTON_SEQUENTIAL_ANIMATION_DELAY = 0.05f;

        private Queue<TextOverImageButton> _secondaryButtonsToAnimate = new Queue<TextOverImageButton>();
        private float _secondaryButtonAnimationDelayTimer = 0f;

        // Spam Click Prevention State
        private readonly Queue<float> _clickTimestamps = new Queue<float>();
        private bool _isSpamming = false;
        private float _spamCooldownTimer = 0f;
        private const int SPAM_CLICKS = 3; // Number of clicks to be considered spam
        private const float SPAM_WINDOW_SECONDS = 1f; // Time window to check for spam
        private const float SPAM_COOLDOWN_SECONDS = 0.25f; // Time to wait after spamming stops

        // Shared animation timer for main action buttons
        public float SharedSwayTimer { get; private set; } = 0f;
        private bool _wasAnyActionHoveredLastFrame = false;

        // Discard Animation State
        private MoveData? _deferredMove;
        private SpellbookEntry? _deferredSpellbookEntry;
        private MoveButton? _selectedMoveButton;
        private readonly List<MoveButton> _buttonsBeingDiscarded = new List<MoveButton>();
        private static readonly Random _random = new Random();

        // Hover Box Animation
        private MoveButton? _hoveredMoveButton;


        public ActionMenu()
        {
            _global = ServiceLocator.Get<Global>();
            _backButton = new Button(Rectangle.Empty, "BACK") { CustomDefaultTextColor = _global.Palette_Gray };
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
            EventBus.Subscribe<GameEvents.PlayerHandDrawn>(OnPlayerHandDrawn);
        }

        private void OnPlayerHandDrawn(GameEvents.PlayerHandDrawn e)
        {
            _newlyDrawnEntries.Clear();
            foreach (var entry in e.DrawnEntries)
            {
                _newlyDrawnEntries.Add(entry);
            }
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
            var strikeButton = new TextOverImageButton(Rectangle.Empty, "STRIKE", secondaryButtonBg, font: secondaryFont, iconTexture: actionIconsSheet, iconSourceRect: actionIconRects[0]) { HasRightClickHint = true };
            strikeButton.OnClick += () => {
                if (_player != null && !string.IsNullOrEmpty(_player.DefaultStrikeMoveID) && BattleDataCache.Moves.TryGetValue(_player.DefaultStrikeMoveID, out var strikeMove))
                {
                    SelectMove(strikeMove, null);
                }
            };
            _secondaryActionButtons.Add(strikeButton);

            var dodgeButton = new TextOverImageButton(Rectangle.Empty, "DODGE", secondaryButtonBg, font: secondaryFont, iconTexture: actionIconsSheet, iconSourceRect: actionIconRects[1]) { HasRightClickHint = true };
            dodgeButton.OnClick += () => {
                if (BattleDataCache.Moves.TryGetValue("Dodge", out var dodgeMove))
                {
                    SelectMove(dodgeMove, null);
                }
            };
            _secondaryActionButtons.Add(dodgeButton);

            var stallButton = new TextOverImageButton(Rectangle.Empty, "STALL", secondaryButtonBg, font: secondaryFont, iconTexture: actionIconsSheet, iconSourceRect: actionIconRects[2]) { HasRightClickHint = true };
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
                button.ResetAnimationState();
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

            // If we are leaving the moves menu, mark all newly drawn cards as "seen"
            if (oldState == MenuState.Moves && newState != MenuState.Moves)
            {
                _newlyDrawnEntries.Clear();
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

                _secondaryButtonsToAnimate.Clear();
                _secondaryButtonAnimationDelayTimer = 0f;
                foreach (var button in _secondaryActionButtons)
                {
                    if (button is TextOverImageButton toib)
                    {
                        toib.HideForAnimation();
                        _secondaryButtonsToAnimate.Enqueue(toib);
                    }
                }
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
            else if (newState == MenuState.AnimatingHandDiscard)
            {
                _selectedMoveButton?.TriggerDiscardAnimation();
            }
        }

        private void PopulateMoveButtons()
        {
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            var spriteManager = ServiceLocator.Get<SpriteManager>();

            _moveButtons.Clear();
            _buttonAnimationStates.Clear();
            _pendingButtonAnimations.Clear();
            _animationDelayTimer = 0f;

            var currentHand = _player.Hand;
            if (currentHand == null) return;

            for (int i = 0; i < Math.Min(currentHand.Length, 4); i++)
            {
                var entry = currentHand[i];
                if (entry == null || !BattleDataCache.Moves.TryGetValue(entry.MoveID, out var move)) continue;

                bool isNewForAnimation = _newlyDrawnEntries.Contains(entry);

                int effectivePower = DamageCalculator.GetEffectiveMovePower(_player, move);
                var moveButton = CreateMoveButton(move, entry, effectivePower, secondaryFont, spriteManager.ActionButtonTemplateSpriteSheet, isNewForAnimation, false);
                _moveButtons.Add(moveButton);

                if (isNewForAnimation)
                {
                    // If it's new, queue it for the "NEW!" animation sequence.
                    _buttonAnimationStates[moveButton] = (ButtonAnimationPhase.Hidden, 0f);
                    _pendingButtonAnimations.Enqueue(moveButton);
                }
                else
                {
                    // If it's not new, make it appear instantly.
                    moveButton.ShowInstantly();
                    _buttonAnimationStates[moveButton] = (ButtonAnimationPhase.Idle, 0f);
                }
            }
        }

        private MoveButton CreateMoveButton(MoveData move, SpellbookEntry entry, int displayPower, BitmapFont font, Texture2D background, bool isNew, bool startVisible)
        {
            var spriteManager = ServiceLocator.Get<SpriteManager>();
            int elementId = move.OffensiveElementIDs.FirstOrDefault();
            Rectangle? sourceRect = null;
            if (spriteManager.ElementIconSourceRects.TryGetValue(elementId, out var rect))
            {
                sourceRect = rect;
            }

            var moveButton = new MoveButton(move, entry, displayPower, font, background, spriteManager.ElementIconsSpriteSheet, sourceRect, isNew, startVisible) { HasRightClickHint = true };
            moveButton.OnClick += () => HandleMoveButtonClick(move, entry, moveButton);
            return moveButton;
        }

        private void HandleMoveButtonClick(MoveData move, SpellbookEntry? entry, MoveButton button)
        {
            if (_player.Stats.CurrentMana < move.ManaCost)
            {
                EventBus.Publish(new GameEvents.AlertPublished { Message = "NOT ENOUGH MANA" });
                return;
            }

            // Only trigger discard animation for spells from the hand
            if (move.MoveType == MoveType.Spell && entry != null)
            {
                _deferredMove = move;
                _deferredSpellbookEntry = entry;
                _selectedMoveButton = button;
                SetState(MenuState.AnimatingHandDiscard);
            }
            else
            {
                SelectMove(move, entry);
            }
        }

        private void SelectMove(MoveData move, SpellbookEntry? entry)
        {
            _selectedMove = move;
            _selectedSpellbookEntry = entry;

            if (entry != null)
            {
                _newlyDrawnEntries.Remove(entry);
            }

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

        public void Update(MouseState currentMouseState, GameTime gameTime)
        {
            InitializeButtons();
            if (!_isVisible) return;

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

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

            if (_pendingButtonAnimations.Any())
            {
                _animationDelayTimer += dt;
                if (_animationDelayTimer >= SEQUENTIAL_ANIMATION_DELAY)
                {
                    _animationDelayTimer = 0f;
                    var buttonToStart = _pendingButtonAnimations.Dequeue();
                    if (buttonToStart.IsNew)
                    {
                        _buttonAnimationStates[buttonToStart] = (ButtonAnimationPhase.AnimatingNewText, 0f);
                    }
                }
            }

            if (_secondaryButtonsToAnimate.Any())
            {
                _secondaryButtonAnimationDelayTimer += dt;
                if (_secondaryButtonAnimationDelayTimer >= SEQUENTIAL_ANIMATION_DELAY)
                {
                    _secondaryButtonAnimationDelayTimer = 0f;
                    var buttonToAnimate = _secondaryButtonsToAnimate.Dequeue();
                    buttonToAnimate.TriggerAppearAnimation();
                }
            }

            HoveredButton = null; // Reset at the start of each frame

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
                        SharedSwayTimer = 0f; // Reset when mouse leaves the button group
                    }
                    _wasAnyActionHoveredLastFrame = isAnyActionHovered;
                    break;
                case MenuState.Moves:
                    HoveredMove = null;
                    _hoveredSpellbookEntry = null;
                    _hoveredMoveButton = null;

                    // Update button animation states
                    foreach (var button in _moveButtons)
                    {
                        if (_buttonAnimationStates.TryGetValue(button, out var state))
                        {
                            if (state.phase == ButtonAnimationPhase.AnimatingNewText)
                            {
                                state.timer += dt;
                                if (state.timer >= NEW_TEXT_TOTAL_DURATION)
                                {
                                    state.phase = ButtonAnimationPhase.Idle;
                                    button.ShowInstantly();
                                }
                                _buttonAnimationStates[button] = state;
                            }
                        }
                    }

                    bool rightClickHeldOnAButton = false;
                    MoveData? moveForTooltip = null;
                    bool simpleTooltip = false;

                    // Update buttons and check for hover/right-click
                    foreach (var button in _moveButtons)
                    {
                        button.Update(currentMouseState);
                        if (button.IsHovered)
                        {
                            HoveredMove = button.Move;
                            _hoveredSpellbookEntry = button.Entry;
                            HoveredButton = button;
                            _hoveredMoveButton = button;

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
                                "DODGE" => "Dodge",
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
                                    simpleTooltip = (move.MoveID == "Dodge" || move.MoveID == "Stall");
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
                case MenuState.AnimatingHandDiscard:
                    // Input is blocked during this animation.
                    // We only need to check for the animation's completion.
                    if (_selectedMoveButton != null && !_selectedMoveButton.IsAnimatingDiscard)
                    {
                        // Animation is complete, execute the deferred action
                        if (_deferredMove != null)
                        {
                            // Store locally before clearing, as SelectMove might trigger events that depend on a clean state.
                            var move = _deferredMove;
                            var entry = _deferredSpellbookEntry;

                            _deferredMove = null;
                            _deferredSpellbookEntry = null;
                            _selectedMoveButton = null;

                            SelectMove(move, entry);

                            if (_currentState == MenuState.AnimatingHandDiscard)
                            {
                                SetState(MenuState.Moves);
                            }
                        }
                    }
                    break;
            }

            _previousMouseState = currentMouseState;
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            InitializeButtons();
            if (!_isVisible) return;

            switch (_currentState)
            {
                case MenuState.Main:
                    {
                        const int buttonWidth = 96;
                        const int buttonHeight = 43;
                        const int buttonSpacing = 5;
                        const int dividerY = 123;

                        int totalWidth = (buttonWidth * _actionButtons.Count) + (buttonSpacing * (_actionButtons.Count - 1));
                        int startX = (Global.VIRTUAL_WIDTH - totalWidth) / 2;

                        // Center the buttons vertically in the space below the divider
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
                case MenuState.AnimatingHandDiscard:
                    {
                        DrawMovesMenu(spriteBatch, font, gameTime, transform);
                        break;
                    }
                case MenuState.Targeting:
                    {
                        const int backButtonPadding = 8;
                        const int backButtonHeight = 5;
                        const int backButtonTopMargin = 1;
                        const int dividerY = 123;
                        const int horizontalPadding = 10;
                        const int verticalPadding = 2;
                        int availableWidth = Global.VIRTUAL_WIDTH - (horizontalPadding * 2);
                        int availableHeight = Global.VIRTUAL_HEIGHT - dividerY - (verticalPadding * 2);
                        int gridAreaHeight = availableHeight - backButtonHeight - backButtonTopMargin;
                        int gridStartY = dividerY + verticalPadding + 4;

                        string text = "CHOOSE A TARGET";
                        Vector2 textSize = font.MeasureString(text);

                        // Figure 8 animation
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
                            horizontalPadding + (availableWidth - backButtonWidth) / 2,
                            gridStartY + gridAreaHeight + backButtonTopMargin - 5,
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

            // --- Layout Constants from ItemMenu ---
            const int boxWidth = 294;
            const int boxHeight = 47;
            const int boxY = 117;
            int boxX = (Global.VIRTUAL_WIDTH - boxWidth) / 2;
            var tooltipBounds = new Rectangle(boxX, boxY, boxWidth, boxHeight);

            // Draw the border
            var borderColor = _global.Palette_White;
            spriteBatch.DrawSnapped(pixel, new Rectangle(tooltipBounds.Left, tooltipBounds.Top, tooltipBounds.Width, 1), borderColor);
            spriteBatch.DrawSnapped(pixel, new Rectangle(tooltipBounds.Left, tooltipBounds.Bottom - 1, tooltipBounds.Width, 1), borderColor);
            spriteBatch.DrawSnapped(pixel, new Rectangle(tooltipBounds.Left, tooltipBounds.Top, 1, tooltipBounds.Height), borderColor);
            spriteBatch.DrawSnapped(pixel, new Rectangle(tooltipBounds.Right - 1, tooltipBounds.Top, 1, tooltipBounds.Height), borderColor);

            if (_tooltipMove != null)
            {
                // 1. Draw Move Name
                var moveName = _tooltipMove.MoveName.ToUpper();
                var nameSize = font.MeasureString(moveName);
                var namePos = new Vector2(
                    tooltipBounds.Center.X - nameSize.Width / 2,
                    tooltipBounds.Y + 8
                );
                spriteBatch.DrawStringSnapped(font, moveName, namePos, _global.Palette_BrightWhite);

                // 2. Draw Description (wrapped)
                if (!string.IsNullOrEmpty(_tooltipMove.Description))
                {
                    float availableWidth = tooltipBounds.Width - 20; // Padding
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

            // Draw the back button
            int backButtonY = tooltipBounds.Bottom + 7;
            var backSize = (_backButton.Font ?? font).MeasureString(_backButton.Text);
            int backWidth = (int)backSize.Width + 16;
            _backButton.Bounds = new Rectangle(
                (Global.VIRTUAL_WIDTH - backWidth) / 2,
                backButtonY,
                backWidth,
                5
            );
            _backButton.Draw(spriteBatch, font, gameTime, transform);
        }

        private void DrawComplexTooltip(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            // Define the area for the tooltip content
            const int dividerY = 114;
            const int moveButtonWidth = 157;
            const int columns = 2;
            const int columnSpacing = 0;

            int totalGridWidth = (moveButtonWidth * columns) + columnSpacing;
            const int gridHeight = 40;
            int gridStartX = (Global.VIRTUAL_WIDTH - totalGridWidth) / 2;
            int gridStartY = dividerY + 2 + 12;

            // Draw the background sprite for the tooltip area
            var spriteManager = ServiceLocator.Get<SpriteManager>();
            var tooltipBg = spriteManager.ActionTooltipBackgroundSprite;
            var tooltipBgRect = new Rectangle(gridStartX, gridStartY, totalGridWidth, gridHeight);
            spriteBatch.DrawSnapped(tooltipBg, tooltipBgRect, Color.White);

            if (_tooltipMove != null)
            {
                DrawMoveInfoPanelContent(spriteBatch, _tooltipMove, tooltipBgRect, font, secondaryFont, transform, true);
            }

            // Draw the back button
            const int backButtonTopMargin = 0; // Vertical spacing from the tooltip panel.
            int backButtonY = gridStartY + gridHeight + backButtonTopMargin + 4;
            var backSize = (_backButton.Font ?? font).MeasureString(_backButton.Text);
            int backWidth = (int)backSize.Width + 16;
            _backButton.Bounds = new Rectangle(
                (Global.VIRTUAL_WIDTH - backWidth) / 2,
                backButtonY,
                backWidth,
                5
            );
            _backButton.Draw(spriteBatch, font, gameTime, transform);
        }

        private void DrawMovesMenu(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            var pixel = ServiceLocator.Get<Texture2D>();

            // --- NEW LAYOUT CONSTANTS ---

            // Main Moves (stack)
            const int moveButtonWidth = 128;
            const int moveButtonHeight = 9;
            const int moveRows = 4;
            const int moveRowSpacing = 0;
            const int moveBlockHeight = moveRows * (moveButtonHeight + moveRowSpacing) - moveRowSpacing;

            // Secondary Actions (vertical stack)
            const int secButtonWidth = 60;
            const int secButtonHeight = 13;
            const int secRowSpacing = 0;
            const int secRows = 3;
            const int secBlockHeight = secRows * (secButtonHeight + secRowSpacing) - secRowSpacing;

            // Border
            const int borderWidth = 120;
            const int borderHeight = 37;
            const int layoutGap = 2;

            // Combined Layout Calculation
            const int totalCombinedWidth = moveButtonWidth + layoutGap + borderWidth + layoutGap + secButtonWidth;
            int layoutStartX = (Global.VIRTUAL_WIDTH - totalCombinedWidth) / 2;

            // Positioning
            int moveGridStartY = 128;

            // 1. Main Moves Position
            int moveGridStartX = layoutStartX;

            // 2. Border Position
            int borderX = moveGridStartX + moveButtonWidth + layoutGap;
            int borderY = moveGridStartY + (moveBlockHeight / 2) - (borderHeight / 2);

            // 3. Secondary Actions Position
            int secGridStartX = borderX + borderWidth + layoutGap;
            int secGridStartY = moveGridStartY + (moveBlockHeight / 2) - (secBlockHeight / 2);


            // --- Draw the border ---
            var borderRect = new Rectangle(borderX, borderY, borderWidth, borderHeight);
            var borderColor = _global.Palette_DarkGray;
            spriteBatch.DrawSnapped(pixel, new Rectangle(borderRect.Left, borderRect.Top, borderRect.Width, 1), borderColor); // Top
            spriteBatch.DrawSnapped(pixel, new Rectangle(borderRect.Left, borderRect.Bottom - 1, borderRect.Width, 1), borderColor); // Bottom
            spriteBatch.DrawSnapped(pixel, new Rectangle(borderRect.Left, borderRect.Top, 1, borderRect.Height), borderColor); // Left
            spriteBatch.DrawSnapped(pixel, new Rectangle(borderRect.Right - 1, borderRect.Top, 1, borderRect.Height), borderColor); // Right

            // --- Draw Info Panel Content ---
            DrawMoveInfoPanelContent(spriteBatch, HoveredMove, borderRect, font, secondaryFont, transform, false);


            // --- Draw Main Move Buttons ---
            for (int i = 0; i < _moveButtons.Count; i++)
            {
                var button = _moveButtons[i];

                if (_currentState == MenuState.AnimatingHandDiscard && button != _selectedMoveButton)
                {
                    continue; // Skip drawing non-selected buttons
                }

                int row = i; // Single column

                var buttonBounds = new Rectangle(
                    moveGridStartX,
                    moveGridStartY + row * (moveButtonHeight + moveRowSpacing),
                    moveButtonWidth,
                    moveButtonHeight
                );
                button.Bounds = buttonBounds;

                if (button == _hoveredMoveButton && button.IsEnabled)
                {
                    spriteBatch.DrawSnapped(pixel, button.Bounds, _global.Palette_DarkGray);
                }

                if (_buttonAnimationStates.TryGetValue(button, out var state))
                {
                    if (state.phase == ButtonAnimationPhase.AnimatingNewText)
                    {
                        float alpha = 0f;
                        float scale = 0f;

                        if (state.timer < NEW_TEXT_FADE_IN_DURATION)
                        {
                            float fadeInProgress = state.timer / NEW_TEXT_FADE_IN_DURATION;
                            scale = Easing.EaseOutBack(fadeInProgress);
                            alpha = fadeInProgress;
                        }
                        else if (state.timer < NEW_TEXT_FADE_IN_DURATION + NEW_TEXT_HOLD_DURATION)
                        {
                            alpha = 1f;
                            scale = 1f;
                        }
                        else
                        {
                            float fadeOutProgress = (state.timer - (NEW_TEXT_FADE_IN_DURATION + NEW_TEXT_HOLD_DURATION)) / NEW_TEXT_FADE_OUT_DURATION;
                            float easedProgress = Easing.EaseInQuint(fadeOutProgress);
                            alpha = 1f - easedProgress;
                            scale = 1f - easedProgress;
                        }

                        string newText = "NEW!";
                        var newTextSize = secondaryFont.MeasureString(newText);
                        var newTextOrigin = new Vector2(newTextSize.Width / 2f, newTextSize.Height / 2f);
                        var newTextPos = button.Bounds.Center.ToVector2();

                        spriteBatch.DrawStringOutlinedSnapped(secondaryFont, newText, newTextPos, _global.Palette_Yellow * alpha, Color.Black * alpha, 0f, newTextOrigin, scale, SpriteEffects.None, 0f);
                    }
                    else if (state.phase == ButtonAnimationPhase.AnimatingButton || state.phase == ButtonAnimationPhase.Idle)
                    {
                        button.Draw(spriteBatch, font, gameTime, transform);
                    }
                }
            }

            if (_currentState == MenuState.AnimatingHandDiscard)
            {
                return;
            }

            // --- Draw Secondary Action Buttons (Vertical Stack) ---
            for (int i = 0; i < _secondaryActionButtons.Count; i++)
            {
                var button = _secondaryActionButtons[i];
                int yPos = secGridStartY + i * (secButtonHeight + secRowSpacing);

                button.Bounds = new Rectangle(
                    secGridStartX,
                    yPos,
                    secButtonWidth,
                    secButtonHeight
                );
                button.Draw(spriteBatch, font, gameTime, transform);
            }


            // --- Back Button ---
            int layoutBottomY = Math.Max(borderRect.Bottom, moveGridStartY + moveBlockHeight);
            int backButtonY = layoutBottomY + 7;
            var backSize = (_backButton.Font ?? font).MeasureString(_backButton.Text);
            int backWidth = (int)backSize.Width + 16;
            _backButton.Bounds = new Rectangle(
                (Global.VIRTUAL_WIDTH - backWidth) / 2,
                backButtonY,
                backWidth,
                5
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
                if (move == null) return; // Should not happen for tooltips

                // --- TOOLTIP: Draw Name and Stats on the same line ---
                var moveName = move.MoveName.ToUpper();
                var nameSize = font.MeasureString(moveName);
                var namePos = new Vector2(bounds.X + horizontalPadding, currentY);

                string powerText = move.Power > 0 ? $"POW: {move.Power}" : "POW: ---";
                string accuracyText = move.Accuracy >= 0 ? $"ACC: {move.Accuracy}%" : "ACC: ---";
                var powerSize = secondaryFont.MeasureString(powerText);
                var accSize = secondaryFont.MeasureString(accuracyText);
                string moveTypeText = move.MoveType.ToString().ToUpper();
                string separator = " / ";
                var moveTypeSize = secondaryFont.MeasureString(moveTypeText);
                var separatorSize = secondaryFont.MeasureString(separator);

                float totalStatsWidth = powerSize.Width + separatorSize.Width + accSize.Width + separatorSize.Width + moveTypeSize.Width;
                float statsY = currentY + (nameSize.Height - powerSize.Height) / 2;
                float statsStartX = bounds.Right - horizontalPadding - totalStatsWidth;

                // Draw Stats Line (RIGHT ALIGNED, with colored separators)
                float currentX = statsStartX;
                spriteBatch.DrawStringSnapped(secondaryFont, powerText, new Vector2(currentX, statsY), _global.Palette_White);
                currentX += powerSize.Width;
                spriteBatch.DrawStringSnapped(secondaryFont, separator, new Vector2(currentX, statsY), _global.Palette_DarkGray);
                currentX += separatorSize.Width;
                spriteBatch.DrawStringSnapped(secondaryFont, accuracyText, new Vector2(currentX, statsY), _global.Palette_White);
                currentX += accSize.Width;
                spriteBatch.DrawStringSnapped(secondaryFont, separator, new Vector2(currentX, statsY), _global.Palette_DarkGray);
                currentX += separatorSize.Width;

                Color moveTypeColor = move.MoveType switch
                {
                    MoveType.Spell => _global.Palette_LightBlue,
                    MoveType.Action => _global.Palette_Orange,
                    _ => _global.Palette_White
                };
                spriteBatch.DrawStringSnapped(secondaryFont, moveTypeText, new Vector2(currentX, statsY), moveTypeColor);

                // Draw Name (with scrolling)
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

                // --- SHARED (Tooltip only): Underline and Description ---
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
                // --- HOVER PANEL: Draw stats and types, or placeholders ---
                float statsY = currentY;
                Color valueColor = _global.Palette_White;
                Color labelColor = _global.Palette_DarkGray;
                const int statGap = 5;

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

                // Draw Top Row (POW & ACC)
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

                // Draw Middle Row (MANA)
                var manaLabelPos = new Vector2(bounds.X + horizontalPadding, currentY);
                spriteBatch.DrawStringSnapped(secondaryFont, manaLabel, manaLabelPos, labelColor);
                var manaValueSize = secondaryFont.MeasureString(manaValue);
                var manaValuePos = new Vector2(bounds.Center.X - 3 - manaValueSize.Width, currentY);
                if (!manaValue.Contains("%"))
                {
                    manaValuePos.X -= 6;
                }
                spriteBatch.DrawStringSnapped(secondaryFont, manaValue, manaValuePos, valueColor);

                // --- NEW TARGETING TEXT ---
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

                // Draw Bottom Row (IMPACT & TYPE)
                var impactSize = secondaryFont.MeasureString(impactValue);
                var moveTypeSize = secondaryFont.MeasureString(moveTypeValue);
                const int typeGap = 5;
                float typeY = bounds.Bottom - verticalPadding - secondaryFont.LineHeight;
                float gapCenter = bounds.Center.X + 5;

                // Now draw the target text vertically centered between MANA and the bottom row
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
#nullable restore
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
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
        public event Action<MoveData, BattleCombatant> OnMoveSelected;
        public event Action OnItemMenuRequested;
        public event Action OnMovesMenuOpened;
        public event Action OnMainMenuOpened;
        public event Action OnFleeRequested;
        private bool _isVisible;
        private BattleCombatant _player;
        private List<BattleCombatant> _allCombatants;
        private List<BattleCombatant> _allTargets;
        private List<Button> _actionButtons = new List<Button>();
        private List<MoveButton> _moveButtons = new List<MoveButton>();
        private List<Button> _secondaryActionButtons = new List<Button>();
        private Button _backButton;
        private readonly Global _global;

        public enum MenuState { Main, Moves, Targeting, Tooltip }
        private MenuState _currentState;
        public MenuState CurrentMenuState => _currentState;
        private MoveData _selectedMove;
        public MoveData SelectedMove => _selectedMove;
        private MoveData _tooltipMove;
        public MoveData HoveredMove { get; private set; }

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

        // Scrolling Tuning
        private const float SCROLL_SPEED = 25f;
        private const float SCROLL_PAUSE_DURATION = 1.5f;
        private const int EXTRA_SCROLL_SPACES = 1;
        private static readonly RasterizerState _clipRasterizerState = new RasterizerState { ScissorTestEnable = true };

        // State for animation
        private MoveData[] _previousHandState = new MoveData[4];
        private Queue<MoveButton> _buttonsToAnimate = new Queue<MoveButton>();
        private float _animationDelayTimer = 0f;
        private const float SEQUENTIAL_ANIMATION_DELAY = 0.05f;

        private Queue<ImageButton> _actionButtonsToAnimate = new Queue<ImageButton>();
        private float _actionButtonAnimationDelayTimer = 0f;
        private const float ACTION_BUTTON_SEQUENTIAL_ANIMATION_DELAY = 0.05f;

        public ActionMenu()
        {
            _global = ServiceLocator.Get<Global>();
            _backButton = new Button(Rectangle.Empty, "BACK");
            _backButton.OnClick += () =>
            {
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
            _actionButtons.Add(new ImageButton(Rectangle.Empty, actionSheet, rects[0], rects[1], rects[2], function: "Act", startVisible: false, debugColor: new Color(100, 0, 0, 150)));
            _actionButtons.Add(new ImageButton(Rectangle.Empty, actionSheet, rects[3], rects[4], rects[5], function: "Item", startVisible: false, debugColor: new Color(0, 100, 0, 150)));
            _actionButtons.Add(new ImageButton(Rectangle.Empty, actionSheet, rects[6], rects[7], rects[8], function: "Flee", startVisible: false, debugColor: new Color(0, 0, 100, 150)));

            _actionButtons[0].OnClick += () => SetState(MenuState.Moves);
            _actionButtons[1].OnClick += () => OnItemMenuRequested?.Invoke();
            _actionButtons[2].OnClick += () => OnFleeRequested?.Invoke();

            // Secondary Action Buttons
            var strikeButton = new TextOverImageButton(Rectangle.Empty, "STRIKE", secondaryButtonBg, font: secondaryFont, iconTexture: actionIconsSheet, iconSourceRect: actionIconRects[0]);
            strikeButton.OnClick += () =>
            {
                if (_player != null && !string.IsNullOrEmpty(_player.DefaultStrikeMoveID) && BattleDataCache.Moves.TryGetValue(_player.DefaultStrikeMoveID, out var strikeMove))
                {
                    SelectMove(strikeMove);
                }
            };
            _secondaryActionButtons.Add(strikeButton);

            var dodgeButton = new TextOverImageButton(Rectangle.Empty, "DODGE", secondaryButtonBg, font: secondaryFont, iconTexture: actionIconsSheet, iconSourceRect: actionIconRects[1]);
            dodgeButton.OnClick += () =>
            {
                if (BattleDataCache.Moves.TryGetValue("Dodge", out var dodgeMove))
                {
                    SelectMove(dodgeMove);
                }
            };
            _secondaryActionButtons.Add(dodgeButton);

            var stallButton = new TextOverImageButton(Rectangle.Empty, "STALL", secondaryButtonBg, font: secondaryFont, iconTexture: actionIconsSheet, iconSourceRect: actionIconRects[2]);
            stallButton.OnClick += () =>
            {
                if (BattleDataCache.Moves.TryGetValue("Stall", out var stallMove))
                {
                    SelectMove(stallMove);
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
            Array.Clear(_previousHandState, 0, _previousHandState.Length);
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
            _currentState = newState;
            HoveredMove = null;

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
                PopulateAndAnimateMoveButtons();
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

        private void PopulateAndAnimateMoveButtons()
        {
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            var spriteManager = ServiceLocator.Get<SpriteManager>();

            _moveButtons.Clear();
            _buttonsToAnimate.Clear();
            _animationDelayTimer = 0f;

            var currentHand = _player.AvailableMoves.Take(4).ToList();
            if (!currentHand.Any())
            {
                // If hand is empty, we don't auto-stall. The player can still use secondary actions.
            }

            var newHand = new MoveData[4];
            for (int i = 0; i < Math.Min(currentHand.Count, 4); i++)
            {
                newHand[i] = currentHand[i];
            }

            for (int i = 0; i < 4; i++)
            {
                var move = newHand[i];
                if (move == null) continue;

                bool isNew = _previousHandState[i] == null || _previousHandState[i].MoveID != move.MoveID;
                var moveButton = CreateMoveButton(move, secondaryFont, spriteManager.ActionButtonTemplateSprite, !isNew);
                _moveButtons.Add(moveButton);

                if (isNew)
                {
                    _buttonsToAnimate.Enqueue(moveButton);
                }
            }

            Array.Copy(newHand, _previousHandState, newHand.Length);
        }

        private MoveButton CreateMoveButton(MoveData move, BitmapFont font, Texture2D background, bool startVisible)
        {
            var spriteManager = ServiceLocator.Get<SpriteManager>();
            int elementId = move.OffensiveElementIDs.FirstOrDefault();
            Rectangle? sourceRect = null;
            if (spriteManager.ElementIconSourceRects.TryGetValue(elementId, out var rect))
            {
                sourceRect = rect;
            }

            var moveButton = new MoveButton(move, font, background, spriteManager.ElementIconsSpriteSheet, sourceRect, startVisible);
            moveButton.OnClick += () => SelectMove(move);
            moveButton.OnRightClick += () =>
            {
                _tooltipMove = move;
                SetState(MenuState.Tooltip);
            };
            return moveButton;
        }

        private void SelectMove(MoveData move)
        {
            _selectedMove = move;

            // Mark the move as used if it's in the hand
            for (int i = 0; i < _previousHandState.Length; i++)
            {
                if (_previousHandState[i] == move)
                {
                    _previousHandState[i] = null;
                    break;
                }
            }

            switch (move.Target)
            {
                case TargetType.Self:
                    OnMoveSelected?.Invoke(move, _player);
                    break;

                case TargetType.None:
                case TargetType.Every:
                case TargetType.EveryAll:
                    OnMoveSelected?.Invoke(move, null);
                    break;

                case TargetType.Single:
                    var enemies = _allTargets.Where(c => !c.IsDefeated).ToList();
                    if (enemies.Count == 1)
                    {
                        OnMoveSelected?.Invoke(move, enemies[0]);
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
                        OnMoveSelected?.Invoke(move, allValidTargets[0]);
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

        public void Update(MouseState currentMouseState, GameTime gameTime)
        {
            InitializeButtons();
            if (!_isVisible) return;

            if (_actionButtonsToAnimate.Any())
            {
                _actionButtonAnimationDelayTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (_actionButtonAnimationDelayTimer >= ACTION_BUTTON_SEQUENTIAL_ANIMATION_DELAY)
                {
                    _actionButtonAnimationDelayTimer = 0f;
                    var buttonToAnimate = _actionButtonsToAnimate.Dequeue();
                    buttonToAnimate.TriggerAppearAnimation();
                }
            }

            if (_buttonsToAnimate.Any())
            {
                _animationDelayTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (_animationDelayTimer >= SEQUENTIAL_ANIMATION_DELAY)
                {
                    _animationDelayTimer = 0f;
                    var buttonToAnimate = _buttonsToAnimate.Dequeue();
                    buttonToAnimate.TriggerAppearAnimation();
                }
            }

            switch (_currentState)
            {
                case MenuState.Main:
                    foreach (var button in _actionButtons) button.Update(currentMouseState);
                    break;
                case MenuState.Moves:
                    HoveredMove = null;
                    foreach (var button in _moveButtons)
                    {
                        button.Update(currentMouseState);
                        if (button.IsHovered)
                        {
                            HoveredMove = button.Move;
                        }
                    }
                    foreach (var button in _secondaryActionButtons)
                    {
                        button.Update(currentMouseState);
                    }
                    _backButton.Update(currentMouseState);
                    break;
                case MenuState.Targeting:
                    _targetingTextAnimTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                    _backButton.Update(currentMouseState);
                    break;
                case MenuState.Tooltip:
                    UpdateTooltipScrolling(gameTime);
                    bool leftClick = currentMouseState.LeftButton == ButtonState.Released && _previousMouseState.LeftButton == ButtonState.Pressed;
                    bool rightClick = currentMouseState.RightButton == ButtonState.Released && _previousMouseState.RightButton == ButtonState.Pressed;
                    if (leftClick || rightClick)
                    {
                        if (UIInputManager.CanProcessMouseClick())
                        {
                            SetState(MenuState.Moves);
                            UIInputManager.ConsumeMouseClick();
                        }
                    }
                    _backButton.Update(currentMouseState);
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
                        const int horizontalPadding = 10;
                        const int buttonSpacing = 5;
                        const int dividerY = 120;

                        int availableWidth = Global.VIRTUAL_WIDTH - (horizontalPadding * 2);
                        int availableHeight = Global.VIRTUAL_HEIGHT - dividerY;

                        int buttonWidth = (availableWidth - (buttonSpacing * (_actionButtons.Count - 1))) / _actionButtons.Count;
                        int buttonHeight = availableHeight;

                        int startY = dividerY - 9;
                        int startX = horizontalPadding;

                        int currentX = startX;
                        foreach (var button in _actionButtons)
                        {
                            button.Bounds = new Rectangle(currentX, startY, buttonWidth, buttonHeight);
                            button.Draw(spriteBatch, font, gameTime, transform);
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
                        const int backButtonHeight = 13;
                        const int backButtonTopMargin = 1;
                        const int dividerY = 120;
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
                            gridStartY + (gridAreaHeight - textSize.Y) / 2
                        ) + animOffset;
                        spriteBatch.DrawStringSnapped(font, text, textPos, Color.Red);

                        int backButtonWidth = (int)(_backButton.Font ?? font).MeasureString(_backButton.Text).Width + backButtonPadding * 2;
                        _backButton.Bounds = new Rectangle(
                            horizontalPadding + (availableWidth - backButtonWidth) / 2,
                            gridStartY + gridAreaHeight + backButtonTopMargin,
                            backButtonWidth,
                            backButtonHeight
                        );
                        _backButton.Draw(spriteBatch, font, gameTime, transform);
                        break;
                    }
                case MenuState.Tooltip:
                    {
                        var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
                        // Define the area for the tooltip content, matching the moves menu area.
                        const int dividerY = 114;
                        const int moveButtonWidth = 157;
                        const int moveButtonHeight = 17;
                        const int columns = 2;
                        const int rows = 3;
                        const int columnSpacing = 0;
                        const int rowSpacing = 0;

                        int totalGridWidth = (moveButtonWidth * columns) + columnSpacing;
                        int gridHeight = (moveButtonHeight * rows) + (rowSpacing * (rows - 1));
                        int gridStartX = (Global.VIRTUAL_WIDTH - totalGridWidth) / 2;
                        int gridStartY = dividerY - 4;

                        // Draw the background sprite for the tooltip area
                        var spriteManager = ServiceLocator.Get<SpriteManager>();
                        var tooltipBg = spriteManager.ActionTooltipBackgroundSprite;
                        var tooltipBgRect = new Rectangle(gridStartX, gridStartY, totalGridWidth, gridHeight);
                        spriteBatch.DrawSnapped(tooltipBg, tooltipBgRect, Color.White);

                        // Draw the move name and stats
                        if (_tooltipMove != null)
                        {
                            const int horizontalPadding = 8;
                            const int verticalPadding = 6;
                            float currentY = tooltipBgRect.Y + verticalPadding;

                            // 1. Prepare Move Name and Stats
                            var moveName = _tooltipMove.MoveName.ToUpper();
                            var nameSize = font.MeasureString(moveName);
                            var namePos = new Vector2(tooltipBgRect.X + horizontalPadding, currentY);

                            string powerText = _tooltipMove.Power > 0 ? $"POW: {_tooltipMove.Power}" : "POW: ---";
                            string accuracyText = _tooltipMove.Accuracy >= 0 ? $"ACC: {_tooltipMove.Accuracy}%" : "ACC: ---";
                            string moveTypeText = _tooltipMove.MoveType.ToString().ToUpper();
                            string separator = " / ";

                            var powerSize = secondaryFont.MeasureString(powerText);
                            var accSize = secondaryFont.MeasureString(accuracyText);
                            var moveTypeSize = secondaryFont.MeasureString(moveTypeText);
                            var separatorSize = secondaryFont.MeasureString(separator);

                            float totalStatsWidth = powerSize.Width + separatorSize.Width + accSize.Width + separatorSize.Width + moveTypeSize.Width;
                            float statsY = currentY + (nameSize.Height - powerSize.Height) / 2;
                            float statsStartX = tooltipBgRect.Right - horizontalPadding - totalStatsWidth;

                            // 2. Draw Stats Line (RIGHT ALIGNED, with colored separators)
                            float currentX = statsStartX;
                            spriteBatch.DrawStringSnapped(secondaryFont, powerText, new Vector2(currentX, statsY), _global.Palette_White);
                            currentX += powerSize.Width;
                            spriteBatch.DrawStringSnapped(secondaryFont, separator, new Vector2(currentX, statsY), _global.Palette_DarkGray);
                            currentX += separatorSize.Width;
                            spriteBatch.DrawStringSnapped(secondaryFont, accuracyText, new Vector2(currentX, statsY), _global.Palette_White);
                            currentX += accSize.Width;
                            spriteBatch.DrawStringSnapped(secondaryFont, separator, new Vector2(currentX, statsY), _global.Palette_DarkGray);
                            currentX += separatorSize.Width;
                            spriteBatch.DrawStringSnapped(secondaryFont, moveTypeText, new Vector2(currentX, statsY), _global.Palette_White);

                            // 3. Draw Move Name (static or scrolling)
                            const int titleCharLimit = 16;
                            bool needsScrolling = moveName.Length > titleCharLimit;
                            float textAvailableWidth = statsStartX - namePos.X - 4;

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
                                spriteBatch.DrawStringSnapped(font, moveName, scrollingTextPosition, _global.Palette_BrightWhite);

                                spriteBatch.End();
                                spriteBatch.GraphicsDevice.ScissorRectangle = originalScissorRect;
                                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, originalRasterizerState, null, transform);
                            }
                            else
                            {
                                spriteBatch.DrawStringSnapped(font, moveName, namePos, _global.Palette_BrightWhite);
                            }

                            // 4. Update currentY and draw Underline
                            currentY += nameSize.Height;
                            var underlineStart = new Vector2(namePos.X - 1, currentY + 1);
                            var underlineEnd = new Vector2(namePos.X + nameSize.Width + 1, currentY + 1);
                            spriteBatch.DrawLineSnapped(underlineStart, underlineEnd, _global.Palette_BrightWhite);
                            currentY += 5;

                            // 5. Draw Description (word-wrapped and capitalized)
                            if (!string.IsNullOrEmpty(_tooltipMove.Description))
                            {
                                float availableWidth = tooltipBgRect.Width - (horizontalPadding * 2);
                                var wrappedLines = WrapText(_tooltipMove.Description.ToUpper(), availableWidth, secondaryFont);
                                foreach (var line in wrappedLines)
                                {
                                    var descPos = new Vector2(tooltipBgRect.X + horizontalPadding, currentY);
                                    spriteBatch.DrawStringSnapped(secondaryFont, line, descPos, _global.Palette_White);
                                    currentY += secondaryFont.LineHeight;
                                }
                            }
                        }

                        // Draw the back button
                        const int backButtonTopMargin = 7;
                        int backButtonY = gridStartY + gridHeight + backButtonTopMargin;
                        var backSize = (_backButton.Font ?? font).MeasureString(_backButton.Text);
                        int backWidth = (int)backSize.Width + 16;
                        _backButton.Bounds = new Rectangle(
                            (Global.VIRTUAL_WIDTH - backWidth) / 2,
                            backButtonY,
                            backWidth,
                            13
                        );
                        _backButton.Draw(spriteBatch, font, gameTime, transform);
                        break;
                    }
            }
        }

        private void DrawMovesMenu(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            // --- Main Moves (2x2 Grid) ---
            const int moveButtonWidth = 157;
            const int moveButtonHeight = 17;
            const int moveColumns = 2;
            const int moveRows = 2;
            const int moveColSpacing = 0;
            const int moveRowSpacing = 0;

            int totalMoveGridWidth = (moveButtonWidth * moveColumns) + moveColSpacing;
            int moveGridStartX = (Global.VIRTUAL_WIDTH - totalMoveGridWidth) / 2;
            int moveGridStartY = 114 - 4;

            for (int i = 0; i < _moveButtons.Count; i++)
            {
                var button = _moveButtons[i];
                int row = i / moveColumns;
                int col = i % moveColumns;

                button.Bounds = new Rectangle(
                    moveGridStartX + col * (moveButtonWidth + moveColSpacing),
                    moveGridStartY + row * (moveButtonHeight + moveRowSpacing),
                    moveButtonWidth,
                    moveButtonHeight
                );
                button.Draw(spriteBatch, font, gameTime, transform);
            }

            // --- Secondary Actions (1x3 Row) ---
            const int secButtonWidth = 104;
            const int secButtonHeight = 17;
            const int secButtonSpacing = 1;
            int secRowY = moveGridStartY + (moveButtonHeight * moveRows) + (moveRowSpacing * (moveRows - 1));

            int totalSecGridWidth = (secButtonWidth * _secondaryActionButtons.Count) + (secButtonSpacing * (_secondaryActionButtons.Count - 1));
            int secGridStartX = (Global.VIRTUAL_WIDTH - totalSecGridWidth) / 2;

            for (int i = 0; i < _secondaryActionButtons.Count; i++)
            {
                var button = _secondaryActionButtons[i];
                int xPos = secGridStartX + i * (secButtonWidth + secButtonSpacing);

                if (i == 0) // Strike button
                {
                    xPos += 1;
                }
                else if (i == 2) // Stall button
                {
                    xPos -= 1;
                }

                button.Bounds = new Rectangle(
                    xPos,
                    secRowY,
                    secButtonWidth,
                    secButtonHeight
                );
                button.Draw(spriteBatch, font, gameTime, transform);
            }

            // --- Back Button ---
            int backButtonY = secRowY + secButtonHeight + 7;
            var backSize = (_backButton.Font ?? font).MeasureString(_backButton.Text);
            int backWidth = (int)backSize.Width + 16;
            _backButton.Bounds = new Rectangle(
                (Global.VIRTUAL_WIDTH - backWidth) / 2,
                backButtonY,
                backWidth,
                13
            );
            _backButton.Draw(spriteBatch, font, gameTime, transform);
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
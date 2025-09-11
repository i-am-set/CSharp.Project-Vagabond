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

namespace ProjectVagabond.Battle.UI
{
    public class ActionMenu
    {
        public event Action<MoveData, BattleCombatant> OnMoveSelected;
        public event Action<MoveData> OnTargetingInitiated;
        public event Action OnTargetingCancelled;

        private bool _isVisible;
        private BattleCombatant _player;
        private List<BattleCombatant> _allTargets;
        private List<Button> _actionButtons = new List<Button>();
        private List<MoveButton> _moveButtons = new List<MoveButton>();
        private Button _backButton;
        private Button _filterButton;
        private readonly Global _global;

        private enum MenuState { Main, Moves, Targeting }
        private MenuState _currentState;
        private MoveData _selectedMove;
        private float _targetingTextAnimTimer = 0f;
        private bool _buttonsInitialized = false;

        // New fields for scrolling and layout
        private int _scrollIndex = 0; // The index of the top-most visible row
        private int _totalRows = 0;
        private int _maxVisibleRows = 0;
        private Rectangle _moveListBounds;
        private MouseState _previousMouseState;

        public ActionMenu()
        {
            _global = ServiceLocator.Get<Global>();
            _backButton = new Button(Rectangle.Empty, "BACK");
            _backButton.OnClick += () => {
                if (_currentState == MenuState.Targeting)
                {
                    OnTargetingCancelled?.Invoke();
                    SetState(MenuState.Moves);
                }
                else if (_currentState == MenuState.Moves)
                {
                    SetState(MenuState.Main);
                }
            };

            _filterButton = new Button(Rectangle.Empty, "FILTER") { IsEnabled = false };

            _previousMouseState = Mouse.GetState();
        }

        private void InitializeButtons()
        {
            if (_buttonsInitialized) return;

            var spriteManager = ServiceLocator.Get<SpriteManager>();
            var actionSheet = spriteManager.ActionButtonsSpriteSheet;
            var rects = spriteManager.ActionButtonSourceRects;
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;

            _actionButtons.Add(new ImageButton(Rectangle.Empty, actionSheet, rects[0], rects[1], rects[2], function: "Act", debugColor: new Color(100, 0, 0, 150)));
            _actionButtons.Add(new ImageButton(Rectangle.Empty, actionSheet, rects[3], rects[4], rects[5], function: "Item", debugColor: new Color(0, 100, 0, 150)) { IsEnabled = false });
            _actionButtons.Add(new ImageButton(Rectangle.Empty, actionSheet, rects[6], rects[7], rects[8], function: "Flee", debugColor: new Color(0, 0, 100, 150)) { IsEnabled = false });

            _actionButtons[0].OnClick += () => SetState(MenuState.Moves);

            _backButton.Font = secondaryFont;
            _filterButton.Font = secondaryFont;

            _buttonsInitialized = true;
        }

        public void GoBack()
        {
            if (_isVisible && (_currentState == MenuState.Moves || _currentState == MenuState.Targeting))
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
            _backButton.ResetAnimationState();
            _filterButton.ResetAnimationState();
        }

        public void Show(BattleCombatant player, List<BattleCombatant> allCombatants)
        {
            _isVisible = true;
            _player = player;
            _allTargets = allCombatants.Where(c => !c.IsPlayerControlled && !c.IsDefeated).ToList();
            SetState(MenuState.Main);
        }

        public void Hide()
        {
            _isVisible = false;
        }

        private void SetState(MenuState newState)
        {
            _currentState = newState;
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;

            if (_currentState == MenuState.Moves)
            {
                _moveButtons.Clear();
                // This list can be sorted later by the filter button
                var movesToDisplay = new List<MoveData>(_player.AvailableMoves);

                // Always add Stall as an available option in the list
                if (BattleDataCache.Moves.TryGetValue("Stall", out var stallMove))
                {
                    movesToDisplay.Add(stallMove);
                }
                else
                {
                    Debug.WriteLine("[ActionMenu] [FATAL] Could not find 'Stall' move in BattleDataCache.");
                }


                foreach (var move in movesToDisplay)
                {
                    var moveButton = new MoveButton(move, secondaryFont);
                    moveButton.OnClick += () => {
                        _selectedMove = move;
                        if (_allTargets.Count == 1)
                        {
                            OnMoveSelected?.Invoke(_selectedMove, _allTargets[0]);
                            Hide();
                        }
                        else
                        {
                            OnTargetingInitiated?.Invoke(_selectedMove);
                            SetState(MenuState.Targeting);
                        }
                    };
                    _moveButtons.Add(moveButton);
                }
                _scrollIndex = 0;
            }
            else if (newState == MenuState.Targeting)
            {
                _targetingTextAnimTimer = 0f;
            }
        }

        public void Update(MouseState currentMouseState, GameTime gameTime)
        {
            InitializeButtons();
            if (!_isVisible) return;

            switch (_currentState)
            {
                case MenuState.Main:
                    foreach (var button in _actionButtons) button.Update(currentMouseState);
                    break;
                case MenuState.Moves:
                    var virtualMousePos = Core.TransformMouse(currentMouseState.Position);
                    if (_moveListBounds.Contains(virtualMousePos))
                    {
                        int scrollDelta = currentMouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
                        if (scrollDelta != 0)
                        {
                            _scrollIndex -= Math.Sign(scrollDelta);
                        }
                    }

                    int maxScrollIndex = Math.Max(0, _totalRows - _maxVisibleRows);
                    _scrollIndex = Math.Clamp(_scrollIndex, 0, maxScrollIndex);

                    int startIndex = _scrollIndex * 2;
                    int endIndex = Math.Min(_moveButtons.Count, startIndex + _maxVisibleRows * 2);
                    for (int i = 0; i < _moveButtons.Count; i++)
                    {
                        var button = _moveButtons[i];
                        if (i >= startIndex && i < endIndex)
                        {
                            button.Update(currentMouseState);
                        }
                        else
                        {
                            button.IsHovered = false;
                        }
                    }

                    _backButton.Update(currentMouseState);
                    _filterButton.Update(currentMouseState);
                    break;
                case MenuState.Targeting:
                    _targetingTextAnimTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                    _backButton.Update(currentMouseState);
                    break;
            }
            _previousMouseState = currentMouseState;
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            InitializeButtons();
            if (!_isVisible) return;

            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            var spriteManager = ServiceLocator.Get<SpriteManager>();

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

                        int startX = horizontalPadding;
                        int startY = dividerY - 5;

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
                        // --- Layout Constants ---
                        const int horizontalPadding = 10;
                        const int dividerY = 114;
                        const int menuVerticalOffset = 4;

                        const int moveListHeight = 45;
                        const int bottomBarTopPadding = 3;
                        const int bottomBarHeight = 13;

                        const int moveButtonWidth = 145;
                        const int moveButtonHeight = 9;
                        const int columnSpacing = 2;
                        const int rowSpacing = 0;
                        const int columns = 2;

                        int totalGridWidth = (moveButtonWidth * columns) + columnSpacing;
                        int gridStartX = horizontalPadding + (Global.VIRTUAL_WIDTH - (horizontalPadding * 2) - totalGridWidth) / 2;

                        // --- Background Sprite ---
                        var bgSprite = spriteManager.ActionMovesBackgroundSprite;
                        var bgRect = new Rectangle(gridStartX - 1, dividerY + menuVerticalOffset - 1, 294, 47);
                        spriteBatch.DrawSnapped(bgSprite, bgRect, Color.White);

                        // --- Move List Area ---
                        int gridStartY = dividerY + menuVerticalOffset;
                        _moveListBounds = new Rectangle(gridStartX, gridStartY, totalGridWidth, moveListHeight);
                        _maxVisibleRows = moveListHeight / moveButtonHeight;

                        int startIndex = _scrollIndex * columns;
                        int visibleButtonCount = Math.Min(_moveButtons.Count - startIndex, _maxVisibleRows * columns);

                        for (int i = 0; i < visibleButtonCount; i++)
                        {
                            int moveIndex = startIndex + i;
                            var button = _moveButtons[moveIndex];

                            int row = i / columns;
                            int col = i % columns;

                            button.Bounds = new Rectangle(
                                gridStartX + col * (moveButtonWidth + columnSpacing),
                                gridStartY + row * (moveButtonHeight + rowSpacing),
                                moveButtonWidth,
                                moveButtonHeight
                            );
                            button.Draw(spriteBatch, secondaryFont, gameTime, transform);
                        }

                        _totalRows = (int)Math.Ceiling(_moveButtons.Count / (double)columns);
                        if (_totalRows > _maxVisibleRows)
                        {
                            var pixel = ServiceLocator.Get<Texture2D>();
                            int scrollbarX = _moveListBounds.Right + 2;
                            var scrollbarBgRect = new Rectangle(scrollbarX, _moveListBounds.Y, 1, _moveListBounds.Height);
                            spriteBatch.DrawSnapped(pixel, scrollbarBgRect, _global.Palette_DarkGray);

                            float handleHeight = Math.Max(5, (float)_maxVisibleRows / _totalRows * _moveListBounds.Height);
                            float handleY = _moveListBounds.Y;
                            if (_totalRows - _maxVisibleRows > 0)
                            {
                                handleY += ((float)_scrollIndex / (_totalRows - _maxVisibleRows) * (_moveListBounds.Height - handleHeight));
                            }

                            var scrollbarHandleRect = new Rectangle(scrollbarX, (int)handleY, 1, (int)handleHeight);
                            spriteBatch.DrawSnapped(pixel, scrollbarHandleRect, _global.Palette_White);
                        }

                        // --- Bottom Bar (Filter & Back) ---
                        int bottomBarY = _moveListBounds.Bottom + bottomBarTopPadding;

                        var backSize = secondaryFont.MeasureString(_backButton.Text);
                        const int backButtonPadding = 8;
                        int backWidth = (int)backSize.Width + backButtonPadding * 2;
                        _backButton.Bounds = new Rectangle(
                            _moveListBounds.Center.X - backWidth / 2,
                            bottomBarY,
                            backWidth,
                            bottomBarHeight
                        );

                        var filterSize = secondaryFont.MeasureString(_filterButton.Text);
                        const int filterButtonPadding = 4;
                        int filterWidth = (int)filterSize.Width + filterButtonPadding;
                        _filterButton.Bounds = new Rectangle(
                            _moveListBounds.Right - filterWidth,
                            bottomBarY,
                            filterWidth,
                            bottomBarHeight
                        );

                        // --- Draw All ---
                        _filterButton.Draw(spriteBatch, font, gameTime, transform);
                        _backButton.Draw(spriteBatch, font, gameTime, transform);

                        // --- DEBUG DRAWING ---
                        if (_global.ShowDebugOverlays)
                        {
                            var pixel = ServiceLocator.Get<Texture2D>();
                            spriteBatch.DrawSnapped(pixel, bgRect, new Color(255, 165, 0, 100)); // Orange for background
                            spriteBatch.DrawSnapped(pixel, _moveListBounds, new Color(0, 0, 255, 100)); // Blue for scroll area
                            for (int i = 0; i < visibleButtonCount; i++)
                            {
                                int moveIndex = startIndex + i;
                                var button = _moveButtons[moveIndex];
                                spriteBatch.DrawSnapped(pixel, button.Bounds, new Color(255, 0, 0, 100)); // Red for buttons
                            }
                        }
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
            }
        }
    }
}
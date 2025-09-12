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
        private List<IActionMenuItem> _displayItems = new List<IActionMenuItem>();
        private Button _backButton;
        private Button _sortButton;
        private readonly Global _global;
        private readonly ContextMenu _sortContextMenu;

        private enum MenuState { Main, Moves, Targeting }
        private enum SortMode { Unsorted, ByElement, ByPower }
        private enum SortDirection { Ascending, Descending }
        private MenuState _currentState;
        private SortMode _currentSortMode = SortMode.Unsorted;
        private SortDirection _currentSortDirection = SortDirection.Ascending;
        private MoveData _selectedMove;
        private float _targetingTextAnimTimer = 0f;
        private bool _buttonsInitialized = false;

        // A private marker class to signify the second half of a full-width header.
        private class HeaderContinuation : IActionMenuItem { }

        // New fields for scrolling and layout
        private int _scrollIndex = 0; // The index of the top-most visible row
        private int _totalRows = 0;
        private int _maxVisibleRows = 0;
        private Rectangle _moveListBounds;
        private MouseState _previousMouseState;

        public ActionMenu()
        {
            _global = ServiceLocator.Get<Global>();
            _sortContextMenu = new ContextMenu();

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

            _sortButton = new Button(Rectangle.Empty, "SORT");
            _sortButton.OnClick += OpenSortMenu;

            _previousMouseState = Mouse.GetState();
        }

        private void OpenSortMenu()
        {
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            string direction = _currentSortDirection == SortDirection.Ascending ? "ASCENDING" : "DESCENDING";
            string directionText = $"ORDER: {direction}";

            var items = new List<ContextMenuItem>
            {
                new ContextMenuItem
                {
                    Text = directionText,
                    OnClick = () => {
                        _currentSortDirection = _currentSortDirection == SortDirection.Ascending ? SortDirection.Descending : SortDirection.Ascending;
                        RebuildMoveList();
                        _sortContextMenu.Hide();
                    },
                    IsVisible = () => _currentSortMode != SortMode.Unsorted
                },
                new ContextMenuItem { Text = "---", IsVisible = () => _currentSortMode != SortMode.Unsorted }, // Divider
                new ContextMenuItem
                {
                    Text = "BY TYPE",
                    OnClick = () => SetSortMode(SortMode.Unsorted),
                    IsSelected = () => _currentSortMode == SortMode.Unsorted
                },
                new ContextMenuItem
                {
                    Text = "BY ELEMENT",
                    OnClick = () => SetSortMode(SortMode.ByElement),
                    IsSelected = () => _currentSortMode == SortMode.ByElement
                },
                new ContextMenuItem
                {
                    Text = "BY POWER",
                    OnClick = () => SetSortMode(SortMode.ByPower),
                    IsSelected = () => _currentSortMode == SortMode.ByPower
                }
            };

            // Pre-calculate the menu's dimensions to position it correctly.
            float menuWidth = items.Where(i => i.IsVisible()).Max(i => secondaryFont.MeasureString(i.Text).Width) + 24;
            float itemHeight = secondaryFont.LineHeight + 4;
            float menuHeight = (items.Where(i => i.IsVisible()).Count() * itemHeight) + 8;

            // Position the menu so its right edge aligns with the sort button's right edge.
            var menuPosition = new Vector2(
                _sortButton.Bounds.Right - menuWidth,
                _sortButton.Bounds.Top - menuHeight
            );

            _sortContextMenu.Show(menuPosition, items, secondaryFont);
        }

        private void SetSortMode(SortMode newMode)
        {
            _currentSortMode = newMode;
            RebuildMoveList();
            _scrollIndex = 0;
            _sortContextMenu.Hide();
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
            _sortButton.Font = secondaryFont;

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
            foreach (var item in _displayItems)
            {
                if (item is Button button)
                {
                    button.ResetAnimationState();
                }
            }
            _backButton.ResetAnimationState();
            _sortButton.ResetAnimationState();
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

            if (_currentState == MenuState.Moves)
            {
                _currentSortMode = SortMode.Unsorted;
                RebuildMoveList();
                _scrollIndex = 0;
            }
            else if (newState == MenuState.Targeting)
            {
                _targetingTextAnimTimer = 0f;
            }
        }

        private void RebuildMoveList()
        {
            _displayItems.Clear();
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;

            var movesToDisplay = new List<MoveData>(_player.AvailableMoves);
            if (BattleDataCache.Moves.TryGetValue("Stall", out var stallMove))
            {
                movesToDisplay.Add(stallMove);
            }

            switch (_currentSortMode)
            {
                case SortMode.Unsorted:
                    var attackMoves = movesToDisplay
                        .Where(m => m.ActionType == ActionType.Physical || m.ActionType == ActionType.Magical)
                        .OrderBy(m => m.MoveName)
                        .ToList();

                    var otherMoves = movesToDisplay
                        .Where(m => m.ActionType == ActionType.Other)
                        .OrderBy(m => m.MoveName)
                        .ToList();

                    if (attackMoves.Any())
                    {
                        if (_displayItems.Count % 2 != 0) _displayItems.Add(null);
                        _displayItems.Add(new ActionMenuHeader("SPELLS"));
                        _displayItems.Add(new HeaderContinuation());
                        foreach (var move in attackMoves)
                        {
                            _displayItems.Add(CreateMoveButton(move, secondaryFont));
                        }
                    }

                    if (otherMoves.Any())
                    {
                        if (_displayItems.Count % 2 != 0) _displayItems.Add(null);
                        _displayItems.Add(new ActionMenuHeader("OTHER"));
                        _displayItems.Add(new HeaderContinuation());
                        foreach (var move in otherMoves)
                        {
                            _displayItems.Add(CreateMoveButton(move, secondaryFont));
                        }
                    }
                    break;

                case SortMode.ByElement:
                    var groupedByElement = movesToDisplay
                        .GroupBy(m => m.OffensiveElementIDs.FirstOrDefault())
                        .OrderBy(g => g.Key);

                    foreach (var group in groupedByElement)
                    {
                        if (_displayItems.Count % 2 != 0)
                        {
                            _displayItems.Add(null);
                        }

                        string elementName = "Unknown";
                        if (BattleDataCache.Elements.TryGetValue(group.Key, out var elementDef))
                        {
                            elementName = elementDef.ElementName;
                        }
                        _displayItems.Add(new ActionMenuHeader(elementName));
                        _displayItems.Add(new HeaderContinuation());

                        var sortedGroup = _currentSortDirection == SortDirection.Ascending
                            ? group.OrderBy(m => m.MoveName)
                            : group.OrderByDescending(m => m.MoveName);

                        foreach (var move in sortedGroup)
                        {
                            _displayItems.Add(CreateMoveButton(move, secondaryFont));
                        }
                    }
                    break;

                case SortMode.ByPower:
                    var groupedByPower = movesToDisplay
                        .GroupBy(m => m.Power > 0 ? (m.Power - 1) / 10 : -1);

                    var sortedPowerGroups = _currentSortDirection == SortDirection.Ascending
                        ? groupedByPower.OrderBy(g => g.Key)
                        : groupedByPower.OrderByDescending(g => g.Key);

                    foreach (var group in sortedPowerGroups)
                    {
                        if (_displayItems.Count % 2 != 0)
                        {
                            _displayItems.Add(null);
                        }

                        string headerText;
                        if (group.Key == -1)
                        {
                            headerText = "OTHER";
                        }
                        else
                        {
                            int minPower = group.Key * 10 + 1;
                            int maxPower = minPower + 9;
                            headerText = $"POWER: {minPower}-{maxPower}";
                        }
                        _displayItems.Add(new ActionMenuHeader(headerText));
                        _displayItems.Add(new HeaderContinuation());

                        var sortedGroup = _currentSortDirection == SortDirection.Ascending
                            ? group.OrderBy(m => m.MoveName)
                            : group.OrderByDescending(m => m.MoveName);

                        foreach (var move in sortedGroup)
                        {
                            _displayItems.Add(CreateMoveButton(move, secondaryFont));
                        }
                    }
                    break;
            }
        }

        private MoveButton CreateMoveButton(MoveData move, BitmapFont font)
        {
            var moveButton = new MoveButton(move, font);
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
            return moveButton;
        }

        public void Update(MouseState currentMouseState, GameTime gameTime)
        {
            InitializeButtons();
            if (!_isVisible) return;

            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            var virtualMousePos = Core.TransformMouse(currentMouseState.Position);

            switch (_currentState)
            {
                case MenuState.Main:
                    foreach (var button in _actionButtons) button.Update(currentMouseState);
                    break;
                case MenuState.Moves:
                    if (_sortContextMenu.IsOpen)
                    {
                        _sortContextMenu.Update(currentMouseState, _previousMouseState, virtualMousePos, secondaryFont);
                    }
                    else
                    {
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
                        int endIndex = Math.Min(_displayItems.Count, startIndex + _maxVisibleRows * 2);
                        for (int i = 0; i < _displayItems.Count; i++)
                        {
                            if (_displayItems[i] is Button button)
                            {
                                if (i >= startIndex && i < endIndex)
                                {
                                    button.Update(currentMouseState);
                                }
                                else
                                {
                                    button.IsHovered = false;
                                }
                            }
                        }

                        _backButton.Update(currentMouseState);
                        _sortButton.Update(currentMouseState);
                    }
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

                        const int itemWidth = 145;
                        const int itemHeight = 9;
                        const int columnSpacing = 2;
                        const int rowSpacing = 0;
                        const int columns = 2;

                        int totalGridWidth = (itemWidth * columns) + columnSpacing;
                        int gridStartX = horizontalPadding + (Global.VIRTUAL_WIDTH - (horizontalPadding * 2) - totalGridWidth) / 2;

                        // --- Background Sprite ---
                        var bgSprite = spriteManager.ActionMovesBackgroundSprite;
                        var bgRect = new Rectangle(gridStartX - 1, dividerY + menuVerticalOffset - 1, 294, 47);
                        spriteBatch.DrawSnapped(bgSprite, bgRect, Color.White);

                        // --- Move List Area ---
                        int gridStartY = dividerY + menuVerticalOffset;
                        _moveListBounds = new Rectangle(gridStartX, gridStartY, totalGridWidth, moveListHeight);
                        _maxVisibleRows = moveListHeight / itemHeight;

                        _totalRows = (int)Math.Ceiling(_displayItems.Count / (double)columns);
                        int startIndex = _scrollIndex * columns;
                        int visibleItemCount = Math.Min(_displayItems.Count - startIndex, _maxVisibleRows * columns);

                        for (int i = 0; i < visibleItemCount; i++)
                        {
                            int itemIndex = startIndex + i;
                            var item = _displayItems[itemIndex];
                            if (item == null) continue;

                            int row = i / columns;
                            int col = i % columns;

                            if (item is HeaderContinuation) continue;

                            if (item is ActionMenuHeader header)
                            {
                                var headerBounds = new Rectangle(gridStartX, gridStartY + row * (itemHeight + rowSpacing), totalGridWidth, itemHeight);
                                header.Draw(spriteBatch, secondaryFont, headerBounds);
                            }
                            else if (item is MoveButton moveButton)
                            {
                                var itemBounds = new Rectangle(
                                    gridStartX + col * (itemWidth + columnSpacing),
                                    gridStartY + row * (itemHeight + rowSpacing),
                                    itemWidth,
                                    itemHeight
                                );
                                moveButton.Bounds = itemBounds;
                                moveButton.Draw(spriteBatch, secondaryFont, gameTime, transform);
                            }
                        }

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

                        var sortSize = secondaryFont.MeasureString(_sortButton.Text);
                        const int sortButtonPadding = 4;
                        int sortWidth = (int)sortSize.Width + sortButtonPadding;
                        _sortButton.Bounds = new Rectangle(
                            _moveListBounds.Right - sortWidth,
                            bottomBarY,
                            sortWidth,
                            bottomBarHeight
                        );

                        // --- Draw All ---
                        _sortButton.Draw(spriteBatch, font, gameTime, transform);
                        _backButton.Draw(spriteBatch, font, gameTime, transform);
                        _sortContextMenu.Draw(spriteBatch, secondaryFont);

                        // --- DEBUG DRAWING ---
                        if (_global.ShowDebugOverlays)
                        {
                            var pixel = ServiceLocator.Get<Texture2D>();
                            spriteBatch.DrawSnapped(pixel, bgRect, new Color(255, 165, 0, 100)); // Orange for background
                            spriteBatch.DrawSnapped(pixel, _moveListBounds, new Color(0, 0, 255, 100)); // Blue for scroll area
                            for (int i = 0; i < visibleItemCount; i++)
                            {
                                int itemIndex = startIndex + i;
                                if (_displayItems[itemIndex] is MoveButton button)
                                {
                                    spriteBatch.DrawSnapped(pixel, button.Bounds, new Color(255, 0, 0, 100)); // Red for buttons
                                }
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
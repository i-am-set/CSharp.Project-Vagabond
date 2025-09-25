#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using System.Text;

namespace ProjectVagabond.Battle.UI
{
    public class ItemMenu
    {
        public event Action? OnBack;
        public event Action<ConsumableItemData>? OnItemConfirmed;
        public event Action<ConsumableItemData>? OnItemTargetingRequested;
        private bool _isVisible;
        private readonly Global _global;
        private readonly List<IInventoryMenuItem> _displayItems = new List<IInventoryMenuItem>();
        private readonly Button _backButton;
        private readonly Button _sortButton;
        private readonly ContextMenu _sortContextMenu;
        private Button _yesButton;
        private Button _noButton;

        private int _scrollIndex = 0;
        private int _totalRows = 0;
        private int _maxVisibleRows = 0;
        private Rectangle _itemListBounds;
        private MouseState _previousMouseState;
        private bool _buttonsInitialized = false;

        private enum MenuState { List, Tooltip, Confirm }
        private MenuState _currentState = MenuState.List;

        private ConsumableItemData? _itemForTooltip;
        private ConsumableItemData? _itemForConfirmation;
        private List<BattleCombatant>? _allCombatants;
        public Button? HoveredButton { get; private set; }

        public ItemMenu()
        {
            _global = ServiceLocator.Get<Global>();
            _sortContextMenu = new ContextMenu();
            _backButton = new Button(Rectangle.Empty, "BACK", function: "Back");
            _backButton.OnClick += HandleBack;
            _sortButton = new Button(Rectangle.Empty, "SORT", function: "Sort");
            _sortButton.OnClick += OpenSortMenu;

            _yesButton = new Button(Rectangle.Empty, "YES");
            _yesButton.OnClick += () =>
            {
                if (_itemForConfirmation != null)
                {
                    OnItemConfirmed?.Invoke(_itemForConfirmation);
                }
            };

            _noButton = new Button(Rectangle.Empty, "NO");
            _noButton.OnClick += () => _currentState = MenuState.List;

            _previousMouseState = Mouse.GetState();
        }

        private void InitializeButtons()
        {
            if (_buttonsInitialized) return;
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            _backButton.Font = secondaryFont;
            _sortButton.Font = secondaryFont;
            _yesButton.Font = secondaryFont;
            _noButton.Font = secondaryFont;
            _buttonsInitialized = true;
        }

        public void Show(List<BattleCombatant> allCombatants)
        {
            _isVisible = true;
            _currentState = MenuState.List;
            _allCombatants = allCombatants;
            PopulateItems();
        }

        public void Hide()
        {
            _isVisible = false;
        }

        public void PopulateItems()
        {
            _displayItems.Clear();
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            var gameState = ServiceLocator.Get<GameState>();

            var items = gameState.PlayerState.Inventory
                .Where(kvp => kvp.Value > 0)
                .OrderBy(kvp => kvp.Key)
                .ToList();

            if (items.Any())
            {
                foreach (var itemEntry in items)
                {
                    if (BattleDataCache.Consumables.TryGetValue(itemEntry.Key, out var itemData))
                    {
                        var itemButton = new InventoryItemButton(itemData, itemEntry.Value, secondaryFont);
                        itemButton.OnClick += () => HandleItemClick(itemButton.Item);
                        itemButton.OnRightClick += () => HandleItemRightClick(itemButton.Item);
                        _displayItems.Add(itemButton);
                    }
                }
            }
        }

        private void HandleItemClick(ConsumableItemData item)
        {
            if (_allCombatants == null) return;

            switch (item.Target)
            {
                case TargetType.Single:
                    var enemies = _allCombatants.Where(c => !c.IsPlayerControlled && !c.IsDefeated).ToList();
                    if (enemies.Count > 1)
                    {
                        OnItemTargetingRequested?.Invoke(item);
                    }
                    else
                    {
                        _itemForConfirmation = item;
                        _currentState = MenuState.Confirm;
                    }
                    break;

                case TargetType.SingleAll:
                    var allTargets = _allCombatants.Where(c => !c.IsDefeated).ToList();
                    if (allTargets.Count > 1)
                    {
                        OnItemTargetingRequested?.Invoke(item);
                    }
                    else
                    {
                        _itemForConfirmation = item;
                        _currentState = MenuState.Confirm;
                    }
                    break;

                case TargetType.Self:
                case TargetType.None:
                case TargetType.Every:
                case TargetType.EveryAll:
                default:
                    _itemForConfirmation = item;
                    _currentState = MenuState.Confirm;
                    break;
            }
        }

        private void HandleItemRightClick(ConsumableItemData item)
        {
            _itemForTooltip = item;
            _currentState = MenuState.Tooltip;
        }

        private void HandleBack()
        {
            switch (_currentState)
            {
                case MenuState.Tooltip:
                case MenuState.Confirm:
                    _currentState = MenuState.List;
                    break;
                case MenuState.List:
                    OnBack?.Invoke();
                    break;
            }
        }

        public void GoBack()
        {
            if (!_isVisible) return;
            HandleBack();
        }

        private void OpenSortMenu()
        {
            // Placeholder for future sorting logic
        }

        public void Update(MouseState currentMouseState, GameTime gameTime)
        {
            InitializeButtons();
            if (!_isVisible) return;

            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            var virtualMousePos = Core.TransformMouse(currentMouseState.Position);

            HoveredButton = null; // Reset at the start of each frame

            if (_sortContextMenu.IsOpen)
            {
                _sortContextMenu.Update(currentMouseState, _previousMouseState, virtualMousePos, secondaryFont);
            }
            else
            {
                switch (_currentState)
                {
                    case MenuState.List:
                        UpdateList(currentMouseState);
                        break;
                    case MenuState.Tooltip:
                        UpdateTooltip(currentMouseState);
                        break;
                    case MenuState.Confirm:
                        UpdateConfirm(currentMouseState);
                        break;
                }
            }
            _previousMouseState = currentMouseState;
        }

        private void UpdateList(MouseState currentMouseState)
        {
            var virtualMousePos = Core.TransformMouse(currentMouseState.Position);
            if (_itemListBounds.Contains(virtualMousePos))
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
                        if (button.IsHovered) HoveredButton = button;
                    }
                    else
                    {
                        button.IsHovered = false;
                    }
                }
            }

            _backButton.Update(currentMouseState);
            if (_backButton.IsHovered) HoveredButton = _backButton;

            _sortButton.Update(currentMouseState);
            if (_sortButton.IsHovered) HoveredButton = _sortButton;
        }

        private void UpdateTooltip(MouseState currentMouseState)
        {
            bool leftClick = currentMouseState.LeftButton == ButtonState.Released && _previousMouseState.LeftButton == ButtonState.Pressed;
            bool rightClick = currentMouseState.RightButton == ButtonState.Released && _previousMouseState.RightButton == ButtonState.Pressed;
            if (leftClick || rightClick)
            {
                if (UIInputManager.CanProcessMouseClick())
                {
                    _currentState = MenuState.List;
                    UIInputManager.ConsumeMouseClick();
                }
            }
            _backButton.Update(currentMouseState);
            if (_backButton.IsHovered) HoveredButton = _backButton;
        }

        private void UpdateConfirm(MouseState currentMouseState)
        {
            _yesButton.Update(currentMouseState);
            if (_yesButton.IsHovered) HoveredButton = _yesButton;

            _noButton.Update(currentMouseState);
            if (_noButton.IsHovered) HoveredButton = _noButton;
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            InitializeButtons();
            if (!_isVisible) return;

            switch (_currentState)
            {
                case MenuState.List:
                    DrawList(spriteBatch, font, gameTime, transform);
                    break;
                case MenuState.Tooltip:
                    DrawTooltip(spriteBatch, font, gameTime, transform);
                    break;
                case MenuState.Confirm:
                    DrawConfirm(spriteBatch, font, gameTime, transform);
                    break;
            }
        }

        private void DrawList(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            var spriteManager = ServiceLocator.Get<SpriteManager>();

            // --- Layout Constants ---
            const int horizontalPadding = 10;
            const int dividerY = 114;
            const int menuVerticalOffset = 4;
            const int itemListHeight = 45;
            const int bottomBarTopPadding = 5;
            const int bottomBarHeight = 13;
            const int itemWidth = 145;
            const int itemHeight = 9;
            const int columnSpacing = 2;
            const int columns = 2;

            int totalGridWidth = (itemWidth * columns) + columnSpacing;
            int gridStartX = horizontalPadding + (Global.VIRTUAL_WIDTH - (horizontalPadding * 2) - totalGridWidth) / 2;

            var bgSprite = spriteManager.ActionMovesBackgroundSprite;
            var bgRect = new Rectangle(gridStartX - 1, dividerY + menuVerticalOffset - 1, 294, 47);
            spriteBatch.DrawSnapped(bgSprite, bgRect, Color.White);

            int gridStartY = dividerY + menuVerticalOffset;
            _itemListBounds = new Rectangle(gridStartX, gridStartY, totalGridWidth, itemListHeight);
            _maxVisibleRows = itemListHeight / itemHeight;

            if (!_displayItems.Any())
            {
                string emptyText = "EMPTY";
                var textSize = secondaryFont.MeasureString(emptyText);
                var textPos = new Vector2(
                    _itemListBounds.Center.X - textSize.Width / 2,
                    _itemListBounds.Center.Y - textSize.Height / 2
                );
                spriteBatch.DrawStringSnapped(secondaryFont, emptyText, textPos, _global.ButtonDisableColor);
            }
            else
            {
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

                    if (item is InventoryItemButton itemButton)
                    {
                        var itemBounds = new Rectangle(
                            gridStartX + col * (itemWidth + columnSpacing),
                            gridStartY + row * itemHeight,
                            itemWidth,
                            itemHeight
                        );
                        itemButton.Bounds = itemBounds;
                        itemButton.Draw(spriteBatch, secondaryFont, gameTime, transform);
                    }
                }

                if (_totalRows > _maxVisibleRows)
                {
                    var pixel = ServiceLocator.Get<Texture2D>();
                    int scrollbarX = _itemListBounds.Right + 2;
                    var scrollbarBgRect = new Rectangle(scrollbarX, _itemListBounds.Y, 1, _itemListBounds.Height);
                    spriteBatch.DrawSnapped(pixel, scrollbarBgRect, _global.Palette_DarkGray);

                    float handleHeight = Math.Max(5, (float)_maxVisibleRows / _totalRows * _itemListBounds.Height);
                    float handleY = _itemListBounds.Y;
                    if (_totalRows - _maxVisibleRows > 0)
                    {
                        handleY += ((float)_scrollIndex / (_totalRows - _maxVisibleRows) * (_itemListBounds.Height - handleHeight));
                    }

                    var scrollbarHandleRect = new Rectangle(scrollbarX, (int)handleY, 1, (int)handleHeight);
                    spriteBatch.DrawSnapped(pixel, scrollbarHandleRect, _global.Palette_White);
                }
            }

            int bottomBarY = _itemListBounds.Bottom + bottomBarTopPadding;
            var backSize = secondaryFont.MeasureString(_backButton.Text);
            int backWidth = (int)backSize.Width + 16;
            _backButton.Bounds = new Rectangle(_itemListBounds.Center.X - backWidth / 2, bottomBarY, backWidth, bottomBarHeight);

            var sortSize = secondaryFont.MeasureString(_sortButton.Text);
            int sortWidth = (int)sortSize.Width + 8;
            _sortButton.Bounds = new Rectangle(_itemListBounds.Right - sortWidth, bottomBarY, sortWidth, bottomBarHeight);

            _sortButton.Draw(spriteBatch, font, gameTime, transform);
            _backButton.Draw(spriteBatch, font, gameTime, transform);
            _sortContextMenu.Draw(spriteBatch, secondaryFont);
        }

        private void DrawTooltip(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            var pixel = ServiceLocator.Get<Texture2D>();

            // --- Layout Constants from Screenshot ---
            const int boxWidth = 294;
            const int boxHeight = 47;
            const int boxY = 117;
            int boxX = (Global.VIRTUAL_WIDTH - boxWidth) / 2;

            var tooltipBounds = new Rectangle(boxX, boxY, boxWidth, boxHeight);

            // Draw the border
            spriteBatch.DrawLineSnapped(new Vector2(tooltipBounds.Left, tooltipBounds.Top), new Vector2(tooltipBounds.Right, tooltipBounds.Top), _global.Palette_White);
            spriteBatch.DrawLineSnapped(new Vector2(tooltipBounds.Left, tooltipBounds.Bottom), new Vector2(tooltipBounds.Right, tooltipBounds.Bottom), _global.Palette_White);
            spriteBatch.DrawLineSnapped(new Vector2(tooltipBounds.Left, tooltipBounds.Top), new Vector2(tooltipBounds.Left, tooltipBounds.Bottom), _global.Palette_White);
            spriteBatch.DrawLineSnapped(new Vector2(tooltipBounds.Right, tooltipBounds.Top), new Vector2(tooltipBounds.Right, tooltipBounds.Bottom), _global.Palette_White);

            if (_itemForTooltip != null)
            {
                // 1. Draw Item Name
                var itemName = _itemForTooltip.ItemName.ToUpper();
                var nameSize = font.MeasureString(itemName);
                var namePos = new Vector2(
                    tooltipBounds.Center.X - nameSize.Width / 2,
                    tooltipBounds.Y + 8
                );
                spriteBatch.DrawStringSnapped(font, itemName, namePos, _global.Palette_BrightWhite);

                // 2. Draw Description (wrapped)
                string effectText = GetItemEffectDescription(_itemForTooltip);
                var wrappedLines = WrapTextByCharCount(effectText, 46);
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

            // Draw the back button
            int backButtonY = tooltipBounds.Bottom + 4;
            var backSize = secondaryFont.MeasureString(_backButton.Text);
            int backWidth = (int)backSize.Width + 16;
            _backButton.Bounds = new Rectangle(
                (Global.VIRTUAL_WIDTH - backWidth) / 2 + 1,
                backButtonY,
                backWidth,
                13
            );
            _backButton.Draw(spriteBatch, font, gameTime, transform);
        }

        private void DrawConfirm(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            const int horizontalPadding = 10;
            const int dividerY = 114;
            const int menuVerticalOffset = 4;
            const int bottomBarTopPadding = 5;
            const int itemWidth = 145;
            const int columns = 2;
            const int columnSpacing = 2;
            int totalGridWidth = (itemWidth * columns) + columnSpacing;
            int gridStartX = horizontalPadding + (Global.VIRTUAL_WIDTH - (horizontalPadding * 2) - totalGridWidth) / 2;
            int gridStartY = dividerY + menuVerticalOffset;

            var spriteManager = ServiceLocator.Get<SpriteManager>();
            var tooltipBg = spriteManager.ActionTooltipBackgroundSprite;
            var tooltipBgRect = new Rectangle(gridStartX - 1, gridStartY - 1, 294, 47);
            spriteBatch.DrawSnapped(tooltipBg, tooltipBgRect, Color.White);

            if (_itemForConfirmation != null)
            {
                string promptText = $"Use '{_itemForConfirmation.ItemName}'?";
                var promptSize = font.MeasureString(promptText);
                var promptPos = new Vector2(
                    tooltipBgRect.Center.X - promptSize.Width / 2,
                    tooltipBgRect.Y + 15
                );
                spriteBatch.DrawStringSnapped(font, promptText, promptPos, _global.Palette_BrightWhite);
            }

            int bottomBarY = tooltipBgRect.Bottom + bottomBarTopPadding;
            const int buttonWidth = 60;
            const int buttonHeight = 13;
            const int buttonSpacing = 10;
            int totalButtonsWidth = buttonWidth * 2 + buttonSpacing;
            int buttonsStartX = tooltipBgRect.Center.X - totalButtonsWidth / 2;

            _yesButton.Bounds = new Rectangle(buttonsStartX, bottomBarY, buttonWidth, buttonHeight);
            _noButton.Bounds = new Rectangle(buttonsStartX + buttonWidth + buttonSpacing, bottomBarY, buttonWidth, buttonHeight);

            _yesButton.Draw(spriteBatch, font, gameTime, transform);
            _noButton.Draw(spriteBatch, font, gameTime, transform);
        }

        private string GetItemEffectDescription(ConsumableItemData item)
        {
            return item.Description.ToUpper();
        }

        private List<string> WrapTextByCharCount(string text, int maxCharsPerLine)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(text)) return lines;

            var words = text.Split(' ');
            var currentLine = new StringBuilder();

            foreach (var word in words)
            {
                if (currentLine.Length > 0 && currentLine.Length + word.Length + 1 > maxCharsPerLine)
                {
                    lines.Add(currentLine.ToString());
                    currentLine.Clear();
                }

                if (word.Length > maxCharsPerLine)
                {
                    if (currentLine.Length > 0)
                    {
                        lines.Add(currentLine.ToString());
                        currentLine.Clear();
                    }
                    lines.Add(word);
                    continue;
                }

                if (currentLine.Length > 0)
                {
                    currentLine.Append(" ");
                }
                currentLine.Append(word);
            }

            if (currentLine.Length > 0)
            {
                lines.Add(currentLine.ToString());
            }

            return lines;
        }
    }
}
#nullable restore
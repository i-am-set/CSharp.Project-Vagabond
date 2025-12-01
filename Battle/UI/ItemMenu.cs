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

        // Debug Bounds
        private Rectangle _tooltipBounds;
        private Rectangle _confirmBounds;

        public ItemMenu()
        {
            _global = ServiceLocator.Get<Global>();
            _sortContextMenu = new ContextMenu();
            _backButton = new Button(Rectangle.Empty, "BACK", function: "Back", enableHoverSway: false) { CustomDefaultTextColor = _global.Palette_Gray };
            _backButton.OnClick += HandleBack;

            // Updated: Disable hover sway for Sort button
            _sortButton = new Button(Rectangle.Empty, "SORT", function: "Sort", enableHoverSway: false);
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

            // --- Use Consumables Inventory ---
            var items = gameState.PlayerState.Consumables
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
        }

        public void Update(MouseState currentMouseState, GameTime gameTime)
        {
            InitializeButtons();
            if (!_isVisible) return;

            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            var virtualMousePos = Core.TransformMouse(currentMouseState.Position);

            HoveredButton = null;

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

            bool rightClickHeldOnItem = false;
            ConsumableItemData? itemForTooltip = null;

            for (int i = 0; i < _displayItems.Count; i++)
            {
                if (_displayItems[i] is InventoryItemButton button)
                {
                    if (i >= startIndex && i < endIndex)
                    {
                        button.Update(currentMouseState);
                        if (button.IsHovered)
                        {
                            HoveredButton = button;
                            if (currentMouseState.RightButton == ButtonState.Pressed)
                            {
                                rightClickHeldOnItem = true;
                                itemForTooltip = button.Item;
                            }
                        }
                    }
                    else
                    {
                        button.IsHovered = false;
                    }
                }
            }

            if (rightClickHeldOnItem)
            {
                _itemForTooltip = itemForTooltip;
                _currentState = MenuState.Tooltip;
            }

            // Right click to back logic
            if (currentMouseState.RightButton == ButtonState.Pressed && _previousMouseState.RightButton == ButtonState.Released)
            {
                if (!rightClickHeldOnItem)
                {
                    HandleBack();
                }
            }

            _backButton.Update(currentMouseState);
            if (_backButton.IsHovered) HoveredButton = _backButton;

            _sortButton.Update(currentMouseState);
            if (_sortButton.IsHovered) HoveredButton = _sortButton;
        }

        private void UpdateTooltip(MouseState currentMouseState)
        {
            // Release right click to exit tooltip
            if (currentMouseState.RightButton == ButtonState.Released)
            {
                _currentState = MenuState.List;
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

            // --- DEBUG DRAWING (F1) ---
            if (_global.ShowSplitMapGrid)
            {
                var pixel = ServiceLocator.Get<Texture2D>();

                if (_currentState == MenuState.List)
                {
                    spriteBatch.DrawSnapped(pixel, _itemListBounds, Color.Blue * 0.2f);
                    spriteBatch.DrawSnapped(pixel, _backButton.Bounds, Color.Red * 0.5f);
                    spriteBatch.DrawSnapped(pixel, _sortButton.Bounds, Color.Red * 0.5f);

                    // Draw visible item buttons
                    int startIndex = _scrollIndex * 2;
                    int endIndex = Math.Min(_displayItems.Count, startIndex + _maxVisibleRows * 2);
                    for (int i = startIndex; i < endIndex; i++)
                    {
                        if (_displayItems[i] is InventoryItemButton button)
                        {
                            spriteBatch.DrawSnapped(pixel, button.Bounds, Color.LightBlue * 0.5f);
                        }
                    }
                }
                else if (_currentState == MenuState.Tooltip)
                {
                    spriteBatch.DrawSnapped(pixel, _tooltipBounds, Color.Magenta * 0.5f);
                    spriteBatch.DrawSnapped(pixel, _backButton.Bounds, Color.Red * 0.5f);
                }
                else if (_currentState == MenuState.Confirm)
                {
                    spriteBatch.DrawSnapped(pixel, _confirmBounds, Color.Magenta * 0.5f);
                    spriteBatch.DrawSnapped(pixel, _yesButton.Bounds, Color.Red * 0.5f);
                    spriteBatch.DrawSnapped(pixel, _noButton.Bounds, Color.Red * 0.5f);
                }
            }
        }

        private void DrawList(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            var spriteManager = ServiceLocator.Get<SpriteManager>();

            const int horizontalPadding = 10;
            const int dividerY = 122;
            const int menuVerticalOffset = 4;
            const int itemListHeight = 37;
            const int bottomBarTopPadding = 3;
            const int bottomBarHeight = 15;
            const int itemWidth = 145;
            const int itemHeight = 9;
            const int columnSpacing = 2;
            const int columns = 2;

            int totalGridWidth = (itemWidth * columns) + columnSpacing;
            int gridStartX = horizontalPadding + (Global.VIRTUAL_WIDTH - (horizontalPadding * 2) - totalGridWidth) / 2;

            // Draw opaque black background
            var pixel = ServiceLocator.Get<Texture2D>();
            var bgColor = _global.Palette_Black;
            const int bgY = 123;
            var bgRect = new Rectangle(0, bgY, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT - bgY);
            spriteBatch.DrawSnapped(pixel, bgRect, bgColor);

            // Draw Border
            spriteBatch.DrawSnapped(spriteManager.BattleBorderItem, Vector2.Zero, Color.White);

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
            var spriteManager = ServiceLocator.Get<SpriteManager>();

            // Layout constants matching ActionMenu
            const int dividerY = 114;
            const int moveButtonWidth = 157;
            const int columns = 2;
            const int columnSpacing = 0;
            int totalGridWidth = (moveButtonWidth * columns) + columnSpacing; // 314
            const int gridHeight = 40;
            int gridStartX = (Global.VIRTUAL_WIDTH - totalGridWidth) / 2;
            int gridStartY = dividerY + 2 + 12; // 128

            var tooltipBg = spriteManager.ActionTooltipBackgroundSprite;
            var tooltipBgRect = new Rectangle(gridStartX, gridStartY, totalGridWidth, gridHeight);
            _tooltipBounds = tooltipBgRect; // Store for debug

            // Draw opaque black background
            spriteBatch.DrawSnapped(pixel, tooltipBgRect, _global.Palette_Black);
            // Draw sprite background
            spriteBatch.DrawSnapped(tooltipBg, tooltipBgRect, Color.White);

            if (_itemForTooltip != null)
            {
                // Draw Content
                const int horizontalPadding = 4;
                const int verticalPadding = 3;
                float currentY = tooltipBgRect.Y + verticalPadding;

                // Name
                var itemName = _itemForTooltip.ItemName.ToUpper();
                var namePos = new Vector2(tooltipBgRect.X + horizontalPadding, currentY);
                spriteBatch.DrawStringSnapped(font, itemName, namePos, _global.Palette_BrightWhite);

                // Type (Right aligned)
                string typeText = "CONSUMABLE";
                var typeSize = secondaryFont.MeasureString(typeText);
                var typePos = new Vector2(tooltipBgRect.Right - horizontalPadding - typeSize.Width, currentY + (font.LineHeight - secondaryFont.LineHeight) / 2);
                spriteBatch.DrawStringSnapped(secondaryFont, typeText, typePos, _global.Palette_LightBlue);

                currentY += font.LineHeight + 1;

                // Underline
                var underlineStart = new Vector2(tooltipBgRect.X + horizontalPadding, currentY);
                var underlineEnd = new Vector2(tooltipBgRect.Right - horizontalPadding, currentY);
                spriteBatch.DrawLineSnapped(underlineStart, underlineEnd, _global.Palette_DarkGray);
                currentY += 3;

                // Description
                if (!string.IsNullOrEmpty(_itemForTooltip.Description))
                {
                    float availableWidth = tooltipBgRect.Width - (horizontalPadding * 2);
                    var wrappedLines = WrapText(_itemForTooltip.Description.ToUpper(), availableWidth, secondaryFont);
                    foreach (var line in wrappedLines)
                    {
                        if (currentY + secondaryFont.LineHeight > tooltipBgRect.Bottom - verticalPadding) break;
                        var descPos = new Vector2(tooltipBgRect.X + horizontalPadding, currentY);
                        spriteBatch.DrawStringSnapped(secondaryFont, line, descPos, _global.Palette_White);
                        currentY += secondaryFont.LineHeight;
                    }
                }
            }

            // Back Button
            const int backButtonTopMargin = 0;
            int backButtonY = gridStartY + gridHeight + backButtonTopMargin + 2;

            // Ensure button size matches ActionMenu
            var backSize = (_backButton.Font ?? font).MeasureString(_backButton.Text);
            int backWidth = (int)backSize.Width + 16;

            _backButton.Bounds = new Rectangle(
                (Global.VIRTUAL_WIDTH - backWidth) / 2 + 1, // +1 X from ActionMenu adjustment
                backButtonY,
                backWidth,
                7 // Height from ActionMenu
            );
            _backButton.Draw(spriteBatch, font, gameTime, transform);
        }

        private void DrawConfirm(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            const int horizontalPadding = 10;
            const int dividerY = 122;
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
            _confirmBounds = tooltipBgRect; // Store for debug

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
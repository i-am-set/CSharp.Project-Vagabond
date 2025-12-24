#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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

        // Buttons
        private readonly Button _backButton;
        private readonly Button _sortButton;
        private readonly Button _useButton;

        private readonly ContextMenu _sortContextMenu;

        // Scrolling / List State
        private int _scrollIndex = 0;
        private int _totalRows = 0;
        private int _maxVisibleRows = 0;
        private Rectangle _itemListBounds;

        // Input State
        private MouseState _previousMouseState;
        private bool _buttonsInitialized = false;

        // Menu States
        private enum MenuState { List, Selected }
        private MenuState _currentState = MenuState.List;

        // Selection Data
        private ConsumableItemData? _selectedItem; // For Selection (Left Click)

        private List<BattleCombatant>? _allCombatants;
        public Button? HoveredButton { get; private set; }

        // Debug Bounds
        private Rectangle _tooltipBounds;

        public ItemMenu()
        {
            _global = ServiceLocator.Get<Global>();
            _sortContextMenu = new ContextMenu();

            _backButton = new Button(Rectangle.Empty, "BACK", function: "Back", enableHoverSway: false) { CustomDefaultTextColor = _global.Palette_Gray };
            _backButton.OnClick += HandleBack;

            _sortButton = new Button(Rectangle.Empty, "SORT", function: "Sort", enableHoverSway: false);
            _sortButton.OnClick += OpenSortMenu;

            // Initialize the USE button for the Selected state
            _useButton = new Button(Rectangle.Empty, "USE", function: "UseItem", enableHoverSway: false);
            _useButton.OnClick += () =>
            {
                if (_selectedItem != null)
                {
                    HandleItemUse(_selectedItem);
                }
            };

            _previousMouseState = Mouse.GetState();
        }

        private void InitializeButtons()
        {
            if (_buttonsInitialized) return;
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            _backButton.Font = secondaryFont;
            _sortButton.Font = secondaryFont;
            _useButton.Font = secondaryFont;
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
                        // Left Click -> Select (Toggle)
                        itemButton.OnClick += () =>
                        {
                            _selectedItem = itemButton.Item;
                            _currentState = MenuState.Selected;
                        };
                        _displayItems.Add(itemButton);
                    }
                }
            }
        }

        private void HandleItemUse(ConsumableItemData item)
        {
            if (_allCombatants == null) return;

            switch (item.Target)
            {
                case TargetType.Single:
                case TargetType.SingleTeam:
                case TargetType.Every:
                case TargetType.All:
                    // Always request targeting for these types
                    OnItemTargetingRequested?.Invoke(item);
                    break;

                default:
                    OnItemConfirmed?.Invoke(item);
                    break;
            }
        }

        private void HandleBack()
        {
            switch (_currentState)
            {
                case MenuState.Selected:
                    _currentState = MenuState.List;
                    _selectedItem = null;
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
            // Sort logic placeholder
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
                    case MenuState.Selected:
                        UpdateSelected(currentMouseState);
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
                if (_displayItems[i] is InventoryItemButton button)
                {
                    if (i >= startIndex && i < endIndex)
                    {
                        button.Update(currentMouseState);
                        if (button.IsHovered)
                        {
                            HoveredButton = button;
                        }
                    }
                    else
                    {
                        button.IsHovered = false;
                    }
                }
            }

            // Right click to back logic
            if (currentMouseState.RightButton == ButtonState.Pressed && _previousMouseState.RightButton == ButtonState.Released)
            {
                HandleBack();
            }

            _backButton.Update(currentMouseState);
            if (_backButton.IsHovered) HoveredButton = _backButton;

            _sortButton.Update(currentMouseState);
            if (_sortButton.IsHovered) HoveredButton = _sortButton;
        }

        private void UpdateSelected(MouseState currentMouseState)
        {
            // Right click to go back (Toggle behavior)
            if (currentMouseState.RightButton == ButtonState.Pressed && _previousMouseState.RightButton == ButtonState.Released)
            {
                HandleBack();
                return;
            }

            // Update USE button first
            _useButton.Update(currentMouseState);

            if (_useButton.IsHovered)
            {
                HoveredButton = _useButton;
                // If hovering USE, force BACK to not be hovered to prevent conflict
                _backButton.IsHovered = false;
            }
            else
            {
                // Only update BACK if USE is not hovered
                _backButton.Update(currentMouseState);
                if (_backButton.IsHovered) HoveredButton = _backButton;
            }
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            InitializeButtons();
            if (!_isVisible) return;

            // Always draw the list first to serve as the background
            DrawList(spriteBatch, font, gameTime, transform);

            // Then draw the overlay if needed
            if (_currentState == MenuState.Selected)
            {
                // Draw the info panel for the selected item (Left Click Toggle)
                DrawItemInfoPanel(spriteBatch, font, gameTime, transform, _selectedItem, true);
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
                }
                else if (_currentState == MenuState.Selected)
                {
                    spriteBatch.DrawSnapped(pixel, _tooltipBounds, Color.Magenta * 0.5f);
                    spriteBatch.DrawSnapped(pixel, _backButton.Bounds, Color.Red * 0.5f);
                    if (_currentState == MenuState.Selected)
                    {
                        spriteBatch.DrawSnapped(pixel, _useButton.Bounds, Color.Green * 0.5f);
                    }
                }
            }
        }

        private void DrawList(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            var spriteManager = ServiceLocator.Get<SpriteManager>();

            const int horizontalPadding = 10;
            const int dividerY = 122;
            const int menuVerticalOffset = 8;
            const int itemListHeight = 37;
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

                InventoryItemButton? hoveredItemButton = null;

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

                        // Defer drawing if hovered to ensure it draws on top
                        if (itemButton.IsHovered)
                        {
                            hoveredItemButton = itemButton;
                        }
                        else
                        {
                            itemButton.Draw(spriteBatch, secondaryFont, gameTime, transform);
                        }
                    }
                }

                // Draw the hovered item last so its upscaled sprite is on top
                if (hoveredItemButton != null)
                {
                    hoveredItemButton.Draw(spriteBatch, secondaryFont, gameTime, transform);
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

            int bottomBarY = 165;
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

        /// <summary>
        /// Draws the item information panel. Used for Selected (Left-Click Toggle) state.
        /// </summary>
        private void DrawItemInfoPanel(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform, ConsumableItemData? item, bool showUseButton)
        {
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            var pixel = ServiceLocator.Get<Texture2D>();
            var spriteManager = ServiceLocator.Get<SpriteManager>();

            // Layout constants matching ActionMenu exactly
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

            if (item != null)
            {
                // Draw Content
                const int horizontalPadding = 4;
                const int verticalPadding = 3;
                float currentY = tooltipBgRect.Y + verticalPadding;

                // Name
                var itemName = item.ItemName.ToUpper();
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
                if (!string.IsNullOrEmpty(item.Description))
                {
                    float availableWidth = tooltipBgRect.Width - (horizontalPadding * 2);
                    // If showing USE button, reduce available width for description to avoid overlap
                    if (showUseButton)
                    {
                        availableWidth -= 50; // Reserve space for button
                    }

                    var wrappedLines = WrapText(item.Description.ToUpper(), availableWidth, secondaryFont);
                    foreach (var line in wrappedLines)
                    {
                        if (currentY + secondaryFont.LineHeight > tooltipBgRect.Bottom - verticalPadding) break;
                        var descPos = new Vector2(tooltipBgRect.X + horizontalPadding, currentY);
                        spriteBatch.DrawStringSnapped(secondaryFont, line, descPos, _global.Palette_White);
                        currentY += secondaryFont.LineHeight;
                    }
                }
            }

            // Back Button (Always drawn at the standard position)
            // MATCHING ITEM MENU LIST POSITION (Y=165)
            int backButtonY = 165;

            var backSize = (_backButton.Font ?? font).MeasureString(_backButton.Text);
            int backWidth = (int)backSize.Width + 16;
            int backX = (Global.VIRTUAL_WIDTH - backWidth) / 2 + 1; // Centered + 1px right

            _backButton.Bounds = new Rectangle(
                backX,
                backButtonY,
                backWidth,
                15
            );
            _backButton.Draw(spriteBatch, font, gameTime, transform);

            // Draw USE Button if in Selected state
            if (showUseButton)
            {
                var useSize = (_useButton.Font ?? font).MeasureString(_useButton.Text);
                int useWidth = (int)useSize.Width + 16;
                int useHeight = 15; // Match back button height

                // Position directly above the back button, moved down 5 pixels from previous logic
                // Previous logic: backButtonY - useHeight - 2
                int useX = (Global.VIRTUAL_WIDTH - useWidth) / 2; // Removed + 1 to shift left
                int useY = backButtonY - useHeight + 3;

                _useButton.Bounds = new Rectangle(useX, useY, useWidth, useHeight);
                _useButton.Draw(spriteBatch, font, gameTime, transform);
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

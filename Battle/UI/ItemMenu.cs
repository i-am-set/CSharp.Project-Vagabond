using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using MonoGame.Extended.BitmapFonts;

namespace ProjectVagabond.Battle.UI
{
    public class ItemMenu
    {
        public event Action OnBack;
        private bool _isVisible;
        private readonly Global _global;
        private readonly List<IInventoryMenuItem> _displayItems = new List<IInventoryMenuItem>();
        private readonly Button _backButton;
        private readonly Button _sortButton;
        private readonly ContextMenu _sortContextMenu;

        private int _scrollIndex = 0;
        private int _totalRows = 0;
        private int _maxVisibleRows = 0;
        private Rectangle _itemListBounds;
        private MouseState _previousMouseState;
        private bool _buttonsInitialized = false;

        public ItemMenu()
        {
            _global = ServiceLocator.Get<Global>();
            _sortContextMenu = new ContextMenu();
            _backButton = new Button(Rectangle.Empty, "BACK", function: "Back");
            _backButton.OnClick += () => OnBack?.Invoke();
            _sortButton = new Button(Rectangle.Empty, "SORT", function: "Sort");
            _sortButton.OnClick += OpenSortMenu;
            _previousMouseState = Mouse.GetState();
        }

        private void InitializeButtons()
        {
            if (_buttonsInitialized) return;
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            _backButton.Font = secondaryFont;
            _sortButton.Font = secondaryFont;
            _buttonsInitialized = true;
        }

        public void Show()
        {
            _isVisible = true;
            // For now, populate with dummy data. Later, this will come from an inventory system.
            PopulateDummyItems();
        }

        public void Hide()
        {
            _isVisible = false;
        }

        private void PopulateDummyItems()
        {
            _displayItems.Clear();
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;

            // This list will eventually come from the player's inventory component/state
            var items = new List<string>(); // Empty for now

            if (items.Any())
            {
                for (int i = 1; i <= 20; i++)
                {
                    _displayItems.Add(new InventoryItemButton($"Dummy Item {i}", secondaryFont));
                }
            }
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

            if (_sortContextMenu.IsOpen)
            {
                _sortContextMenu.Update(currentMouseState, _previousMouseState, virtualMousePos, secondaryFont);
            }
            else
            {
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
            _previousMouseState = currentMouseState;
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            InitializeButtons();
            if (!_isVisible) return;

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
    }
}
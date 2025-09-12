using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond
{
    public class ContextMenu
    {
        private readonly Global _global;

        private List<ContextMenuItem> _allItems = new List<ContextMenuItem>();
        private List<ContextMenuItem> _visibleItems = new List<ContextMenuItem>();
        private bool _isOpen;
        private Vector2 _position;
        private Rectangle _bounds;
        private int _hoveredIndex = -1;
        private bool _isPressedInsideMenu = false;

        public bool IsOpen => _isOpen;

        public ContextMenu()
        {
            _global = ServiceLocator.Get<Global>();
        }

        public void Show(Vector2 position, List<ContextMenuItem> items, BitmapFont font)
        {
            _allItems = items;
            _visibleItems = _allItems.Where(i => i.IsVisible()).ToList();
            if (!_visibleItems.Any()) return;

            _position = position;
            _isOpen = true;
            _hoveredIndex = -1;
            _isPressedInsideMenu = false;

            float width = _visibleItems.Max(i => font.MeasureString(i.Text).Width) + 24; // Increased padding for selection square
            float height = (_visibleItems.Count * (font.LineHeight + 4)) + 8;
            _bounds = new Rectangle((int)position.X, (int)position.Y, (int)width, (int)height);
        }

        public void Hide() => _isOpen = false;

        public void Update(MouseState currentMouseState, MouseState previousMouseState, Vector2 virtualMousePos, BitmapFont font)
        {
            if (!_isOpen) return;

            bool mousePressed = currentMouseState.LeftButton == ButtonState.Pressed && previousMouseState.LeftButton == ButtonState.Released;
            bool mouseReleased = currentMouseState.LeftButton == ButtonState.Released && previousMouseState.LeftButton == ButtonState.Pressed;
            bool rightClickPressed = currentMouseState.RightButton == ButtonState.Pressed && previousMouseState.RightButton == ButtonState.Released;

            if (mousePressed)
            {
                if (_bounds.Contains(virtualMousePos))
                {
                    _isPressedInsideMenu = true;
                }
            }

            if (mouseReleased)
            {
                if (_isPressedInsideMenu && _bounds.Contains(virtualMousePos) && _hoveredIndex != -1 && _visibleItems[_hoveredIndex].IsEnabled())
                {
                    _visibleItems[_hoveredIndex].OnClick?.Invoke();
                }
                // Always hide on release, whether it was a successful click or not
                Hide();
            }
            else if (rightClickPressed)
            {
                Hide();
            }

            _hoveredIndex = -1;
            if (_bounds.Contains(virtualMousePos))
            {
                float yOffset = virtualMousePos.Y - _bounds.Y - 4;
                int itemHeight = font.LineHeight + 4;
                int index = (int)(yOffset / itemHeight);
                if (index >= 0 && index < _visibleItems.Count && _visibleItems[index].Text != "---")
                {
                    _hoveredIndex = index;
                }
            }
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont font)
        {
            if (!_isOpen) return;

            Texture2D pixel = ServiceLocator.Get<Texture2D>();

            spriteBatch.Draw(pixel, _bounds, _global.ToolTipBGColor * 0.9f);
            spriteBatch.Draw(pixel, new Rectangle(_bounds.X, _bounds.Y, _bounds.Width, 1), _global.ToolTipBorderColor);
            spriteBatch.Draw(pixel, new Rectangle(_bounds.X, _bounds.Bottom - 1, _bounds.Width, 1), _global.ToolTipBorderColor);
            spriteBatch.Draw(pixel, new Rectangle(_bounds.X, _bounds.Y, 1, _bounds.Height), _global.ToolTipBorderColor);
            spriteBatch.Draw(pixel, new Rectangle(_bounds.Right - 1, _bounds.Y, 1, _bounds.Height), _global.ToolTipBorderColor);

            float y = _bounds.Y + 4;
            for (int i = 0; i < _visibleItems.Count; i++)
            {
                var item = _visibleItems[i];

                if (item.Text == "---")
                {
                    var lineRect = new Rectangle(_bounds.X + 4, (int)(y + (font.LineHeight / 2)), _bounds.Width - 8, 1);
                    spriteBatch.Draw(pixel, lineRect, _global.Palette_Gray);
                }
                else
                {
                    Color color;
                    bool isEnabled = item.IsEnabled();

                    if (!isEnabled)
                    {
                        color = _global.Palette_Gray;
                    }
                    else if (i == _hoveredIndex)
                    {
                        color = _global.ButtonHoverColor;
                    }
                    else
                    {
                        color = item.Color ?? _global.ToolTipTextColor;
                    }

                    float textX = _bounds.X + 8;

                    if (item.IsSelected())
                    {
                        const int squareSize = 5;
                        var squareRect = new Rectangle(
                            (int)textX,
                            (int)(y + (font.LineHeight - squareSize) / 2f),
                            squareSize,
                            squareSize
                        );
                        spriteBatch.Draw(pixel, squareRect, color);
                        textX += squareSize + 4;
                    }
                    else
                    {
                        textX += 9; // Same offset to keep text aligned
                    }

                    spriteBatch.DrawString(font, item.Text, new Vector2(textX, y), color);
                }
                y += font.LineHeight + 4;
            }
        }
    }
}
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond
{
    public class ContextMenuItem
    {
        public string Text { get; set; }
        public Action OnClick { get; set; }
        public Func<bool> IsVisible { get; set; } = () => true;
        public Color? Color { get; set; }
    }

    public class ContextMenu
    {
        private List<ContextMenuItem> _allItems = new List<ContextMenuItem>();
        private List<ContextMenuItem> _visibleItems = new List<ContextMenuItem>();
        private bool _isOpen;
        private Vector2 _position;
        private Rectangle _bounds;
        private int _hoveredIndex = -1;

        public bool IsOpen => _isOpen;

        public void Show(Vector2 position, List<ContextMenuItem> items)
        {
            _allItems = items;
            _visibleItems = _allItems.Where(i => i.IsVisible()).ToList();
            if (!_visibleItems.Any()) return;

            _position = position;
            _isOpen = true;
            _hoveredIndex = -1;

            float width = _visibleItems.Max(i => Global.Instance.DefaultFont.MeasureString(i.Text).Width) + 16;
            float height = (_visibleItems.Count * (Global.Instance.DefaultFont.LineHeight + 4)) + 8;
            _bounds = new Rectangle((int)position.X, (int)position.Y, (int)width, (int)height);
        }

        public void Hide() => _isOpen = false;

        public void Update(MouseState currentMouseState, MouseState previousMouseState, Vector2 virtualMousePos)
        {
            if (!_isOpen) return;

            bool leftClickPressed = currentMouseState.LeftButton == ButtonState.Pressed && previousMouseState.LeftButton == ButtonState.Released;
            bool rightClickPressed = currentMouseState.RightButton == ButtonState.Pressed && previousMouseState.RightButton == ButtonState.Released;

            if (leftClickPressed || rightClickPressed)
            {
                if (leftClickPressed && _bounds.Contains(virtualMousePos) && _hoveredIndex != -1)
                {
                    _visibleItems[_hoveredIndex].OnClick?.Invoke();
                    Hide();
                }
                else if (!_bounds.Contains(virtualMousePos))
                {
                    Hide();
                }
            }
            {
                _hoveredIndex = -1;
                if (_bounds.Contains(virtualMousePos))
                {
                    float yOffset = virtualMousePos.Y - _bounds.Y - 4;
                    int itemHeight = Global.Instance.DefaultFont.LineHeight + 4;
                    int index = (int)(yOffset / itemHeight);
                    if (index >= 0 && index < _visibleItems.Count)
                    {
                        _hoveredIndex = index;
                    }
                }
            }
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            if (!_isOpen) return;

            var pixel = new Texture2D(Core.Instance.GraphicsDevice, 1, 1);
            pixel.SetData(new[] { Color.White });

            spriteBatch.Draw(pixel, _bounds, Global.Instance.ToolTipBGColor * 0.9f);
            spriteBatch.Draw(pixel, new Rectangle(_bounds.X, _bounds.Y, _bounds.Width, 1), Global.Instance.ToolTipBorderColor);
            spriteBatch.Draw(pixel, new Rectangle(_bounds.X, _bounds.Bottom - 1, _bounds.Width, 1), Global.Instance.ToolTipBorderColor);
            spriteBatch.Draw(pixel, new Rectangle(_bounds.X, _bounds.Y, 1, _bounds.Height), Global.Instance.ToolTipBorderColor);
            spriteBatch.Draw(pixel, new Rectangle(_bounds.Right - 1, _bounds.Y, 1, _bounds.Height), Global.Instance.ToolTipBorderColor);

            float y = _bounds.Y + 4;
            for (int i = 0; i < _visibleItems.Count; i++)
            {
                var item = _visibleItems[i];
                Color color;

                if (i == _hoveredIndex)
                {
                    color = Global.Instance.OptionHoverColor;
                }
                else
                {
                    color = item.Color ?? Global.Instance.ToolTipTextColor;
                }

                spriteBatch.DrawString(Global.Instance.DefaultFont, item.Text, new Vector2(_bounds.X + 8, y), color);
                y += Global.Instance.DefaultFont.LineHeight + 4;
            }
        }
    }
}
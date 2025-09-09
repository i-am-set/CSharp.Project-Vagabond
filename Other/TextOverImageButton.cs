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

namespace ProjectVagabond.UI
{
    public class TextOverImageButton : Button
    {
        private readonly Texture2D _backgroundTexture;

        public TextOverImageButton(Rectangle bounds, string text, Texture2D backgroundTexture, string? function = null, Color? customDefaultTextColor = null, Color? customHoverTextColor = null, Color? customDisabledTextColor = null, bool alignLeft = false, float overflowScrollSpeed = 0, bool enableHoverSway = true, bool clickOnPress = false, BitmapFont? font = null)
            : base(bounds, text, function, customDefaultTextColor, customHoverTextColor, customDisabledTextColor, alignLeft, overflowScrollSpeed, enableHoverSway, clickOnPress, font)
        {
            _backgroundTexture = backgroundTexture;
        }

        public override void Draw(SpriteBatch spriteBatch, BitmapFont defaultFont, GameTime gameTime, Matrix transform, bool forceHover = false)
        {
            bool isActivated = IsEnabled && (IsHovered || forceHover);
            Color tintColor = Color.White;

            if (!IsEnabled)
            {
                tintColor = _global.ButtonDisableColor * 0.5f;
            }
            else if (_isPressed)
            {
                tintColor = Color.Gray;
            }
            else if (isActivated)
            {
                tintColor = _global.ButtonHoverColor;
            }

            spriteBatch.DrawSnapped(_backgroundTexture, this.Bounds, tintColor);

            // Now, call the base class's Draw method to render the text on top.
            base.Draw(spriteBatch, defaultFont, gameTime, transform, forceHover);
        }
    }
}
#nullable restore

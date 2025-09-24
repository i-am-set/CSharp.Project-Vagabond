using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ProjectVagabond.UI
{
    public class ChoiceCard : Button
    {
        public object Data { get; }
        public string Title { get; private set; }
        public string Description { get; private set; }
        public string SubText { get; private set; } // For stats like Power/Accuracy

        public ChoiceCard(Rectangle bounds, MoveData move) : base(bounds, move.MoveName)
        {
            Data = move;
            Title = move.MoveName.ToUpper();
            Description = move.Description.ToUpper();
            SubText = $"POW: {move.Power} / ACC: {move.Accuracy}% / MANA: {move.ManaCost}";
        }

        public ChoiceCard(Rectangle bounds, AbilityData ability) : base(bounds, ability.AbilityName)
        {
            Data = ability;
            Title = ability.AbilityName.ToUpper();
            Description = ability.Description.ToUpper();
            SubText = "PASSIVE ABILITY";
        }

        public ChoiceCard(Rectangle bounds, ConsumableItemData item) : base(bounds, item.ItemName)
        {
            Data = item;
            Title = item.ItemName.ToUpper();
            Description = item.Description.ToUpper();
            SubText = "CONSUMABLE ITEM";
        }

        public override void Draw(SpriteBatch spriteBatch, BitmapFont defaultFont, GameTime gameTime, Matrix transform, bool forceHover = false, float? externalSwayOffset = null)
        {
            // Placeholder drawing logic. This will be implemented in the next step.
            var pixel = ServiceLocator.Get<Texture2D>();
            bool isActivated = IsEnabled && (IsHovered || forceHover);

            spriteBatch.DrawSnapped(pixel, Bounds, isActivated ? _global.Palette_DarkGray : _global.Palette_Black);
            spriteBatch.DrawLineSnapped(new Vector2(Bounds.Left, Bounds.Top), new Vector2(Bounds.Right, Bounds.Top), _global.Palette_White);
            spriteBatch.DrawLineSnapped(new Vector2(Bounds.Left, Bounds.Bottom), new Vector2(Bounds.Right, Bounds.Bottom), _global.Palette_White);
            spriteBatch.DrawLineSnapped(new Vector2(Bounds.Left, Bounds.Top), new Vector2(Bounds.Left, Bounds.Bottom), _global.Palette_White);
            spriteBatch.DrawLineSnapped(new Vector2(Bounds.Right, Bounds.Top), new Vector2(Bounds.Right, Bounds.Bottom), _global.Palette_White);

            var titleSize = defaultFont.MeasureString(Title);
            var titlePos = new Vector2(Bounds.Center.X - titleSize.Width / 2, Bounds.Y + 10);
            spriteBatch.DrawStringSnapped(defaultFont, Title, titlePos, isActivated ? _global.ButtonHoverColor : _global.Palette_BrightWhite);
        }
    }
}
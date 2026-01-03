using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Progression;
using ProjectVagabond.Scenes;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace ProjectVagabond
{
    public class ColoredText
    {
        public string Text { get; set; }
        public Color Color { get; set; }
        public TextEffectType Effect { get; set; } = TextEffectType.None;

        public ColoredText(string text, Color color, TextEffectType effect = TextEffectType.None)
        {
            Text = text;
            Color = color;
            Effect = effect;
        }
    }
}

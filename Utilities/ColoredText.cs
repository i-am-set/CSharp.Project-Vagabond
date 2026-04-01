using Microsoft.Xna.Framework;
using ProjectVagabond.UI;

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
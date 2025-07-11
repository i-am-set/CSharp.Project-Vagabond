using Microsoft.Xna.Framework;

namespace ProjectVagabond
{
    public class ColoredText
    {
        public string Text { get; set; }
        public Color Color { get; set; }

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public ColoredText(string text, Color color)
        {
            Text = text;
            Color = color;
        }
    }
}

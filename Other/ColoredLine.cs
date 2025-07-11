using System.Collections.Generic;

namespace ProjectVagabond
{
    public class ColoredLine
    {
        public List<ColoredText> Segments { get; set; } = new List<ColoredText>();
        public int LineNumber { get; set; } = 0;
    }
}

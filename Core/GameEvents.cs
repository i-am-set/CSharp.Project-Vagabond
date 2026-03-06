using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace ProjectVagabond
{
    public static class GameEvents
    {
        public struct RoundLogUpdate
        {
            public string LogText { get; set; }
        }

        public struct TerminalMessagePublished
        {
            public string Message { get; set; }
            public Microsoft.Xna.Framework.Color? BaseColor { get; set; }
        }

        public struct AlertPublished
        {
            public string Message { get; set; }
        }

        public struct UIThemeOrResolutionChanged { }
    }
}
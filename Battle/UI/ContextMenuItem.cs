using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond;
using System;
using System.Collections.Generic;

namespace ProjectVagabond
{
    public class ContextMenuItem
    {
        public string Text { get; set; }
        public Action OnClick { get; set; }
        public Func<bool> IsVisible { get; set; } = () => true;
        public Func<bool> IsEnabled { get; set; } = () => true;
        public Func<bool> IsSelected { get; set; } = () => false;
        public Color? Color { get; set; }
    }
}
﻿
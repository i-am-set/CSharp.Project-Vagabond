using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Progression;
using ProjectVagabond.Scenes;
using ProjectVagabond.Transitions;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ProjectVagabond.Utils
{
    public static class PriceCalculator
    {
        private static readonly Random _random = new Random();

        public static int CalculatePrice(object itemData, float shopMultiplier)
        {
            int basePrice = 50; // Fallback base price

            // Apply Shop Multiplier (The "Mood" of the shop)
            float price = basePrice * shopMultiplier;

            // Apply Individual Jitter (+/- 20%) to make prices feel organic but fair
            // Range: -0.20 to +0.20
            float jitter = (float)(_random.NextDouble() * 0.4 - 0.2);
            price *= (1.0f + jitter);

            // Round up to the nearest whole number
            int finalPrice = (int)Math.Ceiling(price);

            // Ensure minimum price of 1
            return Math.Max(1, finalPrice);
        }
    }
}

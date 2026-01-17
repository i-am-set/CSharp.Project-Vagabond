using ProjectVagabond.Battle;
using System;

namespace ProjectVagabond.Utils
{
    public static class PriceCalculator
    {
        private static readonly Random _random = new Random();

        // Flat pricing model for the unweighted system
        private const int PRICE_WEAPON = 150;
        private const int PRICE_RELIC = 200;

        public static int CalculatePrice(object itemData, float shopMultiplier)
        {
            int basePrice = 0;

            if (itemData is WeaponData)
            {
                basePrice = PRICE_WEAPON;
            }
            else if (itemData is RelicData)
            {
                basePrice = PRICE_RELIC;
            }
            else
            {
                // Fallback
                basePrice = 50;
            }

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
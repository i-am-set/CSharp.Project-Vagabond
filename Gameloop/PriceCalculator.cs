using ProjectVagabond.Battle;
using System;

namespace ProjectVagabond.Utils
{
    public static class PriceCalculator
    {
        private static readonly Random _random = new Random();

        public static int CalculatePrice(object itemData, float shopMultiplier)
        {
            var global = ServiceLocator.Get<Global>();
            int basePrice = 0;
            float typeMultiplier = 1.0f;
            int rarity = 0;

            if (itemData is WeaponData w) { rarity = w.Rarity; }
            else if (itemData is RelicData r) { rarity = r.Rarity; }
            else if (itemData is ConsumableItemData c)
            {
                // Consumables don't have a rarity field in JSON usually, default to 0 or check logic
                // Assuming ConsumableItemData might get a Rarity field or we treat them as Common/Uncommon
                // For now, let's assume Common (0) unless specified otherwise
                rarity = 0;
                typeMultiplier = global.PriceMultiplier_Consumable;
            }

            switch (rarity)
            {
                case 0: basePrice = global.BasePrice_Common; break;
                case 1: basePrice = global.BasePrice_Uncommon; break;
                case 2: basePrice = global.BasePrice_Rare; break;
                case 3: basePrice = global.BasePrice_Epic; break;
                case 4: basePrice = global.BasePrice_Mythic; break;
                case 5: basePrice = global.BasePrice_Legendary; break;
                default: basePrice = global.BasePrice_Common; break;
            }

            // Apply Shop Multiplier (The "Mood" of the shop)
            float price = basePrice * typeMultiplier * shopMultiplier;

            // Apply Individual Jitter (+/- 75%)
            // Range: -0.75 to +0.75
            float jitter = (float)(_random.NextDouble() * 1.5 - 0.75);
            price *= (1.0f + jitter);

            // Round up to the nearest whole number (Ceiling ensures we don't get 0 for very cheap items)
            int finalPrice = (int)Math.Ceiling(price);

            // Ensure minimum price of 1
            return Math.Max(1, finalPrice);
        }
    }
}
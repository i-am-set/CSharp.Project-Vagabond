using Microsoft.Xna.Framework;
using ProjectVagabond.Battle;
using System;
using System.Collections.Generic;

namespace ProjectVagabond.UI
{
    public class InventoryDataProcessor
    {
        // This class is now largely empty as the item grid logic was removed.
        // Kept for potential future use or tooltip data retrieval if needed.
        private readonly SplitMapInventoryOverlay _overlay;

        public InventoryDataProcessor(SplitMapInventoryOverlay overlay)
        {
            _overlay = overlay;
        }
    }
}
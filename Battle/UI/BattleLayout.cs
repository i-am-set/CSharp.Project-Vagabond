using Microsoft.Xna.Framework;
using ProjectVagabond.Battle;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Battle.UI
{
    /// <summary>
    /// Centralizes all hardcoded positions, dimensions, and layout logic for the battle screen.
    /// </summary>
    public static class BattleLayout
    {
        // --- Screen Constants ---
        public const int DIVIDER_Y = 123;
        public const int ENEMY_SLOT_Y_OFFSET = 12;

        // --- Enemy Layout ---
        public const int ENEMY_AREA_PADDING = 40;
        public const int ENEMY_SPRITE_SIZE_NORMAL = 64;
        public const int ENEMY_SPRITE_SIZE_MAJOR = 96;

        // --- Player Layout ---
        public const float PLAYER_HEART_CENTER_Y = 99f;
        public const float PLAYER_BARS_TOP_Y = 78f;
        public const float PLAYER_NAME_TOP_Y = 111f;

        // FIX: Reduced width to 40 to match enemies
        public const int PLAYER_BAR_WIDTH = 40;

        // --- HUD Constants ---
        public const int STATUS_ICON_SIZE = 5;
        public const int STATUS_ICON_GAP = 1;
        public const int ENEMY_BAR_WIDTH = 40;
        public const int ENEMY_BAR_HEIGHT = 2;

        public static Vector2 GetEnemySlotCenter(int slotIndex)
        {
            int availableWidth = Global.VIRTUAL_WIDTH - (ENEMY_AREA_PADDING * 2);
            int slotWidth = availableWidth / 2;
            return new Vector2(ENEMY_AREA_PADDING + (slotIndex * slotWidth) + (slotWidth / 2), ENEMY_SLOT_Y_OFFSET);
        }

        /// <summary>
        /// Gets the center position used when only one enemy remains.
        /// </summary>
        public static Vector2 GetEnemyCenter()
        {
            return new Vector2(Global.VIRTUAL_WIDTH / 2f, ENEMY_SLOT_Y_OFFSET);
        }

        public static Vector2 GetPlayerSpriteCenter(int slotIndex)
        {
            bool isRightSide = slotIndex == 1;
            float x = isRightSide ? (Global.VIRTUAL_WIDTH * 0.75f) : (Global.VIRTUAL_WIDTH * 0.25f);
            return new Vector2(x, PLAYER_HEART_CENTER_Y);
        }

        public static Vector2 GetPlayerBarPosition(int slotIndex)
        {
            bool isRightSide = slotIndex == 1;
            float centerX = isRightSide ? (Global.VIRTUAL_WIDTH * 0.75f) : (Global.VIRTUAL_WIDTH * 0.25f);
            return new Vector2(centerX - (PLAYER_BAR_WIDTH / 2f), PLAYER_BARS_TOP_Y);
        }
    }
}
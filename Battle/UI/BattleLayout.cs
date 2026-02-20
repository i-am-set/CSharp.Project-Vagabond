using Microsoft.Xna.Framework;
using ProjectVagabond.Battle;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Battle.UI
{
    public static class BattleLayout
    {
        // --- Screen Constants ---
        public const int DIVIDER_Y = 123;
        public const int ENEMY_SLOT_Y_OFFSET = 20;

        // --- Action Menu Layout ---
        public const int ACTION_MENU_WIDTH = 160;
        public const int ACTION_MENU_HEIGHT = 48;
        public const int ACTION_MENU_Y = Global.VIRTUAL_HEIGHT - ACTION_MENU_HEIGHT;

        // --- Enemy Layout ---
        public const int ENEMY_AREA_PADDING = 40;
        public const int ENEMY_SPRITE_SIZE_NORMAL = 64;
        public const int ENEMY_SPRITE_SIZE_MAJOR = 96;

        // --- HUD Constants ---
        public const int STATUS_ICON_SIZE = 5;
        public const int STATUS_ICON_GAP = 1;

        public const float HEALTH_PIXELS_PER_HP = 1.0f;
        public const int MIN_BAR_WIDTH = 0;

        public const int ENEMY_BAR_HEIGHT = 2;

        public static Rectangle GetActionMenuArea(int slotIndex)
        {
            int x = (slotIndex == 0) ? 0 : ACTION_MENU_WIDTH;
            return new Rectangle(x, ACTION_MENU_Y, ACTION_MENU_WIDTH, ACTION_MENU_HEIGHT);
        }

        public static Vector2 GetEnemySlotCenter(int slotIndex)
        {
            int availableWidth = Global.VIRTUAL_WIDTH - (ENEMY_AREA_PADDING * 2);
            int slotWidth = availableWidth / 2;
            return new Vector2(ENEMY_AREA_PADDING + (slotIndex * slotWidth) + (slotWidth / 2), ENEMY_SLOT_Y_OFFSET);
        }

        public static Vector2 GetEnemyCenter()
        {
            return new Vector2(Global.VIRTUAL_WIDTH / 2f, ENEMY_SLOT_Y_OFFSET);
        }

        public static Vector2 GetPlayerSpriteCenter(int slotIndex)
        {
            // Y Alignment:
            // Previous: ACTION_MENU_Y - 19.
            // New Request: Move UP by 4 pixels.
            // New Y: (ACTION_MENU_Y - 19) - 4 = ACTION_MENU_Y - 23.
            float centerY = ACTION_MENU_Y - 23;

            if (slotIndex == 0) // Left Slot
            {
                // Align Left side of sprite with Left side of "BASIC" button.
                // Button Left X = 14. Sprite Center X = 14 + 16 = 30.
                return new Vector2(30, centerY);
            }
            else // Right Slot (Slot 1)
            {
                // Align Right side of sprite with Right side of "CORE/ALT" buttons.
                // Button Group Right X = 307. Sprite Center X = 307 - 16 = 291.
                return new Vector2(291, centerY);
            }
        }
    }
}
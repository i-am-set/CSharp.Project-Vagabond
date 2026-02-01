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
        public const int ENEMY_SLOT_Y_OFFSET = 4;

        // --- Action Menu Layout ---
        public const int ACTION_MENU_WIDTH = 160;
        public const int ACTION_MENU_HEIGHT = 48;
        public const int ACTION_MENU_Y = Global.VIRTUAL_HEIGHT - ACTION_MENU_HEIGHT;

        // --- Enemy Layout ---
        public const int ENEMY_AREA_PADDING = 40;
        public const int ENEMY_SPRITE_SIZE_NORMAL = 64;
        public const int ENEMY_SPRITE_SIZE_MAJOR = 96;

        // --- Player Layout ---
        public const float PLAYER_HEART_CENTER_Y = 104f;
        public const float PLAYER_BARS_TOP_Y = 88f; // Moved down 5px (was 83f)
        public const float PLAYER_NAME_TOP_Y = 116f;

        public const int PLAYER_BAR_WIDTH = 40;

        // --- HUD Constants ---
        public const int STATUS_ICON_SIZE = 5;
        public const int STATUS_ICON_GAP = 1;
        public const int ENEMY_BAR_WIDTH = 40;
        public const int ENEMY_BAR_HEIGHT = 2;

        public static Rectangle GetActionMenuArea(int slotIndex, bool isCentered = false)
        {
            int x;
            if (isCentered)
            {
                x = (Global.VIRTUAL_WIDTH - ACTION_MENU_WIDTH) / 2;
            }
            else
            {
                x = (slotIndex == 0) ? 0 : ACTION_MENU_WIDTH;
            }
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
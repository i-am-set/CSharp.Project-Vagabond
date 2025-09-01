using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace ProjectVagabond.Utils
{
    /// <summary>
    /// A centralized utility for calculating the screen positions of animation anchors.
    /// This ensures that both the AnimationEditorScene and CombatScene use the exact same
    /// coordinate system for hand animations, preventing discrepancies.
    /// </summary>
    public static class AnimationAnchorCalculator
    {
        // Hand Layout Constants
        private const float HAND_IDLE_Y_OFFSET_COMBAT = -50f;
        private const float HAND_IDLE_Y_OFFSET_EDITOR = -40f;
        private const float HAND_IDLE_X_OFFSET_FROM_CENTER = 58f;
        private static readonly Vector2 HAND_CAST_OFFSET = new Vector2(30, -15);
        private static readonly Vector2 HAND_RECOIL_OFFSET = new Vector2(-5, 8);
        private const float HAND_THROW_Y_OFFSET = -10f;
        private const float HAND_OFFSCREEN_Y_OFFSET = 125f;

        /// <summary>
        /// Calculates all anchor points based on the current screen dimensions.
        /// </summary>
        /// <param name="isEditor">A flag to determine whether to use editor-specific or combat-specific vertical offsets.</param>
        /// <param name="screenBottomInVirtualCoords">Outputs the calculated Y-coordinate for the bottom of the screen.</param>
        /// <returns>A dictionary of all calculated anchor points.</returns>
        public static Dictionary<string, Vector2> CalculateAnchors(bool isEditor, out float screenBottomInVirtualCoords)
        {
            var core = ServiceLocator.Get<Core>();
            Rectangle actualScreenVirtualBounds = core.GetActualScreenVirtualBounds();
            float screenCenterX = actualScreenVirtualBounds.Center.X;

            // Anchor Y position calculation to the physical window bottom, transformed into virtual coordinates.
            var windowBottomRight = new Point(core.GraphicsDevice.PresentationParameters.BackBufferWidth, core.GraphicsDevice.PresentationParameters.BackBufferHeight);
            screenBottomInVirtualCoords = Core.TransformMouse(windowBottomRight).Y;

            float idleYOffset = isEditor ? HAND_IDLE_Y_OFFSET_EDITOR : HAND_IDLE_Y_OFFSET_COMBAT;

            var leftHandIdle = new Vector2(screenCenterX - HAND_IDLE_X_OFFSET_FROM_CENTER, screenBottomInVirtualCoords + idleYOffset);
            var rightHandIdle = new Vector2(screenCenterX + HAND_IDLE_X_OFFSET_FROM_CENTER, screenBottomInVirtualCoords + idleYOffset);

            var leftHandCast = leftHandIdle + new Vector2(HAND_CAST_OFFSET.X, HAND_CAST_OFFSET.Y);
            var rightHandCast = rightHandIdle + new Vector2(-HAND_CAST_OFFSET.X, HAND_CAST_OFFSET.Y);

            var leftHandThrow = leftHandCast + new Vector2(0, HAND_THROW_Y_OFFSET);
            var rightHandThrow = rightHandCast + new Vector2(0, HAND_THROW_Y_OFFSET);

            var leftHandOffscreen = new Vector2(leftHandIdle.X, screenBottomInVirtualCoords + HAND_OFFSCREEN_Y_OFFSET);
            var rightHandOffscreen = new Vector2(rightHandIdle.X, screenBottomInVirtualCoords + HAND_OFFSCREEN_Y_OFFSET);

            return new Dictionary<string, Vector2>
            {
                { "LeftHandIdle", leftHandIdle },
                { "RightHandIdle", rightHandIdle },
                { "LeftHandCast", leftHandCast },
                { "RightHandCast", rightHandCast },
                { "LeftHandRecoil", leftHandCast + new Vector2(HAND_RECOIL_OFFSET.X, HAND_RECOIL_OFFSET.Y) },
                { "RightHandRecoil", rightHandCast + new Vector2(-HAND_RECOIL_OFFSET.X, HAND_RECOIL_OFFSET.Y) },
                { "LeftHandThrow", leftHandThrow },
                { "RightHandThrow", rightHandThrow },
                { "LeftHandOffscreen", leftHandOffscreen },
                { "RightHandOffscreen", rightHandOffscreen }
            };
        }
    }
}
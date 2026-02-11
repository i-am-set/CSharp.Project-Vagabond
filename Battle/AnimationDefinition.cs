namespace ProjectVagabond.Battle
{
    /// <summary>
    /// Defines the visual properties and timing for a combat animation.
    /// </summary>
    public class AnimationDefinition
    {
        /// <summary>
        /// Unique identifier for this animation (referenced by MoveData).
        /// </summary>
        public string Id { get; set; } = "";

        /// <summary>
        /// The content path to the texture (e.g., "Sprites/MoveAnimationSpriteSheets/basic_impact").
        /// </summary>
        public string TexturePath { get; set; } = "";

        /// <summary>
        /// Width of a single frame in pixels.
        /// </summary>
        public int FrameWidth { get; set; }

        /// <summary>
        /// Height of a single frame in pixels.
        /// </summary>
        public int FrameHeight { get; set; }

        /// <summary>
        /// Frames per second.
        /// </summary>
        public float FPS { get; set; } = 12f;

        /// <summary>
        /// The frame index (0-based) where the impact/damage logic should trigger.
        /// </summary>
        public int ImpactFrameIndex { get; set; }
    }
}
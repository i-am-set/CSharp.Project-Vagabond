using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ProjectVagabond.Combat
{
    /// <summary>
    /// Defines a keyframe in an animation timeline, representing a specific instruction at a point in time.
    /// </summary>
    public class Keyframe
    {
        /// <summary>
        /// The point in time for this keyframe, represented as a percentage of the timeline's total duration (0.0 to 1.0).
        /// </summary>
        [JsonPropertyName("time")]
        public float Time { get; set; }

        /// <summary>
        /// The type of instruction for this keyframe (e.g., "MoveTo", "PlayAnimation", "SpawnParticle", "TriggerEffects").
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; }

        /// <summary>
        /// A named position for the target to move to (e.g., "CastingPointA", "Idle"). Used with "MoveTo" type.
        /// </summary>
        [JsonPropertyName("position")]
        public string Position { get; set; }

        /// <summary>
        /// The name of a particle effect to spawn. Used with "SpawnParticle" type.
        /// </summary>
        [JsonPropertyName("effect")]
        public string Effect { get; set; }

        /// <summary>
        /// The location to spawn a particle effect (e.g., "SingularityPoint", "Target"). Used with "SpawnParticle" type.
        /// </summary>
        [JsonPropertyName("at")]
        public string At { get; set; }

        /// <summary>
        /// The name of an animation to play on the target (e.g., "cast_start", "cast_release"). Used with "PlayAnimation" type.
        /// </summary>
        [JsonPropertyName("animation")]
        public string AnimationName { get; set; }
    }

    /// <summary>
    /// Represents a single track within an animation timeline, controlling one specific visual element.
    /// </summary>
    public class AnimationTrack
    {
        /// <summary>
        /// The target element this track controls (e.g., "LeftHand", "RightHand", "SpellVFX").
        /// </summary>
        [JsonPropertyName("target")]
        public string Target { get; set; }

        /// <summary>
        /// The list of keyframes that define the behavior of this track over time.
        /// </summary>
        [JsonPropertyName("keyframes")]
        public List<Keyframe> Keyframes { get; set; } = new List<Keyframe>();
    }

    /// <summary>
    /// Represents the complete animation "script" for a combat action, containing all tracks and keyframes.
    /// </summary>
    public class AnimationTimeline
    {
        /// <summary>
        /// The total duration of the animation sequence in seconds.
        /// </summary>
        [JsonPropertyName("duration")]
        public float Duration { get; set; } = 1.0f;

        /// <summary>
        /// The list of tracks that orchestrate the different visual elements of the animation.
        /// </summary>
        [JsonPropertyName("tracks")]
        public List<AnimationTrack> Tracks { get; set; } = new List<AnimationTrack>();
    }
}
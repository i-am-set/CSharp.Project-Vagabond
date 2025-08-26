using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace ProjectVagabond.Combat
{
    /// <summary>
    /// Represents the state of a keyframe within the animation editor.
    /// </summary>
    public enum KeyframeState
    {
        /// <summary>
        /// The keyframe is unchanged from the loaded file.
        /// </summary>
        Unmodified,
        /// <summary>
        /// The keyframe has been newly added and is not yet saved.
        /// </summary>
        Added,
        /// <summary>
        /// The keyframe is marked for deletion upon saving.
        /// </summary>
        Deleted
    }

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
        /// The type of instruction for this keyframe (e.g., "MoveTo", "RotateTo", "ScaleTo", "PlayAnimation", "TriggerEffects", "Wait").
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; }

        /// <summary>
        /// A named position for the target to move to (e.g., "CastingPointA", "Idle"). Used with "MoveTo" type.
        /// This is for legacy animations; new keyframes should use TargetX/TargetY.
        /// </summary>
        [JsonPropertyName("position")]
        public string Position { get; set; }

        /// <summary>
        /// The explicit target X coordinate. If set, this overrides the named 'Position'.
        /// </summary>
        [JsonPropertyName("targetX")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public float? TargetX { get; set; }

        /// <summary>
        /// The explicit target Y coordinate. If set, this overrides the named 'Position'.
        /// </summary>
        [JsonPropertyName("targetY")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public float? TargetY { get; set; }

        /// <summary>
        /// The target rotation in degrees. Used with "RotateTo" type.
        /// </summary>
        [JsonPropertyName("rotation")]
        public float Rotation { get; set; }

        /// <summary>
        /// The target scale multiplier. Used with "ScaleTo" type.
        /// </summary>
        [JsonPropertyName("scale")]
        public float Scale { get; set; } = 1f;

        /// <summary>
        /// The name of the easing function to use for the tween (e.g., "EaseOutCubic"). Used with transform types.
        /// </summary>
        [JsonPropertyName("easing")]
        public string Easing { get; set; } = "EaseOutCubic";

        /// <summary>
        /// The name of an animation to play on the target (e.g., "cast_start", "cast_release"). Used with "PlayAnimation" type.
        /// </summary>
        [JsonPropertyName("animation")]
        public string AnimationName { get; set; }

        /// <summary>
        /// The runtime state of the keyframe in the editor. This is not saved to JSON.
        /// </summary>
        [JsonIgnore]
        public KeyframeState State { get; set; } = KeyframeState.Added;
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

        /// <summary>
        /// Adds a new keyframe or updates an existing one of the same type at the same time.
        /// </summary>
        public void AddOrUpdateKeyframe(Keyframe newKeyframe)
        {
            // Find if a keyframe of the same type exists at nearly the same time
            var existingKeyframe = Keyframes.FirstOrDefault(k =>
                k.Type == newKeyframe.Type &&
                System.Math.Abs(k.Time - newKeyframe.Time) < 0.001f);

            if (existingKeyframe != null)
            {
                // Update the existing keyframe's properties
                existingKeyframe.Position = newKeyframe.Position;
                existingKeyframe.Rotation = newKeyframe.Rotation;
                existingKeyframe.Scale = newKeyframe.Scale;
                existingKeyframe.Easing = newKeyframe.Easing;
                existingKeyframe.AnimationName = newKeyframe.AnimationName;
                existingKeyframe.TargetX = newKeyframe.TargetX;
                existingKeyframe.TargetY = newKeyframe.TargetY;
                // Mark it as added/modified if it was previously unmodified or deleted
                if (existingKeyframe.State != KeyframeState.Added)
                {
                    existingKeyframe.State = KeyframeState.Added;
                }
            }
            else
            {
                // Add the new keyframe
                newKeyframe.State = KeyframeState.Added;
                Keyframes.Add(newKeyframe);
            }

            // Re-sort the list to maintain time order
            Keyframes = Keyframes.OrderBy(k => k.Time).ToList();
        }
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
using Microsoft.Xna.Framework;
using System.Text.Json.Serialization;

namespace ProjectVagabond.Combat
{
    /// <summary>
    /// Defines the rendering order of a hand relative to particle effects.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum RenderLayer
    {
        BehindParticles,
        InFrontOfParticles
    }

    /// <summary>
    /// A data container for the state of a single hand within a pose.
    /// </summary>
    public class HandState
    {
        [JsonPropertyName("position")]
        public Vector2 Position { get; set; }

        [JsonPropertyName("rotation")]
        public float Rotation { get; set; } // In degrees

        [JsonPropertyName("scale")]
        public float Scale { get; set; } = 1f;

        [JsonPropertyName("animation")]
        public string AnimationName { get; set; }

        [JsonPropertyName("renderLayer")]
        public RenderLayer RenderLayer { get; set; } = RenderLayer.InFrontOfParticles;
    }

    /// <summary>
    /// Represents a static, named pose for the player's hands in combat,
    /// including their transforms and any associated particle effects.
    /// Designed to be deserialized from a JSON file.
    /// </summary>
    public class PoseData
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("leftHand")]
        public HandState LeftHand { get; set; } = new HandState();

        [JsonPropertyName("rightHand")]
        public HandState RightHand { get; set; } = new HandState();

        [JsonPropertyName("particleEffectName")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string ParticleEffectName { get; set; }

        [JsonPropertyName("particleAnchor")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public ParticleAnchorType ParticleAnchor { get; set; } = ParticleAnchorType.Nowhere;
    }
}
namespace ProjectVagabond.Battle
{
    /// <summary>
    /// Defines the visual properties and timing for a combat animation.
    /// </summary>
    public class AnimationDefinition
    {
        public string Id { get; set; } = "";
        public string TexturePath { get; set; } = "";
        public int FrameWidth { get; set; }
        public int FrameHeight { get; set; }
        public float FPS { get; set; } = 12f;
        public int ImpactFrameIndex { get; set; }

        // --- PARTICLE SYSTEM FIELDS ---
        public bool IsParticle { get; set; } = false;
        public string ParticleProfile { get; set; } = "";
        public float TotalDuration { get; set; } = 1.0f;
        public float ImpactTime { get; set; } = 0.5f;

        // Granular controls for beam/projectile phases
        public float ExpansionTime { get; set; } = 0.2f;
        public float RetractionTime { get; set; } = 0.2f;
    }
}
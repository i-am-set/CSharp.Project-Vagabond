using System.Collections.Generic;

namespace ProjectVagabond.Particles
{
    /// <summary>
    /// A component that attaches one or more particle emitters to an entity.
    /// This is a runtime component and is not part of the archetype templates.
    /// </summary>
    public class ParticleEmitterComponent : IComponent
    {
        public Dictionary<string, ParticleEmitter> Emitters { get; } = new Dictionary<string, ParticleEmitter>();
    }
}
using Microsoft.Xna.Framework;
using ProjectVagabond;
using System;

namespace ProjectVagabond
{
    /// <summary>
    /// Holds state information for an AI-controlled entity.
    /// </summary>
    public class AIComponent : IComponent, ICloneableComponent
    {
        /// <summary>
        /// The current state of the AI's finite state machine.
        /// </summary>
        public AIState CurrentState { get; set; } = AIState.Idle;

        /// <summary>
        /// A timer used to control how long the AI remains in its current state.
        /// </summary>
        public float StateTimer { get; set; } = 0f;

        public IComponent Clone()
        {
            return (IComponent)this.MemberwiseClone();
        }
    }
}
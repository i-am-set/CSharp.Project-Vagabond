namespace ProjectVagabond.Combat
{
    /// <summary>
    /// Defines the anchor points for a particle effect during a combat action animation.
    /// </summary>
    public enum ParticleAnchorType
    {
        /// <summary>
        /// No particle effect is shown.
        /// </summary>
        Nowhere,
        /// <summary>
        /// The effect originates from the left hand's pivot point.
        /// </summary>
        LeftHand,
        /// <summary>
        /// The effect originates from the right hand's pivot point.
        /// </summary>
        RightHand,
        /// <summary>
        /// Two separate effects originate, one from each hand's pivot point.
        /// </summary>
        BothHands,
        /// <summary>
        /// A single effect originates from the midpoint between the two hands' pivot points.
        /// </summary>
        BetweenHands
    }
}
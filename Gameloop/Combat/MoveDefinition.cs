using System.Collections.Generic;
using ProjectVagabond.Deliveries;

namespace ProjectVagabond.Battle
{
    public class MoveDefinition
    {
        public string Name { get; set; }
        public string AnimationID { get; set; }
        public string CastSoundCue { get; set; }
        public float CastSoundPitchVariance { get; set; }
        public int BasePower { get; set; }
        public float ChargeTime { get; set; }
        public int Weight { get; set; }
        public float Knockback { get; set; }
        public bool TargetSelf { get; set; }
        public bool CanEffectSelf { get; set; }
        public bool TargetClosest { get; set; }
        public float ProjectileTravelTime { get; set; }
        public bool ExecuteOnChargeStart { get; set; }
        public bool RequiresFocus { get; set; }
        public bool ShowProjectileIndicator { get; set; }
        public bool DeliveryImpactMidFlight { get; set; } = true;
        public IDelivery Delivery { get; set; }
        public List<IEffect> Effects { get; set; } = new List<IEffect>();
    }
}
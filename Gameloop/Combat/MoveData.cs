// text/plain
namespace ProjectVagabond.Battle
{
    public class MoveData
    {
        public string ID { get; set; }
        public string Name { get; set; }
        public string AnimationID { get; set; }
        public int BasePower { get; set; }
        public float ChargeTime { get; set; }
        public int Weight { get; set; }
        public float Knockback { get; set; }

        public string DeliveryType { get; set; }
        public float DeliveryRadius { get; set; }
        public float DeliveryWidth { get; set; }
        public float DeliveryLength { get; set; }
        public float DeliveryLifetime { get; set; }
        public float DeliveryTickRate { get; set; }
        public float DeliveryDashDistance { get; set; }

        public string EffectType { get; set; }
        public float EffectArg { get; set; }

        public bool TargetSelf { get; set; }
        public bool CanEffectSelf { get; set; }

        public bool ExecuteOnChargeStart { get; set; }
        public bool RequiresFocus { get; set; }
    }
}
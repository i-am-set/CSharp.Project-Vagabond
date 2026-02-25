using System.Collections.Generic;
using ProjectVagabond.Battle.Abilities;

namespace ProjectVagabond.Battle
{
    public class MoveData
    {
        public string MoveID { get; set; }
        public string MoveName { get; set; }
        public string ActionPhrase { get; set; }
        public string Description { get; set; }
        public string Flavor { get; set; }
        public int Power { get; set; }
        public MoveType MoveType { get; set; }
        public ImpactType ImpactType { get; set; }
        public OffensiveStatType OffensiveStat { get; set; }
        public bool MakesContact { get; set; }
        public TargetType Target { get; set; }
        public int Accuracy { get; set; }
        public int Priority { get; set; }
        public List<string> Tags { get; set; } = new List<string>();
        public List<string> SerializedTags { get; set; }
        public string AnimationId { get; set; }
        public bool IsAnimationCentralized { get; set; }
        public Dictionary<string, string> Effects { get; set; } = new Dictionary<string, string>();

        public int Cooldown { get; set; }

        public List<IAbility> Abilities { get; set; } = new List<IAbility>();
        public bool AffectsUserHP { get; set; }
        public bool AffectsTargetHP { get; set; }
    }
}
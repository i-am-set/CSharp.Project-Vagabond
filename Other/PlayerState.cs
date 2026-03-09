using ProjectVagabond;
using ProjectVagabond.Battle;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond
{
    public class PlayerState
    {
        public List<WizardCat> Party { get; set; } = new List<WizardCat>();
        public HashSet<string> PastMemberIds { get; set; } = new HashSet<string>();
        public WizardCat Leader => Party.Count > 0 ? Party[0] : null;

        public int MaxHP { get => Leader?.MaxHP ?? 100; }
        public int CurrentHP { get => Leader?.CurrentHP ?? 100; set { if (Leader != null) Leader.CurrentHP = value; } }
        public int Power { get => Leader?.Power ?? 10; set { if (Leader != null) Leader.Power = value; } }
        public int Intelligence { get => Leader?.Intelligence ?? 10; set { if (Leader != null) Leader.Intelligence = value; } }
        public int Tenacity { get => Leader?.Tenacity ?? 10; set { if (Leader != null) Leader.Tenacity = value; } }
        public int Agility { get => Leader?.Agility ?? 10; set { if (Leader != null) Leader.Agility = value; } }

        public int PortraitIndex { get => Leader?.PortraitIndex ?? 0; set { if (Leader != null) Leader.PortraitIndex = value; } }

        public int Gold { get; set; }

        public PlayerState() { }

        public bool AddWizardCat(WizardCat member)
        {
            if (Party.Count >= 4) return false;
            if (Party.Any(m => m.Name.Equals(member.Name, StringComparison.OrdinalIgnoreCase))) return false;

            Party.Add(member);

            var kvp = GameDataCache.WizardCats.FirstOrDefault(x => x.Value.Name == member.Name);
            if (!string.IsNullOrEmpty(kvp.Key)) PastMemberIds.Add(kvp.Key);

            return true;
        }

        public int GetBaseStat(WizardCat member, string statName)
        {
            if (member == null) return 0;
            switch (statName.ToLowerInvariant())
            {
                case "power": return member.Power;
                case "intelligence": return member.Intelligence;
                case "tenacity": return member.Tenacity;
                case "agility": return member.Agility;
                case "maxhp": return member.MaxHP;
                default: return 0;
            }
        }

        public int GetEffectiveStat(WizardCat member, string statName)
        {
            return GetBaseStat(member, statName);
        }
    }
}
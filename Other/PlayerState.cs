using ProjectVagabond;
using ProjectVagabond.Battle;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond
{
    public class PlayerState
    {
        public List<PartyMember> Party { get; set; } = new List<PartyMember>();
        public HashSet<string> PastMemberIds { get; set; } = new HashSet<string>();
        public PartyMember Leader => Party.Count > 0 ? Party[0] : null;

        private int _coin = 100;
        public int Coin
        {
            get => _coin;
            set => _coin = Math.Max(0, value);
        }

        public int MaxHP { get => Leader?.MaxHP ?? 100; set { if (Leader != null) Leader.MaxHP = value; } }
        public int CurrentHP { get => Leader?.CurrentHP ?? 100; set { if (Leader != null) Leader.CurrentHP = value; } }
        public int MaxMana { get => Leader?.MaxMana ?? 100; set { if (Leader != null) Leader.MaxMana = value; } }
        public int CurrentMana { get => Leader?.CurrentMana ?? 100; set { if (Leader != null) Leader.CurrentMana = value; } }
        public int Strength { get => Leader?.Strength ?? 10; set { if (Leader != null) Leader.Strength = value; } }
        public int Intelligence { get => Leader?.Intelligence ?? 10; set { if (Leader != null) Leader.Intelligence = value; } }
        public int Tenacity { get => Leader?.Tenacity ?? 10; set { if (Leader != null) Leader.Tenacity = value; } }
        public int Agility { get => Leader?.Agility ?? 10; set { if (Leader != null) Leader.Agility = value; } }

        public string DefaultStrikeMoveID { get => Leader?.DefaultStrikeMoveID ?? ""; set { if (Leader != null) Leader.DefaultStrikeMoveID = value; } }
        public int PortraitIndex { get => Leader?.PortraitIndex ?? 0; set { if (Leader != null) Leader.PortraitIndex = value; } }

        public MoveEntry?[] Spells { get => Leader?.Spells ?? new MoveEntry?[4]; set { if (Leader != null) Leader.Spells = value; } }
        public List<MoveEntry> Actions { get => Leader?.Actions ?? new List<MoveEntry>(); set { if (Leader != null) Leader.Actions = value; } }

        public PlayerState() { }

        public bool AddPartyMember(PartyMember member)
        {
            if (Party.Count >= 4) return false;
            if (Party.Any(m => m.Name.Equals(member.Name, StringComparison.OrdinalIgnoreCase))) return false;

            Party.Add(member);

            var kvp = BattleDataCache.PartyMembers.FirstOrDefault(x => x.Value.Name == member.Name);
            if (!string.IsNullOrEmpty(kvp.Key)) PastMemberIds.Add(kvp.Key);

            return true;
        }

        public int GetBaseStat(PartyMember member, string statName)
        {
            if (member == null) return 0;
            switch (statName.ToLowerInvariant())
            {
                case "strength": return member.Strength;
                case "intelligence": return member.Intelligence;
                case "tenacity": return member.Tenacity;
                case "agility": return member.Agility;
                case "maxhp": return member.MaxHP;
                case "maxmana": return member.MaxMana;
                default: return 0;
            }
        }

        public int GetEffectiveStat(PartyMember member, string statName)
        {
            // Relics removed, effective stat is just base stat
            return GetBaseStat(member, statName);
        }

        public void AddMove(string moveId, PartyMember member = null)
        {
            var target = member ?? Leader;
            if (target == null || !BattleDataCache.Moves.TryGetValue(moveId, out var moveData)) return;

            if (moveData.MoveType == MoveType.Spell)
            {
                for (int i = 0; i < target.Spells.Length; i++) if (target.Spells[i]?.MoveID == moveId) return;
                for (int i = 0; i < target.Spells.Length; i++)
                {
                    if (target.Spells[i] == null)
                    {
                        target.Spells[i] = new MoveEntry(moveId, 0);
                        return;
                    }
                }
            }
            else if (moveData.MoveType == MoveType.Action)
            {
                if (!target.Actions.Any(m => m.MoveID == moveId)) target.Actions.Add(new MoveEntry(moveId, 0));
            }
        }

        public void RemoveMove(string moveId, PartyMember member = null)
        {
            var target = member ?? Leader;
            if (target == null) return;

            for (int i = 0; i < target.Spells.Length; i++)
            {
                if (target.Spells[i]?.MoveID == moveId)
                {
                    target.Spells[i] = null;
                    return;
                }
            }
            var actionIndex = target.Actions.FindIndex(m => m.MoveID == moveId);
            if (actionIndex != -1) target.Actions.RemoveAt(actionIndex);
        }
    }
}
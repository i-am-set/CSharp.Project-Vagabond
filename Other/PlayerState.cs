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

        public int MaxHP { get => Leader?.MaxHP ?? 100; set { if (Leader != null) Leader.MaxHP = value; } }
        public int CurrentHP { get => Leader?.CurrentHP ?? 100; set { if (Leader != null) Leader.CurrentHP = value; } }
        public int Strength { get => Leader?.Strength ?? 10; set { if (Leader != null) Leader.Strength = value; } }
        public int Intelligence { get => Leader?.Intelligence ?? 10; set { if (Leader != null) Leader.Intelligence = value; } }
        public int Tenacity { get => Leader?.Tenacity ?? 10; set { if (Leader != null) Leader.Tenacity = value; } }
        public int Agility { get => Leader?.Agility ?? 10; set { if (Leader != null) Leader.Agility = value; } }

        public int PortraitIndex { get => Leader?.PortraitIndex ?? 0; set { if (Leader != null) Leader.PortraitIndex = value; } }

        // Shifted to strict 2-move system (Strike and Alt)
        public MoveEntry? StrikeMove { get => Leader?.StrikeMove; set { if (Leader != null) Leader.StrikeMove = value; } }
        public MoveEntry? AltMove { get => Leader?.AltMove; set { if (Leader != null) Leader.AltMove = value; } }

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
                default: return 0;
            }
        }

        public int GetEffectiveStat(PartyMember member, string statName)
        {
            return GetBaseStat(member, statName);
        }

        public void AddMove(string moveId, PartyMember member = null)
        {
            var target = member ?? Leader;
            if (target == null || !BattleDataCache.Moves.TryGetValue(moveId, out var moveData)) return;

            var compiledMove = new CompiledMove(moveData, new List<ModifierToken>());

            if (target.StrikeMove == null)
            {
                target.StrikeMove = new MoveEntry(compiledMove, 0);
            }
            else
            {
                target.AltMove = new MoveEntry(compiledMove, 0);
            }
        }

        public void RemoveMove(string moveId, PartyMember member = null)
        {
            var target = member ?? Leader;
            if (target == null) return;

            if (target.StrikeMove?.CompiledMove.BaseTemplate.MoveID == moveId) target.StrikeMove = null;
            if (target.AltMove?.CompiledMove.BaseTemplate.MoveID == moveId) target.AltMove = null;
        }
    }
}
using Microsoft.Xna.Framework;
using ProjectVagabond.Battle;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ProjectVagabond.Battle
{
    public static class PartyMemberFactory
    {
        private static readonly Random _rng = new Random();

        public static PartyMember CreateMember(string memberId)
        {
            if (!BattleDataCache.PartyMembers.TryGetValue(memberId, out var data))
            {
                Debug.WriteLine($"[PartyMemberFactory] Error: Member ID '{memberId}' not found in cache.");
                return null;
            }
            var member = new PartyMember
            {
                Name = data.Name,
                MaxHP = data.MaxHP,
                CurrentHP = data.MaxHP,
                Strength = data.Strength,
                Intelligence = data.Intelligence,
                Tenacity = data.Tenacity,
                Agility = data.Agility,
                PortraitIndex = int.TryParse(data.MemberID, out int pid) ? pid : 0
            };

            if (data.PassiveAbilityPool != null && data.PassiveAbilityPool.Any())
            {
                var selectedPassive = data.PassiveAbilityPool[_rng.Next(data.PassiveAbilityPool.Count)];
                member.IntrinsicAbilities = new Dictionary<string, string>(selectedPassive);
                Debug.WriteLine($"[PartyMemberFactory] {member.Name} generated with passive: {string.Join(", ", member.IntrinsicAbilities.Keys)}");
            }

            if (!string.IsNullOrEmpty(data.BasicMoveId) && BattleDataCache.Moves.ContainsKey(data.BasicMoveId))
            {
                member.BasicMove = new MoveEntry(data.BasicMoveId, 0);
            }

            AssignRandomMoveFromPool(data.CoreMovePool, out var coreMove);
            if (coreMove != null)
            {
                member.CoreMove = coreMove;
            }

            AssignRandomMoveFromPool(data.AltMovePool, out var altMove);
            if (altMove != null)
            {
                member.AltMove = altMove;
            }

            return member;
        }

        private static void AssignRandomMoveFromPool(List<MovePoolEntry> pool, out MoveEntry assignedMove)
        {
            assignedMove = null;
            if (pool == null || !pool.Any()) return;

            var validEntries = pool.Where(p => !p.IsFiller &&
                                               BattleDataCache.Moves.TryGetValue(p.MoveId, out var moveData) &&
                                               !moveData.IsRare).ToList();

            if (validEntries.Any())
            {
                var selected = validEntries[_rng.Next(validEntries.Count)];
                assignedMove = new MoveEntry(selected.MoveId, 0);
            }
        }
    }
}
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
            }

            if (!string.IsNullOrEmpty(data.BasicMoveId) && BattleDataCache.Moves.ContainsKey(data.BasicMoveId))
            {
                member.BasicMove = new MoveEntry(data.BasicMoveId, 0);
            }

            if (!string.IsNullOrEmpty(data.StartSpellSlot1MoveId) && BattleDataCache.Moves.ContainsKey(data.StartSpellSlot1MoveId))
            {
                member.Spell1 = new MoveEntry(data.StartSpellSlot1MoveId, 0);
                member.KnownMovesHistory.Add(data.StartSpellSlot1MoveId);
            }

            if (!string.IsNullOrEmpty(data.StartSpellSlot2MoveId) && BattleDataCache.Moves.ContainsKey(data.StartSpellSlot2MoveId))
            {
                member.Spell2 = new MoveEntry(data.StartSpellSlot2MoveId, 0);
                member.KnownMovesHistory.Add(data.StartSpellSlot2MoveId);
            }

            if (!string.IsNullOrEmpty(data.StartSpellSlot3MoveId) && BattleDataCache.Moves.ContainsKey(data.StartSpellSlot3MoveId))
            {
                member.Spell3 = new MoveEntry(data.StartSpellSlot3MoveId, 0);
                member.KnownMovesHistory.Add(data.StartSpellSlot3MoveId);
            }

            return member;
        }
    }
}
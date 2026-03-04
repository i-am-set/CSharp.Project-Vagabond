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
                MaxHP = data.MaxHP * 2,
                CurrentHP = data.MaxHP * 2,
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

            if (!string.IsNullOrEmpty(data.BasicMoveId) && BattleDataCache.Moves.TryGetValue(data.BasicMoveId, out var bMove))
            {
                member.BasicMove = new MoveEntry(new CompiledMove(bMove, new List<ModifierToken>()), 0);
            }

            if (!string.IsNullOrEmpty(data.StartSpellSlot1MoveId) && BattleDataCache.Moves.TryGetValue(data.StartSpellSlot1MoveId, out var s1Move))
            {
                member.Spell1 = new MoveEntry(new CompiledMove(s1Move, new List<ModifierToken>()), 0);
                member.KnownMovesHistory.Add(data.StartSpellSlot1MoveId);
            }

            if (!string.IsNullOrEmpty(data.StartSpellSlot2MoveId) && BattleDataCache.Moves.TryGetValue(data.StartSpellSlot2MoveId, out var s2Move))
            {
                member.Spell2 = new MoveEntry(new CompiledMove(s2Move, new List<ModifierToken>()), 0);
                member.KnownMovesHistory.Add(data.StartSpellSlot2MoveId);
            }

            if (!string.IsNullOrEmpty(data.StartSpellSlot3MoveId) && BattleDataCache.Moves.TryGetValue(data.StartSpellSlot3MoveId, out var s3Move))
            {
                member.Spell3 = new MoveEntry(new CompiledMove(s3Move, new List<ModifierToken>()), 0);
                member.KnownMovesHistory.Add(data.StartSpellSlot3MoveId);
            }

            return member;
        }
    }
}
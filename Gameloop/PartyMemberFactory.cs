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

            if (!string.IsNullOrEmpty(data.StrikeMoveId) && BattleDataCache.Moves.TryGetValue(data.StrikeMoveId, out var strikeMove))
            {
                member.StrikeMove = new MoveEntry(new CompiledMove(strikeMove, new List<ModifierToken>()), 0);
                member.KnownMovesHistory.Add(data.StrikeMoveId);
            }

            if (!string.IsNullOrEmpty(data.AltMoveId) && BattleDataCache.Moves.TryGetValue(data.AltMoveId, out var altMove))
            {
                member.AltMove = new MoveEntry(new CompiledMove(altMove, new List<ModifierToken>()), 0);
                member.KnownMovesHistory.Add(data.AltMoveId);
            }

            return member;
        }
    }
}
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
                MaxMana = data.MaxMana,
                CurrentMana = data.MaxMana,
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

            // Assign Attack Move
            if (data.AttackMovePool != null && data.AttackMovePool.Any())
            {
                string moveId = data.AttackMovePool[_rng.Next(data.AttackMovePool.Count)];
                if (BattleDataCache.Moves.ContainsKey(moveId))
                {
                    member.AttackMove = new MoveEntry(moveId, 0);
                }
            }

            // Assign Special Move
            if (data.SpecialMovePool != null && data.SpecialMovePool.Any())
            {
                string moveId = data.SpecialMovePool[_rng.Next(data.SpecialMovePool.Count)];
                if (BattleDataCache.Moves.ContainsKey(moveId))
                {
                    member.SpecialMove = new MoveEntry(moveId, 0);
                }
            }

            return member;
        }
    }
}
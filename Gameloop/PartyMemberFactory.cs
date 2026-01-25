using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Progression;
using ProjectVagabond.Scenes;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

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
                WeaknessElementIDs = new List<int>(data.WeaknessElementIDs),
                ResistanceElementIDs = new List<int>(data.ResistanceElementIDs),
                DefaultStrikeMoveID = data.DefaultStrikeMoveID,
                PortraitIndex = int.TryParse(data.MemberID, out int pid) ? pid : 0
            };

            // --- Select Intrinsic Passive ---
            if (data.PassiveAbilityPool != null && data.PassiveAbilityPool.Any())
            {
                var selectedPassive = data.PassiveAbilityPool[_rng.Next(data.PassiveAbilityPool.Count)];
                member.IntrinsicAbilities = new Dictionary<string, string>(selectedPassive);
                Debug.WriteLine($"[PartyMemberFactory] {member.Name} generated with passive: {string.Join(", ", member.IntrinsicAbilities.Keys)}");
            }

            // --- Populate Combat Slots ---
            AssignMoveToSlot(member, 0, data.Slot1MovePool);
            AssignMoveToSlot(member, 1, data.Slot2MovePool);
            AssignMoveToSlot(member, 2, data.Slot3MovePool);
            AssignMoveToSlot(member, 3, data.Slot4MovePool);

            return member;
        }

        private static void AssignMoveToSlot(PartyMember member, int slotIndex, List<string> pool)
        {
            if (pool != null && pool.Any())
            {
                // Pick a random move from the pool for this slot
                string moveId = pool[_rng.Next(pool.Count)];

                if (BattleDataCache.Moves.ContainsKey(moveId))
                {
                    member.Spells[slotIndex] = new MoveEntry(moveId, 0);
                }
                else
                {
                    Debug.WriteLine($"[PartyMemberFactory] Warning: Move ID '{moveId}' defined in pool for {member.Name} not found in Moves cache.");
                }
            }
            else
            {
                // Explicitly null the slot if the pool is empty
                member.Spells[slotIndex] = null;
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ProjectVagabond.Battle;

namespace ProjectVagabond
{
    public static class PartyMemberFactory
    {
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
                ArchetypeId = data.ArchetypeId,
                Level = 1,
                MaxHP = data.MaxHP,
                CurrentHP = data.MaxHP,
                MaxMana = data.MaxMana,
                CurrentMana = data.MaxMana,
                Strength = data.Strength,
                Intelligence = data.Intelligence,
                Tenacity = data.Tenacity,
                Agility = data.Agility,
                DefensiveElementIDs = new List<int>(data.DefensiveElementIDs),
                DefaultStrikeMoveID = data.DefaultStrikeMoveID
            };

            // Load Spells
            foreach (var moveId in data.StartingSpells)
            {
                if (BattleDataCache.Moves.ContainsKey(moveId))
                {
                    member.Spells.Add(new MoveEntry(moveId, 0));
                }
            }

            // Load Actions
            foreach (var moveId in data.StartingActions)
            {
                if (BattleDataCache.Moves.ContainsKey(moveId))
                {
                    member.Actions.Add(new MoveEntry(moveId, 0));
                }
            }

            // Auto-Equip Spells (First 4)
            for (int i = 0; i < 4 && i < member.Spells.Count; i++)
            {
                member.EquippedSpells[i] = member.Spells[i];
            }

            // Handle Equipment (Weapons/Armor)
            // Note: This factory creates the member, but adding the items to the SHARED inventory
            // must be done by the caller (GameState) to ensure they exist in the pool.
            // However, we can set the *Equipped* ID here.
            foreach (var kvp in data.StartingEquipment)
            {
                string itemId = kvp.Key;
                if (BattleDataCache.Weapons.ContainsKey(itemId))
                {
                    member.EquippedWeaponId = itemId;
                }
                else if (BattleDataCache.Armors.ContainsKey(itemId))
                {
                    member.EquippedArmorId = itemId;
                }
            }

            return member;
        }
    }
}
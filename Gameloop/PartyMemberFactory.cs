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
                DefaultStrikeMoveID = data.DefaultStrikeMoveID,
                PortraitIndex = int.TryParse(data.MemberID, out int pid) ? pid : 0
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

            // Auto-Equip Weapons
            if (data.StartingWeapons.Any())
            {
                string weaponId = data.StartingWeapons.First().Key;
                if (BattleDataCache.Weapons.ContainsKey(weaponId))
                {
                    member.EquippedWeaponId = weaponId;
                }
            }

            // Auto-Equip Armor
            if (data.StartingArmor.Any())
            {
                string armorId = data.StartingArmor.First().Key;
                if (BattleDataCache.Armors.ContainsKey(armorId))
                {
                    member.EquippedArmorId = armorId;
                }
            }

            // Auto-Equip Relic (Only 1 now)
            if (data.StartingRelics.Any())
            {
                string relicId = data.StartingRelics.First().Key;
                if (BattleDataCache.Relics.ContainsKey(relicId))
                {
                    member.EquippedRelicId = relicId;
                }
            }

            return member;
        }
    }
}
using Microsoft.Xna.Framework;
using ProjectVagabond.Battle;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ProjectVagabond
{
    public class PlayerState
    {
        // --- PARTY SYSTEM ---
        public List<PartyMember> Party { get; set; } = new List<PartyMember>();

        // Helper to get the main character (Avatar)
        public PartyMember Leader => Party.Count > 0 ? Party[0] : null;

        // --- SHARED INVENTORY (Team Shared) ---
        public Dictionary<string, int> Weapons { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> Armors { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> Relics { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> Consumables { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> MiscItems { get; set; } = new Dictionary<string, int>();

        // --- LEGACY ACCESSORS (Redirect to Leader for backward compatibility) ---
        public int Level { get => Leader?.Level ?? 1; set { if (Leader != null) Leader.Level = value; } }
        public int MaxHP { get => Leader?.MaxHP ?? 100; set { if (Leader != null) Leader.MaxHP = value; } }
        public int CurrentHP { get => Leader?.CurrentHP ?? 100; set { if (Leader != null) Leader.CurrentHP = value; } }
        public int MaxMana { get => Leader?.MaxMana ?? 100; set { if (Leader != null) Leader.MaxMana = value; } }
        public int CurrentMana { get => Leader?.CurrentMana ?? 100; set { if (Leader != null) Leader.CurrentMana = value; } }
        public int Strength { get => Leader?.Strength ?? 10; set { if (Leader != null) Leader.Strength = value; } }
        public int Intelligence { get => Leader?.Intelligence ?? 10; set { if (Leader != null) Leader.Intelligence = value; } }
        public int Tenacity { get => Leader?.Tenacity ?? 10; set { if (Leader != null) Leader.Tenacity = value; } }
        public int Agility { get => Leader?.Agility ?? 10; set { if (Leader != null) Leader.Agility = value; } }
        public List<int> DefensiveElementIDs { get => Leader?.DefensiveElementIDs ?? new List<int>(); set { if (Leader != null) Leader.DefensiveElementIDs = value; } }
        public string DefaultStrikeMoveID { get => Leader?.DefaultStrikeMoveID ?? ""; set { if (Leader != null) Leader.DefaultStrikeMoveID = value; } }
        public int PortraitIndex { get => Leader?.PortraitIndex ?? 0; set { if (Leader != null) Leader.PortraitIndex = value; } }

        public string? EquippedWeaponId { get => Leader?.EquippedWeaponId; set { if (Leader != null) Leader.EquippedWeaponId = value; } }
        public string? EquippedArmorId { get => Leader?.EquippedArmorId; set { if (Leader != null) Leader.EquippedArmorId = value; } }
        public string?[] EquippedRelics { get => Leader?.EquippedRelics ?? new string?[3]; set { if (Leader != null) Leader.EquippedRelics = value; } }
        public MoveEntry?[] EquippedSpells { get => Leader?.EquippedSpells ?? new MoveEntry?[4]; set { if (Leader != null) Leader.EquippedSpells = value; } }
        public List<MoveEntry> Spells { get => Leader?.Spells ?? new List<MoveEntry>(); set { if (Leader != null) Leader.Spells = value; } }
        public List<MoveEntry> Actions { get => Leader?.Actions ?? new List<MoveEntry>(); set { if (Leader != null) Leader.Actions = value; } }

        public PlayerState()
        {
            // Ensure there is always at least one member (the leader)
            Party.Add(new PartyMember { Name = "Player" });
        }

        // --- PARTY MANAGEMENT ---

        /// <summary>
        /// Attempts to add a new member to the party.
        /// Checks for duplicates and party size limits.
        /// </summary>
        /// <param name="member">The member to add.</param>
        /// <returns>True if added successfully, False otherwise.</returns>
        public bool AddPartyMember(PartyMember member)
        {
            if (Party.Count >= 4)
            {
                Debug.WriteLine("[error] Error: Cannot add member. Party is full (Max 4).");
                return false;
            }

            // Check for duplicate by Name (assuming Name is unique for party members)
            if (Party.Any(m => m.Name.Equals(member.Name, StringComparison.OrdinalIgnoreCase)))
            {
                Debug.WriteLine($"[error] Error: Cannot add member. '{member.Name}' is already in the party.");
                return false;
            }

            Party.Add(member);
            return true;
        }

        // --- STAT CALCULATIONS ---

        // Overload for backward compatibility (defaults to Leader)
        public int GetBaseStat(string statName) => GetBaseStat(Leader, statName);

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
                case "level": return member.Level;
                default: return 0;
            }
        }

        // Overload for backward compatibility (defaults to Leader)
        public int GetEffectiveStat(string statName) => GetEffectiveStat(Leader, statName);

        public int GetEffectiveStat(PartyMember member, string statName)
        {
            if (member == null) return 0;
            int baseValue = GetBaseStat(member, statName);
            int bonus = 0;

            // 1. Relic Bonuses
            foreach (var relicId in member.EquippedRelics)
            {
                if (!string.IsNullOrEmpty(relicId) && BattleDataCache.Relics.TryGetValue(relicId, out var relic))
                {
                    if (relic.StatModifiers.TryGetValue(statName, out int mod)) bonus += mod;
                }
            }

            // 2. Weapon Bonuses
            if (!string.IsNullOrEmpty(member.EquippedWeaponId) && BattleDataCache.Weapons.TryGetValue(member.EquippedWeaponId, out var weapon))
            {
                if (weapon.StatModifiers.TryGetValue(statName, out int mod)) bonus += mod;
            }

            // 3. Armor Bonuses
            if (!string.IsNullOrEmpty(member.EquippedArmorId) && BattleDataCache.Armors.TryGetValue(member.EquippedArmorId, out var armor))
            {
                if (armor.StatModifiers.TryGetValue(statName, out int mod)) bonus += mod;
            }

            return Math.Max(1, baseValue + bonus);
        }

        // --- INVENTORY MANAGEMENT (Shared) ---
        public void AddWeapon(string weaponId, int quantity = 1)
        {
            if (Weapons.ContainsKey(weaponId)) Weapons[weaponId] += quantity;
            else Weapons[weaponId] = quantity;
        }

        public void RemoveWeapon(string weaponId, int quantity = 1)
        {
            if (Weapons.TryGetValue(weaponId, out int current))
            {
                Weapons[weaponId] = Math.Max(0, current - quantity);
                if (Weapons[weaponId] == 0) Weapons.Remove(weaponId);

                // Check unequip for ALL party members
                foreach (var member in Party)
                {
                    if (member.EquippedWeaponId == weaponId && Weapons.GetValueOrDefault(weaponId) == 0)
                        member.EquippedWeaponId = null;
                }
            }
        }

        public void AddArmor(string armorId, int quantity = 1)
        {
            if (Armors.ContainsKey(armorId)) Armors[armorId] += quantity;
            else Armors[armorId] = quantity;
        }

        public void RemoveArmor(string armorId, int quantity = 1)
        {
            if (Armors.TryGetValue(armorId, out int current))
            {
                Armors[armorId] = Math.Max(0, current - quantity);
                if (Armors[armorId] == 0) Armors.Remove(armorId);

                foreach (var member in Party)
                {
                    if (member.EquippedArmorId == armorId && Armors.GetValueOrDefault(armorId) == 0)
                        member.EquippedArmorId = null;
                }
            }
        }

        public void AddRelic(string relicId, int quantity = 1)
        {
            if (Relics.ContainsKey(relicId)) Relics[relicId] += quantity;
            else Relics[relicId] = quantity;
        }

        public void RemoveRelic(string relicId, int quantity = 1)
        {
            if (Relics.TryGetValue(relicId, out int current))
            {
                Relics[relicId] = Math.Max(0, current - quantity);
                if (Relics[relicId] == 0) Relics.Remove(relicId);
            }
        }

        public void AddConsumable(string itemId, int quantity = 1)
        {
            if (Consumables.ContainsKey(itemId)) Consumables[itemId] += quantity;
            else Consumables[itemId] = quantity;
        }

        public bool RemoveConsumable(string itemId, int quantity = 1)
        {
            if (Consumables.TryGetValue(itemId, out int current) && current >= quantity)
            {
                Consumables[itemId] -= quantity;
                if (Consumables[itemId] <= 0) Consumables.Remove(itemId);
                return true;
            }
            return false;
        }

        public void AddMiscItem(string itemId, int quantity = 1)
        {
            if (MiscItems.ContainsKey(itemId)) MiscItems[itemId] += quantity;
            else MiscItems[itemId] = quantity;
        }

        public bool RemoveMiscItem(string itemId, int quantity = 1)
        {
            if (MiscItems.TryGetValue(itemId, out int current) && current >= quantity)
            {
                MiscItems[itemId] -= quantity;
                if (MiscItems[itemId] <= 0) MiscItems.Remove(itemId);
                return true;
            }
            return false;
        }

        // --- MOVE MANAGEMENT (Target specific member) ---
        public void AddMove(string moveId, PartyMember member = null)
        {
            var target = member ?? Leader;
            if (target == null || !BattleDataCache.Moves.TryGetValue(moveId, out var moveData)) return;

            if (moveData.MoveType == MoveType.Spell)
            {
                if (!target.Spells.Any(m => m.MoveID == moveId)) target.Spells.Add(new MoveEntry(moveId, 0));
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

            var spellIndex = target.Spells.FindIndex(m => m.MoveID == moveId);
            if (spellIndex != -1)
            {
                var entry = target.Spells[spellIndex];
                target.Spells.RemoveAt(spellIndex);
                for (int i = 0; i < target.EquippedSpells.Length; i++)
                {
                    if (target.EquippedSpells[i] == entry) target.EquippedSpells[i] = null;
                }
                return;
            }

            var actionIndex = target.Actions.FindIndex(m => m.MoveID == moveId);
            if (actionIndex != -1) target.Actions.RemoveAt(actionIndex);
        }
    }
}
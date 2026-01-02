using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Dice;
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

namespace ProjectVagabond
{
    public class PlayerState
    {
        // --- PARTY SYSTEM ---
        public List<PartyMember> Party { get; set; } = new List<PartyMember>();

        // Track IDs of all members who have ever been in the party to prevent duplicates
        public HashSet<string> PastMemberIds { get; set; } = new HashSet<string>();

        // Helper to get the main character (Avatar)
        public PartyMember Leader => Party.Count > 0 ? Party[0] : null;

        // --- ECONOMY ---
        private int _coin = 100;
        public int Coin
        {
            get => _coin;
            set => _coin = Math.Max(0, value); // Universal rule: Coin cannot be negative
        }

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

        public List<int> WeaknessElementIDs { get => Leader?.WeaknessElementIDs ?? new List<int>(); set { if (Leader != null) Leader.WeaknessElementIDs = value; } }
        public List<int> ResistanceElementIDs { get => Leader?.ResistanceElementIDs ?? new List<int>(); set { if (Leader != null) Leader.ResistanceElementIDs = value; } }

        public string DefaultStrikeMoveID { get => Leader?.DefaultStrikeMoveID ?? ""; set { if (Leader != null) Leader.DefaultStrikeMoveID = value; } }
        public int PortraitIndex { get => Leader?.PortraitIndex ?? 0; set { if (Leader != null) Leader.PortraitIndex = value; } }

        public string? EquippedWeaponId { get => Leader?.EquippedWeaponId; set { if (Leader != null) Leader.EquippedWeaponId = value; } }
        public string? EquippedArmorId { get => Leader?.EquippedArmorId; set { if (Leader != null) Leader.EquippedArmorId = value; } }
        public string? EquippedRelicId { get => Leader?.EquippedRelicId; set { if (Leader != null) Leader.EquippedRelicId = value; } }

        public MoveEntry?[] Spells { get => Leader?.Spells ?? new MoveEntry?[4]; set { if (Leader != null) Leader.Spells = value; } }
        public List<MoveEntry> Actions { get => Leader?.Actions ?? new List<MoveEntry>(); set { if (Leader != null) Leader.Actions = value; } }

        public PlayerState()
        {
            // Ensure there is always at least one member (the leader)
            // Note: The actual leader is loaded via InitializeWorld in GameState, this is just a constructor safety.
        }

        // --- PARTY MANAGEMENT ---

        public bool AddPartyMember(PartyMember member)
        {
            if (Party.Count >= 4)
            {
                Debug.WriteLine("[error] Error: Cannot add member. Party is full (Max 4).");
                return false;
            }

            if (Party.Any(m => m.Name.Equals(member.Name, StringComparison.OrdinalIgnoreCase)))
            {
                Debug.WriteLine($"[error] Error: Cannot add member. '{member.Name}' is already in the party.");
                return false;
            }

            Party.Add(member);

            // Track that this member has been recruited (using Name as ID for now, or MemberID if available)
            // Assuming PartyMemberFactory sets the name correctly.
            // Ideally we'd store the ID, but Name is unique enough for this scope.
            // We will try to find the ID from the cache to be safe.
            var kvp = BattleDataCache.PartyMembers.FirstOrDefault(x => x.Value.Name == member.Name);
            if (!string.IsNullOrEmpty(kvp.Key))
            {
                PastMemberIds.Add(kvp.Key);
            }

            return true;
        }

        // --- STAT CALCULATIONS ---

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

        public int GetEffectiveStat(string statName) => GetEffectiveStat(Leader, statName);

        public int GetEffectiveStat(PartyMember member, string statName)
        {
            if (member == null) return 0;
            int baseValue = GetBaseStat(member, statName);
            int bonus = 0;

            if (!string.IsNullOrEmpty(member.EquippedRelicId) && BattleDataCache.Relics.TryGetValue(member.EquippedRelicId, out var relic))
            {
                if (relic.StatModifiers.TryGetValue(statName, out int mod)) bonus += mod;
            }

            if (!string.IsNullOrEmpty(member.EquippedWeaponId) && BattleDataCache.Weapons.TryGetValue(member.EquippedWeaponId, out var weapon))
            {
                if (weapon.StatModifiers.TryGetValue(statName, out int mod)) bonus += mod;
            }

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

                foreach (var member in Party)
                {
                    if (member.EquippedRelicId == relicId && Relics.GetValueOrDefault(relicId) == 0)
                        member.EquippedRelicId = null;
                }
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
                // Check if already known
                for (int i = 0; i < target.Spells.Length; i++)
                {
                    if (target.Spells[i]?.MoveID == moveId) return; // Already have it
                }

                // Find first empty slot
                for (int i = 0; i < target.Spells.Length; i++)
                {
                    if (target.Spells[i] == null)
                    {
                        target.Spells[i] = new MoveEntry(moveId, 0);
                        SortSpells(target); // Auto-sort after adding
                        return;
                    }
                }

                // If full, we do nothing for now (requires a replacement UI or logic)
                Debug.WriteLine($"[PlayerState] Cannot add spell {moveId} to {target.Name}: Slots full.");
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

            // Check Spells
            for (int i = 0; i < target.Spells.Length; i++)
            {
                if (target.Spells[i]?.MoveID == moveId)
                {
                    target.Spells[i] = null;
                    SortSpells(target); // Auto-sort after removing
                    return;
                }
            }

            // Check Actions
            var actionIndex = target.Actions.FindIndex(m => m.MoveID == moveId);
            if (actionIndex != -1) target.Actions.RemoveAt(actionIndex);
        }

        /// <summary>
        /// Sorts the spell slots of a party member based on ImpactType: Magical -> Physical -> Status.
        /// </summary>
        private void SortSpells(PartyMember member)
        {
            // 1. Extract all non-null entries
            var activeSpells = member.Spells.Where(s => s != null).ToList();

            // 2. Sort them
            activeSpells.Sort((a, b) =>
            {
                int scoreA = GetSortScore(a!.MoveID);
                int scoreB = GetSortScore(b!.MoveID);
                return scoreA.CompareTo(scoreB);
            });

            // 3. Re-populate the array
            for (int i = 0; i < 4; i++)
            {
                if (i < activeSpells.Count)
                {
                    member.Spells[i] = activeSpells[i];
                }
                else
                {
                    member.Spells[i] = null;
                }
            }
        }

        private int GetSortScore(string moveId)
        {
            if (BattleDataCache.Moves.TryGetValue(moveId, out var move))
            {
                // Order: Magical (0), Physical (1), Status (2)
                if (move.ImpactType == ImpactType.Magical) return 0;
                if (move.ImpactType == ImpactType.Physical) return 1;
                return 2; // Status
            }
            return 3; // Unknown/Fallback
        }
    }
}

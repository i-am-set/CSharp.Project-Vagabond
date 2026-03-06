using Microsoft.Xna.Framework;
using ProjectVagabond.Battle;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ProjectVagabond.Battle
{
    public static class WizardCatFactory
    {
        private static readonly Random _rng = new Random();

        public static WizardCat CreateMember(string memberId)
        {
            if (!GameDataCache.WizardCats.TryGetValue(memberId, out var data))
            {
                Debug.WriteLine($"[WizardCatFactory] Error: Member ID '{memberId}' not found in cache.");
                return null;
            }

            var member = new WizardCat
            {
                Name = data.Name,
                Strength = data.Strength,
                Intelligence = data.Intelligence,
                Tenacity = data.Tenacity,
                Agility = data.Agility,
                PortraitIndex = int.TryParse(data.MemberID, out int pid) ? pid : 0,
                SparName = data.SparName ?? "Scratch",
                SparBasePower = data.SparBasePower > 0 ? data.SparBasePower : 10,
                SparEffectType = data.SparEffectType ?? "Damage"
            };

            member.CurrentHP = member.MaxHP;

            return member;
        }
    }
}
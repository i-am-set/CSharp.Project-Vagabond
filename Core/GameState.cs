using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond;
using ProjectVagabond.Battle;
using ProjectVagabond.Scenes;
using ProjectVagabond.Transitions;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ProjectVagabond
{
    public class GameState
    {
        private readonly Global _global;
        private readonly SpriteManager _spriteManager;

        private bool _isPaused = false;
        private readonly Random _random = new Random();

        public PlayerState PlayerState { get; private set; }
        public Dictionary<string, WizardCat> RunWizards { get; private set; }
        public List<ArenaWizard> LastMatchWizards { get; set; }

        public bool IsPausedByConsole { get; set; } = false;
        public bool IsPaused => _isPaused || IsPausedByConsole;

        public string LastRunKiller { get; set; } = "Unknown";

        public int CurrentDay { get; private set; }
        public int CurrentEntryFee { get; private set; }

        public GameState(Global global, SpriteManager spriteManager)
        {
            _global = global;
            _spriteManager = spriteManager;
        }

        private int RollStat(int baseStat)
        {
            int roll = _random.Next(100);
            int offset = 0;

            if (roll < 40) offset = 0;
            else if (roll < 60) offset = 1;
            else if (roll < 80) offset = -1;
            else if (roll < 90) offset = 2;
            else offset = -2;

            return Math.Clamp(baseStat + offset, 1, 10);
        }

        public void InitializeWorld(string startingMemberId)
        {
            RunWizards = new Dictionary<string, WizardCat>();

            foreach (var kvp in GameDataCache.WizardCats)
            {
                var data = kvp.Value;
                var cat = new WizardCat
                {
                    Name = data.Name,
                    HP = data.HP,
                    Power = RollStat(data.Power),
                    Tenacity = RollStat(data.Tenacity),
                    Agility = RollStat(data.Agility),
                    PortraitIndex = int.TryParse(data.MemberID, out int pid) ? pid : 0,
                    ActiveSpell = data.ActiveSpell
                };
                cat.CurrentHP = cat.MaxHP;
                RunWizards[kvp.Key] = cat;
            }

            PlayerState = new PlayerState();
            PlayerState.Party.Clear();

            if (!RunWizards.TryGetValue(startingMemberId, out var member))
                throw new Exception($"CRITICAL: Could not load starting member (ID: {startingMemberId})");

            PlayerState.Party.Add(member.Clone());
            PlayerState.Gold = _global.StartingGold;

            CurrentDay = 1;
            CurrentEntryFee = CalculateEntryFee(CurrentDay);
        }

        public void AdvanceDay()
        {
            CurrentDay++;
            CurrentEntryFee = CalculateEntryFee(CurrentDay);
        }

        public int CalculateEntryFee(int day)
        {
            return 10 + (day * 5);
        }

        public void Reset()
        {
            PlayerState = null;
            RunWizards = null;
            LastMatchWizards = null;
            _isPaused = false;
            IsPausedByConsole = false;
            LastRunKiller = "Unknown";
        }

        public void TogglePause()
        {
            _isPaused = !_isPaused;
        }
    }
}
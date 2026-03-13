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

        public static readonly List<ArenaTier> ArenaTiers = new List<ArenaTier>
        {
            new ArenaTier { Name = "Iron", EntryFee = 10, FirstPlace = 20, SecondPlace = 10, ThirdPlace = 5, BonusDamage = 2, BonusKills = 2 },
            new ArenaTier { Name = "Silver", EntryFee = 30, FirstPlace = 75, SecondPlace = 30, ThirdPlace = 10, BonusDamage = 5, BonusKills = 5 },
            new ArenaTier { Name = "Gold", EntryFee = 80, FirstPlace = 240, SecondPlace = 80, ThirdPlace = 20, BonusDamage = 15, BonusKills = 15 },
            new ArenaTier { Name = "Platinum", EntryFee = 200, FirstPlace = 700, SecondPlace = 200, ThirdPlace = 40, BonusDamage = 40, BonusKills = 40 },
            new ArenaTier { Name = "Diamond", EntryFee = 500, FirstPlace = 2000, SecondPlace = 500, ThirdPlace = 0, BonusDamage = 100, BonusKills = 100 }
        };

        public ArenaTier SelectedTier { get; set; }

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
            SelectedTier = ArenaTiers[0];
        }

        public void AdvanceDay()
        {
            CurrentDay++;
        }

        public int GetDailyFloor(int day)
        {
            if (day <= 2) return 10;
            if (day <= 4) return 30;
            if (day <= 6) return 80;
            return 200;
        }

        public void Reset()
        {
            PlayerState = null;
            RunWizards = null;
            LastMatchWizards = null;
            _isPaused = false;
            IsPausedByConsole = false;
            LastRunKiller = "Unknown";
            SelectedTier = null;
        }

        public void TogglePause()
        {
            _isPaused = !_isPaused;
        }
    }
}
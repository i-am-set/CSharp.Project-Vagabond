using Microsoft.Xna.Framework;
using ProjectVagabond.Battle;
using System;
using System.Collections.Generic;

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

        public List<string> SelectedRoster { get; private set; } = new List<string>();
        public string PlayerControlledId { get; set; } = null;

        public bool IsPausedByConsole { get; set; } = false;
        public bool IsPaused => _isPaused || IsPausedByConsole;

        public string LastRunKiller { get; set; } = "Unknown";

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

        public void InitializeWorld(List<string> selectedIds)
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

            SelectedRoster = selectedIds;
            PlayerState = new PlayerState();
            PlayerState.Party.Clear();

            if (selectedIds.Count > 0 && RunWizards.TryGetValue(selectedIds[0], out var member))
            {
                PlayerState.Party.Add(member.Clone());
            }
        }

        public void Reset()
        {
            PlayerState = null;
            RunWizards = null;
            LastMatchWizards = null;
            SelectedRoster.Clear();
            PlayerControlledId = null;
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
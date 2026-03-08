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

        public bool IsPausedByConsole { get; set; } = false;
        public bool IsPaused => _isPaused || IsPausedByConsole;

        public string LastRunKiller { get; set; } = "Unknown";

        public GameState(Global global, SpriteManager spriteManager)
        {
            _global = global;
            _spriteManager = spriteManager;
        }

        public void InitializeWorld(string startingMemberId)
        {
            PlayerState = new PlayerState();
            PlayerState.Party.Clear();

            var member = WizardCatFactory.CreateMember(startingMemberId);
            if (member == null) throw new Exception($"CRITICAL: Could not load starting member (ID: {startingMemberId})");

            PlayerState.Party.Add(member);
            PlayerState.Gold = _global.StartingGold;
        }

        public void Reset()
        {
            PlayerState = null;
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
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
        private readonly NoiseMapManager _noiseManager;
        private readonly Global _global;
        private readonly SpriteManager _spriteManager;

        private bool _isPaused = false;
        private readonly Random _random = new Random();

        public PlayerState PlayerState { get; private set; }

        public bool IsPausedByConsole { get; set; } = false;
        public bool IsPaused => _isPaused || IsPausedByConsole;
        public NoiseMapManager NoiseManager => _noiseManager;

        public string LastRunKiller { get; set; } = "Unknown";

        public GameState(NoiseMapManager noiseManager, Global global, SpriteManager spriteManager)
        {
            _noiseManager = noiseManager;
            _global = global;
            _spriteManager = spriteManager;
        }

        public void InitializeWorld()
        {
            PlayerState = new PlayerState();
            PlayerState.Party.Clear();

            var oakley = PartyMemberFactory.CreateMember("0");
            if (oakley == null) throw new Exception("CRITICAL: Could not load 'Oakley' (ID: 0)");

            PlayerState.Party.Add(oakley);
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
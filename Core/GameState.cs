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

        public void ApplyNarrativeOutcomes(List<NarrativeOutcome> outcomes)
        {
            if (outcomes == null || PlayerState == null) return;

            // Apply to leader by default for single-target stats
            var leader = PlayerState.Leader;

            foreach (var outcome in outcomes)
            {
                switch (outcome.OutcomeType)
                {
                    case "AddBuff":
                        if (Enum.TryParse<StatusEffectType>(outcome.Value, true, out var effectType))
                        {
                            if (leader != null)
                            {
                                leader.ActiveBuffs.Add(new TemporaryBuff { EffectType = effectType, RemainingBattles = outcome.Amount });
                                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[Palette_Sky]Gained {outcome.Value} ({outcome.Amount} battles)!" });
                            }
                        }
                        break;

                    case "ModifyStat":
                        if (leader != null)
                        {
                            string stat = outcome.Value.ToLowerInvariant();
                            int amt = outcome.Amount;
                            if (stat == "strength") leader.Strength += amt;
                            else if (stat == "intelligence") leader.Intelligence += amt;
                            else if (stat == "tenacity") leader.Tenacity += amt;
                            else if (stat == "agility") leader.Agility += amt;
                            else if (stat == "maxhp") { leader.MaxHP += amt; leader.CurrentHP += amt; }
                            else if (stat == "maxmana") { leader.MaxMana += amt; leader.CurrentMana += amt; }

                            string sign = amt > 0 ? "+" : "";
                            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[Palette_Sky]{outcome.Value} {sign}{amt}" });
                        }
                        break;

                    case "Damage":
                        if (leader != null)
                        {
                            leader.CurrentHP = Math.Max(0, leader.CurrentHP - outcome.Amount);
                            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[Palette_Rust]Took {outcome.Amount} damage!" });
                        }
                        break;

                    case "DamagePercent":
                        if (leader != null)
                        {
                            int dmg = (int)(leader.MaxHP * (outcome.Amount / 100f));
                            leader.CurrentHP = Math.Max(0, leader.CurrentHP - dmg);
                            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[Palette_Rust]Took {dmg} damage!" });
                        }
                        break;

                    case "HealFull":
                        if (leader != null)
                        {
                            leader.CurrentHP = leader.MaxHP;
                            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[Palette_Leaf]Fully Healed!" });
                        }
                        break;

                    case "Gold":
                        PlayerState.Coin += outcome.Amount;
                        string gSign = outcome.Amount > 0 ? "+" : "";
                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[Palette_DarkSun]{gSign}{outcome.Amount} Gold" });
                        break;
                }
            }
        }
    }
}
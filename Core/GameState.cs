using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Dice;
using ProjectVagabond.Items;
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
        private readonly ComponentStore _componentStore;
        private readonly Global _global;
        private readonly SpriteManager _spriteManager;

        private bool _isPaused = false;
        private readonly Random _random = new Random();

        public int PlayerEntityId { get; private set; }
        public PlayerState PlayerState { get; private set; }

        public bool IsPausedByConsole { get; set; } = false;
        public bool IsPaused => _isPaused || IsPausedByConsole;
        public NoiseMapManager NoiseManager => _noiseManager;

        public string LastRunKiller { get; set; } = "Unknown";

        // --- Deck of Cards System ---
        // Tracks every item ID generated in this run to prevent duplicates.
        public HashSet<string> SeenItemIds { get; private set; } = new HashSet<string>();

        public GameState(NoiseMapManager noiseManager, ComponentStore componentStore, Global global, SpriteManager spriteManager)
        {
            _noiseManager = noiseManager;
            _componentStore = componentStore;
            _global = global;
            _spriteManager = spriteManager;
        }

        public void InitializeWorld()
        {
            // 1. Create PlayerState container
            PlayerState = new PlayerState();
            PlayerState.Party.Clear();
            SeenItemIds.Clear();

            // 2. Create "Oakley" (The Main Character) using ID "0"
            var oakley = PartyMemberFactory.CreateMember("0");
            if (oakley == null) throw new Exception("CRITICAL: Could not load 'Oakley' (ID: 0)");

            PlayerState.Party.Add(oakley);

            // 3. Add Starting Relics (Global)
            if (BattleDataCache.PartyMembers.TryGetValue("0", out var oakleyData))
            {
                // Weapons loop removed entirely.

                foreach (var kvp in oakleyData.StartingRelics)
                {
                    if (BattleDataCache.Relics.ContainsKey(kvp.Key))
                    {
                        PlayerState.AddRelic(kvp.Key); // No quantity argument
                        SeenItemIds.Add(kvp.Key);
                    }
                }
            }

            // 4. Spawn the Entity
            PlayerEntityId = Spawner.Spawn("player", Vector2.Zero);

            // 5. Sync Entity Components
            var liveStats = new CombatantStatsComponent
            {
                MaxHP = oakley.MaxHP,
                CurrentHP = oakley.MaxHP,
                MaxMana = oakley.MaxMana,
                CurrentMana = oakley.MaxMana,
                Strength = oakley.Strength,
                Intelligence = oakley.Intelligence,
                Tenacity = oakley.Tenacity,
                Agility = oakley.Agility,
                WeaknessElementIDs = new List<int>(oakley.WeaknessElementIDs),
                ResistanceElementIDs = new List<int>(oakley.ResistanceElementIDs),
                AvailableMoveIDs = oakley.Spells
                    .Where(m => m != null)
                    .Select(m => m!.MoveID)
                    .Concat(oakley.Actions.Select(m => m.MoveID))
                    .ToList()
            };
            _componentStore.AddComponent(PlayerEntityId, liveStats);
        }

        public void Reset()
        {
            PlayerEntityId = 0;
            PlayerState = null;
            SeenItemIds.Clear();
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
            if (outcomes == null) return;

            foreach (var outcome in outcomes)
            {
                switch (outcome.OutcomeType)
                {
                    case "GiveItem":
                        // Only support Relics now
                        if (BattleDataCache.Relics.ContainsKey(outcome.Value))
                        {
                            PlayerState.AddRelic(outcome.Value);
                            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[Palette_Sky]Obtained {outcome.Value}!" });
                        }
                        break;

                    case "RemoveItem":
                        // Only check Global Relics
                        if (PlayerState.GlobalRelics.Contains(outcome.Value))
                        {
                            PlayerState.RemoveRelic(outcome.Value);
                            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[Palette_Fruit]Lost {outcome.Value}." });
                        }
                        break;

                    // ... (Keep existing cases for AddBuff, ModifyStat, Damage, etc.) ...
                    case "AddBuff":
                        if (Enum.TryParse<StatusEffectType>(outcome.Value, true, out var effectType))
                        {
                            var buffsComp = _componentStore.GetComponent<TemporaryBuffsComponent>(PlayerEntityId);
                            if (buffsComp == null)
                            {
                                buffsComp = new TemporaryBuffsComponent();
                                _componentStore.AddComponent(PlayerEntityId, buffsComp);
                            }
                            buffsComp.Buffs.Add(new TemporaryBuff { EffectType = effectType, RemainingBattles = outcome.Amount });
                            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[Palette_Sky]Gained {outcome.Value} ({outcome.Amount} battles)!" });
                        }
                        break;

                    case "ModifyStat":
                        var statsComp = _componentStore.GetComponent<CombatantStatsComponent>(PlayerEntityId);
                        if (statsComp != null)
                        {
                            string stat = outcome.Value.ToLowerInvariant();
                            int amt = outcome.Amount;
                            if (stat == "strength") statsComp.Strength += amt;
                            else if (stat == "intelligence") statsComp.Intelligence += amt;
                            else if (stat == "tenacity") statsComp.Tenacity += amt;
                            else if (stat == "agility") statsComp.Agility += amt;
                            else if (stat == "maxhp") { statsComp.MaxHP += amt; statsComp.CurrentHP += amt; }
                            else if (stat == "maxmana") { statsComp.MaxMana += amt; statsComp.CurrentMana += amt; }

                            string sign = amt > 0 ? "+" : "";
                            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[Palette_Sky]{outcome.Value} {sign}{amt}" });
                        }
                        break;

                    case "Damage":
                        var dmgComp = _componentStore.GetComponent<CombatantStatsComponent>(PlayerEntityId);
                        if (dmgComp != null)
                        {
                            dmgComp.CurrentHP = Math.Max(0, dmgComp.CurrentHP - outcome.Amount);
                            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[Palette_Rust]Took {outcome.Amount} damage!" });
                        }
                        break;

                    case "DamagePercent":
                        var dmgPerComp = _componentStore.GetComponent<CombatantStatsComponent>(PlayerEntityId);
                        if (dmgPerComp != null)
                        {
                            int dmg = (int)(dmgPerComp.MaxHP * (outcome.Amount / 100f));
                            dmgPerComp.CurrentHP = Math.Max(0, dmgPerComp.CurrentHP - dmg);
                            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[Palette_Rust]Took {dmg} damage!" });
                        }
                        break;

                    case "HealFull":
                        var healComp = _componentStore.GetComponent<CombatantStatsComponent>(PlayerEntityId);
                        if (healComp != null)
                        {
                            healComp.CurrentHP = healComp.MaxHP;
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

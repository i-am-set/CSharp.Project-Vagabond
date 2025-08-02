using Microsoft.Xna.Framework;
using System;
using System.Linq;

namespace ProjectVagabond
{
    /// <summary>
    /// Manages the triggering of random overland travel encounters based on player movement.
    /// </summary>
    public class EncounterTriggerSystem : ISystem
    {
        // --- TUNING PARAMETERS ---
        private const float BASE_ENCOUNTER_CHANCE = 0.01f; // 1% base chance
        private const float ENCOUNTER_CHANCE_INCREMENT = 0.005f; // Adds 0.5% chance per step

        private readonly EncounterManager _encounterManager;
        private readonly PossibleEncounterListBuilder _encounterListBuilder;
        private readonly Random _random = new();

        private float _encounterChance = BASE_ENCOUNTER_CHANCE;

        public EncounterTriggerSystem()
        {
            _encounterManager = ServiceLocator.Get<EncounterManager>();
            _encounterListBuilder = ServiceLocator.Get<PossibleEncounterListBuilder>();
            EventBus.Subscribe<GameEvents.PlayerMoved>(HandlePlayerMoved);
        }

        private void HandlePlayerMoved(GameEvents.PlayerMoved e)
        {
            if (_random.NextDouble() < _encounterChance)
            {
                var possibleEncounters = _encounterListBuilder.BuildList(e.NewPosition);
                if (possibleEncounters.Any())
                {
                    // Select a random encounter from the valid list
                    var chosenEncounter = possibleEncounters[_random.Next(possibleEncounters.Count)];
                    _encounterManager.TriggerEncounter(chosenEncounter.Id);
                    _encounterChance = BASE_ENCOUNTER_CHANCE; // Reset chance after an encounter
                }
            }
            else
            {
                _encounterChance += ENCOUNTER_CHANCE_INCREMENT;
            }
        }

        public void Update(GameTime gameTime)
        {
            // This system is entirely event-driven by HandlePlayerMoved.
        }
    }
}
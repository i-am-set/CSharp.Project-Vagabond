using Microsoft.Xna.Framework;
using ProjectVagabond.Encounters;
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

        private readonly PossibleEncounterListBuilder _encounterListBuilder;
        private readonly GameState _gameState;
        private readonly Random _random = new();

        private float _encounterChance = BASE_ENCOUNTER_CHANCE;

        public EncounterTriggerSystem()
        {
            _encounterListBuilder = ServiceLocator.Get<PossibleEncounterListBuilder>();
            _gameState = ServiceLocator.Get<GameState>();
            EventBus.Subscribe<GameEvents.PlayerMoved>(HandlePlayerMoved);
        }

        private void HandlePlayerMoved(GameEvents.PlayerMoved e)
        {
            // First, increment the chance for having taken a step.
            // This makes the first step after being idle have a slightly higher chance than baseline,
            // and ensures the probability ramps up progressively with continuous travel.
            _encounterChance += ENCOUNTER_CHANCE_INCREMENT;

            // Now, roll against this newly increased chance.
            if (_random.NextDouble() < _encounterChance)
            {
                var possibleEncounters = _encounterListBuilder.BuildList(e.NewPosition);
                if (possibleEncounters.Any())
                {
                    // Select a random encounter from the valid list
                    var chosenEncounter = possibleEncounters[_random.Next(possibleEncounters.Count)];

                    // Interrupt any ongoing movement before starting the encounter animation.
                    _gameState.CancelExecutingActions(true);

                    // Publish an event instead of directly calling the manager
                    EventBus.Publish(new GameEvents.EncounterTriggered { Encounter = chosenEncounter });

                    _encounterChance = BASE_ENCOUNTER_CHANCE; // Reset chance after an encounter
                }
            }
        }

        public void Update(GameTime gameTime)
        {
            // This system is entirely event-driven by HandlePlayerMoved.
        }
    }
}
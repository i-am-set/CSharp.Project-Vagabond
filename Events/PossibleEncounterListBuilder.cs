using Microsoft.Xna.Framework;
using ProjectVagabond.Encounters;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond
{
    /// <summary>
    /// A class responsible for building a weighted list of valid random encounters
    /// based on the player's current context (e.g., location, stats).
    /// </summary>
    public class PossibleEncounterListBuilder
    {
        private readonly EncounterManager _encounterManager;
        private readonly GameState _gameState;

        public PossibleEncounterListBuilder()
        {
            _encounterManager = ServiceLocator.Get<EncounterManager>();
            _gameState = ServiceLocator.Get<GameState>();
        }

        /// <summary>
        /// Builds a list of all random encounters that are valid for the player's current position.
        /// </summary>
        /// <param name="worldPosition">The player's current world position.</param>
        /// <returns>A list of valid EncounterData objects.</returns>
        public List<EncounterData> BuildList(Vector2 worldPosition)
        {
            var allRandomEncounters = _encounterManager.GetRandomEncounters();
            var validEncounters = new List<EncounterData>();
            var mapData = _gameState.GetMapDataAt((int)worldPosition.X, (int)worldPosition.Y);

            foreach (var encounterData in allRandomEncounters)
            {
                if (AreConditionsMet(encounterData, mapData))
                {
                    validEncounters.Add(encounterData);
                }
            }

            return validEncounters;
        }

        /// <summary>
        /// Checks if all trigger conditions for a given encounter are met.
        /// </summary>
        private bool AreConditionsMet(EncounterData encounterData, MapData mapData)
        {
            if (encounterData.TriggerConditions == null || !encounterData.TriggerConditions.Any())
            {
                return true; // No conditions means it's always valid.
            }

            foreach (var condition in encounterData.TriggerConditions)
            {
                if (!IsConditionMet(condition, mapData))
                {
                    return false; // If any condition fails, the encounter is invalid.
                }
            }

            return true; // All conditions passed.
        }

        /// <summary>
        /// Evaluates a single trigger condition.
        /// </summary>
        private bool IsConditionMet(EncounterTriggerCondition condition, MapData mapData)
        {
            float sourceValue = 0;

            // This switch can be expanded to check player stats, inventory, time of day, etc.
            switch (condition.Type.ToLowerInvariant())
            {
                case "terrainheight":
                    sourceValue = mapData.TerrainHeight;
                    break;
                default:
                    Console.WriteLine($"[WARNING] Unknown trigger condition type: '{condition.Type}'");
                    return false;
            }

            switch (condition.Comparison.ToLowerInvariant())
            {
                case "greaterthan":
                    return sourceValue > condition.Value;
                case "lessthan":
                    return sourceValue < condition.Value;
                case "equalto":
                    // Use an epsilon for float comparison
                    return Math.Abs(sourceValue - condition.Value) < 0.001f;
                default:
                    Console.WriteLine($"[WARNING] Unknown trigger condition comparison: '{condition.Comparison}'");
                    return false;
            }
        }
    }
}
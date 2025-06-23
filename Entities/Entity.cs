using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace ProjectVagabond
{
    public enum EntityType
    {
        Creature,   // Player, animals, monsters
        NPC,        // Traders, quest givers
        Structure,  // Buildings, ruins
        Item,       // Loot bags, dropped items
        Effect      // Fire, smoke signals
    }

    public abstract class Entity
    {
        public Guid Id { get; }
        public string Name { get; protected set; }
        public EntityType Type { get; protected set; }
        public Vector2 WorldPosition { get; protected set; }
    
        public List<PendingAction> ActionQueue { get; } = new List<PendingAction>();

        protected Entity(string name, EntityType type, Vector2 initialPosition)
        {
            Id = Guid.NewGuid();
            Name = name;
            Type = type;
            WorldPosition = initialPosition;
        }

        /// <summary>
        /// Processes the entity's actions for a given duration of time.
        /// Each entity will implement its own logic for how it behaves.
        /// </summary>
        /// <param name="minutesPassed">The amount of time in minutes to simulate.</param>
        /// <param name="gameState">A reference to the current game state for context.</param>
        public abstract void Update(int minutesPassed, GameState gameState);

        public void SetPosition(Vector2 newPosition)
        {
            WorldPosition = newPosition;
        }
    }
}

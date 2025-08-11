using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ProjectVagabond.Combat
{
    /// <summary>
    /// Represents a single entity (player or enemy) within the combat scene.
    /// It manages its own visual state, such as position, scale, and targeting indicators.
    /// </summary>
    public class CombatEntity
    {
        public int EntityId { get; }
        public int MaxHealth { get; private set; }
        public int CurrentHealth { get; private set; }
        public Texture2D Texture { get; }

        public Vector2 Position { get; private set; }
        public float Scale { get; private set; }
        public Rectangle Bounds { get; private set; }

        public bool IsTargeted { get; set; }

        public CombatEntity(int entityId, int health, Texture2D texture)
        {
            EntityId = entityId;
            MaxHealth = health;
            CurrentHealth = health;
            Texture = texture;
        }

        /// <summary>
        /// Sets the entity's position and scale, and recalculates its bounds.
        /// This is typically called by a layout manager in the CombatScene.
        /// </summary>
        public void SetLayout(Vector2 position, float scale)
        {
            Position = position;
            Scale = scale;
            RecalculateBounds();
        }

        private void RecalculateBounds()
        {
            if (Texture == null) return;

            float width = Texture.Width * Scale;
            float height = Texture.Height * Scale;
            Bounds = new Rectangle(
                (int)(Position.X - width / 2),
                (int)(Position.Y - height / 2),
                (int)width,
                (int)height
            );
        }

        public void Update(GameTime gameTime)
        {
            // Future logic for animations or status effect visuals can go here.
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            if (Texture == null) return;

            spriteBatch.Draw(Texture, Bounds, Color.White);
        }
    }
}
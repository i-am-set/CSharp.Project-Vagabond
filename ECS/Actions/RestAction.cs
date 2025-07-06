using Microsoft.Xna.Framework;

namespace ProjectVagabond
{
    /// <summary>
    /// An action representing an entity resting at a location.
    /// </summary>
    public class RestAction : IAction
    {
        public int ActorId { get; }
        public bool IsComplete { get; set; }

        /// <summary>
        /// The type of rest to perform.
        /// </summary>
        public RestType RestType { get; }

        /// <summary>
        /// The position where the rest is taking place.
        /// </summary>
        public Vector2 Position { get; }

        public RestAction(int actorId, RestType restType, Vector2 position)
        {
            ActorId = actorId;
            RestType = restType;
            Position = position;
            IsComplete = false;
        }
    }
}
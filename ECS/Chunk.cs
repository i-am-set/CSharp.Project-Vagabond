using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace ProjectVagabond
{
    /// <summary>
    /// Represents the data associated with a single 1x1 grid cell on the world map.
    /// It holds a collection of all entity IDs located within its boundaries.
    /// </summary>
    public class Chunk
    {
        /// <summary>
        /// The coordinate of this chunk, which directly corresponds to a world map position.
        /// </summary>
        public Point ChunkCoords { get; }

        /// <summary>
        /// A collection of entity IDs currently located within this chunk.
        /// </summary>
        public HashSet<int> EntityIds { get; } = new HashSet<int>();

        /// <summary>
        /// Initializes a new instance of the Chunk class.
        /// </summary>
        /// <param name="chunkCoords">The world coordinates of this chunk.</param>
        public Chunk(Point chunkCoords)
        {
            ChunkCoords = chunkCoords;
        }
    }
}
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ProjectVagabond.Dice
{
    /// <summary>
    /// Represents the visual component of a 3D die. This class is responsible
    /// for holding the graphical model and rendering it to the screen.
    /// </summary>
    public class RenderableDie
    {
        private readonly Model _dieModel;

        /// <summary>
        /// Gets or sets the world transformation matrix for this die, which defines
        /// its position, rotation, and scale in 3D space.
        /// </summary>
        public Matrix World { get; set; }

        /// <summary>
        /// Initializes a new instance of the RenderableDie class.
        /// </summary>
        /// <param name="model">The MonoGame Model to be used for rendering this die.</param>
        public RenderableDie(Model model)
        {
            _dieModel = model;
            World = Matrix.Identity;
        }

        /// <summary>
        /// Draws the die model to the screen.
        /// </summary>
        /// <param name="view">The camera's view matrix.</param>
        /// <param name="projection">The camera's projection matrix.</param>
        public void Draw(Matrix view, Matrix projection)
        {
            if (_dieModel == null)
            {
                return;
            }

            // The GraphicsDevice is needed to set the vertex and index buffers.
            // We can get it from the model's vertex buffer.
            var graphicsDevice = _dieModel.Meshes[0].MeshParts[0].VertexBuffer.GraphicsDevice;

            // Iterate through each mesh in the model. A die model will likely have only one.
            foreach (var mesh in _dieModel.Meshes)
            {
                // Iterate through each part of the mesh. A simple cube might have one part.
                foreach (var part in mesh.MeshParts)
                {
                    // We assume the model uses BasicEffect, which is standard for content pipeline models.
                    if (part.Effect is BasicEffect effect)
                    {
                        // Assign the world, view, and projection matrices to the effect.
                        effect.World = World;
                        effect.View = view;
                        effect.Projection = projection;

                        // Enable default lighting to give the die a 3D appearance.
                        effect.EnableDefaultLighting();

                        // Enable texturing to see the die faces.
                        effect.TextureEnabled = true;

                        // Apply the effect changes before drawing.
                        effect.CurrentTechnique.Passes[0].Apply();
                    }

                    // Set the vertex and index buffers for the graphics device.
                    graphicsDevice.SetVertexBuffer(part.VertexBuffer);
                    graphicsDevice.Indices = part.IndexBuffer;

                    // Draw the mesh part using its vertex and index data.
                    graphicsDevice.DrawIndexedPrimitives(
                        PrimitiveType.TriangleList,
                        part.VertexOffset,
                        part.StartIndex,
                        part.PrimitiveCount);
                }
            }
        }
    }
}

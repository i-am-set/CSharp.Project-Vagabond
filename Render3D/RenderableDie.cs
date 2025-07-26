using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.Linq;
using BepuNumeric = System.Numerics; // Alias Bepu's Vector3 to avoid conflict

namespace ProjectVagabond.Dice
{
    /// <summary>
    /// Represents the visual component of a 3D die. This class is responsible
    /// for holding the graphical model and rendering it to the screen.
    /// </summary>
    public class RenderableDie
    {
        private readonly Model _dieModel;
        private readonly List<BepuNumeric.Vector3> _colliderVertices;
        private readonly VertexPositionColor[] _debugAxisVertices;
        private readonly BasicEffect _debugEffect;
        private readonly GraphicsDevice _graphicsDevice;

        /// <summary>
        /// Gets or sets the world transformation matrix for this die, which defines
        /// its position, rotation, and scale in 3D space.
        /// </summary>
        public Matrix World { get; set; }

        /// <summary>
        /// The color to tint this die's model.
        /// </summary>
        public Color Tint { get; set; }

        /// <summary>
        /// The identifier for the group this die belongs to. This is now settable
        /// to support object pooling.
        /// </summary>
        public string GroupId { get; set; }

        /// <summary>
        /// Initializes a new instance of the RenderableDie class.
        /// </summary>
        /// <param name="model">The MonoGame Model to be used for rendering this die.</param>
        /// <param name="colliderVertices">A list of vertices defining the physics collider for debug visualization.</param>
        /// <param name="debugAxisSize">The size of the axis lines drawn in debug mode.</param>
        /// <param name="tint">The color to tint this die's model.</param>
        /// <param name="groupId">The identifier for the group this die belongs to.</param>
        public RenderableDie(Model model, List<BepuNumeric.Vector3> colliderVertices, float debugAxisSize, Color tint, string groupId)
        {
            _dieModel = model;
            _colliderVertices = colliderVertices;
            World = Matrix.Identity;
            Tint = tint;
            GroupId = groupId;

            // We can get the graphics device from the model itself.
            _graphicsDevice = _dieModel.Meshes[0].MeshParts[0].VertexBuffer.GraphicsDevice;

            // Initialize the effect used for drawing debug shapes.
            _debugEffect = new BasicEffect(_graphicsDevice)
            {
                VertexColorEnabled = true,
                LightingEnabled = false,
                TextureEnabled = false
            };

            // Create the vertices for a small 3-axis cross (X, Y, Z) to be drawn at each collider point.
            // The size of the debug markers is now passed in from the DiceRollingSystem.
            _debugAxisVertices = new[]
            {
                // X-axis (Red)
                new VertexPositionColor(new Vector3(-debugAxisSize, 0, 0), Color.Red),
                new VertexPositionColor(new Vector3(debugAxisSize, 0, 0), Color.Red),
                // Y-axis (Green)
                new VertexPositionColor(new Vector3(0, -debugAxisSize, 0), Color.Green),
                new VertexPositionColor(new Vector3(0, debugAxisSize, 0), Color.Green),
                // Z-axis (Blue)
                new VertexPositionColor(new Vector3(0, 0, -debugAxisSize), Color.Blue),
                new VertexPositionColor(new Vector3(0, 0, debugAxisSize), Color.Blue)
            };
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

                        // Apply the tint color to the model's material.
                        effect.DiffuseColor = this.Tint.ToVector3();

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

        /// <summary>
        /// Draws a debug visualization of the collider's vertices.
        /// </summary>
        /// <param name="view">The camera's view matrix.</param>
        /// <param name="projection">The camera's projection matrix.</param>
        public void DrawDebug(Matrix view, Matrix projection)
        {
            if (_colliderVertices == null || !_colliderVertices.Any())
            {
                return;
            }

            _debugEffect.View = view;
            _debugEffect.Projection = projection;

            foreach (var vertex in _colliderVertices)
            {
                // Convert the BEPU vector to an XNA vector for matrix transformation.
                var xnaVertex = new Vector3(vertex.X, vertex.Y, vertex.Z);

                // Create a world matrix for the debug axis shape.
                // This matrix will position the small axis cross at the vertex's location,
                // relative to the die's overall position and rotation.
                _debugEffect.World = Matrix.CreateTranslation(xnaVertex) * World;

                // Apply the effect and draw the lines.
                _debugEffect.CurrentTechnique.Passes[0].Apply();
                _graphicsDevice.DrawUserPrimitives(
                    PrimitiveType.LineList,
                    _debugAxisVertices,
                    0,
                    3); // 3 pairs of vertices for 3 lines
            }
        }
    }
}
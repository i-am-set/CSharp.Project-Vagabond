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
        private readonly GraphicsDevice _graphicsDevice;

        /// <summary>
        /// The 3D model to be rendered for this die. This is set when the die is
        /// retrieved from the object pool.
        /// </summary>
        public Model CurrentModel { get; set; }

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
        /// The type of die (D6, D4, etc.). This is used to determine which physics
        /// shape and result calculation logic to use.
        /// </summary>
        public DieType DieType { get; set; }

        /// <summary>
        /// The base scale of this die, determined by its group.
        /// </summary>
        public float BaseScale { get; set; } = 1.0f;

        /// <summary>
        /// If true, the die will be rendered with a special highlight effect.
        /// </summary>
        public bool IsHighlighted { get; set; } = false;

        /// <summary>
        /// The color to use for the highlight effect.
        /// </summary>
        public Color HighlightColor { get; set; } = Color.White;

        /// <summary>
        /// A temporary visual-only offset applied to the die's position, used for animations like bouncing.
        /// This does not affect the physics body.
        /// </summary>
        public Vector3 VisualOffset { get; set; } = Vector3.Zero;

        /// <summary>
        /// A temporary visual-only scale multiplier applied to the die, used for animations.
        /// This does not affect the physics body.
        /// </summary>
        public float VisualScale { get; set; } = 1.0f;

        /// <summary>
        /// If true, this die has been "counted" and should no longer be rendered.
        /// </summary>
        public bool IsDespawned { get; set; } = false;

        /// <summary>
        /// Tracks the number of significant collisions this die has experienced.
        /// Used for effects like the D4's initial tumble.
        /// </summary>
        public int CollisionCount { get; set; }


        /// <summary>
        /// Initializes a new instance of the RenderableDie class.
        /// </summary>
        /// <param name="graphicsDevice">The GraphicsDevice to be used for rendering.</param>
        /// <param name="tint">The color to tint this die's model.</param>
        /// <param name="groupId">The identifier for the group this die belongs to.</param>
        public RenderableDie(GraphicsDevice graphicsDevice, Color tint, string groupId)
        {
            _graphicsDevice = graphicsDevice;
            World = Matrix.Identity;
            Tint = tint;
            GroupId = groupId;
            DieType = DieType.D6; // Default value
        }

        /// <summary>
        /// Resets the visual state of the die, typically when it's returned to an object pool.
        /// </summary>
        public void Reset()
        {
            CurrentModel = null;
            World = Matrix.Identity;
            IsHighlighted = false;
            VisualOffset = Vector3.Zero;
            HighlightColor = Color.White;
            VisualScale = 1.0f;
            BaseScale = 1.0f;
            IsDespawned = false;
            DieType = DieType.D6; // Reset to default
            CollisionCount = 0;
        }

        /// <summary>
        /// Draws the die model to the screen.
        /// </summary>
        /// <param name="view">The camera's view matrix.</param>
        /// <param name="projection">The camera's projection matrix.</param>
        public void Draw(Matrix view, Matrix projection)
        {
            if (CurrentModel == null)
            {
                return;
            }

            // Apply the visual offset and scale for animations. Scale is applied first to scale around the object's origin.
            Matrix finalWorld = Matrix.CreateScale(VisualScale * BaseScale) * Matrix.CreateTranslation(VisualOffset) * World;

            // Iterate through each mesh in the model. A die model will likely have only one.
            foreach (var mesh in CurrentModel.Meshes)
            {
                // Iterate through each part of the mesh. A simple cube might have one part.
                foreach (var part in mesh.MeshParts)
                {
                    // We assume the model uses BasicEffect, which is standard for content pipeline models.
                    if (part.Effect is BasicEffect effect)
                    {
                        // Assign the world, view, and projection matrices to the effect.
                        effect.World = finalWorld;
                        effect.View = view;
                        effect.Projection = projection;

                        // --- Manual Lighting Configuration ---
                        effect.LightingEnabled = true;
                        effect.TextureEnabled = true;

                        // Key Light (main light source)
                        effect.DirectionalLight0.Enabled = true;
                        effect.DirectionalLight0.Direction = Vector3.Normalize(new Vector3(-1f, -1.5f, 0f));
                        effect.DirectionalLight0.DiffuseColor = Vector3.One; // White light
                        effect.DirectionalLight0.SpecularColor = new Vector3(0.2f); // Soft gray highlight

                        // Disable other default lights for a simpler setup
                        effect.DirectionalLight1.Enabled = false;
                        effect.DirectionalLight2.Enabled = false;

                        // Ambient Light (fills in shadows)
                        effect.AmbientLightColor = new Vector3(0.15f);


                        // Apply the tint color or highlight to the model's material.
                        if (IsHighlighted)
                        {
                            effect.DiffuseColor = this.HighlightColor.ToVector3();
                            // Add a subtle emissive color to make the highlight "pop"
                            effect.EmissiveColor = this.HighlightColor.ToVector3() * 0.25f;
                        }
                        else
                        {
                            effect.DiffuseColor = this.Tint.ToVector3();
                            effect.EmissiveColor = Vector3.Zero; // No glow when not highlighted
                        }


                        // Apply the effect changes before drawing.
                        effect.CurrentTechnique.Passes[0].Apply();
                    }

                    // Set the vertex and index buffers for the graphics device.
                    _graphicsDevice.SetVertexBuffer(part.VertexBuffer);
                    _graphicsDevice.Indices = part.IndexBuffer;

                    // Draw the mesh part using its vertex and index data.
                    _graphicsDevice.DrawIndexedPrimitives(
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
        /// <param name="debugEffect">The shared BasicEffect for drawing debug lines.</param>
        /// <param name="debugAxisVertices">The shared vertex array for the axis lines.</param>
        /// <param name="colliderVertices">The list of vertices for the specific collider shape being used.</param>
        public void DrawDebug(Matrix view, Matrix projection, BasicEffect debugEffect, VertexPositionColor[] debugAxisVertices, List<BepuNumeric.Vector3> colliderVertices)
        {
            if (colliderVertices == null || !colliderVertices.Any() || debugEffect == null || debugAxisVertices == null)
            {
                return;
            }

            debugEffect.View = view;
            debugEffect.Projection = projection;

            // Apply the visual offset and scale to the base world matrix for debug drawing
            Matrix finalWorld = Matrix.CreateScale(VisualScale * BaseScale) * Matrix.CreateTranslation(VisualOffset) * World;

            foreach (var vertex in colliderVertices)
            {
                // Convert the BEPU vector to an XNA vector for matrix transformation.
                var xnaVertex = new Vector3(vertex.X, vertex.Y, vertex.Z);

                // Create a world matrix for the debug axis shape.
                // This matrix will position the small axis cross at the vertex's location,
                // relative to the die's overall position and rotation.
                debugEffect.World = Matrix.CreateTranslation(xnaVertex) * finalWorld;

                // Apply the effect and draw the lines.
                debugEffect.CurrentTechnique.Passes[0].Apply();
                _graphicsDevice.DrawUserPrimitives(
                    PrimitiveType.LineList,
                    debugAxisVertices,
                    0,
                    3); // 3 pairs of vertices for 3 lines
            }
        }
    }
}
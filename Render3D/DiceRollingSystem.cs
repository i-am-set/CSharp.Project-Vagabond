using BepuPhysics;
using BepuPhysics.Collidables;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using ProjectVagabond.Physics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics; // Required for BEPUphysics types
using BepuUtilities.Memory; // Required for ConvexHull

// Explicitly alias the XNA Quaternion to avoid ambiguity
using XnaQuaternion = Microsoft.Xna.Framework.Quaternion;

namespace ProjectVagabond.Dice
{
    /// <summary>
    /// Manages the entire 3D dice rolling simulation, including physics and rendering.
    /// This system renders the 3D scene to an off-screen texture, which is then
    /// drawn onto the main 2D game scene.
    /// </summary>
    public class DiceRollingSystem
    {
        // Core Components
        private GraphicsDevice _graphicsDevice;
        private PhysicsWorld _physicsWorld;
        private RenderTarget2D _renderTarget;
        private readonly Random _random = new Random();

        // Rendering
        private readonly List<RenderableDie> _renderableDice = new List<RenderableDie>();
        private Model _dieModel;
        private Texture2D _dieTexture;

        // Camera
        private Matrix _view;
        private Matrix _projection;
        private float _viewWidth;
        private float _viewHeight;

        // Physics and Rendering Link
        private readonly Dictionary<BodyHandle, RenderableDie> _bodyToDieMap = new Dictionary<BodyHandle, RenderableDie>();

        // State Tracking
        private bool _wasRollingLastFrame = false;

        /// <summary>
        /// If true, the physics colliders will be rendered as debug visuals.
        /// Can be toggled at runtime (e.g., via a key press in Core.cs).
        /// </summary>
        public bool DebugShowColliders { get; set; } = false;

        /// <summary>
        /// Fired once when all dice in a roll have come to a complete stop.
        /// The payload is a list of integers representing the face value of each die.
        /// </summary>
        public event Action<List<int>> OnRollCompleted;

        /// <summary>
        /// Gets a value indicating whether any dice are currently in motion.
        /// </summary>
        public bool IsRolling
        {
            get
            {
                if (_bodyToDieMap.Count == 0)
                {
                    return false;
                }

                // Increased threshold to be less sensitive to tiny movements
                const float sleepThreshold = 0.2f;

                foreach (var handle in _bodyToDieMap.Keys)
                {
                    var body = _physicsWorld.Simulation.Bodies.GetBodyReference(handle);
                    if (body.Velocity.Linear.LengthSquared() > sleepThreshold || body.Velocity.Angular.LengthSquared() > sleepThreshold)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// Initializes the dice rolling system, loading content and setting up the physics world.
        /// </summary>
        /// <param name="graphicsDevice">The game's GraphicsDevice.</param>
        /// <param name="content">The game's ContentManager.</param>
        public void Initialize(GraphicsDevice graphicsDevice, ContentManager content)
        {
            _graphicsDevice = graphicsDevice;

            _renderTarget = new RenderTarget2D(
                _graphicsDevice,
                Global.VIRTUAL_WIDTH,
                Global.VIRTUAL_HEIGHT,
                false,
                _graphicsDevice.PresentationParameters.BackBufferFormat,
                DepthFormat.Depth24);

            // Load the 3D model and its texture for the dice.
            // These files must be present in the Content project.
            _dieModel = content.Load<Model>("Models/die");
            _dieTexture = content.Load<Texture2D>("Textures/die_texture");

            // Assign the texture to the model's effect
            foreach (var mesh in _dieModel.Meshes)
            {
                foreach (var part in mesh.MeshParts)
                {
                    if (part.Effect is BasicEffect effect)
                    {
                        effect.Texture = _dieTexture;
                    }
                }
            }

            // --- Reduce the view area to make dice appear larger and walls effective ---
            float aspectRatio = (float)_renderTarget.Width / _renderTarget.Height;
            _viewHeight = 35f; // This value controls the "zoom". A smaller value makes everything bigger.
            _viewWidth = _viewHeight * aspectRatio; // Calculate width to maintain the correct aspect ratio.

            // Create the physics world with the new, smaller, aspect-ratio-correct dimensions.
            _physicsWorld = new PhysicsWorld(_viewWidth, _viewHeight);

            // Set up the 3D camera to look at the entire smaller play area.
            var cameraPosition = new Microsoft.Xna.Framework.Vector3(_viewWidth / 2f, 60f, _viewHeight / 2f); // Lowered camera to match smaller world
            var cameraTarget = new Microsoft.Xna.Framework.Vector3(_viewWidth / 2f, 0, _viewHeight / 2f);
            _view = Matrix.CreateLookAt(cameraPosition, cameraTarget, Microsoft.Xna.Framework.Vector3.Forward);

            // Create an orthographic projection that exactly matches the size and aspect ratio of our physics world.
            _projection = Matrix.CreateOrthographic(
                _viewWidth,  // The calculated width of the area to view
                _viewHeight, // The chosen height of the area to view
                1f,          // Near clipping plane
                200f);       // Far clipping plane
        }

        /// <summary>
        /// Clears existing dice and rolls a new set.
        /// </summary>
        /// <param name="numberOfDice">The number of dice to roll.</param>
        public void Roll(int numberOfDice)
        {
            // Clear previous roll
            foreach (var handle in _bodyToDieMap.Keys)
            {
                _physicsWorld.RemoveBody(handle);
            }
            _bodyToDieMap.Clear();
            _renderableDice.Clear();

            // --- Create a physically beveled Convex Hull for the die shape ---
            const float size = 1f; // Half-width of the die
            const float bevelAmount = size * 0.2f; // How much to shave off the corners
            var points = new List<System.Numerics.Vector3>();
            for (int i = 0; i < 8; ++i)
            {
                var corner = new System.Numerics.Vector3(
                    (i & 1) == 0 ? -size : size,
                    (i & 2) == 0 ? -size : size,
                    (i & 4) == 0 ? -size : size);
                points.Add(corner + new System.Numerics.Vector3(Math.Sign(corner.X) * -bevelAmount, 0, 0));
                points.Add(corner + new System.Numerics.Vector3(0, Math.Sign(corner.Y) * -bevelAmount, 0));
                points.Add(corner + new System.Numerics.Vector3(0, 0, Math.Sign(corner.Z) * -bevelAmount));
            }

            var dieShape = new ConvexHull(points.ToArray(), _physicsWorld.BufferPool, out _);
            var dieInertia = dieShape.ComputeInertia(1);
            var shapeIndex = _physicsWorld.Simulation.Shapes.Add(dieShape);
            // --- END OF SHAPE CREATION ---

            // Define spawn boundaries and height, adjusted for the smaller world
            float padding = 3f;
            float spawnHeightMin = 15f;
            float spawnHeightMax = 25f;

            for (int i = 0; i < numberOfDice; i++)
            {
                // Create a renderable die, passing the collider vertices for debug rendering
                var renderableDie = new RenderableDie(_dieModel, points);
                _renderableDice.Add(renderableDie);

                // Create a corresponding physics body, spawned randomly within the viewable area.
                var bodyDescription = BodyDescription.CreateDynamic(
                    new System.Numerics.Vector3(
                        (float)(_random.NextDouble() * (_viewWidth - padding * 2) + padding),
                        (float)(_random.NextDouble() * (spawnHeightMax - spawnHeightMin) + spawnHeightMin),
                        (float)(_random.NextDouble() * (_viewHeight - padding * 2) + padding)),
                    dieInertia,
                    shapeIndex,
                    new BodyActivityDescription(0.01f));

                // --- FIX: Set Continuous Collision Detection properly for BepuPhysics v2 ---
                // In BepuPhysics v2, CCD is controlled via the Collidable.Continuity property
                // which expects a ContinuousDetection struct, not ContinuousDetectionSettings
                bodyDescription.Collidable.Continuity = new ContinuousDetection
                {
                    Mode = ContinuousDetectionMode.Continuous,
                    MinimumSweepTimestep = 1e-3f,
                    SweepConvergenceThreshold = 1e-3f
                };

                bodyDescription.Pose.Orientation = System.Numerics.Quaternion.Normalize(new System.Numerics.Quaternion(
                    (float)_random.NextDouble() * 2 - 1,
                    (float)_random.NextDouble() * 2 - 1,
                    (float)_random.NextDouble() * 2 - 1,
                    (float)_random.NextDouble() * 2 - 1));

                bodyDescription.Velocity.Linear = new System.Numerics.Vector3(
                    (float)(_random.NextDouble() * 100 - 50),
                    -100,
                    (float)(_random.NextDouble() * 100 - 50));

                // Increased angular velocity for a better tumble
                bodyDescription.Velocity.Angular = new System.Numerics.Vector3(
                    (float)(_random.NextDouble() * 40 - 20),
                    (float)(_random.NextDouble() * 40 - 20),
                    (float)(_random.NextDouble() * 40 - 20));

                var handle = _physicsWorld.AddBody(bodyDescription);
                _bodyToDieMap.Add(handle, renderableDie);
            }

            // Set the state to indicate a roll is in progress.
            _wasRollingLastFrame = true;
        }

        /// <summary>
        /// Updates the physics simulation and synchronizes the visual models.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        public void Update(GameTime gameTime)
        {
            _physicsWorld.Update((float)gameTime.ElapsedGameTime.TotalSeconds);

            // Synchronize renderable dice with their physics bodies
            foreach (var pair in _bodyToDieMap)
            {
                var body = _physicsWorld.Simulation.Bodies.GetBodyReference(pair.Key);
                var pose = body.Pose;

                // Convert BEPU's System.Numerics types to MonoGame's XNA types
                var position = new Microsoft.Xna.Framework.Vector3(pose.Position.X, pose.Position.Y, pose.Position.Z);
                var orientation = new XnaQuaternion(pose.Orientation.X, pose.Orientation.Y, pose.Orientation.Z, pose.Orientation.W);

                // Update the world matrix for rendering
                pair.Value.World = Matrix.CreateFromQuaternion(orientation) * Matrix.CreateTranslation(position);
            }

            // Check if the roll has just completed
            bool isCurrentlyRolling = this.IsRolling;
            if (_wasRollingLastFrame && !isCurrentlyRolling)
            {
                var results = new List<int>();
                foreach (var die in _renderableDice)
                {
                    int result = DiceResultHelper.GetUpFaceValue(die.World);
                    results.Add(result);
                }
                OnRollCompleted?.Invoke(results);
            }
            _wasRollingLastFrame = isCurrentlyRolling;
        }

        /// <summary>
        /// Draws the 3D dice scene to an off-screen texture.
        /// </summary>
        /// <returns>The RenderTarget2D containing the rendered dice scene.</returns>
        public RenderTarget2D Draw()
        {
            if (_renderableDice.Count == 0)
            {
                return null;
            }

            // --- Render 3D scene to the RenderTarget ---
            _graphicsDevice.SetRenderTarget(_renderTarget);
            _graphicsDevice.Clear(Color.Transparent);

            // Enable depth testing for proper 3D rendering
            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
            // Use Opaque blend state to draw the solid dice onto the transparent background
            _graphicsDevice.BlendState = BlendState.Opaque;

            foreach (var die in _renderableDice)
            {
                die.Draw(_view, _projection);
            }

            // If debug mode is enabled, draw the collider vertices on top.
            if (DebugShowColliders)
            {
                // Store the current depth state
                var originalDepthState = _graphicsDevice.DepthStencilState;
                // Disable depth testing so our debug lines draw on top of the model
                _graphicsDevice.DepthStencilState = DepthStencilState.None;

                foreach (var die in _renderableDice)
                {
                    die.DrawDebug(_view, _projection);
                }

                // Restore the original depth state for the next draw cycle
                _graphicsDevice.DepthStencilState = originalDepthState;
            }

            // --- Return the result ---
            _graphicsDevice.SetRenderTarget(null);
            return _renderTarget;
        }
    }
}
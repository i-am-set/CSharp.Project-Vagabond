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
        private TypedIndex _dieShapeIndex;
        private BodyInertia _dieInertia;
        private List<System.Numerics.Vector3> _dieColliderVertices;


        // State Tracking
        private bool _wasRollingLastFrame = false;
        private bool _isWaitingForSettle = false;
        private float _settleTimer = 0f;
        private const float SettleDelay = 0.5f; // How long to wait after dice stop before checking for cantering.

        // Failsafe State
        private float _rollInProgressTimer;
        private const float RollTimeout = 4.5f; // After this many seconds, check for stuck dice.
        private List<int> _rerollAttempts; // Tracks failsafe rerolls per die slot.
        private const int MaxRerollAttempts = 5; // Max attempts before forcing a result.
        private Dictionary<int, int> _forcedResults; // Maps a die slot index to a forced result.

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

                // The sleep threshold determines how much movement is considered "stopped".
                // A higher value means the dice will be considered "stopped" even with tiny jitters.
                // A lower value is more sensitive and requires dice to be almost perfectly still.
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
            // This value controls the "zoom" level of the camera.
            // A smaller value makes the dice and the rolling area appear larger on screen.
            _viewHeight = 20f;
            _viewWidth = _viewHeight * aspectRatio; // Calculate width to maintain the correct aspect ratio.

            // Create the physics world with the new, smaller, aspect-ratio-correct dimensions.
            _physicsWorld = new PhysicsWorld(_viewWidth, _viewHeight);

            // --- Create and store the die's physical shape and inertia once ---
            // This avoids re-calculating it on every roll.
            const float size = 1f;
            const float bevelAmount = size * 0.2f;
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
            _dieColliderVertices = points;

            var dieShape = new ConvexHull(_dieColliderVertices.ToArray(), _physicsWorld.BufferPool, out _);

            _dieInertia = dieShape.ComputeInertia(1f);

            _dieShapeIndex = _physicsWorld.Simulation.Shapes.Add(dieShape);


            // Set up the 3D camera to look at the entire smaller play area.
            // The Y value of cameraPosition determines its height. Higher is more top-down.
            var cameraPosition = new Microsoft.Xna.Framework.Vector3(_viewWidth / 2f, 60f, _viewHeight / 2f);
            // The target should be the center of the play area.
            var cameraTarget = new Microsoft.Xna.Framework.Vector3(_viewWidth / 2f, 0, _viewHeight / 2f);
            _view = Matrix.CreateLookAt(cameraPosition, cameraTarget, Microsoft.Xna.Framework.Vector3.Forward);

            // Create an orthographic projection that exactly matches the size and aspect ratio of our physics world.
            // The near (1f) and far (200f) clipping planes define the visible depth range.
            _projection = Matrix.CreateOrthographic(
                _viewWidth,  // The calculated width of the area to view
                _viewHeight, // The chosen height of the area to view
                1f,          // Near clipping plane
                200f);       // Far clipping plane
        }

        /// <summary>
        /// Clears existing dice and rolls a new set, throwing them from off-screen.
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

            // Reset failsafe mechanisms
            _rollInProgressTimer = 0f;
            _rerollAttempts = new List<int>(new int[numberOfDice]);
            _forcedResults = new Dictionary<int, int>();

            for (int i = 0; i < numberOfDice; i++)
            {
                // Create a renderable die, passing the collider vertices for debug rendering
                var renderableDie = new RenderableDie(_dieModel, _dieColliderVertices);
                _renderableDice.Add(renderableDie);

                // Create a corresponding physics body by throwing it from off-screen
                var handle = ThrowDieFromOffscreen(renderableDie);
                _bodyToDieMap.Add(handle, renderableDie);
            }

            // Set the state to indicate a roll is in progress.
            _wasRollingLastFrame = true;
        }

        /// <summary>
        /// Creates a physics body for a die, positions it off-screen, and gives it velocity to enter the view.
        /// </summary>
        /// <param name="renderableDie">The visual die to create a physics body for.</param>
        /// <returns>The BodyHandle of the newly created physics body.</returns>
        private BodyHandle ThrowDieFromOffscreen(RenderableDie renderableDie)
        {
            // Defines how far off-screen the dice will spawn.
            const float offscreenMargin = 5f;
            // Defines the height range from which dice are dropped.
            const float spawnHeightMin = 15f;
            const float spawnHeightMax = 25f;
            // Defines a "no-spawn zone" at the ends of each edge to prevent getting caught on corners.
            const float spawnEdgePadding = 5f;

            System.Numerics.Vector3 spawnPos;
            int side = _random.Next(4); // 0: Left, 1: Right, 2: Top, 3: Bottom

            switch (side)
            {
                case 0: // Left
                    spawnPos = new System.Numerics.Vector3(
                        -offscreenMargin,
                        (float)(_random.NextDouble() * (spawnHeightMax - spawnHeightMin) + spawnHeightMin),
                        (float)(_random.NextDouble() * (_viewHeight - spawnEdgePadding * 2) + spawnEdgePadding));
                    break;
                case 1: // Right
                    spawnPos = new System.Numerics.Vector3(
                        _viewWidth + offscreenMargin,
                        (float)(_random.NextDouble() * (spawnHeightMax - spawnHeightMin) + spawnHeightMin),
                        (float)(_random.NextDouble() * (_viewHeight - spawnEdgePadding * 2) + spawnEdgePadding));
                    break;
                case 2: // Top (Far Z)
                    spawnPos = new System.Numerics.Vector3(
                        (float)(_random.NextDouble() * (_viewWidth - spawnEdgePadding * 2) + spawnEdgePadding),
                        (float)(_random.NextDouble() * (spawnHeightMax - spawnHeightMin) + spawnHeightMin),
                        -offscreenMargin);
                    break;
                default: // Bottom (Near Z)
                    spawnPos = new System.Numerics.Vector3(
                        (float)(_random.NextDouble() * (_viewWidth - spawnEdgePadding * 2) + spawnEdgePadding),
                        (float)(_random.NextDouble() * (spawnHeightMax - spawnHeightMin) + spawnHeightMin),
                        _viewHeight + offscreenMargin);
                    break;
            }

            // The target is always the center of the play area.
            var targetPos = new System.Numerics.Vector3(_viewWidth / 2f, 0, _viewHeight / 2f);

            var direction = System.Numerics.Vector3.Normalize(targetPos - spawnPos);
            float throwForce = (float)(_random.NextDouble() * 25 + 50); // Randomize throw strength

            var bodyDescription = BodyDescription.CreateDynamic(
                spawnPos,
                _dieInertia,
                _dieShapeIndex,
                new BodyActivityDescription(0.01f));

            bodyDescription.Collidable.Continuity = new ContinuousDetection
            {
                Mode = ContinuousDetectionMode.Continuous,
                MinimumSweepTimestep = 1e-4f,
                SweepConvergenceThreshold = 1e-4f
            };

            bodyDescription.Pose.Orientation = System.Numerics.Quaternion.Normalize(new System.Numerics.Quaternion(
                (float)_random.NextDouble() * 2 - 1,
                (float)_random.NextDouble() * 2 - 1,
                (float)_random.NextDouble() * 2 - 1,
                (float)_random.NextDouble() * 2 - 1));

            bodyDescription.Velocity.Linear = direction * throwForce;
            // Increased angular velocity for a much faster, more chaotic tumble.
            bodyDescription.Velocity.Angular = new System.Numerics.Vector3(
                (float)(_random.NextDouble() * 100 - 50),
                (float)(_random.NextDouble() * 100 - 50),
                (float)(_random.NextDouble() * 100 - 50));

            return _physicsWorld.AddBody(bodyDescription);
        }

        /// <summary>
        /// Advances the physics simulation by one fixed time step.
        /// This should be called from a fixed-rate loop in Core.cs.
        /// </summary>
        /// <param name="deltaTime">The fixed time step duration.</param>
        public void PhysicsStep(float deltaTime)
        {
            _physicsWorld.Update(deltaTime);
        }

        /// <summary>
        /// Updates the visual models and game logic based on the current physics state.
        /// This should be called every frame from Core.Update().
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        public void Update(GameTime gameTime)
        {
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

            bool isCurrentlyRolling = this.IsRolling;

            // If dice are rolling, increment the failsafe timer.
            if (isCurrentlyRolling)
            {
                _rollInProgressTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
            }
            else
            {
                _rollInProgressTimer = 0f;
            }

            // If the failsafe timer exceeds the timeout, check for and handle any stuck dice.
            if (_rollInProgressTimer > RollTimeout)
            {
                HandleStuckDice();
                _rollInProgressTimer = 0f; // Reset timer to give new dice a full timeout period.
                return; // Skip the normal settle check for this frame.
            }

            // If the dice were rolling but have just stopped, start the settle timer.
            if (_wasRollingLastFrame && !isCurrentlyRolling)
            {
                _isWaitingForSettle = true;
                _settleTimer = 0f;
            }

            // If we are in the "waiting to settle" state...
            if (_isWaitingForSettle)
            {
                // If the dice start moving again (e.g., from a physics glitch), cancel the settle check.
                if (isCurrentlyRolling)
                {
                    _isWaitingForSettle = false;
                    _settleTimer = 0f;
                }
                else
                {
                    // Otherwise, increment the timer.
                    _settleTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;

                    // If the timer has elapsed, it's time to check the dice state.
                    if (_settleTimer >= SettleDelay)
                    {
                        // --- CHECK 1: Re-roll any dice that are off-screen ---
                        var offscreenDice = new List<BodyHandle>();
                        foreach (var pair in _bodyToDieMap)
                        {
                            var bodyRef = _physicsWorld.Simulation.Bodies.GetBodyReference(pair.Key);
                            var pos = bodyRef.Pose.Position;
                            if (pos.X < 0 || pos.X > _viewWidth || pos.Z < 0 || pos.Z > _viewHeight)
                            {
                                offscreenDice.Add(pair.Key);
                            }
                        }

                        if (offscreenDice.Any())
                        {
                            var diceToReThrow = new List<RenderableDie>();
                            foreach (var handle in offscreenDice)
                            {
                                if (_bodyToDieMap.TryGetValue(handle, out var renderableDie))
                                {
                                    diceToReThrow.Add(renderableDie);
                                    _physicsWorld.RemoveBody(handle);
                                    _bodyToDieMap.Remove(handle);
                                }
                            }

                            foreach (var renderableDie in diceToReThrow)
                            {
                                var newHandle = ThrowDieFromOffscreen(renderableDie);
                                _bodyToDieMap.Add(newHandle, renderableDie);
                            }
                        }
                        else
                        {
                            // --- CHECK 2: Nudge any dice that are cantered ---
                            const float rerollThreshold = 0.99f;
                            var canteredDiceHandles = new List<BodyHandle>();

                            foreach (var pair in _bodyToDieMap)
                            {
                                var (_, alignment) = DiceResultHelper.GetUpFaceValueAndAlignment(pair.Value.World);
                                if (alignment < rerollThreshold)
                                {
                                    canteredDiceHandles.Add(pair.Key);
                                }
                            }

                            if (canteredDiceHandles.Any())
                            {
                                foreach (var handle in canteredDiceHandles)
                                {
                                    var body = _physicsWorld.Simulation.Bodies.GetBodyReference(handle);
                                    if (!body.Awake) body.Awake = true;

                                    body.Velocity.Linear += new System.Numerics.Vector3(
                                        (float)(_random.NextDouble() * 20 - 10),
                                        (float)(_random.NextDouble() * 20 + 10),
                                        (float)(_random.NextDouble() * 20 - 10));

                                    body.Velocity.Angular += new System.Numerics.Vector3(
                                        (float)(_random.NextDouble() * 30 - 15),
                                        (float)(_random.NextDouble() * 30 - 15),
                                        (float)(_random.NextDouble() * 30 - 15));
                                }
                            }
                            else
                            {
                                // --- All checks passed: The roll is complete ---
                                var results = new List<int>();
                                for (int i = 0; i < _renderableDice.Count; i++)
                                {
                                    if (_forcedResults.TryGetValue(i, out int forcedValue))
                                    {
                                        results.Add(forcedValue);
                                    }
                                    else
                                    {
                                        results.Add(DiceResultHelper.GetUpFaceValue(_renderableDice[i].World));
                                    }
                                }
                                OnRollCompleted?.Invoke(results);
                            }
                        }

                        // The check is complete, so reset the waiting state.
                        _isWaitingForSettle = false;
                        _settleTimer = 0f;
                    }
                }
            }

            // Update the state for the next frame.
            _wasRollingLastFrame = isCurrentlyRolling;
        }

        /// <summary>
        /// Finds any dice that are still moving after the timeout, and either re-rolls them
        /// or forces their result if they have been re-rolled too many times.
        /// </summary>
        private void HandleStuckDice()
        {
            // Find all dice that are still physically moving.
            var stuckDiceInfo = new List<(BodyHandle handle, RenderableDie die)>();
            foreach (var pair in _bodyToDieMap)
            {
                var body = _physicsWorld.Simulation.Bodies.GetBodyReference(pair.Key);
                if (body.Awake)
                {
                    stuckDiceInfo.Add((pair.Key, pair.Value));
                }
            }

            if (!stuckDiceInfo.Any()) return;

            foreach (var (handle, die) in stuckDiceInfo)
            {
                // Find the "slot" for this die to track its attempts.
                int slotIndex = _renderableDice.IndexOf(die);
                if (slotIndex == -1) continue; // Should not happen

                _rerollAttempts[slotIndex]++;

                // Remove the stuck die from the simulation.
                _physicsWorld.RemoveBody(handle);
                _bodyToDieMap.Remove(handle);

                if (_rerollAttempts[slotIndex] >= MaxRerollAttempts)
                {
                    // Too many attempts, force the result to 3.
                    _forcedResults[slotIndex] = 3;
                }
                else
                {
                    // Re-throw the die for another attempt.
                    var newHandle = ThrowDieFromOffscreen(die);
                    _bodyToDieMap.Add(newHandle, die);
                }
            }
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
                // Don't draw dice that have been culled by the failsafe.
                // We can check if its corresponding slot has a forced result.
                int slotIndex = _renderableDice.IndexOf(die);
                if (slotIndex != -1 && _forcedResults.ContainsKey(slotIndex))
                {
                    continue;
                }
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
                    // Also skip drawing debug info for culled dice.
                    int slotIndex = _renderableDice.IndexOf(die);
                    if (slotIndex != -1 && _forcedResults.ContainsKey(slotIndex))
                    {
                        continue;
                    }
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
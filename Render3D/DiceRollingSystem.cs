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
using ProjectVagabond.Particles;
using MonoGame.Extended.BitmapFonts;

// Explicitly alias the XNA Quaternion to avoid ambiguity
using XnaQuaternion = Microsoft.Xna.Framework.Quaternion;
using Vector2 = Microsoft.Xna.Framework.Vector2;

namespace ProjectVagabond.Dice
{
    /// <summary>
    /// Manages the entire 3D dice rolling simulation, including physics and rendering.
    /// This system renders the 3D scene to an off-screen texture, which is then
    /// drawn onto the main 2D game scene.
    /// </summary>
    public class DiceRollingSystem
    {
        // Internal state for the dice roll lifecycle
        private enum RollState
        {
            Idle,
            Rolling,
            Settling,
            Enumerating,
            PostEnumerationDelay,
            GatheringResults,
            Complete
        }

        // A small helper class to manage the animated numbers shown during enumeration.
        private class FloatingResultText
        {
            public string Text;
            public int DieValue;
            public Vector2 StartPosition;
            public Vector2 CurrentPosition;
            public Color CurrentColor;
            public float Scale;
            public bool IsFinalSum;
            public float Age;
            public float Lifetime;
        }

        // Core Components
        private GraphicsDevice _graphicsDevice;
        private PhysicsWorld _physicsWorld;
        private RenderTarget2D _renderTarget;
        private readonly Random _random = new Random();
        private readonly Global _global;

        // Rendering & Object Pooling
        private const int InitialPoolSize = 10; // Start with a small pool to avoid startup hitch.
        private readonly List<RenderableDie> _activeDice = new List<RenderableDie>();
        private readonly List<RenderableDie> _diePool = new List<RenderableDie>();
        private Model _dieModel;
        private Texture2D _dieTexture;

        // Particle Effects
        private ParticleSystemManager _particleManager;
        private ParticleEmitter _sparkEmitter;
        private SpriteBatch _particleSpriteBatch;

        // Shared Debug Resources (to prevent hitches)
        private BasicEffect _debugEffect;
        private VertexPositionColor[] _debugAxisVertices;

        // Camera
        private Matrix _view;
        private Matrix _projection;
        private float _viewWidth;
        private float _viewHeight;
        private float _physicsWorldWidth;
        private float _physicsWorldHeight;

        // Physics and Rendering Link
        private readonly Dictionary<BodyHandle, RenderableDie> _bodyToDieMap = new Dictionary<BodyHandle, RenderableDie>();
        private TypedIndex _dieShapeIndex;
        private BodyInertia _dieInertia;
        private List<System.Numerics.Vector3> _dieColliderVertices;
        private List<DiceGroup> _currentRollGroups;


        // State Tracking
        private RollState _currentState = RollState.Idle;
        private float _settleTimer = 0f;
        // SettleDelay is now in Global.cs

        // Failsafe State (reused to avoid allocations)
        private float _rollInProgressTimer;
        private readonly Dictionary<RenderableDie, int> _rerollAttempts = new Dictionary<RenderableDie, int>();
        private readonly Dictionary<RenderableDie, int> _forcedResults = new Dictionary<RenderableDie, int>();
        private int _completeRerollAttempts;

        // --- NEW: State for Enumeration & Gathering Animation ---
        private readonly Queue<RenderableDie> _enumerationQueue = new Queue<RenderableDie>();
        private RenderableDie _currentlyEnumeratingDie;
        private float _enumerationTimer;
        private int _finalSum;
        private readonly List<FloatingResultText> _floatingResults = new List<FloatingResultText>();
        private float _gatheringTimer;


        /// <summary>
        /// If true, the physics colliders will be rendered as debug visuals.
        /// Can be toggled at runtime (e.g., via a key press in Core.cs).
        /// </summary>
        public bool DebugShowColliders { get; set; } = false;

        /// <summary>
        /// Fired once when all dice in a roll have come to a complete stop.
        /// The payload is a structured result object containing the outcome for each group.
        /// </summary>
        public event Action<DiceRollResult> OnRollCompleted;

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

                // The sleep threshold is now configured in the global settings.
                float sleepThreshold = _global.DiceSleepThreshold;

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
        /// Initializes the DiceRollingSystem.
        /// </summary>
        public DiceRollingSystem()
        {
            // Assign the readonly field in the constructor.
            _global = ServiceLocator.Get<Global>();
        }

        /// <summary>
        /// Loads content and sets up the physics world.
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

            float aspectRatio = (float)_renderTarget.Width / _renderTarget.Height;

            // --- Create a single, fixed-size physics world large enough for any roll ---
            const float maxCameraZoom = 40f;
            _physicsWorldHeight = maxCameraZoom;
            _physicsWorldWidth = _physicsWorldHeight * aspectRatio;
            _physicsWorld = new PhysicsWorld(_physicsWorldWidth, _physicsWorldHeight);

            // --- Create and store the die's physical shape and inertia once ---
            float size = _global.DiceColliderSize;
            float bevelAmount = size * _global.DiceColliderBevelRatio;
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
            _dieInertia = dieShape.ComputeInertia(_global.DiceMass);
            _dieShapeIndex = _physicsWorld.Simulation.Shapes.Add(dieShape);

            // --- Pre-populate the RenderableDie object pool with an initial small amount ---
            for (int i = 0; i < InitialPoolSize; i++)
            {
                _diePool.Add(new RenderableDie(_dieModel, _dieColliderVertices, Color.White, ""));
            }

            // --- Set up the initial 3D camera view and projection ---
            _viewHeight = _global.DiceCameraZoom;
            _viewWidth = _viewHeight * aspectRatio;

            var cameraPosition = new Microsoft.Xna.Framework.Vector3(_physicsWorldWidth / 2f, _global.DiceCameraHeight, _physicsWorldHeight / 2f);
            var cameraTarget = new Microsoft.Xna.Framework.Vector3(_physicsWorldWidth / 2f, 0, _physicsWorldHeight / 2f);
            _view = Matrix.CreateLookAt(cameraPosition, cameraTarget, Microsoft.Xna.Framework.Vector3.Forward);

            _projection = Matrix.CreateOrthographic(_viewWidth, _viewHeight, 1f, 200f);
            _physicsWorld.UpdateBoundaryPositions(_viewWidth, _viewHeight);

            // --- Initialize Particle System for Sparks ---
            _particleManager = new ParticleSystemManager();
            _sparkEmitter = _particleManager.CreateEmitter(ParticleEffects.CreateSparks());
            _particleSpriteBatch = new SpriteBatch(_graphicsDevice);
            EventBus.Subscribe<GameEvents.DiceCollisionOccurred>(HandleDiceCollision);

            // --- OPTIMIZATION: Create shared debug resources once ---
            _debugEffect = new BasicEffect(_graphicsDevice)
            {
                VertexColorEnabled = true,
                LightingEnabled = false,
                TextureEnabled = false
            };
            float debugAxisSize = _global.DiceDebugAxisLineSize;
            _debugAxisVertices = new[]
            {
                // X-axis (Red)
                new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(-debugAxisSize, 0, 0), Color.Red),
                new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(debugAxisSize, 0, 0), Color.Red),
                // Y-axis (Green)
                new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(0, -debugAxisSize, 0), Color.Green),
                new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(0, debugAxisSize, 0), Color.Green),
                // Z-axis (Blue)
                new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(0, 0, -debugAxisSize), Color.Blue),
                new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(0, 0, debugAxisSize), Color.Blue)
            };
        }

        /// <summary>
        /// Clears existing dice and rolls a new set based on a list of dice groups.
        /// </summary>
        /// <param name="rollGroups">A list of DiceGroup objects, each defining a set of dice to roll.</param>
        public void Roll(List<DiceGroup> rollGroups)
        {
            // --- Return previously active dice to the pool and clear physics bodies ---
            foreach (var pair in _bodyToDieMap)
            {
                _physicsWorld.RemoveBody(pair.Key);
                pair.Value.Reset(); // Reset visual state
                _diePool.Add(pair.Value);
            }
            _activeDice.Clear();
            _bodyToDieMap.Clear();

            // --- Reset state and clear collections to avoid new allocations ---
            _rollInProgressTimer = 0f;
            _rerollAttempts.Clear();
            _forcedResults.Clear();
            _completeRerollAttempts = 0;
            _currentRollGroups = rollGroups;
            _floatingResults.Clear();
            _enumerationQueue.Clear();
            _currentlyEnumeratingDie = null;

            // --- Dynamic Camera Zoom ---
            int totalDice = rollGroups.Sum(g => g.NumberOfDice);
            float requiredZoom = totalDice <= 8 ? 20f : (totalDice <= 20 ? 30f : 40f);
            float aspectRatio = (float)_renderTarget.Width / _renderTarget.Height;
            _viewHeight = requiredZoom;
            _viewWidth = _viewHeight * aspectRatio;

            _projection = Matrix.CreateOrthographic(_viewWidth, _viewHeight, 1f, 200f);
            _physicsWorld.UpdateBoundaryPositions(_viewWidth, _viewHeight);

            // --- Get dice from the pool or create new ones if needed (growable pool) ---
            foreach (var group in rollGroups)
            {
                // Determine a single spawn side for this entire group.
                int spawnSideForGroup = _random.Next(4);

                for (int i = 0; i < group.NumberOfDice; i++)
                {
                    RenderableDie renderableDie;

                    if (_diePool.Count > 0)
                    {
                        // Get a die from the pool
                        renderableDie = _diePool.Last();
                        _diePool.RemoveAt(_diePool.Count - 1);
                    }
                    else
                    {
                        // Pool is empty, "hot add" a new die.
                        // This is now a very fast operation.
                        renderableDie = new RenderableDie(_dieModel, _dieColliderVertices, Color.White, "");
                    }

                    // Configure the recycled die for its new role
                    renderableDie.GroupId = group.GroupId;
                    renderableDie.Tint = group.Tint;
                    _activeDice.Add(renderableDie);

                    // Create a corresponding physics body, using the pre-determined side for the group.
                    var handle = ThrowDieFromOffscreen(renderableDie, spawnSideForGroup);
                    _bodyToDieMap.Add(handle, renderableDie);
                }
            }

            _currentState = RollState.Rolling;
        }

        /// <summary>
        /// Creates a physics body for a die, positions it off-screen, and gives it velocity to enter the view.
        /// </summary>
        /// <param name="renderableDie">The visual die to create a physics body for.</param>
        /// <param name="spawnSide">The side to spawn from (0:Left, 1:Right, 2:Top, 3:Bottom).</param>
        /// <returns>The BodyHandle of the newly created physics body.</returns>
        private BodyHandle ThrowDieFromOffscreen(RenderableDie renderableDie, int spawnSide)
        {
            // Spawning parameters are now pulled from global settings.
            float offscreenMargin = _global.DiceSpawnOffscreenMargin;
            float spawnHeightMin = _global.DiceSpawnHeightMin;
            float spawnHeightMax = _global.DiceSpawnHeightMax;
            float spawnEdgePadding = _global.DiceSpawnEdgePadding;

            System.Numerics.Vector3 spawnPos;

            // Calculate the bounds of the current visible area within the larger physics world.
            float centerX = _physicsWorldWidth / 2f;
            float centerZ = _physicsWorldHeight / 2f;
            float visibleMinX = centerX - _viewWidth / 2f;
            float visibleMaxX = centerX + _viewWidth / 2f;
            float visibleMinZ = centerZ - _viewHeight / 2f;
            float visibleMaxZ = centerZ + _viewHeight / 2f;

            switch (spawnSide)
            {
                case 0: // Left
                    spawnPos = new System.Numerics.Vector3(
                        visibleMinX - offscreenMargin,
                        (float)(_random.NextDouble() * (spawnHeightMax - spawnHeightMin) + spawnHeightMin),
                        (float)(_random.NextDouble() * (_viewHeight - spawnEdgePadding * 2) + visibleMinZ + spawnEdgePadding));
                    break;
                case 1: // Right
                    spawnPos = new System.Numerics.Vector3(
                        visibleMaxX + offscreenMargin,
                        (float)(_random.NextDouble() * (spawnHeightMax - spawnHeightMin) + spawnHeightMin),
                        (float)(_random.NextDouble() * (_viewHeight - spawnEdgePadding * 2) + visibleMinZ + spawnEdgePadding));
                    break;
                case 2: // Top (Far Z)
                    spawnPos = new System.Numerics.Vector3(
                        (float)(_random.NextDouble() * (_viewWidth - spawnEdgePadding * 2) + visibleMinX + spawnEdgePadding),
                        (float)(_random.NextDouble() * (spawnHeightMax - spawnHeightMin) + spawnHeightMin),
                        visibleMinZ - offscreenMargin);
                    break;
                default: // Bottom (Near Z)
                    spawnPos = new System.Numerics.Vector3(
                        (float)(_random.NextDouble() * (_viewWidth - spawnEdgePadding * 2) + visibleMinX + spawnEdgePadding),
                        (float)(_random.NextDouble() * (spawnHeightMax - spawnHeightMin) + spawnHeightMin),
                        visibleMaxZ + offscreenMargin);
                    break;
            }

            // The target is always the center of the physics world (and thus the center of the view).
            var targetPos = new System.Numerics.Vector3(centerX, 0, centerZ);

            var direction = System.Numerics.Vector3.Normalize(targetPos - spawnPos);
            // Throw force is randomized within the range defined in global settings.
            float throwForce = (float)(_random.NextDouble() * (_global.DiceThrowForceMax - _global.DiceThrowForceMin) + _global.DiceThrowForceMin);

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
            // Angular velocity is randomized based on the max value in global settings.
            float maxAngVel = _global.DiceInitialAngularVelocityMax;
            bodyDescription.Velocity.Angular = new System.Numerics.Vector3(
                (float)(_random.NextDouble() * maxAngVel * 2 - maxAngVel),
                (float)(_random.NextDouble() * maxAngVel * 2 - maxAngVel),
                (float)(_random.NextDouble() * maxAngVel * 2 - maxAngVel));

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
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Update the particle system for this simulation
            _particleManager.Update(gameTime);

            // Update floating result text animations
            UpdateFloatingResults(gameTime);

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

            // --- Main State Machine ---
            switch (_currentState)
            {
                case RollState.Idle:
                case RollState.Complete:
                    return; // Do nothing until a new roll is requested.

                case RollState.Rolling:
                    UpdateRollingState(deltaTime);
                    break;

                case RollState.Settling:
                    UpdateSettlingState(deltaTime);
                    break;

                case RollState.Enumerating:
                    UpdateEnumeratingState(deltaTime);
                    break;

                case RollState.PostEnumerationDelay:
                    UpdatePostEnumerationDelayState(deltaTime);
                    break;

                case RollState.GatheringResults:
                    UpdateGatheringState(deltaTime);
                    break;
            }
        }

        private void UpdateRollingState(float deltaTime)
        {
            // PRIMARY FAILSAFE: Instantly detect and replace any missing dice
            if (_activeDice.Any())
            {
                int expectedDiceCount = _activeDice.Count;
                int activeDiceCount = _bodyToDieMap.Count + _forcedResults.Count;
                if (activeDiceCount < expectedDiceCount)
                {
                    HandleMissingDice();
                    return; // End the update for this frame to allow the new die to enter the simulation.
                }
            }

            bool isCurrentlyRolling = this.IsRolling;

            // If dice are rolling, increment the timeout timer.
            if (isCurrentlyRolling)
            {
                _rollInProgressTimer += deltaTime;
            }
            else
            {
                _rollInProgressTimer = 0f;
            }

            // TERTIARY FAILSAFE: If the entire roll is taking too long, re-roll everything.
            if (_rollInProgressTimer > _global.DiceCompleteRollTimeout)
            {
                HandleCompleteReroll();
                return; // End update for this frame.
            }

            // SECONDARY FAILSAFE: If the roll takes too long, check for and handle any stuck dice
            if (_rollInProgressTimer > _global.DiceRollTimeout)
            {
                HandleStuckDice();
                return; // Skip the normal settle check for this frame.
            }

            // If the dice were rolling but have just stopped, start the settle timer.
            if (!isCurrentlyRolling)
            {
                _currentState = RollState.Settling;
                _settleTimer = 0f;
            }
        }

        private void UpdateSettlingState(float deltaTime)
        {
            // If the dice start moving again (e.g., from a physics glitch), go back to rolling state.
            if (this.IsRolling)
            {
                _currentState = RollState.Rolling;
                _settleTimer = 0f;
                return;
            }

            _settleTimer += deltaTime;

            // If the timer has elapsed, it's time to check the dice state.
            if (_settleTimer >= _global.DiceSettleDelay)
            {
                // CHECK 1: Re-roll any dice that are off-screen
                var offscreenDice = new List<BodyHandle>();
                float centerX = _physicsWorldWidth / 2f;
                float centerZ = _physicsWorldHeight / 2f;
                float visibleMinX = centerX - _viewWidth / 2f;
                float visibleMaxX = centerX + _viewWidth / 2f;
                float visibleMinZ = centerZ - _viewHeight / 2f;
                float visibleMaxZ = centerZ + _viewHeight / 2f;

                foreach (var pair in _bodyToDieMap)
                {
                    var bodyRef = _physicsWorld.Simulation.Bodies.GetBodyReference(pair.Key);
                    var pos = bodyRef.Pose.Position;
                    if (pos.X < visibleMinX || pos.X > visibleMaxX || pos.Z < visibleMinZ || pos.Z > visibleMaxZ)
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
                        var newHandle = ThrowDieFromOffscreen(renderableDie, _random.Next(4));
                        _bodyToDieMap.Add(newHandle, renderableDie);
                    }
                    _rollInProgressTimer = 0f;
                    _currentState = RollState.Rolling;
                }
                else
                {
                    // CHECK 2: Nudge any dice that are cantered
                    float rerollThreshold = _global.DiceCantingRerollThreshold;
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

                            float nudgeForceMin = _global.DiceNudgeForceMin;
                            float nudgeForceMax = _global.DiceNudgeForceMax;
                            float nudgeUpMin = _global.DiceNudgeUpwardForceMin;
                            float nudgeUpMax = _global.DiceNudgeUpwardForceMax;
                            float nudgeTorqueMax = _global.DiceNudgeTorqueMax;

                            body.Velocity.Linear += new System.Numerics.Vector3(
                                (float)(_random.NextDouble() * (nudgeForceMax - nudgeForceMin) + nudgeForceMin),
                                (float)(_random.NextDouble() * (nudgeUpMax - nudgeUpMin) + nudgeUpMin),
                                (float)(_random.NextDouble() * (nudgeForceMax - nudgeForceMin) + nudgeForceMin));

                            body.Velocity.Angular += new System.Numerics.Vector3(
                                (float)(_random.NextDouble() * nudgeTorqueMax * 2 - nudgeTorqueMax),
                                (float)(_random.NextDouble() * nudgeTorqueMax * 2 - nudgeTorqueMax),
                                (float)(_random.NextDouble() * nudgeTorqueMax * 2 - nudgeTorqueMax));
                        }
                        _rollInProgressTimer = 0f;
                        _currentState = RollState.Rolling;
                    }
                    else
                    {
                        // === All checks passed: The roll is ready for enumeration ===
                        StartEnumeration();
                    }
                }
                _settleTimer = 0f;
            }
        }

        private void StartEnumeration()
        {
            _currentState = RollState.Enumerating;
            _finalSum = 0;
            _enumerationTimer = 0f;
            _enumerationQueue.Clear();

            // Sort dice by their X position on the screen to count from left to right.
            var sortedDice = _activeDice
                .Where(d => !_forcedResults.ContainsKey(d)) // Exclude dice that were forced
                .OrderBy(d => d.World.Translation.X)
                .ToList();

            // Add forced dice to the end of the queue
            var forcedDice = _activeDice.Where(d => _forcedResults.ContainsKey(d));
            sortedDice.AddRange(forcedDice);

            foreach (var die in sortedDice)
            {
                _enumerationQueue.Enqueue(die);
            }

            // Start the first enumeration step immediately
            ProcessNextEnumerationStep();
        }

        private void UpdateEnumeratingState(float deltaTime)
        {
            _enumerationTimer += deltaTime;

            if (_currentlyEnumeratingDie != null)
            {
                // Handle the visual scale animation
                float progress = Math.Clamp(_enumerationTimer / _global.DiceEnumerationStepDuration, 0f, 1f);
                float scale;
                float baseScale = 1.0f;
                float scaleRange = _global.DiceEnumerationMaxScale - baseScale;

                // Use a split easing function for a smooth scale-up and scale-down motion
                if (progress < 0.5f)
                {
                    // First half: scaling up, using EaseOut
                    float upProgress = progress * 2f; // remap 0 -> 0.5 to 0 -> 1
                    scale = baseScale + scaleRange * Easing.EaseOutCubic(upProgress);
                }
                else
                {
                    // Second half: scaling down, using EaseIn
                    float downProgress = (progress - 0.5f) * 2f; // remap 0.5 -> 1 to 0 -> 1
                    scale = baseScale + scaleRange * (1 - Easing.EaseInCubic(downProgress));
                }
                _currentlyEnumeratingDie.VisualScale = scale;


                // Handle the white flash effect
                if (_enumerationTimer < _global.DiceEnumerationFlashDuration)
                {
                    _currentlyEnumeratingDie.IsHighlighted = true;
                    _currentlyEnumeratingDie.HighlightColor = Color.White;
                }
                else
                {
                    // After the flash, keep it highlighted with its tint
                    _currentlyEnumeratingDie.IsHighlighted = true;
                    _currentlyEnumeratingDie.HighlightColor = _currentlyEnumeratingDie.Tint;
                }
            }

            if (_enumerationTimer >= _global.DiceEnumerationStepDuration)
            {
                // Reset the timer and process the next die in the queue
                _enumerationTimer = 0f;
                if (_currentlyEnumeratingDie != null)
                {
                    _currentlyEnumeratingDie.IsHighlighted = false;
                    _currentlyEnumeratingDie.VisualScale = 1.0f;
                }
                ProcessNextEnumerationStep();
            }
        }

        private void ProcessNextEnumerationStep()
        {
            if (_enumerationQueue.TryDequeue(out _currentlyEnumeratingDie))
            {
                // Get the result of this die
                int dieValue = _forcedResults.TryGetValue(_currentlyEnumeratingDie, out int forcedValue)
                    ? forcedValue
                    : DiceResultHelper.GetUpFaceValue(_currentlyEnumeratingDie.World);

                _finalSum += dieValue;

                // Create the floating number text
                var dieWorldPos = _currentlyEnumeratingDie.World.Translation;
                var viewport = new Viewport(_renderTarget.Bounds);
                var dieScreenPos = viewport.Project(dieWorldPos, _projection, _view, Matrix.Identity);
                var dieScreenPos2D = new Vector2(dieScreenPos.X, dieScreenPos.Y);

                _floatingResults.Add(new FloatingResultText
                {
                    Text = dieValue.ToString(),
                    DieValue = dieValue,
                    StartPosition = dieScreenPos2D,
                    CurrentPosition = dieScreenPos2D,
                    Scale = 1.2f
                });
            }
            else
            {
                // Queue is empty, enumeration is complete, start the delay
                _currentlyEnumeratingDie = null;
                _currentState = RollState.PostEnumerationDelay;
                _gatheringTimer = 0f; // Reuse this timer for the delay
            }
        }

        private void UpdatePostEnumerationDelayState(float deltaTime)
        {
            _gatheringTimer += deltaTime;
            if (_gatheringTimer >= _global.DicePostEnumerationDelay)
            {
                _currentState = RollState.GatheringResults;
                _gatheringTimer = 0f; // Reset for the gathering animation
            }
        }

        private void UpdateGatheringState(float deltaTime)
        {
            _gatheringTimer += deltaTime;
            float progress = Math.Clamp(_gatheringTimer / _global.DiceGatheringDuration, 0f, 1f);
            float easedProgress = Easing.EaseInCirc(progress);

            var screenCenter = new Vector2(_renderTarget.Width / 2f, _renderTarget.Height / 2f);

            foreach (var result in _floatingResults)
            {
                result.CurrentPosition = Vector2.Lerp(result.StartPosition, screenCenter, easedProgress);
            }

            if (progress >= 1.0f)
            {
                _floatingResults.Clear();
                _floatingResults.Add(new FloatingResultText
                {
                    Text = _finalSum.ToString(),
                    CurrentPosition = screenCenter,
                    Scale = 2.5f,
                    IsFinalSum = true,
                    Age = 0,
                    Lifetime = _global.DiceFinalSumLifetime
                });

                _currentState = RollState.Complete;
                FinalizeAndReportResults();
            }
        }


        private void UpdateFloatingResults(GameTime gameTime)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            float totalTime = (float)gameTime.TotalGameTime.TotalSeconds;

            for (int i = _floatingResults.Count - 1; i >= 0; i--)
            {
                var result = _floatingResults[i];

                if (result.IsFinalSum)
                {
                    result.Age += deltaTime;
                    if (result.Age >= result.Lifetime)
                    {
                        _floatingResults.RemoveAt(i);
                    }
                    else
                    {
                        float lifeRatio = result.Age / result.Lifetime;
                        // Fade out
                        result.CurrentColor = Color.White * (1.0f - lifeRatio);
                        // Float upwards
                        float easedProgress = Easing.EaseOutCirc(lifeRatio);
                        var screenCenter = new Vector2(_renderTarget.Width / 2f, _renderTarget.Height / 2f);
                        result.CurrentPosition = new Vector2(screenCenter.X, screenCenter.Y - (_global.DiceFinalSumFloatHeight * easedProgress));
                    }
                }
                else
                {
                    // Pulsing color effect
                    float pulseValue = (float)(Math.Sin(totalTime * 15f) + 1) / 2.0f;
                    result.CurrentColor = Color.Lerp(Color.Black, Color.White, pulseValue);
                }
            }
        }

        /// <summary>
        /// Handles a DiceCollisionOccurred event, spawning spark particles at the collision point.
        /// </summary>
        private void HandleDiceCollision(GameEvents.DiceCollisionOccurred e)
        {
            // Convert the 3D world position from the physics engine to a 2D screen position on our render target.
            var worldPos = new Microsoft.Xna.Framework.Vector3(e.WorldPosition.X, e.WorldPosition.Y, e.WorldPosition.Z);
            var viewport = new Viewport(_renderTarget.Bounds);
            var screenPos3D = viewport.Project(worldPos, _projection, _view, Matrix.Identity);

            // The projection can result in a point outside the viewport; we don't need sparks for those.
            if (screenPos3D.Z < 0 || screenPos3D.Z > 1)
            {
                return;
            }

            // Set the emitter's 2D position and fire a burst of particles.
            _sparkEmitter.Position = new Vector2(screenPos3D.X, screenPos3D.Y);

            // --- Emit a large, explosive burst of particles all at once. ---
            int burstCount = _random.Next(40, 71); // Emit 40 to 70 particles for a big impact.
            for (int i = 0; i < burstCount; i++)
            {
                int pIndex = _sparkEmitter.EmitParticleAndGetIndex();
                if (pIndex == -1) break; // Emitter is full

                // Manually set the particle's velocity for a radial burst effect.
                ref var p = ref _sparkEmitter.GetParticle(pIndex);
                float angle = (float)(_random.NextDouble() * MathHelper.TwoPi);
                float speed = _sparkEmitter.Settings.InitialVelocityX.GetValue(_random); // Using X as speed range
                p.Velocity = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * speed;
            }
        }

        /// <summary>
        /// Gathers results from all dice (physical and forced), processes them, and invokes the OnRollCompleted event.
        /// </summary>
        private void FinalizeAndReportResults()
        {
            var result = new DiceRollResult();
            var rawResults = new Dictionary<string, List<int>>();

            // 1. Gather raw face values from all dice, including forced ones.
            foreach (var die in _activeDice)
            {
                if (!rawResults.ContainsKey(die.GroupId))
                {
                    rawResults[die.GroupId] = new List<int>();
                }

                if (_forcedResults.TryGetValue(die, out int forcedValue))
                {
                    rawResults[die.GroupId].Add(forcedValue);
                }
                else
                {
                    rawResults[die.GroupId].Add(DiceResultHelper.GetUpFaceValue(die.World));
                }
            }

            // 2. Process the raw results according to the rules of each group.
            foreach (var group in _currentRollGroups)
            {
                if (rawResults.TryGetValue(group.GroupId, out var values))
                {
                    if (group.ResultProcessing == DiceResultProcessing.Sum)
                    {
                        result.ResultsByGroup[group.GroupId] = new List<int> { values.Sum() };
                    }
                    else // IndividualValues
                    {
                        result.ResultsByGroup[group.GroupId] = values;
                    }
                }
            }

            OnRollCompleted?.Invoke(result);
            _currentState = RollState.Idle; // Ready for the next roll
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
                HandleReroll(die, handle);
            }
            _rollInProgressTimer = 0f; // Reset timeout since we initiated a re-roll.
        }

        /// <summary>
        /// Instantly detects which die slots are missing a physics body and triggers a re-roll for them.
        /// </summary>
        private void HandleMissingDice()
        {
            var activeRenderableDice = _bodyToDieMap.Values.ToHashSet();

            // Find any die from our master list that is not currently in the physics simulation.
            var missingDice = _activeDice.Where(d => !activeRenderableDice.Contains(d) && !_forcedResults.ContainsKey(d)).ToList();

            foreach (var die in missingDice)
            {
                HandleReroll(die);
            }

            if (missingDice.Any())
            {
                _rollInProgressTimer = 0f; // Reset timeout since we initiated a re-roll.
            }
        }

        /// <summary>
        /// Centralized logic for handling a re-roll attempt for a specific die.
        /// Increments the attempt counter and either re-throws the die or forces its result.
        /// </summary>
        /// <param name="die">The renderable die to be re-rolled.</param>
        /// <param name="handleToRemove">Optional handle of the old physics body to remove.</param>
        private void HandleReroll(RenderableDie die, BodyHandle? handleToRemove = null)
        {
            // Increment attempt counter for this specific die instance.
            if (!_rerollAttempts.ContainsKey(die))
            {
                _rerollAttempts[die] = 0;
            }
            _rerollAttempts[die]++;

            // If there's an old physics body, remove it.
            if (handleToRemove.HasValue)
            {
                _physicsWorld.RemoveBody(handleToRemove.Value);
                _bodyToDieMap.Remove(handleToRemove.Value);
            }

            if (_rerollAttempts[die] >= _global.DiceMaxRerollAttempts)
            {
                // Too many attempts, force the result.
                _forcedResults[die] = _global.DiceForcedResultValue;
            }
            else
            {
                // Re-throw the die for another attempt, giving it a new random side.
                var newHandle = ThrowDieFromOffscreen(die, _random.Next(4));
                _bodyToDieMap.Add(newHandle, die);
            }
        }

        /// <summary>
        /// A major failsafe that re-rolls all dice in the simulation. This is triggered
        /// when the roll takes an exceptionally long time to resolve.
        /// </summary>
        private void HandleCompleteReroll()
        {
            _completeRerollAttempts++;

            // If we've exceeded the max attempts for a complete reroll, force the entire roll to end.
            if (_completeRerollAttempts >= _global.DiceMaxRerollAttempts)
            {
                HandleForcedCompletion();
                return;
            }

            // --- Re-roll all dice ---
            // 1. Clear all existing physics bodies
            foreach (var handle in _bodyToDieMap.Keys)
            {
                _physicsWorld.RemoveBody(handle);
            }
            _bodyToDieMap.Clear();
            _forcedResults.Clear(); // Clear any previously forced results

            // 2. Re-throw each active die
            foreach (var die in _activeDice)
            {
                var newHandle = ThrowDieFromOffscreen(die, _random.Next(4));
                _bodyToDieMap.Add(newHandle, die);
            }

            // 3. Reset timers and state for the new attempt
            _rollInProgressTimer = 0f;
            _currentState = RollState.Rolling;
        }

        /// <summary>
        /// A final failsafe that ends the roll immediately, assigning a forced value to all dice.
        /// This is triggered after the maximum number of complete re-rolls has been attempted.
        /// </summary>
        private void HandleForcedCompletion()
        {
            // 1. Clear all physics bodies to stop the simulation
            foreach (var handle in _bodyToDieMap.Keys)
            {
                _physicsWorld.RemoveBody(handle);
            }
            _bodyToDieMap.Clear();
            _forcedResults.Clear();

            // 2. Force a result for every single active die
            foreach (var die in _activeDice)
            {
                _forcedResults[die] = _global.DiceForcedResultValue;
            }

            // 3. Immediately process and fire the OnRollCompleted event
            FinalizeAndReportResults();

            // 4. Reset state to prevent further updates on this roll
            _currentState = RollState.Idle;
            _rollInProgressTimer = 0f;
        }


        /// <summary>
        /// Draws the 3D dice scene to an off-screen texture.
        /// </summary>
        /// <param name="font">The font to use for drawing result numbers.</param>
        /// <returns>The RenderTarget2D containing the rendered dice scene.</returns>
        public RenderTarget2D Draw(BitmapFont font)
        {
            if (_activeDice.Count == 0)
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

            foreach (var die in _activeDice)
            {
                // Don't draw dice that have been culled by the failsafe.
                if (_forcedResults.ContainsKey(die))
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

                foreach (var die in _activeDice)
                {
                    // Also skip drawing debug info for culled dice.
                    if (_forcedResults.ContainsKey(die))
                    {
                        continue;
                    }
                    // MODIFIED: Pass the shared debug resources to the draw call.
                    die.DrawDebug(_view, _projection, _debugEffect, _debugAxisVertices);
                }

                // Restore the original depth state for the next draw cycle
                _graphicsDevice.DepthStencilState = originalDepthState;
            }

            // --- Draw 2D Particle Effects and Floating Numbers on top of the 3D scene ---
            _particleManager.Draw(_particleSpriteBatch);

            // Draw the floating result numbers during enumeration
            if (_floatingResults.Any() && font != null)
            {
                _particleSpriteBatch.Begin(samplerState: SamplerState.PointClamp);
                foreach (var result in _floatingResults)
                {
                    Vector2 textSize = font.MeasureString(result.Text) * result.Scale;
                    Vector2 textOrigin = new Vector2(textSize.X / (2 * result.Scale), textSize.Y / (2 * result.Scale));
                    _particleSpriteBatch.DrawString(font, result.Text, result.CurrentPosition, result.CurrentColor, 0f, textOrigin, result.Scale, SpriteEffects.None, 0f);
                }
                _particleSpriteBatch.End();
            }


            // --- Return the result ---
            _graphicsDevice.SetRenderTarget(null);
            return _renderTarget;
        }
    }
}
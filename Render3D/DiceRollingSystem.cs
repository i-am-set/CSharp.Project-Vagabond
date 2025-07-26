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
        private readonly Global _global;

        // Rendering
        private readonly List<RenderableDie> _renderableDice = new List<RenderableDie>();
        private Model _dieModel;
        private Texture2D _dieTexture;

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
        private bool _wasRollingLastFrame = false;
        private bool _isWaitingForSettle = false;
        private float _settleTimer = 0f;
        // SettleDelay is now in Global.cs

        // Failsafe State
        private float _rollInProgressTimer;
        // RollTimeout is now in Global.cs
        private Dictionary<RenderableDie, int> _rerollAttempts; // Tracks failsafe rerolls per die.
        // MaxRerollAttempts is now in Global.cs
        private Dictionary<RenderableDie, int> _forcedResults = new Dictionary<RenderableDie, int>(); // Maps a die object to a forced result.

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

            float aspectRatio = (float)_renderTarget.Width / _renderTarget.Height;

            // --- Create a single, fixed-size physics world large enough for any roll ---
            // We'll use the largest potential zoom level (e.g., 40f for >20 dice) to define the physics bounds.
            const float maxCameraZoom = 40f;
            _physicsWorldHeight = maxCameraZoom;
            _physicsWorldWidth = _physicsWorldHeight * aspectRatio;
            _physicsWorld = new PhysicsWorld(_physicsWorldWidth, _physicsWorldHeight);

            // --- Create and store the die's physical shape and inertia once ---
            // This avoids re-calculating it on every roll.
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

            // --- Set up the initial 3D camera view and projection ---
            _viewHeight = _global.DiceCameraZoom; // Default zoom (e.g., 20f)
            _viewWidth = _viewHeight * aspectRatio;

            // The camera is positioned above the center of the large, static physics world.
            var cameraPosition = new Microsoft.Xna.Framework.Vector3(_physicsWorldWidth / 2f, _global.DiceCameraHeight, _physicsWorldHeight / 2f);
            var cameraTarget = new Microsoft.Xna.Framework.Vector3(_physicsWorldWidth / 2f, 0, _physicsWorldHeight / 2f);
            _view = Matrix.CreateLookAt(cameraPosition, cameraTarget, Microsoft.Xna.Framework.Vector3.Forward);

            // Create an orthographic projection that matches the initial view size. This will be updated per-roll.
            _projection = Matrix.CreateOrthographic(
                _viewWidth,  // The calculated width of the area to view
                _viewHeight, // The chosen height of the area to view
                1f,          // Near clipping plane
                200f);       // Far clipping plane

            // Set the initial positions of the kinematic walls to match the default view.
            _physicsWorld.UpdateBoundaryPositions(_viewWidth, _viewHeight);
        }

        /// <summary>
        /// Clears existing dice and rolls a new set based on a list of dice groups.
        /// </summary>
        /// <param name="rollGroups">A list of DiceGroup objects, each defining a set of dice to roll.</param>
        public void Roll(List<DiceGroup> rollGroups)
        {
            // --- Dynamic Camera Zoom ---
            int totalDice = rollGroups.Sum(g => g.NumberOfDice);

            float requiredZoom;
            if (totalDice <= 8)
            {
                requiredZoom = 20f;
            }
            else if (totalDice <= 20)
            {
                requiredZoom = 30f;
            }
            else
            {
                requiredZoom = 40f;
            }

            // Update the camera's view dimensions and projection matrix for the current roll.
            float aspectRatio = (float)_renderTarget.Width / _renderTarget.Height;
            _viewHeight = requiredZoom;
            _viewWidth = _viewHeight * aspectRatio;

            // Update the projection matrix to change the zoom level. This is a cheap operation.
            _projection = Matrix.CreateOrthographic(
                _viewWidth,  // Use the new calculated width
                _viewHeight, // Use the new calculated height
                1f,          // Near clipping plane
                200f);       // Far clipping plane

            // Move the kinematic walls to match the new view boundaries. This is also cheap.
            _physicsWorld.UpdateBoundaryPositions(_viewWidth, _viewHeight);

            // --- Roll Setup ---
            // Clear previous roll data from the existing simulation.
            foreach (var handle in _bodyToDieMap.Keys)
            {
                _physicsWorld.RemoveBody(handle);
            }
            _bodyToDieMap.Clear();
            _renderableDice.Clear();

            // Reset failsafe mechanisms
            _rollInProgressTimer = 0f;
            _rerollAttempts = new Dictionary<RenderableDie, int>();
            _forcedResults = new Dictionary<RenderableDie, int>();
            _currentRollGroups = rollGroups;

            // Loop through the requested groups and spawn the dice for each.
            foreach (var group in rollGroups)
            {
                for (int i = 0; i < group.NumberOfDice; i++)
                {
                    // Create a renderable die, passing the group's properties.
                    var renderableDie = new RenderableDie(_dieModel, _dieColliderVertices, _global.DiceDebugAxisLineSize, group.Tint, group.GroupId);
                    _renderableDice.Add(renderableDie);

                    // Create a corresponding physics body by throwing it from off-screen
                    var handle = ThrowDieFromOffscreen(renderableDie);
                    _bodyToDieMap.Add(handle, renderableDie);
                }
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
            // Spawning parameters are now pulled from global settings.
            float offscreenMargin = _global.DiceSpawnOffscreenMargin;
            float spawnHeightMin = _global.DiceSpawnHeightMin;
            float spawnHeightMax = _global.DiceSpawnHeightMax;
            float spawnEdgePadding = _global.DiceSpawnEdgePadding;

            System.Numerics.Vector3 spawnPos;
            int side = _random.Next(4); // 0: Left, 1: Right, 2: Top, 3: Bottom

            // Calculate the bounds of the current visible area within the larger physics world.
            float centerX = _physicsWorldWidth / 2f;
            float centerZ = _physicsWorldHeight / 2f;
            float visibleMinX = centerX - _viewWidth / 2f;
            float visibleMaxX = centerX + _viewWidth / 2f;
            float visibleMinZ = centerZ - _viewHeight / 2f;
            float visibleMaxZ = centerZ + _viewHeight / 2f;

            switch (side)
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

            // PRIMARY FAILSAFE: Instantly detect and replace any missing dice
            // This check is only necessary if a roll is supposed to be in progress.
            if (_renderableDice.Any())
            {
                int expectedDiceCount = _renderableDice.Count;
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
                _rollInProgressTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
            }
            else
            {
                _rollInProgressTimer = 0f;
            }

            // SECONDARY FAILSAFE: If the roll takes too long, check for and handle any stuck dice 
            if (_rollInProgressTimer > _global.DiceRollTimeout)
            {
                HandleStuckDice();
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
                                var newHandle = ThrowDieFromOffscreen(renderableDie);
                                _bodyToDieMap.Add(newHandle, renderableDie);
                            }
                            _rollInProgressTimer = 0f; // Reset timeout since we initiated a re-roll.
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

                                    // Nudge forces are now pulled from global settings for fine-tuning.
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
                                _rollInProgressTimer = 0f; // Reset timeout since we initiated a re-roll.
                            }
                            else
                            {
                                // === All checks passed: The roll is complete ===
                                var result = new DiceRollResult();
                                var rawResults = new Dictionary<string, List<int>>();

                                // 1. Gather raw face values from all dice, including forced ones.
                                foreach (var die in _renderableDice)
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
            var missingDice = _renderableDice.Where(d => !activeRenderableDice.Contains(d) && !_forcedResults.ContainsKey(d)).ToList();

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
                // The die's physics body is already removed (or was never there).
                // We just record the forced result. The die will no longer be updated or drawn.
                _forcedResults[die] = _global.DiceForcedResultValue;
            }
            else
            {
                // Re-throw the die for another attempt.
                var newHandle = ThrowDieFromOffscreen(die);
                _bodyToDieMap.Add(newHandle, die);
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

                foreach (var die in _renderableDice)
                {
                    // Also skip drawing debug info for culled dice.
                    if (_forcedResults.ContainsKey(die))
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
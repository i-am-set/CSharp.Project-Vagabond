using BepuPhysics;
using BepuPhysics.Collidables;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using ProjectVagabond.Physics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using BepuUtilities.Memory;
using ProjectVagabond.Particles;
using MonoGame.Extended.BitmapFonts;
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
            ShiftingSums,
            GatheringResults,
            SpawningNewSum,
            PostSumDelay,
            ApplyingMultipliers,
            FinalSumHold,
            SequentialFadeOut,
            Complete
        }

        // A small helper class to manage the animated numbers shown during enumeration.
        private class FloatingResultText
        {
            public enum TextType { IndividualDie, GroupSum, Multiplier, Modifier }

            public string Text;
            public Vector2 StartPosition;
            public Vector2 TargetPosition;
            public Vector2 CurrentPosition;
            public Vector2 ShakeOffset;
            public Color CurrentColor;
            public Color TintColor;
            public float Scale;
            public float Rotation; // For pivot shake animation
            public TextType Type;
            public string GroupId; // Links multipliers/modifiers to their sum
            public float Age;
            public float Lifetime;
            public float AnimationProgress;
            public bool IsAnimating;
            public bool ShouldPopOnAnimate;
            public bool ImpactEffectTriggered;
            public bool IsAnimatingScale;
            public bool IsVisible; // Controls whether the text is rendered
            public bool IsFadingOut;
            public float FadeOutProgress;

            // For collision animations
            public bool IsAwaitingCollision;
            public bool IsColliding;
            public float CollisionProgress;
        }

        // Core Components
        private GraphicsDevice _graphicsDevice;
        private PhysicsWorld _physicsWorld;
        private RenderTarget2D _renderTarget;
        private readonly Random _random = new Random();
        private readonly Global _global;

        // Rendering & Object Pooling
        private const int InitialPoolSize = 10;
        private readonly List<RenderableDie> _activeDice = new List<RenderableDie>();
        private readonly List<RenderableDie> _diePool = new List<RenderableDie>();
        private Model _d6Model;
        private Texture2D _d6Texture;
        private Model _d4Model;
        private Texture2D _d4Texture;
        private List<System.Numerics.Vector3> _d4ModelVertices;


        // Particle Effects
        private ParticleSystemManager _particleManager;
        private ParticleEmitter _sparkEmitter;
        private ParticleEmitter _sumImpactEmitter;
        private SpriteBatch _particleSpriteBatch;

        // Shared Debug Resources
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
        private readonly Dictionary<(DieType, float), (TypedIndex ShapeIndex, BodyInertia Inertia, List<System.Numerics.Vector3> Vertices)> _shapeCache = new();
        private List<DiceGroup> _currentRollGroups;

        // State Tracking
        private RollState _currentState = RollState.Idle;
        private float _settleTimer = 0f;

        // Failsafe State
        private float _rollInProgressTimer;
        private readonly Dictionary<RenderableDie, int> _rerollAttempts = new Dictionary<RenderableDie, int>();
        private readonly Dictionary<RenderableDie, int> _forcedResults = new Dictionary<RenderableDie, int>();
        private int _completeRerollAttempts;

        // State for handling group-by-group enumeration and animation
        private readonly Queue<string> _displayGroupQueue = new Queue<string>();
        private string _currentDisplayGroupId;
        private List<DiceGroup> _currentGroupsForDisplay;
        private readonly Queue<RenderableDie> _enumerationQueue = new Queue<RenderableDie>();
        private RenderableDie _currentlyEnumeratingDie;
        private float _animationTimer;
        private int _currentGroupSum;
        private readonly List<FloatingResultText> _floatingResults = new List<FloatingResultText>();
        private readonly List<FloatingResultText> _groupSumResults = new List<FloatingResultText>();
        private readonly List<FloatingResultText> _activeModifiers = new List<FloatingResultText>();
        private int _fadingSumIndex;


        /// <summary>
        /// If true, the physics colliders will be rendered as debug visuals.
        /// Can be toggled at runtime.
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

        public DiceRollingSystem()
        {
            _global = ServiceLocator.Get<Global>();
        }

        /// <summary>
        /// Loads content and sets up the physics world.
        /// </summary>
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

            // Load models and textures for each die type
            _d6Model = content.Load<Model>("Models/die");
            _d6Texture = content.Load<Texture2D>("Textures/die_texture");
            _d4Model = content.Load<Model>("Models/die_d4");
            _d4Texture = content.Load<Texture2D>("Textures/die_d4_texture");

            // Apply D6 texture to D6 model
            foreach (var mesh in _d6Model.Meshes)
            {
                foreach (var part in mesh.MeshParts)
                {
                    if (part.Effect is BasicEffect effect)
                    {
                        effect.Texture = _d6Texture;
                    }
                }
            }

            // Apply D4 texture to D4 model
            foreach (var mesh in _d4Model.Meshes)
            {
                foreach (var part in mesh.MeshParts)
                {
                    if (part.Effect is BasicEffect effect)
                    {
                        effect.Texture = _d4Texture;
                    }
                }
            }

            _d4ModelVertices = new List<System.Numerics.Vector3>();
            var uniqueVertices = new HashSet<Microsoft.Xna.Framework.Vector3>();
            foreach (var mesh in _d4Model.Meshes)
            {
                foreach (var part in mesh.MeshParts)
                {
                    var vertices = new VertexPositionNormalTexture[part.NumVertices];
                    part.VertexBuffer.GetData(part.VertexOffset * part.VertexBuffer.VertexDeclaration.VertexStride, vertices, 0, part.NumVertices, part.VertexBuffer.VertexDeclaration.VertexStride);
                    foreach (var vertex in vertices)
                    {
                        uniqueVertices.Add(vertex.Position);
                    }
                }
            }
            foreach (var vertex in uniqueVertices)
            {
                _d4ModelVertices.Add(new System.Numerics.Vector3(vertex.X, vertex.Y, vertex.Z));
            }

            float aspectRatio = (float)_renderTarget.Width / _renderTarget.Height;

            const float maxCameraZoom = 40f;
            _physicsWorldHeight = maxCameraZoom;
            _physicsWorldWidth = _physicsWorldHeight * aspectRatio;
            _physicsWorld = new PhysicsWorld(_physicsWorldWidth, _physicsWorldHeight);

            // Pre-cache the default D6 shape.
            CacheShapeForScale(DieType.D6, 1.0f);

            // Pre-populate the object pool to avoid allocations during gameplay.
            for (int i = 0; i < InitialPoolSize; i++)
            {
                _diePool.Add(new RenderableDie(_graphicsDevice, Color.White, ""));
            }

            // Set up the 3D camera.
            _viewHeight = _global.DiceCameraZoom;
            _viewWidth = _viewHeight * aspectRatio;

            var cameraPosition = new Microsoft.Xna.Framework.Vector3(_physicsWorldWidth / 2f, _global.DiceCameraHeight, _physicsWorldHeight / 2f);
            var cameraTarget = new Microsoft.Xna.Framework.Vector3(_physicsWorldWidth / 2f, 0, _physicsWorldHeight / 2f);
            _view = Matrix.CreateLookAt(cameraPosition, cameraTarget, Microsoft.Xna.Framework.Vector3.Forward);

            _projection = Matrix.CreateOrthographic(_viewWidth, _viewHeight, 1f, 200f);
            _physicsWorld.UpdateBoundaryPositions(_viewWidth, _viewHeight);

            // Initialize particle systems for visual effects.
            _particleManager = new ParticleSystemManager();
            _sparkEmitter = _particleManager.CreateEmitter(ParticleEffects.CreateSparks());
            _sumImpactEmitter = _particleManager.CreateEmitter(ParticleEffects.CreateSumImpact());
            _particleSpriteBatch = new SpriteBatch(_graphicsDevice);
            EventBus.Subscribe<GameEvents.DiceCollisionOccurred>(HandleDiceCollision);

            // Create shared debug resources once to prevent re-allocations.
            _debugEffect = new BasicEffect(_graphicsDevice)
            {
                VertexColorEnabled = true,
                LightingEnabled = false,
                TextureEnabled = false
            };
            float debugAxisSize = _global.DiceDebugAxisLineSize;
            _debugAxisVertices = new[]
            {
                new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(-debugAxisSize, 0, 0), Color.Red),
                new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(debugAxisSize, 0, 0), Color.Red),
                new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(0, -debugAxisSize, 0), Color.Green),
                new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(0, debugAxisSize, 0), Color.Green),
                new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(0, 0, -debugAxisSize), Color.Blue),
                new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(0, 0, debugAxisSize), Color.Blue)
            };
        }

        /// <summary>
        /// Creates and caches a physics shape and inertia for a given die type and scale if it doesn't already exist.
        /// </summary>
        private void CacheShapeForScale(DieType dieType, float scale)
        {
            if (_shapeCache.ContainsKey((dieType, scale)))
            {
                return;
            }

            var points = new List<System.Numerics.Vector3>();
            ConvexHull dieShape;

            switch (dieType)
            {
                case DieType.D4:
                    // A perfect tetrahedron has infinitely sharp points, which are unstable for physics solvers.
                    // We "shave off" the points by creating new vertices along the edges leading to each point.
                    // This creates small, flat triangular faces in place of the sharp vertices, which is much more stable.
                    var beveledPoints = new List<System.Numerics.Vector3>();
                    var originalVertices = new List<System.Numerics.Vector3>();
                    foreach (var vertex in _d4ModelVertices)
                    {
                        originalVertices.Add(vertex * scale);
                    }

                    float bevelRatio = _global.DiceD4ColliderBevelRatio;

                    // For each vertex, find the edges connected to it and create new points along them.
                    for (int i = 0; i < originalVertices.Count; i++)
                    {
                        var currentVertex = originalVertices[i];
                        for (int j = 0; j < originalVertices.Count; j++)
                        {
                            if (i == j) continue;
                            var otherVertex = originalVertices[j];
                            // Create a new point beveled away from the current vertex towards the other.
                            var newPoint = currentVertex + (otherVertex - currentVertex) * bevelRatio;
                            beveledPoints.Add(newPoint);
                        }
                    }
                    points.AddRange(beveledPoints.Distinct()); // Use distinct to avoid duplicate points
                    break;

                case DieType.D6:
                default:
                    // A beveled cube's vertices.
                    float size = _global.DiceColliderSize * scale;
                    float bevelAmount = size * _global.DiceColliderBevelRatio;
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
                    break;
            }

            dieShape = new ConvexHull(points.ToArray(), _physicsWorld.BufferPool, out _);
            float scaledMass = _global.DiceMass * (scale * scale * scale);
            var dieInertia = dieShape.ComputeInertia(scaledMass);
            var dieShapeIndex = _physicsWorld.Simulation.Shapes.Add(dieShape);

            _shapeCache[(dieType, scale)] = (dieShapeIndex, dieInertia, points);
        }

        /// <summary>
        /// Clears existing dice and rolls a new set based on a list of dice groups.
        /// </summary>
        public void Roll(List<DiceGroup> rollGroups)
        {
            // Return all previously used dice to the object pool and clear the simulation.
            foreach (var pair in _bodyToDieMap)
            {
                _physicsWorld.RemoveBody(pair.Key);
                pair.Value.Reset();
                _diePool.Add(pair.Value);
            }
            _activeDice.Clear();
            _bodyToDieMap.Clear();

            // Reset all state tracking variables for the new roll.
            _rollInProgressTimer = 0f;
            _rerollAttempts.Clear();
            _forcedResults.Clear();
            _completeRerollAttempts = 0;
            _currentRollGroups = rollGroups;
            _floatingResults.Clear();
            _groupSumResults.Clear();
            _activeModifiers.Clear();
            _enumerationQueue.Clear();
            _displayGroupQueue.Clear();
            _currentlyEnumeratingDie = null;
            _currentDisplayGroupId = null;
            _currentGroupsForDisplay = null;

            // Get unique display group IDs, preserving order, and queue them for processing.
            var uniqueDisplayGroupIds = rollGroups
                .Select(g => g.DisplayGroupId ?? g.GroupId) // Fallback to GroupId if DisplayGroupId is null
                .Distinct()
                .ToList();

            foreach (var displayId in uniqueDisplayGroupIds)
            {
                _displayGroupQueue.Enqueue(displayId);
            }

            // Ensure a physics shape is cached for every type/scale combination in the roll.
            foreach (var group in rollGroups)
            {
                CacheShapeForScale(group.DieType, group.Scale);
            }

            // Dynamically adjust camera zoom based on the number of dice.
            int totalDice = rollGroups.Sum(g => g.NumberOfDice);
            float requiredZoom = totalDice <= 8 ? 20f : (totalDice <= 20 ? 30f : 40f);
            float aspectRatio = (float)_renderTarget.Width / _renderTarget.Height;
            _viewHeight = requiredZoom;
            _viewWidth = _viewHeight * aspectRatio;

            _projection = Matrix.CreateOrthographic(_viewWidth, _viewHeight, 1f, 200f);
            _physicsWorld.UpdateBoundaryPositions(_viewWidth, _viewHeight);

            // Spawn dice for each group, pulling from the object pool.
            foreach (var group in rollGroups)
            {
                int spawnSideForGroup = _random.Next(4);

                for (int i = 0; i < group.NumberOfDice; i++)
                {
                    RenderableDie renderableDie;

                    if (_diePool.Count > 0)
                    {
                        renderableDie = _diePool.Last();
                        _diePool.RemoveAt(_diePool.Count - 1);
                    }
                    else
                    {
                        // The pool is empty; create a new die instance as a fallback.
                        renderableDie = new RenderableDie(_graphicsDevice, Color.White, "");
                    }

                    renderableDie.GroupId = group.GroupId;
                    renderableDie.Tint = group.Tint;
                    renderableDie.BaseScale = group.Scale;
                    renderableDie.DieType = group.DieType; // Assign the die type
                    renderableDie.CurrentModel = group.DieType == DieType.D4 ? _d4Model : _d6Model; // Assign the correct model
                    _activeDice.Add(renderableDie);

                    var handle = ThrowDieFromOffscreen(renderableDie, group, spawnSideForGroup);
                    _bodyToDieMap.Add(handle, renderableDie);
                }
            }

            _currentState = RollState.Rolling;
        }

        /// <summary>
        /// Creates a physics body for a die, positions it off-screen, and gives it velocity to enter the view.
        /// </summary>
        private BodyHandle ThrowDieFromOffscreen(RenderableDie renderableDie, DiceGroup group, int spawnSide)
        {
            float offscreenMargin = _global.DiceSpawnOffscreenMargin;
            float spawnHeightMin = _global.DiceSpawnHeightMin;
            float spawnHeightMax = _global.DiceSpawnHeightMax;
            float spawnEdgePadding = _global.DiceSpawnEdgePadding;

            System.Numerics.Vector3 spawnPos;

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

            var targetPos = new System.Numerics.Vector3(centerX, 0, centerZ);
            var direction = System.Numerics.Vector3.Normalize(targetPos - spawnPos);
            float throwForce = (float)(_random.NextDouble() * (_global.DiceThrowForceMax - _global.DiceThrowForceMin) + _global.DiceThrowForceMin);

            var shapeData = _shapeCache[(group.DieType, group.Scale)];

            var collidable = new CollidableDescription(shapeData.ShapeIndex, 0.01f);

            var bodyDescription = BodyDescription.CreateDynamic(
                spawnPos,
                shapeData.Inertia,
                collidable,
                new BodyActivityDescription(0.01f));

            bodyDescription.Collidable.Continuity = new ContinuousDetection
            {
                Mode = ContinuousDetectionMode.Continuous,
                MinimumSweepTimestep = 1e-5f,
                SweepConvergenceThreshold = 1e-5f
            };
            double u1 = _random.NextDouble();
            double u2 = _random.NextDouble();
            double u3 = _random.NextDouble();

            double sqrt1MinusU1 = Math.Sqrt(1 - u1);
            double sqrtU1 = Math.Sqrt(u1);

            float qx = (float)(sqrt1MinusU1 * Math.Sin(2 * Math.PI * u2));
            float qy = (float)(sqrt1MinusU1 * Math.Cos(2 * Math.PI * u2));
            float qz = (float)(sqrtU1 * Math.Sin(2 * Math.PI * u3));
            float qw = (float)(sqrtU1 * Math.Cos(2 * Math.PI * u3));

            bodyDescription.Pose.Orientation = new System.Numerics.Quaternion(qx, qy, qz, qw);

            bodyDescription.Velocity.Linear = direction * throwForce;
            float maxAngVel = _global.DiceInitialAngularVelocityMax;
            bodyDescription.Velocity.Angular = new System.Numerics.Vector3(
                (float)(_random.NextDouble() * maxAngVel * 2 - maxAngVel),
                (float)(_random.NextDouble() * maxAngVel * 2 - maxAngVel),
                (float)(_random.NextDouble() * maxAngVel * 2 - maxAngVel));

            return _physicsWorld.AddBody(bodyDescription);
        }

        /// <summary>
        /// Advances the physics simulation by one fixed time step.
        /// </summary>
        public void PhysicsStep(float deltaTime)
        {
            _physicsWorld.Update(deltaTime);
        }

        /// <summary>
        /// Updates the visual models and game logic based on the current physics state.
        /// </summary>
        public void Update(GameTime gameTime)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            _particleManager.Update(gameTime);
            UpdateFloatingResults(gameTime);

            // Synchronize the visual model's transform with its physics body's transform.
            foreach (var pair in _bodyToDieMap)
            {
                var body = _physicsWorld.Simulation.Bodies.GetBodyReference(pair.Key);
                var pose = body.Pose;
                var position = new Microsoft.Xna.Framework.Vector3(pose.Position.X, pose.Position.Y, pose.Position.Z);
                // --- MODIFIED: Corrected the quaternion component mapping. ---
                // The previous version had incorrect swizzling (Y, Z, W, X), which would cause visual
                // and logical errors. This restores the correct 1-to-1 mapping.
                var orientation = new XnaQuaternion(pose.Orientation.X, pose.Orientation.Y, pose.Orientation.Z, pose.Orientation.W);
                // --- END MODIFIED ---
                pair.Value.World = Matrix.CreateFromQuaternion(orientation) * Matrix.CreateTranslation(position);
            }

            // The core state machine for the dice rolling process.
            switch (_currentState)
            {
                case RollState.Idle:
                case RollState.Complete:
                    return;

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

                case RollState.ShiftingSums:
                    UpdateShiftingSumsState(deltaTime);
                    break;

                case RollState.GatheringResults:
                    UpdateGatheringState(deltaTime);
                    break;

                case RollState.SpawningNewSum:
                    UpdateSpawningNewSumState(deltaTime);
                    break;

                case RollState.PostSumDelay:
                    UpdatePostSumDelayState(deltaTime);
                    break;

                case RollState.ApplyingMultipliers:
                    UpdateApplyingMultipliersState(deltaTime);
                    break;

                case RollState.FinalSumHold:
                    UpdateFinalSumHoldState(deltaTime);
                    break;

                case RollState.SequentialFadeOut:
                    UpdateSequentialFadeOutState(deltaTime);
                    break;
            }
        }

        private void UpdateRollingState(float deltaTime)
        {
            // Failsafe 1: Check for any dice that may have fallen out of the simulation.
            if (_activeDice.Any())
            {
                int expectedDiceCount = _activeDice.Count;
                int activeDiceCount = _bodyToDieMap.Count + _forcedResults.Count;
                if (activeDiceCount < expectedDiceCount)
                {
                    HandleMissingDice();
                    return;
                }
            }

            bool isCurrentlyRolling = this.IsRolling;

            if (isCurrentlyRolling)
            {
                _rollInProgressTimer += deltaTime;
            }
            else
            {
                _rollInProgressTimer = 0f;
            }

            // Failsafe 3: If the entire roll is taking too long, re-roll everything.
            if (_rollInProgressTimer > _global.DiceCompleteRollTimeout)
            {
                HandleCompleteReroll();
                return;
            }

            // Failsafe 2: If the roll is taking a while, check for individual stuck dice.
            if (_rollInProgressTimer > _global.DiceRollTimeout)
            {
                HandleStuckDice();
                return;
            }

            // If dice have stopped moving, transition to the settling state.
            if (!isCurrentlyRolling)
            {
                _currentState = RollState.Settling;
                _settleTimer = 0f;
            }
        }

        private void UpdateSettlingState(float deltaTime)
        {
            // If a die starts moving again, go back to the rolling state.
            if (this.IsRolling)
            {
                _currentState = RollState.Rolling;
                _settleTimer = 0f;
                return;
            }

            _settleTimer += deltaTime;

            // Wait for a short delay after dice stop to ensure they are truly settled.
            if (_settleTimer >= _global.DiceSettleDelay)
            {
                // Check 1: Re-roll any dice that have ended up off-screen.
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
                        var group = _currentRollGroups.First(g => g.GroupId == renderableDie.GroupId);
                        var newHandle = ThrowDieFromOffscreen(renderableDie, group, _random.Next(4));
                        _bodyToDieMap.Add(newHandle, renderableDie);
                    }
                    _rollInProgressTimer = 0f;
                    _currentState = RollState.Rolling;
                }
                else
                {
                    // Check 2: Nudge any dice that are "canted" (resting on an edge or corner).
                    float rerollThreshold = _global.DiceCantingRerollThreshold;
                    var canteredDiceHandles = new List<BodyHandle>();

                    foreach (var pair in _bodyToDieMap)
                    {
                        var renderableDie = pair.Value;
                        List<System.Numerics.Vector3> vertices = null;
                        if (renderableDie.DieType == DieType.D4)
                        {
                            var shapeData = _shapeCache[(renderableDie.DieType, renderableDie.BaseScale)];
                            vertices = shapeData.Vertices;
                        }
                        var (_, alignment) = DiceResultHelper.GetFaceValueAndAlignment(renderableDie.DieType, renderableDie.World, vertices);
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
                        // All checks passed; the roll is valid. Begin counting the results.
                        StartNextDisplayGroupEnumeration();
                    }
                }
                _settleTimer = 0f;
            }
        }

        private void StartNextDisplayGroupEnumeration()
        {
            if (_displayGroupQueue.TryDequeue(out _currentDisplayGroupId))
            {
                _currentState = RollState.Enumerating;
                _currentGroupSum = 0;
                _animationTimer = 0f;
                _enumerationQueue.Clear();

                _currentGroupsForDisplay = _currentRollGroups
                    .Where(g => (g.DisplayGroupId ?? g.GroupId) == _currentDisplayGroupId)
                    .ToList();

                var groupIds = _currentGroupsForDisplay.Select(g => g.GroupId).ToHashSet();
                var diceInDisplayGroup = _activeDice
                    .Where(d => groupIds.Contains(d.GroupId))
                    .OrderBy(d => d.World.Translation.X)
                    .ToList();

                foreach (var die in diceInDisplayGroup)
                {
                    _enumerationQueue.Enqueue(die);
                }
                ProcessNextEnumerationStep();
            }
            else
            {
                // All display groups have been processed. Start applying multipliers.
                _currentState = RollState.ApplyingMultipliers;
                _animationTimer = 0f;
                PrepareMultipliers();
            }
        }

        private void UpdateEnumeratingState(float deltaTime)
        {
            _animationTimer += deltaTime;

            if (_currentlyEnumeratingDie != null)
            {
                float totalDuration = _global.DiceEnumerationStepDuration;
                float progress = Math.Clamp(_animationTimer / totalDuration, 0f, 1f);

                // Define the timing for each part of the animation sequence.
                const float popDuration = 0.15f;
                const float shrinkDuration = 0.10f;

                // Define phase boundaries as a percentage of the total duration.
                float popPhaseEnd = popDuration / totalDuration;
                float shrinkPhaseEnd = (popDuration + shrinkDuration) / totalDuration;

                var resultText = _floatingResults.LastOrDefault();

                if (progress < popPhaseEnd)
                {
                    // Phase 1: Die "pops" (scales up and down) and highlights.
                    float popProgress = progress / popPhaseEnd;
                    float scale;
                    float baseScale = 1.0f;
                    float scaleRange = _global.DiceEnumerationMaxScale - baseScale;

                    if (popProgress < 0.5f)
                    {
                        scale = baseScale + scaleRange * Easing.EaseOutCubic(popProgress * 2f);
                    }
                    else
                    {
                        scale = baseScale + scaleRange * (1 - Easing.EaseInCubic((popProgress - 0.5f) * 2f));
                    }
                    _currentlyEnumeratingDie.VisualScale = scale;
                    _currentlyEnumeratingDie.IsHighlighted = true;
                    _currentlyEnumeratingDie.HighlightColor = _animationTimer < _global.DiceEnumerationFlashDuration ? Color.White : _currentlyEnumeratingDie.Tint;
                }
                else if (progress < shrinkPhaseEnd)
                {
                    // Phase 2: Die shrinks to nothing.
                    float shrinkProgress = (progress - popPhaseEnd) / (shrinkPhaseEnd - popPhaseEnd);
                    _currentlyEnumeratingDie.IsHighlighted = false;
                    _currentlyEnumeratingDie.VisualScale = 1.0f - Easing.EaseInQuint(shrinkProgress);
                }
                else
                {
                    // Phase 3: Result number expands into view.
                    float expandProgress = (progress - shrinkPhaseEnd) / (1.0f - shrinkPhaseEnd);
                    if (resultText != null && resultText.IsAnimatingScale)
                    {
                        const float targetScale = 4.0f;
                        resultText.Scale = targetScale * Easing.EaseOutCubic(expandProgress);
                    }
                }
            }

            // Check if the animation for the current die is complete.
            if (_animationTimer >= _global.DiceEnumerationStepDuration)
            {
                if (_currentlyEnumeratingDie != null)
                {
                    // Finalize the state to ensure it's correct before moving on.
                    _currentlyEnumeratingDie.VisualScale = 0f;
                    _currentlyEnumeratingDie.IsDespawned = true;

                    var resultText = _floatingResults.LastOrDefault();
                    if (resultText != null)
                    {
                        resultText.Scale = 4.0f;
                        resultText.IsAnimatingScale = false;
                    }
                }

                _animationTimer = 0f;
                ProcessNextEnumerationStep(); // Move to the next die in the queue.
            }
        }

        private void ProcessNextEnumerationStep()
        {
            if (_enumerationQueue.TryDequeue(out _currentlyEnumeratingDie))
            {
                List<System.Numerics.Vector3> vertices = null;
                if (_currentlyEnumeratingDie.DieType == DieType.D4)
                {
                    vertices = _shapeCache[(_currentlyEnumeratingDie.DieType, _currentlyEnumeratingDie.BaseScale)].Vertices;
                }
                int dieValue = _forcedResults.TryGetValue(_currentlyEnumeratingDie, out int forcedValue)
                    ? forcedValue
                    : DiceResultHelper.GetFaceValue(_currentlyEnumeratingDie.DieType, _currentlyEnumeratingDie.World, vertices);

                _currentGroupSum += dieValue;

                var dieWorldPos = _currentlyEnumeratingDie.World.Translation;
                var viewport = new Viewport(_renderTarget.Bounds);
                var dieScreenPos = viewport.Project(dieWorldPos, _projection, _view, Matrix.Identity);
                var dieScreenPos2D = new Vector2(dieScreenPos.X, dieScreenPos.Y);

                // Create the text object, starting it at scale 0, ready for its animation phase.
                _floatingResults.Add(new FloatingResultText
                {
                    Text = dieValue.ToString(),
                    StartPosition = dieScreenPos2D,
                    TargetPosition = dieScreenPos2D,
                    CurrentPosition = dieScreenPos2D,
                    Scale = 0.0f,
                    Age = 0f,
                    Lifetime = _global.DiceGatheringDuration,
                    Type = FloatingResultText.TextType.IndividualDie,
                    IsAnimatingScale = true,
                    CurrentColor = Color.White, // Set initial color to solid white
                    IsVisible = true
                });
            }
            else
            {
                // All dice in the current group have been counted.
                _currentlyEnumeratingDie = null;
                _currentState = RollState.PostEnumerationDelay;
                _animationTimer = 0f;
            }
        }

        private void UpdatePostEnumerationDelayState(float deltaTime)
        {
            _animationTimer += deltaTime;
            if (_animationTimer >= _global.DicePostEnumerationDelay)
            {
                // After the delay, create the sum object (invisibly) and calculate final positions.
                var font = ServiceLocator.Get<BitmapFont>();

                // Calculate the final sum, including all modifiers from the constituent groups.
                int totalModifier = _currentGroupsForDisplay.Sum(g => g.Modifier);
                int finalSum = _currentGroupSum + totalModifier;
                string newSumText = finalSum.ToString();

                // Create the new sum object now to ensure its data is not lost.
                var newSum = new FloatingResultText
                {
                    Text = newSumText,
                    Type = FloatingResultText.TextType.GroupSum,
                    GroupId = _currentDisplayGroupId,
                    TintColor = _currentGroupsForDisplay.First().Tint,
                    Scale = 3.5f,
                    IsVisible = false // It starts invisible.
                };

                // Create a temporary list with all sums to calculate the final layout.
                var allSumsForLayout = _groupSumResults.Concat(new[] { newSum }).ToList();

                float totalWidth = 0;
                const float padding = 50f;
                foreach (var sum in allSumsForLayout)
                {
                    totalWidth += (font.MeasureString(sum.Text).Width * sum.Scale) + padding;
                }
                totalWidth -= padding;

                float currentX = (_renderTarget.Width / 2f) - (totalWidth / 2f);

                // Set the final target positions for all sums.
                foreach (var sum in allSumsForLayout)
                {
                    float textWidth = font.MeasureString(sum.Text).Width * sum.Scale;
                    sum.StartPosition = sum.CurrentPosition; // Store current pos for animation.
                    sum.TargetPosition = new Vector2(currentX + textWidth / 2f, _renderTarget.Height / 2f);
                    sum.IsAnimating = true;
                    sum.AnimationProgress = 0f;
                    sum.ShouldPopOnAnimate = false;
                    currentX += textWidth + padding;
                }

                // Officially add the new (still invisible) sum to the list.
                _groupSumResults.Add(newSum);

                _animationTimer = 0f;
                _currentState = RollState.ShiftingSums;
            }
        }

        private void UpdateShiftingSumsState(float deltaTime)
        {
            _animationTimer += deltaTime;
            float progress = Math.Clamp(_animationTimer / _global.DiceSumShiftDuration, 0f, 1f);

            // Animate only the already visible sums sliding to their new positions.
            foreach (var sum in _groupSumResults)
            {
                if (sum.IsVisible && sum.IsAnimating)
                {
                    sum.AnimationProgress = progress;
                    if (progress >= 1.0f)
                    {
                        sum.IsAnimating = false;
                    }
                }
            }

            if (progress >= 1.0f)
            {
                // Now that space has been made, the individual results can gather.
                _animationTimer = 0f;
                _currentState = RollState.GatheringResults;
            }
        }

        private void UpdateGatheringState(float deltaTime)
        {
            _animationTimer += deltaTime;
            float progress = Math.Clamp(_animationTimer / _global.DiceGatheringDuration, 0f, 1f);
            float easedProgress = Easing.EaseInQuint(progress);

            // The target is the empty spot where the new sum will appear.
            var targetPosition = _groupSumResults.Last().TargetPosition;

            // Animate each result number moving to the target and shrinking.
            foreach (var result in _floatingResults)
            {
                result.CurrentPosition = Vector2.Lerp(result.StartPosition, targetPosition, easedProgress);
                result.Scale = MathHelper.Lerp(4.0f, 0.0f, easedProgress);
            }

            if (progress >= 1.0f)
            {
                // Once all numbers have converged and vanished, spawn the sum.
                _floatingResults.Clear();
                _animationTimer = 0f;
                _currentState = RollState.SpawningNewSum;
            }
        }

        private void UpdateSpawningNewSumState(float deltaTime)
        {
            _animationTimer += deltaTime;
            float progress = Math.Clamp(_animationTimer / _global.DiceNewSumAnimationDuration, 0f, 1f);

            // The sum object already exists, we just need to make it visible and animate it.
            var newSum = _groupSumResults.Last();

            // On the first frame, make it visible and set up its animation properties.
            if (!newSum.IsVisible)
            {
                newSum.IsVisible = true;
                newSum.CurrentPosition = newSum.TargetPosition; // It appears directly at its final spot
                newSum.StartPosition = newSum.TargetPosition;
                newSum.IsAnimating = true;
                newSum.ShouldPopOnAnimate = true;
                newSum.AnimationProgress = 0f;
            }

            // Animate the pop-in of the new sum.
            if (newSum.IsAnimating)
            {
                newSum.AnimationProgress = progress;
                if (progress >= 1.0f)
                {
                    newSum.IsAnimating = false;
                }
            }

            if (progress >= 1.0f)
            {
                // Transition to the final delay before the next group starts.
                _animationTimer = 0f;
                _currentState = RollState.PostSumDelay;
            }
        }

        private void UpdatePostSumDelayState(float deltaTime)
        {
            _animationTimer += deltaTime;
            if (_animationTimer >= _global.DicePostSumDelayDuration)
            {
                // Delay is over; start processing the next group of dice.
                StartNextDisplayGroupEnumeration();
            }
        }

        private void PrepareMultipliers()
        {
            _activeModifiers.Clear();
            foreach (var sum in _groupSumResults)
            {
                var groupsForSum = _currentRollGroups.Where(g => (g.DisplayGroupId ?? g.GroupId) == sum.GroupId).ToList();
                var groupWithMultiplier = groupsForSum.FirstOrDefault(g => g.Multiplier != 1.0f);

                if (groupWithMultiplier != null)
                {
                    var multiplierText = new FloatingResultText
                    {
                        Text = $"x{groupWithMultiplier.Multiplier:0.0#}",
                        Type = FloatingResultText.TextType.Multiplier,
                        GroupId = sum.GroupId,
                        Scale = 0f, // Start at scale 0 for pop-in
                        TintColor = _global.Palette_Red,
                        IsVisible = true,
                        StartPosition = sum.CurrentPosition + new Vector2(0, -60),
                        TargetPosition = sum.CurrentPosition,
                        CurrentPosition = sum.CurrentPosition + new Vector2(0, -60),
                        IsAnimating = true,
                        AnimationProgress = 0f
                    };
                    _activeModifiers.Add(multiplierText);
                    sum.IsAwaitingCollision = true;
                }
            }

            if (!_activeModifiers.Any())
            {
                // No multipliers to apply, skip to the final hold.
                _currentState = RollState.FinalSumHold;
                _animationTimer = 0f;
            }
        }

        private void UpdateApplyingMultipliersState(float deltaTime)
        {
            _animationTimer += deltaTime;
            float progress = Math.Clamp(_animationTimer / _global.DiceMultiplierAnimationDuration, 0f, 1f);

            bool allDone = true;
            foreach (var multiplier in _activeModifiers.Where(m => m.Type == FloatingResultText.TextType.Multiplier))
            {
                if (!multiplier.IsVisible) continue;
                allDone = false;
                multiplier.AnimationProgress = progress;
            }

            if (progress >= 1.0f && !allDone)
            {
                // This ensures the collision is triggered only once when the animation completes.
                foreach (var multiplier in _activeModifiers.Where(m => m.Type == FloatingResultText.TextType.Multiplier && m.IsVisible))
                {
                    multiplier.IsVisible = false;
                    var targetSum = _groupSumResults.FirstOrDefault(s => s.GroupId == multiplier.GroupId);
                    if (targetSum != null)
                    {
                        targetSum.IsColliding = true;
                        targetSum.CollisionProgress = 0f;
                    }
                }
            }

            // The state transitions once all sums have finished their collision animations.
            if (allDone && _groupSumResults.All(s => !s.IsColliding))
            {
                _currentState = RollState.FinalSumHold;
                _animationTimer = 0f;
            }
        }

        private void UpdateFinalSumHoldState(float deltaTime)
        {
            _animationTimer += deltaTime;
            if (_animationTimer >= _global.DiceFinalSumLifetime)
            {
                _currentState = RollState.SequentialFadeOut;
                _animationTimer = 0f; // Reset timer for the sequential delay
                _fadingSumIndex = 0;
            }
        }

        private void UpdateSequentialFadeOutState(float deltaTime)
        {
            // If all sums have started fading, we just wait for them to finish.
            if (_fadingSumIndex >= _groupSumResults.Count)
            {
                if (!_groupSumResults.Any(s => s.IsFadingOut))
                {
                    _currentState = RollState.Complete;
                    FinalizeAndReportResults();
                }
                return;
            }

            _animationTimer += deltaTime;

            // Check if it's time to trigger the next fade-out.
            if (_animationTimer >= _global.DiceFinalSumSequentialFadeDelay)
            {
                if (_fadingSumIndex < _groupSumResults.Count)
                {
                    _groupSumResults[_fadingSumIndex].IsFadingOut = true;
                    _fadingSumIndex++;
                    _animationTimer = 0f; // Reset delay for the next sum.
                }
            }
        }

        private void UpdateFloatingResults(GameTime gameTime)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            var allResults = _floatingResults.Concat(_groupSumResults).Concat(_activeModifiers).ToList();

            for (int i = allResults.Count - 1; i >= 0; i--)
            {
                var result = allResults[i];
                result.Age += deltaTime;
                result.ShakeOffset = Vector2.Zero;
                result.Rotation = 0f;

                if (result.Type == FloatingResultText.TextType.Multiplier)
                {
                    if (result.IsAnimating)
                    {
                        const float popInDuration = 0.3f;
                        const float holdDuration = 0.5f;
                        float flyInDuration = _global.DiceMultiplierAnimationDuration - popInDuration - holdDuration;

                        float popInEnd = popInDuration / _global.DiceMultiplierAnimationDuration;
                        float holdEnd = (popInDuration + holdDuration) / _global.DiceMultiplierAnimationDuration;

                        if (result.AnimationProgress < popInEnd) // Phase 1: Pop-in
                        {
                            float phaseProgress = result.AnimationProgress / popInEnd;
                            result.Scale = 2.5f * Easing.EaseOutBack(phaseProgress);
                        }
                        else if (result.AnimationProgress < holdEnd) // Phase 2: Hold
                        {
                            result.Scale = 2.5f;
                        }
                        else // Phase 3: Fly-in
                        {
                            float phaseProgress = (result.AnimationProgress - holdEnd) / (1.0f - holdEnd);
                            float easedProgress = Easing.EaseInQuint(phaseProgress);
                            result.CurrentPosition = Vector2.Lerp(result.StartPosition, result.TargetPosition, easedProgress);
                            result.Scale = MathHelper.Lerp(2.5f, 0f, easedProgress);
                        }
                    }
                }
                else if (result.Type == FloatingResultText.TextType.GroupSum)
                {
                    if (result.IsFadingOut)
                    {
                        // Handle the shrinking/fade-out animation.
                        result.FadeOutProgress += deltaTime / _global.DiceFinalSumFadeOutDuration;
                        result.FadeOutProgress = Math.Clamp(result.FadeOutProgress, 0f, 1f);
                        float easedProgress = Easing.EaseInOutQuint(result.FadeOutProgress);
                        result.Scale = MathHelper.Lerp(3.5f, 0.0f, easedProgress);

                        if (result.FadeOutProgress >= 1.0f)
                        {
                            result.IsFadingOut = false; // Stop processing this animation
                            result.IsVisible = false;
                            // DO NOT remove from the list here. It disrupts the sequential fade logic.
                            // The list will be cleared at the start of the next roll.
                        }
                    }
                    else if (result.IsColliding)
                    {
                        result.CollisionProgress += deltaTime / 0.4f; // 0.4s collision animation
                        result.CollisionProgress = Math.Clamp(result.CollisionProgress, 0f, 1f);
                        float popProgress = result.CollisionProgress;
                        const float baseScale = 3.5f;
                        const float minScale = baseScale * 0.5f;

                        if (popProgress < 0.5f)
                        {
                            result.Scale = MathHelper.Lerp(baseScale, minScale, Easing.EaseInCubic(popProgress * 2f));
                        }
                        else
                        {
                            if (result.IsAwaitingCollision)
                            {
                                result.IsAwaitingCollision = false;
                                var groupsForSum = _currentRollGroups.Where(g => (g.DisplayGroupId ?? g.GroupId) == result.GroupId).ToList();
                                var groupWithMultiplier = groupsForSum.FirstOrDefault(g => g.Multiplier != 1.0f);
                                if (groupWithMultiplier != null)
                                {
                                    int currentVal = int.Parse(result.Text);
                                    float multipliedValue = currentVal * groupWithMultiplier.Multiplier;
                                    if (groupWithMultiplier.Multiplier < 1.0f)
                                    {
                                        currentVal = (int)Math.Floor(multipliedValue);
                                    }
                                    else
                                    {
                                        currentVal = (int)Math.Ceiling(multipliedValue);
                                    }
                                    result.Text = currentVal.ToString();
                                }
                            }
                            result.Scale = MathHelper.Lerp(minScale, baseScale, Easing.EaseOutBack((popProgress - 0.5f) * 2f));
                        }

                        if (result.CollisionProgress >= 1.0f)
                        {
                            result.IsColliding = false;
                            result.Scale = baseScale;
                        }
                    }
                    else if (result.IsAnimating)
                    {
                        // Animate the group sum text (position, scale, shake).
                        float easedProgress = Easing.EaseOutCirc(result.AnimationProgress);
                        result.CurrentPosition = Vector2.Lerp(result.StartPosition, result.TargetPosition, easedProgress);

                        if (result.ShouldPopOnAnimate)
                        {
                            float popProgress = result.AnimationProgress;
                            const float inflateEndTime = 0.2f;
                            const float holdEndTime = 0.7f;
                            const float baseScale = 3.5f;
                            const float maxScale = baseScale * 1.5f;

                            if (popProgress <= inflateEndTime)
                            {
                                float inflateProgress = popProgress / inflateEndTime;
                                result.Scale = baseScale + (maxScale - baseScale) * Easing.EaseOutCubic(inflateProgress);
                            }
                            else if (popProgress <= holdEndTime)
                            {
                                result.Scale = maxScale;
                                const float shakeAmount = 0.05f; // Radians for pivot shake
                                result.Rotation = (float)(_random.NextDouble() * 2 - 1) * shakeAmount;
                            }
                            else
                            {
                                float deflateProgress = (popProgress - holdEndTime) / (1.0f - holdEndTime);
                                result.Scale = maxScale - (maxScale - baseScale) * Easing.EaseInCubic(deflateProgress);
                            }
                        }
                    }
                    else
                    {
                        result.Scale = 3.5f;
                    }

                    if (result.ShouldPopOnAnimate && result.AnimationProgress >= 1.0f && !result.ImpactEffectTriggered)
                    {
                        _sumImpactEmitter.Position = result.CurrentPosition;
                        _sumImpactEmitter.EmitBurst(50);
                        result.ImpactEffectTriggered = true;
                    }
                }
            }
        }

        private void HandleDiceCollision(GameEvents.DiceCollisionOccurred e)
        {
            var worldPos = new Microsoft.Xna.Framework.Vector3(e.WorldPosition.X, e.WorldPosition.Y, e.WorldPosition.Z);
            var viewport = new Viewport(_renderTarget.Bounds);
            var screenPos3D = viewport.Project(worldPos, _projection, _view, Matrix.Identity);

            if (screenPos3D.Z < 0 || screenPos3D.Z > 1)
            {
                return;
            }

            _sparkEmitter.Position = new Vector2(screenPos3D.X, screenPos3D.Y);

            int burstCount = _random.Next(40, 71);
            for (int i = 0; i < burstCount; i++)
            {
                int pIndex = _sparkEmitter.EmitParticleAndGetIndex();
                if (pIndex == -1) break;

                ref var p = ref _sparkEmitter.GetParticle(pIndex);
                float angle = (float)(_random.NextDouble() * MathHelper.TwoPi);
                float speed = _sparkEmitter.Settings.InitialVelocityX.GetValue(_random);
                p.Velocity = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * speed;
            }
        }

        /// <summary>
        /// Gathers results from all dice, processes them by group, and invokes the OnRollCompleted event.
        /// </summary>
        private void FinalizeAndReportResults()
        {
            var result = new DiceRollResult();
            var rawResults = new Dictionary<string, List<int>>();

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
                    List<System.Numerics.Vector3> vertices = null;
                    if (die.DieType == DieType.D4)
                    {
                        vertices = _shapeCache[(die.DieType, die.BaseScale)].Vertices;
                    }
                    rawResults[die.GroupId].Add(DiceResultHelper.GetFaceValue(die.DieType, die.World, vertices));
                }
            }

            foreach (var group in _currentRollGroups)
            {
                if (rawResults.TryGetValue(group.GroupId, out var values))
                {
                    if (group.ResultProcessing == DiceResultProcessing.Sum)
                    {
                        int sum = values.Sum();
                        float multipliedValue = sum * group.Multiplier;
                        if (group.Multiplier < 1.0f)
                        {
                            sum = (int)Math.Floor(multipliedValue);
                        }
                        else
                        {
                            sum = (int)Math.Ceiling(multipliedValue);
                        }
                        sum += group.Modifier;
                        result.ResultsByGroup[group.GroupId] = new List<int> { sum };
                    }
                    else
                    {
                        result.ResultsByGroup[group.GroupId] = values;
                    }
                }
            }

            OnRollCompleted?.Invoke(result);
            _currentState = RollState.Idle;
        }

        /// <summary>
        /// Finds any dice that are still moving after the timeout and attempts to re-roll them.
        /// </summary>
        private void HandleStuckDice()
        {
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
            _rollInProgressTimer = 0f;
        }

        /// <summary>
        /// Instantly detects if a die is missing from the physics simulation and triggers a re-roll.
        /// </summary>
        private void HandleMissingDice()
        {
            var activeRenderableDice = _bodyToDieMap.Values.ToHashSet();
            var missingDice = _activeDice.Where(d => !activeRenderableDice.Contains(d) && !_forcedResults.ContainsKey(d)).ToList();

            foreach (var die in missingDice)
            {
                HandleReroll(die);
            }

            if (missingDice.Any())
            {
                _rollInProgressTimer = 0f;
            }
        }

        /// <summary>
        /// Centralized logic for handling a re-roll attempt for a specific die.
        /// </summary>
        private void HandleReroll(RenderableDie die, BodyHandle? handleToRemove = null)
        {
            if (!_rerollAttempts.ContainsKey(die))
            {
                _rerollAttempts[die] = 0;
            }
            _rerollAttempts[die]++;

            if (handleToRemove.HasValue)
            {
                _physicsWorld.RemoveBody(handleToRemove.Value);
                _bodyToDieMap.Remove(handleToRemove.Value);
            }

            if (_rerollAttempts[die] >= _global.DiceMaxRerollAttempts)
            {
                // If a die fails too many times, force its result instead of re-rolling again.
                _forcedResults[die] = _global.DiceForcedResultValue;
            }
            else
            {
                // Re-throw the die for another attempt.
                var group = _currentRollGroups.First(g => g.GroupId == die.GroupId);
                var newHandle = ThrowDieFromOffscreen(die, group, _random.Next(4));
                _bodyToDieMap.Add(newHandle, die);
            }
        }

        /// <summary>
        /// A major failsafe that re-rolls all dice if the simulation takes too long to resolve.
        /// </summary>
        private void HandleCompleteReroll()
        {
            _completeRerollAttempts++;

            if (_completeRerollAttempts >= _global.DiceMaxRerollAttempts)
            {
                HandleForcedCompletion();
                return;
            }

            foreach (var handle in _bodyToDieMap.Keys)
            {
                _physicsWorld.RemoveBody(handle);
            }
            _bodyToDieMap.Clear();
            _forcedResults.Clear();

            foreach (var die in _activeDice)
            {
                var group = _currentRollGroups.First(g => g.GroupId == die.GroupId);
                var newHandle = ThrowDieFromOffscreen(die, group, _random.Next(4));
                _bodyToDieMap.Add(newHandle, die);
            }

            _rollInProgressTimer = 0f;
            _currentState = RollState.Rolling;
        }

        /// <summary>
        /// A final failsafe that ends the roll immediately, assigning a forced value to all dice.
        /// </summary>
        private void HandleForcedCompletion()
        {
            foreach (var handle in _bodyToDieMap.Keys)
            {
                _physicsWorld.RemoveBody(handle);
            }
            _bodyToDieMap.Clear();
            _forcedResults.Clear();

            foreach (var die in _activeDice)
            {
                _forcedResults[die] = _global.DiceForcedResultValue;
            }

            FinalizeAndReportResults();

            _currentState = RollState.Idle;
            _rollInProgressTimer = 0f;
        }

        /// <summary>
        /// Draws the 3D dice scene to an off-screen texture.
        /// </summary>
        public RenderTarget2D Draw(BitmapFont font)
        {
            if (_activeDice.Count == 0)
            {
                return null;
            }

            _graphicsDevice.SetRenderTarget(_renderTarget);
            _graphicsDevice.Clear(Color.Transparent);
            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
            _graphicsDevice.BlendState = BlendState.Opaque;

            foreach (var die in _activeDice)
            {
                if (_forcedResults.ContainsKey(die) || die.IsDespawned)
                {
                    continue;
                }
                die.Draw(_view, _projection);
            }

            if (DebugShowColliders)
            {
                var originalDepthState = _graphicsDevice.DepthStencilState;
                _graphicsDevice.DepthStencilState = DepthStencilState.None;

                foreach (var die in _activeDice)
                {
                    if (_forcedResults.ContainsKey(die) || die.IsDespawned)
                    {
                        continue;
                    }
                    var shapeData = _shapeCache[(die.DieType, die.BaseScale)];
                    die.DrawDebug(_view, _projection, _debugEffect, _debugAxisVertices, shapeData.Vertices);
                }

                _graphicsDevice.DepthStencilState = originalDepthState;
            }

            _particleManager.Draw(_particleSpriteBatch);

            // Draw all animated text elements (individual results and group sums).
            var allFloatingText = _floatingResults.Concat(_groupSumResults).Concat(_activeModifiers).ToList();
            if (allFloatingText.Any() && font != null)
            {
                // Use BackToFront sorting to ensure particles (high depth) draw under text (low depth).
                _particleSpriteBatch.Begin(sortMode: SpriteSortMode.BackToFront, samplerState: SamplerState.PointClamp);
                foreach (var result in allFloatingText)
                {
                    if (!result.IsVisible) continue;

                    Vector2 drawPosition = result.CurrentPosition + result.ShakeOffset;
                    Vector2 textSize = font.MeasureString(result.Text) * result.Scale;
                    Vector2 textOrigin = new Vector2(textSize.X / (2 * result.Scale), textSize.Y / (2 * result.Scale));

                    Color outlineColor = Color.Black;
                    Color mainTextColor = result.TintColor != default ? result.TintColor : result.CurrentColor;
                    float rotation = result.Rotation;
                    int outlineOffset = 1;

                    // Draw a simple black outline by rendering the text multiple times with an offset.
                    _particleSpriteBatch.DrawString(font, result.Text, drawPosition + new Vector2(-outlineOffset, -outlineOffset), outlineColor, rotation, textOrigin, result.Scale, SpriteEffects.None, 0.1f);
                    _particleSpriteBatch.DrawString(font, result.Text, drawPosition + new Vector2(0, -outlineOffset), outlineColor, rotation, textOrigin, result.Scale, SpriteEffects.None, 0.1f);
                    _particleSpriteBatch.DrawString(font, result.Text, drawPosition + new Vector2(outlineOffset, -outlineOffset), outlineColor, rotation, textOrigin, result.Scale, SpriteEffects.None, 0.1f);
                    _particleSpriteBatch.DrawString(font, result.Text, drawPosition + new Vector2(-outlineOffset, 0), outlineColor, rotation, textOrigin, result.Scale, SpriteEffects.None, 0.1f);
                    _particleSpriteBatch.DrawString(font, result.Text, drawPosition + new Vector2(outlineOffset, 0), outlineColor, rotation, textOrigin, result.Scale, SpriteEffects.None, 0.1f);
                    _particleSpriteBatch.DrawString(font, result.Text, drawPosition + new Vector2(-outlineOffset, outlineOffset), outlineColor, rotation, textOrigin, result.Scale, SpriteEffects.None, 0.1f);
                    _particleSpriteBatch.DrawString(font, result.Text, drawPosition + new Vector2(0, outlineOffset), outlineColor, rotation, textOrigin, result.Scale, SpriteEffects.None, 0.1f);
                    _particleSpriteBatch.DrawString(font, result.Text, drawPosition + new Vector2(outlineOffset, outlineOffset), outlineColor, rotation, textOrigin, result.Scale, SpriteEffects.None, 0.1f);

                    // Draw the main text on top of the outline.
                    _particleSpriteBatch.DrawString(font, result.Text, drawPosition, mainTextColor, rotation, textOrigin, result.Scale, SpriteEffects.None, 0f);
                }
                _particleSpriteBatch.End();
            }

            _graphicsDevice.SetRenderTarget(null);
            return _renderTarget;
        }
    }
}
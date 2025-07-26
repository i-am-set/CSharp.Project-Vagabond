using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics.Constraints;
using BepuUtilities;
using BepuUtilities.Memory;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace ProjectVagabond.Physics
{
    // The following structs are required boilerplate for BEPUphysics v2.
    // They define how the simulation should respond to collisions and integrate forces.
    // For this project, the default behavior is sufficient.

    public struct NarrowPhaseCallbacks : INarrowPhaseCallbacks
    {
        // Removed 'readonly' to allow assignment in the Initialize method.
        private Global _global;

        public void Initialize(Simulation simulation)
        {
            // This struct is created by the simulation, so we can't use a constructor.
            // We fetch the global instance here.
            _global = ServiceLocator.Get<Global>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AllowContactGeneration(int workerIndex, CollidableReference a, CollidableReference b, ref float speculativeMargin)
        {
            // Allow contact generation for all pairs, including kinematic-dynamic.
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AllowContactGeneration(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB)
        {
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ConfigureContactManifold<TManifold>(int workerIndex, CollidablePair pair, ref TManifold manifold, out PairMaterialProperties material) where TManifold : unmanaged, IContactManifold<TManifold>
        {
            // These material properties define how two objects interact upon collision.
            material = new PairMaterialProperties
            {
                // Controls the "slipperiness" of surfaces.
                FrictionCoefficient = _global.DiceFrictionCoefficient,

                // Controls the bounciness of a collision.
                MaximumRecoveryVelocity = _global.DiceBounciness,

                // Defines spring-like physics for the collision, affecting how "hard" or "soft" the contact is.
                SpringSettings = new SpringSettings(_global.DiceSpringStiffness, _global.DiceSpringDamping)
            };
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ConfigureContactManifold(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB, ref ConvexContactManifold manifold)
        {
            return true;
        }

        public void Dispose() { }
    }

    public struct PoseIntegratorCallbacks : IPoseIntegratorCallbacks
    {
        public Vector3 Gravity;
        private Vector3Wide gravityDt;

        // The constructor now takes the gravity from the global settings.
        public PoseIntegratorCallbacks(Vector3 gravity)
        {
            Gravity = gravity;
            gravityDt = default;
        }

        public AngularIntegrationMode AngularIntegrationMode => AngularIntegrationMode.Nonconserving;
        public bool AllowSubstepsForUnconstrainedBodies => false;
        public bool IntegrateVelocityForKinematics => false;

        public void Initialize(Simulation simulation) { }

        public void PrepareForIntegration(float dt)
        {
            gravityDt = Vector3Wide.Broadcast(Gravity * dt);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void IntegrateVelocity(Vector<int> bodyIndices, Vector3Wide position, QuaternionWide orientation, BodyInertiaWide localInertia, Vector<int> integrationMask, int workerIndex, Vector<float> dt, ref BodyVelocityWide velocity)
        {
            // Apply gravity only to dynamic bodies. The integrationMask is -1 for dynamics and 0 for kinematics.
            // We use ConditionalSelect to apply the change only where the mask is set.
            var newLinearVelocity = velocity.Linear + gravityDt;
            Vector3Wide.ConditionalSelect(integrationMask, newLinearVelocity, velocity.Linear, out velocity.Linear);
        }
    }


    /// <summary>
    /// Encapsulates the BEPUphysics v2 simulation, providing a controlled environment
    /// for 3D physics interactions like dice rolling.
    /// </summary>
    public class PhysicsWorld
    {
        /// <summary>
        /// Gets the main physics simulation instance.
        /// </summary>
        public Simulation Simulation { get; }

        /// <summary>
        /// Gets the buffer pool used by the simulation for memory management.
        /// </summary>
        public BufferPool BufferPool { get; }

        private readonly ThreadDispatcher _threadDispatcher;
        private readonly Global _global;
        private readonly float _worldWidth;
        private readonly float _worldHeight;

        // Handles for the kinematic walls, allowing them to be moved.
        private BodyHandle _leftWallHandle;
        private BodyHandle _rightWallHandle;
        private BodyHandle _topWallHandle;
        private BodyHandle _bottomWallHandle;

        /// <summary>
        /// Initializes a new instance of the PhysicsWorld class.
        /// Sets up the simulation, thread dispatcher, and the static environment.
        /// </summary>
        /// <param name="worldWidth">The maximum width of the physics play area.</param>
        /// <param name="worldHeight">The maximum height of the physics play area.</param>
        public PhysicsWorld(float worldWidth, float worldHeight)
        {
            _global = ServiceLocator.Get<Global>();
            _worldWidth = worldWidth;
            _worldHeight = worldHeight;
            BufferPool = new BufferPool();

            // Use the number of available hardware threads for the simulation
            int threadCount = Math.Max(1, Environment.ProcessorCount > 4 ? Environment.ProcessorCount - 2 : 1);
            _threadDispatcher = new ThreadDispatcher(threadCount);

            // The gravity vector is now pulled from the global settings.
            var gravity = _global.DiceGravity;

            // The SolveDescription is now configured from the global settings.
            var solveDescription = new SolveDescription(_global.DiceSolverIterations, _global.DiceSolverSubsteps);

            Simulation = Simulation.Create(BufferPool, new NarrowPhaseCallbacks(), new PoseIntegratorCallbacks(gravity), solveDescription);

            CreateEnvironment();
        }

        /// <summary>
        /// Creates the static floor and kinematic walls that form the container for the physics objects.
        /// </summary>
        private void CreateEnvironment()
        {
            // The floor is static and covers the entire maximum possible area.
            var floorShape = new Box(_worldWidth, 1, _worldHeight);
            var floorShapeIndex = Simulation.Shapes.Add(floorShape);
            var floorDescription = new StaticDescription(new Vector3(_worldWidth / 2f, -0.5f, _worldHeight / 2f), floorShapeIndex);
            Simulation.Statics.Add(floorDescription);

            // Define wall properties from global settings
            float wallHeight = _global.DiceContainerWallHeight;
            float wallThickness = _global.DiceContainerWallThickness;

            // Create shapes for the walls, sized to span the maximum possible dimensions.
            var sideWallShape = Simulation.Shapes.Add(new Box(wallThickness, wallHeight, _worldHeight)); // For Left/Right
            var endWallShape = Simulation.Shapes.Add(new Box(_worldWidth, wallHeight, wallThickness));   // For Top/Bottom

            // Create the walls as KINEMATIC bodies. Their initial positions don't matter as they will be set immediately.
            // Kinematic bodies do not take an inertia parameter.
            var speculativeMargin = 0.01f;
            _leftWallHandle = Simulation.Bodies.Add(BodyDescription.CreateKinematic(new RigidPose(), sideWallShape, speculativeMargin));
            _rightWallHandle = Simulation.Bodies.Add(BodyDescription.CreateKinematic(new RigidPose(), sideWallShape, speculativeMargin));
            _topWallHandle = Simulation.Bodies.Add(BodyDescription.CreateKinematic(new RigidPose(), endWallShape, speculativeMargin));
            _bottomWallHandle = Simulation.Bodies.Add(BodyDescription.CreateKinematic(new RigidPose(), endWallShape, speculativeMargin));

            // Create the ceiling as a static object, as it never needs to move.
            var ceilingShape = new Box(_worldWidth, 1, _worldHeight);
            var ceilingShapeIndex = Simulation.Shapes.Add(ceilingShape);
            var ceilingDesc = new StaticDescription(new Vector3(_worldWidth / 2f, wallHeight + 0.5f, _worldHeight / 2f), ceilingShapeIndex);
            Simulation.Statics.Add(ceilingDesc);
        }

        /// <summary>
        /// Repositions the kinematic walls to form a boundary around the specified view dimensions.
        /// </summary>
        /// <param name="viewWidth">The width of the visible area.</param>
        /// <param name="viewHeight">The height of the visible area.</param>
        public void UpdateBoundaryPositions(float viewWidth, float viewHeight)
        {
            float wallHeight = _global.DiceContainerWallHeight;
            float wallThickness = _global.DiceContainerWallThickness;

            // The visible area is always centered within the larger physics world.
            float centerX = _worldWidth / 2f;
            float centerZ = _worldHeight / 2f;

            // Calculate the edges of the visible area.
            float minX = centerX - viewWidth / 2f;
            float maxX = centerX + viewWidth / 2f;
            float minZ = centerZ - viewHeight / 2f;
            float maxZ = centerZ + viewHeight / 2f;

            // Get references to the wall bodies and update their poses.
            var leftWall = Simulation.Bodies.GetBodyReference(_leftWallHandle);
            leftWall.Pose.Position = new Vector3(minX - wallThickness / 2f, wallHeight / 2f, centerZ);

            var rightWall = Simulation.Bodies.GetBodyReference(_rightWallHandle);
            rightWall.Pose.Position = new Vector3(maxX + wallThickness / 2f, wallHeight / 2f, centerZ);

            var topWall = Simulation.Bodies.GetBodyReference(_topWallHandle);
            topWall.Pose.Position = new Vector3(centerX, wallHeight / 2f, minZ - wallThickness / 2f);

            var bottomWall = Simulation.Bodies.GetBodyReference(_bottomWallHandle);
            bottomWall.Pose.Position = new Vector3(centerX, wallHeight / 2f, maxZ + wallThickness / 2f);
        }

        /// <summary>
        /// Adds a dynamic body to the simulation.
        /// </summary>
        /// <param name="description">The description of the body to add.</param>
        /// <returns>The handle of the newly created body.</returns>
        public BodyHandle AddBody(BodyDescription description)
        {
            return Simulation.Bodies.Add(description);
        }

        /// <summary>
        /// Removes a body from the simulation.
        /// </summary>
        /// <param name="handle">The handle of the body to remove.</param>
        public void RemoveBody(BodyHandle handle)
        {
            if (Simulation.Bodies.BodyExists(handle))
            {
                Simulation.Bodies.Remove(handle);
            }
        }

        /// <summary>
        /// Advances the physics simulation by a given time step.
        /// </summary>
        /// <param name="deltaTime">The amount of time to simulate, in seconds.</param>
        public void Update(float deltaTime)
        {
            Simulation.Timestep(deltaTime, _threadDispatcher);
        }
    }
}
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
        public void Initialize(Simulation simulation) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AllowContactGeneration(int workerIndex, CollidableReference a, CollidableReference b, ref float speculativeMargin)
        {
            return a.Mobility == CollidableMobility.Dynamic || b.Mobility == CollidableMobility.Dynamic;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AllowContactGeneration(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB)
        {
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ConfigureContactManifold<TManifold>(int workerIndex, CollidablePair pair, ref TManifold manifold, out PairMaterialProperties material) where TManifold : unmanaged, IContactManifold<TManifold>
        {
            // <<< BOUNCINESS AND FRICTION ARE CONTROLLED HERE. >>>
            // FrictionCoefficient: Reduced for a slightly more slidy feel.
            // SpringSettings DampingRatio: Lowered significantly for a much more exaggerated bounce.
            material = new PairMaterialProperties { FrictionCoefficient = 0.6f, MaximumRecoveryVelocity = 2f, SpringSettings = new SpringSettings(30, 0.1f) };
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
            velocity.Linear += gravityDt;
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

        /// <summary>
        /// Initializes a new instance of the PhysicsWorld class.
        /// Sets up the simulation, thread dispatcher, and the static environment.
        /// </summary>
        /// <param name="viewWidth">The width of the visible area for physics.</param>
        /// <param name="viewHeight">The height of the visible area for physics.</param>
        public PhysicsWorld(float viewWidth, float viewHeight)
        {
            BufferPool = new BufferPool();

            // Use the number of available hardware threads for the simulation
            int threadCount = Math.Max(1, Environment.ProcessorCount > 4 ? Environment.ProcessorCount - 2 : 1);
            _threadDispatcher = new ThreadDispatcher(threadCount);

            // <<< "HEAVINESS" IS CONTROLLED HERE. >>>
            // Gravity has been reduced from -1000 to -600 to make the dice feel lighter.
            Simulation = Simulation.Create(BufferPool, new NarrowPhaseCallbacks(), new PoseIntegratorCallbacks(new Vector3(0, -600, 0)), new SolveDescription(24, 1));

            CreateEnvironment(viewWidth, viewHeight);
        }

        /// <summary>
        /// Creates the static colliders that form the container for the physics objects.
        /// This includes a floor plane and four invisible walls sized to the camera's view.
        /// </summary>
        /// <param name="viewWidth">The width of the visible area.</param>
        /// <param name="viewHeight">The height of the visible area.</param>
        private void CreateEnvironment(float viewWidth, float viewHeight)
        {
            // Center the physics world around the origin of the view space
            float centerX = viewWidth / 2f;
            float centerZ = viewHeight / 2f;

            // Create the floor as a large, thin box at Y=0
            var floorShape = new Box(viewWidth * 2, 1, viewHeight * 2);
            var floorShapeIndex = Simulation.Shapes.Add(floorShape);
            var floorDescription = new StaticDescription(new Vector3(centerX, -0.5f, centerZ), floorShapeIndex);
            Simulation.Statics.Add(floorDescription);

            // Define wall properties
            const float wallHeight = 200f;
            const float wallThickness = 50f;

            // Create the four containing walls, positioned just outside the view
            // Left Wall
            var leftWallShapeIndex = Simulation.Shapes.Add(new Box(wallThickness, wallHeight, viewHeight));
            var leftWallDesc = new StaticDescription(new Vector3(-wallThickness / 2f, wallHeight / 2f, centerZ), leftWallShapeIndex);
            Simulation.Statics.Add(leftWallDesc);

            // Right Wall
            var rightWallShapeIndex = Simulation.Shapes.Add(new Box(wallThickness, wallHeight, viewHeight));
            var rightWallDesc = new StaticDescription(new Vector3(viewWidth + wallThickness / 2f, wallHeight / 2f, centerZ), rightWallShapeIndex);
            Simulation.Statics.Add(rightWallDesc);

            // Top Wall (Far Z)
            var topWallShapeIndex = Simulation.Shapes.Add(new Box(viewWidth, wallHeight, wallThickness));
            var topWallDesc = new StaticDescription(new Vector3(centerX, wallHeight / 2f, -wallThickness / 2f), topWallShapeIndex);
            Simulation.Statics.Add(topWallDesc);

            // Bottom Wall (Near Z)
            var bottomWallShapeIndex = Simulation.Shapes.Add(new Box(viewWidth, wallHeight, wallThickness));
            var bottomWallDesc = new StaticDescription(new Vector3(centerX, wallHeight / 2f, viewHeight + wallThickness / 2f), bottomWallShapeIndex);
            Simulation.Statics.Add(bottomWallDesc);
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

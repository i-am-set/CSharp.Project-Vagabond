using BepuPhysics;
using BepuPhysics.Collidables;
using ProjectVagabond.Physics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using BepuVector3 = System.Numerics.Vector3;

namespace ProjectVagabond.Dice
{
    /// <summary>
    /// Manages the BEPUphysics simulation, including world setup, body creation, and shape caching.
    /// </summary>
    public class DicePhysicsController
    {
        private readonly Global _global;
        private PhysicsWorld _physicsWorld;
        private float _physicsWorldWidth;
        private float _physicsWorldHeight;

        private readonly Dictionary<(DieType, float), (TypedIndex ShapeIndex, BodyInertia Inertia, List<BepuVector3> Vertices)> _shapeCache = new();

        public Simulation Simulation => _physicsWorld.Simulation;

        public DicePhysicsController()
        {
            _global = ServiceLocator.Get<Global>();
        }

        public void Initialize()
        {
            // The physics world is sized to the maximum possible camera zoom to ensure
            // the floor and walls are large enough for any roll.
            const float maxCameraZoom = 40f;
            float aspectRatio = (float)Global.VIRTUAL_WIDTH / Global.VIRTUAL_HEIGHT;
            _physicsWorldHeight = maxCameraZoom;
            _physicsWorldWidth = _physicsWorldHeight * aspectRatio;
            _physicsWorld = new PhysicsWorld(_physicsWorldWidth, _physicsWorldHeight);

            // Pre-cache the default D6 shape. The D4 requires model vertices and will be cached on its first roll.
            CacheShapeForScale(DieType.D6, 1.0f);
        }

        public void Update(float deltaTime)
        {
            _physicsWorld.Update(deltaTime);
        }

        public void UpdateBoundaryPositions(float viewWidth, float viewHeight)
        {
            _physicsWorld.UpdateBoundaryPositions(viewWidth, viewHeight);
        }

        public void CacheShapeForScale(DieType dieType, float scale, List<BepuVector3> modelVertices = null)
        {
            if (_shapeCache.ContainsKey((dieType, scale)))
            {
                return;
            }

            var points = new List<BepuVector3>();

            switch (dieType)
            {
                case DieType.D4:
                    if (modelVertices == null || !modelVertices.Any())
                    {
                        // This is a critical failure if we are trying to create a D4 shape without vertices.
                        throw new InvalidOperationException("Cannot create a D4 physics shape without providing model vertices.");
                    }
                    var beveledPoints = new List<BepuVector3>();
                    var originalVertices = modelVertices.Select(v => v * scale).ToList();
                    float bevelRatio = _global.DiceD4ColliderBevelRatio;

                    for (int i = 0; i < originalVertices.Count; i++)
                    {
                        var currentVertex = originalVertices[i];
                        for (int j = 0; j < originalVertices.Count; j++)
                        {
                            if (i == j) continue;
                            var otherVertex = originalVertices[j];
                            var newPoint = currentVertex + (otherVertex - currentVertex) * bevelRatio;
                            beveledPoints.Add(newPoint);
                        }
                    }
                    points.AddRange(beveledPoints.Distinct());
                    break;

                case DieType.D6:
                default:
                    float size = _global.DiceColliderSize * scale;
                    float bevelAmount = size * _global.DiceColliderBevelRatio;
                    for (int i = 0; i < 8; ++i)
                    {
                        var corner = new BepuVector3(
                            (i & 1) == 0 ? -size : size,
                            (i & 2) == 0 ? -size : size,
                            (i & 4) == 0 ? -size : size);
                        points.Add(corner + new BepuVector3(Math.Sign(corner.X) * -bevelAmount, 0, 0));
                        points.Add(corner + new BepuVector3(0, Math.Sign(corner.Y) * -bevelAmount, 0));
                        points.Add(corner + new BepuVector3(0, 0, Math.Sign(corner.Z) * -bevelAmount));
                    }
                    break;
            }

            if (!points.Any())
            {
                throw new InvalidOperationException($"Failed to generate any physics points for DieType {dieType}.");
            }

            var dieShape = new ConvexHull(points.ToArray(), _physicsWorld.BufferPool, out _);
            float scaledMass = _global.DiceMass * (scale * scale * scale);
            var dieInertia = dieShape.ComputeInertia(scaledMass);
            var dieShapeIndex = _physicsWorld.Simulation.Shapes.Add(dieShape);

            _shapeCache[(dieType, scale)] = (dieShapeIndex, dieInertia, points);
        }

        public BodyHandle AddDieBody(DiceGroup group, BepuVector3 position, BepuVector3 linearVelocity, BepuVector3 angularVelocity)
        {
            var shapeData = _shapeCache[(group.DieType, group.Scale)];
            var collidable = new CollidableDescription(shapeData.ShapeIndex, 0.01f);

            var bodyDescription = BodyDescription.CreateDynamic(
                position,
                shapeData.Inertia,
                collidable,
                new BodyActivityDescription(0.01f));

            bodyDescription.Collidable.Continuity = new ContinuousDetection
            {
                Mode = ContinuousDetectionMode.Continuous,
                MinimumSweepTimestep = 1e-5f,
                SweepConvergenceThreshold = 1e-5f
            };

            bodyDescription.Pose.Orientation = Quaternion.CreateFromYawPitchRoll(
                (float)(Random.Shared.NextDouble() * Math.PI * 2),
                (float)(Random.Shared.NextDouble() * Math.PI * 2),
                (float)(Random.Shared.NextDouble() * Math.PI * 2));

            bodyDescription.Velocity.Linear = linearVelocity;
            bodyDescription.Velocity.Angular = angularVelocity;

            return _physicsWorld.AddBody(bodyDescription);
        }

        public void RemoveBody(BodyHandle handle)
        {
            _physicsWorld.RemoveBody(handle);
        }

        public BodyReference GetBodyReference(BodyHandle handle)
        {
            return _physicsWorld.Simulation.Bodies.GetBodyReference(handle);
        }

        public List<BepuVector3> GetColliderVertices(DieType dieType, float scale)
        {
            return _shapeCache.TryGetValue((dieType, scale), out var data) ? data.Vertices : null;
        }

        public bool AreAllDiceSleeping(IEnumerable<BodyHandle> bodyHandles)
        {
            if (!bodyHandles.Any())
            {
                return true;
            }

            float sleepThreshold = _global.DiceSleepThreshold;

            foreach (var handle in bodyHandles)
            {
                var body = GetBodyReference(handle);
                if (body.Velocity.Linear.LengthSquared() > sleepThreshold || body.Velocity.Angular.LengthSquared() > sleepThreshold)
                {
                    return false;
                }
            }
            return true;
        }

        public void NudgeDie(BodyHandle handle)
        {
            var body = GetBodyReference(handle);
            if (!body.Awake) body.Awake = true;

            float nudgeForceMin = _global.DiceNudgeForceMin;
            float nudgeForceMax = _global.DiceNudgeForceMax;
            float nudgeUpMin = _global.DiceNudgeUpwardForceMin;
            float nudgeUpMax = _global.DiceNudgeUpwardForceMax;
            float nudgeTorqueMax = _global.DiceNudgeTorqueMax;

            body.Velocity.Linear += new BepuVector3(
                (float)(Random.Shared.NextDouble() * (nudgeForceMax - nudgeForceMin) + nudgeForceMin),
                (float)(Random.Shared.NextDouble() * (nudgeUpMax - nudgeUpMin) + nudgeUpMin),
                (float)(Random.Shared.NextDouble() * (nudgeForceMax - nudgeForceMin) + nudgeForceMin));

            body.Velocity.Angular += new BepuVector3(
                (float)(Random.Shared.NextDouble() * nudgeTorqueMax * 2 - nudgeTorqueMax),
                (float)(Random.Shared.NextDouble() * nudgeTorqueMax * 2 - nudgeTorqueMax),
                (float)(Random.Shared.NextDouble() * nudgeTorqueMax * 2 - nudgeTorqueMax));
        }

        public void ApplyTumbleImpulse(BodyHandle handle)
        {
            var body = GetBodyReference(handle);
            if (!body.Awake) body.Awake = true;

            float upwardForceMin = _global.DiceD4TumbleUpwardForceMin;
            float upwardForceMax = _global.DiceD4TumbleUpwardForceMax;
            float torqueMax = _global.DiceD4TumbleTorqueMax;

            // Apply a small upward pop to get it off the surface
            body.Velocity.Linear += new BepuVector3(
                0,
                (float)(Random.Shared.NextDouble() * (upwardForceMax - upwardForceMin) + upwardForceMin),
                0);

            // Apply a random torque to make it tumble
            body.Velocity.Angular += new BepuVector3(
                (float)(Random.Shared.NextDouble() * torqueMax * 2 - torqueMax),
                (float)(Random.Shared.NextDouble() * torqueMax * 2 - torqueMax),
                (float)(Random.Shared.NextDouble() * torqueMax * 2 - torqueMax));
        }
    }
}
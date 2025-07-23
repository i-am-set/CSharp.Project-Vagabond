using Microsoft.Xna.Framework;
using ProjectVagabond.Particles; // Added using directive
using System;
using System.Linq;

namespace ProjectVagabond
{
    /// <summary>
    /// Updates all entities with an InterpolationComponent, smoothly moving their
    /// visual position over time. When the interpolation is complete, it updates
    /// the entity's actual LocalPositionComponent and removes the interpolation.
    /// </summary>
    public class InterpolationSystem : ISystem
    {
        private readonly ComponentStore _componentStore;
        private WorldClockManager _worldClockManager;
        private MapRenderer _mapRenderer; // Added for coordinate conversion
        private readonly Random _random = new Random(); // Added for particle variation

        public InterpolationSystem()
        {
            _componentStore = ServiceLocator.Get<ComponentStore>();
        }

        public void Update(GameTime gameTime)
        {
            _worldClockManager ??= ServiceLocator.Get<WorldClockManager>();
            _mapRenderer ??= ServiceLocator.Get<MapRenderer>();

            // We must ToList() here to avoid modifying the collection while iterating
            var entitiesToInterpolate = _componentStore.GetAllEntitiesWithComponent<InterpolationComponent>().ToList();

            foreach (var entityId in entitiesToInterpolate)
            {
                var interpComp = _componentStore.GetComponent<InterpolationComponent>(entityId);
                if (interpComp == null) continue;

                // The timer now advances based on real time multiplied by the current time scale.
                // This makes the animation speed up or slow down instantly when the scale changes.
                interpComp.Timer += (float)gameTime.ElapsedGameTime.TotalSeconds * _worldClockManager.TimeScale;

                if (interpComp.Timer >= interpComp.GameTimeDuration)
                {
                    // Interpolation finished. Snap to the end position.
                    var localPosComp = _componentStore.GetComponent<LocalPositionComponent>(entityId);
                    if (localPosComp != null)
                    {
                        localPosComp.LocalPosition = interpComp.EndPosition;
                    }
                    _componentStore.RemoveComponent<InterpolationComponent>(entityId);
                }
                else
                {
                    // Interpolation in progress.
                    float progress = interpComp.Timer / interpComp.GameTimeDuration;
                    interpComp.CurrentVisualPosition = Vector2.Lerp(interpComp.StartPosition, interpComp.EndPosition, progress);

                    // --- NEW: Trigger Particle Emission ---
                    TriggerMovementParticles(entityId, interpComp);
                }
            }
        }

        private void TriggerMovementParticles(int entityId, InterpolationComponent interpComp)
        {
            var emitterComp = _componentStore.GetComponent<ParticleEmitterComponent>(entityId);
            if (emitterComp == null || !emitterComp.Emitters.TryGetValue("DirtSpray", out var emitter))
            {
                return;
            }

            // Convert the entity's current visual position (local grid coords) to screen coords
            Vector2? screenPos = _mapRenderer.MapCoordsToScreen(interpComp.CurrentVisualPosition);
            if (!screenPos.HasValue) return;

            // Center the emitter on the tile
            int cellSize = Global.LOCAL_GRID_CELL_SIZE;
            emitter.Position = screenPos.Value + new Vector2(cellSize / 2f, cellSize / 2f);

            // Calculate movement direction and emit particles
            Vector2 moveDirection = interpComp.EndPosition - interpComp.StartPosition;
            if (moveDirection.LengthSquared() > 0)
            {
                Vector2 emitDirection = -Vector2.Normalize(moveDirection);
                float baseAngle = (float)Math.Atan2(emitDirection.Y, emitDirection.X);

                // Emit a small burst of particles
                int particleCount = (interpComp.Mode == MovementMode.Run) ? 2 : 1;
                for (int i = 0; i < particleCount; i++)
                {
                    // Find an available particle
                    int particleIndex = emitter.EmitParticleAndGetIndex();
                    if (particleIndex == -1) break; // Emitter is full

                    ref var p = ref emitter.GetParticle(particleIndex);

                    // Apply velocity based on movement direction
                    float spread = MathHelper.ToRadians(30); // 30 degree cone
                    float angle = baseAngle + (float)(_random.NextDouble() * 2 - 1) * spread;
                    float speed = emitter.Settings.InitialVelocityX.GetValue(_random); // Using X as speed

                    p.Velocity = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * speed;
                }
            }
        }
    }
}
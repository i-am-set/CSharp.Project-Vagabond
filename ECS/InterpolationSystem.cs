﻿using Microsoft.Xna.Framework;
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

        public InterpolationSystem()
        {
            _componentStore = ServiceLocator.Get<ComponentStore>();
        }

        public void Update(GameTime gameTime)
        {
            _worldClockManager ??= ServiceLocator.Get<WorldClockManager>();

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
                }
            }
        }
    }
}

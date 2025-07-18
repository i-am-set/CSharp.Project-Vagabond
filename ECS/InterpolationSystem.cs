using Microsoft.Xna.Framework;
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

        public InterpolationSystem()
        {
            _componentStore = ServiceLocator.Get<ComponentStore>();
        }

        public void Update(GameTime gameTime)
        {
            // We must ToList() here to avoid modifying the collection while iterating
            var entitiesToInterpolate = _componentStore.GetAllEntitiesWithComponent<InterpolationComponent>().ToList();

            foreach (var entityId in entitiesToInterpolate)
            {
                var interpComp = _componentStore.GetComponent<InterpolationComponent>(entityId);
                if (interpComp == null) continue;

                interpComp.Timer += (float)gameTime.ElapsedGameTime.TotalSeconds;

                if (interpComp.Timer >= interpComp.Duration)
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
                    float progress = interpComp.Timer / interpComp.Duration;
                    interpComp.CurrentVisualPosition = Vector2.Lerp(interpComp.StartPosition, interpComp.EndPosition, progress);
                }
            }
        }
    }
}
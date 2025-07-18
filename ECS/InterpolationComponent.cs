using Microsoft.Xna.Framework;

namespace ProjectVagabond
{
    /// <summary>
    /// A temporary component that handles the visual interpolation of an entity's movement
    /// from a start point to an end point over a set duration.
    /// </summary>
    public class InterpolationComponent : IComponent, ICloneableComponent
    {
        public Vector2 StartPosition { get; set; }
        public Vector2 EndPosition { get; set; }
        public float Duration { get; set; }
        public float Timer { get; set; }
        public Vector2 CurrentVisualPosition { get; set; }

        public InterpolationComponent(Vector2 start, Vector2 end, float duration)
        {
            StartPosition = start;
            EndPosition = end;
            Duration = duration;
            Timer = 0f;
            CurrentVisualPosition = start;
        }

        public IComponent Clone()
        {
            // This is a runtime component and should not be part of templates.
            // Cloning creates a default, non-moving state.
            return new InterpolationComponent(Vector2.Zero, Vector2.Zero, 0f);
        }
    }
}
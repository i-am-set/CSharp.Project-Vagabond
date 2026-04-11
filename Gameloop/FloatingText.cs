using Microsoft.Xna.Framework;
using ProjectVagabond.Utils;

namespace ProjectVagabond.UI
{
    public sealed class FloatingText : IPoolable
    {
        public bool IsPooled { get; set; }
        public int Number;
        public bool IsHealing;
        public bool IsGold;
        public bool IsCrit;
        public float Timer;
        public float Duration;
        public Vector2 LocalOffset;
        public Vector2 StartOffset;

        public PlinkAnimator Plink { get; } = new PlinkAnimator { MaxScale = 2.0f, RestScale = 1.0f };

        public void Reset()
        {
            Number = 0;
            IsHealing = false;
            IsGold = false;
            IsCrit = false;
            Timer = 0f;
            Duration = 0f;
            LocalOffset = Vector2.Zero;
            StartOffset = Vector2.Zero;
        }

        public void ReturnToPool()
        {
            Pool<FloatingText>.Return(this);
        }
    }
}
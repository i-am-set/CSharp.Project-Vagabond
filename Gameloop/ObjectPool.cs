using System.Collections.Generic;
using ProjectVagabond.Battle;
using ProjectVagabond.Deliveries;
using ProjectVagabond.Animations;

namespace ProjectVagabond.Utils
{
    public interface IPoolable
    {
        bool IsPooled { get; set; }
        void Reset();
    }

    public class ObjectPool<T> where T : IPoolable, new()
    {
        private readonly Stack<T> _pool = new Stack<T>();
        public int MaxCapacity { get; set; } = 1000;

        public T Get()
        {
            T item = _pool.Count > 0 ? _pool.Pop() : new T();
            item.IsPooled = false;
            return item;
        }

        public void Return(T item)
        {
            if (item == null || item.IsPooled) return;
            item.Reset();
            item.IsPooled = true;
            if (_pool.Count < MaxCapacity)
            {
                _pool.Push(item);
            }
        }
    }

    public static class Pools
    {
        public static readonly ObjectPool<FloatingText> FloatingText = new ObjectPool<FloatingText>();
        public static readonly ObjectPool<ActiveAttack> ActiveAttack = new ObjectPool<ActiveAttack>();

        public static readonly ObjectPool<DashMeleeDelivery> DashMeleeDeliveries = new ObjectPool<DashMeleeDelivery>();
        public static readonly ObjectPool<InstantAOEDelivery> InstantAOEDeliveries = new ObjectPool<InstantAOEDelivery>();
        public static readonly ObjectPool<MeteorStrikeDelivery> MeteorStrikeDeliveries = new ObjectPool<MeteorStrikeDelivery>();
        public static readonly ObjectPool<MultiProjectileDelivery> MultiProjectileDeliveries = new ObjectPool<MultiProjectileDelivery>();
        public static readonly ObjectPool<SeekAndDashDelivery> SeekAndDashDeliveries = new ObjectPool<SeekAndDashDelivery>();
        public static readonly ObjectPool<SingleTargetDelivery> SingleTargetDeliveries = new ObjectPool<SingleTargetDelivery>();
        public static readonly ObjectPool<TickingBeamDelivery> TickingBeamDeliveries = new ObjectPool<TickingBeamDelivery>();

        public static readonly ObjectPool<SpriteAnimationInstance> SpriteAnimations = new ObjectPool<SpriteAnimationInstance>();
        public static readonly ObjectPool<ParticleAnimationInstance> ParticleAnimations = new ObjectPool<ParticleAnimationInstance>();

        public static void ReturnDelivery(IDelivery delivery)
        {
            if (delivery == null) return;
            if (delivery is DashMeleeDelivery dmd) DashMeleeDeliveries.Return(dmd);
            else if (delivery is InstantAOEDelivery iaoe) InstantAOEDeliveries.Return(iaoe);
            else if (delivery is MeteorStrikeDelivery msd) MeteorStrikeDeliveries.Return(msd);
            else if (delivery is MultiProjectileDelivery mpd) MultiProjectileDeliveries.Return(mpd);
            else if (delivery is SeekAndDashDelivery sadd) SeekAndDashDeliveries.Return(sadd);
            else if (delivery is SingleTargetDelivery std) SingleTargetDeliveries.Return(std);
            else if (delivery is TickingBeamDelivery tbd) TickingBeamDeliveries.Return(tbd);
        }

        public static void ReturnAnimation(IAnimationInstance animation)
        {
            if (animation == null) return;
            if (animation is SpriteAnimationInstance sai) SpriteAnimations.Return(sai);
            else if (animation is ParticleAnimationInstance pai) ParticleAnimations.Return(pai);
        }
    }
}
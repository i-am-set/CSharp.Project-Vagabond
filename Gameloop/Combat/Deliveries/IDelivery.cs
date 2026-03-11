using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectVagabond.Battle;
using ProjectVagabond.Utils;

namespace ProjectVagabond.Deliveries
{
    public interface IDelivery : IPoolable
    {
        bool IsFinished { get; }
        bool IsAnimationPaused { get; }
        void Setup(IDelivery template);
        IDelivery GetInstanceFromPool();
        void Start(ActiveAttack attack);
        void Update(float dt, BattleContext context, ActiveAttack attack);
        void Draw(SpriteBatch spriteBatch, ActiveAttack attack);
        void DrawTelegraph(SpriteBatch spriteBatch, Vector2 origin, Vector2 direction, Vector2 targetPos, BattleContext context);
        void TriggerImpact(BattleContext context, ActiveAttack attack);
    }
}
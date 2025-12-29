using Microsoft.Xna.Framework;
using ProjectVagabond.Battle;

namespace ProjectVagabond.Battle.UI
{
    /// <summary>
    /// Contains shared data structures used by the BattleRenderer and its helpers.
    /// </summary>
    public struct TargetInfo
    {
        public BattleCombatant Combatant;
        public Rectangle Bounds;
    }

    public struct StatusIconInfo
    {
        public StatusEffectInstance Effect;
        public Rectangle Bounds;
    }
}
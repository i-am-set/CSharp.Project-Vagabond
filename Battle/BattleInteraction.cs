using System;
using System.Linq;
using ProjectVagabond.Utils;

namespace ProjectVagabond.Battle
{
    /// <summary>
    /// Base class for any mid-turn interruption that requires input (Player UI or AI Logic).
    /// </summary>
    public abstract class BattleInteraction
    {
        public BattleCombatant Actor { get; }
        protected Action<object> OnComplete;

        protected BattleInteraction(BattleCombatant actor, Action<object> onComplete)
        {
            Actor = actor;
            OnComplete = onComplete;
        }

        /// <summary>
        /// Called when the interaction becomes active.
        /// </summary>
        public abstract void Start(BattleManager bm);

        /// <summary>
        /// Called by the UI or AI logic to finish the interaction.
        /// </summary>
        public void Resolve(object result)
        {
            OnComplete?.Invoke(result);
        }
    }

    /// <summary>
    /// Handles the logic for a forced switch (Disengage).
    /// </summary>
    public class SwitchInteraction : BattleInteraction
    {
        public SwitchInteraction(BattleCombatant actor, Action<object> onComplete) : base(actor, onComplete) { }

        public override void Start(BattleManager bm)
        {
            // Check for valid bench members for the actor's team
            var bench = bm.AllCombatants
                .Where(c => c.IsPlayerControlled == Actor.IsPlayerControlled && !c.IsDefeated && c.BattleSlot >= 2)
                .ToList();

            if (!bench.Any())
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{Actor.Name} tries to switch, but has no allies!" });
                Resolve(null);
                return;
            }

            if (Actor.IsPlayerControlled)
            {
                // Player: Fire event to open UI
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "Shadow Step! Choose a replacement!" });
                EventBus.Publish(new GameEvents.ForcedSwitchRequested { Actor = Actor });
            }
            else
            {
                // AI: Pick best bench member
                // Simple AI: Pick highest HP %
                var target = bench.OrderByDescending(c => (float)c.Stats.CurrentHP / c.Stats.MaxHP).First();
                Resolve(target);
            }
        }
    }
}
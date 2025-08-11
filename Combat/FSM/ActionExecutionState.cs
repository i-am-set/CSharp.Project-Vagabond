using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Diagnostics;

namespace ProjectVagabond.Combat.FSM
{
    /// <summary>
    /// The "cinematic" state where selected actions are visually executed.
    /// </summary>
    public class ActionExecutionState : ICombatState
    {
        private Queue<CombatAction> _executionQueue;
        private bool _isWaitingForAnimation;
        private float _failsafeTimer;
        private const float FAILSAFE_DURATION = 10f; // 10 seconds

        public void OnEnter(CombatManager combatManager)
        {
            _isWaitingForAnimation = false;
            _failsafeTimer = 0f;

            // The ActionHandUI will hide automatically based on the FSM state.

            var actions = combatManager.GetActionsForTurn();
            var resolvedOrder = TurnResolver.ResolveTurnOrder(new List<CombatAction>(actions));
            _executionQueue = new Queue<CombatAction>(resolvedOrder);

            EventBus.Subscribe<GameEvents.ActionAnimationComplete>(OnActionAnimationCompleted);

            ProcessNextAction(combatManager);
        }

        public void OnExit(CombatManager combatManager)
        {
            EventBus.Unsubscribe<GameEvents.ActionAnimationComplete>(OnActionAnimationCompleted);
        }

        public void Update(GameTime gameTime, CombatManager combatManager)
        {
            if (_isWaitingForAnimation)
            {
                _failsafeTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (_failsafeTimer >= FAILSAFE_DURATION)
                {
                    Debug.WriteLine($"[WARNING] Action animation timed out after {FAILSAFE_DURATION}s. Forcing next action.");
                    OnActionAnimationCompleted(new GameEvents.ActionAnimationComplete());
                }
            }
        }

        private void ProcessNextAction(CombatManager combatManager)
        {
            if (_executionQueue.Count > 0)
            {
                var actionToExecute = _executionQueue.Dequeue();
                _isWaitingForAnimation = true;
                _failsafeTimer = 0f;
                combatManager.Scene.ExecuteActionVisuals(actionToExecute);
            }
            else
            {
                // All actions for the turn are complete.
                combatManager.FSM.ChangeState(new TurnEndState(), combatManager);
            }
        }

        private void OnActionAnimationCompleted(GameEvents.ActionAnimationComplete e)
        {
            if (!_isWaitingForAnimation) return; // Prevent double execution

            _isWaitingForAnimation = false;
            var combatManager = ServiceLocator.Get<CombatManager>(); // A bit of a hack, but necessary for event handlers
            ProcessNextAction(combatManager);
        }
    }
}
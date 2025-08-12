using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

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

            var actions = combatManager.GetActionsForTurn();
            var resolvedOrder = TurnResolver.ResolveTurnOrder(new List<CombatAction>(actions));
            _executionQueue = new Queue<CombatAction>(resolvedOrder);

            if (!_executionQueue.Any())
            {
                Debug.WriteLine("    ... No actions to execute this turn.");
            }

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
                    Debug.WriteLine($"    ... [WARNING] Action animation timed out after {FAILSAFE_DURATION}s. Forcing next action.");
                    OnActionAnimationCompleted(new GameEvents.ActionAnimationComplete());
                }
            }
        }

        private void ProcessNextAction(CombatManager combatManager)
        {
            if (_executionQueue.Count > 0)
            {
                var actionToExecute = _executionQueue.Dequeue();
                Debug.WriteLine($"    ... Executing action: {actionToExecute.ActionData.Name} by Caster {actionToExecute.CasterEntityId}");
                _isWaitingForAnimation = true;
                _failsafeTimer = 0f;
                Debug.WriteLine("    ... Waiting for action visuals...");
                combatManager.Scene.ExecuteActionVisuals(actionToExecute);
            }
            else
            {
                // All actions for the turn are complete.
                Debug.WriteLine("    ... All actions executed.");
                combatManager.FSM.ChangeState(new TurnEndState(), combatManager);
            }
        }

        private void OnActionAnimationCompleted(GameEvents.ActionAnimationComplete e)
        {
            if (!_isWaitingForAnimation) return; // Prevent double execution
            Debug.WriteLine("    ... Action visuals complete.");
            _isWaitingForAnimation = false;
            var combatManager = ServiceLocator.Get<CombatManager>();
            ProcessNextAction(combatManager);
        }
    }
}
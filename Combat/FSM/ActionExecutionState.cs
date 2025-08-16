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

        // --- TUNING ---
        /// <summary>
        /// A short delay after each action resolves to improve game feel and pacing.
        /// </summary>
        private const float POST_ACTION_DELAY = 0.5f;
        private float _postActionTimer;
        private bool _isDelaying;

        private ActionAnimator _actionAnimator;

        public void OnEnter(CombatManager combatManager)
        {
            _isWaitingForAnimation = false;
            _isDelaying = false;
            _failsafeTimer = 0f;
            _actionAnimator = combatManager.Scene.ActionAnimator;

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
            else if (_isDelaying)
            {
                _postActionTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (_postActionTimer <= 0)
                {
                    _isDelaying = false;
                    ProcessNextAction(combatManager);
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

                // The ActionAnimator now plays the timeline. The timeline itself will trigger
                // the ActionResolver and the ActionAnimationComplete event when finished.
                _actionAnimator.Play(actionToExecute);
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

            // Start the post-action delay
            _postActionTimer = POST_ACTION_DELAY;
            _isDelaying = true;
        }
    }
}
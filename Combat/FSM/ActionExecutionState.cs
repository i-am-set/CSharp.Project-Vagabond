using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ProjectVagabond.Combat.FSM
{
    /// <summary>
    /// The "cinematic" state where selected actions are visually executed in their resolved order.
    /// </summary>
    public class ActionExecutionState : ICombatState
    {
        private Queue<CombatAction> _executionQueue;
        private bool _isWaitingForAnimation;
        private float _failsafeTimer;
        private const float FAILSAFE_DURATION = 10f; // 10 seconds

        // --- TUNING ---
        private const float POST_ACTION_DELAY = 0.5f;
        private const float PRE_EFFECT_IDLE_DURATION = 0.4f;
        private float _postActionTimer;
        private bool _isDelaying;

        private ActionAnimator _actionAnimator;

        public void OnEnter(CombatManager combatManager)
        {
            Debug.WriteLine("--- PHASE: ACTION EXECUTION ROUND START ---");
            _isWaitingForAnimation = false;
            _isDelaying = false;
            _failsafeTimer = 0f;
            _actionAnimator = combatManager.Scene.ActionAnimator;

            // The list of actions is now pre-rolled and pre-sorted. We just need to enqueue it.
            var resolvedOrder = combatManager.GetActionsForTurn();
            _executionQueue = new Queue<CombatAction>(resolvedOrder);

            if (!_executionQueue.Any())
            {
                Debug.WriteLine("  > No actions to execute this round.");
            }
            else
            {
                // Set all enemies to their inactive "background" state.
                foreach (var entity in combatManager.Scene.GetAllCombatEntities())
                {
                    if (entity.EntityId != ServiceLocator.Get<GameState>().PlayerEntityId)
                    {
                        entity.SetInactiveVisuals();
                    }
                }
            }

            ProcessNextAction(combatManager);
        }

        public void OnExit(CombatManager combatManager)
        {
            // Ensure all entities are returned to their normal state when leaving this phase.
            foreach (var entity in combatManager.Scene.GetAllCombatEntities())
            {
                entity.SetActiveVisuals();
            }
            combatManager.Scene.CurrentExecutingAction = null;
        }

        public void Update(GameTime gameTime, CombatManager combatManager)
        {
            if (_isWaitingForAnimation)
            {
                _failsafeTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (_failsafeTimer >= FAILSAFE_DURATION)
                {
                    Debug.WriteLine($"    [WARNING] Action animation timed out after {FAILSAFE_DURATION}s. Forcing next action.");
                    OnActionAnimationCompleted();
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
                string entityName = EntityNamer.GetName(actionToExecute.CasterEntityId);

                var health = ServiceLocator.Get<ComponentStore>().GetComponent<HealthComponent>(actionToExecute.CasterEntityId);
                if (health != null && health.CurrentHealth <= 0)
                {
                    Debug.WriteLine($"  > {entityName}'s turn skipped (defeated).");
                    ProcessNextAction(combatManager);
                    return;
                }

                combatManager.Scene.CurrentExecutingAction = actionToExecute;

                var allEntities = combatManager.Scene.GetAllCombatEntities();
                var player = allEntities.FirstOrDefault(e => e.EntityId == ServiceLocator.Get<GameState>().PlayerEntityId);

                foreach (var entity in allEntities)
                {
                    if (entity == player) continue;

                    bool isCaster = entity.EntityId == actionToExecute.CasterEntityId;
                    bool isTarget = actionToExecute.TargetEntityIds.Contains(entity.EntityId);

                    if (isCaster || isTarget)
                    {
                        entity.SetActiveVisuals();
                    }
                    else
                    {
                        entity.SetInactiveVisuals();
                    }
                }

                Debug.WriteLine($"  > Executing: {entityName}'s {actionToExecute.ActionData.Name}");
                _isWaitingForAnimation = true;
                _failsafeTimer = 0f;

                // If it's the player's action, animate hands back to idle BEFORE resolving effects.
                if (actionToExecute.CasterEntityId == ServiceLocator.Get<GameState>().PlayerEntityId)
                {
                    _actionAnimator.ReturnToIdle(PRE_EFFECT_IDLE_DURATION, () =>
                    {
                        // When the return-to-idle animation is complete, resolve the action.
                        var resolver = ServiceLocator.Get<ActionResolver>();
                        resolver.Resolve(actionToExecute, combatManager.Scene.GetAllCombatEntities());

                        // Now that effects are resolved, we can complete the action and start the post-action delay.
                        OnActionAnimationCompleted();
                    });
                }
                else // It's an AI action, no hand animation involved. Resolve immediately.
                {
                    var resolver = ServiceLocator.Get<ActionResolver>();
                    resolver.Resolve(actionToExecute, combatManager.Scene.GetAllCombatEntities());
                    OnActionAnimationCompleted();
                }
            }
            else
            {
                // All actions for the turn are complete.
                Debug.WriteLine("--- END PHASE: ACTION EXECUTION ROUND ---");
                combatManager.FSM.ChangeState(new RoundEndState(), combatManager);
            }
        }

        private void OnActionAnimationCompleted()
        {
            if (!_isWaitingForAnimation) return; // Prevent double execution
            _isWaitingForAnimation = false;
            ServiceLocator.Get<CombatManager>().Scene.CurrentExecutingAction = null;

            // Start the post-action delay
            _postActionTimer = POST_ACTION_DELAY;
            _isDelaying = true;
        }
    }
}
using Microsoft.Xna.Framework;
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

            // Explicitly force hands to their off-screen position at the start of the phase
            // to ensure a consistent starting state, regardless of what happened before.
            var scene = combatManager.Scene;
            if (scene.AnimationAnchors != null) // Anchors might not exist in certain test contexts
            {
                if (scene.AnimationAnchors.TryGetValue("LeftHandOffscreen", out var leftPos))
                {
                    scene.LeftHandRenderer.ForcePositionAndRotation(leftPos, 0);
                }
                if (scene.AnimationAnchors.TryGetValue("RightHandOffscreen", out var rightPos))
                {
                    scene.RightHandRenderer.ForcePositionAndRotation(rightPos, 0);
                }
            }

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

            EventBus.Subscribe<GameEvents.ActionAnimationComplete>(OnActionAnimationCompleted);

            ProcessNextAction(combatManager);
        }

        public void OnExit(CombatManager combatManager)
        {
            EventBus.Unsubscribe<GameEvents.ActionAnimationComplete>(OnActionAnimationCompleted);

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
                string entityName = EntityNamer.GetName(actionToExecute.CasterEntityId);

                // MODIFIED: Check if the combatant is dead BEFORE doing anything else.
                var health = ServiceLocator.Get<ComponentStore>().GetComponent<HealthComponent>(actionToExecute.CasterEntityId);
                if (health != null && health.CurrentHealth <= 0)
                {
                    Debug.WriteLine($"  > {entityName}'s turn skipped (defeated).");
                    ProcessNextAction(combatManager); // Immediately process the next in queue without any delay or animation.
                    return;
                }

                // If the combatant is alive, proceed with the turn.
                combatManager.Scene.CurrentExecutingAction = actionToExecute;

                // Set the visual state for all entities based on this action.
                var allEntities = combatManager.Scene.GetAllCombatEntities();
                var player = allEntities.FirstOrDefault(e => e.EntityId == ServiceLocator.Get<GameState>().PlayerEntityId);

                foreach (var entity in allEntities)
                {
                    if (entity == player) continue; // Player sprite isn't shown, so skip.

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

                _actionAnimator.Play(actionToExecute);
            }
            else
            {
                // All actions for the turn are complete.
                Debug.WriteLine("--- END PHASE: ACTION EXECUTION ROUND ---");
                combatManager.FSM.ChangeState(new RoundEndState(), combatManager);
            }
        }

        private void OnActionAnimationCompleted(GameEvents.ActionAnimationComplete e)
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
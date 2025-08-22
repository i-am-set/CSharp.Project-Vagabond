﻿using Microsoft.Xna.Framework;
using System.Diagnostics;

namespace ProjectVagabond.Combat.FSM
{
    /// <summary>
    /// A state that waits for the player's card "play" animation to complete
    /// before allowing the turn to proceed to AI selection.
    /// </summary>
    public class PlayerActionConfirmedState : ICombatState
    {
        private bool _isWaitingForAnimation;
        private float _failsafeTimer;
        private const float FAILSAFE_DURATION = 10f;

        public void OnEnter(CombatManager combatManager)
        {
            Debug.WriteLine("    ... Waiting for player card animation...");
            _isWaitingForAnimation = true;
            _failsafeTimer = 0f;
            EventBus.Subscribe<GameEvents.ActionAnimationComplete>(OnPlayerActionAnimationCompleted);
        }

        public void OnExit(CombatManager combatManager)
        {
            EventBus.Unsubscribe<GameEvents.ActionAnimationComplete>(OnPlayerActionAnimationCompleted);
        }

        public void Update(GameTime gameTime, CombatManager combatManager)
        {
            if (_isWaitingForAnimation)
            {
                _failsafeTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (_failsafeTimer >= FAILSAFE_DURATION)
                {
                    Debug.WriteLine($"    ... [WARNING] Player action confirmation animation timed out. Forcing next state.");
                    OnPlayerActionAnimationCompleted(new GameEvents.ActionAnimationComplete());
                }
            }
        }

        private void OnPlayerActionAnimationCompleted(GameEvents.ActionAnimationComplete e)
        {
            if (!_isWaitingForAnimation) return;
            Debug.WriteLine("    ... Player card animation complete.");
            _isWaitingForAnimation = false;
            var combatManager = ServiceLocator.Get<CombatManager>();

            // MODIFIED: After player selects, advance to the next combatant for their selection.
            combatManager.AdvanceTurn();

            // If we've looped back to the start, everyone has selected their action.
            if (combatManager.IsNewRound())
            {
                Debug.WriteLine("  --- All combatants have selected actions. Proceeding to execution. ---");
                combatManager.FSM.ChangeState(new ActionExecutionState(), combatManager);
            }
            else
            {
                // Otherwise, start the next combatant's turn for selection.
                combatManager.FSM.ChangeState(new TurnStartState(), combatManager);
            }
        }
    }
}

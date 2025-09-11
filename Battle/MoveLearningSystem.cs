using Microsoft.Xna.Framework;
using ProjectVagabond.Battle;
using System.Linq;

namespace ProjectVagabond
{
    /// <summary>
    /// A system responsible for handling changes to the player's available combat moves.
    /// It listens for events and modifies the player's state accordingly.
    /// </summary>
    public class MoveLearningSystem : ISystem
    {
        private readonly GameState _gameState;

        public MoveLearningSystem()
        {
            _gameState = ServiceLocator.Get<GameState>();
            EventBus.Subscribe<GameEvents.PlayerMoveSetChanged>(OnPlayerMoveSetChanged);
        }

        private void OnPlayerMoveSetChanged(GameEvents.PlayerMoveSetChanged e)
        {
            if (_gameState.PlayerState == null) return;

            switch (e.ChangeType)
            {
                case GameEvents.MoveSetChangeType.Learn:
                    HandleLearnMove(e.MoveID);
                    break;
                case GameEvents.MoveSetChangeType.Forget:
                    HandleForgetMove(e.MoveID);
                    break;
            }
        }

        private void HandleLearnMove(string moveId)
        {
            // 1. Validate that the move exists in the game data.
            if (!BattleDataCache.Moves.TryGetValue(moveId, out var moveData))
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[error]Move '{moveId}' does not exist." });
                return;
            }

            // 2. Check if the player already knows the move.
            if (_gameState.PlayerState.CurrentActionMoveIDs.Any(m => m.Equals(moveId, System.StringComparison.OrdinalIgnoreCase)))
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"Player already knows {moveData.MoveName}." });
                return;
            }

            // 3. Add the move and provide feedback.
            _gameState.PlayerState.CurrentActionMoveIDs.Add(moveId);
            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[palette_teal]Player learned {moveData.MoveName}!" });
        }

        private void HandleForgetMove(string moveId)
        {
            // 1. Find the move in the player's current list (case-insensitive).
            var moveToRemove = _gameState.PlayerState.CurrentActionMoveIDs
                .FirstOrDefault(m => m.Equals(moveId, System.StringComparison.OrdinalIgnoreCase));

            // 2. Check if the player actually knows the move.
            if (moveToRemove == null)
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[error]Player does not know a move with ID '{moveId}'." });
                return;
            }

            // 3. Get move data for feedback message before removing.
            BattleDataCache.Moves.TryGetValue(moveToRemove, out var moveData);
            string moveName = moveData?.MoveName ?? moveId;

            // 4. Remove the move and provide feedback.
            _gameState.PlayerState.CurrentActionMoveIDs.Remove(moveToRemove);
            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[palette_orange]Player forgot {moveName}." });
        }

        public void Update(GameTime gameTime)
        {
            // This system is purely event-driven and does not need per-frame updates.
        }
    }
}
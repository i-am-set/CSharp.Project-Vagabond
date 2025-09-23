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

            // 2. Enforce the "Spells only" rule for the spellbook.
            if (moveData.MoveType != MoveType.Spell)
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[error]Cannot learn '{moveData.MoveName}'. Only Spells can be added to the spellbook." });
                return;
            }

            // 3. Check if the player already knows the move.
            if (_gameState.PlayerState.SpellbookPages.Any(p => p != null && p.MoveID.Equals(moveId, System.StringComparison.OrdinalIgnoreCase)))
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"Player already knows {moveData.MoveName}." });
                return;
            }

            // 4. Find an empty spell page.
            int emptyPageIndex = _gameState.PlayerState.SpellbookPages.FindIndex(p => p == null);
            if (emptyPageIndex == -1)
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Spellbook is full. Cannot learn new moves." });
                return;
            }

            // 5. Add the move and provide feedback.
            _gameState.PlayerState.SpellbookPages[emptyPageIndex] = new SpellbookEntry(moveId, 0);
            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[palette_teal]Player learned {moveData.MoveName}!" });
        }

        private void HandleForgetMove(string moveId)
        {
            // 1. Find the page with the move to forget.
            int pageIndex = _gameState.PlayerState.SpellbookPages
                .FindIndex(p => p != null && p.MoveID.Equals(moveId, System.StringComparison.OrdinalIgnoreCase));

            // 2. Check if the player actually knows the move.
            if (pageIndex == -1)
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[error]Player does not know a move with ID '{moveId}'." });
                return;
            }

            // 3. Get move data for feedback message before removing.
            BattleDataCache.Moves.TryGetValue(moveId, out var moveData);
            string moveName = moveData?.MoveName ?? moveId;

            // 4. Remove the move (by setting the page to null) and provide feedback.
            _gameState.PlayerState.SpellbookPages[pageIndex] = null;
            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[palette_orange]Player forgot {moveName}." });
        }

        public void Update(GameTime gameTime)
        {
            // This system is purely event-driven and does not need per-frame updates.
        }
    }
}
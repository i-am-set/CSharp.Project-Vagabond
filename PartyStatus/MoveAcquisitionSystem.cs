using Microsoft.Xna.Framework;
using ProjectVagabond;
using ProjectVagabond.Battle;
using System.Linq;

namespace ProjectVagabond
{
    /// <summary>
    /// Handles acquiring new moves (Spells or Actions).
    /// </summary>
    public class MoveAcquisitionSystem
    {
        private readonly GameState _gameState;

        public MoveAcquisitionSystem()
        {
            _gameState = ServiceLocator.Get<GameState>();
            EventBus.Subscribe<GameEvents.PlayerMoveAdded>(OnPlayerMoveAdded);
        }

        private void OnPlayerMoveAdded(GameEvents.PlayerMoveAdded e)
        {
            if (_gameState.PlayerState == null) return;

            switch (e.Type)
            {
                case GameEvents.AcquisitionType.Add:
                    _gameState.PlayerState.AddMove(e.MoveID);
                    if (BattleDataCache.Moves.TryGetValue(e.MoveID, out var move))
                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[Palette_Sky]Obtained {move.MoveName}!" });
                    else
                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[error]Move ID '{e.MoveID}' not found." });
                    break;
                case GameEvents.AcquisitionType.Remove:
                    _gameState.PlayerState.RemoveMove(e.MoveID);
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[Palette_Fruit]Removed move {e.MoveID}." });
                    break;
            }
        }
    }
}
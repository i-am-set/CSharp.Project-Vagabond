using Microsoft.Xna.Framework;
using ProjectVagabond;
using ProjectVagabond.Battle;
using ProjectVagabond.Utils;

namespace ProjectVagabond
{
    /// <summary>
    /// Handles acquiring new relics (which grant passive abilities).
    /// </summary>
    public class RelicAcquisitionSystem
    {
        private readonly GameState _gameState;

        public RelicAcquisitionSystem()
        {
            _gameState = ServiceLocator.Get<GameState>();
            EventBus.Subscribe<GameEvents.PlayerRelicAdded>(OnPlayerRelicAdded);
        }

        private void OnPlayerRelicAdded(GameEvents.PlayerRelicAdded e)
        {
            if (_gameState.PlayerState == null) return;

            switch (e.Type)
            {
                case GameEvents.AcquisitionType.Add:
                    _gameState.PlayerState.AddRelic(e.RelicID);

                    if (BattleDataCache.Relics.TryGetValue(e.RelicID, out var relic))
                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[Palette_Sky]Obtained {relic.RelicName}!" });
                    else
                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[error]Relic ID '{e.RelicID}' not found." });

                    break;
                case GameEvents.AcquisitionType.Remove:
                    _gameState.PlayerState.RemoveRelic(e.RelicID);
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[Palette_Fruit]Removed relic {e.RelicID}." });
                    break;
            }
        }
    }
}
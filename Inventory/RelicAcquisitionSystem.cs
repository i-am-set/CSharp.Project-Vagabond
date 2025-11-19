using Microsoft.Xna.Framework;
using ProjectVagabond.Battle;
using System.Linq;

namespace ProjectVagabond
{
    /// <summary>
    /// Handles acquiring new relics (which grant passive abilities).
    /// </summary>
    public class RelicAcquisitionSystem : ISystem
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
                    // Relics are added to the Relic Inventory.
                    _gameState.PlayerState.AddRelic(e.RelicID);

                    if (BattleDataCache.Abilities.TryGetValue(e.RelicID, out var ability))
                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[palette_teal]Obtained {ability.RelicName}!" });
                    else
                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[error]Relic ID '{e.RelicID}' not found." });

                    break;
                case GameEvents.AcquisitionType.Remove:
                    _gameState.PlayerState.RemoveRelic(e.RelicID);
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[palette_orange]Removed relic {e.RelicID}." });
                    break;
            }
        }

        public void Update(GameTime gameTime) { }
    }
}
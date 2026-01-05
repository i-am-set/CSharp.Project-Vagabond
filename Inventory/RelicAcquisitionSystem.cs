using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond;
using ProjectVagabond.Battle;
using ProjectVagabond.Progression;
using ProjectVagabond.Scenes;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

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
                    _gameState.PlayerState.AddRelic(e.RelicID);

                    if (BattleDataCache.Relics.TryGetValue(e.RelicID, out var relic))
                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[palette_blue]Obtained {relic.RelicName}!" });
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

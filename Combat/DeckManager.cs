using System.Diagnostics;

namespace ProjectVagabond
{
    /// <summary>
    /// A centralized service for managing the player's persistent deck of cards.
    /// Provides a simple, encapsulated API for other game systems to add or remove cards
    /// without needing to know the underlying component structure.
    /// </summary>
    public class DeckManager
    {
        private readonly GameState _gameState;
        private readonly ComponentStore _componentStore;

        public DeckManager()
        {
            _gameState = ServiceLocator.Get<GameState>();
            _componentStore = ServiceLocator.Get<ComponentStore>();
        }

        /// <summary>
        /// Adds a new card (by its action ID) to the player's persistent master deck.
        /// </summary>
        /// <param name="actionId">The unique ID of the action to add as a card.</param>
        public void AddCardToPlayerDeck(string actionId)
        {
            var playerDeck = _componentStore.GetComponent<PlayerDeckComponent>(_gameState.PlayerEntityId);
            if (playerDeck == null)
            {
                Debug.WriteLine($"[DeckManager] [ERROR] Could not add card '{actionId}'. Player entity does not have a PlayerDeckComponent.");
                return;
            }

            // --- Validation ---
            // 1. Check for duplicates
            if (playerDeck.MasterDeck.Contains(actionId))
            {
                Debug.WriteLine($"[DeckManager] [INFO] Player already owns card '{actionId}'. No action taken.");
                return; // Don't add duplicates
            }

            // 2. Future validation: Check for deck size limits
            // const int MAX_DECK_SIZE = 30;
            // if (playerDeck.MasterDeck.Count >= MAX_DECK_SIZE)
            // {
            //     Debug.WriteLine($"[DeckManager] [INFO] Cannot add card '{actionId}'. Deck is full.");
            //     return;
            // }

            // --- Add Card and Publish Event ---
            playerDeck.MasterDeck.Add(actionId);
            Debug.WriteLine($"[DeckManager] Added card '{actionId}' to player's master deck.");

            EventBus.Publish(new GameEvents.PlayerDeckChanged());
        }
    }
}
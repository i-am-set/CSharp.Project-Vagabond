using System.Collections.Generic;

namespace ProjectVagabond
{
    /// <summary>
    /// A component that holds the player's persistent, master list of all collected action cards.
    /// This serves as the source for building the temporary combat deck.
    /// </summary>
    public class PlayerDeckComponent : IComponent, ICloneableComponent
    {
        /// <summary>
        /// The master list of action IDs for all cards the player owns.
        /// This list persists outside of combat.
        /// </summary>
        public List<string> MasterDeck { get; set; } = new List<string>();

        public IComponent Clone()
        {
            var clone = (PlayerDeckComponent)this.MemberwiseClone();
            // Ensure the clone gets its own instance of the list, not a reference to the template's list.
            clone.MasterDeck = new List<string>(this.MasterDeck);
            return clone;
        }
    }
}
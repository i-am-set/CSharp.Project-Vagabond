using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Battle
{
    /// <summary>
    /// Manages a combatant's "deck" of moves during a battle, including their hand, draw pile, and discard pile.
    /// </summary>
    public class CombatDeckManager
    {
        private const int HAND_SIZE = 4;
        private static readonly Random _rng = new Random();

        private List<SpellbookEntry> _deck = new List<SpellbookEntry>();
        private Queue<SpellbookEntry> _drawPile = new Queue<SpellbookEntry>();
        private List<SpellbookEntry> _discardPile = new List<SpellbookEntry>();

        public SpellbookEntry[] Hand { get; private set; } = new SpellbookEntry[HAND_SIZE];
        public IEnumerable<SpellbookEntry> DrawPile => _drawPile;
        public IEnumerable<SpellbookEntry> DiscardPile => _discardPile;

        /// <summary>
        /// Initializes the deck manager for a new battle.
        /// </summary>
        /// <param name="spellbookPages">A list of all spellbook entries the combatant knows.</param>
        public void Initialize(List<SpellbookEntry> spellbookPages)
        {
            // Create a new list of cloned SpellbookEntry objects for the battle deck.
            // This is a critical architectural step to ensure that the in-combat deck is
            // completely decoupled from the player's persistent spellbook state.
            _deck = spellbookPages.Where(p => p != null).Select(p => p.Clone()).ToList();

            _discardPile.Clear();
            ShuffleDeckIntoDrawPile();
            DrawInitialHand();
        }

        /// <summary>
        /// Moves a used spell from the hand to the discard pile and increments its usage count.
        /// </summary>
        /// <param name="entry">The spellbook entry that was cast from the hand.</param>
        public void CastMove(SpellbookEntry entry)
        {
            for (int i = 0; i < Hand.Length; i++)
            {
                if (Hand[i] == entry)
                {
                    // Increment the usage counter on the combat-specific clone.
                    entry.TimesUsed++;

                    // Always add the spell to the discard pile.
                    _discardPile.Add(Hand[i]);

                    Hand[i] = null; // Leave an empty slot
                    return;
                }
            }
        }

        /// <summary>
        /// Discards all cards currently in the hand into the discard pile.
        /// </summary>
        public void DiscardHand()
        {
            for (int i = 0; i < Hand.Length; i++)
            {
                if (Hand[i] != null)
                {
                    _discardPile.Add(Hand[i]);
                    Hand[i] = null;
                }
            }
        }

        /// <summary>
        /// Fills any empty slots in the hand at the start of a turn.
        /// </summary>
        /// <returns>A list of the specific SpellbookEntry objects that were drawn.</returns>
        public List<SpellbookEntry> DrawToFillHand()
        {
            var drawnCards = new List<SpellbookEntry>();
            for (int i = 0; i < Hand.Length; i++)
            {
                if (Hand[i] == null)
                {
                    if (_drawPile.Count == 0)
                    {
                        if (_discardPile.Count == 0)
                        {
                            // No cards in draw or discard, cannot draw.
                            continue;
                        }
                        // Reshuffle discard into draw pile
                        ShuffleDiscardIntoDrawPile();
                    }

                    if (_drawPile.Count > 0)
                    {
                        var newCard = _drawPile.Dequeue();
                        Hand[i] = newCard;
                        drawnCards.Add(newCard);
                    }
                }
            }
            return drawnCards;
        }

        private void DrawInitialHand()
        {
            for (int i = 0; i < HAND_SIZE; i++)
            {
                if (_drawPile.Count > 0)
                {
                    Hand[i] = _drawPile.Dequeue();
                }
                else
                {
                    Hand[i] = null;
                }
            }
        }

        private void ShuffleDeckIntoDrawPile()
        {
            var shuffled = _deck.OrderBy(a => _rng.Next()).ToList();
            _drawPile = new Queue<SpellbookEntry>(shuffled);
        }

        private void ShuffleDiscardIntoDrawPile()
        {
            var shuffled = _discardPile.OrderBy(a => _rng.Next()).ToList();
            _drawPile = new Queue<SpellbookEntry>(shuffled);
            _discardPile.Clear();
        }
    }
}
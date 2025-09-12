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
        private const int HAND_SIZE = 6;
        private static readonly Random _rng = new Random();

        private List<MoveData> _deck = new List<MoveData>();
        private Queue<MoveData> _drawPile = new Queue<MoveData>();
        private List<MoveData> _discardPile = new List<MoveData>();

        public MoveData[] Hand { get; private set; } = new MoveData[HAND_SIZE];
        public IEnumerable<MoveData> DrawPile => _drawPile;
        public IEnumerable<MoveData> DiscardPile => _discardPile;

        /// <summary>
        /// Initializes the deck manager for a new battle.
        /// </summary>
        /// <param name="knownMoveIDs">A list of all move IDs the combatant knows.</param>
        public void Initialize(List<string> knownMoveIDs)
        {
            _deck.Clear();
            foreach (var moveId in knownMoveIDs)
            {
                if (BattleDataCache.Moves.TryGetValue(moveId, out var moveData))
                {
                    _deck.Add(moveData);
                }
            }

            _discardPile.Clear();
            ShuffleDeckIntoDrawPile();
            DrawInitialHand();
        }

        /// <summary>
        /// Moves a used spell from the hand to the discard pile.
        /// </summary>
        /// <param name="move">The move that was cast.</param>
        public void CastMove(MoveData move)
        {
            for (int i = 0; i < Hand.Length; i++)
            {
                if (Hand[i] == move)
                {
                    _discardPile.Add(Hand[i]);
                    Hand[i] = null; // Leave an empty slot
                    return;
                }
            }
        }

        /// <summary>
        /// Fills any empty slots in the hand at the start of a turn.
        /// </summary>
        public void DrawToFillHand()
        {
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
                        Hand[i] = _drawPile.Dequeue();
                    }
                }
            }
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
            _drawPile = new Queue<MoveData>(shuffled);
        }

        private void ShuffleDiscardIntoDrawPile()
        {
            var shuffled = _discardPile.OrderBy(a => _rng.Next()).ToList();
            _drawPile = new Queue<MoveData>(shuffled);
            _discardPile.Clear();
        }
    }
}
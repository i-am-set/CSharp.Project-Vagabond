using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ProjectVagabond.Battle
{
    public class CombatDeckManager
    {
        private const int HAND_SIZE = 4;
        private static readonly Random _rng = new Random();

        private List<MoveEntry> _deck = new List<MoveEntry>();
        private Queue<MoveEntry> _drawPile = new Queue<MoveEntry>();
        private List<MoveEntry> _discardPile = new List<MoveEntry>();

        public MoveEntry[] Hand { get; private set; } = new MoveEntry[HAND_SIZE];
        public IEnumerable<MoveEntry> DrawPile => _drawPile;
        public IEnumerable<MoveEntry> DiscardPile => _discardPile;

        public void Initialize(List<MoveEntry> spells)
        {
            // Create a new list of cloned MoveEntry objects for the battle deck.
            _deck = spells.Where(p => p != null).Select(p => p.Clone()).ToList();

            _discardPile.Clear();
            ShuffleDeckIntoDrawPile();
            DrawInitialHand();
        }

        public void CastMove(MoveEntry entry)
        {
            for (int i = 0; i < Hand.Length; i++)
            {
                if (Hand[i] == entry)
                {
                    entry.TimesUsed++;
                    _discardPile.Add(Hand[i]);
                    Hand[i] = null;
                    return;
                }
            }
        }

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

        public List<MoveEntry> DrawToFillHand()
        {
            var drawnCards = new List<MoveEntry>();
            for (int i = 0; i < Hand.Length; i++)
            {
                if (Hand[i] == null)
                {
                    if (_drawPile.Count == 0)
                    {
                        if (_discardPile.Count == 0) continue;
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
                if (_drawPile.Count > 0) Hand[i] = _drawPile.Dequeue();
                else Hand[i] = null;
            }
        }

        private void ShuffleDeckIntoDrawPile()
        {
            var shuffled = _deck.OrderBy(a => _rng.Next()).ToList();
            _drawPile = new Queue<MoveEntry>(shuffled);
        }

        private void ShuffleDiscardIntoDrawPile()
        {
            var shuffled = _discardPile.OrderBy(a => _rng.Next()).ToList();
            _drawPile = new Queue<MoveEntry>(shuffled);
            _discardPile.Clear();
        }
    }
}

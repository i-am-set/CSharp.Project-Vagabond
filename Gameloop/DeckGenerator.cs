using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Scenes
{
    public static class DeckGenerator
    {
        private static readonly Random _random = new Random();

        private static Card CreateCard(RunContext context, CardSuit suit, CardType type, int rank, int baseValue)
        {
            return new Card(suit, type, rank, baseValue);
        }

        public static List<Card> Generate(RunContext context)
        {
            return GenerateClassic(context);
        }

        private static List<Card> GenerateClassic(RunContext context)
        {
            var deck = new List<Card>();
            for (int i = 2; i <= 10; i++) deck.Add(CreateCard(context, CardSuit.Hearts, CardType.Potion, i, i));
            for (int i = 2; i <= 10; i++) deck.Add(CreateCard(context, CardSuit.Diamonds, CardType.Weapon, i, i));
            for (int i = 2; i <= 14; i++) deck.Add(CreateCard(context, CardSuit.Spades, CardType.Monster, i, i));
            for (int i = 2; i <= 14; i++) deck.Add(CreateCard(context, CardSuit.Clubs, CardType.Monster, i, i));

            var shuffled = deck.OrderBy(x => _random.Next()).ToList();
            var treasureCard = CreateCard(context, CardSuit.None, CardType.Treasure, 0, 0);
            shuffled.Insert(shuffled.Count / 2, treasureCard);

            return shuffled;
        }
    }
}
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
            var card = new Card(suit, type, rank, baseValue);
            card.Modifier = context.GetCardModifier(suit, rank);
            return card;
        }

        public static List<Card> Generate(RunContext context)
        {
            if (context.Mode == GameMode.Classic)
            {
                return GenerateClassic(context);
            }
            return GenerateRoguelike(context);
        }

        private static List<Card> GenerateClassic(RunContext context)
        {
            var deck = new List<Card>();
            for (int i = 2; i <= 10; i++) deck.Add(CreateCard(context, CardSuit.Hearts, CardType.Potion, i, i));
            for (int i = 2; i <= 10; i++) deck.Add(CreateCard(context, CardSuit.Diamonds, CardType.Weapon, i, i));
            for (int i = 2; i <= 14; i++) deck.Add(CreateCard(context, CardSuit.Spades, CardType.Monster, i, i));
            for (int i = 2; i <= 14; i++) deck.Add(CreateCard(context, CardSuit.Clubs, CardType.Monster, i, i));

            return deck.OrderBy(x => _random.Next()).ToList();
        }

        private static List<Card> GenerateRoguelike(RunContext context)
        {
            int floor = context.Floor;
            int deckSize = 16 + (floor * 4);
            int monsterBudget = 60 + (floor * 10);
            int numPotions = floor <= 3 ? 3 : (floor <= 6 ? 3 : 4);
            int numWeapons = floor <= 4 ? 2 : 3;

            var masterPotions = new List<Card>();
            var masterWeapons = new List<Card>();
            var masterMonsters = new List<Card>();

            for (int i = 2; i <= 10; i++) masterPotions.Add(CreateCard(context, CardSuit.Hearts, CardType.Potion, i, i));
            for (int i = 2; i <= 10; i++) masterWeapons.Add(CreateCard(context, CardSuit.Diamonds, CardType.Weapon, i, i));
            for (int i = 2; i <= 14; i++) masterMonsters.Add(CreateCard(context, CardSuit.Spades, CardType.Monster, i, i));
            for (int i = 2; i <= 14; i++) masterMonsters.Add(CreateCard(context, CardSuit.Clubs, CardType.Monster, i, i));

            masterPotions = masterPotions.OrderBy(x => _random.Next()).ToList();
            masterWeapons = masterWeapons.OrderBy(x => _random.Next()).ToList();
            masterMonsters = masterMonsters.OrderBy(x => _random.Next()).ToList();

            var deck = new List<Card>();

            deck.AddRange(masterPotions.Take(numPotions));
            deck.AddRange(masterWeapons.Take(numWeapons));

            var lowMonsters = masterMonsters.Where(m => m.Value <= 4).ToList();
            var midMonsters = masterMonsters.Where(m => m.Value >= 5 && m.Value <= 8).ToList();
            var highMonsters = masterMonsters.Where(m => m.Value >= 9).ToList();

            float lowWeight = floor <= 3 ? 0.5f : (floor <= 6 ? 0.3f : 0.2f);
            float midWeight = floor <= 3 ? 0.4f : (floor <= 6 ? 0.5f : 0.5f);
            float highWeight = floor <= 3 ? 0.1f : (floor <= 6 ? 0.2f : 0.3f);

            int currentBudget = 0;
            int monsterCount = 0;
            int maxMonsters = deckSize - (numPotions + numWeapons);

            while (currentBudget < monsterBudget && monsterCount < maxMonsters)
            {
                double roll = _random.NextDouble();
                Card selected = null;

                if (roll < lowWeight && lowMonsters.Count > 0)
                {
                    selected = lowMonsters[0];
                    lowMonsters.RemoveAt(0);
                }
                else if (roll < lowWeight + midWeight && midMonsters.Count > 0)
                {
                    selected = midMonsters[0];
                    midMonsters.RemoveAt(0);
                }
                else if (highMonsters.Count > 0)
                {
                    selected = highHighMonsters(highMonsters);
                }
                else if (midMonsters.Count > 0)
                {
                    selected = midMonsters[0];
                    midMonsters.RemoveAt(0);
                }
                else if (lowMonsters.Count > 0)
                {
                    selected = lowMonsters[0];
                    lowMonsters.RemoveAt(0);
                }

                if (selected != null)
                {
                    deck.Add(selected);
                    currentBudget += selected.Value;
                    monsterCount++;
                }
                else
                {
                    break;
                }
            }

            while (deck.Count < deckSize)
            {
                if (masterPotions.Count > numPotions)
                {
                    deck.Add(masterPotions[numPotions]);
                    numPotions++;
                }
                else if (lowMonsters.Count > 0)
                {
                    deck.Add(lowMonsters[0]);
                    lowMonsters.RemoveAt(0);
                }
                else
                {
                    break;
                }
            }

            deck = deck.OrderBy(x => _random.Next()).ToList();

            bool isSafe = false;
            for (int i = 0; i < Math.Min(4, deck.Count); i++)
            {
                if (deck[i].Type == CardType.Potion || (deck[i].Type == CardType.Monster && deck[i].Value <= 4))
                {
                    isSafe = true;
                    break;
                }
            }

            if (!isSafe && deck.Count > 4)
            {
                int safeIndex = -1;
                for (int i = 4; i < deck.Count; i++)
                {
                    if (deck[i].Type == CardType.Potion || (deck[i].Type == CardType.Monster && deck[i].Value <= 4))
                    {
                        safeIndex = i;
                        break;
                    }
                }

                if (safeIndex != -1)
                {
                    var temp = deck[3];
                    deck[3] = deck[safeIndex];
                    deck[safeIndex] = temp;
                }
            }

            return deck;
        }

        private static Card highHighMonsters(List<Card> highMonsters)
        {
            var selected = highMonsters[0];
            highMonsters.RemoveAt(0);
            return selected;
        }
    }
}
using System.Collections.Generic;

namespace ProjectVagabond
{
    public enum GameMode
    {
        Classic,
        Roguelike
    }

    public class RunContext
    {
        public GameMode Mode { get; set; } = GameMode.Classic;
        public int Floor { get; set; } = 1;
        public int MaxFloors { get; set; } = 9;
        public int Health { get; set; } = 20;
        public int MaxHealth { get; set; } = 20;
        public int Gold { get; set; } = 0;

        public Dictionary<string, int> CardModifiers { get; set; } = new Dictionary<string, int>();

        public void Reset()
        {
            Floor = 1;
            MaxHealth = 20;
            Health = MaxHealth;
            Gold = 0;
            CardModifiers.Clear();
        }

        public int GetCardModifier(Scenes.CardSuit suit, int rank)
        {
            string key = $"{suit}_{rank}";
            return CardModifiers.TryGetValue(key, out int mod) ? mod : 0;
        }

        public void SetCardModifier(Scenes.CardSuit suit, int rank, int modifier)
        {
            string key = $"{suit}_{rank}";
            CardModifiers[key] = modifier;
        }
    }
}
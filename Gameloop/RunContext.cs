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
        public float RoomTimeLimit { get; set; } = 15f;
        public int CurrentScore { get; set; }
        public int RelicSeed { get; set; }

        public void Reset()
        {
            Floor = 1;
            MaxHealth = 20;
            Health = MaxHealth;
            CurrentScore = 0;
            RelicSeed = new System.Random().Next();
        }
    }
}
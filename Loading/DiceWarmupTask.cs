using Microsoft.Xna.Framework;
using ProjectVagabond.Dice;
using System.Collections.Generic;

namespace ProjectVagabond.Scenes
{
    public class DiceWarmupTask : LoadingTask
    {
        private readonly DiceRollingSystem _diceRollingSystem;
        private float _timer;
        private const float WARMUP_DURATION = 0.2f;

        public DiceWarmupTask() : base("Warming up physics engine...")
        {
            _diceRollingSystem = ServiceLocator.Get<DiceRollingSystem>();
        }

        public override void Start()
        {
            _timer = 0f;
            var rollRequest = new List<DiceGroup>
            {
                new DiceGroup { GroupId = "warmup_d6", NumberOfDice = 5, DieType = DieType.D6, Tint = Color.Transparent },
                new DiceGroup { GroupId = "warmup_d4", NumberOfDice = 5, DieType = DieType.D4, Tint = Color.Transparent }
            };
            _diceRollingSystem.Roll(rollRequest);
        }

        public override void Update(GameTime gameTime)
        {
            if (IsComplete) return;

            _timer += (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_timer >= WARMUP_DURATION)
            {
                _diceRollingSystem.ClearRoll();
                IsComplete = true;
            }
        }
    }
}
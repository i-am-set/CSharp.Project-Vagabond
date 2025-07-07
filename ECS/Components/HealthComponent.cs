namespace ProjectVagabond
{
    public class HealthComponent : IComponent
    {
        public int CurrentHealth { get; set; }
        public int MaxHealth { get; set; }

        /// <summary>
        /// Reduces the current health by a given amount.
        /// Health will not go below zero.
        /// </summary>
        /// <param name="amount">The amount of damage to take. Must be non-negative.</param>
        public void TakeDamage(int amount)
        {
            if (amount < 0) return; // Can't take negative damage
            CurrentHealth -= amount;
            if (CurrentHealth < 0)
            {
                CurrentHealth = 0;
            }
        }
    }
}
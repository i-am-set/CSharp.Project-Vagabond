using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond;
using System;
using System.Collections.Generic;

namespace ProjectVagabond
{
    public class HealthComponent : IComponent, ICloneableComponent
    {
        public int CurrentHealth { get; set; }
        public int MaxHealth { get; set; }

        /// <summary>
        /// Fired when health changes. The integer value is the amount of the change
        /// (negative for damage, positive for healing).
        /// </summary>
        public event Action<int> OnHealthChanged;

        /// <summary>
        /// Reduces the current health by a given amount.
        /// Health will not go below zero.
        /// </summary>
        /// <param name="amount">The amount of damage to take. Must be non-negative.</param>
        public void TakeDamage(int amount)
        {
            if (amount <= 0) return; // Can't take negative damage
            int oldHealth = CurrentHealth;
            CurrentHealth -= amount;
            if (CurrentHealth < 0)
            {
                CurrentHealth = 0;
            }

            if (CurrentHealth != oldHealth)
            {
                OnHealthChanged?.Invoke(-amount);
            }
        }

        /// <summary>
        /// Increases the current health by a given amount.
        /// Health will not go above the maximum.
        /// </summary>
        /// <param name="amount">The amount of health to restore. Must be non-negative.</param>
        public void Heal(int amount)
        {
            if (amount <= 0) return; // Can't heal negative amounts
            int oldHealth = CurrentHealth;
            CurrentHealth += amount;
            if (CurrentHealth > MaxHealth)
            {
                CurrentHealth = MaxHealth;
            }

            if (CurrentHealth != oldHealth)
            {
                OnHealthChanged?.Invoke(amount);
            }
        }

        public IComponent Clone()
        {
            var clone = (HealthComponent)this.MemberwiseClone();
            clone.OnHealthChanged = null; // Events should not be cloned
            return clone;
        }
    }
}
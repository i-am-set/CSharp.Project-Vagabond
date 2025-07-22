using System;

namespace ProjectVagabond
{
    public class StatsComponent : IComponent, IInitializableComponent, ICloneableComponent
    {
        // Main stats (1-10) - now with public setters for the Spawner
        public int Strength { get; set; }
        public int Agility { get; set; }
        public int Tenacity { get; set; }
        public int Intelligence { get; set; }
        public int Charm { get; set; }

        // Secondary stats (calculated from main stats)
        private int _maxHealthPoints;
        private int _maxEnergyPoints;
        private float _walkSpeed;
        private float _jogSpeed;
        private float _runSpeed;
        private int _carryCapacity;
        private int _mentalResistance;
        private int _socialInfluence;
        private int _shortRestDuration = 10; // minutes
        private int _longRestDuration = 60;
        private int _fullRestDuration = 60 * 8;

        // Current values
        private int _currentHealthPoints;
        private int _currentEnergyPoints;

        // Character selection stats - internal storage is always metric (kg)
        private float _weight;
        private int _age;
        private string _background;

        // Events for stat changes
        public event Action<int, int> OnHealthChanged; // current, max
        public event Action<int, int> OnEnergyChanged; // current, max
        public event Action OnStatsRecalculated;

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //
        // PROPERTIES
        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        // New Time Variance Property
        public float TimeVariance { get; set; } = 0.2f;

        // Secondary stats (read-only)
        public int MaxHealthPoints => _maxHealthPoints;
        public int MaxEnergyPoints => _maxEnergyPoints;
        public float WalkSpeed => _walkSpeed;
        public float JogSpeed => _jogSpeed;
        public float RunSpeed => _runSpeed;
        public int CarryCapacity => _carryCapacity;
        public int MentalResistance => _mentalResistance;
        public int SocialInfluence => _socialInfluence;
        public int ShortRestDuration => _shortRestDuration; // in minutes
        public int LongRestDuration => _longRestDuration; // in minutes
        public int FullRestDuration => _fullRestDuration; // in minutes
        public int ShortRestEnergyRestored => (int)Math.Floor((double)_maxEnergyPoints * 0.8f);
        public int LongRestEnergyRestored => _maxEnergyPoints;
        public int FullRestEnergyRestored => _maxEnergyPoints;

        // Current values (read-only)
        public int CurrentHealthPoints => _currentHealthPoints;
        public int CurrentEnergyPoints => _currentEnergyPoints;

        // Character info (read-only, always in metric units)
        public float Weight => _weight;
        public int Age => _age;
        public string Background => _background;

        // Calculated properties
        public float HealthPercentage => _maxHealthPoints > 0 ? (float)_currentHealthPoints / _maxHealthPoints : 0f;
        public float EnergyPercentage => _maxEnergyPoints > 0 ? (float)_currentEnergyPoints / _maxEnergyPoints : 0f;
        public bool IsAlive => _currentHealthPoints > 0;
        public bool IsExhausted => _currentEnergyPoints <= 0;

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //
        // CONSTRUCTOR & INITIALIZATION
        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        /// <summary>
        /// Parameterless constructor required by the Spawner.
        /// </summary>
        public StatsComponent()
        {
            // Set temporary default character info
            _weight = 70f; // kg
            _age = 25;
            _background = "Wanderer";
        }

        /// <summary>
        /// Called by the Spawner after all properties from the JSON have been set.
        /// </summary>
        public void Initialize()
        {
            // Clamp values to ensure they are within the valid 1-10 range
            Strength = Math.Clamp(Strength, 1, 10);
            Agility = Math.Clamp(Agility, 1, 10);
            Tenacity = Math.Clamp(Tenacity, 1, 10);
            Intelligence = Math.Clamp(Intelligence, 1, 10);
            Charm = Math.Clamp(Charm, 1, 10);

            RecalculateSecondaryStats();
            RestoreToFull();
        }

        public IComponent Clone()
        {
            var clone = (StatsComponent)this.MemberwiseClone();
            // Null out the event handlers on the new instance to prevent
            // subscribers from being carried over to the clone.
            clone.OnHealthChanged = null;
            clone.OnEnergyChanged = null;
            clone.OnStatsRecalculated = null;
            return clone;
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //
        // MAIN STAT MANAGEMENT
        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public void SetMainStats(int strength, int agility, int tenacity, int intelligence, int charm)
        {
            Strength = strength;
            Agility = agility;
            Tenacity = tenacity;
            Intelligence = intelligence;
            Charm = charm;

            Initialize(); // Use the same finalization logic
        }

        public void ModifyMainStat(StatType statType, int amount)
        {
            switch (statType)
            {
                case StatType.Strength:
                    Strength = Math.Clamp(Strength + amount, 1, 10);
                    break;
                case StatType.Agility:
                    Agility = Math.Clamp(Agility + amount, 1, 10);
                    break;
                case StatType.Tenacity:
                    Tenacity = Math.Clamp(Tenacity + amount, 1, 10);
                    break;
                case StatType.Intelligence:
                    Intelligence = Math.Clamp(Intelligence + amount, 1, 10);
                    break;
                case StatType.Charm:
                    Charm = Math.Clamp(Charm + amount, 1, 10);
                    break;
            }

            RecalculateSecondaryStats();
        }

        /// <summary>
        /// Sets character info. Weight is always expected in kilograms (kg).
        /// The UI layer is responsible for any unit conversions before calling this method.
        /// </summary>
        public void SetCharacterInfo(float weightInKg, int age, string background)
        {
            _weight = Math.Max(0f, weightInKg);
            _age = Math.Max(0, age);
            _background = background ?? "Unknown";
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //
        // SECONDARY STAT CALCULATION
        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        private void RecalculateSecondaryStats()
        {
            // Health Points
            int oldMaxHP = _maxHealthPoints;
            int _calculatedHealthPoints = (int)Math.Floor((Strength * 0.5f) + (2 * Tenacity) + ((1 * 0.5f) * ((Tenacity * 0.5f))));
            _maxHealthPoints = Math.Min(_calculatedHealthPoints, Global.MAX_MAX_HEALTH_ENERGY);

            // Energy Points
            int oldMaxEP = _maxEnergyPoints;
            int _calculatedEnergyPoints = 5 + (int)Math.Floor(Agility * 0.5f);
            _maxEnergyPoints = Math.Min(_calculatedEnergyPoints, Global.MAX_MAX_HEALTH_ENERGY);

            // World Map Move Speed 
            float weightFactor = Math.Max(0f, (_weight - 70f) * 0.01f); // Penalty for being over 70kg
            _walkSpeed = Math.Max(0.1f, 1.0f + (Agility * 0.08f) - weightFactor);
            _jogSpeed = Math.Max(0.1f, 1.0f + (Agility * 0.15f) - weightFactor);
            _runSpeed = _jogSpeed * 2.0f;

            // Carry Capacity = Base(20) + (Strength * 8) + (Tenacity * 3)
            _carryCapacity = 20 + (Strength * 8) + (Tenacity * 3);

            // Mental Resistance = Base(10) + (Intelligence * 6) + (Tenacity * 4)
            _mentalResistance = 10 + (Intelligence * 6) + (Tenacity * 4);

            // Social Influence = Base(5) + (Charm * 8) + (Intelligence * 2)
            _socialInfluence = 5 + (Charm * 8) + (Intelligence * 2);

            // Adjust current values if max values changed
            if (oldMaxHP != _maxHealthPoints)
            {
                float healthRatio = oldMaxHP > 0 ? (float)_currentHealthPoints / oldMaxHP : 1f;
                _currentHealthPoints = Math.Min(_maxHealthPoints, (int)(_maxHealthPoints * healthRatio));
                OnHealthChanged?.Invoke(_currentHealthPoints, _maxHealthPoints);
            }

            if (oldMaxEP != _maxEnergyPoints)
            {
                float energyRatio = oldMaxEP > 0 ? (float)_currentEnergyPoints / oldMaxEP : 1f;
                _currentEnergyPoints = Math.Min(_maxEnergyPoints, (int)(_maxEnergyPoints * energyRatio));
                OnEnergyChanged?.Invoke(_currentEnergyPoints, _maxEnergyPoints);
            }

            OnStatsRecalculated?.Invoke();
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //
        // HEALTH MANAGEMENT
        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public void TakeDamage(int damage)
        {
            if (damage <= 0) return;

            int oldHealth = _currentHealthPoints;
            _currentHealthPoints = Math.Max(0, _currentHealthPoints - damage);

            if (_currentHealthPoints != oldHealth)
            {
                OnHealthChanged?.Invoke(_currentHealthPoints, _maxHealthPoints);
            }
        }

        public void Heal(int healAmount)
        {
            if (healAmount <= 0) return;

            int oldHealth = _currentHealthPoints;
            _currentHealthPoints = Math.Min(_maxHealthPoints, _currentHealthPoints + healAmount);

            if (_currentHealthPoints != oldHealth)
            {
                OnHealthChanged?.Invoke(_currentHealthPoints, _maxHealthPoints);
            }
        }

        public void SetHealth(int healthAmount)
        {
            int oldHealth = _currentHealthPoints;
            _currentHealthPoints = Math.Clamp(healthAmount, 0, _maxHealthPoints);

            if (_currentHealthPoints != oldHealth)
            {
                OnHealthChanged?.Invoke(_currentHealthPoints, _maxHealthPoints);
            }
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //
        // ENERGY MANAGEMENT
        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public void ExertEnergy(int energyAmount)
        {
            if (energyAmount <= 0) return;

            int oldEnergy = _currentEnergyPoints;
            _currentEnergyPoints = Math.Max(0, _currentEnergyPoints - energyAmount);

            if (_currentEnergyPoints != oldEnergy)
            {
                OnEnergyChanged?.Invoke(_currentEnergyPoints, _maxEnergyPoints);
            }
        }

        public void RestoreEnergy(int energyAmount)
        {
            if (energyAmount <= 0) return;

            int oldEnergy = _currentEnergyPoints;
            _currentEnergyPoints = Math.Min(_maxEnergyPoints, _currentEnergyPoints + energyAmount);

            if (_currentEnergyPoints != oldEnergy)
            {
                OnEnergyChanged?.Invoke(_currentEnergyPoints, _maxEnergyPoints);
            }
        }

        public void SetEnergy(int energyAmount)
        {
            int oldEnergy = _currentEnergyPoints;
            _currentEnergyPoints = Math.Clamp(energyAmount, 0, _maxEnergyPoints);

            if (_currentEnergyPoints != oldEnergy)
            {
                OnEnergyChanged?.Invoke(_currentEnergyPoints, _maxEnergyPoints);
            }
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //
        // RESTING MECHANICS
        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public void Rest(RestType restType)
        {
            switch (restType)
            {
                case RestType.ShortRest:
                    // Short rest: restore some energy, minor health
                    RestoreEnergy(ShortRestEnergyRestored);
                    Heal(1);
                    break;

                case RestType.LongRest:
                    // Long rest: restore most energy, moderate health
                    RestoreEnergy(LongRestEnergyRestored);
                    Heal((int)Math.Ceiling(_maxHealthPoints * 0.5f));
                    break;

                case RestType.FullRest:
                    // Full rest: restore everything
                    RestoreToFull();
                    break;
            }
        }

        public void RestoreToFull()
        {
            SetHealth(_maxHealthPoints);
            SetEnergy(FullRestEnergyRestored);
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //
        // UTILITY METHODS
        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public bool CanExertEnergy(int energyAmount)
        {
            return _currentEnergyPoints >= energyAmount;
        }

        public int GetMainStat(StatType statType)
        {
            return statType switch
            {
                StatType.Strength => Strength,
                StatType.Agility => Agility,
                StatType.Tenacity => Tenacity,
                StatType.Intelligence => Intelligence,
                StatType.Charm => Charm,
                _ => 0
            };
        }
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //
    // ENUMS
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

    public enum StatType
    {
        Strength,
        Agility,
        Tenacity,
        Intelligence,
        Charm
    }

    public enum RestType
    {
        ShortRest,  // Quick break - minor recovery
        LongRest,   // Extended rest - moderate recovery
        FullRest    // Complete rest - full recovery
    }
}
using System;

namespace ProjectVagabond
{
    public class PlayerStats
    {
        // Main stats (1-10, average 5)
        private int _strength;
        private int _agility;
        private int _tenacity;
        private int _intelligence;
        private int _charm;

        // Secondary stats (calculated from main stats)
        private int _maxHealthPoints;
        private int _maxEnergyPoints;
        private float _moveSpeed;
        private int _carryCapacity;
        private int _mentalResistance;
        private int _socialInfluence;

        // Current values
        private int _currentHealthPoints;
        private int _currentEnergyPoints;

        // Character selection stats
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

        // Main stats (read-only)
        public int Strength => _strength;
        public int Agility => _agility;
        public int Tenacity => _tenacity;
        public int Intelligence => _intelligence;
        public int Charm => _charm;

        // Secondary stats (read-only)
        public int MaxHealthPoints => _maxHealthPoints;
        public int MaxEnergyPoints => _maxEnergyPoints;
        public float MoveSpeed => _moveSpeed;
        public int CarryCapacity => _carryCapacity;
        public int MentalResistance => _mentalResistance;
        public int SocialInfluence => _socialInfluence;

        // Current values (read-only)
        public int CurrentHealthPoints => _currentHealthPoints;
        public int CurrentEnergyPoints => _currentEnergyPoints;

        // Character info (read-only)
        public float Weight => _weight;
        public float DisplayWeight => Global.Instance.UseImperialUnits ? _weight * 2.20462f : _weight;
        public int Age => _age;
        public string Background => _background;

        // Calculated properties
        public float HealthPercentage => _maxHealthPoints > 0 ? (float)_currentHealthPoints / _maxHealthPoints : 0f;
        public float EnergyPercentage => _maxEnergyPoints > 0 ? (float)_currentEnergyPoints / _maxEnergyPoints : 0f;
        public bool IsAlive => _currentHealthPoints > 0;
        public bool IsExhausted => _currentEnergyPoints <= 0;

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //
        // CONSTRUCTOR
        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public PlayerStats(int strength = 5, int agility = 5, int tenacity = 5, int intelligence = 5, int charm = 5)
        {
            SetMainStats(strength, agility, tenacity, intelligence, charm);
            
            // Set default character info
            _weight = 70f; // Always stored in kg internally
            _age = 25;
            _background = "Wanderer";
            
            RecalculateSecondaryStats();
            RestoreToFull();
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //
        // MAIN STAT MANAGEMENT
        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public void SetMainStats(int strength, int agility, int tenacity, int intelligence, int charm)
        {
            _strength = Math.Clamp(strength, 1, 10);
            _agility = Math.Clamp(agility, 1, 10);
            _tenacity = Math.Clamp(tenacity, 1, 10);
            _intelligence = Math.Clamp(intelligence, 1, 10);
            _charm = Math.Clamp(charm, 1, 10);

            RecalculateSecondaryStats();
        }

        public void ModifyMainStat(StatType statType, int amount)
        {
            switch (statType)
            {
                case StatType.Strength:
                    _strength = Math.Clamp(_strength + amount, 1, 10);
                    break;
                case StatType.Agility:
                    _agility = Math.Clamp(_agility + amount, 1, 10);
                    break;
                case StatType.Tenacity:
                    _tenacity = Math.Clamp(_tenacity + amount, 1, 10);
                    break;
                case StatType.Intelligence:
                    _intelligence = Math.Clamp(_intelligence + amount, 1, 10);
                    break;
                case StatType.Charm:
                    _charm = Math.Clamp(_charm + amount, 1, 10);
                    break;
            }

            RecalculateSecondaryStats();
        }

        public void SetCharacterInfo(float weight, int age, string background)
        {
            _weight = Global.Instance.UseImperialUnits ? weight / 2.20462f : weight;
            _weight = Math.Max(0f, _weight);
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
            int _calculatedHealthPoints = (int)Math.Floor((_strength*0.5f)+(2*_tenacity)+ ((1*0.5f)*((_tenacity*0.5f))));
            _maxHealthPoints = Math.Min(_calculatedHealthPoints, Global.MAX_MAX_HEALTH_ENERGY);

            // Energy Points
            int oldMaxEP = _maxEnergyPoints;
            int _calculatedEnergyPoints = 5 + (int)Math.Floor(_agility*0.5f);
            _maxEnergyPoints = Math.Min(_calculatedEnergyPoints, Global.MAX_MAX_HEALTH_ENERGY);

            // Move Speed = Base(1.0) + (Agility * 0.1) - (Weight factor)
            float weightFactor = Math.Max(0f, (_weight - 70f) * 0.01f); // Penalty for being over 70kg
            _moveSpeed = Math.Max(0.1f, 1.0f + (_agility * 0.1f) - weightFactor);

            // Carry Capacity = Base(20) + (Strength * 8) + (Tenacity * 3)
            _carryCapacity = 20 + (_strength * 8) + (_tenacity * 3);

            // Mental Resistance = Base(10) + (Intelligence * 6) + (Tenacity * 4)
            _mentalResistance = 10 + (_intelligence * 6) + (_tenacity * 4);

            // Social Influence = Base(5) + (Charm * 8) + (Intelligence * 2)
            _socialInfluence = 5 + (_charm * 8) + (_intelligence * 2);

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
                    RestoreEnergy((int)Math.Ceiling((double)_maxEnergyPoints / 2));
                    Heal((int)Math.Ceiling(_maxHealthPoints*0.25f));
                    break;

                case RestType.LongRest:
                    // Long rest: restore most energy, moderate health
                    RestoreEnergy(_maxEnergyPoints);
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
            SetEnergy(_maxEnergyPoints);
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
                StatType.Strength => _strength,
                StatType.Agility => _agility,
                StatType.Tenacity => _tenacity,
                StatType.Intelligence => _intelligence,
                StatType.Charm => _charm,
                _ => 0
            };
        }

        public string GetWeightDisplayString()
        {
            if (Global.Instance.UseImperialUnits)
            {
                return $"{DisplayWeight:F1} lbs";
            }
            else
            {
                return $"{DisplayWeight:F1} kg";
            }
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
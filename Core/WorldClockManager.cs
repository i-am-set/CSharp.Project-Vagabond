using System;
using System.Security.Cryptography;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ProjectVagabond
{
    // Enum to represent the seasons
    public enum Season
    {
        Fall,
        Winter,
        Spring,
        Summer
    }

    public class WorldClockManager
    {
        // Singleton instance //
        public static WorldClockManager Instance { get; private set; }

        // Time-related events //
        public event Action OnTimeChanged;
        public event Action OnDayChanged;
        public event Action OnSeasonChanged;
        public event Action OnYearChanged;

        // Private fields for tracking time //
        private int _year;
        private int _dayOfYear; // 1-365
        private int _hour;      // 0-23
        private int _minute;    // 0-59

        // Public properties to access time information //
        public int CurrentYear => _year;
        public int CurrentHour => _hour;
        public int CurrentMinute => _minute;

        // Privte fields for season lengths //
        private const int _fallDays = 91;
        private const int _winterDays = 92;
        private const int _springDays = 91;
        private const int _summerDays = 91;

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public WorldClockManager()
        {
            if (Instance != null)
            {
                throw new Exception("WorldClockManager instance already exists.");
            }
            Instance = this;

            _year = RandomNumberGenerator.GetInt32(55, 785); // just random ass numbers
            _dayOfYear = RandomNumberGenerator.GetInt32(1, 365);
            _hour = RandomNumberGenerator.GetInt32(0, 23);
            _minute = RandomNumberGenerator.GetInt32(0, 59);
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        /// <summary>
        /// Calculates the current season based on the day of the year.
        /// </summary>
        public Season CurrentSeason
        {
            get
            {
                if (_dayOfYear <= _fallDays) return Season.Fall;
                if (_dayOfYear <= _fallDays + _winterDays) return Season.Winter;
                if (_dayOfYear <= _fallDays + _winterDays + _springDays) return Season.Spring;
                return Season.Summer;
            }
        }

        /// <summary>
        /// Calculates the day number within the current season (e.g., "Day 5 of Winter").
        /// </summary>
        public int DayOfSeason
        {
            get
            {
                switch (CurrentSeason)
                {
                    case Season.Fall:   return _dayOfYear;
                    case Season.Winter: return _dayOfYear - _fallDays;
                    case Season.Spring: return _dayOfYear - (_fallDays + _winterDays);
                    case Season.Summer: return _dayOfYear - (_fallDays + _winterDays + _springDays);
                    default:            return 0;
                }
            }
        }

        /// <summary>
        /// Advances the game time by a specified amount of days, hours, and minutes.
        /// </summary>
        /// <param name="days">Number of days to pass.</param>
        /// <param name="hours">Number of hours to pass.</param>
        /// <param name="minutes">Number of minutes to pass.</param>
        public void PassTime(int days = 0, int hours = 0, int minutes = 0)
        {
            if (days == 0 && hours == 0 && minutes == 0) return;

            // Calculate total minutes to add
            int totalMinutesToAdd = (days * 24 * 60) + (hours * 60) + minutes;

            // Add minutes and handle rollovers
            _minute += totalMinutesToAdd;
            int hoursToAdd = _minute / 60;
            _minute %= 60;

            // Add hours and handle rollovers
            _hour += hoursToAdd;
            int daysToAdd = _hour / 24;
            _hour %= 24;

            // If days have passed, invoke the day change event
            if (daysToAdd > 0)
            {
                Season previousSeason = CurrentSeason;
                _dayOfYear += daysToAdd;
                OnDayChanged?.Invoke();

                // Handle year rollovers
                while (_dayOfYear > 365)
                {
                    _dayOfYear -= 365;
                    _year++;
                    OnYearChanged?.Invoke();
                }

                if (CurrentSeason != previousSeason)
                {
                    OnSeasonChanged?.Invoke();
                }
            }
            
            // Notify listeners that time has changed
            OnTimeChanged?.Invoke();
        }

        /// <summary>
        /// Gets a formatted string for the current date, season, and time.
        /// </summary>
        /// <returns>A string like "Year 426, Day 1 of Fall - 08:00"</returns>
        public string GetDateSeasonTimeString()
        {
            return $"Year {CurrentYear}, Day {DayOfSeason} of {CurrentSeason} - {_hour:D2}:{_minute:D2}";
        }

        /// <summary>
        /// Gets a formatted string for the current date and season.
        /// </summary>
        /// <returns>A string like "Year 426, Day 1 of Fall"</returns>
        public string GetDateSeasonString()
        {
            return $"Year {CurrentYear}, Day {DayOfSeason} of {CurrentSeason}";
        }

        /// <summary>
        /// Gets a formatted string for the current date.
        /// </summary>
        /// <returns>A string like "Year 426, Day 1"</returns>
        public string GetDate()
        {
            return $"Year {CurrentYear}, Day {DayOfSeason}";
        }

        /// <summary>
        /// Gets a formatted string for the season.
        /// </summary>
        /// <returns>A string like "Fall"</returns>
        public string GetSeasonString()
        {
            return $"{CurrentSeason}";
        }

        /// <summary>
        /// Gets a formatted string for the time.
        /// </summary>
        /// <returns>A string like "08:00"</returns>
        public string GetTimeString()
        {
            return $"{_hour:D2}:{_minute:D2}";
        }
    }
}
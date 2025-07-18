﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.Xna.Framework;

namespace ProjectVagabond
{
    public enum Season
    {
        Fall,
        Winter,
        Spring,
        Summer
    }

    public class WorldClockManager
    {
        // Injected Dependencies
        private readonly Global _global;

        // Time-related events
        public event Action OnTimeChanged;
        public event Action OnDayChanged;
        public event Action OnSeasonChanged;
        public event Action OnYearChanged;
        public event Action<float> OnTimePassed;

        // Private fields for tracking time
        private int _year;
        private int _dayOfYear; // 1-365
        private int _hour;      // 0-23
        private int _minute;    // 0-59
        private int _second;    // 0-59
        private readonly Random _random = new();

        // Public properties to access time information
        public int CurrentYear => _year;
        public int CurrentHour => _hour;
        public int CurrentMinute => _minute;
        public int CurrentSecond => _second;
        public string CurrentTime => _global.Use24HourClock ? GetTimeString() : GetConverted24hToAmPm(GetTimeString());
        public float TimeScale { get; set; } = 1.0f;
        public TimeSpan CurrentTimeSpan { get; private set; }

        // Interpolation State Fields
        private bool _isInterpolating = false;
        private TimeSpan _interpolationStartTime;
        private TimeSpan _interpolationTargetTime;
        private float _interpolationDurationRealSeconds;
        private float _interpolationTimer;

        public bool IsInterpolatingTime => _isInterpolating;
        public float InterpolationDurationRealSeconds => _interpolationDurationRealSeconds;

        // Private fields for season lengths
        private const int _fallDays = 91;
        private const int _winterDays = 92;
        private const int _springDays = 91;
        private const int _summerDays = 91;

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public WorldClockManager()
        {
            _global = ServiceLocator.Get<Global>();

            _year = RandomNumberGenerator.GetInt32(55, 785);
            _dayOfYear = RandomNumberGenerator.GetInt32(1, 365);
            _hour = RandomNumberGenerator.GetInt32(0, 23);
            _minute = RandomNumberGenerator.GetInt32(0, 59);
            _second = RandomNumberGenerator.GetInt32(0, 59);

            CurrentTimeSpan = new TimeSpan(_dayOfYear - 1, _hour, _minute, _second);
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //
        // UPDATE & INTERPOLATION LOGIC
        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public void Update(GameTime gameTime)
        {
            var gameState = ServiceLocator.Get<GameState>();
            if (!_isInterpolating || gameState.IsPaused) return;

            _interpolationTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (_interpolationTimer >= _interpolationDurationRealSeconds)
            {
                // Visual Animation Finished
                _isInterpolating = false;
            }
        }

        /// <summary>
        /// Kicks off the time-lapse animation. The game will wait for this to complete.
        /// This overload is for convenience and backwards compatibility.
        /// </summary>
        public void PassTime(int days = 0, int hours = 0, int minutes = 0, int seconds = 0)
        {
            double totalSeconds = (double)seconds + ((double)minutes * 60) + ((double)hours * 3600) + ((double)days * 86400);
            PassTime(totalSeconds);
        }

        /// <summary>
        /// Instantly advances the logical game time and fires the OnTimePassed event.
        /// It then starts a background visual animation for the clock face to "catch up".
        /// </summary>
        /// <param name="totalSecondsToPass">The total number of seconds to pass, can be a fractional value.</param>
        /// <param name="randomizationFactor">A factor from 0.0 to 1.0 to randomize the duration. E.g., 0.5f means +/- 50%.</param>
        public void PassTime(double totalSecondsToPass, float randomizationFactor = 0f)
        {
            if (totalSecondsToPass <= 0) return;

            if (randomizationFactor > 0f)
            {
                double randomizationAmount = totalSecondsToPass * randomizationFactor;
                double minGameTimeDuration = totalSecondsToPass - randomizationAmount;
                double maxGameTimeDuration = totalSecondsToPass + randomizationAmount;
                totalSecondsToPass = _random.NextDouble() * (maxGameTimeDuration - minGameTimeDuration) + minGameTimeDuration;
            }

            // --- LOGICAL TIME ADVANCEMENT (INSTANT) ---
            var previousTimeSpan = CurrentTimeSpan;
            var targetTimeSpan = previousTimeSpan.Add(TimeSpan.FromSeconds(totalSecondsToPass));
            SetTimeFromTimeSpan(targetTimeSpan); // Update the internal time state immediately

            // Fire events based on what boundaries were crossed
            if (targetTimeSpan.Days > previousTimeSpan.Days)
            {
                Season previousSeason = CurrentSeasonFromDay(previousTimeSpan.Days + 1);
                int newDayOfYear = targetTimeSpan.Days + 1;
                int oldDayOfYear = previousTimeSpan.Days + 1;

                if ((newDayOfYear / 365) > (oldDayOfYear / 365)) OnYearChanged?.Invoke();
                OnDayChanged?.Invoke();
                if (CurrentSeasonFromDay(newDayOfYear) != previousSeason) OnSeasonChanged?.Invoke();
            }
            OnTimeChanged?.Invoke();
            OnTimePassed?.Invoke((float)totalSecondsToPass); // Fire the event for AI and other systems

            // --- VISUAL CLOCK ANIMATION (BACKGROUND) ---
            _interpolationStartTime = previousTimeSpan;
            _interpolationTargetTime = targetTimeSpan;

            const float minDuration = 0.4f;
            const float maxDuration = 6.0f;
            const long ONE_HOUR = 3600;
            const long EIGHT_HOURS = 28800;
            const long ONE_DAY = 86400;

            float scaleFactor;
            if (totalSecondsToPass > ONE_DAY) scaleFactor = 0.00005f;
            else if (totalSecondsToPass > EIGHT_HOURS) scaleFactor = 0.0001f;
            else if (totalSecondsToPass > ONE_HOUR) scaleFactor = 0.0002f;
            else scaleFactor = Global.BASE_TIME_SCALE;

            _interpolationDurationRealSeconds = Math.Clamp(minDuration + ((float)totalSecondsToPass * scaleFactor), minDuration, maxDuration);

            if (TimeScale > 0)
            {
                _interpolationDurationRealSeconds /= TimeScale;
            }

            _interpolationTimer = 0f;
            _isInterpolating = true;
        }

        /// <summary>
        /// Helper to update the public time fields from a TimeSpan object.
        /// </summary>
        private void SetTimeFromTimeSpan(TimeSpan time)
        {
            CurrentTimeSpan = time;
            // Handle year wrapping within the TimeSpan
            _year = 1 + (time.Days / 365);
            _dayOfYear = 1 + (time.Days % 365);
            _hour = time.Hours;
            _minute = time.Minutes;
            _second = time.Seconds;
        }

        /// <summary>
        /// Immediately stops any ongoing time interpolation.
        /// </summary>
        public void CancelInterpolation()
        {
            if (!_isInterpolating) return;
            _isInterpolating = false;
        }

        /// <summary>
        /// Gets the progress of the current time interpolation, from 0.0 to 1.0.
        /// </summary>
        public float GetInterpolationProgress()
        {
            if (!_isInterpolating || _interpolationDurationRealSeconds <= 0)
            {
                return 0f;
            }
            return Math.Clamp(_interpolationTimer / _interpolationDurationRealSeconds, 0f, 1f);
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //
        // BASE LOGIC
        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //
        public Season CurrentSeason => CurrentSeasonFromDay(_dayOfYear);

        private Season CurrentSeasonFromDay(int day)
        {
            if (day <= _fallDays) return Season.Fall;
            if (day <= _fallDays + _winterDays) return Season.Winter;
            if (day <= _fallDays + _winterDays + _springDays) return Season.Spring;
            return Season.Summer;
        }

        public int DayOfSeason
        {
            get
            {
                switch (CurrentSeason)
                {
                    case Season.Fall: return _dayOfYear;
                    case Season.Winter: return _dayOfYear - _fallDays;
                    case Season.Spring: return _dayOfYear - (_fallDays + _winterDays);
                    case Season.Summer: return _dayOfYear - (_fallDays + _winterDays + _springDays);
                    default: return 0;
                }
            }
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //
        // HELPER METHODS
        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //
        public string GetDateSeasonTimeString()
        {
            return $"Year {CurrentYear}, Day {DayOfSeason} of {CurrentSeason} - {_hour:D2}:{_minute:D2}";
        }

        public string GetDateSeasonString()
        {
            return $"Year {CurrentYear}, Day {DayOfSeason} of {CurrentSeason}";
        }

        public string GetDate()
        {
            return $"Year {CurrentYear}, Day {DayOfSeason}";
        }

        public string GetSeasonString()
        {
            return $"{CurrentSeason}";
        }

        public string GetTimeString()
        {
            return $"{_hour:D2}:{_minute:D2}";
        }

        public string GetCommaFormattedTimeFromSeconds(int totalSeconds)
        {
            if (totalSeconds <= 0) return "0 seconds";

            var timeSpan = TimeSpan.FromSeconds(totalSeconds);
            var parts = new List<string>();

            if (timeSpan.Days > 0) parts.Add($"{timeSpan.Days} {(timeSpan.Days == 1 ? "day" : "days")}");
            if (timeSpan.Hours > 0) parts.Add($"{timeSpan.Hours} {(timeSpan.Hours == 1 ? "hour" : "hours")}");
            if (timeSpan.Minutes > 0) parts.Add($"{timeSpan.Minutes} {(timeSpan.Minutes == 1 ? "minute" : "minutes")}");
            if (timeSpan.Seconds > 0) parts.Add($"{timeSpan.Seconds} {(timeSpan.Seconds == 1 ? "second" : "seconds")}");

            return string.Join(", ", parts);
        }

        public string GetPreciseFormattedTimeFromSeconds(float totalSeconds)
        {
            if (totalSeconds <= 0.01f) return "0s";

            var timeSpan = TimeSpan.FromSeconds(totalSeconds);
            var parts = new List<string>();

            if (timeSpan.Days > 0) parts.Add($"{timeSpan.Days}d");
            if (timeSpan.Hours > 0) parts.Add($"{timeSpan.Hours}hr");
            if (timeSpan.Minutes > 0) parts.Add($"{timeSpan.Minutes}min");
            if (timeSpan.Seconds > 0) parts.Add($"{timeSpan.Seconds}s");

            if (parts.Count == 0) return "0s";

            return string.Join(" ", parts);
        }

        public string GetCalculatedNewTime(string currentTime, int secondsToAdd)
        {
            currentTime = GetConvertedTo24hTime(currentTime);

            string[] timeParts = currentTime.Split(':');
            if (timeParts.Length < 2) throw new ArgumentException("Time must be in at least HH:MM format");
            if (!int.TryParse(timeParts[0], out int currentHour) || !int.TryParse(timeParts[1], out int currentMinute)) throw new ArgumentException("Invalid time format");
            if (currentHour < 0 || currentHour > 23 || currentMinute < 0 || currentMinute > 59) throw new ArgumentException("Invalid time component range");

            var timeSpan = new TimeSpan(currentHour, currentMinute, 0);
            var newTimeSpan = timeSpan.Add(TimeSpan.FromSeconds(secondsToAdd));

            return $"{newTimeSpan.Hours:D2}:{newTimeSpan.Minutes:D2}";
        }

        public string GetFormattedTimeFromMinutesShortHand(int totalMinutes)
        {
            if (totalMinutes == 0) return "0min";

            int days = totalMinutes / (24 * 60);
            int remainingMinutes = totalMinutes % (24 * 60);
            int hours = remainingMinutes / 60;
            int minutes = remainingMinutes % 60;

            var parts = new List<string>();

            if (days > 0)
            {
                parts.Add($"{days}d");
                parts.Add($"{hours}hr");
                parts.Add($"{minutes}min");
            }
            else if (hours > 0)
            {
                parts.Add($"{hours}hr");
                parts.Add($"{minutes}min");
            }
            else if (minutes > 0)
            {
                parts.Add($"{minutes}min");
            }

            return parts.Count > 0 ? string.Join(" ", parts) : "0min";
        }

        public string GetFormattedTimeFromSecondsShortHand(int totalSeconds)
        {
            if (totalSeconds == 0) return "0 sec";

            var ts = TimeSpan.FromSeconds(totalSeconds);
            var parts = new List<string>();

            if (ts.Days > 0) parts.Add($"{ts.Days} d");
            if (ts.Hours > 0) parts.Add($"{ts.Hours} hr");
            if (ts.Minutes > 0) parts.Add($"{ts.Minutes} min");
            if (ts.Seconds > 0) parts.Add($"{ts.Seconds} sec");

            return parts.Count > 0 ? string.Join(" ", parts) : "0 sec";
        }

        public string GetFormattedTimeFromSecondsShortHand(float totalSeconds)
        {
            if (totalSeconds < 1.0f)
            {
                return $"{totalSeconds:F1}s";
            }
            return GetFormattedTimeFromSecondsShortHand((int)totalSeconds);
        }

        public string GetConverted24hToAmPm(string militaryTime)
        {
            string[] timeParts = militaryTime.Split(':');
            if (timeParts.Length < 2) throw new ArgumentException("Time must be in at least HH:MM format");
            if (!int.TryParse(timeParts[0], out int hour) || !int.TryParse(timeParts[1], out int minute)) throw new ArgumentException("Invalid time format");
            if (hour < 0 || hour > 23 || minute < 0 || minute > 59) throw new ArgumentException("Hour must be 0-23 and minute must be 0-59");

            string period = hour >= 12 ? "PM" : "AM";
            int displayHour = hour;

            if (hour == 0) displayHour = 12; // Midnight becomes 12 AM
            else if (hour > 12) displayHour = hour - 12; // Convert to 12-hour format

            return $"{displayHour}:{minute:D2} {period}";
        }

        public string GetConvertedTo24hTime(string time)
        {
            if (string.IsNullOrWhiteSpace(time)) throw new ArgumentException("Time cannot be null or empty");

            time = time.Trim();

            if (!time.ToUpper().Contains("AM") && !time.ToUpper().Contains("PM"))
            {
                string[] parts = time.Split(':');
                if (parts.Length < 2) throw new ArgumentException("Time must be in HH:MM format");
                if (!int.TryParse(parts[0], out int hour) || !int.TryParse(parts[1], out int minute)) throw new ArgumentException("Invalid time format");
                if (hour < 0 || hour > 23 || minute < 0 || minute > 59) throw new ArgumentException("Hour must be 0-23 and minute must be 0-59");
                return $"{hour:D2}:{minute:D2}";
            }

            bool isPM = time.ToUpper().Contains("PM");
            bool isAM = time.ToUpper().Contains("AM");

            if (!isPM && !isAM) throw new ArgumentException("Time must contain AM or PM");

            string timeOnly = time.ToUpper().Replace("AM", "").Replace("PM", "").Trim();
            string[] timeParts = timeOnly.Split(':');

            if (timeParts.Length < 2) throw new ArgumentException("Time must be in H:MM or HH:MM format");
            if (!int.TryParse(timeParts[0], out int inputHour) || !int.TryParse(timeParts[1], out int inputMinute)) throw new ArgumentException("Invalid time format");
            if (inputHour < 1 || inputHour > 12 || inputMinute < 0 || inputMinute > 59) throw new ArgumentException("Hour must be 1-12 and minute must be 0-59 for AM/PM format");

            int militaryHour = inputHour;
            if (isPM && inputHour != 12) militaryHour = inputHour + 12;
            else if (isAM && inputHour == 12) militaryHour = 0;

            return $"{militaryHour:D2}:{inputMinute:D2}";
        }
    }
}
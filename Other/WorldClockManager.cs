using System;
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

    public enum ActivityType
    {
        Waiting,
        Walking,
        Jogging,
        Running,
        Combat
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
        public event Action<float, ActivityType> OnTimePassed;

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
        public float TimeScale { get; private set; } = 1.0f;
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
            if (gameState.IsPaused) return;

            // This Update method is now ONLY for the visual interpolation of the clock face.
            // The logical passage of time is now driven by the ActionExecutionSystem.
            if (_isInterpolating)
            {
                _interpolationTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (_interpolationTimer >= _interpolationDurationRealSeconds)
                {
                    _isInterpolating = false;
                    // Snap to final time to ensure precision
                    SetTimeFromTimeSpan(_interpolationTargetTime);
                    OnTimeChanged?.Invoke();
                }
                else
                {
                    // Update display time based on interpolation progress
                    float progress = _interpolationTimer / _interpolationDurationRealSeconds;
                    var newDisplayTime = _interpolationStartTime + TimeSpan.FromTicks((long)((_interpolationTargetTime - _interpolationStartTime).Ticks * progress));
                    SetTimeFromTimeSpan(newDisplayTime);
                    OnTimeChanged?.Invoke();
                }
            }
        }

        /// <summary>
        /// Updates the time scale and dynamically adjusts any ongoing time interpolation.
        /// </summary>
        /// <param name="newTimeScale">The new time scale multiplier.</param>
        public void UpdateTimeScale(float newTimeScale)
        {
            if (newTimeScale <= 0 || newTimeScale == this.TimeScale) return;

            if (_isInterpolating)
            {
                // Calculate how much in-game time has already passed visually
                double totalGameSecondsToPass = (_interpolationTargetTime - _interpolationStartTime).TotalSeconds;
                float progress = _interpolationTimer / _interpolationDurationRealSeconds;
                double gameSecondsPassedSoFar = totalGameSecondsToPass * progress;

                // Calculate the remaining in-game time
                double remainingGameSeconds = totalGameSecondsToPass - gameSecondsPassedSoFar;

                // Calculate the new real-world duration for the remaining in-game time
                float newRemainingRealSeconds = (float)remainingGameSeconds / newTimeScale;

                // Update the total duration and the current timer to reflect the change
                _interpolationDurationRealSeconds = _interpolationTimer + newRemainingRealSeconds;
            }

            this.TimeScale = newTimeScale;
        }

        /// <summary>
        /// Instantly advances the logical game time and fires the OnTimePassed event.
        /// It then starts a background visual animation for the clock face to "catch up"
        /// over a specified real-world duration.
        /// </summary>
        /// <param name="gameSecondsToPass">The total number of in-game seconds to pass.</param>
        /// <param name="realSecondsDuration">The number of real-world seconds the visual interpolation should take.</param>
        /// <param name="activity">The type of activity that caused time to pass.</param>
        public void PassTime(double gameSecondsToPass, float realSecondsDuration, ActivityType activity)
        {
            if (gameSecondsToPass <= 0) return;

            // --- LOGICAL TIME ADVANCEMENT (INSTANT) ---
            var previousTimeSpan = CurrentTimeSpan;
            var targetTimeSpan = previousTimeSpan.Add(TimeSpan.FromSeconds(gameSecondsToPass));

            // Fire events based on what boundaries were crossed before updating the time
            if (targetTimeSpan.Days > previousTimeSpan.Days)
            {
                Season previousSeason = CurrentSeasonFromDay(previousTimeSpan.Days + 1);
                int newDayOfYear = targetTimeSpan.Days + 1;
                int oldDayOfYear = previousTimeSpan.Days + 1;

                if ((newDayOfYear / 365) > (oldDayOfYear / 365)) OnYearChanged?.Invoke();
                OnDayChanged?.Invoke();
                if (CurrentSeasonFromDay(newDayOfYear) != previousSeason) OnSeasonChanged?.Invoke();
            }

            // Fire the main event for systems like AI to react to the time budget.
            OnTimePassed?.Invoke((float)gameSecondsToPass, activity);

            // --- VISUAL CLOCK ANIMATION (BACKGROUND) ---
            _interpolationStartTime = CurrentTimeSpan;
            _interpolationTargetTime = targetTimeSpan;
            // The real duration is now calculated based on the current time scale
            _interpolationDurationRealSeconds = (float)gameSecondsToPass / TimeScale;
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
        /// Immediately stops any ongoing time interpolation and snaps the clock to the target time.
        /// </summary>
        public void CancelInterpolation()
        {
            if (!_isInterpolating) return;
            _isInterpolating = false;
            SetTimeFromTimeSpan(_interpolationTargetTime);
            OnTimeChanged?.Invoke();
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
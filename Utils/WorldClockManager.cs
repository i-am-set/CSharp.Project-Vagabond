using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

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
        private int _second;    // 0-59

        // Public properties to access time information //
        public int CurrentYear => _year;
        public int CurrentHour => _hour;
        public int CurrentMinute => _minute;
        public int CurrentSecond => _second;
        public string CurrentTime => Global.Instance.Use24HourClock ? GetTimeString() : GetConverted24hToAmPm(GetTimeString());

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
            _second = RandomNumberGenerator.GetInt32(0, 59);
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //
        // BASE LOGIC
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
                    case Season.Fall: return _dayOfYear;
                    case Season.Winter: return _dayOfYear - _fallDays;
                    case Season.Spring: return _dayOfYear - (_fallDays + _winterDays);
                    case Season.Summer: return _dayOfYear - (_fallDays + _winterDays + _springDays);
                    default: return 0;
                }
            }
        }

        /// <summary>
        /// Advances the game time by a specified amount of days, hours, minutes, and seconds.
        /// </summary>
        public void PassTime(int days = 0, int hours = 0, int minutes = 0, int seconds = 0)
        {
            if (days == 0 && hours == 0 && minutes == 0 && seconds == 0) return;

            // Calculate total seconds to add
            long totalSecondsToAdd = (long)seconds + ((long)minutes * 60) + ((long)hours * 3600) + ((long)days * 86400);
            if (totalSecondsToAdd == 0) return;

            // Add seconds and handle rollovers
            _second += (int)(totalSecondsToAdd % 60);
            long totalMinutesToAdd = totalSecondsToAdd / 60;
            if (_second >= 60)
            {
                _second -= 60;
                totalMinutesToAdd++;
            }

            // Add minutes and handle rollovers
            _minute += (int)(totalMinutesToAdd % 60);
            long totalHoursToAdd = totalMinutesToAdd / 60;
            if (_minute >= 60)
            {
                _minute -= 60;
                totalHoursToAdd++;
            }

            // Add hours and handle rollovers
            _hour += (int)(totalHoursToAdd % 24);
            int daysToAdd = (int)(totalHoursToAdd / 24);
            if (_hour >= 24)
            {
                _hour -= 24;
                daysToAdd++;
            }

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

            Core.CurrentTerminalRenderer.AddOutputToHistory($"[dimgray]{GetCommaFormattedTimeFromSeconds((int)totalSecondsToAdd)} passed");

            // Notify listeners that time has changed
            OnTimeChanged?.Invoke();
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //
        // HELPER METHODS
        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

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

        /// <summary>
        /// Converts total seconds into a human-readable string like "1 day, 2 hours, 3 minutes, 4 seconds".
        /// Only includes non-zero components, and handles singular/plural formatting.
        /// </summary>
        /// <param name="totalSeconds">Total seconds to convert</param>
        /// <returns>Formatted string like "1 day, 2 hours, 3 minutes, 4 seconds"</returns>
        public string GetCommaFormattedTimeFromSeconds(int totalSeconds)
        {
            if (totalSeconds <= 0)
                return "0 seconds";

            var timeSpan = TimeSpan.FromSeconds(totalSeconds);
            var parts = new List<string>();

            if (timeSpan.Days > 0)
                parts.Add($"{timeSpan.Days} {(timeSpan.Days == 1 ? "day" : "days")}");
            if (timeSpan.Hours > 0)
                parts.Add($"{timeSpan.Hours} {(timeSpan.Hours == 1 ? "hour" : "hours")}");
            if (timeSpan.Minutes > 0)
                parts.Add($"{timeSpan.Minutes} {(timeSpan.Minutes == 1 ? "minute" : "minutes")}");
            if (timeSpan.Seconds > 0)
                parts.Add($"{timeSpan.Seconds} {(timeSpan.Seconds == 1 ? "second" : "seconds")}");

            return string.Join(", ", parts);
        }

        /// <summary>
        /// Calculates what time it will be after adding seconds to a given time.
        /// </summary>
        /// <param name="currentTime">Current time in "HH:MM" or "HH:MM:SS" format</param>
        /// <param name="secondsToAdd">Number of seconds to add</param>
        /// <returns>New time in "HH:MM" format after adding the seconds</returns>
        public string GetCalculatedNewTime(string currentTime, int secondsToAdd)
        {
            currentTime = GetConvertedTo24hTime(currentTime);

            // Parse the current time
            string[] timeParts = currentTime.Split(':');
            if (timeParts.Length < 2)
                throw new ArgumentException("Time must be in at least HH:MM format");

            if (!int.TryParse(timeParts[0], out int currentHour) ||
                !int.TryParse(timeParts[1], out int currentMinute))
                throw new ArgumentException("Invalid time format");

            // Validate hour and minute ranges
            if (currentHour < 0 || currentHour > 23 || currentMinute < 0 || currentMinute > 59)
                throw new ArgumentException("Invalid time component range");

            var timeSpan = new TimeSpan(currentHour, currentMinute, 0);
            var newTimeSpan = timeSpan.Add(TimeSpan.FromSeconds(secondsToAdd));

            return $"{newTimeSpan.Hours:D2}:{newTimeSpan.Minutes:D2}";
        }

        /// <summary>
        /// Converts total minutes into a shorthand formatted string.
        /// Format: "Xd Yhr Zmin" where larger units force smaller units to display.
        /// Example: 1500 minutes = "1d 1hr 0min"
        /// </summary>
        /// <param name="totalMinutes">Total minutes to convert</param>
        /// <returns>Formatted shorthand string</returns>
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

        /// <summary>
        /// Converts total seconds into a shorthand formatted string.
        /// Format: "Xd Yhr Zmin Ws" where only non-zero values are shown.
        /// Example: 3661 seconds = "1hr 1min 1s"
        /// </summary>
        /// <param name="totalSeconds">Total seconds to convert</param>
        /// <returns>Formatted shorthand string</returns>
        public string GetFormattedTimeFromSecondsShortHand(int totalSeconds)
        {
            if (totalSeconds == 0) return "0 sec";

            var ts = TimeSpan.FromSeconds(totalSeconds);
            var parts = new List<string>();

            if (ts.Days > 0)
                parts.Add($"{ts.Days} d");
            if (ts.Hours > 0)
                parts.Add($"{ts.Hours} hr");
            if (ts.Minutes > 0)
                parts.Add($"{ts.Minutes} min");
            if (ts.Seconds > 0)
                parts.Add($"{ts.Seconds} sec");

            return parts.Count > 0 ? string.Join(" ", parts) : "0 sec";
        }

        /// <summary>
        /// Converts military time (24-hour format) to AM/PM format.
        /// </summary>
        /// <param name="militaryTime">24-hour in "HH:MM" or "HH:MM:SS" format</param>
        /// <returns>Time in AM/PM format (e.g., "9:15 PM")</returns>
        public string GetConverted24hToAmPm(string militaryTime)
        {
            string[] timeParts = militaryTime.Split(':');
            if (timeParts.Length < 2)
                throw new ArgumentException("Time must be in at least HH:MM format");

            if (!int.TryParse(timeParts[0], out int hour) ||
                !int.TryParse(timeParts[1], out int minute))
                throw new ArgumentException("Invalid time format");

            if (hour < 0 || hour > 23 || minute < 0 || minute > 59)
                throw new ArgumentException("Hour must be 0-23 and minute must be 0-59");

            string period = hour >= 12 ? "PM" : "AM";
            int displayHour = hour;

            if (hour == 0)
                displayHour = 12; // Midnight becomes 12 AM
            else if (hour > 12)
                displayHour = hour - 12; // Convert to 12-hour format

            return $"{displayHour}:{minute:D2} {period}";
        }

        /// <summary>
        /// Converts time to 24-hour format. If already in military format, returns as-is.
        /// </summary>
        /// <param name="time">Time in either "HH:MM" or "H:MM AM/PM" format (seconds are ignored)</param>
        /// <returns>Time in military format "HH:MM"</returns>
        public string GetConvertedTo24hTime(string time)
        {
            if (string.IsNullOrWhiteSpace(time))
                throw new ArgumentException("Time cannot be null or empty");

            time = time.Trim();

            if (!time.ToUpper().Contains("AM") && !time.ToUpper().Contains("PM"))
            {
                string[] parts = time.Split(':');
                if (parts.Length < 2)
                    throw new ArgumentException("Time must be in HH:MM format");

                if (!int.TryParse(parts[0], out int hour) || !int.TryParse(parts[1], out int minute))
                    throw new ArgumentException("Invalid time format");

                if (hour < 0 || hour > 23 || minute < 0 || minute > 59)
                    throw new ArgumentException("Hour must be 0-23 and minute must be 0-59");

                return $"{hour:D2}:{minute:D2}";
            }

            bool isPM = time.ToUpper().Contains("PM");
            bool isAM = time.ToUpper().Contains("AM");

            if (!isPM && !isAM)
                throw new ArgumentException("Time must contain AM or PM");

            string timeOnly = time.ToUpper().Replace("AM", "").Replace("PM", "").Trim();
            string[] timeParts = timeOnly.Split(':');

            if (timeParts.Length < 2)
                throw new ArgumentException("Time must be in H:MM or HH:MM format");

            if (!int.TryParse(timeParts[0], out int inputHour) || !int.TryParse(timeParts[1], out int inputMinute))
                throw new ArgumentException("Invalid time format");

            if (inputHour < 1 || inputHour > 12 || inputMinute < 0 || inputMinute > 59)
                throw new ArgumentException("Hour must be 1-12 and minute must be 0-59 for AM/PM format");

            int militaryHour = inputHour;

            if (isPM && inputHour != 12)
                militaryHour = inputHour + 12;
            else if (isAM && inputHour == 12)
                militaryHour = 0;

            return $"{militaryHour:D2}:{inputMinute:D2}";
        }
    }
}
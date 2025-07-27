using System.Text.RegularExpressions;

namespace ProjectVagabond.Dice
{
    /// <summary>
    /// A static helper class for parsing dice notation strings (e.g., "2d6", "1d6+5", "10").
    /// </summary>
    public static class DiceParser
    {
        // Regex to capture the components: (number of dice)d(number of sides)+(modifier)
        // All parts are optional except for the case of a single flat number.
        private static readonly Regex DiceRegex = new Regex(
            @"^(?:(\d+)[dD])?(\d+)(?:[+](-?\d+))?$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Parses a dice notation string into its component parts.
        /// </summary>
        /// <param name="notation">The string to parse (e.g., "2d6", "d6+2", "5").</param>
        /// <returns>A tuple containing the number of dice, the number of sides, and the flat modifier.</returns>
        public static (int numDice, int numSides, int modifier) Parse(string notation)
        {
            if (string.IsNullOrWhiteSpace(notation))
            {
                return (0, 0, 0);
            }

            notation = notation.Trim();

            // First, try to parse it as a simple integer.
            if (int.TryParse(notation, out int flatValue))
            {
                return (0, 0, flatValue);
            }

            // Handle the case of "d6", which implies "1d6".
            if (notation.StartsWith("d", System.StringComparison.OrdinalIgnoreCase))
            {
                notation = "1" + notation;
            }

            var match = DiceRegex.Match(notation);

            if (match.Success)
            {
                // Group 1: Number of dice (optional)
                int numDice = 1; // Default to 1 if not specified (e.g., "d6")
                if (match.Groups[1].Success && !string.IsNullOrEmpty(match.Groups[1].Value))
                {
                    int.TryParse(match.Groups[1].Value, out numDice);
                }

                // Group 2: Number of sides (required for dice notation)
                int.TryParse(match.Groups[2].Value, out int numSides);

                // Group 3: Modifier (optional)
                int modifier = 0;
                if (match.Groups[3].Success)
                {
                    int.TryParse(match.Groups[3].Value, out modifier);
                }

                return (numDice, numSides, modifier);
            }

            // If regex fails, it's not a valid format we support.
            return (0, 0, 0);
        }
    }
}
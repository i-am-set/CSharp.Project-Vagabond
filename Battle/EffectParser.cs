using ProjectVagabond.Battle;
using System;
using System.Globalization;

namespace ProjectVagabond.Utils
{
    /// <summary>
    /// A static utility class for parsing effect strings from MoveData into usable types.
    /// This centralizes parsing logic and provides robust error handling.
    /// </summary>
    public static class EffectParser
    {
        public static bool TryParseInt(string value, out int result)
        {
            return int.TryParse(value, CultureInfo.InvariantCulture, out result);
        }

        public static bool TryParseFloat(string value, out float result)
        {
            return float.TryParse(value, CultureInfo.InvariantCulture, out result);
        }

        public static bool TryParseIntArray(string value, out int[] result)
        {
            result = null;
            if (string.IsNullOrEmpty(value)) return false;

            var parts = value.Split(',');
            var intArray = new int[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                if (!int.TryParse(parts[i].Trim(), CultureInfo.InvariantCulture, out intArray[i]))
                {
                    return false;
                }
            }
            result = intArray;
            return true;
        }

        public static bool TryParseFloatArray(string value, out float[] result)
        {
            result = null;
            if (string.IsNullOrEmpty(value)) return false;

            var parts = value.Split(',');
            var floatArray = new float[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                if (!float.TryParse(parts[i].Trim(), CultureInfo.InvariantCulture, out floatArray[i]))
                {
                    return false;
                }
            }
            result = floatArray;
            return true;
        }

        public static bool TryParseStatusEffectParams(string value, out StatusEffectType type, out int chance, out int duration)
        {
            type = default;
            chance = 0;
            duration = 0;

            if (string.IsNullOrEmpty(value)) return false;

            var parts = value.Split(',');
            if (parts.Length != 3) return false;

            if (!Enum.TryParse(parts[0].Trim(), true, out type)) return false;
            if (!int.TryParse(parts[1].Trim(), out chance)) return false;
            if (!int.TryParse(parts[2].Trim(), out duration)) return false;

            return true;
        }
    }
}
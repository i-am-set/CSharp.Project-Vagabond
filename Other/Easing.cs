using ProjectVagabond;
using System;
using System.Collections.Generic;

namespace ProjectVagabond
{
    /// <summary>
    /// A static class providing a collection of easing functions for animations.
    /// These functions are based on the standard equations from easings.net.
    /// Each function takes a float 'x' (progress) from 0.0 to 1.0 and returns the eased value.
    /// </summary>
    public static class Easing
    {
        private static readonly Dictionary<string, Func<float, float>> _easingFunctions = new Dictionary<string, Func<float, float>>(StringComparer.OrdinalIgnoreCase)
        {
            { "Linear", x => x },
            { "EaseInSine", EaseInSine },
            { "EaseOutSine", EaseOutSine },
            { "EaseInOutSine", EaseInOutSine },
            { "EaseInQuad", EaseInQuad },
            { "EaseOutQuad", EaseOutQuad },
            { "EaseInOutQuad", EaseInOutQuad },
            { "EaseInCubic", EaseInCubic },
            { "EaseOutCubic", EaseOutCubic },
            { "EaseInOutCubic", EaseInOutCubic },
            { "EaseInQuart", EaseInQuart },
            { "EaseOutQuart", EaseOutQuart },
            { "EaseInOutQuart", EaseInOutQuart },
            { "EaseInQuint", EaseInQuint },
            { "EaseOutQuint", EaseOutQuint },
            { "EaseInOutQuint", EaseInOutQuint },
            { "EaseInExpo", EaseInExpo },
            { "EaseOutExpo", EaseOutExpo },
            { "EaseInOutExpo", EaseInOutExpo },
            { "EaseInCirc", EaseInCirc },
            { "EaseOutCirc", EaseOutCirc },
            { "EaseInOutCirc", EaseInOutCirc },
            { "EaseInBack", EaseInBack },
            { "EaseOutBack", EaseOutBack },
            { "EaseInOutBack", EaseInOutBack },
            { "EaseInElastic", EaseInElastic },
            { "EaseOutElastic", EaseOutElastic },
            { "EaseInOutElastic", EaseInOutElastic },
            { "EaseInBounce", EaseInBounce },
            { "EaseOutBounce", EaseOutBounce },
            { "EaseInOutBounce", EaseInOutBounce }
        };

        /// <summary>
        /// Retrieves an easing function delegate by its name.
        /// </summary>
        /// <param name="name">The case-insensitive name of the easing function.</param>
        /// <returns>The easing function delegate, or a linear function if the name is not found.</returns>
        public static Func<float, float> GetEasingFunction(string name)
        {
            if (string.IsNullOrEmpty(name) || !_easingFunctions.TryGetValue(name, out var func))
            {
                return _easingFunctions["Linear"]; // Fallback to linear
            }
            return func;
        }

        public static float EaseInSine(float x)
        {
            return 1 - (float)Math.Cos((x * Math.PI) / 2);
        }
        public static float EaseOutSine(float x)
        {
            return (float)Math.Sin((x * Math.PI) / 2);
        }

        public static float EaseInOutSine(float x)
        {
            return -((float)Math.Cos(Math.PI * x) - 1) / 2;
        }

        public static float EaseInQuad(float x)
        {
            return x * x;
        }

        public static float EaseOutQuad(float x)
        {
            return 1 - (1 - x) * (1 - x);
        }

        public static float EaseInOutQuad(float x)
        {
            return x < 0.5 ? 2 * x * x : 1 - (float)Math.Pow(-2 * x + 2, 2) / 2;
        }

        public static float EaseInCubic(float x)
        {
            return x * x * x;
        }

        public static float EaseOutCubic(float x)
        {
            return 1 - (float)Math.Pow(1 - x, 3);
        }

        public static float EaseInOutCubic(float x)
        {
            return x < 0.5 ? 4 * x * x * x : 1 - (float)Math.Pow(-2 * x + 2, 3) / 2;
        }

        public static float EaseInQuart(float x)
        {
            return x * x * x * x;
        }

        public static float EaseOutQuart(float x)
        {
            return 1 - (float)Math.Pow(1 - x, 4);
        }

        public static float EaseInOutQuart(float x)
        {
            return x < 0.5 ? 8 * x * x * x * x : 1 - (float)Math.Pow(-2 * x + 2, 4) / 2;
        }

        public static float EaseInQuint(float x)
        {
            return x * x * x * x * x;
        }

        public static float EaseOutQuint(float x)
        {
            return 1 - (float)Math.Pow(1 - x, 5);
        }

        public static float EaseInOutQuint(float x)
        {
            return x < 0.5 ? 16 * x * x * x * x * x : 1 - (float)Math.Pow(-2 * x + 2, 5) / 2;
        }

        public static float EaseInExpo(float x)
        {
            return x == 0 ? 0 : (float)Math.Pow(2, 10 * x - 10);
        }

        public static float EaseOutExpo(float x)
        {
            return x == 1 ? 1 : 1 - (float)Math.Pow(2, -10 * x);
        }

        public static float EaseInOutExpo(float x)
        {
            return x == 0
              ? 0
              : x == 1
              ? 1
              : x < 0.5 ? (float)Math.Pow(2, 20 * x - 10) / 2
              : (2 - (float)Math.Pow(2, -20 * x + 10)) / 2;
        }

        public static float EaseInCirc(float x)
        {
            return 1 - (float)Math.Sqrt(1 - Math.Pow(x, 2));
        }

        public static float EaseOutCirc(float x)
        {
            return (float)Math.Sqrt(1 - Math.Pow(x - 1, 2));
        }

        public static float EaseInOutCirc(float x)
        {
            return x < 0.5
              ? (1 - (float)Math.Sqrt(1 - Math.Pow(2 * x, 2))) / 2
              : ((float)Math.Sqrt(1 - Math.Pow(-2 * x + 2, 2)) + 1) / 2;
        }

        public static float EaseInBack(float x)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1;
            return c3 * x * x * x - c1 * x * x;
        }

        public static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1;
            return 1 + c3 * (float)Math.Pow(x - 1, 3) + c1 * (float)Math.Pow(x - 1, 2);
        }

        public static float EaseOutBackSlight(float x)
        {
            const float c1 = 1.0f; // Reduced from 1.70158f for less overshoot
            const float c3 = c1 + 1;
            return 1 + c3 * (float)Math.Pow(x - 1, 3) + c1 * (float)Math.Pow(x - 1, 2);
        }

        public static float EaseInOutBack(float x)
        {
            const float c1 = 1.70158f;
            const float c2 = c1 * 1.525f;
            return x < 0.5
              ? ((float)Math.Pow(2 * x, 2) * ((c2 + 1) * 2 * x - c2)) / 2
              : ((float)Math.Pow(2 * x - 2, 2) * ((c2 + 1) * (x * 2 - 2) + c2) + 2) / 2;
        }

        public static float EaseInElastic(float x)
        {
            const float c4 = (float)(2 * Math.PI) / 3;
            return x == 0
              ? 0
              : x == 1
              ? 1
              : -(float)Math.Pow(2, 10 * x - 10) * (float)Math.Sin((x * 10 - 10.75) * c4);
        }

        public static float EaseOutElastic(float x)
        {
            const float c4 = (float)(2 * Math.PI) / 3;
            return x == 0
              ? 0
              : x == 1
              ? 1
              : (float)Math.Pow(2, -10 * x) * (float)Math.Sin((x * 10 - 0.75) * c4) + 1;
        }

        public static float EaseInOutElastic(float x)
        {
            const float c5 = (float)(2 * Math.PI) / 4.5f;
            return x == 0
              ? 0
              : x == 1
              ? 1
              : x < 0.5
              ? -((float)Math.Pow(2, 20 * x - 10) * (float)Math.Sin((20 * x - 11.125) * c5)) / 2
              : ((float)Math.Pow(2, -20 * x + 10) * (float)Math.Sin((20 * x - 11.125) * c5)) / 2 + 1;
        }

        public static float EaseInBounce(float x)
        {
            return 1 - EaseOutBounce(1 - x);
        }

        public static float EaseOutBounce(float x)
        {
            const float n1 = 7.5625f;
            const float d1 = 2.75f;

            if (x < 1 / d1)
            {
                return n1 * x * x;
            }
            else if (x < 2 / d1)
            {
                return n1 * (x -= 1.5f / d1) * x + 0.75f;
            }
            else if (x < 2.5 / d1)
            {
                return n1 * (x -= 2.25f / d1) * x + 0.9375f;
            }
            else
            {
                return n1 * (x -= 2.625f / d1) * x + 0.984375f;
            }
        }

        public static float EaseInOutBounce(float x)
        {
            return x < 0.5
              ? (1 - EaseOutBounce(1 - 2 * x)) / 2
              : (1 + EaseOutBounce(2 * x - 1)) / 2;
        }
    }
}
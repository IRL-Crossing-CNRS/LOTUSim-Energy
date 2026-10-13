using UnityEngine;

namespace Lotusim
{
    public static class BeaufortScale
    {
        private static readonly (float maxSpeed, int force, string label, int points, float multiplier)[] Scale =
        {
            (0.5f,  0,  "Calm",             0,  1f),
            (1.5f,  1,  "Light air",        0,  1f),
            (3.3f,  2,  "Light breeze",     0,  1f),
            (5.5f,  3,  "Gentle breeze",    0,  1f),
            (7.9f,  4,  "Moderate breeze",  5,  1f),
            (10.7f, 5,  "Fresh breeze",     10, 1.1f),
            (13.8f, 6,  "Strong breeze",    15, 1.2f),
            (17.1f, 7,  "Near gale",        25, 1.3f),
            (20.7f, 8,  "Gale",             40, 1.5f),
            (24.4f, 9,  "Strong gale",      60, 1.75f),
            (28.4f, 10, "Storm",            85, 2f),
            (32.6f, 11, "Violent storm",    120,2.5f),
        };

        private static readonly (int force, string label, int points, float multiplier) Hurricane
            = (12, "Hurricane force", 160, 3f);

        public static (int force, string label, int points, float multiplier) GetForce(float speedMs)
        {
            foreach (var entry in Scale)
            {
                if (speedMs < entry.maxSpeed)
                    return (entry.force, entry.label, entry.points, entry.multiplier);
            }
            return Hurricane;
        }
    }
}
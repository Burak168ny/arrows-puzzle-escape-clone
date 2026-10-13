using UnityEngine;

namespace Arrows.Utils
{
    /// <summary>Allocation-free easing functions on t in [0,1].</summary>
    public static class Easing
    {
        public static float InQuad(float t) => t * t;
        public static float InCubic(float t) => t * t * t;
        public static float OutQuad(float t) => 1f - (1f - t) * (1f - t);
        public static float OutCubic(float t) { float u = 1f - t; return 1f - u * u * u; }
        public static float InOutQuad(float t) => t < 0.5f ? 2f * t * t : 1f - 2f * (1f - t) * (1f - t);

        public static float OutBack(float t, float overshoot = 1.70158f)
        {
            float u = t - 1f;
            return 1f + u * u * ((overshoot + 1f) * u + overshoot);
        }

        public static float OutElastic(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            const float period = 0.3f;
            return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t - period / 4f) * (2f * Mathf.PI) / period) + 1f;
        }
    }
}

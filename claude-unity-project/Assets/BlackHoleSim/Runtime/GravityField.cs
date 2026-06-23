using UnityEngine;

namespace BlackHoleSim
{
    /// <summary>Single-source softened Newtonian gravity. Pure functions, no scene state.</summary>
    public static class GravityField
    {
        public static Vector3 AccelerationAt(Vector3 source, float mu, float softening, Vector3 pos)
        {
            Vector3 r = source - pos;                 // toward the source
            float dist2 = r.sqrMagnitude + softening * softening;
            if (dist2 <= Mathf.Epsilon) return Vector3.zero;
            float invDist = 1f / Mathf.Sqrt(dist2);
            float invDist3 = invDist / dist2;         // 1 / (r^2+e^2)^(3/2)
            return mu * invDist3 * r;
        }

        public static float OrbitalSpeed(float mu, float radius)
        {
            return radius <= 0f ? 0f : Mathf.Sqrt(mu / radius);
        }
    }
}

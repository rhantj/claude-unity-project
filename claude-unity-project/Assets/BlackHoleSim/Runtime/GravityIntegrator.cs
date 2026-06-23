using UnityEngine;

namespace BlackHoleSim
{
    /// <summary>Velocity Verlet single step. Energy-stable for orbital motion.</summary>
    public static class GravityIntegrator
    {
        public static void Step(ref Vector3 pos, ref Vector3 vel, System.Func<Vector3, Vector3> accel, float dt)
        {
            Vector3 a0 = accel(pos);
            pos += vel * dt + 0.5f * a0 * dt * dt;
            Vector3 a1 = accel(pos);
            vel += 0.5f * (a0 + a1) * dt;
        }
    }
}

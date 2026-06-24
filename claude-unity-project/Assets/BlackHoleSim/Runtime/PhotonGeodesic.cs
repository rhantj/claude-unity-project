using UnityEngine;

namespace BlackHoleSim
{
    /// <summary>슈바르츠실트 null 측지선의 카르테시안 적분(순수함수). BH 중심 좌표 기준.
    /// a = -1.5·r_s·h²·pos/r⁵ (h=cross(pos,vel) 보존). 상수 1.5는 약장 편향 2·r_s/b를 재현.</summary>
    public static class PhotonGeodesic
    {
        public static float AngularMomentumSq(Vector3 pos, Vector3 vel)
            => Vector3.Cross(pos, vel).sqrMagnitude;

        public static Vector3 Acceleration(Vector3 pos, float h2, float rs)
        {
            float r2 = Vector3.Dot(pos, pos);
            if (r2 <= Mathf.Epsilon) return Vector3.zero;
            float invR5 = 1f / (r2 * r2 * Mathf.Sqrt(r2));
            return -1.5f * rs * h2 * invR5 * pos;
        }

        // velocity Verlet. h2는 중심력이라 보존되므로 한 번 계산해 넘긴다(가속도는 pos에만 의존).
        public static void Step(ref Vector3 pos, ref Vector3 vel, float h2, float rs, float dλ)
        {
            Vector3 a0 = Acceleration(pos, h2, rs);
            pos += vel * dλ + 0.5f * a0 * dλ * dλ;
            Vector3 a1 = Acceleration(pos, h2, rs);
            vel += 0.5f * (a0 + a1) * dλ;
        }
    }
}

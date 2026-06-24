using UnityEngine;

namespace BlackHoleSim
{
    /// <summary>Paczyński–Wiita 유사-뉴턴 중력. 풀 GR 없이 슈바르츠실트 ISCO(r=3·r_s)와
    /// 호라이즌 포획(원심 장벽 붕괴 → 나선 낙하)을 재현한다. r_s=0이면 순수 뉴턴으로 환원.</summary>
    public static class GravityField
    {
        const float MinGapFraction = 0.25f; // (r - r_s)를 이 비율의 r_s까지만 좁혀 호라이즌 근처에서 힘을 유한하게 유지

        public static Vector3 AccelerationAt(Vector3 source, float mu, float schwarzschildRadius, Vector3 pos)
        {
            Vector3 r = source - pos;                 // toward the source
            float dist = r.magnitude;
            if (dist <= Mathf.Epsilon) return Vector3.zero;

            // Paczyński–Wiita: Φ = -μ/(r - r_s) → a = μ/(r - r_s)^2 (호라이즌으로 갈수록 발산).
            float minGap = schwarzschildRadius * MinGapFraction + Mathf.Epsilon;
            float gap = Mathf.Max(dist - schwarzschildRadius, minGap);
            float aMag = mu / (gap * gap);
            return aMag * (r / dist);
        }

        /// <summary>PW 원궤도 속도 v=√(μ·r)/(r−r_s). r_s=0이면 케플러 √(μ/r). r≤r_s이면 0.</summary>
        public static float OrbitalSpeed(float mu, float radius, float schwarzschildRadius)
        {
            float gap = radius - schwarzschildRadius;
            if (radius <= 0f || gap <= 0f) return 0f;
            return Mathf.Sqrt(mu * radius) / gap;
        }
    }
}

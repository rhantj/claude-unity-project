using UnityEngine;

namespace BlackHoleSim
{
    /// <summary>강착원반 상대론적 셰이딩 인자(순수함수). c=1 단위. 셰이더 미러의 기준점.</summary>
    public static class Relativity
    {
        // 슈바르츠실트 원궤도 국소 속도 v=√((r_s/2)/(r−r_s)). r≤r_s이면 0, <1로 클램프.
        public static float OrbitalSpeed(float r, float rs)
        {
            float denom = r - rs;
            if (denom <= 0f) return 0f;
            return Mathf.Min(Mathf.Sqrt((rs * 0.5f) / denom), 0.999f);
        }

        // δ = 1/(γ(1−β·cosθ)). cosθ>0이면 접근(δ>1).
        public static float DopplerFactor(float beta, float cosAngle)
        {
            float gamma = 1f / Mathf.Sqrt(Mathf.Max(1f - beta * beta, 1e-6f));
            return 1f / (gamma * (1f - beta * cosAngle));
        }

        public static float GravitationalRedshift(float r, float rs)
            => Mathf.Sqrt(Mathf.Max(1f - rs / r, 0f));

        public static float BeamingIntensity(float dopplerFactor)
            => dopplerFactor * dopplerFactor * dopplerFactor * dopplerFactor;
    }
}

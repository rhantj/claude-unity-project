using UnityEngine;

namespace BlackHoleSim
{
    /// <summary>Tanner Helland 근사 — 온도(K)를 RGB로 변환. 셰이더의 BlackBodyColorApprox와 동일 다항식.</summary>
    public static class BlackBodyColor
    {
        public static Color Evaluate(float temperatureKelvin)
        {
            float t = Mathf.Clamp(temperatureKelvin, 1000f, 40000f) / 100f;

            float r = t <= 66f
                ? 255f
                : Mathf.Clamp(329.698727446f * Mathf.Pow(t - 60f, -0.1332047592f), 0f, 255f);

            float g = t <= 66f
                ? Mathf.Clamp(99.4708025861f * Mathf.Log(t) - 161.1195681661f, 0f, 255f)
                : Mathf.Clamp(288.1221695283f * Mathf.Pow(t - 60f, -0.0755148492f), 0f, 255f);

            float b = t >= 66f
                ? 255f
                : (t <= 19f ? 0f : Mathf.Clamp(138.5177312231f * Mathf.Log(t - 10f) - 305.0447927307f, 0f, 255f));

            return new Color(r / 255f, g / 255f, b / 255f, 1f);
        }
    }
}

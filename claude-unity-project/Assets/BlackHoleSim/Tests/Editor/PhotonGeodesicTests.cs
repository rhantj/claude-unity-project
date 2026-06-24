using NUnit.Framework;
using UnityEngine;
using BlackHoleSim;

namespace BlackHoleSim.Tests
{
    public class PhotonGeodesicTests
    {
        // 큰 충돌계수에서 약장 편향각 ≈ 2·r_s/b (GR 결과). 상수 1.5 검증.
        [Test]
        public void Deflection_WeakField_MatchesTwoRsOverB()
        {
            float rs = 2f, b = 120f;
            Vector3 pos = new Vector3(-2000f, b, 0f);
            Vector3 vel = new Vector3(1f, 0f, 0f);
            float h2 = PhotonGeodesic.AngularMomentumSq(pos, vel);

            for (int i = 0; i < 200000 && pos.x < 2000f; i++)
                PhotonGeodesic.Step(ref pos, ref vel, h2, rs, 0.05f);

            float deflection = Mathf.Atan2(-vel.y, vel.x); // 중심 쪽(-y)으로 휜 각
            // 상수 1.5는 해석적으로 2·r_s/b를 재현. 허용오차 5%는 Verlet 이산화 오차 여유.
            Assert.That(deflection, Is.EqualTo(2f * rs / b).Within(0.05f * (2f * rs / b)));
        }

        [Test]
        public void PhotonSphere_TangentialRay_KeepsRadiusBriefly()
        {
            float rs = 2f, photonR = 1.5f * rs; // = 3
            Vector3 pos = new Vector3(photonR, 0f, 0f);
            Vector3 vel = new Vector3(0f, 0f, 1f); // tangential
            float h2 = PhotonGeodesic.AngularMomentumSq(pos, vel);

            float maxDev = 0f;
            for (int i = 0; i < 300; i++)
            {
                PhotonGeodesic.Step(ref pos, ref vel, h2, rs, 0.01f);
                maxDev = Mathf.Max(maxDev, Mathf.Abs(pos.magnitude - photonR));
            }
            Assert.That(maxDev, Is.LessThan(0.2f), "포톤 스피어 근처에서 잠시 반경 유지(불안정)");
        }

        [Test]
        public void LowImpactParameter_RayFallsInsideHorizon()
        {
            float rs = 2f, b = 1.5f; // 임계(≈2.6·r_s) 이하 → 포획
            Vector3 pos = new Vector3(-200f, b, 0f);
            Vector3 vel = new Vector3(1f, 0f, 0f);
            float h2 = PhotonGeodesic.AngularMomentumSq(pos, vel);

            float minR = pos.magnitude;
            for (int i = 0; i < 100000; i++)
            {
                PhotonGeodesic.Step(ref pos, ref vel, h2, rs, 0.02f);
                minR = Mathf.Min(minR, pos.magnitude);
                if (minR <= rs) break;
            }
            Assert.That(minR, Is.LessThanOrEqualTo(rs), "임계 이하 광선은 호라이즌에 포획");
        }
    }
}

using NUnit.Framework;
using UnityEngine;
using BlackHoleSim;

namespace BlackHoleSim.Tests
{
    public class GravityCoreTests
    {
        [Test]
        public void AccelerationAt_PointsTowardSource_ReducesToNewtonianWhenHorizonZero()
        {
            // r_s = 0 → PW 환원 = 뉴턴 역제곱.
            Vector3 a = GravityField.AccelerationAt(Vector3.zero, 100f, 0f, new Vector3(5f, 0f, 0f));

            Assert.That(a.x, Is.LessThan(0f), "accel should point toward source (-x)");
            Assert.That(a.y, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(a.z, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(a.magnitude, Is.EqualTo(100f / 25f).Within(1e-3f)); // mu/r^2 = 4
        }

        [Test]
        public void AccelerationAt_PaczynskiWiita_DivergesNearHorizon()
        {
            // r_s = 2에서 r=5: a = mu/(r - r_s)^2 = 100/9, 뉴턴(100/25)보다 강하다.
            Vector3 pw = GravityField.AccelerationAt(Vector3.zero, 100f, 2f, new Vector3(5f, 0f, 0f));
            Assert.That(pw.magnitude, Is.EqualTo(100f / 9f).Within(1e-3f));
            Assert.That(pw.magnitude, Is.GreaterThan(100f / 25f), "PW가 같은 반경에서 뉴턴보다 강해야 한다");
        }

        [Test]
        public void AccelerationAt_StaysFiniteAtAndInsideHorizon()
        {
            Vector3 atCenter = GravityField.AccelerationAt(Vector3.zero, 100f, 2f, Vector3.zero);
            Assert.That(float.IsFinite(atCenter.x) && float.IsFinite(atCenter.y) && float.IsFinite(atCenter.z), Is.True);
            Assert.That(atCenter.magnitude, Is.EqualTo(0f).Within(1e-4f)); // r=0 → 방향 미정 → 0

            Vector3 onHorizon = GravityField.AccelerationAt(Vector3.zero, 100f, 2f, new Vector3(2f, 0f, 0f));
            Assert.That(float.IsFinite(onHorizon.magnitude), Is.True, "호라이즌에서도 힘이 발산하지 않아야 한다");
        }

        [Test]
        public void OrbitalSpeed_ReducesToKeplerWhenHorizonZero()
        {
            Assert.That(GravityField.OrbitalSpeed(100f, 10f, 0f), Is.EqualTo(Mathf.Sqrt(10f)).Within(1e-4f));
        }

        [Test]
        public void OrbitalSpeed_PaczynskiWiita_IsFasterThanKepler()
        {
            // v = √(μ·r)/(r − r_s) = √(1000)/8.
            float pw = GravityField.OrbitalSpeed(100f, 10f, 2f);
            Assert.That(pw, Is.EqualTo(Mathf.Sqrt(1000f) / 8f).Within(1e-4f));
            Assert.That(pw, Is.GreaterThan(Mathf.Sqrt(10f)), "PW 원궤도 속도가 케플러보다 빨라야 한다");

            Assert.That(GravityField.OrbitalSpeed(100f, 2f, 2f), Is.EqualTo(0f), "r≤r_s이면 안정 원궤도 없음 → 0");
        }

        [Test]
        public void Verlet_CircularOrbit_ConservesRadius_OverOnePeriod()
        {
            float mu = 100f, rs = 0f, r = 10f, dt = 0.01f;
            Vector3 source = Vector3.zero;
            Vector3 pos = new Vector3(r, 0f, 0f);
            float v = GravityField.OrbitalSpeed(mu, r, rs);
            Vector3 vel = new Vector3(0f, 0f, v); // tangential in XZ plane

            System.Func<Vector3, Vector3> accel = p => GravityField.AccelerationAt(source, mu, rs, p);

            int steps = Mathf.RoundToInt((2f * Mathf.PI * r / v) / dt);
            for (int i = 0; i < steps; i++)
                GravityIntegrator.Step(ref pos, ref vel, accel, dt);

            float finalR = (pos - source).magnitude;
            Assert.That(finalR, Is.EqualTo(r).Within(0.3f), "radius should be conserved for a circular orbit");
        }

        [Test]
        public void Verlet_InsideISCO_SpiralsInToHorizon()
        {
            // ISCO = 3·r_s. r_s=2 → ISCO=6. r=4(<6)에서 PW 원궤도 속도로 출발해도 불안정 → 호라이즌 진입.
            float mu = 100f, rs = 2f, r = 4f, dt = 0.005f;
            Vector3 source = Vector3.zero;
            Vector3 pos = new Vector3(r, 0f, 0f);
            float v = 0.8f * GravityField.OrbitalSpeed(mu, r, rs); // 아임계 각운동량 → 원심 장벽 부족 → 낙하
            Vector3 vel = new Vector3(0f, 0f, v);

            System.Func<Vector3, Vector3> accel = p => GravityField.AccelerationAt(source, mu, rs, p);

            float minR = r;
            for (int i = 0; i < 20000; i++)
            {
                GravityIntegrator.Step(ref pos, ref vel, accel, dt);
                minR = Mathf.Min(minR, (pos - source).magnitude);
                if (minR <= rs) break;
            }

            Assert.That(minR, Is.LessThanOrEqualTo(rs), "ISCO 안쪽 궤도는 호라이즌까지 나선 낙하해야 한다");
        }
    }
}

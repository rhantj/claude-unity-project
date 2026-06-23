using NUnit.Framework;
using UnityEngine;
using BlackHoleSim;

namespace BlackHoleSim.Tests
{
    public class GravityCoreTests
    {
        [Test]
        public void AccelerationAt_PointsTowardSource_WithInverseSquareMagnitude()
        {
            Vector3 a = GravityField.AccelerationAt(Vector3.zero, 100f, 0f, new Vector3(5f, 0f, 0f));

            Assert.That(a.x, Is.LessThan(0f), "accel should point toward source (-x)");
            Assert.That(a.y, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(a.z, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(a.magnitude, Is.EqualTo(100f / 25f).Within(1e-3f)); // mu/r^2 = 4
        }

        [Test]
        public void Softening_PreventsSingularityAtCenter()
        {
            Vector3 a = GravityField.AccelerationAt(Vector3.zero, 100f, 0.5f, Vector3.zero);
            Assert.That(float.IsFinite(a.x) && float.IsFinite(a.y) && float.IsFinite(a.z), Is.True);
            Assert.That(a.magnitude, Is.EqualTo(0f).Within(1e-4f)); // at center r=0 -> zero direction
        }

        [Test]
        public void OrbitalSpeed_IsSqrtMuOverRadius()
        {
            Assert.That(GravityField.OrbitalSpeed(100f, 10f), Is.EqualTo(Mathf.Sqrt(10f)).Within(1e-4f));
        }

        [Test]
        public void Verlet_CircularOrbit_ConservesRadius_OverOnePeriod()
        {
            float mu = 100f, soft = 0f, r = 10f, dt = 0.01f;
            Vector3 source = Vector3.zero;
            Vector3 pos = new Vector3(r, 0f, 0f);
            float v = GravityField.OrbitalSpeed(mu, r);
            Vector3 vel = new Vector3(0f, 0f, v); // tangential in XZ plane

            System.Func<Vector3, Vector3> accel = p => GravityField.AccelerationAt(source, mu, soft, p);

            int steps = Mathf.RoundToInt((2f * Mathf.PI * r / v) / dt);
            for (int i = 0; i < steps; i++)
                GravityIntegrator.Step(ref pos, ref vel, accel, dt);

            float finalR = (pos - source).magnitude;
            Assert.That(finalR, Is.EqualTo(r).Within(0.3f), "radius should be conserved for a circular orbit");
        }

        [Test]
        public void BlackHole_IsCaptured_TrueInsideHorizon()
        {
            var go = new GameObject("bh");
            go.transform.position = Vector3.zero;
            var bh = go.AddComponent<BlackHole>();
            bh.Configure(gravitationalConstant: 1f, mass: 100f, softening: 0.5f, eventHorizonRadius: 2f);

            Assert.That(bh.IsCaptured(new Vector3(1f, 0f, 0f)), Is.True);
            Assert.That(bh.IsCaptured(new Vector3(5f, 0f, 0f)), Is.False);
            Assert.That(bh.Mu, Is.EqualTo(100f).Within(1e-4f));

            Object.DestroyImmediate(go);
        }
    }
}

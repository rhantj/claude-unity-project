using NUnit.Framework;
using UnityEngine;
using BlackHoleSim;

namespace BlackHoleSim.Tests
{
    public class BlackHoleTests
    {
        [Test]
        public void Properties_AreReadWrite_AndAffectMu()
        {
            var go = new GameObject("BH");
            var bh = go.AddComponent<BlackHole>();
            bh.Configure(1f, 4000f, 0.5f, 2f);

            bh.GravitationalConstant = 2f;
            bh.Mass = 1000f;
            bh.Softening = 0.1f;
            bh.EventHorizonRadius = 3f;

            Assert.That(bh.GravitationalConstant, Is.EqualTo(2f));
            Assert.That(bh.Mass, Is.EqualTo(1000f));
            Assert.That(bh.Softening, Is.EqualTo(0.1f));
            Assert.That(bh.EventHorizonRadius, Is.EqualTo(3f));
            Assert.That(bh.Mu, Is.EqualTo(2000f).Within(1e-3f)); // G*mass

            Object.DestroyImmediate(go);
        }
    }
}

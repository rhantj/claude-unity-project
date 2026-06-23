using NUnit.Framework;
using UnityEngine;
using BlackHoleSim;

namespace BlackHoleSim.Tests
{
    public class BlackBodyColorTests
    {
        [Test]
        public void Evaluate_LowTemperature_IsRedDominant()
        {
            Color c = BlackBodyColor.Evaluate(1500f);
            Assert.Greater(c.r, c.b);
        }

        [Test]
        public void Evaluate_HighTemperature_IsBlueWhiteDominant()
        {
            Color c = BlackBodyColor.Evaluate(20000f);
            Assert.GreaterOrEqual(c.b, c.r);
            Assert.Greater(c.b, 0.9f);
        }

        [Test]
        public void Evaluate_ClampsBelowMinimumTemperature()
        {
            Color low = BlackBodyColor.Evaluate(500f);
            Color clamped = BlackBodyColor.Evaluate(1000f);
            Assert.AreEqual(clamped.r, low.r, 0.001f);
            Assert.AreEqual(clamped.g, low.g, 0.001f);
            Assert.AreEqual(clamped.b, low.b, 0.001f);
        }
    }
}

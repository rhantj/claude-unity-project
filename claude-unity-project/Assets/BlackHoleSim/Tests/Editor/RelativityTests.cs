using NUnit.Framework;
using UnityEngine;
using BlackHoleSim;

namespace BlackHoleSim.Tests
{
    public class RelativityTests
    {
        [Test]
        public void OrbitalSpeed_IsHalfC_AtISCO()
        {
            // v=√((r_s/2)/(r−r_s)), ISCO r=3·r_s → √((r_s/2)/(2·r_s))=0.5
            Assert.That(Relativity.OrbitalSpeed(6f, 2f), Is.EqualTo(0.5f).Within(1e-4f));
        }

        [Test]
        public void OrbitalSpeed_FasterInside_AndClampedBelowOne()
        {
            Assert.That(Relativity.OrbitalSpeed(8f, 2f), Is.LessThan(Relativity.OrbitalSpeed(6.5f, 2f)));
            Assert.That(Relativity.OrbitalSpeed(2.01f, 2f), Is.LessThan(1f));
        }

        [Test]
        public void DopplerFactor_BrightensApproaching_DimsReceding()
        {
            float beta = 0.5f;
            float approaching = Relativity.DopplerFactor(beta, 1f);  // 정면 접근
            float receding = Relativity.DopplerFactor(beta, -1f);    // 정면 후퇴
            Assert.That(approaching, Is.GreaterThan(1f));
            Assert.That(receding, Is.LessThan(1f));
        }

        [Test]
        public void GravitationalRedshift_InRange_AndSmallerInside()
        {
            float inner = Relativity.GravitationalRedshift(6f, 2f);
            float outer = Relativity.GravitationalRedshift(20f, 2f);
            Assert.That(inner, Is.GreaterThan(0f).And.LessThan(1f));
            Assert.That(inner, Is.LessThan(outer), "안쪽일수록 적색편이 강함(작은 값)");
        }

        [Test]
        public void BeamingIntensity_IsFourthPower()
        {
            Assert.That(Relativity.BeamingIntensity(2f), Is.EqualTo(16f).Within(1e-4f));
        }
    }
}

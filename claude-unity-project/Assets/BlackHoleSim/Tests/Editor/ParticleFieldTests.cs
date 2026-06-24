using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using BlackHoleSim;

namespace BlackHoleSim.Tests
{
    public class ParticleFieldTests
    {
        static ParticleField CreateField(out GameObject pfGo, out GameObject bhGo)
        {
            bhGo = new GameObject("TestBH4");
            var bh = bhGo.AddComponent<BlackHole>();

            pfGo = new GameObject("TestPF4");
            pfGo.AddComponent<ParticleSystem>();
            var pf = pfGo.AddComponent<ParticleField>();
            typeof(ParticleField)
                .GetField("blackHole", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(pf, bh);
            return pf;
        }

        [Test]
        public void Reinitialize_ResizesPositionArray_ToNewCount()
        {
            var pf = CreateField(out var pfGo, out var bhGo);

            pf.Reinitialize(50);

            Assert.That(pf.Count, Is.EqualTo(50));
            var posArray = (Vector3[])typeof(ParticleField)
                .GetField("pos", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(pf);
            Assert.That(posArray.Length, Is.EqualTo(50));

            Object.DestroyImmediate(pfGo);
            Object.DestroyImmediate(bhGo);
        }

        [Test]
        public void FloatProperties_AreReadWrite()
        {
            var pf = CreateField(out var pfGo, out var bhGo);

            pf.InnerRadius = 4f;
            pf.OuterRadius = 12f;
            pf.DiskThickness = 0.3f;
            pf.SpeedJitter = 0.2f;
            pf.MaxRadius = 50f;
            pf.InfallSpeedFactor = 0.7f;
            pf.ParticleSize = 0.25f;

            Assert.That(pf.InnerRadius, Is.EqualTo(4f));
            Assert.That(pf.OuterRadius, Is.EqualTo(12f));
            Assert.That(pf.DiskThickness, Is.EqualTo(0.3f));
            Assert.That(pf.SpeedJitter, Is.EqualTo(0.2f));
            Assert.That(pf.MaxRadius, Is.EqualTo(50f));
            Assert.That(pf.InfallSpeedFactor, Is.EqualTo(0.7f));
            Assert.That(pf.ParticleSize, Is.EqualTo(0.25f));

            Object.DestroyImmediate(pfGo);
            Object.DestroyImmediate(bhGo);
        }
    }
}

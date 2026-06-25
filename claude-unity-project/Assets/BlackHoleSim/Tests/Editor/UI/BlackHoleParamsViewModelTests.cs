using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using BlackHoleSim;
using BlackHoleSim.UI;

namespace BlackHoleSim.Tests.UI
{
    public class BlackHoleParamsViewModelTests
    {
        static (BlackHoleParamsViewModel vm, GameObject bhGo, GameObject camGo, GameObject lensGo, GameObject pfGo)
            CreateViewModel()
        {
            var bhGo = new GameObject("VmBH");
            var bh = bhGo.AddComponent<BlackHole>();

            var camGo = new GameObject("VmCam");
            var cam = camGo.AddComponent<Camera>();

            var lensGo = new GameObject("VmLens");
            var lens = lensGo.AddComponent<BlackHoleLensController>();
            lens.Configure(bh, cam);

            var pfGo = new GameObject("VmPF");
            pfGo.AddComponent<ParticleSystem>();
            var pf = pfGo.AddComponent<ParticleField>();
            typeof(ParticleField)
                .GetField("blackHole", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(pf, bh);
            pf.Reinitialize(10);

            var vm = new BlackHoleParamsViewModel(bh, lens, pf);
            return (vm, bhGo, camGo, lensGo, pfGo);
        }

        [Test]
        public void SettingDiskInnerRadius_WritesBackToLensController()
        {
            var (vm, bhGo, camGo, lensGo, pfGo) = CreateViewModel();
            var lens = lensGo.GetComponent<BlackHoleLensController>();

            vm.DiskInnerRadius.Value = 9.5f;

            Assert.That(lens.DiskInnerRadius, Is.EqualTo(9.5f).Within(1e-4f));
            Cleanup(bhGo, camGo, lensGo, pfGo);
        }

        [Test]
        public void SettingMass_WritesBackToBlackHole()
        {
            var (vm, bhGo, camGo, lensGo, pfGo) = CreateViewModel();
            var bh = bhGo.GetComponent<BlackHole>();

            vm.Mass.Value = 8000f;

            Assert.That(bh.Mass, Is.EqualTo(8000f));
            Cleanup(bhGo, camGo, lensGo, pfGo);
        }

        [Test]
        public void SettingParticleCount_ReinitializesParticleField()
        {
            var (vm, bhGo, camGo, lensGo, pfGo) = CreateViewModel();
            var pf = pfGo.GetComponent<ParticleField>();

            vm.ParticleCount.Value = 33;

            Assert.That(pf.Count, Is.EqualTo(33));
            Cleanup(bhGo, camGo, lensGo, pfGo);
        }

        [Test]
        public void InitialValues_MatchModelAtConstruction()
        {
            var (vm, bhGo, camGo, lensGo, pfGo) = CreateViewModel();
            var lens = lensGo.GetComponent<BlackHoleLensController>();

            Assert.That(vm.DiskOuterRadius.Value, Is.EqualTo(lens.DiskOuterRadius));
            Cleanup(bhGo, camGo, lensGo, pfGo);
        }

        static void Cleanup(GameObject bhGo, GameObject camGo, GameObject lensGo, GameObject pfGo)
        {
            Object.DestroyImmediate(bhGo);
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(lensGo);
            Object.DestroyImmediate(pfGo);
        }
    }
}

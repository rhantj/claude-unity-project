using System;
using UnityEngine;

namespace BlackHoleSim.UI
{
    /// <summary>BlackHole/BlackHoleLensController/ParticleField パラメータをObservableValueで露出するViewModel.
    /// 単方向バインディング: ObservableValue変更 → Model setter即時反映.</summary>
    public class BlackHoleParamsViewModel
    {
        public readonly ObservableValue<float> DiskInnerRadius;
        public readonly ObservableValue<float> DiskOuterRadius;
        public readonly ObservableValue<float> DiskThickness;
        public readonly ObservableValue<float> DiskDensity;
        public readonly ObservableValue<float> DiskTempInnerKelvin;
        public readonly ObservableValue<float> DiskTempOuterKelvin;
        public readonly ObservableValue<Color> DiskColorTint;
        public readonly ObservableValue<float> BeamingStrength;
        public readonly ObservableValue<float> RedshiftStrength;
        public readonly ObservableValue<float> PhotonRing;
        public readonly ObservableValue<int> StepCount;
        public readonly ObservableValue<float> StepSize;
        public readonly ObservableValue<float> StarDensity;

        public readonly ObservableValue<bool> ParticlesEnabled;
        public readonly ObservableValue<int> ParticleCount;
        public readonly ObservableValue<float> ParticleInnerRadius;
        public readonly ObservableValue<float> ParticleOuterRadius;
        public readonly ObservableValue<float> ParticleDiskThickness;
        public readonly ObservableValue<float> ParticleSpeedJitter;
        public readonly ObservableValue<float> ParticleMaxRadius;
        public readonly ObservableValue<float> ParticleInfallSpeedFactor;
        public readonly ObservableValue<float> ParticleSize;

        public readonly ObservableValue<float> GravitationalConstant;
        public readonly ObservableValue<float> Mass;
        public readonly ObservableValue<float> Softening;
        public readonly ObservableValue<float> EventHorizonRadius;

        public readonly ObservableValue<bool> PanelExpanded;
        public readonly ObservableValue<bool> DangerZoneExpanded;

        public BlackHoleParamsViewModel(BlackHole blackHole, BlackHoleLensController lens, ParticleField particles)
        {
            DiskInnerRadius = Bind(lens.DiskInnerRadius, v => lens.DiskInnerRadius = v);
            DiskOuterRadius = Bind(lens.DiskOuterRadius, v => lens.DiskOuterRadius = v);
            DiskThickness = Bind(lens.DiskThickness, v => lens.DiskThickness = v);
            DiskDensity = Bind(lens.DiskDensity, v => lens.DiskDensity = v);
            DiskTempInnerKelvin = Bind(lens.DiskTempInnerKelvin, v => lens.DiskTempInnerKelvin = v);
            DiskTempOuterKelvin = Bind(lens.DiskTempOuterKelvin, v => lens.DiskTempOuterKelvin = v);
            DiskColorTint = Bind(lens.DiskColorTint, v => lens.DiskColorTint = v);
            BeamingStrength = Bind(lens.BeamingStrength, v => lens.BeamingStrength = v);
            RedshiftStrength = Bind(lens.RedshiftStrength, v => lens.RedshiftStrength = v);
            PhotonRing = Bind(lens.PhotonRing, v => lens.PhotonRing = v);
            StepCount = Bind(lens.StepCount, v => lens.StepCount = v);
            StepSize = Bind(lens.StepSize, v => lens.StepSize = v);
            StarDensity = Bind(lens.StarDensity, v => lens.StarDensity = v);

            ParticlesEnabled = Bind(particles.enabled, v => particles.enabled = v);
            ParticleCount = Bind(particles.Count, v => particles.Count = v);
            ParticleInnerRadius = Bind(particles.InnerRadius, v => particles.InnerRadius = v);
            ParticleOuterRadius = Bind(particles.OuterRadius, v => particles.OuterRadius = v);
            ParticleDiskThickness = Bind(particles.DiskThickness, v => particles.DiskThickness = v);
            ParticleSpeedJitter = Bind(particles.SpeedJitter, v => particles.SpeedJitter = v);
            ParticleMaxRadius = Bind(particles.MaxRadius, v => particles.MaxRadius = v);
            ParticleInfallSpeedFactor = Bind(particles.InfallSpeedFactor, v => particles.InfallSpeedFactor = v);
            ParticleSize = Bind(particles.ParticleSize, v => particles.ParticleSize = v);

            GravitationalConstant = Bind(blackHole.GravitationalConstant, v => blackHole.GravitationalConstant = v);
            Mass = Bind(blackHole.Mass, v => blackHole.Mass = v);
            Softening = Bind(blackHole.Softening, v => blackHole.Softening = v);
            EventHorizonRadius = Bind(blackHole.EventHorizonRadius, v => blackHole.EventHorizonRadius = v);

            PanelExpanded = new ObservableValue<bool>(true);
            DangerZoneExpanded = new ObservableValue<bool>(false);
        }

        static ObservableValue<T> Bind<T>(T initial, Action<T> writeBack)
        {
            var ov = new ObservableValue<T>(initial);
            ov.Changed += writeBack;
            return ov;
        }
    }
}

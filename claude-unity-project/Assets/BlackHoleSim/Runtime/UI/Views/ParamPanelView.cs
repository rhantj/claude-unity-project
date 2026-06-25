using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BlackHoleSim.UI
{
    public class ParamPanelView : MonoBehaviour
    {
        [Header("Model refs")]
        [SerializeField] BlackHole blackHole;
        [SerializeField] BlackHoleLensController lens;
        [SerializeField] ParticleField particles;

        [Header("Panel")]
        [SerializeField] GameObject panelContent;
        [SerializeField] Button headerCollapseButton;

        [Header("Disk")]
        [SerializeField] FloatSliderRow diskInnerRadiusRow;
        [SerializeField] FloatSliderRow diskOuterRadiusRow;
        [SerializeField] FloatSliderRow diskThicknessRow;
        [SerializeField] FloatSliderRow diskDensityRow;
        [SerializeField] FloatSliderRow diskTempInnerRow;
        [SerializeField] FloatSliderRow diskTempOuterRow;
        [SerializeField] ColorSwatchRow diskColorTintRow;

        [Header("Relativity")]
        [SerializeField] FloatSliderRow beamingRow;
        [SerializeField] FloatSliderRow redshiftRow;
        [SerializeField] FloatSliderRow photonRingRow;

        [Header("Render Quality")]
        [SerializeField] IntSliderRow stepCountRow;
        [SerializeField] FloatSliderRow stepSizeRow;
        [SerializeField] FloatSliderRow starDensityRow;

        [Header("Particles")]
        [SerializeField] ToggleRow particlesEnabledRow;
        [SerializeField] IntSliderRow particleCountRow;
        [SerializeField] FloatSliderRow particleInnerRadiusRow;
        [SerializeField] FloatSliderRow particleOuterRadiusRow;
        [SerializeField] FloatSliderRow particleDiskThicknessRow;
        [SerializeField] FloatSliderRow particleSpeedJitterRow;
        [SerializeField] FloatSliderRow particleMaxRadiusRow;
        [SerializeField] FloatSliderRow particleInfallSpeedFactorRow;
        [SerializeField] FloatSliderRow particleSizeRow;

        [Header("Danger Zone")]
        [SerializeField] CollapsibleSection dangerZoneSection;
        [SerializeField] FloatSliderRow gravitationalConstantRow;
        [SerializeField] FloatSliderRow massRow;
        [SerializeField] FloatSliderRow softeningRow;
        [SerializeField] FloatSliderRow eventHorizonRadiusRow;

        BlackHoleParamsViewModel vm;

        void Awake()
        {
            vm = new BlackHoleParamsViewModel(blackHole, lens, particles);

            diskInnerRadiusRow.Bind("Disk Inner Radius", vm.DiskInnerRadius, 0f, 30f);
            diskOuterRadiusRow.Bind("Disk Outer Radius", vm.DiskOuterRadius, 0f, 60f);
            diskThicknessRow.Bind("Disk Thickness", vm.DiskThickness, 0f, 5f);
            diskDensityRow.Bind("Disk Density", vm.DiskDensity, 0f, 5f);
            diskTempInnerRow.Bind("Temp Inner (K)", vm.DiskTempInnerKelvin, 1000f, 40000f);
            diskTempOuterRow.Bind("Temp Outer (K)", vm.DiskTempOuterKelvin, 1000f, 40000f);
            diskColorTintRow.Bind("Disk Color Tint", vm.DiskColorTint);

            beamingRow.Bind("Beaming Strength", vm.BeamingStrength, 0f, 1f);
            redshiftRow.Bind("Redshift Strength", vm.RedshiftStrength, 0f, 1f);
            photonRingRow.Bind("Photon Ring", vm.PhotonRing, 0f, 4f);

            stepCountRow.Bind("Step Count", vm.StepCount, 64, 1200);
            stepSizeRow.Bind("Step Size", vm.StepSize, 0.05f, 1f);
            starDensityRow.Bind("Star Density", vm.StarDensity, 0f, 0.02f);

            particlesEnabledRow.Bind("Particles Enabled", vm.ParticlesEnabled);
            particleCountRow.Bind("Particle Count", vm.ParticleCount, 100, 5000);
            particleInnerRadiusRow.Bind("Inner Radius", vm.ParticleInnerRadius, 0f, 30f);
            particleOuterRadiusRow.Bind("Outer Radius", vm.ParticleOuterRadius, 0f, 60f);
            particleDiskThicknessRow.Bind("Disk Thickness", vm.ParticleDiskThickness, 0f, 5f);
            particleSpeedJitterRow.Bind("Speed Jitter", vm.ParticleSpeedJitter, 0f, 0.5f);
            particleMaxRadiusRow.Bind("Max Radius", vm.ParticleMaxRadius, 10f, 100f);
            particleInfallSpeedFactorRow.Bind("Infall Speed Factor", vm.ParticleInfallSpeedFactor, 0.3f, 0.95f);
            particleSizeRow.Bind("Particle Size", vm.ParticleSize, 0.01f, 1f);

            gravitationalConstantRow.Bind("G", vm.GravitationalConstant, 0.1f, 10f);
            massRow.Bind("Mass", vm.Mass, 100f, 20000f);
            softeningRow.Bind("Softening", vm.Softening, 0f, 5f);
            eventHorizonRadiusRow.Bind("Event Horizon Radius (r_s)", vm.EventHorizonRadius, 0.5f, 10f);

            dangerZoneSection.Bind(vm.DangerZoneExpanded);

            panelContent.SetActive(vm.PanelExpanded.Value);
            vm.PanelExpanded.Changed += panelContent.SetActive;
            headerCollapseButton.onClick.AddListener(() => vm.PanelExpanded.Value = !vm.PanelExpanded.Value);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.tabKey.wasPressedThisFrame)
                vm.PanelExpanded.Value = !vm.PanelExpanded.Value;
        }
    }
}

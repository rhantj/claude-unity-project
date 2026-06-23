using UnityEngine;

namespace BlackHoleSim
{
    /// <summary>매 프레임 BlackHole/원반/카메라 파라미터를 전역 셰이더 프로퍼티로 push. BlackHoleLens.shader가 소비.</summary>
    public class BlackHoleLensController : MonoBehaviour
    {
        [SerializeField] BlackHole blackHole;
        [SerializeField] Camera cam;

        [Header("Disk")]
        [SerializeField] float diskInnerRadius = 4f;
        [SerializeField] float diskOuterRadius = 14f;
        [SerializeField] float diskTempInnerKelvin = 18000f;
        [SerializeField] float diskTempOuterKelvin = 3000f;
        [SerializeField] float dopplerStrength = 0.6f;

        [Header("Marching")]
        [SerializeField] float lensStrength = 1f;
        [SerializeField] float softening = 0.5f;
        [SerializeField] float stepSize = 0.25f;
        [SerializeField] int stepCount = 256;

        [Header("Stars")]
        [SerializeField] float starDensity = 0.0025f;

        static readonly int BHWorldPos = Shader.PropertyToID("_BHWorldPos");
        static readonly int BHHorizonRadius = Shader.PropertyToID("_BHHorizonRadius");
        static readonly int BHLensMu = Shader.PropertyToID("_BHLensMu");
        static readonly int BHSoftening = Shader.PropertyToID("_BHSoftening");
        static readonly int BHStepSize = Shader.PropertyToID("_BHStepSize");
        static readonly int BHStepCount = Shader.PropertyToID("_BHStepCount");
        static readonly int BHDiskInner = Shader.PropertyToID("_BHDiskInner");
        static readonly int BHDiskOuter = Shader.PropertyToID("_BHDiskOuter");
        static readonly int BHDiskTempInner = Shader.PropertyToID("_BHDiskTempInner");
        static readonly int BHDiskTempOuter = Shader.PropertyToID("_BHDiskTempOuter");
        static readonly int BHDopplerStrength = Shader.PropertyToID("_BHDopplerStrength");
        static readonly int BHStarDensity = Shader.PropertyToID("_BHStarDensity");
        static readonly int BHCamPos = Shader.PropertyToID("_BHCamPos");
        static readonly int BHCamForward = Shader.PropertyToID("_BHCamForward");
        static readonly int BHCamRight = Shader.PropertyToID("_BHCamRight");
        static readonly int BHCamUp = Shader.PropertyToID("_BHCamUp");
        static readonly int BHTanHalfFovX = Shader.PropertyToID("_BHTanHalfFovX");
        static readonly int BHTanHalfFovY = Shader.PropertyToID("_BHTanHalfFovY");

        void Awake()
        {
            if (cam == null) cam = Camera.main;
        }

        void Update() => PushGlobals();

        public void PushGlobals()
        {
            if (blackHole == null || cam == null) return;

            Shader.SetGlobalVector(BHWorldPos, blackHole.transform.position);
            Shader.SetGlobalFloat(BHHorizonRadius, blackHole.EventHorizonRadius);
            Shader.SetGlobalFloat(BHLensMu, blackHole.Mu * lensStrength);
            Shader.SetGlobalFloat(BHSoftening, softening);
            Shader.SetGlobalFloat(BHStepSize, stepSize);
            Shader.SetGlobalInt(BHStepCount, stepCount);
            Shader.SetGlobalFloat(BHDiskInner, diskInnerRadius);
            Shader.SetGlobalFloat(BHDiskOuter, diskOuterRadius);
            Shader.SetGlobalFloat(BHDiskTempInner, diskTempInnerKelvin);
            Shader.SetGlobalFloat(BHDiskTempOuter, diskTempOuterKelvin);
            Shader.SetGlobalFloat(BHDopplerStrength, dopplerStrength);
            Shader.SetGlobalFloat(BHStarDensity, starDensity);

            Transform t = cam.transform;
            Shader.SetGlobalVector(BHCamPos, t.position);
            Shader.SetGlobalVector(BHCamForward, t.forward);
            Shader.SetGlobalVector(BHCamRight, t.right);
            Shader.SetGlobalVector(BHCamUp, t.up);

            float tanHalfFovY = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            Shader.SetGlobalFloat(BHTanHalfFovY, tanHalfFovY);
            Shader.SetGlobalFloat(BHTanHalfFovX, tanHalfFovY * cam.aspect);
        }

        // Test/editor hook: configure tunables programmatically.
        public void Configure(BlackHole bh, Camera camera)
        {
            blackHole = bh;
            cam = camera;
        }
    }
}

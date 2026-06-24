using UnityEngine;

namespace BlackHoleSim
{
    /// <summary>매 프레임 블랙홀/원반/카메라/적분 파라미터를 전역 셰이더 프로퍼티로 push.
    /// BlackHoleLens.shader(측지선 광학)가 소비. 모든 스케일은 r_s(EventHorizonRadius) 기준.</summary>
    public class BlackHoleLensController : MonoBehaviour
    {
        [SerializeField] BlackHole blackHole;
        [SerializeField] Camera cam;

        [Header("Disk (r_s 배수, ISCO=3·r_s)")]
        [SerializeField] float diskInnerRadius = 6f;   // ISCO
        [SerializeField] float diskOuterRadius = 22f;
        [SerializeField] float diskThickness = 0.5f;
        [SerializeField] float diskDensity = 1.2f;
        [SerializeField] float diskTempInnerKelvin = 18000f;
        [SerializeField] float diskTempOuterKelvin = 3000f;
        [SerializeField] Color diskColorTint = new Color(1f, 0.78f, 0.55f); // 옅은 주황

        [Header("Relativity")]
        [SerializeField, Range(0f, 1f)] float beamingStrength = 1f;
        [SerializeField, Range(0f, 1f)] float redshiftStrength = 1f;
        [SerializeField, Range(0f, 4f)] float photonRing = 1.5f;

        [Header("Quality")]
        [SerializeField, Range(64, 1200)] int stepCount = 400; // 인스펙터 품질 슬라이더
        [SerializeField] float stepSize = 0.25f;

        [Header("Stars")]
        [SerializeField] float starDensity = 0.0025f;

        static readonly int BHWorldPos = Shader.PropertyToID("_BHWorldPos");
        static readonly int BHRs = Shader.PropertyToID("_BHRs");
        static readonly int BHDiskInner = Shader.PropertyToID("_BHDiskInner");
        static readonly int BHDiskOuter = Shader.PropertyToID("_BHDiskOuter");
        static readonly int BHDiskThickness = Shader.PropertyToID("_BHDiskThickness");
        static readonly int BHDiskDensity = Shader.PropertyToID("_BHDiskDensity");
        static readonly int BHDiskTempInner = Shader.PropertyToID("_BHDiskTempInner");
        static readonly int BHDiskTempOuter = Shader.PropertyToID("_BHDiskTempOuter");
        static readonly int BHDiskColorTint = Shader.PropertyToID("_BHDiskColorTint");
        static readonly int BHBeaming = Shader.PropertyToID("_BHBeaming");
        static readonly int BHRedshift = Shader.PropertyToID("_BHRedshift");
        static readonly int BHPhotonRing = Shader.PropertyToID("_BHPhotonRing");
        static readonly int BHStepCount = Shader.PropertyToID("_BHStepCount");
        static readonly int BHStepSize = Shader.PropertyToID("_BHStepSize");
        static readonly int BHStarDensity = Shader.PropertyToID("_BHStarDensity");
        static readonly int BHCamPos = Shader.PropertyToID("_BHCamPos");
        static readonly int BHCamForward = Shader.PropertyToID("_BHCamForward");
        static readonly int BHCamRight = Shader.PropertyToID("_BHCamRight");
        static readonly int BHCamUp = Shader.PropertyToID("_BHCamUp");
        static readonly int BHTanHalfFovX = Shader.PropertyToID("_BHTanHalfFovX");
        static readonly int BHTanHalfFovY = Shader.PropertyToID("_BHTanHalfFovY");

        void Awake() { if (cam == null) cam = Camera.main; }

        void Update() => PushGlobals();

        public void PushGlobals()
        {
            if (blackHole == null || cam == null) return;

            Shader.SetGlobalVector(BHWorldPos, blackHole.transform.position);
            Shader.SetGlobalFloat(BHRs, blackHole.EventHorizonRadius);
            Shader.SetGlobalFloat(BHDiskInner, diskInnerRadius);
            Shader.SetGlobalFloat(BHDiskOuter, diskOuterRadius);
            Shader.SetGlobalFloat(BHDiskThickness, diskThickness);
            Shader.SetGlobalFloat(BHDiskDensity, diskDensity);
            Shader.SetGlobalFloat(BHDiskTempInner, diskTempInnerKelvin);
            Shader.SetGlobalFloat(BHDiskTempOuter, diskTempOuterKelvin);
            Shader.SetGlobalColor(BHDiskColorTint, diskColorTint);
            Shader.SetGlobalFloat(BHBeaming, beamingStrength);
            Shader.SetGlobalFloat(BHRedshift, redshiftStrength);
            Shader.SetGlobalFloat(BHPhotonRing, photonRing);
            Shader.SetGlobalInt(BHStepCount, stepCount);
            Shader.SetGlobalFloat(BHStepSize, stepSize);
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

        public void Configure(BlackHole bh, Camera camera)
        {
            blackHole = bh;
            cam = camera;
        }
    }
}

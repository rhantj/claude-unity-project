# 블랙홀 측지선 광학 + 볼륨메트릭 강착원반(3단계) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 2단계의 약장 뉴턴 렌즈를 슈바르츠실트 null 측지선 적분으로 교체해, 포톤 링·디스크 이중상·볼륨메트릭 강착원반·상대론적 도플러 빔ing/중력 적색편이를 가진 가르강튀아급 블랙홀을 구현한다.

**Architecture:** URP `ScriptableRendererFeature` 풀스크린 패스(2단계 골격 유지)가 프래그먼트마다 광자를 측지선으로 적분한다. 스텝마다 호라이즌(그림자)·포톤 스피어(링)·디스크 슬랩(볼륨 누적)을 검사하고, 탈출 시 렌즈된 절차적 별 배경을 샘플한다. 동역학(PW 입자·던지기)은 별개 경로로 유지하며 토글(기본 꺼짐).

**Tech Stack:** Unity 6 (6000.5.0f1), URP 17.5.0, Render Graph API, HLSL, Unity Test Framework(NUnit, EditMode).

## Global Constraints

- 엔진/렌더 파이프라인: Unity 6 (6000.5.0f1), URP 17.5.0 — 신규 패키지 추가하지 않음.
- 동역학 코어(`GravityField.cs`, `GravityIntegrator.cs`, `ParticleField.cs`, `ThrowableBody.cs`, `SimController.cs`)와 `BlackHole.cs`는 수정하지 않는다. 광학 경로만 바꾼다.
- 셰이더는 2단계 검증 패턴 사용: `Blit.hlsl`/`Blitter` 금지. 자체 풀스크린 Vert(`Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl`의 `GetFullScreenTriangleVertexPosition/TexCoord`) + `ctx.cmd.DrawProcedural(...,3,1)`. (참조: 커밋 8da4787, e506bbf)
- 카메라 clear는 SolidColor 유지(기존 `ConfigureCameraForLens`). 절차적 별 배경이 스카이박스 대체.
- 측지선 가속도: `a = -1.5 · r_s · h² · pos / r⁵` (BH 중심 좌표, h=cross(pos,vel) 보존). 이 상수 1.5는 약장 편향각 `2·r_s/b`를 정확히 재현한다(Task 1에서 검증).
- 씬 스케일(모두 r_s 기준): r_s = EventHorizonRadius = 2, 포톤 스피어 1.5·r_s=3, ISCO=디스크 내경=3·r_s=6, 디스크 외경≈22, 디스크 반두께≈0.5.
- 모든 코드 변경 후 `mcp__UnityMCP__editor_refresh_assets` → `editor_recompile` → `editor_wait_ready` → `editor_read_log`(`{"types":["Error"]}`)로 컴파일 에러 0건 확인.
- 셰이더 검증은 `sim_play` → `editor_wait_ready` → `screenshot_game` → `sim_stop`. 단위 테스트는 `editor_invoke_method`(`BlackHoleSim.Tests.EditModeTestRunner.RunAll`) 후 `editor_read_log`의 `[TESTRESULT] failed=0` 확인.

## 파일 구조

| 파일 | 역할 |
|---|---|
| `Assets/BlackHoleSim/Runtime/PhotonGeodesic.cs` | 슈바르츠실트 null 측지선 가속도 + Verlet 스텝(순수함수). 셰이더 미러의 C# 기준점 |
| `Assets/BlackHoleSim/Tests/Editor/PhotonGeodesicTests.cs` | 편향각·포톤 스피어·호라이즌 포획 EditMode 테스트 |
| `Assets/BlackHoleSim/Runtime/Relativity.cs` | 궤도속도·도플러·중력 적색편이·빔ing(순수함수) |
| `Assets/BlackHoleSim/Tests/Editor/RelativityTests.cs` | `Relativity` EditMode 테스트 |
| `Assets/BlackHoleSim/Runtime/BlackHoleLensController.cs` | (재설계) r_s 기반 전역 push. `_BHLensMu/lensStrength/_BHSoftening` 폐기 |
| `Assets/BlackHoleSim/Tests/Editor/BlackHoleLensControllerTests.cs` | (갱신) 신규 전역 push 테스트 |
| `Assets/BlackHoleSim/Shaders/BlackHoleLens.shader` | (전면 재작성) 측지선 March + 포톤 링 + 볼륨 디스크 + 상대론 셰이딩 |
| `Assets/BlackHoleSim/Editor/BlackHoleSimEditorTools.cs` | (메서드 추가) ACES 톤매핑, 입자 토글 |
| `Assets/Scenes/SampleScene.unity` | (MCP 수정) 디스크 파라미터, 입자 토글 |

---

### Task 1: PhotonGeodesic (측지선 가속도 + 스텝)

**Files:**
- Create: `Assets/BlackHoleSim/Runtime/PhotonGeodesic.cs`
- Test: `Assets/BlackHoleSim/Tests/Editor/PhotonGeodesicTests.cs`

**Interfaces:**
- Produces: `public static class PhotonGeodesic { public static float AngularMomentumSq(Vector3 pos, Vector3 vel); public static Vector3 Acceleration(Vector3 pos, float h2, float rs); public static void Step(ref Vector3 pos, ref Vector3 vel, float h2, float rs, float dλ); }` — Task 4가 동일 식을 HLSL로 미러.

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
using NUnit.Framework;
using UnityEngine;
using BlackHoleSim;

namespace BlackHoleSim.Tests
{
    public class PhotonGeodesicTests
    {
        // 큰 충돌계수에서 약장 편향각 ≈ 2·r_s/b (GR 결과). 상수 1.5 검증.
        [Test]
        public void Deflection_WeakField_MatchesTwoRsOverB()
        {
            float rs = 2f, b = 120f;
            Vector3 pos = new Vector3(-2000f, b, 0f);
            Vector3 vel = new Vector3(1f, 0f, 0f);
            float h2 = PhotonGeodesic.AngularMomentumSq(pos, vel);

            for (int i = 0; i < 200000 && pos.x < 2000f; i++)
                PhotonGeodesic.Step(ref pos, ref vel, h2, rs, 0.05f);

            float deflection = Mathf.Atan2(-vel.y, vel.x); // 중심 쪽(-y)으로 휜 각
            // 상수 1.5는 해석적으로 2·r_s/b를 재현. 허용오차 5%는 Verlet 이산화 오차 여유.
            Assert.That(deflection, Is.EqualTo(2f * rs / b).Within(0.05f * (2f * rs / b)));
        }

        [Test]
        public void PhotonSphere_TangentialRay_KeepsRadiusBriefly()
        {
            float rs = 2f, photonR = 1.5f * rs; // = 3
            Vector3 pos = new Vector3(photonR, 0f, 0f);
            Vector3 vel = new Vector3(0f, 0f, 1f); // tangential
            float h2 = PhotonGeodesic.AngularMomentumSq(pos, vel);

            float maxDev = 0f;
            for (int i = 0; i < 300; i++)
            {
                PhotonGeodesic.Step(ref pos, ref vel, h2, rs, 0.01f);
                maxDev = Mathf.Max(maxDev, Mathf.Abs(pos.magnitude - photonR));
            }
            Assert.That(maxDev, Is.LessThan(0.2f), "포톤 스피어 근처에서 잠시 반경 유지(불안정)");
        }

        [Test]
        public void LowImpactParameter_RayFallsInsideHorizon()
        {
            float rs = 2f, b = 1.5f; // 임계(≈2.6·r_s) 이하 → 포획
            Vector3 pos = new Vector3(-200f, b, 0f);
            Vector3 vel = new Vector3(1f, 0f, 0f);
            float h2 = PhotonGeodesic.AngularMomentumSq(pos, vel);

            float minR = pos.magnitude;
            for (int i = 0; i < 100000; i++)
            {
                PhotonGeodesic.Step(ref pos, ref vel, h2, rs, 0.02f);
                minR = Mathf.Min(minR, pos.magnitude);
                if (minR <= rs) break;
            }
            Assert.That(minR, Is.LessThanOrEqualTo(rs), "임계 이하 광선은 호라이즌에 포획");
        }
    }
}
```

이 파일을 `Assets/BlackHoleSim/Tests/Editor/PhotonGeodesicTests.cs`에 Write 툴로 작성한다.

- [ ] **Step 2: 컴파일 에러로 실패 확인**

Tool: `editor_refresh_assets` → `editor_recompile` → `editor_wait_ready` → `editor_read_log`(`{"types":["Error"],"count":50}`)
Expected: `BlackHoleSim.PhotonGeodesic`를 찾을 수 없다는 `CS0103`/`CS0117`.

- [ ] **Step 3: 최소 구현 작성**

```csharp
using UnityEngine;

namespace BlackHoleSim
{
    /// <summary>슈바르츠실트 null 측지선의 카르테시안 적분(순수함수). BH 중심 좌표 기준.
    /// a = -1.5·r_s·h²·pos/r⁵ (h=cross(pos,vel) 보존). 상수 1.5는 약장 편향 2·r_s/b를 재현.</summary>
    public static class PhotonGeodesic
    {
        public static float AngularMomentumSq(Vector3 pos, Vector3 vel)
            => Vector3.Cross(pos, vel).sqrMagnitude;

        public static Vector3 Acceleration(Vector3 pos, float h2, float rs)
        {
            float r2 = Vector3.Dot(pos, pos);
            if (r2 <= Mathf.Epsilon) return Vector3.zero;
            float invR5 = 1f / (r2 * r2 * Mathf.Sqrt(r2));
            return -1.5f * rs * h2 * invR5 * pos;
        }

        // velocity Verlet. h2는 중심력이라 보존되므로 한 번 계산해 넘긴다(가속도는 pos에만 의존).
        public static void Step(ref Vector3 pos, ref Vector3 vel, float h2, float rs, float dλ)
        {
            Vector3 a0 = Acceleration(pos, h2, rs);
            pos += vel * dλ + 0.5f * a0 * dλ * dλ;
            Vector3 a1 = Acceleration(pos, h2, rs);
            vel += 0.5f * (a0 + a1) * dλ;
        }
    }
}
```

이 파일을 `Assets/BlackHoleSim/Runtime/PhotonGeodesic.cs`에 Write 툴로 작성한다.

- [ ] **Step 4: 테스트 실행, 통과 확인**

Tool: `editor_refresh_assets` → `editor_recompile` → `editor_wait_ready` → `editor_read_log`(Error 0건) → `editor_invoke_method`(`{"className":"BlackHoleSim.Tests.EditModeTestRunner","methodName":"RunAll"}`) → `editor_wait_ready` → `editor_read_log`(`{"types":["Log"],"count":20}`, `[TESTRESULT] failed=0`, passed가 이전보다 3 증가).

- [ ] **Step 5: 커밋**

```bash
git add Assets/BlackHoleSim/Runtime/PhotonGeodesic.cs Assets/BlackHoleSim/Tests/Editor/PhotonGeodesicTests.cs
git commit -m "feat: 슈바르츠실트 측지선 광자 적분 + 테스트"
```

---

### Task 2: Relativity (도플러·적색편이·궤도속도)

**Files:**
- Create: `Assets/BlackHoleSim/Runtime/Relativity.cs`
- Test: `Assets/BlackHoleSim/Tests/Editor/RelativityTests.cs`

**Interfaces:**
- Produces: `public static class Relativity { public static float OrbitalSpeed(float r, float rs); public static float DopplerFactor(float beta, float cosAngle); public static float GravitationalRedshift(float r, float rs); public static float BeamingIntensity(float dopplerFactor); }` — Task 6이 HLSL로 미러.

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
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
```

이 파일을 `Assets/BlackHoleSim/Tests/Editor/RelativityTests.cs`에 Write 툴로 작성한다.

- [ ] **Step 2: 컴파일 에러로 실패 확인**

Tool: `editor_refresh_assets` → `editor_recompile` → `editor_wait_ready` → `editor_read_log`(`{"types":["Error"],"count":50}`)
Expected: `BlackHoleSim.Relativity`를 찾을 수 없다는 에러.

- [ ] **Step 3: 최소 구현 작성**

```csharp
using UnityEngine;

namespace BlackHoleSim
{
    /// <summary>강착원반 상대론적 셰이딩 인자(순수함수). c=1 단위. 셰이더 미러의 기준점.</summary>
    public static class Relativity
    {
        // 슈바르츠실트 원궤도 국소 속도 v=√((r_s/2)/(r−r_s)). r≤r_s이면 0, <1로 클램프.
        public static float OrbitalSpeed(float r, float rs)
        {
            float denom = r - rs;
            if (denom <= 0f) return 0f;
            return Mathf.Min(Mathf.Sqrt((rs * 0.5f) / denom), 0.999f);
        }

        // δ = 1/(γ(1−β·cosθ)). cosθ>0이면 접근(δ>1).
        public static float DopplerFactor(float beta, float cosAngle)
        {
            float gamma = 1f / Mathf.Sqrt(Mathf.Max(1f - beta * beta, 1e-6f));
            return 1f / (gamma * (1f - beta * cosAngle));
        }

        public static float GravitationalRedshift(float r, float rs)
            => Mathf.Sqrt(Mathf.Max(1f - rs / r, 0f));

        public static float BeamingIntensity(float dopplerFactor)
            => dopplerFactor * dopplerFactor * dopplerFactor * dopplerFactor;
    }
}
```

이 파일을 `Assets/BlackHoleSim/Runtime/Relativity.cs`에 Write 툴로 작성한다.

- [ ] **Step 4: 테스트 실행, 통과 확인**

Tool: `editor_refresh_assets` → `editor_recompile` → `editor_wait_ready` → `editor_read_log`(Error 0건) → `editor_invoke_method`(`RunAll`) → `editor_wait_ready` → `editor_read_log`(`[TESTRESULT] failed=0`, passed가 5 증가).

- [ ] **Step 5: 커밋**

```bash
git add Assets/BlackHoleSim/Runtime/Relativity.cs Assets/BlackHoleSim/Tests/Editor/RelativityTests.cs
git commit -m "feat: 상대론적 도플러/적색편이/빔ing 순수함수 + 테스트"
```

---

### Task 3: 컨트롤러 r_s 기반 재설계

**Files:**
- Modify: `Assets/BlackHoleSim/Runtime/BlackHoleLensController.cs`
- Modify: `Assets/BlackHoleSim/Tests/Editor/BlackHoleLensControllerTests.cs`

**Interfaces:**
- Consumes: `BlackHole.transform.position`, `BlackHole.EventHorizonRadius`.
- Produces: 전역 셰이더 프로퍼티 — `_BHWorldPos`, `_BHRs`, `_BHDiskInner`, `_BHDiskOuter`, `_BHDiskThickness`, `_BHDiskDensity`, `_BHDiskTempInner`, `_BHDiskTempOuter`, `_BHBeaming`, `_BHRedshift`, `_BHPhotonRing`, `_BHStepCount`, `_BHStepSize`, `_BHStarDensity`, `_BHCamPos`, `_BHCamForward`, `_BHCamRight`, `_BHCamUp`, `_BHTanHalfFovX`, `_BHTanHalfFovY`. Task 4~6 셰이더가 이 이름으로 읽는다.

- [ ] **Step 1: 테스트를 신규 프로퍼티로 갱신(실패 상태)**

`Assets/BlackHoleSim/Tests/Editor/BlackHoleLensControllerTests.cs` 전체를 Write 툴로 교체:

```csharp
using NUnit.Framework;
using UnityEngine;
using BlackHoleSim;

public class BlackHoleLensControllerTests
{
    [Test]
    public void PushGlobals_WritesBlackHoleWorldPositionAndSchwarzschildRadius()
    {
        var bhGo = new GameObject("TestBH");
        bhGo.transform.position = new Vector3(3f, 0f, 5f);
        var bh = bhGo.AddComponent<BlackHole>();
        bh.Configure(1f, 4000f, 0.5f, 2.5f); // eventHorizonRadius = 2.5

        var camGo = new GameObject("TestCam");
        var cam = camGo.AddComponent<Camera>();

        var ctrlGo = new GameObject("TestController");
        var ctrl = ctrlGo.AddComponent<BlackHoleLensController>();
        ctrl.Configure(bh, cam);

        ctrl.PushGlobals();

        Vector4 worldPos = Shader.GetGlobalVector("_BHWorldPos");
        Assert.AreEqual(3f, worldPos.x, 0.001f);
        Assert.AreEqual(5f, worldPos.z, 0.001f);
        Assert.AreEqual(2.5f, Shader.GetGlobalFloat("_BHRs"), 0.001f);

        Object.DestroyImmediate(ctrlGo);
        Object.DestroyImmediate(camGo);
        Object.DestroyImmediate(bhGo);
    }

    [Test]
    public void PushGlobals_WritesDiskInnerOuterAndStepCount()
    {
        var bhGo = new GameObject("TestBH2");
        var bh = bhGo.AddComponent<BlackHole>();

        var camGo = new GameObject("TestCam2");
        var cam = camGo.AddComponent<Camera>();

        var ctrlGo = new GameObject("TestController2");
        var ctrl = ctrlGo.AddComponent<BlackHoleLensController>();
        ctrl.Configure(bh, cam);

        ctrl.PushGlobals();

        Assert.That(Shader.GetGlobalFloat("_BHDiskOuter"),
            Is.GreaterThan(Shader.GetGlobalFloat("_BHDiskInner")));
        Assert.That(Shader.GetGlobalInt("_BHStepCount"), Is.GreaterThan(0));

        Object.DestroyImmediate(ctrlGo);
        Object.DestroyImmediate(camGo);
        Object.DestroyImmediate(bhGo);
    }
}
```

- [ ] **Step 2: 컴파일 에러로 실패 확인**

Tool: `editor_refresh_assets` → `editor_recompile` → `editor_wait_ready` → `editor_read_log`(`{"types":["Error"]}`)
Expected: `_BHRs` 등이 push되지 않으므로 두 번째 테스트는 통과할 수 있으나 첫 테스트의 `_BHRs`가 0이라 실패(또는 컨트롤러가 아직 신규 프로퍼티 미push). 컴파일은 통과.

- [ ] **Step 3: 컨트롤러 재작성**

`Assets/BlackHoleSim/Runtime/BlackHoleLensController.cs` 전체를 Write 툴로 교체:

```csharp
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
```

이 파일을 Write 툴로 작성한다.

- [ ] **Step 4: 테스트 실행, 통과 확인**

Tool: `editor_refresh_assets` → `editor_recompile` → `editor_wait_ready` → `editor_read_log`(Error 0건) → `editor_invoke_method`(`RunAll`) → `editor_wait_ready` → `editor_read_log`(`[TESTRESULT] failed=0`).

- [ ] **Step 5: 커밋**

```bash
git add Assets/BlackHoleSim/Runtime/BlackHoleLensController.cs Assets/BlackHoleSim/Tests/Editor/BlackHoleLensControllerTests.cs
git commit -m "feat: 렌즈 컨트롤러 r_s 기반 재설계(측지선/디스크/상대론 전역)"
```

---

### Task 4: 셰이더 — 측지선 별 배경 + 그림자 + 포톤 링

**Files:**
- Modify: `Assets/BlackHoleSim/Shaders/BlackHoleLens.shader`

이 단계는 단위 테스트 불가 영역이라 스크린샷으로 검증한다.

- [ ] **Step 1: 셰이더를 측지선 버전으로 전면 교체**

`Assets/BlackHoleSim/Shaders/BlackHoleLens.shader` 전체를 Write 툴로 교체:

```hlsl
Shader "Hidden/BlackHoleSim/BlackHoleLens"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "BlackHoleLens"
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float3 _BHCamPos, _BHCamForward, _BHCamRight, _BHCamUp;
            float _BHTanHalfFovX, _BHTanHalfFovY, _BHStarDensity;
            float3 _BHWorldPos;
            float _BHRs;
            float _BHDiskInner, _BHDiskOuter, _BHDiskThickness, _BHDiskDensity;
            float _BHDiskTempInner, _BHDiskTempOuter;
            float _BHBeaming, _BHRedshift, _BHPhotonRing;
            float _BHStepSize;
            int _BHStepCount;

            struct Attributes { uint vertexID : SV_VertexID; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 texcoord : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                o.texcoord = GetFullScreenTriangleTexCoord(input.vertexID);
                return o;
            }

            float Hash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float3 CameraRayDir(float2 uv)
            {
                float2 ndc = uv * 2.0 - 1.0;
                float3 dir = _BHCamForward
                    + ndc.x * _BHTanHalfFovX * _BHCamRight
                    + ndc.y * _BHTanHalfFovY * _BHCamUp;
                return normalize(dir);
            }

            half3 StarField(float3 dir)
            {
                float3 cell = floor(dir * 400.0);
                float n = Hash13(cell);
                float star = step(1.0 - _BHStarDensity, n) * n;
                return half3(star, star, star);
            }

            // PhotonGeodesic.Acceleration(BlackHoleSim/Runtime/PhotonGeodesic.cs)의 HLSL 미러.
            float3 GeodesicAccel(float3 pos, float h2, float rs)
            {
                float r2 = dot(pos, pos);
                float invR5 = 1.0 / (r2 * r2 * sqrt(r2));
                return -1.5 * rs * h2 * invR5 * pos;
            }

            // 측지선 적분. BH 중심 좌표(pos = world - _BHWorldPos). 반환: HDR 색.
            half3 March(float3 worldOrigin, float3 dir)
            {
                float rs = _BHRs;
                float photonR = 1.5 * rs;
                float3 pos = worldOrigin - _BHWorldPos;
                float3 vel = dir;
                float h2 = dot(cross(pos, vel), cross(pos, vel));

                half3 accum = 0;
                float minR = 1e9;

                for (int i = 0; i < _BHStepCount; i++)
                {
                    float r = length(pos);
                    minR = min(minR, r);
                    if (r <= rs) return accum; // 호라이즌(앞쪽 누적색 유지)

                    float3 a0 = GeodesicAccel(pos, h2, rs);
                    float3 nextPos = pos + vel * _BHStepSize + 0.5 * a0 * _BHStepSize * _BHStepSize;
                    float3 a1 = GeodesicAccel(nextPos, h2, rs);
                    vel += 0.5 * (a0 + a1) * _BHStepSize;
                    pos = nextPos;
                }

                accum += StarField(normalize(vel));

                // 포톤 링: 광선의 최근접 반경이 포톤 스피어에 가까울수록 밝은 고리.
                float ring = _BHPhotonRing * exp(-pow((minR - photonR) / (0.18 * rs), 2.0));
                accum += ring * half3(1.0, 0.95, 0.85);

                return accum;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 dir = CameraRayDir(input.texcoord);
                half3 color = March(_BHCamPos, dir);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
```

- [ ] **Step 2: 컴파일 에러 0건 확인**

Tool: `editor_refresh_assets` → `editor_recompile` → `editor_wait_ready` → `editor_read_log`(`{"types":["Error"]}`)
Expected: 0건.

- [ ] **Step 3: 스크린샷 검증**

Tool: `sim_play` → `editor_wait_ready` → `screenshot_game`(`{"description":"geodesic star lensing + photon ring"}`) → `sim_stop`
Expected: 검은 그림자 둘레에 **얇고 밝은 포톤 링**, 그 바깥으로 별빛이 강하게 휘어 감기는 모습(2단계보다 굴절이 강하고 고리가 선명). 약하면 컨트롤러 `photonRing` 값을 키워 재확인.

- [ ] **Step 4: 커밋**

```bash
git add Assets/BlackHoleSim/Shaders/BlackHoleLens.shader
git commit -m "feat: 측지선 별 배경 렌즈 + 포톤 링"
```

---

### Task 5: 셰이더 — 볼륨메트릭 강착원반(온도·밀도)

**Files:**
- Modify: `Assets/BlackHoleSim/Shaders/BlackHoleLens.shader`

**Interfaces:**
- Produces: `BlackBodyColorApprox(tempKelvin)`(BlackBodyColor 미러), `DiskDensity(rCyl, absY)`. Task 6이 디스크 누적에 상대론 보정을 추가할 지점.

- [ ] **Step 1: BlackBody 미러 + 디스크 누적 추가**

`GeodesicAccel` 함수 다음에 아래 두 함수를 추가(Edit 툴, `// 측지선 적분.` 주석 줄 앞에 삽입):

```hlsl
            // BlackBodyColor.Evaluate(BlackHoleSim/Runtime/BlackBodyColor.cs)의 HLSL 미러.
            half3 BlackBodyColorApprox(float tempKelvin)
            {
                float t = clamp(tempKelvin, 1000.0, 40000.0) / 100.0;
                float r = t <= 66.0 ? 255.0 : clamp(329.698727446 * pow(t - 60.0, -0.1332047592), 0.0, 255.0);
                float g = t <= 66.0
                    ? clamp(99.4708025861 * log(t) - 161.1195681661, 0.0, 255.0)
                    : clamp(288.1221695283 * pow(t - 60.0, -0.0755148492), 0.0, 255.0);
                float b = t >= 66.0 ? 255.0 : (t <= 19.0 ? 0.0 : clamp(138.5177312231 * log(t - 10.0) - 305.0447927307, 0.0, 255.0));
                return half3(r, g, b) / 255.0;
            }

            // 디스크 슬랩 밀도: 반경 falloff(안쪽 진함) × 수직 가우시안.
            float DiskDensity(float rCyl, float absY)
            {
                if (rCyl < _BHDiskInner || rCyl > _BHDiskOuter || absY > _BHDiskThickness) return 0.0;
                float u = saturate((rCyl - _BHDiskInner) / max(_BHDiskOuter - _BHDiskInner, 1e-4));
                float vGauss = exp(-pow(absY / (0.5 * _BHDiskThickness), 2.0));
                return _BHDiskDensity * (1.0 - u) * vGauss;
            }
```

- [ ] **Step 2: March 루프에 볼륨 누적 삽입**

`March`의 for 루프에서 호라이즌 검사 직후, 측지선 스텝 직전에 아래 블록을 추가(Edit 툴). 기존:

```hlsl
                    if (r <= rs) return accum; // 호라이즌(앞쪽 누적색 유지)

                    float3 a0 = GeodesicAccel(pos, h2, rs);
```

교체:

```hlsl
                    if (r <= rs) return accum; // 호라이즌(앞쪽 누적색 유지)

                    float rCyl = length(pos.xz);
                    float dens = DiskDensity(rCyl, abs(pos.y));
                    if (dens > 0.0)
                    {
                        float u = saturate((rCyl - _BHDiskInner) / max(_BHDiskOuter - _BHDiskInner, 1e-4));
                        float temp = lerp(_BHDiskTempInner, _BHDiskTempOuter, u);
                        half3 emit = BlackBodyColorApprox(temp) * dens;
                        accum += trans * emit * _BHStepSize;
                        trans *= exp(-dens * _BHStepSize);
                        if (trans < 0.01) return accum;
                    }

                    float3 a0 = GeodesicAccel(pos, h2, rs);
```

또한 `half3 accum = 0;` 다음 줄에 투과도 변수를 추가:

```hlsl
                half3 accum = 0;
                float trans = 1.0;
```

그리고 별 배경 합성을 투과도로 가중하도록 교체. 기존:

```hlsl
                accum += StarField(normalize(vel));
```

교체:

```hlsl
                accum += trans * StarField(normalize(vel));
```

- [ ] **Step 3: 컴파일 에러 0건 확인**

Tool: `editor_refresh_assets` → `editor_recompile` → `editor_wait_ready` → `editor_read_log`(`{"types":["Error"]}`)
Expected: 0건.

- [ ] **Step 4: 스크린샷 검증**

Tool: `sim_play` → `editor_wait_ready` → `screenshot_game`(`{"description":"volumetric accretion disk + lensed double image"}`) → `sim_stop`
Expected: 그림자 둘레에 발광 원반이 보이고, **렌즈로 디스크의 뒤쪽이 그림자 위/아래로 감겨 올라간 이중상**(가르강튀아 띠). 안쪽 청백색→바깥 적색 온도 그라데이션. 밀도가 과하거나 약하면 `diskDensity` 조정.

- [ ] **Step 5: 커밋**

```bash
git add Assets/BlackHoleSim/Shaders/BlackHoleLens.shader
git commit -m "feat: 볼륨메트릭 강착원반 + 측지선 이중상"
```

---

### Task 6: 셰이더 — 상대론적 도플러 빔ing + 중력 적색편이

**Files:**
- Modify: `Assets/BlackHoleSim/Shaders/BlackHoleLens.shader`

**Interfaces:**
- Consumes: `_BHBeaming`, `_BHRedshift`(Task 3), `DiskDensity`(Task 5).
- Produces: `Relativity` 함수들의 HLSL 미러 + 디스크 샘플의 도플러·적색편이 보정.

- [ ] **Step 1: Relativity 미러 추가**

`DiskDensity` 함수 다음에 추가(Edit 툴):

```hlsl
            // Relativity(BlackHoleSim/Runtime/Relativity.cs)의 HLSL 미러.
            float OrbitalSpeedRel(float r, float rs)
            {
                float denom = r - rs;
                if (denom <= 0.0) return 0.0;
                return min(sqrt((rs * 0.5) / denom), 0.999);
            }
            float DopplerFactor(float beta, float cosAngle)
            {
                float gamma = 1.0 / sqrt(max(1.0 - beta * beta, 1e-6));
                return 1.0 / (gamma * (1.0 - beta * cosAngle));
            }
            float GravRedshift(float r, float rs)
            {
                return sqrt(max(1.0 - rs / r, 0.0));
            }
```

- [ ] **Step 2: 디스크 누적에 상대론 보정 적용**

Task 5에서 넣은 디스크 블록을 아래로 교체(Edit 툴). 기존:

```hlsl
                    if (dens > 0.0)
                    {
                        float u = saturate((rCyl - _BHDiskInner) / max(_BHDiskOuter - _BHDiskInner, 1e-4));
                        float temp = lerp(_BHDiskTempInner, _BHDiskTempOuter, u);
                        half3 emit = BlackBodyColorApprox(temp) * dens;
                        accum += trans * emit * _BHStepSize;
                        trans *= exp(-dens * _BHStepSize);
                        if (trans < 0.01) return accum;
                    }
```

교체:

```hlsl
                    if (dens > 0.0)
                    {
                        float u = saturate((rCyl - _BHDiskInner) / max(_BHDiskOuter - _BHDiskInner, 1e-4));
                        float temp = lerp(_BHDiskTempInner, _BHDiskTempOuter, u);

                        // 궤도 운동: xz평면 접선 방향. 광선 진행 방향(vel) 대비 접근/후퇴.
                        float beta = OrbitalSpeedRel(length(pos), rs);
                        float3 tangent = normalize(cross(float3(0, 1, 0), pos));
                        float cosAng = dot(tangent, -normalize(vel));
                        float shift = DopplerFactor(beta, cosAng) * GravRedshift(length(pos), rs);

                        // 주파수 이동: 색온도(파랑/빨강)와 세기(빔ing ∝ shift⁴)에 반영.
                        float tShift = lerp(1.0, shift, _BHRedshift);
                        half3 emit = BlackBodyColorApprox(temp * tShift) * dens;
                        emit *= lerp(1.0, pow(max(shift, 0.0), 4.0), _BHBeaming);

                        accum += trans * emit * _BHStepSize;
                        trans *= exp(-dens * _BHStepSize);
                        if (trans < 0.01) return accum;
                    }
```

- [ ] **Step 3: 컴파일 에러 0건 확인**

Tool: `editor_refresh_assets` → `editor_recompile` → `editor_wait_ready` → `editor_read_log`(`{"types":["Error"]}`)
Expected: 0건.

- [ ] **Step 4: 스크린샷 검증**

Tool: `sim_play` → `editor_wait_ready` → `screenshot_game`(`{"description":"doppler beaming + gravitational redshift"}`) → `sim_stop`
Expected: 디스크 **한쪽(접근)은 밝고 푸르게, 반대쪽(후퇴)은 어둡고 붉게** — 가르강튀아 특유의 좌우 비대칭. 안쪽일수록 중력 적색편이로 약간 어둡고 붉어짐. 효과 과/소시 `beamingStrength`/`redshiftStrength`(0~1) 조정.

- [ ] **Step 5: 커밋**

```bash
git add Assets/BlackHoleSim/Shaders/BlackHoleLens.shader
git commit -m "feat: 상대론적 도플러 빔ing + 중력 적색편이"
```

---

### Task 7: 통합 — ACES 톤매핑 + Bloom + 입자 토글 + 회귀

**Files:**
- Modify: `Assets/BlackHoleSim/Editor/BlackHoleSimEditorTools.cs`
- Modify: `Assets/Scenes/SampleScene.unity` (MCP)

**Interfaces:**
- Consumes: 기존 `SetBloomIntensity`(2단계). `ParticleField` GameObject.

- [ ] **Step 1: Editor 헬퍼에 톤매핑/입자 토글 추가**

`Assets/BlackHoleSim/Editor/BlackHoleSimEditorTools.cs`의 `SetBloomIntensity` 메서드 정의 바로 앞에 아래를 추가(Edit 툴):

```csharp
        public static void SetTonemappingACES()
        {
            const string profilePath = "Assets/Settings/SampleSceneProfile.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null) { Debug.LogWarning("[Editor] Volume profile not found: " + profilePath); return; }

            if (!profile.TryGet(out Tonemapping tm))
                tm = profile.Add<Tonemapping>(true);
            tm.mode.overrideState = true;
            tm.mode.value = TonemappingMode.ACES;
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            Debug.Log("[Editor] Tonemapping set to ACES");
        }

        public static void SetParticleFieldActive(bool active)
        {
            var pf = Object.FindAnyObjectByType<BlackHoleSim.ParticleField>(FindObjectsInactive.Include);
            if (pf == null) { Debug.LogWarning("[Editor] ParticleField not found"); return; }
            pf.gameObject.SetActive(active);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(pf.gameObject.scene);
            Debug.Log("[Editor] ParticleField active=" + active);
        }
```

(파일 상단 `using`에 `using UnityEngine.Rendering;`이 이미 있는지 확인 — 2단계에서 추가됨. `Tonemapping`/`TonemappingMode`는 `UnityEngine.Rendering.Universal` 네임스페이스로 기존 `using`에 포함.)

- [ ] **Step 2: 컴파일 에러 0건 확인**

Tool: `editor_refresh_assets` → `editor_recompile` → `editor_wait_ready` → `editor_read_log`(`{"types":["Error"]}`)
Expected: 0건.

- [ ] **Step 3: ACES 톤매핑 적용 + 입자 끄기**

Tool 순서:
1. `editor_invoke_method`(`{"className":"BlackHoleSim.Editor.BlackHoleSimEditorTools","methodName":"SetTonemappingACES"}`) → `editor_read_log`에서 `"Tonemapping set to ACES"` 확인.
2. `editor_invoke_method`(`{"className":"BlackHoleSim.Editor.BlackHoleSimEditorTools","methodName":"SetParticleFieldActive","args":[false]}`). 인자 전달이 안 되면(2단계 사례) 무인자 래퍼 `DisableParticleField`를 추가하는 대신, `game_object_set_active`(`{"name":"ParticleField","active":false}`) MCP 툴로 직접 끈다.
3. `editor_save_scene`.

- [ ] **Step 4: 통합 스크린샷**

Tool: `sim_play` → `editor_wait_ready` → `screenshot_game`(`{"description":"full Gargantua: geodesic lensing + volumetric disk + beaming + redshift + ACES + bloom"}`) → `sim_stop`
Expected: 포톤 링 + 그림자 위/아래로 감긴 발광 디스크 이중상 + 좌우 도플러 비대칭 + Bloom 발광이 톤매핑으로 자연스럽게 합성. 입자 빌보드 없음. 너무 어둡거나 날아가면 `editor_invoke_method`(`SetBloomIntensity`, args [1.0, 0.7]) 또는 컨트롤러 `diskDensity`로 조정.

- [ ] **Step 5: 전체 EditMode 회귀 확인**

Tool: `editor_invoke_method`(`RunAll`) → `editor_wait_ready` → `editor_read_log`(`{"types":["Log"]}`)
Expected: `[TESTRESULT] failed=0`. passed = 기존 12 + Task1(3) + Task2(5) = 20.

- [ ] **Step 6: 입자 토글 동작 회귀 확인(다시 켜고 끄기)**

Tool: `game_object_set_active`(`{"name":"ParticleField","active":true}`) → `sim_play` → `screenshot_game`(`{"description":"particle dynamics toggled back on over geodesic disk"}`) → `sim_stop` → `game_object_set_active`(`{"name":"ParticleField","active":false}`) → `editor_save_scene`.
Expected: 토글 시 PW 입자가 측지선 배경 위에 정상 합성, 다시 끄면 순수 렌더 디스크만.

- [ ] **Step 7: 커밋**

```bash
git add Assets/BlackHoleSim/Editor/BlackHoleSimEditorTools.cs Assets/Scenes/SampleScene.unity Assets/Settings/SampleSceneProfile.asset
git commit -m "feat: 3단계 통합 — ACES 톤매핑 + Bloom + 입자 토글"
```

---

## Self-Review 결과

**Spec 커버리지:** 측지선 적분(Task 1,4), 약장 편향 보정(Task 1), 포톤 스피어/링(Task 1,4), 볼륨메트릭 디스크(Task 5), 도플러 빔ing+중력 적색편이(Task 2,6), 톤매핑(Task 7), 품질 슬라이더(`_BHStepCount`, Task 3), 입자 토글(Task 7), 동역학 비변경(전 태스크 광학 한정), 신규 패키지 미추가 — 모두 커버.

**플레이스홀더 스캔:** "TBD/TODO" 없음. 셰이더 단계는 스크린샷 검증으로 명시. Task 7 Step 3의 인자 전달 실패 대비책(`game_object_set_active` 직접 호출)을 구체적으로 적었음.

**타입/이름 일관성:** `_BH*` 전역 이름이 Task 3(push)과 Task 4~6(소비)에서 동일. `PhotonGeodesic.Acceleration`/`AngularMomentumSq`/`Step`(Task 1)이 Task 4 HLSL `GeodesicAccel`로 미러. `Relativity.OrbitalSpeed/DopplerFactor/GravitationalRedshift/BeamingIntensity`(Task 2)가 Task 6 HLSL `OrbitalSpeedRel/DopplerFactor/GravRedshift`로 미러. 측지선 상수 1.5가 Constraints·Task 1·Task 4에서 일치.

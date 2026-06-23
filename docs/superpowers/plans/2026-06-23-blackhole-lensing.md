# 블랙홀 레이마칭 렌즈(2단계 비주얼) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 블랙홀 주변 화면을 실시간 레이마칭으로 휘게 그려 중력 렌즈·이벤트 호라이즌 그림자·발광하는 강착원반·절차적 별 배경을 가진 비주얼을 구현한다.

**Architecture:** URP `ScriptableRendererFeature`(Render Graph API) 풀스크린 패스가 `BeforeRenderingOpaques`에 배경(별+렌즈+그림자+원반)을 그리고, 1단계 동역학 오브젝트(입자/던진 물체)는 그 위에 일반 렌더로 합성된다. 매개변수는 `BlackHoleLensController`가 매 프레임 전역 셰이더 프로퍼티로 push한다.

**Tech Stack:** Unity 6 (6000.5.0f1), URP 17.5.0, Render Graph API, HLSL, Unity Test Framework(NUnit, EditMode).

## Global Constraints

- 엔진/렌더 파이프라인: Unity 6 (6000.5.0f1), URP 17.5.0 — 신규 패키지 추가하지 않음(URP 기본 기능만 사용).
- 1단계 코드(`BlackHole.cs`, `GravityField.cs`, `GravityIntegrator.cs`, `ParticleField.cs`, `ThrowableBody.cs`, `SimController.cs`)는 수정하지 않는다(스펙: "1단계는 변경하지 않고 그대로 사용").
- 렌즈는 화면 배경 레이마칭 패스로만 구현. 1단계 입자/던진 물체 자체를 광학적으로 휘게 하지 않는다(범위 밖).
- 회전 블랙홀(커 메트릭), 다중 블랙홀, 정확한 GR 측지선 적분, 외부 큐브맵 임포트는 범위 밖.
- 모든 코드 변경 후 `mcp__UnityMCP__editor_refresh_assets` → `mcp__UnityMCP__editor_recompile` → `mcp__UnityMCP__editor_wait_ready` → `mcp__UnityMCP__editor_read_log`(`types:["Error"]`)로 컴파일 에러 0건을 확인한다.

---

## 파일 구조

| 파일 | 역할 |
|---|---|
| `Assets/BlackHoleSim/Runtime/BlackBodyColor.cs` | 온도(K) → RGB 순수 함수. 단위 테스트 가능, 셰이더 색 램프의 C# 기준점 |
| `Assets/BlackHoleSim/Tests/Editor/BlackBodyColorTests.cs` | `BlackBodyColor` EditMode 테스트 |
| `Assets/BlackHoleSim/Runtime/BlackHoleLensController.cs` | 매 프레임 블랙홀/원반/카메라 파라미터를 전역 셰이더 프로퍼티로 push |
| `Assets/BlackHoleSim/Tests/Editor/BlackHoleLensControllerTests.cs` | `BlackHoleLensController.PushGlobals` EditMode 테스트 |
| `Assets/BlackHoleSim/Runtime/BlackHoleLensFeature.cs` | `ScriptableRendererFeature` + Render Graph 풀스크린 패스 |
| `Assets/BlackHoleSim/Shaders/BlackHoleLens.shader` | 별 배경 + 레이마칭 렌즈 + 그림자 + 강착원반 HLSL |
| `Assets/BlackHoleSim/Editor/BlackHoleSimEditorTools.cs` | (기존 파일에 메서드 추가) 머티리얼 생성, Renderer Feature 등록, Bloom 튜닝 |
| `Assets/BlackHoleSim/Runtime/BlackHoleSim.Runtime.asmdef` | (기존 파일 수정) URP 어셈블리 참조 추가 |
| `Assets/Scenes/SampleScene.unity` | (MCP로 수정) `BlackHoleLensController` GameObject 추가, 기존 `BlackHole` 구체 렌더러 비활성화(Task 9) |

---

### Task 1: BlackBodyColor (온도 → 색 순수 함수)

**Files:**
- Create: `Assets/BlackHoleSim/Runtime/BlackBodyColor.cs`
- Test: `Assets/BlackHoleSim/Tests/Editor/BlackBodyColorTests.cs`

**Interfaces:**
- Produces: `public static class BlackBodyColor { public static Color Evaluate(float temperatureKelvin); }` — Task 8(셰이더)이 동일 다항식을 HLSL로 미러링할 때 기준으로 참조.

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
using NUnit.Framework;
using UnityEngine;
using BlackHoleSim;

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
```

이 파일을 `Assets/BlackHoleSim/Tests/Editor/BlackBodyColorTests.cs`에 Write 툴로 작성한다.

- [ ] **Step 2: 컴파일 에러로 실패하는지 확인**

Tool: `mcp__UnityMCP__editor_refresh_assets` → `mcp__UnityMCP__editor_recompile` → `mcp__UnityMCP__editor_wait_ready` → `mcp__UnityMCP__editor_read_log` (`{"types": ["Error"], "count": 50}`)
Expected: `BlackHoleSim.BlackBodyColor`를 찾을 수 없다는 컴파일 에러(`CS0234` 또는 `CS0246`).

- [ ] **Step 3: 최소 구현 작성**

```csharp
using UnityEngine;

namespace BlackHoleSim
{
    /// <summary>Tanner Helland 근사 — 온도(K)를 RGB로 변환. 셰이더의 BlackBodyColorApprox와 동일 다항식.</summary>
    public static class BlackBodyColor
    {
        public static Color Evaluate(float temperatureKelvin)
        {
            float t = Mathf.Clamp(temperatureKelvin, 1000f, 40000f) / 100f;

            float r = t <= 66f
                ? 255f
                : Mathf.Clamp(329.698727446f * Mathf.Pow(t - 60f, -0.1332047592f), 0f, 255f);

            float g = t <= 66f
                ? Mathf.Clamp(99.4708025861f * Mathf.Log(t) - 161.1195681661f, 0f, 255f)
                : Mathf.Clamp(288.1221695283f * Mathf.Pow(t - 60f, -0.0755148492f), 0f, 255f);

            float b = t >= 66f
                ? 255f
                : (t <= 19f ? 0f : Mathf.Clamp(138.5177312231f * Mathf.Log(t - 10f) - 305.0447927307f, 0f, 255f));

            return new Color(r / 255f, g / 255f, b / 255f, 1f);
        }
    }
}
```

이 파일을 `Assets/BlackHoleSim/Runtime/BlackBodyColor.cs`에 Write 툴로 작성한다.

- [ ] **Step 4: 테스트 실행, 통과 확인**

Tool: `mcp__UnityMCP__editor_refresh_assets` → `mcp__UnityMCP__editor_recompile` → `mcp__UnityMCP__editor_wait_ready` → `mcp__UnityMCP__editor_read_log` (`{"types": ["Error"], "count": 50}`, 0건 기대) → `mcp__UnityMCP__editor_invoke_method` (`{"className": "BlackHoleSim.Tests.EditModeTestRunner", "methodName": "RunAll"}`) → `mcp__UnityMCP__editor_wait_ready` → `mcp__UnityMCP__editor_read_log` (`{"types": ["Log"], "count": 20}`, `[TESTRESULT]` 라인에서 `failed=0` 확인, `passed` 값이 이전(5)보다 3 증가).

- [ ] **Step 5: 커밋**

```bash
git add Assets/BlackHoleSim/Runtime/BlackBodyColor.cs Assets/BlackHoleSim/Tests/Editor/BlackBodyColorTests.cs
git commit -m "feat: 온도-색 변환(BlackBodyColor) 순수 함수 + 테스트"
```

---

### Task 2: BlackHoleLensController (전역 셰이더 프로퍼티 push)

**Files:**
- Create: `Assets/BlackHoleSim/Runtime/BlackHoleLensController.cs`
- Test: `Assets/BlackHoleSim/Tests/Editor/BlackHoleLensControllerTests.cs`

**Interfaces:**
- Consumes: `BlackHole.transform.position`(`UnityEngine.Transform.position`), `BlackHole.EventHorizonRadius`(`float`), `BlackHole.Mu`(`float`) — 모두 `Assets/BlackHoleSim/Runtime/BlackHole.cs`에 이미 존재.
- Produces: `public class BlackHoleLensController : MonoBehaviour { public void Configure(BlackHole bh, Camera camera); public void PushGlobals(); }`. 전역 셰이더 프로퍼티 이름: `_BHWorldPos`, `_BHHorizonRadius`, `_BHLensMu`, `_BHSoftening`, `_BHStepSize`, `_BHStepCount`, `_BHDiskInner`, `_BHDiskOuter`, `_BHDiskTempInner`, `_BHDiskTempOuter`, `_BHDopplerStrength`, `_BHStarDensity`, `_BHCamPos`, `_BHCamForward`, `_BHCamRight`, `_BHCamUp`, `_BHTanHalfFovX`, `_BHTanHalfFovY` — Task 4~8의 셰이더가 이 정확한 이름으로 읽는다.

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
using NUnit.Framework;
using UnityEngine;
using BlackHoleSim;

public class BlackHoleLensControllerTests
{
    [Test]
    public void PushGlobals_WritesBlackHoleWorldPositionToGlobalShaderProperty()
    {
        var bhGo = new GameObject("TestBH");
        bhGo.transform.position = new Vector3(3f, 0f, 5f);
        var bh = bhGo.AddComponent<BlackHole>();

        var camGo = new GameObject("TestCam");
        var cam = camGo.AddComponent<Camera>();

        var ctrlGo = new GameObject("TestController");
        var ctrl = ctrlGo.AddComponent<BlackHoleLensController>();
        ctrl.Configure(bh, cam);

        ctrl.PushGlobals();

        Vector4 result = Shader.GetGlobalVector("_BHWorldPos");
        Assert.AreEqual(3f, result.x, 0.001f);
        Assert.AreEqual(5f, result.z, 0.001f);

        Object.DestroyImmediate(ctrlGo);
        Object.DestroyImmediate(camGo);
        Object.DestroyImmediate(bhGo);
    }

    [Test]
    public void PushGlobals_WritesEventHorizonRadius()
    {
        var bhGo = new GameObject("TestBH2");
        var bh = bhGo.AddComponent<BlackHole>();
        bh.Configure(1f, 4000f, 0.5f, 2.5f);

        var camGo = new GameObject("TestCam2");
        var cam = camGo.AddComponent<Camera>();

        var ctrlGo = new GameObject("TestController2");
        var ctrl = ctrlGo.AddComponent<BlackHoleLensController>();
        ctrl.Configure(bh, cam);

        ctrl.PushGlobals();

        float radius = Shader.GetGlobalFloat("_BHHorizonRadius");
        Assert.AreEqual(2.5f, radius, 0.001f);

        Object.DestroyImmediate(ctrlGo);
        Object.DestroyImmediate(camGo);
        Object.DestroyImmediate(bhGo);
    }
}
```

이 파일을 `Assets/BlackHoleSim/Tests/Editor/BlackHoleLensControllerTests.cs`에 Write 툴로 작성한다.

- [ ] **Step 2: 컴파일 에러로 실패하는지 확인**

Tool: `mcp__UnityMCP__editor_refresh_assets` → `mcp__UnityMCP__editor_recompile` → `mcp__UnityMCP__editor_wait_ready` → `mcp__UnityMCP__editor_read_log` (`{"types": ["Error"], "count": 50}`)
Expected: `BlackHoleSim.BlackHoleLensController`를 찾을 수 없다는 컴파일 에러.

- [ ] **Step 3: 최소 구현 작성**

```csharp
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
```

이 파일을 `Assets/BlackHoleSim/Runtime/BlackHoleLensController.cs`에 Write 툴로 작성한다.

- [ ] **Step 4: 테스트 실행, 통과 확인**

Tool: `mcp__UnityMCP__editor_refresh_assets` → `mcp__UnityMCP__editor_recompile` → `mcp__UnityMCP__editor_wait_ready` → `mcp__UnityMCP__editor_read_log`(`{"types": ["Error"], "count": 50}`, 0건) → `mcp__UnityMCP__editor_invoke_method`(`{"className": "BlackHoleSim.Tests.EditModeTestRunner", "methodName": "RunAll"}`) → `mcp__UnityMCP__editor_wait_ready` → `mcp__UnityMCP__editor_read_log`(`{"types": ["Log"], "count": 20}`, `[TESTRESULT] failed=0`, `passed`가 이전보다 2 증가).

- [ ] **Step 5: 커밋**

```bash
git add Assets/BlackHoleSim/Runtime/BlackHoleLensController.cs Assets/BlackHoleSim/Tests/Editor/BlackHoleLensControllerTests.cs
git commit -m "feat: BlackHoleLensController 전역 셰이더 프로퍼티 push"
```

---

### Task 3: Renderer Feature 골격 + 풀스크린 패스 배선 검증

**Files:**
- Create: `Assets/BlackHoleSim/Shaders/BlackHoleLens.shader`
- Create: `Assets/BlackHoleSim/Runtime/BlackHoleLensFeature.cs`
- Modify: `Assets/BlackHoleSim/Runtime/BlackHoleSim.Runtime.asmdef`

**Interfaces:**
- Produces: `public class BlackHoleLensFeature : UnityEngine.Rendering.Universal.ScriptableRendererFeature { public Material lensMaterial; }` — Task 4가 이 `lensMaterial` 필드에 머티리얼을 와이어링하고 PC_Renderer에 이 클래스의 인스턴스를 등록한다.
- 셰이더 이름: `"Hidden/BlackHoleSim/BlackHoleLens"` — Task 4의 머티리얼 생성 코드가 `Shader.Find`/`AssetDatabase.LoadAssetAtPath<Shader>`로 참조.

이 단계는 단위 테스트가 불가능한 영역(렌더 패스 배선)이므로, "단색 출력이 화면에 실제로 그려지는가"를 스크린샷으로 검증하는 것이 이 태스크의 테스트 사이클이다.

- [ ] **Step 1: asmdef에 URP 어셈블리 참조 추가**

`Assets/BlackHoleSim/Runtime/BlackHoleSim.Runtime.asmdef`를 Edit 툴로 수정한다:

```json
{
    "name": "BlackHoleSim.Runtime",
    "rootNamespace": "BlackHoleSim",
    "references": ["Unity.InputSystem", "Unity.RenderPipelines.Universal.Runtime", "Unity.RenderPipelines.Core.Runtime"],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "autoReferenced": true
}
```

- [ ] **Step 2: 단색 출력 셰이더 작성**

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
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            half4 Frag(Varyings input) : SV_Target
            {
                return half4(0.1, 0.6, 0.5, 1.0);
            }
            ENDHLSL
        }
    }
}
```

이 파일을 `Assets/BlackHoleSim/Shaders/BlackHoleLens.shader`에 Write 툴로 작성한다.

- [ ] **Step 3: Renderer Feature 작성**

```csharp
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace BlackHoleSim
{
    /// <summary>풀스크린 배경 패스: 레이마칭 렌즈를 BeforeRenderingOpaques에 그려, 1단계 씬 오브젝트가 그 위에 그려지게 한다.</summary>
    public class BlackHoleLensFeature : ScriptableRendererFeature
    {
        [SerializeField] public Material lensMaterial;

        BlackHoleLensPass pass;

        public override void Create()
        {
            pass = new BlackHoleLensPass
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingOpaques
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (lensMaterial == null) return;
            pass.SetMaterial(lensMaterial);
            renderer.EnqueuePass(pass);
        }

        class BlackHoleLensPass : ScriptableRenderPass
        {
            Material material;
            const string PassName = "BlackHoleLens";

            public void SetMaterial(Material mat) => material = mat;

            class PassData
            {
                public Material material;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resourceData = frameData.Get<UniversalResourceData>();

                using (var builder = renderGraph.AddRasterRenderPass<PassData>(PassName, out var passData))
                {
                    passData.material = material;
                    builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
                    builder.AllowPassCulling(false);

                    builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
                    {
                        Blitter.BlitTexture(ctx.cmd, Vector2.one, data.material, 0);
                    });
                }
            }
        }
    }
}
```

이 파일을 `Assets/BlackHoleSim/Runtime/BlackHoleLensFeature.cs`에 Write 툴로 작성한다.

- [ ] **Step 4: 컴파일 에러 0건 확인**

Tool: `mcp__UnityMCP__editor_refresh_assets` → `mcp__UnityMCP__editor_recompile` → `mcp__UnityMCP__editor_wait_ready` → `mcp__UnityMCP__editor_read_log`(`{"types": ["Error"], "count": 50}`)
Expected: 0건. (이 태스크는 아직 씬/Renderer에 와이어링하지 않으므로 화면에는 변화가 없다 — 배선은 Task 4에서 진행)

- [ ] **Step 5: 커밋**

```bash
git add Assets/BlackHoleSim/Shaders/BlackHoleLens.shader Assets/BlackHoleSim/Runtime/BlackHoleLensFeature.cs Assets/BlackHoleSim/Runtime/BlackHoleSim.Runtime.asmdef
git commit -m "feat: BlackHoleLensFeature 골격 + 단색 출력 셰이더"
```

---

### Task 4: 머티리얼 생성 + Renderer 등록 + 씬 와이어링 (배선 검증)

**Files:**
- Modify: `Assets/BlackHoleSim/Editor/BlackHoleSimEditorTools.cs`
- Create (에디터 스크립트가 생성): `Assets/BlackHoleSim/Materials/BlackHoleLens.mat`
- Modify: `Assets/Settings/PC_Renderer.asset` (에디터 스크립트가 수정)
- Modify: `Assets/Scenes/SampleScene.unity` (MCP 툴로 GameObject 추가)

**Interfaces:**
- Consumes: `BlackHoleSim.BlackHoleLensFeature`(Task 3), `BlackHoleSim.BlackHoleLensController`(Task 2).
- Produces: `BlackHoleSimEditorTools.RegisterLensFeature()` — 멱등(이미 등록되어 있으면 중복 등록하지 않음).

- [ ] **Step 1: Editor 헬퍼에 메서드 추가**

`Assets/BlackHoleSim/Editor/BlackHoleSimEditorTools.cs`의 기존 내용은 유지하고, 파일 상단 `using` 절과 클래스 본문에 아래를 추가한다(Edit 툴 사용):

기존 `using` 절:
```csharp
using System.IO;
using UnityEditor;
using UnityEngine;
```
다음으로 교체:
```csharp
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
```

`WireSimController()` 메서드 닫는 `}` 바로 다음(클래스가 끝나는 `}` 이전)에 아래 메서드들을 추가:

```csharp

        const string LensShaderPath = "Assets/BlackHoleSim/Shaders/BlackHoleLens.shader";
        const string LensMaterialPath = "Assets/BlackHoleSim/Materials/BlackHoleLens.mat";
        const string RendererDataPath = "Assets/Settings/PC_Renderer.asset";

        public static void RegisterLensFeature()
        {
            var rendererData = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(RendererDataPath);
            if (rendererData == null) { Debug.LogWarning("[Editor] Renderer data not found: " + RendererDataPath); return; }

            foreach (var existing in rendererData.rendererFeatures)
            {
                if (existing is BlackHoleSim.BlackHoleLensFeature)
                {
                    Debug.Log("[Editor] BlackHoleLensFeature already registered");
                    return;
                }
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(LensMaterialPath);
            if (material == null)
            {
                Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(LensShaderPath);
                if (shader == null) { Debug.LogWarning("[Editor] Lens shader not found: " + LensShaderPath); return; }

                material = new Material(shader);
                Directory.CreateDirectory(Path.GetDirectoryName(LensMaterialPath));
                AssetDatabase.CreateAsset(material, LensMaterialPath);
            }

            var feature = ScriptableObject.CreateInstance<BlackHoleSim.BlackHoleLensFeature>();
            feature.name = "BlackHoleLensFeature";
            feature.lensMaterial = material;
            AssetDatabase.AddObjectToAsset(feature, rendererData);

            rendererData.rendererFeatures.Add(feature);

            EditorUtility.SetDirty(rendererData);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Editor] Registered BlackHoleLensFeature on " + RendererDataPath);
        }

        public static void SetBloomIntensity(float intensity, float threshold)
        {
            const string profilePath = "Assets/Settings/SampleSceneProfile.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null) { Debug.LogWarning("[Editor] Volume profile not found: " + profilePath); return; }

            if (profile.TryGet(out Bloom bloom))
            {
                bloom.intensity.value = intensity;
                bloom.threshold.value = threshold;
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
                Debug.Log($"[Editor] Bloom intensity={intensity} threshold={threshold}");
            }
            else
            {
                Debug.LogWarning("[Editor] Bloom component not found on volume profile");
            }
        }
```

- [ ] **Step 2: 컴파일 에러 0건 확인**

Tool: `mcp__UnityMCP__editor_refresh_assets` → `mcp__UnityMCP__editor_recompile` → `mcp__UnityMCP__editor_wait_ready` → `mcp__UnityMCP__editor_read_log`(`{"types": ["Error"], "count": 50}`)
Expected: 0건.

- [ ] **Step 3: Renderer Feature 등록 실행**

Tool: `mcp__UnityMCP__editor_invoke_method` (`{"className": "BlackHoleSim.Editor.BlackHoleSimEditorTools", "methodName": "RegisterLensFeature"}`)
→ `mcp__UnityMCP__editor_wait_ready`
→ `mcp__UnityMCP__editor_read_log`(`{"types": ["Log"], "count": 20}`)에서 `"Registered BlackHoleLensFeature"` 로그 확인.

이후 등록된 `ScriptableRendererFeature`를 URP가 인식하도록 한 번 더 리컴파일: `mcp__UnityMCP__editor_refresh_assets` → `mcp__UnityMCP__editor_recompile` → `mcp__UnityMCP__editor_wait_ready` → `mcp__UnityMCP__editor_read_log`(`{"types": ["Error"], "count": 50}`, 0건).

- [ ] **Step 4: 씬에 BlackHoleLensController GameObject 추가**

Tool 순서:
1. `mcp__UnityMCP__game_object_create` (`{"name": "BlackHoleLensController"}`)
2. `mcp__UnityMCP__component_add` (`{"target": "BlackHoleLensController", "componentType": "BlackHoleSim.BlackHoleLensController"}`)
3. `mcp__UnityMCP__component_set` (`{"target": "BlackHoleLensController", "componentType": "BlackHoleSim.BlackHoleLensController", "values": {"blackHole": "BlackHole", "cam": "Main Camera"}}`)
4. `mcp__UnityMCP__component_get_all` (`{"target": "BlackHoleLensController"}`)로 `blackHole`/`cam` 필드가 실제로 와이어링됐는지 확인(이전 세션에서 `component_set`이 씬 오브젝트 참조에는 정상 동작했으나 에셋 참조에는 실패했던 사례가 있었음 — 여기 `blackHole`/`cam`은 씬 오브젝트 참조이므로 정상 동작할 것으로 예상).

- [ ] **Step 5: 배선 검증 스크린샷**

Tool: `mcp__UnityMCP__sim_play` → `mcp__UnityMCP__editor_wait_ready` → `mcp__UnityMCP__screenshot_game`(`{"description": "lens feature wiring check - teal background"}`)
Expected: 화면 배경이 단색 청록(`0.1, 0.6, 0.5`)으로 채워지고, 그 위에 블랙홀 구체/입자가 정상적으로 보임(배경보다 위에 렌더). `mcp__UnityMCP__sim_stop` 호출.

- [ ] **Step 6: 커밋**

```bash
git add Assets/BlackHoleSim/Editor/BlackHoleSimEditorTools.cs Assets/BlackHoleSim/Materials/BlackHoleLens.mat Assets/Settings/PC_Renderer.asset Assets/Scenes/SampleScene.unity
git commit -m "feat: 렌즈 머티리얼/Renderer Feature 등록 + 씬 와이어링"
```

---

### Task 5: 절차적 별 배경

**Files:**
- Modify: `Assets/BlackHoleSim/Shaders/BlackHoleLens.shader`

**Interfaces:**
- Consumes: `_BHCamPos`, `_BHCamForward`, `_BHCamRight`, `_BHCamUp`, `_BHTanHalfFovX`, `_BHTanHalfFovY`, `_BHStarDensity`(Task 2가 push).
- Produces: `CameraRayDir(float2 uv)`, `StarField(float3 dir)`, `Hash13(float3 p)` HLSL 함수 — Task 6이 `CameraRayDir`을 그대로 사용하고, Task 7~8이 `StarField`를 march 종료 케이스로 사용.

- [ ] **Step 1: 셰이더를 별 배경 버전으로 교체**

`Assets/BlackHoleSim/Shaders/BlackHoleLens.shader` 전체를 Write 툴로 아래 내용으로 교체:

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
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float3 _BHCamPos;
            float3 _BHCamForward;
            float3 _BHCamRight;
            float3 _BHCamUp;
            float _BHTanHalfFovX;
            float _BHTanHalfFovY;
            float _BHStarDensity;

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

            half4 Frag(Varyings input) : SV_Target
            {
                float3 dir = CameraRayDir(input.texcoord);
                half3 color = StarField(dir);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
```

- [ ] **Step 2: 컴파일 에러 0건 확인**

Tool: `mcp__UnityMCP__editor_refresh_assets` → `mcp__UnityMCP__editor_recompile` → `mcp__UnityMCP__editor_wait_ready` → `mcp__UnityMCP__editor_read_log`(`{"types": ["Error"], "count": 50}`)
Expected: 0건.

- [ ] **Step 3: 스크린샷 검증**

Tool: `mcp__UnityMCP__sim_play` → `mcp__UnityMCP__editor_wait_ready` → `mcp__UnityMCP__screenshot_game`(`{"description": "procedural star background"}`)
Expected: 검은 배경에 흩어진 흰 점(별)이 보임. 카메라를 회전시켜(`mcp__UnityMCP__transform_set_rotation` 또는 기존 `editor_get_camera`/`editor_set_camera`) 다시 스크린샷을 찍어 별 패턴이 방향에 따라 달라지는지(고정 텍스처가 아님을) 확인. `mcp__UnityMCP__sim_stop` 호출.

- [ ] **Step 4: 커밋**

```bash
git add Assets/BlackHoleSim/Shaders/BlackHoleLens.shader
git commit -m "feat: 절차적 별 배경 레이마칭 셰이더"
```

---

### Task 6: 중력 렌즈 굴절 (레이마칭)

**Files:**
- Modify: `Assets/BlackHoleSim/Shaders/BlackHoleLens.shader`

**Interfaces:**
- Consumes: `_BHWorldPos`, `_BHLensMu`, `_BHSoftening`, `_BHStepSize`, `_BHStepCount`(Task 2가 push), `CameraRayDir`/`StarField`(Task 5).
- Produces: `AccelerationAt(float3 source, float mu, float softening, float3 pos)`(`GravityField.AccelerationAt`의 HLSL 미러), `March(float3 origin, float3 dir)` — Task 7이 그림자 조건을 추가할 지점.

- [ ] **Step 1: 셰이더를 레이마칭 굴절 버전으로 교체**

`Assets/BlackHoleSim/Shaders/BlackHoleLens.shader` 전체를 Write 툴로 아래 내용으로 교체:

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
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float3 _BHCamPos;
            float3 _BHCamForward;
            float3 _BHCamRight;
            float3 _BHCamUp;
            float _BHTanHalfFovX;
            float _BHTanHalfFovY;
            float _BHStarDensity;

            float3 _BHWorldPos;
            float _BHLensMu;
            float _BHSoftening;
            float _BHStepSize;
            int _BHStepCount;

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

            // GravityField.AccelerationAt(BlackHoleSim/Runtime/GravityField.cs)의 HLSL 미러.
            float3 AccelerationAt(float3 source, float mu, float softening, float3 pos)
            {
                float3 r = source - pos;
                float dist2 = dot(r, r) + softening * softening;
                float invDist = rsqrt(max(dist2, 1e-6));
                float invDist3 = invDist / dist2;
                return mu * invDist3 * r;
            }

            half3 March(float3 origin, float3 dir)
            {
                float3 p = origin;
                float3 d = dir;

                for (int i = 0; i < _BHStepCount; i++)
                {
                    d += AccelerationAt(_BHWorldPos, _BHLensMu, _BHSoftening, p) * _BHStepSize;
                    p += normalize(d) * _BHStepSize;
                }

                return StarField(normalize(d));
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

Tool: `mcp__UnityMCP__editor_refresh_assets` → `mcp__UnityMCP__editor_recompile` → `mcp__UnityMCP__editor_wait_ready` → `mcp__UnityMCP__editor_read_log`(`{"types": ["Error"], "count": 50}`)
Expected: 0건.

- [ ] **Step 3: 스크린샷 검증**

Tool: `mcp__UnityMCP__sim_play` → `mcp__UnityMCP__editor_wait_ready` → `mcp__UnityMCP__screenshot_game`(`{"description": "gravitational lensing distortion of star field"}`)
Expected: 블랙홀 근처의 별 패턴이 휘어 보임(직선으로 안 보이고 곡선으로 흩어짐). 효과가 약하면 `BlackHoleLensController`의 `lensStrength` 인스펙터 값을 키워(`mcp__UnityMCP__component_set`) 재확인. `mcp__UnityMCP__sim_stop` 호출.

- [ ] **Step 4: 커밋**

```bash
git add Assets/BlackHoleSim/Shaders/BlackHoleLens.shader
git commit -m "feat: 레이마칭 중력 렌즈 굴절"
```

---

### Task 7: 이벤트 호라이즌 그림자

**Files:**
- Modify: `Assets/BlackHoleSim/Shaders/BlackHoleLens.shader`

**Interfaces:**
- Consumes: `_BHHorizonRadius`(Task 2가 push), `March`(Task 6, 내부 수정).
- Produces: `March`이 호라이즌 진입 시 `half3(0,0,0)`을 반환 — Task 8이 같은 함수에 강착원반 교차 검사를 추가할 지점.

- [ ] **Step 1: March 함수에 그림자 조건 추가**

`Assets/BlackHoleSim/Shaders/BlackHoleLens.shader`에서 `float _BHStepSize;` 다음 줄에 `float _BHHorizonRadius;`를 추가하고, `March` 함수를 아래로 교체(Edit 툴):

기존:
```hlsl
            half3 March(float3 origin, float3 dir)
            {
                float3 p = origin;
                float3 d = dir;

                for (int i = 0; i < _BHStepCount; i++)
                {
                    d += AccelerationAt(_BHWorldPos, _BHLensMu, _BHSoftening, p) * _BHStepSize;
                    p += normalize(d) * _BHStepSize;
                }

                return StarField(normalize(d));
            }
```

교체:
```hlsl
            half3 March(float3 origin, float3 dir)
            {
                float3 p = origin;
                float3 d = dir;

                for (int i = 0; i < _BHStepCount; i++)
                {
                    float3 toCenter = _BHWorldPos - p;
                    if (dot(toCenter, toCenter) <= _BHHorizonRadius * _BHHorizonRadius)
                        return half3(0, 0, 0);

                    d += AccelerationAt(_BHWorldPos, _BHLensMu, _BHSoftening, p) * _BHStepSize;
                    p += normalize(d) * _BHStepSize;
                }

                return StarField(normalize(d));
            }
```

`_BHHorizonRadius` 변수 선언 추가 위치(전체 변수 선언 블록):
```hlsl
            float3 _BHWorldPos;
            float _BHLensMu;
            float _BHSoftening;
            float _BHStepSize;
            float _BHHorizonRadius;
            int _BHStepCount;
```

- [ ] **Step 2: 컴파일 에러 0건 확인**

Tool: `mcp__UnityMCP__editor_refresh_assets` → `mcp__UnityMCP__editor_recompile` → `mcp__UnityMCP__editor_wait_ready` → `mcp__UnityMCP__editor_read_log`(`{"types": ["Error"], "count": 50}`)
Expected: 0건.

- [ ] **Step 3: 스크린샷 검증**

Tool: `mcp__UnityMCP__sim_play` → `mcp__UnityMCP__editor_wait_ready` → `mcp__UnityMCP__screenshot_game`(`{"description": "event horizon shadow"}`)
Expected: 블랙홀 중심에 검은 원(그림자)이 보이고, 그 가장자리에서 별빛이 강하게 휘어 들어가는 모습. `mcp__UnityMCP__sim_stop` 호출.

- [ ] **Step 4: 커밋**

```bash
git add Assets/BlackHoleSim/Shaders/BlackHoleLens.shader
git commit -m "feat: 이벤트 호라이즌 그림자 레이마칭"
```

---

### Task 8: 강착원반 발광 (온도 그라데이션 + 도플러)

**Files:**
- Modify: `Assets/BlackHoleSim/Shaders/BlackHoleLens.shader`

**Interfaces:**
- Consumes: `_BHDiskInner`, `_BHDiskOuter`, `_BHDiskTempInner`, `_BHDiskTempOuter`, `_BHDopplerStrength`(Task 2가 push), `March`(Task 7, 내부 수정).
- Produces: `BlackBodyColorApprox(float tempKelvin)`(`BlackBodyColor.Evaluate`의 HLSL 미러, Task 1과 동일 다항식), `DiskColorAt(float3 hitPoint, float3 marchDir)`.

- [ ] **Step 1: 강착원반 변수/함수 추가 + March 수정**

`Assets/BlackHoleSim/Shaders/BlackHoleLens.shader`에서 변수 선언 블록을 아래로 교체(Edit 툴):

기존:
```hlsl
            float3 _BHWorldPos;
            float _BHLensMu;
            float _BHSoftening;
            float _BHStepSize;
            float _BHHorizonRadius;
            int _BHStepCount;
```

교체:
```hlsl
            float3 _BHWorldPos;
            float _BHLensMu;
            float _BHSoftening;
            float _BHStepSize;
            float _BHHorizonRadius;
            int _BHStepCount;
            float _BHDiskInner;
            float _BHDiskOuter;
            float _BHDiskTempInner;
            float _BHDiskTempOuter;
            float _BHDopplerStrength;
```

`AccelerationAt` 함수 다음, `March` 함수 이전에 아래 두 함수를 추가:

```hlsl
            // BlackBodyColor.Evaluate(BlackHoleSim/Runtime/BlackBodyColor.cs)의 HLSL 미러.
            half3 BlackBodyColorApprox(float tempKelvin)
            {
                float t = clamp(tempKelvin, 1000.0, 40000.0) / 100.0;
                float r = t <= 66.0
                    ? 255.0
                    : clamp(329.698727446 * pow(t - 60.0, -0.1332047592), 0.0, 255.0);
                float g = t <= 66.0
                    ? clamp(99.4708025861 * log(t) - 161.1195681661, 0.0, 255.0)
                    : clamp(288.1221695283 * pow(t - 60.0, -0.0755148492), 0.0, 255.0);
                float b = t >= 66.0
                    ? 255.0
                    : (t <= 19.0 ? 0.0 : clamp(138.5177312231 * log(t - 10.0) - 305.0447927307, 0.0, 255.0));
                return half3(r, g, b) / 255.0;
            }

            half3 DiskColorAt(float3 hitPoint, float3 marchDir)
            {
                float radius = length(hitPoint.xz - _BHWorldPos.xz);
                float u = saturate((radius - _BHDiskInner) / max(_BHDiskOuter - _BHDiskInner, 0.0001));
                float temp = lerp(_BHDiskTempInner, _BHDiskTempOuter, u);
                half3 baseColor = BlackBodyColorApprox(temp);

                float3 tangent = normalize(cross(float3(0, 1, 0), hitPoint - _BHWorldPos));
                float approach = dot(tangent, -marchDir);
                float doppler = 1.0 + _BHDopplerStrength * approach;

                return baseColor * max(doppler, 0.0);
            }
```

`March` 함수를 아래로 교체:

기존:
```hlsl
            half3 March(float3 origin, float3 dir)
            {
                float3 p = origin;
                float3 d = dir;

                for (int i = 0; i < _BHStepCount; i++)
                {
                    float3 toCenter = _BHWorldPos - p;
                    if (dot(toCenter, toCenter) <= _BHHorizonRadius * _BHHorizonRadius)
                        return half3(0, 0, 0);

                    d += AccelerationAt(_BHWorldPos, _BHLensMu, _BHSoftening, p) * _BHStepSize;
                    p += normalize(d) * _BHStepSize;
                }

                return StarField(normalize(d));
            }
```

교체:
```hlsl
            half3 March(float3 origin, float3 dir)
            {
                float3 p = origin;
                float3 d = dir;
                float prevY = p.y - _BHWorldPos.y;

                for (int i = 0; i < _BHStepCount; i++)
                {
                    float3 toCenter = _BHWorldPos - p;
                    if (dot(toCenter, toCenter) <= _BHHorizonRadius * _BHHorizonRadius)
                        return half3(0, 0, 0);

                    d += AccelerationAt(_BHWorldPos, _BHLensMu, _BHSoftening, p) * _BHStepSize;
                    float3 next = p + normalize(d) * _BHStepSize;
                    float nextY = next.y - _BHWorldPos.y;

                    if (prevY * nextY < 0.0)
                    {
                        float tCross = prevY / (prevY - nextY);
                        float3 hit = lerp(p, next, tCross);
                        float radius = length(hit.xz - _BHWorldPos.xz);
                        if (radius >= _BHDiskInner && radius <= _BHDiskOuter)
                            return DiskColorAt(hit, normalize(d));
                    }

                    p = next;
                    prevY = nextY;
                }

                return StarField(normalize(d));
            }
```

- [ ] **Step 2: 컴파일 에러 0건 확인**

Tool: `mcp__UnityMCP__editor_refresh_assets` → `mcp__UnityMCP__editor_recompile` → `mcp__UnityMCP__editor_wait_ready` → `mcp__UnityMCP__editor_read_log`(`{"types": ["Error"], "count": 50}`)
Expected: 0건.

- [ ] **Step 3: 스크린샷 검증**

Tool: `mcp__UnityMCP__sim_play` → `mcp__UnityMCP__editor_wait_ready` → `mcp__UnityMCP__screenshot_game`(`{"description": "accretion disk glow with temperature gradient"}`)
Expected: 그림자 주변에 안쪽은 청백색, 바깥쪽은 주황/적색인 발광 원반이 보이고, 렌즈 효과로 원반 일부가 그림자 위/아래로 휘어 보임. 좌우 비대칭(도플러)도 관찰. `mcp__UnityMCP__sim_stop` 호출.

- [ ] **Step 4: 커밋**

```bash
git add Assets/BlackHoleSim/Shaders/BlackHoleLens.shader
git commit -m "feat: 강착원반 온도 그라데이션 + 도플러 비대칭"
```

---

### Task 9: 최종 통합 (기존 placeholder 비활성화, Bloom 튠, 회귀 검증)

**Files:**
- Modify: `Assets/Scenes/SampleScene.unity` (MCP 툴로 수정)
- Modify: `Assets/Settings/SampleSceneProfile.asset` (필요 시, Editor 헬퍼로 수정)

**Interfaces:**
- Consumes: `BlackHoleSimEditorTools.SetBloomIntensity(float intensity, float threshold)`(Task 4에서 이미 작성됨).

- [ ] **Step 1: 기존 BlackHole 구체 렌더러 비활성화**

이제 셰이더 그림자가 블랙홀의 시각적 표현을 전담하므로, 기존 placeholder 구체의 `Renderer`를 끈다.

Tool: `mcp__UnityMCP__renderer_set_enabled` (`{"target": "BlackHole", "enabled": false}`)

- [ ] **Step 2: 통합 스크린샷**

Tool: `mcp__UnityMCP__sim_play` → `mcp__UnityMCP__editor_wait_ready` → `mcp__UnityMCP__screenshot_game`(`{"description": "full integration: lens + shadow + disk + particles + bloom"}`)
Expected: placeholder 구체 없이 셰이더 그림자만 보임, 강착원반 발광에 Bloom이 자연스럽게 번짐, 1단계 입자(`ParticleField`)가 배경 위에 정상 렌더.

- [ ] **Step 3: 필요 시 Bloom 튠**

Step 2의 스크린샷이 너무 어둡거나(원반이 잘 안 보임) 너무 과하면(원반 디테일이 날아감), Bloom을 조정한다.

Tool: `mcp__UnityMCP__editor_invoke_method` (`{"className": "BlackHoleSim.Editor.BlackHoleSimEditorTools", "methodName": "SetBloomIntensity", "args": [0.6, 0.8]}`)
→ `mcp__UnityMCP__sim_play` → `mcp__UnityMCP__editor_wait_ready` → `mcp__UnityMCP__screenshot_game`(`{"description": "bloom retune"}`)로 재확인. 만족스러우면 다음 단계로.

- [ ] **Step 4: 전체 EditMode 테스트 회귀 확인**

Tool: `mcp__UnityMCP__editor_invoke_method`(`{"className": "BlackHoleSim.Tests.EditModeTestRunner", "methodName": "RunAll"}`) → `mcp__UnityMCP__editor_wait_ready` → `mcp__UnityMCP__editor_read_log`(`{"types": ["Log"], "count": 20}`)
Expected: `[TESTRESULT] failed=0`, `passed`가 1단계 5개 + Task1(3개) + Task2(2개) = 10개.

- [ ] **Step 5: 던지기 동작 회귀 확인**

Tool: `mcp__UnityMCP__editor_invoke_method`(`{"className": "BlackHoleSim.SimController", "methodName": "DebugThrowInScene"}`) → `mcp__UnityMCP__editor_wait_ready` → `mcp__UnityMCP__screenshot_game`(`{"description": "thrown body still visible over lens background"}`)
Expected: 던진 물체와 트레일이 배경 위에 정상적으로 보임(렌즈 패스가 1단계 동역학을 가리거나 깨뜨리지 않음). `mcp__UnityMCP__sim_stop` 호출.

- [ ] **Step 6: 커밋**

```bash
git add Assets/Scenes/SampleScene.unity Assets/Settings/SampleSceneProfile.asset
git commit -m "feat: 블랙홀 비주얼 2단계 통합 — placeholder 비활성화 + Bloom 튠"
```

---

## Self-Review 결과

**Spec 커버리지:** 단일 소스 렌즈(Task 6), 그림자(Task 7), 강착원반 온도/도플러(Task 8), 별 배경(Task 5), Bloom/Tonemapping(이미 프로젝트에 존재 — Task 9에서 확인/튠), 1단계 비변경 및 합성 순서(Task 3~4의 `BeforeRenderingOpaques` 배치), 신규 패키지 미추가(URP 기본 API만 사용) — 모두 태스크로 커버됨.

**플레이스홀더 스캔:** "TBD/TODO" 없음. 셰이더 단계(Task 3,5,6,7,8)는 단위 테스트가 불가능한 영역이라 스크린샷 검증으로 명시(스펙의 "단계적 스크린샷 검증" 전략과 일치), 각 단계마다 구체적 기대 화면을 적었음.

**타입/이름 일관성:** `_BH*` 전역 프로퍼티 이름이 Task 2(C# push)와 Task 5~8(셰이더 소비)에서 동일. `BlackHoleLensFeature.lensMaterial` 필드명이 Task 3(정의)과 Task 4(`feature.lensMaterial = material`)에서 일치. `BlackHoleSimEditorTools.RegisterLensFeature`/`SetBloomIntensity`가 Task 4에서 정의되고 Task 9에서 그대로 호출.

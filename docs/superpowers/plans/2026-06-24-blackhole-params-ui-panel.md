# 블랙홀 파라미터 UI 패널 (MVVM) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `BlackHole`/`BlackHoleLensController`/`ParticleField`의 모든 튜닝 파라미터를 런타임에 조절할 수 있는 콜랩서블 UGUI 패널을 MVVM 패턴으로 추가한다.

**Architecture:** Model(기존 3개 런타임 컴포넌트에 public 프로퍼티 추가) ↔ ViewModel(순수 C# `BlackHoleParamsViewModel`, 파라미터당 `ObservableValue<T>`) ↔ View(UGUI MonoBehaviour Row 컴포넌트, 명시적 구독/콜백 바인딩). 외부 리액티브 라이브러리 없음.

**Tech Stack:** Unity 6000.5.0f1, URP, UGUI(`com.unity.ugui`), TextMeshPro(신규 설치), Unity Input System(`com.unity.inputsystem`), NUnit EditMode 테스트.

## Global Constraints

- 세션 간 값 저장 없음 — Play 시작 시 항상 인스펙터 기본값에서 시작 (스펙: "범위 밖").
- DangerZone(`BlackHole`: G, mass, softening, r_s)은 기본 접힘 + 경고 테두리로 별도 섹션 분리.
- 텍스트는 TextMeshPro(`TMP_Text`) 사용. legacy `Text` 금지.
- 바인딩은 리플렉션/자동 데이터바인딩 없이 명시적 구독·콜백만 사용.
- 패널 토글은 Tab 키 또는 헤더 버튼 양쪽으로 가능해야 함.
- 참조 스펙: `docs/superpowers/specs/2026-06-24-blackhole-params-ui-panel-design.md`

---

## File Structure

```
claude-unity-project/Assets/BlackHoleSim/
├── Runtime/
│   ├── BlackHole.cs                       (수정: 프로퍼티 추가)
│   ├── BlackHoleLensController.cs         (수정: 프로퍼티 추가)
│   ├── ParticleField.cs                   (수정: 프로퍼티 + Reinitialize)
│   └── UI/
│       ├── ViewModels/
│       │   ├── ObservableValue.cs         (신규)
│       │   └── BlackHoleParamsViewModel.cs (신규)
│       └── Views/
│           ├── CollapsibleSection.cs      (신규)
│           ├── FloatSliderRow.cs          (신규)
│           ├── IntSliderRow.cs            (신규)
│           ├── ToggleRow.cs               (신규)
│           ├── ColorSwatchRow.cs          (신규)
│           └── ParamPanelView.cs          (신규)
└── Tests/Editor/
    ├── BlackHoleTests.cs                  (신규)
    ├── ParticleFieldTests.cs              (신규)
    └── UI/
        ├── ObservableValueTests.cs        (신규)
        └── BlackHoleParamsViewModelTests.cs (신규)
```

씬 변경(Canvas/EventSystem/패널 하이어라키 + Row 프리팹)은 Task 10에서 Unity MCP 도구로 직접 빌드한다(코드 파일 아님).

---

### Task 1: ObservableValue<T> (ViewModel 기반 타입)

**Files:**
- Create: `claude-unity-project/Assets/BlackHoleSim/Runtime/UI/ViewModels/ObservableValue.cs`
- Test: `claude-unity-project/Assets/BlackHoleSim/Tests/Editor/UI/ObservableValueTests.cs`

**Interfaces:**
- Produces: `BlackHoleSim.UI.ObservableValue<T>` — `T Value { get; set; }`, `event Action<T> Changed`. `Changed`는 값이 실제로 바뀔 때만 발생(`Equals` 비교).

- [ ] **Step 1: Write the failing test**

```csharp
using NUnit.Framework;
using BlackHoleSim.UI;

namespace BlackHoleSim.Tests.UI
{
    public class ObservableValueTests
    {
        [Test]
        public void SettingValue_FiresChangedEvent_WithNewValue()
        {
            var ov = new ObservableValue<float>(1f);
            float? received = null;
            ov.Changed += v => received = v;

            ov.Value = 2f;

            Assert.That(received, Is.EqualTo(2f));
        }

        [Test]
        public void SettingSameValue_DoesNotFireChangedEvent()
        {
            var ov = new ObservableValue<float>(1f);
            int callCount = 0;
            ov.Changed += _ => callCount++;

            ov.Value = 1f;

            Assert.That(callCount, Is.EqualTo(0));
        }

        [Test]
        public void Value_ReturnsCurrentValue_AfterConstruction()
        {
            var ov = new ObservableValue<int>(42);
            Assert.That(ov.Value, Is.EqualTo(42));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

MCP 도구 호출: `mcp__UnityMCP__editor_invoke_method` (class=`BlackHoleSim.Tests.EditModeTestRunner`, method=`RunAll`), 이어서 `mcp__UnityMCP__editor_read_log`.
Expected: 컴파일 에러 — `ObservableValue` 타입 없음 (이 시점엔 테스트가 아직 실행조차 안 됨).

- [ ] **Step 3: Write minimal implementation**

```csharp
using System;

namespace BlackHoleSim.UI
{
    /// <summary>값이 바뀔 때만 Changed를 발생시키는 경량 ViewModel 셀.</summary>
    public class ObservableValue<T>
    {
        T value;

        public ObservableValue(T initial) => value = initial;

        public T Value
        {
            get => value;
            set
            {
                if (Equals(this.value, value)) return;
                this.value = value;
                Changed?.Invoke(value);
            }
        }

        public event Action<T> Changed;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

MCP 도구 호출: `mcp__UnityMCP__editor_recompile` → `mcp__UnityMCP__editor_wait_ready` → `mcp__UnityMCP__editor_invoke_method`(`BlackHoleSim.Tests.EditModeTestRunner.RunAll`) → `mcp__UnityMCP__editor_read_log`.
Expected log: `[TESTRESULT] passed=3 failed=0 skipped=0 status=Passed` (이 3개 테스트 기준, 기존 테스트 합산 수는 더 많을 수 있음 — `failed=0`만 확인).

- [ ] **Step 5: Commit**

```bash
git add claude-unity-project/Assets/BlackHoleSim/Runtime/UI/ViewModels/ObservableValue.cs claude-unity-project/Assets/BlackHoleSim/Tests/Editor/UI/ObservableValueTests.cs
git commit -m "feat: ObservableValue<T> 기반 ViewModel 셀 추가"
```

---

### Task 2: BlackHole 프로퍼티 (DangerZone Model)

**Files:**
- Modify: `claude-unity-project/Assets/BlackHoleSim/Runtime/BlackHole.cs`
- Test: `claude-unity-project/Assets/BlackHoleSim/Tests/Editor/BlackHoleTests.cs`

**Interfaces:**
- Produces: `BlackHole.GravitationalConstant`, `BlackHole.Mass`, `BlackHole.Softening`, `BlackHole.EventHorizonRadius` — 모두 `float` get/set 프로퍼티.

- [ ] **Step 1: Write the failing test**

```csharp
using NUnit.Framework;
using UnityEngine;
using BlackHoleSim;

namespace BlackHoleSim.Tests
{
    public class BlackHoleTests
    {
        [Test]
        public void Properties_AreReadWrite_AndAffectMu()
        {
            var go = new GameObject("BH");
            var bh = go.AddComponent<BlackHole>();
            bh.Configure(1f, 4000f, 0.5f, 2f);

            bh.GravitationalConstant = 2f;
            bh.Mass = 1000f;
            bh.Softening = 0.1f;
            bh.EventHorizonRadius = 3f;

            Assert.That(bh.GravitationalConstant, Is.EqualTo(2f));
            Assert.That(bh.Mass, Is.EqualTo(1000f));
            Assert.That(bh.Softening, Is.EqualTo(0.1f));
            Assert.That(bh.EventHorizonRadius, Is.EqualTo(3f));
            Assert.That(bh.Mu, Is.EqualTo(2000f).Within(1e-3f)); // G*mass

            Object.DestroyImmediate(go);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

MCP: `editor_invoke_method`(`EditModeTestRunner.RunAll`) → `editor_read_log`.
Expected: 컴파일 에러 — `GravitationalConstant`/`Mass`/`Softening`에 set 접근자가 없음, `EventHorizonRadius`도 get-only.

- [ ] **Step 3: Write minimal implementation**

`claude-unity-project/Assets/BlackHoleSim/Runtime/BlackHole.cs`에서 다음 블록을 교체:

```csharp
        [SerializeField] float gravitationalConstant = 1f;
        [SerializeField] float mass = 4000f;
        [SerializeField] float softening = 0.5f;
        [SerializeField] float eventHorizonRadius = 2f;

        public float GravitationalConstant
        {
            get => gravitationalConstant;
            set => gravitationalConstant = value;
        }

        public float Mass
        {
            get => mass;
            set => mass = value;
        }

        public float Softening
        {
            get => softening;
            set => softening = value;
        }

        public float EventHorizonRadius
        {
            get => eventHorizonRadius;
            set => eventHorizonRadius = value;
        }

        public float Mu => gravitationalConstant * mass;
```

(기존의 `public float Mu => ...` 한 줄과 `public float EventHorizonRadius => eventHorizonRadius;` 한 줄을 제거하고 위 블록으로 대체한다.)

- [ ] **Step 4: Run test to verify it passes**

MCP: `editor_recompile` → `editor_wait_ready` → `editor_invoke_method`(`EditModeTestRunner.RunAll`) → `editor_read_log`.
Expected log: `failed=0` 포함.

- [ ] **Step 5: Commit**

```bash
git add claude-unity-project/Assets/BlackHoleSim/Runtime/BlackHole.cs claude-unity-project/Assets/BlackHoleSim/Tests/Editor/BlackHoleTests.cs
git commit -m "feat: BlackHole 코어 파라미터에 public 프로퍼티 추가"
```

---

### Task 3: BlackHoleLensController 프로퍼티 (Disk/Relativity/Quality Model)

**Files:**
- Modify: `claude-unity-project/Assets/BlackHoleSim/Runtime/BlackHoleLensController.cs`
- Modify: `claude-unity-project/Assets/BlackHoleSim/Tests/Editor/BlackHoleLensControllerTests.cs`

**Interfaces:**
- Produces: `BlackHoleLensController`에 다음 13개 get/set 프로퍼티 — `DiskInnerRadius`, `DiskOuterRadius`, `DiskThickness`, `DiskDensity`, `DiskTempInnerKelvin`, `DiskTempOuterKelvin`, `DiskColorTint`(`Color`), `BeamingStrength`, `RedshiftStrength`, `PhotonRing`, `StepCount`(`int`), `StepSize`, `StarDensity`.

- [ ] **Step 1: Write the failing test**

`BlackHoleLensControllerTests.cs` 맨 아래에 새 테스트 추가:

```csharp
    [Test]
    public void Properties_AreReadWrite_AndPushGlobalsReflectsThem()
    {
        var bhGo = new GameObject("TestBH3");
        var bh = bhGo.AddComponent<BlackHole>();

        var camGo = new GameObject("TestCam3");
        var cam = camGo.AddComponent<Camera>();

        var ctrlGo = new GameObject("TestController3");
        var ctrl = ctrlGo.AddComponent<BlackHoleLensController>();
        ctrl.Configure(bh, cam);

        ctrl.DiskInnerRadius = 7f;
        ctrl.DiskOuterRadius = 25f;
        ctrl.BeamingStrength = 0.5f;
        ctrl.StepCount = 600;
        ctrl.DiskColorTint = new Color(0.9f, 0.4f, 0.2f, 1f);

        ctrl.PushGlobals();

        Assert.That(Shader.GetGlobalFloat("_BHDiskInner"), Is.EqualTo(7f).Within(0.001f));
        Assert.That(Shader.GetGlobalFloat("_BHDiskOuter"), Is.EqualTo(25f).Within(0.001f));
        Assert.That(Shader.GetGlobalFloat("_BHBeaming"), Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(Shader.GetGlobalInt("_BHStepCount"), Is.EqualTo(600));
        Vector4 tint = Shader.GetGlobalColor("_BHDiskColorTint");
        Assert.That(tint.x, Is.EqualTo(0.9f).Within(0.001f));

        Object.DestroyImmediate(ctrlGo);
        Object.DestroyImmediate(camGo);
        Object.DestroyImmediate(bhGo);
    }
```

- [ ] **Step 2: Run test to verify it fails**

MCP: `editor_invoke_method`(`EditModeTestRunner.RunAll`) → `editor_read_log`.
Expected: 컴파일 에러 — `DiskInnerRadius` 등 프로퍼티 없음.

- [ ] **Step 3: Write minimal implementation**

`BlackHoleLensController.cs`의 필드 선언부를 교체(헤더 주석들은 유지):

```csharp
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

        public float DiskInnerRadius { get => diskInnerRadius; set => diskInnerRadius = value; }
        public float DiskOuterRadius { get => diskOuterRadius; set => diskOuterRadius = value; }
        public float DiskThickness { get => diskThickness; set => diskThickness = value; }
        public float DiskDensity { get => diskDensity; set => diskDensity = value; }
        public float DiskTempInnerKelvin { get => diskTempInnerKelvin; set => diskTempInnerKelvin = value; }
        public float DiskTempOuterKelvin { get => diskTempOuterKelvin; set => diskTempOuterKelvin = value; }
        public Color DiskColorTint { get => diskColorTint; set => diskColorTint = value; }
        public float BeamingStrength { get => beamingStrength; set => beamingStrength = value; }
        public float RedshiftStrength { get => redshiftStrength; set => redshiftStrength = value; }
        public float PhotonRing { get => photonRing; set => photonRing = value; }
        public int StepCount { get => stepCount; set => stepCount = value; }
        public float StepSize { get => stepSize; set => stepSize = value; }
        public float StarDensity { get => starDensity; set => starDensity = value; }
```

- [ ] **Step 4: Run test to verify it passes**

MCP: `editor_recompile` → `editor_wait_ready` → `editor_invoke_method`(`EditModeTestRunner.RunAll`) → `editor_read_log`.
Expected log: `failed=0`.

- [ ] **Step 5: Commit**

```bash
git add claude-unity-project/Assets/BlackHoleSim/Runtime/BlackHoleLensController.cs claude-unity-project/Assets/BlackHoleSim/Tests/Editor/BlackHoleLensControllerTests.cs
git commit -m "feat: BlackHoleLensController 디스크/상대론/품질 파라미터에 프로퍼티 추가"
```

---

### Task 4: ParticleField 프로퍼티 + Reinitialize (Particles Model)

**Files:**
- Modify: `claude-unity-project/Assets/BlackHoleSim/Runtime/ParticleField.cs`
- Create: `claude-unity-project/Assets/BlackHoleSim/Tests/Editor/ParticleFieldTests.cs`

**Interfaces:**
- Produces: `ParticleField.Count`(`int`, setter가 `Reinitialize` 호출), `InnerRadius`, `OuterRadius`, `DiskThickness`, `SpeedJitter`, `MaxRadius`, `InfallSpeedFactor`, `ParticleSize`(모두 `float`), `void Reinitialize(int newCount)`.

- [ ] **Step 1: Write the failing test**

```csharp
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
```

- [ ] **Step 2: Run test to verify it fails**

MCP: `editor_invoke_method`(`EditModeTestRunner.RunAll`) → `editor_read_log`.
Expected: 컴파일 에러 — `Reinitialize`/`Count`/`InnerRadius` 등 멤버 없음.

- [ ] **Step 3: Write minimal implementation**

`ParticleField.cs`에서 `Awake()`를 교체하고 프로퍼티 + `Reinitialize`를 추가:

```csharp
        void Awake()
        {
            ps = GetComponent<ParticleSystem>();
            accelFn = blackHole.AccelerationAt;

            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;
            var emission = ps.emission;
            emission.enabled = false;

            EnsureRenderMaterial();

            Reinitialize(count);
        }

        public void Reinitialize(int newCount)
        {
            if (ps == null) ps = GetComponent<ParticleSystem>();
            count = Mathf.Max(1, newCount);

            var main = ps.main;
            main.maxParticles = count;

            pos = new Vector3[count];
            vel = new Vector3[count];
            rendered = new ParticleSystem.Particle[count];
            for (int i = 0; i < count; i++) Spawn(i);
        }

        public int Count { get => count; set => Reinitialize(value); }
        public float InnerRadius { get => innerRadius; set => innerRadius = value; }
        public float OuterRadius { get => outerRadius; set => outerRadius = value; }
        public float DiskThickness { get => diskThickness; set => diskThickness = value; }
        public float SpeedJitter { get => speedJitter; set => speedJitter = value; }
        public float MaxRadius { get => maxRadius; set => maxRadius = value; }
        public float InfallSpeedFactor { get => infallSpeedFactor; set => infallSpeedFactor = value; }
        public float ParticleSize { get => particleSize; set => particleSize = value; }
```

- [ ] **Step 4: Run test to verify it passes**

MCP: `editor_recompile` → `editor_wait_ready` → `editor_invoke_method`(`EditModeTestRunner.RunAll`) → `editor_read_log`.
Expected log: `failed=0`.

- [ ] **Step 5: Commit**

```bash
git add claude-unity-project/Assets/BlackHoleSim/Runtime/ParticleField.cs claude-unity-project/Assets/BlackHoleSim/Tests/Editor/ParticleFieldTests.cs
git commit -m "feat: ParticleField 파라미터 프로퍼티 + 런타임 Count 재초기화 지원"
```

---

### Task 5: BlackHoleParamsViewModel

**Files:**
- Create: `claude-unity-project/Assets/BlackHoleSim/Runtime/UI/ViewModels/BlackHoleParamsViewModel.cs`
- Create: `claude-unity-project/Assets/BlackHoleSim/Tests/Editor/UI/BlackHoleParamsViewModelTests.cs`

**Interfaces:**
- Consumes: Task 1의 `ObservableValue<T>`; Task 2/3/4에서 추가한 모든 프로퍼티.
- Produces: `BlackHoleSim.UI.BlackHoleParamsViewModel(BlackHole, BlackHoleLensController, ParticleField)` 생성자. public readonly 필드로 다음 `ObservableValue<T>` 25개 + 2개(패널 상태) 노출:
  `DiskInnerRadius, DiskOuterRadius, DiskThickness, DiskDensity, DiskTempInnerKelvin, DiskTempOuterKelvin, DiskColorTint, BeamingStrength, RedshiftStrength, PhotonRing, StepCount, StepSize, StarDensity, ParticlesEnabled, ParticleCount, ParticleInnerRadius, ParticleOuterRadius, ParticleDiskThickness, ParticleSpeedJitter, ParticleMaxRadius, ParticleInfallSpeedFactor, ParticleSize, GravitationalConstant, Mass, Softening, EventHorizonRadius, PanelExpanded, DangerZoneExpanded`.

- [ ] **Step 1: Write the failing test**

```csharp
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
```

- [ ] **Step 2: Run test to verify it fails**

MCP: `editor_invoke_method`(`EditModeTestRunner.RunAll`) → `editor_read_log`.
Expected: 컴파일 에러 — `BlackHoleParamsViewModel` 타입 없음.

- [ ] **Step 3: Write minimal implementation**

```csharp
using System;
using UnityEngine;

namespace BlackHoleSim.UI
{
    /// <summary>BlackHole/BlackHoleLensController/ParticleField 파라미터를 ObservableValue로 노출하는 ViewModel.
    /// 단방향 바인딩: ObservableValue 변경 → Model setter 즉시 반영.</summary>
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
```

- [ ] **Step 4: Run test to verify it passes**

MCP: `editor_recompile` → `editor_wait_ready` → `editor_invoke_method`(`EditModeTestRunner.RunAll`) → `editor_read_log`.
Expected log: `failed=0`.

- [ ] **Step 5: Commit**

```bash
git add claude-unity-project/Assets/BlackHoleSim/Runtime/UI/ViewModels/BlackHoleParamsViewModel.cs claude-unity-project/Assets/BlackHoleSim/Tests/Editor/UI/BlackHoleParamsViewModelTests.cs
git commit -m "feat: BlackHoleParamsViewModel 추가 (Model 25개 파라미터 ObservableValue 바인딩)"
```

---

### Task 6: TextMeshPro 설치

**Files:**
- Modify: `claude-unity-project/Packages/manifest.json` (TMP 패키지 추가 — Unity 패키지 매니저가 직접 기록)

**Interfaces:**
- Produces: `TMPro` 네임스페이스 사용 가능, `Assets/TextMesh Pro/Resources/` Essential Resources 존재.

- [ ] **Step 1: 패키지 설치**

MCP 도구 호출: `mcp__UnityMCP__editor_execute_menu`로 `Window/TextMeshPro/Import TMP Essential Resources` 메뉴를 실행한다(패키지가 없으면 먼저 Package Manager UI 경로 대신, `claude-unity-project/Packages/manifest.json`의 `dependencies`에 `"com.unity.ugui": "2.5.0"`이 이미 있어 TMP 런타임은 포함되어 있을 가능성이 높음 — 메뉴 실행으로 Essentials 임포트만 필요한 경우가 많다).

- [ ] **Step 2: 확인**

MCP: `editor_refresh_assets` → `editor_wait_ready`. 그 다음 `editor_invoke_method`로 임의 클래스에서 `typeof(TMPro.TMP_Text)`를 참조하는 더미 호출 대신, Task 7에서 `TMP_Text`를 사용하는 컴포넌트를 작성해 컴파일이 성공하면 간접 확인된다. 이 Task에서는 `Assets/TextMesh Pro/Resources` 폴더가 생성됐는지 확인:

```bash
ls "claude-unity-project/Assets/TextMesh Pro/Resources" 2>/dev/null && echo "TMP_OK"
```

Expected: `TMP_OK` 출력.

- [ ] **Step 3: Commit**

```bash
git add claude-unity-project/Packages/manifest.json "claude-unity-project/Assets/TextMesh Pro" claude-unity-project/Assets/TMP_Settings.asset 2>/dev/null
git commit -m "chore: TextMeshPro 패키지 + Essential Resources 설치"
```

(파일 목록은 실제 임포트 결과에 따라 달라질 수 있으므로, 커밋 전 `git status --short`로 실제 생성된 경로를 확인하고 그 경로들을 추가한다.)

---

### Task 7: CollapsibleSection View

**Files:**
- Create: `claude-unity-project/Assets/BlackHoleSim/Runtime/UI/Views/CollapsibleSection.cs`

**Interfaces:**
- Consumes: Task 1의 `ObservableValue<bool>`.
- Produces: `CollapsibleSection.Bind(ObservableValue<bool> expanded)`. Inspector 필드: `headerButton`(`Button`), `content`(`GameObject`).

이 View는 UGUI `Button`/`GameObject` 동작만 사용해 EditMode 테스트로 검증하기 적합하지 않다(씬 객체·클릭 이벤트 의존). Task 10의 수동 스크린샷 검증으로 확인한다. 코드는 단순하므로 TDD 없이 직접 구현하지만, 리뷰 시 컴파일 성공을 1차 검증으로 삼는다.

- [ ] **Step 1: Write the implementation**

```csharp
using UnityEngine;
using UnityEngine.UI;

namespace BlackHoleSim.UI
{
    public class CollapsibleSection : MonoBehaviour
    {
        [SerializeField] Button headerButton;
        [SerializeField] GameObject content;

        public void Bind(ObservableValue<bool> expanded)
        {
            content.SetActive(expanded.Value);
            expanded.Changed += content.SetActive;
            headerButton.onClick.AddListener(() => expanded.Value = !expanded.Value);
        }
    }
}
```

- [ ] **Step 2: Verify compilation**

MCP: `editor_recompile` → `editor_wait_ready` → `editor_read_log`.
Expected: 컴파일 에러 없음(로그에 `error CS` 문자열 없음).

- [ ] **Step 3: Commit**

```bash
git add claude-unity-project/Assets/BlackHoleSim/Runtime/UI/Views/CollapsibleSection.cs
git commit -m "feat: CollapsibleSection View 컴포넌트 추가"
```

---

### Task 8: Row View 컴포넌트 4종

**Files:**
- Create: `claude-unity-project/Assets/BlackHoleSim/Runtime/UI/Views/FloatSliderRow.cs`
- Create: `claude-unity-project/Assets/BlackHoleSim/Runtime/UI/Views/IntSliderRow.cs`
- Create: `claude-unity-project/Assets/BlackHoleSim/Runtime/UI/Views/ToggleRow.cs`
- Create: `claude-unity-project/Assets/BlackHoleSim/Runtime/UI/Views/ColorSwatchRow.cs`

**Interfaces:**
- Consumes: Task 1의 `ObservableValue<T>`.
- Produces: `FloatSliderRow.Bind(string label, ObservableValue<float> value, float min, float max)`, `IntSliderRow.Bind(string label, ObservableValue<int> value, int min, int max)`, `ToggleRow.Bind(string label, ObservableValue<bool> value)`, `ColorSwatchRow.Bind(string label, ObservableValue<Color> value)`.

- [ ] **Step 1: FloatSliderRow 구현**

```csharp
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlackHoleSim.UI
{
    public class FloatSliderRow : MonoBehaviour
    {
        [SerializeField] TMP_Text label;
        [SerializeField] TMP_Text valueText;
        [SerializeField] Slider slider;

        bool suppressFeedback;

        public void Bind(string labelText, ObservableValue<float> value, float min, float max)
        {
            label.text = labelText;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = false;

            void ApplyValue(float v)
            {
                suppressFeedback = true;
                slider.value = v;
                valueText.text = v.ToString("0.###");
                suppressFeedback = false;
            }

            ApplyValue(value.Value);
            value.Changed += ApplyValue;

            slider.onValueChanged.AddListener(v =>
            {
                if (suppressFeedback) return;
                value.Value = v;
            });
        }
    }
}
```

- [ ] **Step 2: IntSliderRow 구현**

```csharp
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlackHoleSim.UI
{
    public class IntSliderRow : MonoBehaviour
    {
        [SerializeField] TMP_Text label;
        [SerializeField] TMP_Text valueText;
        [SerializeField] Slider slider;

        bool suppressFeedback;

        public void Bind(string labelText, ObservableValue<int> value, int min, int max)
        {
            label.text = labelText;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = true;

            void ApplyValue(int v)
            {
                suppressFeedback = true;
                slider.value = v;
                valueText.text = v.ToString();
                suppressFeedback = false;
            }

            ApplyValue(value.Value);
            value.Changed += ApplyValue;

            slider.onValueChanged.AddListener(v =>
            {
                if (suppressFeedback) return;
                value.Value = Mathf.RoundToInt(v);
            });
        }
    }
}
```

- [ ] **Step 3: ToggleRow 구현**

```csharp
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlackHoleSim.UI
{
    public class ToggleRow : MonoBehaviour
    {
        [SerializeField] TMP_Text label;
        [SerializeField] Toggle toggle;

        bool suppressFeedback;

        public void Bind(string labelText, ObservableValue<bool> value)
        {
            label.text = labelText;

            void ApplyValue(bool v)
            {
                suppressFeedback = true;
                toggle.isOn = v;
                suppressFeedback = false;
            }

            ApplyValue(value.Value);
            value.Changed += ApplyValue;

            toggle.onValueChanged.AddListener(v =>
            {
                if (suppressFeedback) return;
                value.Value = v;
            });
        }
    }
}
```

- [ ] **Step 4: ColorSwatchRow 구현**

```csharp
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlackHoleSim.UI
{
    public class ColorSwatchRow : MonoBehaviour
    {
        [SerializeField] TMP_Text label;
        [SerializeField] Slider rSlider;
        [SerializeField] Slider gSlider;
        [SerializeField] Slider bSlider;
        [SerializeField] Image swatch;

        bool suppressFeedback;
        ObservableValue<Color> bound;

        public void Bind(string labelText, ObservableValue<Color> value)
        {
            label.text = labelText;
            bound = value;

            foreach (var s in new[] { rSlider, gSlider, bSlider })
            {
                s.minValue = 0f;
                s.maxValue = 1f;
            }

            ApplyValue(value.Value);
            value.Changed += ApplyValue;

            rSlider.onValueChanged.AddListener(_ => PushFromSliders());
            gSlider.onValueChanged.AddListener(_ => PushFromSliders());
            bSlider.onValueChanged.AddListener(_ => PushFromSliders());
        }

        void ApplyValue(Color c)
        {
            suppressFeedback = true;
            rSlider.value = c.r;
            gSlider.value = c.g;
            bSlider.value = c.b;
            swatch.color = c;
            suppressFeedback = false;
        }

        void PushFromSliders()
        {
            if (suppressFeedback) return;
            var c = new Color(rSlider.value, gSlider.value, bSlider.value, 1f);
            swatch.color = c;
            bound.Value = c;
        }
    }
}
```

- [ ] **Step 5: Verify compilation**

MCP: `editor_recompile` → `editor_wait_ready` → `editor_read_log`.
Expected: 컴파일 에러 없음.

- [ ] **Step 6: Commit**

```bash
git add claude-unity-project/Assets/BlackHoleSim/Runtime/UI/Views/FloatSliderRow.cs claude-unity-project/Assets/BlackHoleSim/Runtime/UI/Views/IntSliderRow.cs claude-unity-project/Assets/BlackHoleSim/Runtime/UI/Views/ToggleRow.cs claude-unity-project/Assets/BlackHoleSim/Runtime/UI/Views/ColorSwatchRow.cs
git commit -m "feat: Row View 컴포넌트 4종 추가 (Float/Int/Toggle/Color 바인딩)"
```

---

### Task 9: ParamPanelView (루트 View)

**Files:**
- Create: `claude-unity-project/Assets/BlackHoleSim/Runtime/UI/Views/ParamPanelView.cs`

**Interfaces:**
- Consumes: Task 5의 `BlackHoleParamsViewModel`; Task 7/8의 모든 Row/Section `Bind(...)` 시그니처.
- Produces: `ParamPanelView`(MonoBehaviour) — Inspector에 `blackHole`, `lens`, `particles`, `panelContent`, `headerCollapseButton`, 25개 Row/Section 참조 필드를 노출. Task 10의 씬 와이어링에서 이 필드들을 연결한다.

- [ ] **Step 1: 구현**

```csharp
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
```

- [ ] **Step 2: Verify compilation**

MCP: `editor_recompile` → `editor_wait_ready` → `editor_read_log`.
Expected: 컴파일 에러 없음.

- [ ] **Step 3: Commit**

```bash
git add claude-unity-project/Assets/BlackHoleSim/Runtime/UI/Views/ParamPanelView.cs
git commit -m "feat: ParamPanelView 루트 컴포넌트 추가 (Tab 토글 + 전체 Row 바인딩)"
```

---

### Task 10: 씬 와이어링 + 시각 검증

**Files:**
- Modify: `claude-unity-project/Assets/Scenes/SampleScene.unity` (Unity MCP로 직접 편집, diff는 Unity가 생성)
- Create(prefab, 선택): `claude-unity-project/Assets/BlackHoleSim/Prefabs/UI/*.prefab`

**Interfaces:**
- Consumes: Task 9의 `ParamPanelView` 및 그 25개 Inspector 참조 필드, Task 7/8의 4개 Row 컴포넌트, Task 6의 TMP.

이 Task는 코드가 아니라 라이브 Unity 에디터 상태를 MCP 도구로 직접 구성한다. 실행자는 `mcp__UnityMCP__*` 도구에 접근 가능해야 한다(서브에이전트로 분리할 경우 동일 도구 접근 권한 필요).

- [ ] **Step 1: Canvas/EventSystem 생성**

`mcp__UnityMCP__game_object_create`로 `Canvas`(컴포넌트: `Canvas` render mode=ScreenSpaceOverlay, `CanvasScaler`, `GraphicRaycaster`) 생성, `mcp__UnityMCP__game_object_create`로 `EventSystem`(컴포넌트: `EventSystem`, `InputSystemUIInputModule`) 생성.
Expected: `mcp__UnityMCP__scene_hierarchy`에 두 객체가 보임.

- [ ] **Step 2: ParamPanel 루트 + Header + ScrollView 생성**

Canvas 하위에 `ParamPanel`(RectTransform, anchor 우측), 그 하위에 `Header`(`TMP_Text` + 접기 `Button`), `ScrollView`(`ScrollRect` + `Content`에 `VerticalLayoutGroup`)를 `game_object_create` + `component_add` + `component_set`으로 구성.
Expected: `scene_hierarchy`로 구조 확인, `screenshot_game`으로 패널이 우측에 보임.

- [ ] **Step 3: 5개 CollapsibleSection + Row 생성**

각 섹션(`Disk`, `Relativity`, `Render Quality`, `Particles`, `Danger Zone`)을 `ScrollView/Content` 하위에 생성하고, 섹션마다 필요한 Row 프리팹(`FloatSliderRow`/`IntSliderRow`/`ToggleRow`/`ColorSwatchRow`)을 인스턴스화한다. Danger Zone 섹션의 `Image` 컴포넌트 색을 `material_set_color` 또는 `component_set`으로 빨간 계열(예: `(0.6, 0.15, 0.15, 0.6)`)로 설정.
Expected: `screenshot_game`에서 5개 섹션 헤더와 그 안의 슬라이더들이 보임.

- [ ] **Step 4: ParamPanelView 와이어링**

`ParamPanel` 루트에 `ParamPanelView` 컴포넌트를 추가하고, `component_set`으로 `blackHole`/`lens`/`particles`(씬의 기존 `BlackHole`/`BlackHoleLensController`/`ParticleField` 인스턴스)와 25개 Row/Section 참조를 모두 연결한다.
Expected: `mcp__UnityMCP__editor_get_status`에 컴파일/와이어링 에러 없음.

- [ ] **Step 5: Play 모드 검증 — 패널 표시 및 조작**

`mcp__UnityMCP__sim_play` → `mcp__UnityMCP__screenshot_game`으로 패널이 펼쳐진 상태 확인 → Tab 키 입력을 시뮬레이션할 수 없다면 헤더 버튼 클릭으로 대체 검증(예: `editor_invoke_method`로 디버그 헬퍼 호출, 또는 `ParamPanelView`에 임시 디버그 메서드 없이 수동 확인) → `mcp__UnityMCP__screenshot_game`으로 접힌 상태 확인.
Expected: 두 스크린샷에서 패널이 펼침/접힘으로 다르게 보임.

- [ ] **Step 6: Play 모드 검증 — 디스크 색조 슬라이더 조작**

Play 상태에서 디스크 색조 R 슬라이더 값을 변경(에디터 UI 클릭은 MCP로 직접 드래그 불가하므로, `component_set`으로 슬라이더의 `value`를 변경해 `onValueChanged` 콜백을 트리거하거나, 와이어링 검증 목적상 `vm.DiskColorTint.Value`를 변경하는 디버그 경로로 확인) → `screenshot_game`으로 강착원반 색이 즉시 바뀌는지 확인.
Expected: 스크린샷에서 디스크 색이 슬라이더 조작 전과 다르게 렌더링됨.

- [ ] **Step 7: 시뮬레이션 정지 및 EditMode 테스트 최종 확인**

`mcp__UnityMCP__sim_stop` → `editor_invoke_method`(`EditModeTestRunner.RunAll`) → `editor_read_log`.
Expected log: `failed=0` (Task 1~5에서 추가한 모든 테스트 포함).

- [ ] **Step 8: README 갱신**

`README.md`의 파라미터 표 섹션 위에 "런타임 UI 패널" 절을 추가해 Tab 토글, 5개 섹션, DangerZone 설명을 1~2문단으로 정리한다. 스크린샷도 `docs/images/blackhole-ui-panel.png`로 추가.

- [ ] **Step 9: Commit & Push**

```bash
git add claude-unity-project/Assets/Scenes/SampleScene.unity claude-unity-project/Assets/BlackHoleSim/Prefabs README.md docs/images/blackhole-ui-panel.png
git commit -m "feat: 블랙홀 파라미터 런타임 UI 패널 씬 와이어링 + 검증"
git push
```

---

## Self-Review 결과

- **스펙 커버리지**: 5개 섹션 전부(Task 9 와이어링 목록), DangerZone 경고 테두리(Task 10 Step 3), Tab+헤더 토글(Task 9 Update + Task 10 Step 5), RGB 슬라이더+스와치(Task 8 Step 4), `count` 런타임 재초기화(Task 4), 세션 간 저장 안 함(Task 9에서 `PanelExpanded`/모델 모두 매 Awake 초기화, PlayerPrefs 미사용) — 모두 커버됨.
- **플레이스홀더 스캔**: 없음. 모든 코드 블록이 완전한 구현.
- **타입 일관성**: `ObservableValue<T>`(Task1) → `BlackHoleParamsViewModel`(Task5) → Row `Bind` 시그니처(Task7/8) → `ParamPanelView`(Task9) 전체에서 `ObservableValue<float>/<int>/<bool>/<Color>` 타입과 프로퍼티 이름(`DiskInnerRadius` 등)이 일치함을 확인.

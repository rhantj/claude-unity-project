# 블랙홀 동역학 시뮬레이터 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 단일 소스 중력장 안에서 입자가 공전·나선 낙하해 이벤트 호라이즌에 흡수되고, 사용자가 물체를 던질 수 있는 Unity 동역학 시뮬레이터(1단계)를 만든다.

**Architecture:** 순수 함수 물리 코어(`GravityField` + `GravityIntegrator`)를 두고, 그 위에 `BlackHole`(중력장 단일 진실원), `ParticleField`(수천 입자), `ThrowableBody`(개별 물체), `SimController`(입력·카메라)가 동일 코어를 공유한다. 렌더는 `ParticleSystem.SetParticles`로 CPU 적분 결과를 푸시한다.

**Tech Stack:** Unity 6 (6000.5.0f1), URP 17.5.0, C#, Unity Test Framework (EditMode), Unity MCP(편집·검증). 추가 패키지 없음.

## Global Constraints

- Unity 에디터 버전: 6000.5.0f1. 추가 패키지 설치 금지(기존 manifest만 사용).
- 네임스페이스: 모든 런타임 코드는 `BlackHoleSim`, 테스트는 `BlackHoleSim.Tests`.
- 물리는 Unity Rigidbody/PhysX 미사용 — 커스텀 적분만 사용.
- 중력: 단일 소스. `a = -G·M · r / (r² + ε²)^(3/2)` (r은 입자→블랙홀 방향). 입자끼리 인력 없음.
- 적분: Velocity Verlet. 적분은 `FixedUpdate`(고정 timestep)에서만.
- 스크립트 경로 루트: `claude-unity-project/Assets/BlackHoleSim/`.
- 코드 변경 후 검증 루프: `editor_refresh_assets` → `editor_recompile` → `editor_read_log`로 컴파일 0-에러 확인. 동작 검증은 `sim_play` + `screenshot_game`.

---

### Task 1: 물리 코어 (GravityField + GravityIntegrator) + 단위 테스트

순수 함수 코어. UnityEngine.Vector3만 의존하고 MonoBehaviour 비의존이라 EditMode 테스트로 검증.

**Files:**
- Create: `claude-unity-project/Assets/BlackHoleSim/Runtime/BlackHoleSim.Runtime.asmdef`
- Create: `claude-unity-project/Assets/BlackHoleSim/Runtime/GravityField.cs`
- Create: `claude-unity-project/Assets/BlackHoleSim/Runtime/GravityIntegrator.cs`
- Create: `claude-unity-project/Assets/BlackHoleSim/Tests/Editor/BlackHoleSim.Tests.asmdef`
- Test: `claude-unity-project/Assets/BlackHoleSim/Tests/Editor/GravityCoreTests.cs`

**Interfaces:**
- Produces:
  - `static Vector3 GravityField.AccelerationAt(Vector3 source, float mu, float softening, Vector3 pos)` — pos에 작용하는 가속도(source 방향).
  - `static float GravityField.OrbitalSpeed(float mu, float radius)` — `√(mu/radius)`.
  - `static void GravityIntegrator.Step(ref Vector3 pos, ref Vector3 vel, System.Func<Vector3,Vector3> accel, float dt)` — Velocity Verlet 한 스텝.

- [ ] **Step 1: Runtime asmdef 생성**

`claude-unity-project/Assets/BlackHoleSim/Runtime/BlackHoleSim.Runtime.asmdef`:
```json
{
    "name": "BlackHoleSim.Runtime",
    "rootNamespace": "BlackHoleSim",
    "references": [],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "autoReferenced": true
}
```

- [ ] **Step 2: Tests asmdef 생성**

`claude-unity-project/Assets/BlackHoleSim/Tests/Editor/BlackHoleSim.Tests.asmdef`:
```json
{
    "name": "BlackHoleSim.Tests",
    "rootNamespace": "BlackHoleSim.Tests",
    "references": ["BlackHoleSim.Runtime", "UnityEngine.TestRunner", "UnityEditor.TestRunner"],
    "includePlatforms": ["Editor"],
    "excludePlatforms": [],
    "precompiledReferences": ["nunit.framework.dll"],
    "defineConstraints": ["UNITY_INCLUDE_TESTS"],
    "autoReferenced": false
}
```

- [ ] **Step 3: 실패하는 테스트 작성**

`claude-unity-project/Assets/BlackHoleSim/Tests/Editor/GravityCoreTests.cs`:
```csharp
using NUnit.Framework;
using UnityEngine;
using BlackHoleSim;

namespace BlackHoleSim.Tests
{
    public class GravityCoreTests
    {
        [Test]
        public void AccelerationAt_PointsTowardSource_WithInverseSquareMagnitude()
        {
            // source at origin, sample at (5,0,0): accel should point -x with mag mu/r^2
            Vector3 a = GravityField.AccelerationAt(Vector3.zero, 100f, 0f, new Vector3(5f, 0f, 0f));

            Assert.That(a.x, Is.LessThan(0f), "accel should point toward source (-x)");
            Assert.That(a.y, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(a.z, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(a.magnitude, Is.EqualTo(100f / 25f).Within(1e-3f)); // mu/r^2 = 4
        }

        [Test]
        public void Softening_PreventsSingularityAtCenter()
        {
            Vector3 a = GravityField.AccelerationAt(Vector3.zero, 100f, 0.5f, Vector3.zero);
            Assert.That(float.IsFinite(a.x) && float.IsFinite(a.y) && float.IsFinite(a.z), Is.True);
            Assert.That(a.magnitude, Is.EqualTo(0f).Within(1e-4f)); // at center r=0 -> zero direction
        }

        [Test]
        public void OrbitalSpeed_IsSqrtMuOverRadius()
        {
            Assert.That(GravityField.OrbitalSpeed(100f, 10f), Is.EqualTo(Mathf.Sqrt(10f)).Within(1e-4f));
        }

        [Test]
        public void Verlet_CircularOrbit_ConservesRadius_OverOnePeriod()
        {
            float mu = 100f, soft = 0f, r = 10f, dt = 0.01f;
            Vector3 source = Vector3.zero;
            Vector3 pos = new Vector3(r, 0f, 0f);
            float v = GravityField.OrbitalSpeed(mu, r);
            Vector3 vel = new Vector3(0f, 0f, v); // tangential in XZ plane

            System.Func<Vector3, Vector3> accel = p => GravityField.AccelerationAt(source, mu, soft, p);

            // Period T = 2*pi*r/v ; steps = T/dt
            int steps = Mathf.RoundToInt((2f * Mathf.PI * r / v) / dt);
            for (int i = 0; i < steps; i++)
                GravityIntegrator.Step(ref pos, ref vel, accel, dt);

            float finalR = (pos - source).magnitude;
            Assert.That(finalR, Is.EqualTo(r).Within(0.3f), "radius should be conserved for a circular orbit");
        }
    }
}
```

- [ ] **Step 4: 테스트 실패 확인**

`editor_refresh_assets` → `editor_recompile` → `editor_read_log`.
Expected: `GravityField`/`GravityIntegrator` 미정의로 컴파일 에러. (또는 Test Runner: `Window > General > Test Runner`에서 EditMode 빨강)

- [ ] **Step 5: GravityField 구현**

`claude-unity-project/Assets/BlackHoleSim/Runtime/GravityField.cs`:
```csharp
using UnityEngine;

namespace BlackHoleSim
{
    /// <summary>Single-source softened Newtonian gravity. Pure functions, no scene state.</summary>
    public static class GravityField
    {
        public static Vector3 AccelerationAt(Vector3 source, float mu, float softening, Vector3 pos)
        {
            Vector3 r = source - pos;                 // toward the source
            float dist2 = r.sqrMagnitude + softening * softening;
            if (dist2 <= Mathf.Epsilon) return Vector3.zero;
            float invDist = 1f / Mathf.Sqrt(dist2);
            float invDist3 = invDist / dist2;         // 1 / (r^2+e^2)^(3/2)
            return mu * invDist3 * r;
        }

        public static float OrbitalSpeed(float mu, float radius)
        {
            return radius <= 0f ? 0f : Mathf.Sqrt(mu / radius);
        }
    }
}
```

- [ ] **Step 6: GravityIntegrator 구현**

`claude-unity-project/Assets/BlackHoleSim/Runtime/GravityIntegrator.cs`:
```csharp
using UnityEngine;

namespace BlackHoleSim
{
    /// <summary>Velocity Verlet single step. Energy-stable for orbital motion.</summary>
    public static class GravityIntegrator
    {
        public static void Step(ref Vector3 pos, ref Vector3 vel, System.Func<Vector3, Vector3> accel, float dt)
        {
            Vector3 a0 = accel(pos);
            pos += vel * dt + 0.5f * a0 * dt * dt;
            Vector3 a1 = accel(pos);
            vel += 0.5f * (a0 + a1) * dt;
        }
    }
}
```

- [ ] **Step 7: 테스트 통과 확인**

`editor_refresh_assets` → `editor_recompile` → `editor_read_log` (0 에러).
Test Runner(`Window > General > Test Runner`) EditMode 실행 → 4개 테스트 green. (MCP에서는 `editor_execute_menu`로 Test Runner 열고 `screenshot_editor`로 결과 확인.)

- [ ] **Step 8: 커밋**

```bash
git add claude-unity-project/Assets/BlackHoleSim/Runtime/ claude-unity-project/Assets/BlackHoleSim/Tests/
git commit -m "feat: 중력 코어(GravityField/GravityIntegrator) + EditMode 테스트"
```

---

### Task 2: BlackHole 컴포넌트 + 씬 placeholder

중력장 단일 진실원. 코어 함수를 인스펙터 파라미터로 래핑.

**Files:**
- Create: `claude-unity-project/Assets/BlackHoleSim/Runtime/BlackHole.cs`
- Test: 기존 `GravityCoreTests.cs`에 케이스 추가

**Interfaces:**
- Consumes: `GravityField.AccelerationAt`, `GravityField.OrbitalSpeed`.
- Produces (다음 태스크가 의존):
  - `float BlackHole.Mu { get; }` — `gravitationalConstant * mass`
  - `float BlackHole.EventHorizonRadius { get; }`
  - `Vector3 BlackHole.AccelerationAt(Vector3 pos)`
  - `float BlackHole.OrbitalSpeed(float radius)`
  - `bool BlackHole.IsCaptured(Vector3 pos)`

- [ ] **Step 1: 실패하는 테스트 추가**

`GravityCoreTests.cs`에 추가:
```csharp
        [Test]
        public void BlackHole_IsCaptured_TrueInsideHorizon()
        {
            var go = new GameObject("bh");
            go.transform.position = Vector3.zero;
            var bh = go.AddComponent<BlackHole>();
            bh.Configure(gravitationalConstant: 1f, mass: 100f, softening: 0.5f, eventHorizonRadius: 2f);

            Assert.That(bh.IsCaptured(new Vector3(1f, 0f, 0f)), Is.True);
            Assert.That(bh.IsCaptured(new Vector3(5f, 0f, 0f)), Is.False);
            Assert.That(bh.Mu, Is.EqualTo(100f).Within(1e-4f));

            Object.DestroyImmediate(go);
        }
```

- [ ] **Step 2: 테스트 실패 확인**

`editor_refresh_assets` → `editor_recompile` → `editor_read_log`.
Expected: `BlackHole` 미정의 컴파일 에러.

- [ ] **Step 3: BlackHole 구현**

`claude-unity-project/Assets/BlackHoleSim/Runtime/BlackHole.cs`:
```csharp
using UnityEngine;

namespace BlackHoleSim
{
    /// <summary>Single gravity source. The one place mass/G/horizon live.</summary>
    public class BlackHole : MonoBehaviour
    {
        [SerializeField] float gravitationalConstant = 1f;
        [SerializeField] float mass = 4000f;
        [SerializeField] float softening = 0.5f;
        [SerializeField] float eventHorizonRadius = 2f;

        public float Mu => gravitationalConstant * mass;
        public float EventHorizonRadius => eventHorizonRadius;

        public Vector3 AccelerationAt(Vector3 pos) =>
            GravityField.AccelerationAt(transform.position, Mu, softening, pos);

        public float OrbitalSpeed(float radius) => GravityField.OrbitalSpeed(Mu, radius);

        public bool IsCaptured(Vector3 pos)
        {
            float h = eventHorizonRadius;
            return (pos - transform.position).sqrMagnitude <= h * h;
        }

        // Test/editor hook for setting tunables programmatically.
        public void Configure(float gravitationalConstant, float mass, float softening, float eventHorizonRadius)
        {
            this.gravitationalConstant = gravitationalConstant;
            this.mass = mass;
            this.softening = softening;
            this.eventHorizonRadius = eventHorizonRadius;
        }
    }
}
```

- [ ] **Step 4: 테스트 통과 확인**

`editor_recompile` → `editor_read_log` (0 에러). Test Runner EditMode 5개 green.

- [ ] **Step 5: 씬에 블랙홀 placeholder 생성 (MCP)**

1. `scene_new` 또는 기존 SampleScene 사용 → `editor_open_scene`.
2. `game_object_create_primitive` (Sphere), 이름 `BlackHole`, 위치 `(0,0,0)`, scale `(2,2,2)`.
3. `material_set_color` BlackHole 렌더러 → 검정 `(0,0,0,1)`.
4. `physics_remove_colliders` BlackHole (PhysX 미사용).
5. `component_add` BlackHole에 `BlackHoleSim.BlackHole` 추가.
6. `editor_save_scene`.

- [ ] **Step 6: 커밋**

```bash
git add claude-unity-project/Assets/
git commit -m "feat: BlackHole 컴포넌트 + 씬 placeholder"
```

---

### Task 3: ParticleField (수천 입자 공전·흡수) + 동작 검증

**Files:**
- Create: `claude-unity-project/Assets/BlackHoleSim/Runtime/ParticleField.cs`

**Interfaces:**
- Consumes: `BlackHole.AccelerationAt`, `BlackHole.OrbitalSpeed`, `BlackHole.IsCaptured`, `GravityIntegrator.Step`.
- Produces: 씬에서 동작하는 입자 원반 (다음 태스크는 코드 의존 없음).

- [ ] **Step 1: ParticleField 구현**

`claude-unity-project/Assets/BlackHoleSim/Runtime/ParticleField.cs`:
```csharp
using UnityEngine;

namespace BlackHoleSim
{
    /// <summary>CPU-integrated test particles rendered through a ParticleSystem.</summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class ParticleField : MonoBehaviour
    {
        [SerializeField] BlackHole blackHole;
        [SerializeField] int count = 1500;
        [SerializeField] float innerRadius = 5f;
        [SerializeField] float outerRadius = 14f;
        [SerializeField] float diskThickness = 0.4f;
        [SerializeField, Range(0f, 0.5f)] float speedJitter = 0.12f;
        [SerializeField] float maxRadius = 45f;
        [SerializeField] float particleSize = 0.18f;

        ParticleSystem ps;
        ParticleSystem.Particle[] rendered;
        Vector3[] pos;
        Vector3[] vel;
        System.Func<Vector3, Vector3> accelFn;

        void Awake()
        {
            ps = GetComponent<ParticleSystem>();
            accelFn = blackHole.AccelerationAt;

            var main = ps.main;
            main.maxParticles = count;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;
            var emission = ps.emission;
            emission.enabled = false;

            pos = new Vector3[count];
            vel = new Vector3[count];
            rendered = new ParticleSystem.Particle[count];
            for (int i = 0; i < count; i++) Spawn(i);
        }

        void Spawn(int i)
        {
            float ang = Random.value * Mathf.PI * 2f;
            float rad = Mathf.Lerp(innerRadius, outerRadius, Random.value);
            Vector3 bh = blackHole.transform.position;
            Vector3 p = bh + new Vector3(
                Mathf.Cos(ang) * rad,
                (Random.value - 0.5f) * diskThickness,
                Mathf.Sin(ang) * rad);

            Vector3 radial = (p - bh).normalized;
            Vector3 tangent = Vector3.Cross(Vector3.up, radial).normalized;
            float speed = blackHole.OrbitalSpeed(rad) * (1f + (Random.value - 0.5f) * 2f * speedJitter);

            pos[i] = p;
            vel[i] = tangent * speed;
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            Vector3 bh = blackHole.transform.position;
            float maxSq = maxRadius * maxRadius;
            for (int i = 0; i < count; i++)
            {
                Vector3 p = pos[i], v = vel[i];
                GravityIntegrator.Step(ref p, ref v, accelFn, dt);
                pos[i] = p; vel[i] = v;

                if (blackHole.IsCaptured(p) || (p - bh).sqrMagnitude > maxSq)
                    Spawn(i);
            }
        }

        void LateUpdate()
        {
            for (int i = 0; i < count; i++)
            {
                rendered[i].position = pos[i];
                rendered[i].startSize = particleSize;
                rendered[i].startColor = Color.white;
                rendered[i].startLifetime = 1f;
                rendered[i].remainingLifetime = 1f;
            }
            ps.SetParticles(rendered, count);
        }
    }
}
```

- [ ] **Step 2: 컴파일 확인**

`editor_refresh_assets` → `editor_recompile` → `editor_read_log` (0 에러).

- [ ] **Step 3: 씬 배선 (MCP)**

1. `game_object_create` 이름 `ParticleField`, 위치 `(0,0,0)`.
2. `component_add` `UnityEngine.ParticleSystem` (RequireComponent로 자동 추가될 수 있음).
3. `component_add` `BlackHoleSim.ParticleField`.
4. `component_set` ParticleField의 `blackHole` 필드 → 씬의 `BlackHole` 오브젝트 참조.
5. 카메라: `editor_set_camera` 또는 Main Camera 위치를 `(0,18,-28)`, `BlackHole`을 바라보게 (`transform_look_at`).
6. `editor_save_scene`.

- [ ] **Step 4: 동작 검증 (PlayMode 스크린샷)**

1. `sim_play`.
2. 0.5초 간격으로 `screenshot_game` 2~3장: 입자 원반이 공전하며 안쪽이 더 빨리 도는지, 중심으로 나선 낙하 후 재생되는지 확인.
3. `sim_stop`.
Expected: 블랙홀 주위에 회전하는 입자 원반, 호라이즌 근처 입자가 사라졌다 재생.

- [ ] **Step 5: 커밋**

```bash
git add claude-unity-project/Assets/
git commit -m "feat: ParticleField 입자 원반 시뮬레이션"
```

---

### Task 4: SimController — 카메라 궤도 + 물체 던지기(ThrowableBody)

**Files:**
- Create: `claude-unity-project/Assets/BlackHoleSim/Runtime/ThrowableBody.cs`
- Create: `claude-unity-project/Assets/BlackHoleSim/Runtime/SimController.cs`

**Interfaces:**
- Consumes: `BlackHole`, `GravityIntegrator.Step`.
- Produces:
  - `void ThrowableBody.Launch(BlackHole bh, Vector3 position, Vector3 velocity, float maxRadius)`
  - `SimController` (씬 입력 진입점).

- [ ] **Step 1: ThrowableBody 구현**

`claude-unity-project/Assets/BlackHoleSim/Runtime/ThrowableBody.cs`:
```csharp
using UnityEngine;

namespace BlackHoleSim
{
    /// <summary>A single body that shares the gravity core, with a LineRenderer trail.</summary>
    [RequireComponent(typeof(LineRenderer))]
    public class ThrowableBody : MonoBehaviour
    {
        BlackHole blackHole;
        Vector3 vel;
        float maxRadius;
        System.Func<Vector3, Vector3> accelFn;

        LineRenderer trail;
        Vector3[] trailPts;
        int trailCount;
        const int TrailLength = 256;

        public void Launch(BlackHole bh, Vector3 position, Vector3 velocity, float maxRadius)
        {
            blackHole = bh;
            transform.position = position;
            vel = velocity;
            this.maxRadius = maxRadius;
            accelFn = blackHole.AccelerationAt;

            trail = GetComponent<LineRenderer>();
            trail.positionCount = 0;
            trailPts = new Vector3[TrailLength];
            trailCount = 0;
        }

        void FixedUpdate()
        {
            if (blackHole == null) return;

            Vector3 p = transform.position;
            GravityIntegrator.Step(ref p, ref vel, accelFn, Time.fixedDeltaTime);
            transform.position = p;
            PushTrail(p);

            float d = (p - blackHole.transform.position).magnitude;
            if (blackHole.IsCaptured(p) || d > maxRadius)
                Destroy(gameObject);
        }

        void PushTrail(Vector3 p)
        {
            if (trailCount < TrailLength)
            {
                trailPts[trailCount++] = p;
            }
            else
            {
                System.Array.Copy(trailPts, 1, trailPts, 0, TrailLength - 1);
                trailPts[TrailLength - 1] = p;
            }
            trail.positionCount = trailCount;
            trail.SetPositions(trailPts);
        }
    }
}
```

- [ ] **Step 2: SimController 구현**

`claude-unity-project/Assets/BlackHoleSim/Runtime/SimController.cs`:
```csharp
using UnityEngine;

namespace BlackHoleSim
{
    /// <summary>Input + camera orbit. Left-drag aims a throw; release launches a body.</summary>
    public class SimController : MonoBehaviour
    {
        [SerializeField] BlackHole blackHole;
        [SerializeField] Camera cam;
        [SerializeField] GameObject throwablePrefab;
        [SerializeField] float spawnDistance = 24f;
        [SerializeField] float throwSpeedScale = 0.05f;
        [SerializeField] float bodyMaxRadius = 80f;

        [Header("Camera orbit")]
        [SerializeField] float orbitSpeed = 120f;
        [SerializeField] float zoomSpeed = 12f;

        Vector3 dragStart;
        bool dragging;

        void Awake()
        {
            if (cam == null) cam = Camera.main;
        }

        void Update()
        {
            HandleThrow();
            HandleCameraOrbit();
        }

        void HandleThrow()
        {
            if (Input.GetMouseButtonDown(0))
            {
                dragStart = Input.mousePosition;
                dragging = true;
            }
            else if (Input.GetMouseButtonUp(0) && dragging)
            {
                dragging = false;
                Vector3 dragVec = Input.mousePosition - dragStart;

                Vector3 spawnPos = cam.transform.position + cam.transform.forward * spawnDistance;
                // Drag direction in screen space -> world velocity on the camera plane.
                Vector3 worldDir =
                    cam.transform.right * dragVec.x + cam.transform.up * dragVec.y;
                Vector3 velocity = worldDir * throwSpeedScale;

                var go = Instantiate(throwablePrefab, spawnPos, Quaternion.identity);
                var body = go.GetComponent<ThrowableBody>();
                body.Launch(blackHole, spawnPos, velocity, bodyMaxRadius);
            }
        }

        void HandleCameraOrbit()
        {
            Vector3 pivot = blackHole.transform.position;
            if (Input.GetMouseButton(1))
            {
                float yaw = Input.GetAxis("Mouse X") * orbitSpeed * Time.deltaTime;
                float pitch = -Input.GetAxis("Mouse Y") * orbitSpeed * Time.deltaTime;
                cam.transform.RotateAround(pivot, Vector3.up, yaw);
                cam.transform.RotateAround(pivot, cam.transform.right, pitch);
            }
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.0001f)
            {
                Vector3 dir = (cam.transform.position - pivot).normalized;
                cam.transform.position += dir * (-scroll * zoomSpeed);
            }
        }
    }
}
```

> 입력 노트: 프로젝트에 Input System 패키지가 설치돼 있다. Project Settings > Player > Active Input Handling이 "Both" 또는 "Input Manager (Old)"여야 위 `Input.*`(레거시)가 동작한다. `editor` 검증 시 입력이 안 먹으면 `ProjectSettings`에서 Active Input Handling을 Both로 설정.

- [ ] **Step 3: 컴파일 확인**

`editor_refresh_assets` → `editor_recompile` → `editor_read_log` (0 에러).

- [ ] **Step 4: Throwable 프리팹 생성 (MCP)**

1. `game_object_create_primitive` (Sphere) 이름 `Throwable`, scale `(0.6,0.6,0.6)`.
2. `physics_remove_colliders` Throwable.
3. `material_set_color` 밝은 색 (예: `(1, 0.8, 0.3, 1)`).
4. `component_add` `UnityEngine.LineRenderer`; `component_set`로 `startWidth`/`endWidth` `0.08`, `useWorldSpace` true, `numCapVertices` 2.
5. `component_add` `BlackHoleSim.ThrowableBody`.
6. 프리팹화: `Throwable`을 `claude-unity-project/Assets/BlackHoleSim/Prefabs/Throwable.prefab`로 저장(`prefab_*` 도구), 씬 인스턴스 삭제.

- [ ] **Step 5: SimController 배선 (MCP)**

1. `game_object_create` 이름 `SimController`.
2. `component_add` `BlackHoleSim.SimController`.
3. `component_set`: `blackHole`→씬 BlackHole, `cam`→Main Camera, `throwablePrefab`→Throwable.prefab.
4. `editor_save_scene`.

- [ ] **Step 6: 동작 검증 (PlayMode)**

1. `sim_play`.
2. `screenshot_game`로 입자 원반 + 카메라 시점 확인.
3. (가능하면) 마우스 드래그로 물체 던지기 → 궤적(LineRenderer)이 휘며 공전/낙하하는지 `screenshot_game`로 확인. 직접 입력이 불가하면 `editor_invoke_method`로 `SimController` 테스트용 throw를 호출하거나, 던지기 검증은 수동 확인으로 표시.
4. `sim_stop`.

- [ ] **Step 7: 커밋**

```bash
git add claude-unity-project/Assets/
git commit -m "feat: SimController 카메라 궤도 + 물체 던지기(ThrowableBody)"
```

---

## Self-Review 결과

- **Spec coverage:** 물리 코어(Task1), BlackHole/씬(Task2), ParticleField/방출·흡수(Task3), 던지기·카메라(Task4), 테스트(Task1-2 EditMode + Task3-4 PlayMode), 튜닝 파라미터(각 컴포넌트 SerializeField) 모두 매핑됨. 2단계 로드맵은 의도적으로 범위 밖.
- **Placeholder scan:** 모든 코드 스텝에 실제 코드 포함. 던지기 입력의 MCP 검증 한계는 명시적으로 대안(수동/`editor_invoke_method`) 기재 — TBD 없음.
- **Type consistency:** `AccelerationAt`/`OrbitalSpeed`/`IsCaptured`/`Mu`/`Step`/`Launch` 시그니처가 Task 간 일치. `accelFn`(`System.Func<Vector3,Vector3>`) 타입 통일.

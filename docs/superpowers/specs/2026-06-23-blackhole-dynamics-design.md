# 블랙홀 동역학 시뮬레이터 — 설계 (1단계)

- 작성일: 2026-06-23
- 엔진: Unity 6 (6000.5.0f1), URP 17.5.0
- 범위: **1단계 = 동역학(물리) 중심 상호작용**. 비주얼(렌즈/강착원반/포스트프로세싱)은 2단계 로드맵.

## 목표

블랙홀 중력장 안에서 입자/물체가 공전하고 나선 낙하해 이벤트 호라이즌에 흡수되는 과정을 실시간으로 시뮬레이션하고, 사용자가 물체를 던지거나 입자 흐름을 관찰할 수 있게 한다.

핵심 통찰: 입자 흐름과 "물체 던지기"는 **동일한 물리 코어**(중력장 + 적분기)를 공유한다. 코어 하나를 잘 만들면 두 상호작용이 모두 따라온다.

## 결정 사항

| 항목 | 결정 |
|---|---|
| 핵심 | 물리 + 비주얼 모두 목표, **동역학 먼저** |
| 중력 모델 | **단일 소스** — 블랙홀만 인력원, 입자는 test particle(서로 무시). O(n) |
| 구현 기술 | **CPU + Unity ParticleSystem**, ~1~2천 입자. 코어 수식 동일하므로 추후 Jobs/Burst·Compute로 승급 가능 |
| 적분기 | Velocity Verlet (궤도 에너지 보존) |

## 물리 코어

- 가속도: `a = -G·M · r / (r² + ε²)^(3/2)`
  - `ε` 소프트닝으로 중심 특이점(r→0 발산) 방지.
- 적분: Velocity Verlet — 순수 함수로 분리하여 단위 테스트 가능.
- 이벤트 호라이즌: 반경 `R_h` 안에 들어온 입자는 흡수. 기본 동작은 **재활용(emitter에서 리스폰)** 으로 입자 수를 일정하게 유지(개별 물체 `ThrowableBody`는 흡수 시 제거).
- Unity Rigidbody/PhysX 미사용. 커스텀 적분으로 입자·개별물체가 같은 코어 공유.

## 컴포넌트 (작은 단위로 분리)

| 컴포넌트 | 역할 | 의존 |
|---|---|---|
| `BlackHole` | M, G, ε, R_h 보유. `Vector3 AccelerationAt(pos)` 제공 — 중력장 단일 진실원 | 없음 |
| `GravityIntegrator` | `(pos, vel, accelFn, dt) → (pos, vel)`. 순수 정적 함수 | 없음 |
| `ParticleField` | 위치·속도 배열 소유, FixedUpdate마다 전체 적분 → `ParticleSystem.SetParticles`. 방출/호라이즌 재활용 | BlackHole, Integrator |
| `ThrowableBody` | 개별 물체. 같은 코어로 적분 + LineRenderer 궤적 | BlackHole, Integrator |
| `SimController` | 마우스 생성+던지기(드래그=초기속도), 카메라 궤도, 타임스케일 | 위 전부 |

각 단위는 단일 책임 / 명확한 인터페이스 / 독립 테스트 가능을 만족한다.

## 데이터 흐름

`FixedUpdate` → `ParticleField`가 각 입자에 대해: BlackHole에서 가속도 읽기 → Verlet 적분 → 호라이즌/탈출 검사 → 배열을 ParticleSystem에 push. `ThrowableBody`도 동일 + 궤적. 카메라/입력은 `Update`.

## 씬 구성 (MCP로 생성)

- 원점에 검은 구(블랙홀 시각 placeholder) + `BlackHole` 컴포넌트
- `ParticleField` 오브젝트 + ParticleSystem
- 디스크 형태 방출 + 접선 속도 `≈ √(GM/r)` + 약간 분산 → 소용돌이/원반 자연 형성, 천천히 나선 낙하 (2단계 강착원반으로 연결)
- 메인 카메라 궤도 컨트롤러, 어두운 우주 배경

## 엣지 케이스

- 중심 발산 → 소프트닝 ε + 호라이즌 캡처
- 무한 탈출 → `maxRadius` 밖 입자 컬링/재활용
- 적분 안정성 → 고정 timestep, 작은 fixedDeltaTime

## 테스트

- EditMode 단위 테스트 (`com.unity.test-framework` 기존 설치됨):
  - `BlackHole.AccelerationAt` 방향/크기 검증
  - `GravityIntegrator`: 반경 r에서 `v=√(GM/r)` 준 원궤도가 N스텝 후 반경 보존(허용오차 내)
- PlayMode sanity: 입자 방출 → 공전 → 호라이즌 흡수.

## 튜닝 파라미터 (Inspector)

G, M, R_h, ε, 입자수, 방출 모양(원반/구/링), 초기속도 프로파일, 타임스케일, maxRadius.

## 2단계 로드맵 (지금 범위 밖)

중력 렌즈 셰이더 · 강착원반 발광 · Bloom 포스트프로세싱 · 입자 Jobs/Burst·Compute 승급.

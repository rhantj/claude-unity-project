# 블랙홀 측지선 광학 + 볼륨메트릭 강착원반 (3단계) 설계

## 목표

2단계의 약장 뉴턴 근사 렌즈를 **슈바르츠실트 null 측지선 적분**으로 교체하여, 가르강튀아(Interstellar)급 사실성을 가진 블랙홀 비주얼을 만든다. 구체적으로 **포톤 스피어 기반 강한 중력렌즈(디스크의 그림자 위/아래 이중상)**, **포톤 링**, **볼륨메트릭 강착원반(온도·밀도 누적)**, **상대론적 도플러 빔ing + 중력 적색편이**, **HDR 톤매핑**을 구현한다.

## 비목표 (범위 밖)

- 회전(커) 블랙홀, 다중 블랙홀.
- 완전한 측지선 적분기(affine 매개변수 RK45 등 고차) — 2차(Verlet/RK2)면 충분.
- 동역학 코어(PW 입자·던지기) 변경 — **광학 경로만** 바꾼다. 두 경로는 독립이다.
- 외부 큐브맵/HDRI 임포트 — 별 배경은 절차적 유지.

## 기존 코드와의 관계

| 기존 | 처리 |
|---|---|
| `BlackHoleLensFeature.cs` (풀스크린 패스, DrawProcedural) | **유지** — 골격 재사용 |
| `BlackHoleLens.shader` (March/AccelerationAt/DiskColorAt) | **전면 재작성** — 측지선 + 볼륨 디스크 + 상대론 셰이딩 |
| `BlackHoleLensController.cs` 전역 push | **재설계** — `_BHLensMu·lensStrength·_BHSoftening` 폐기, r_s 기반 신규 프로퍼티 |
| `BlackBodyColor.cs` (온도→색, HLSL 미러 패턴) | **유지·확장** — 동일 패턴으로 신규 순수함수 추가 |
| `ParticleField` (PW 동역학) | **유지, 토글(기본 꺼짐)** — 사실적 뷰에선 끄고 동역학 데모 시 켬 |
| `GravityField`(PW), `ThrowableBody` | **무변경** |

## 아키텍처

URP `ScriptableRendererFeature` 풀스크린 패스(2단계 그대로)가 `BeforeRenderingOpaques`에 배경을 그린다. 프래그먼트마다:

1. 카메라 광선 생성 → 광자 측지선으로 적분(스텝 루프).
2. 스텝마다: 호라이즌 진입 검사(그림자) → 디스크 슬랩 통과 시 볼륨 누적(밀도·발광, 도플러·적색편이 적용) → 포톤 스피어 통과 누적(포톤 링).
3. 광선이 멀리 탈출하면 렌즈된 절차적 별 배경 샘플.
4. 누적 HDR 색을 반환 → URP 톤매핑(ACES) + Bloom으로 합성.

동역학 오브젝트(입자/던진 물체)는 토글 시 그 위에 일반 렌더로 합성된다.

## 컴포넌트 (작은 단위 + HLSL 미러)

기존 `BlackBodyColor` 패턴(C# 순수함수 + EditMode 테스트 + 셰이더 미러)을 따른다.

### `Assets/BlackHoleSim/Runtime/PhotonGeodesic.cs`
슈바르츠실트 null 측지선의 카르테시안 가속도와 한 스텝(순수함수).

```
public static class PhotonGeodesic {
    // a = -C · r_s · |h|² · x / r⁵,  h = cross(x, dir).
    // 상수 C는 약장 편향각이 2·r_s/b가 되도록 보정(테스트로 고정).
    public static Vector3 Acceleration(Vector3 pos, Vector3 dir, float rs);
    public static void Step(ref Vector3 pos, ref Vector3 dir, float rs, float dλ); // RK2
}
```

- **불변식**: |h|는 적분 동안 보존(테스트로 확인). 광자 속도 dir은 정규화 유지.
- **포톤 스피어**: r = 1.5·r_s에서 원궤도(불안정) — 이 반경 근처 광선이 휘감겨 이중상·포톤 링 생성.

### `Assets/BlackHoleSim/Runtime/Relativity.cs`
상대론적 셰이딩 인자(순수함수).

```
public static class Relativity {
    public static float OrbitalSpeed(float r, float rs);          // v=√((r_s/2)/(r−r_s)), <1로 클램프
    public static float DopplerFactor(float beta, float cosAngle);// δ=1/(γ(1−β·n̂))
    public static float GravitationalRedshift(float r, float rs); // √(1−r_s/r)
    public static float BeamingIntensity(float doppler);          // ∝ δ⁴
}
```

### `Assets/BlackHoleSim/Shaders/BlackHoleLens.shader`
위 함수들의 HLSL 미러 + 볼륨메트릭 디스크 누적 + 포톤 링 + 렌즈된 별 배경 + `BlackBodyColorApprox`. 출력은 HDR(>1 허용).

### `Assets/BlackHoleSim/Runtime/BlackHoleLensController.cs`
매 프레임 전역 push(카메라·블랙홀·디스크·적분). 신규 프로퍼티 이름:

- `_BHRs`(슈바르츠실트 반경 = EventHorizonRadius), `_BHCamPos/Forward/Right/Up`, `_BHTanHalfFovX/Y`, `_BHStarDensity`(유지)
- 디스크: `_BHDiskInner`(=ISCO=3·r_s), `_BHDiskOuter`, `_BHDiskThickness`, `_BHDiskDensity`, `_BHDiskTempInner/Outer`
- 상대론: `_BHBeaming`(빔ing 강도 0~1), `_BHRedshift`(적색편이 강도 0~1)
- 적분/품질: `_BHStepCount`(인스펙터 슬라이더), `_BHStepSize`(또는 적응 파라미터), `_BHPhotonRing`(포톤 링 강도)
- **폐기**: `_BHLensMu`, `lensStrength`, `_BHSoftening`

### `Assets/BlackHoleSim/Editor/BlackHoleSimEditorTools.cs`
ACES 톤매핑 설정 헬퍼(`SetTonemappingACES`), 입자 토글 헬퍼.

## 씬 스케일·물리 (모두 r_s 기준)

| 양 | 값 |
|---|---|
| 슈바르츠실트 반경 r_s | 2 (= EventHorizonRadius) |
| 포톤 스피어 | 1.5·r_s = 3 |
| ISCO (디스크 내경) | 3·r_s = 6 |
| 디스크 외경 | ≈ 22 (빛이 감길 여유) |
| 디스크 두께(반) | ≈ 0.5 (볼륨 슬랩) |
| 디스크 원궤도 속도 | v(r)=√((r_s/2)/(r−r_s)); ISCO에서 0.5c → 강한 빔ing |

- 온도: 내경(ISCO) 청백색(~18000K) → 외경 적색(~3000K), `BlackBodyColorApprox`.
- 볼륨 누적: 슬랩 내 밀도(반경 falloff × 수직 가우시안 × 선택적 노이즈) 가중 발광, 광선따라 누적(흡수 근사 포함). 샘플마다 도플러·적색편이로 색·세기 보정.
- 적분: 슬라이더 스텝(예 200~1000). 포톤 스피어/디스크 근처는 적응 스텝(작게)으로 stiff 구간 안정화, 먼 영역은 큰 스텝.

## 데이터 흐름

```
Controller.Update → Shader.SetGlobal*( r_s, disk*, beaming, redshift, stepCount, camera* )
Frag(uv):
  dir = CameraRayDir(uv)
  (pos,dir) = CamPos, dir
  color = 0; transmittance = 1
  for i in stepCount:
     if r ≤ r_s: return shadow(누적색)               // 호라이즌
     if photonSphere 근처: color += photonRing 기여
     if 디스크 슬랩 안: 
        emit = BlackBody(temp(r)) * density(r,y)
        emit *= Beaming(Doppler(v(r),angle)) * Redshift(r)   // 상대론
        color += transmittance * emit * dλ; transmittance *= exp(-density*dλ)
     PhotonGeodesic.Step(pos, dir, r_s, dλ)
  color += transmittance * lensedStarField(dir)
  return HDR color
→ URP Tonemapping(ACES) + Bloom
```

## 검증 전략

단위 테스트 불가능한 셰이더 영역은 단계별 스크린샷으로 검증(2단계 전략과 동일).

### EditMode 단위 테스트 (`PhotonGeodesicTests`, `RelativityTests`)
- 약장 편향: 큰 충돌계수 b로 적분 시 편향각 ≈ 2·r_s/b (GR 결과). **상수 C 보정의 기준.**
- |h| 보존: 적분 전후 각운동량 크기 불변(허용오차).
- 포톤 스피어: r=1.5·r_s에서 접선 광자가 (불안정) 원궤도 — 짧은 적분 동안 반경 유지.
- 호라이즌 포획: 충돌계수가 임계 이하면 r ≤ r_s 도달.
- `Relativity`: OrbitalSpeed 단조 증가(내측 빠름)·<1, Doppler δ가 접근 시 >1·후퇴 시 <1, Redshift ∈(0,1)·내측에서 작아짐, Beaming=δ⁴.

### 셰이더 스크린샷 검증 (태스크별)
1. 측지선 별 배경 렌즈 — 포톤 링(그림자 둘레 얇은 밝은 고리) 출현.
2. 볼륨 디스크 — 그림자 위/아래로 감긴 이중상(가르강튀아 띠).
3. 도플러 빔ing — 좌우 극단 밝기 비대칭.
4. 중력 적색편이 — 내측이 붉고 어두워지는 그라데이션.
5. 톤매핑/Bloom 통합 — HDR 디스크가 자연스럽게 발광, 동역학 토글 시 입자 정상 합성.

## 글로벌 제약

- Unity 6 (6000.5.0f1), URP 17.5.0, Render Graph, 신규 패키지 미추가.
- 모든 코드 변경 후 `editor_refresh_assets → editor_recompile → editor_wait_ready → editor_read_log`(Error 0건) 확인.
- 셰이더는 2단계에서 검증된 패턴 사용: `Blit.hlsl` 금지, 자체 풀스크린 Vert(Core.hlsl `GetFullScreenTriangleVertexPosition`) + `DrawProcedural`. (참조: 커밋 8da4787)
- 카메라 clear는 SolidColor 유지(`ConfigureCameraForLens`), 절차적 별 배경이 스카이박스 대체.

## 미해결/구현 시 결정

- 측지선 가속도 상수 C: 약장 편향 테스트로 정확값 고정.
- 적응 스텝 구체식: 1/r 또는 디스크/포톤스피어 근접도 기반 — 성능/안정 보며 튜닝.
- 디스크 노이즈(난류) 포함 여부: 기본 비활성, 여유 시 추가.

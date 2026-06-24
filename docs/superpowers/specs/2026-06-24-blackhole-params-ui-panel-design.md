# 블랙홀 파라미터 UI 패널 (MVVM) 설계

## 배경

`BlackHoleSim`의 시각/물리 파라미터(`BlackHole`, `BlackHoleLensController`, `ParticleField`)는 현재 인스펙터에서만 조절 가능하다. 빌드된 게임/데모에서 직접 만져볼 수 있는 런타임 UI 패널이 필요하다. UI 패턴은 MVVM을 적용한다.

## 아키텍처 개요

외부 리액티브 라이브러리(UniRx 등) 없이 경량 C# MVVM을 직접 구현한다.

- **Model**: 기존 `BlackHole`, `BlackHoleLensController`, `ParticleField`. 각 튜닝 필드에 public 프로퍼티(getter/setter)를 추가해 외부에서 읽고 쓸 수 있게 한다. 기존 내부 로직(`PushGlobals`, `FixedUpdate` 등)은 그대로 private 필드를 읽으므로 프로퍼티를 통한 변경이 다음 프레임에 즉시 반영된다.
- **ViewModel**: 순수 C# 클래스 `BlackHoleParamsViewModel` (MonoBehaviour 아님). 파라미터마다 `ObservableValue<T>`(값 변경 시 `Changed` 이벤트를 발생시키는 경량 래퍼)를 가지며, 생성 시 Model의 현재 값으로 초기화하고 변경 시 Model setter로 되돌려 쓴다. 패널/DangerZone의 펼침-접힘 상태도 ViewModel이 보유한다.
- **View**: UGUI 기반 `MonoBehaviour` 컴포넌트들. 각 Row는 바인딩된 `ObservableValue<T>`를 구독해 UI를 갱신하고, 사용자 입력 시 ViewModel setter를 호출한다. 리플렉션이나 자동 데이터바인딩 없이 명시적 구독/콜백만 사용한다.

데이터 흐름(단방향, 외부에서 모델이 바뀌는 경우는 없음):

```
슬라이더 드래그 → Row.OnValueChanged(value)
                 → ViewModel.SetX(value)
                 → ObservableValue.Value = value → Changed 이벤트
                 → (a) Row 자신의 UI 갱신(피드백 가드로 루프 방지)
                 → (b) Model setter 적용 (예: controller.DiskInnerRadius = value)
```

## 파라미터 그룹 (5개 콜랩서블 섹션)

패널 전체는 Tab 키 또는 헤더 버튼으로 접고 펼친다. 값은 세션 간 저장하지 않으며, Play 시작 시 항상 각 컴포넌트의 인스펙터 기본값에서 시작한다.

1. **Disk** (`BlackHoleLensController`): `diskInnerRadius`, `diskOuterRadius`, `diskThickness`, `diskDensity`, `diskTempInnerKelvin`, `diskTempOuterKelvin`, `diskColorTint`(RGB 슬라이더 3개 + 미리보기 스와치)
2. **Relativity** (`BlackHoleLensController`): `beamingStrength`, `redshiftStrength`, `photonRing`
3. **Render Quality** (`BlackHoleLensController`): `stepCount`, `stepSize`, `starDensity`
4. **Particles** (`ParticleField`): `enabled`(토글), `count`, `innerRadius`, `outerRadius`, `diskThickness`, `speedJitter`, `maxRadius`, `infallSpeedFactor`, `particleSize`
5. **Danger Zone** (`BlackHole`, 기본 접힘 + 빨간 경고 테두리): `gravitationalConstant`, `mass`, `softening`, `eventHorizonRadius`(r_s)
   - 섹션 헤더에 "r_s를 바꾸면 ISCO/호라이즌이 즉시 바뀌어 디스크 반경과 시각적으로 어긋날 수 있음" 경고 문구 1줄 표시.

### Model 변경 사항

- `BlackHole`: `GravitationalConstant`, `Mass`, `Softening`, `EventHorizonRadius`에 setter 추가(기존 `Configure(...)`는 테스트 헬퍼로 유지).
- `BlackHoleLensController`: 12개 필드 각각에 public 프로퍼티 추가.
- `ParticleField`: 8개 필드에 프로퍼티 추가 + `enabled` 토글은 컴포넌트 `enabled` 플래그(또는 GameObject active)로 매핑. `count` 변경은 `Awake`에서 고정 크기로 할당하던 배열(`pos`, `vel`, `rendered`)을 재할당해야 하므로, 기존 초기화 로직을 `Reinitialize(int newCount)` 메서드로 추출해 `Awake`와 런타임 변경 양쪽에서 재사용한다.

## View 계층 구조

```
Canvas (Screen Space - Overlay)
└─ EventSystem
└─ ParamPanel (RectTransform, 화면 우측)
   ├─ Header (제목 TMP_Text + 접기/펼치기 Button) — ParamPanelView가 Tab 키 입력도 처리
   └─ ScrollView/Content (Vertical Layout Group)
      ├─ CollapsibleSection "Disk"
      │   └─ FloatSliderRow × 6, ColorSwatchRow × 1
      ├─ CollapsibleSection "Relativity"
      │   └─ FloatSliderRow × 3
      ├─ CollapsibleSection "Render Quality"
      │   └─ FloatSliderRow × 2, IntSliderRow × 1
      ├─ CollapsibleSection "Particles"
      │   └─ ToggleRow × 1, FloatSliderRow × 7, IntSliderRow × 1
      └─ CollapsibleSection "Danger Zone" (기본 접힘, 빨간 테두리 Image)
          └─ FloatSliderRow × 4
```

재사용 View 컴포넌트 4종, 각각 `Bind(ObservableValue<T>, label, min, max)`을 받는다:

- `FloatSliderRow` (Slider + TMP_Text 라벨/값)
- `IntSliderRow` (Slider whole numbers + TMP_Text)
- `ToggleRow` (Toggle + TMP_Text)
- `ColorSwatchRow` (R/G/B `Slider` 3개 + `Image` 미리보기 스와치)

공통 컨테이너 `CollapsibleSection` (헤더 클릭 시 본문 GameObject `SetActive` 토글, DangerZone은 빨간 `Image` 테두리 변형).

텍스트는 **TextMeshPro**(`TMP_Text`)로 구현한다. 프로젝트에 TMP 패키지/Essential Resources가 아직 없으므로 구현 단계에서 설치 및 임포트한다.

### 바인딩 규칙

Row는 생성 시 `ObservableValue<T>.Changed`를 구독해 UI를 갱신하고, UI 콜백(`onValueChanged`)에서는 `_suppressFeedback` 가드를 사용해 루프 없이 ViewModel setter를 호출한다. `ParamPanelView`가 시작 시 `BlackHoleParamsViewModel`을 생성(씬의 `BlackHole`/`BlackHoleLensController`/`ParticleField` 참조 주입)하고, 각 Row에 해당 `ObservableValue`를 주입한다.

## 테스트 / 검증

- **ViewModel**: 순수 C# 클래스이므로 기존 `Tests/Editor` 패턴대로 NUnit EditMode 테스트 작성. `ObservableValue` 값 변경 시 이벤트 발생 여부, ViewModel setter 호출 시 Model에 실제로 반영되는지, `count` 변경 시 `ParticleField.Reinitialize`가 배열 크기를 올바르게 재조정하는지 검증.
- **Model 프로퍼티**: 기존 `BlackHoleLensControllerTests`처럼 프로퍼티로 값을 설정한 뒤 `PushGlobals()` 호출 시 셰이더 전역값에 반영되는지 확인하는 테스트 추가.
- **View(UGUI)**: 자동 유닛테스트 대상이 아님. Unity MCP로 Play 모드 진입 → 슬라이더/토글 조작 → 스크린샷으로 시각 확인(디스크 색 변경, DangerZone 펼침/접힘, 패널 Tab 토글 등) 수동 검증.

## 작업 범위

- `BlackHole.cs`, `BlackHoleLensController.cs`, `ParticleField.cs`: 프로퍼티 추가(+ `ParticleField.Reinitialize`)
- `Assets/BlackHoleSim/UI/ViewModels/ObservableValue.cs`, `BlackHoleParamsViewModel.cs` (순수 C#, MonoBehaviour 아님)
- `Assets/BlackHoleSim/UI/Views/`: `ParamPanelView`, `CollapsibleSection`, `FloatSliderRow`, `IntSliderRow`, `ToggleRow`, `ColorSwatchRow`
- TextMeshPro 패키지 설치 + Essential Resources 임포트
- 씬에 Canvas/EventSystem/패널 하이어라키 구성 (Unity MCP로 직접 빌드)
- `Tests/Editor/UI/`: ViewModel + Model 프로퍼티 테스트

## 범위 밖 (YAGNI)

- 세션 간 값 저장(PlayerPrefs) — 매번 인스펙터 기본값에서 시작
- HSV 색상 피커, 프리셋 저장/불러오기
- 모바일/터치 전용 레이아웃 대응

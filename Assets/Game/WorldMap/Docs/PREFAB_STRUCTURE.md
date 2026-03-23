# 삼국지 주식 — World 탭 프리팹 구조 정의

9:16 세로(1080×1920 기준) 다크 MTS 스타일 UI. 계층·컴포넌트는 `CreateCityPrefabs.cs`와 동일하게 맞춤.

---

## CityNode

**역할**: 지도 위 개별 성 카드. 군주(세력) 배경색, 성명, 민심·등락률 표시, `IsWar` 시 테두리 글로우 애니메이션.

**루트 GameObject**

| 항목 | 값 |
|------|-----|
| 이름 | `CityNode` |
| 컴포넌트 | `RectTransform`, `Image`, `Button`, `LayoutElement`, `CityNode` |
| RectTransform | 앵커 Min/Max (0.5, 0.5), Pivot (0.5, 0.5), 권장 크기 **220×132** (스크롤 맵용 카드) |
| Image | 카드 면 — `CityNode`가 런타임에 **군주 색**으로 채움. Raycast Target **On** (Button 타깃) |
| Button | `Transition = ColorTint` 또는 `None`. `OnClick`은 비워 둠 — `CityNode`가 `WorldMapManager`에 위임 |
| LayoutElement | `preferredWidth/Height`로 카드 최소 크기 고정(선택) |

**자식 계층**

```
CityNode
├── WarBorder          (RectTransform, Image, Outline)
├── VolumetricGlow     (RectTransform, Image)  — 상단 얇은 하이라이트(깊이감)
├── TextStack          (RectTransform, VerticalLayoutGroup)
│   ├── CityName       (RectTransform, TextMeshProUGUI)
│   ├── Sentiment      (RectTransform, TextMeshProUGUI)
│   └── ChangeRate     (RectTransform, TextMeshProUGUI)
└── ClickBlock         (RectTransform, Image) — 투명, Raycast만 (선택, Button 단일로 충분 시 생략 가능)
```

| 오브젝트 | 컴포넌트 | 비고 |
|----------|-----------|------|
| **WarBorder** | `Image` (Simple, 전체 스트레치), `Outline` | 색·두께는 에디터 기본값; `CityNode`가 `IsWar` 시 알파/색 펄스. 비전쟁 시 알파 0 |
| **VolumetricGlow** | `Image` | 상단 20% 높이 앵커 Top-Stretch, 그라데이션 느낌의 밝은 띠(반투명 흰/청록) |
| **TextStack** | `VerticalLayoutGroup` (Child Alignment: Upper Center), `ContentSizeFitter` 생략 가능 | 패딩 8 |
| **CityName** | `TextMeshProUGUI` | 크기 26–28, **Bold**, 흰색, 그림자 옵션 |
| **Sentiment** | `TextMeshProUGUI` | “민심 72” 형식 |
| **ChangeRate** | `TextMeshProUGUI` | “+2.3%” / “-1.1%”, 색은 런타임에서 상승 녹/하락 적 |

**CityNode 스크립트 바인딩 필드**

- `Image background` → 루트 `Image`
- `Image warBorder`, `Outline warOutline` (있으면)
- `Image volumetricGlow`
- `TMP cityName`, `sentiment`, `changeRate`
- `Button button` → 루트 `Button`

---

## CityDetailOverlay

**역할**: 화면 **하단 1/3** 슬라이드 업 패널. 태수·백성·민심 바(차트 대용)·평시 `[SUPPORT]` / `[WITHDRAW]`, 전쟁 시 `[REINFORCE]` / `[RETREAT]` 등 텍스트·콜백 스위칭.

**루트 GameObject**

| 항목 | 값 |
|------|-----|
| 이름 | `CityDetailOverlay` |
| 컴포넌트 | `RectTransform`, `CanvasGroup`, `CityDetailOverlay` |
| RectTransform | **Stretch-Stretch** 전체 화면 (부모가 Canvas/동일 해상도 기준) |
| CanvasGroup | 알파 0~1, 슬라이드 시 페이드에 사용 가능 |

**자식 계층**

```
CityDetailOverlay
├── Dim                (RectTransform, Image, Button) — 반투명 딤, 클릭 시 닫기
└── PanelRoot          (RectTransform) — 하단 1/3, 슬라이드 대상
    ├── PanelBg        (RectTransform, Image)
    ├── TopRimGlow     (RectTransform, Image) — 상단 에지 글로우
    ├── Header         (RectTransform, HorizontalLayoutGroup)
    │   ├── CityTitle  (TextMeshProUGUI)
    │   └── WarBadge   (TextMeshProUGUI) — 전쟁 중만 표시
    ├── GovernorRow    (HorizontalLayoutGroup)
    │   ├── GovLabel   (TMP)
    │   └── GovName    (TMP)
    ├── PopulationRow  (HorizontalLayoutGroup)
    │   ├── PopLabel   (TMP)
    │   └── PopValue   (TMP)
    ├── ChartRow       (VerticalLayoutGroup 또는 단일 행)
    │   ├── ChartLabel (TMP) “민심”
    │   ├── ChartBarBg (Image) 배경 띠
    │   └── ChartFill  (Image, Filled Horizontal) — 민심 0~100%
    └── ActionRow      (HorizontalLayoutGroup)
        ├── SupportBtn (Button)
        │   └── SupportLabel (TextMeshProUGUI)
        └── WithdrawBtn (Button)
            └── WithdrawLabel (TextMeshProUGUI)
```

| 오브젝트 | 컴포넌트 | 비고 |
|----------|-----------|------|
| **Dim** | `Image` 색 `#000000`, 알파 ~0.45 | `Button`으로 탭 시 `Hide()` |
| **PanelRoot** | 앵커 **Bottom-Stretch**, Pivot (0.5, 0), Height = 부모의 **33%** (스크립트/에디터에서 0.33) |
| **PanelBg** | 다크 그라데이션 느낌 `#0E1018` ~ `#161B28` | 상단은 약간 밝게 |
| **ChartFill** | `Image.Type = Filled`, `Fill Method = Horizontal` | `CityDetailOverlay`가 `fillAmount` 설정 |

**CityDetailOverlay 스크립트 바인딩**

- `RectTransform panelRoot`, `CanvasGroup rootGroup` (루트), `CanvasGroup dimGroup` (선택)
- `TMP cityTitle`, `warBadge`, `governorName`, `populationValue`, `chartLabel`
- `Image chartFill`
- `Button supportButton`, `withdrawButton`, `dimButton`
- `TMP supportLabel`, `withdrawLabel`

**TMP 폰트**

- 에디터 생성 시 `TMP_Settings.defaultFontAsset` 사용. 빈 프로젝트는 TMP 임포트 후 기본 SDF가 설정됨.

---

## 캔버스 / 해상도 (9:16)

- **Canvas Scaler**: `Scale With Screen Size`, Reference **1080 × 1920**, Match **0.5** 또는 Height 우선(세로형).
- **WorldMapManager**가 씬에 Canvas를 만들 때 위 설정을 코드로 적용 가능.

---

## LineRenderer (도시 연결선)

- `WorldMapManager`가 **연결 쌍**마다 자식 오브젝트에 `LineRenderer`를 붙이거나, 기존 풀을 사용.
- UI가 **Screen Space — Overlay**이면 월드 `LineRenderer`가 가려질 수 있어, **Screen Space — Camera** + `worldCamera` 할당을 권장. 스크립트는 두 모드 모두 처리하도록 월드 좌표를 `RectTransform`에서 계산.

---

이 문서는 `Assets/Game/WorldMap/Editor/CreateCityPrefabs.cs`의 생성 결과와 1:1로 대응한다.

---

## 에디터 메뉴 & 실행 순서

1. **StockThreeKingdoms → 천하 → 성 노드·오버레이 프리팹 생성**  
   - `Assets/Prefabs/CityNode.prefab`, `CityDetailOverlay.prefab` 생성  
   - `Assets/Game/WorldMap/Resources/WorldMap/` 에 동일 프리팹 복사 → `WorldMapManager`가 `Resources.Load`로 로드
2. (선택) **StockThreeKingdoms → 천하 → City Database 에셋 생성** — `CityDatabase` ScriptableObject + 50개 기본 데이터
3. **StockThreeKingdoms → 천하 → 씬에 천하 맵 자동 배치** — `WorldMap` 오브젝트와 `WorldMapManager`·프리팹(및 DB) 연결을 한 번에 처리  
4. **Play** — `autoBootstrap`이 캔버스·스크롤·카메라·이벤트 시스템을 만들고, 50개 노드·연결선·오버레이를 **런타임에** 자동 배치합니다. (에디터 모드 씬 뷰에는 플레이 전 UI가 없을 수 있음)

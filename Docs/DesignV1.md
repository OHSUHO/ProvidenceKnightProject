# Providence Knight 2D — v1 설계 (프로토타입 → 본 구현)

> 전제: [CoreSystemDesign.md](CoreSystemDesign.md)(v0)의 게임 루프·카드 순환·방어도·런 구조는 **그대로 유지**한다.
> 이 문서는 v0에서 드러난 네 가지 문제(규칙 모호함 / 클래스 집중 / 코드 생성 씬 / 데이터 관리)의 해결 방침이다.
>
> **확정된 방향**
> - 적 의도 = **실시간 예측 + 결과 보장** (플레이어 행동마다 다시 계산하되, 표시된 그대로 실행됨을 보장)
> - 스테이지 작성 = **StageData(SO) + 그리드 페인팅 인스펙터**
> - 수치 데이터 = **ScriptableObject 유지 + 고유 ID · GameDatabase 레지스트리**

---

## 0. v0 문제 진단 요약

| # | 증상 | 코드상 원인 |
|---|---|---|
| 1 | 공격 표시가 떴는데 막혀서 공격 못 함 | `RefreshIntents()`가 적마다 **독립적으로** 현재 격자 기준 계획 → 서로의 이동을 모름. 실행(`RunEnemyTurn`)은 Id 순서로 **다시 계획** → 표시 ≠ 실행 |
| 1 | 몇 칸 와서 때리는지 모름 | 의도 UI가 `"이동\n공격 N"` 텍스트뿐. 이동력·경로·도착 칸·공격 칸 표시 없음 |
| 1 | (잠재) 벽 뒤에서 멈춤 / 벽 너머 공격 | 이동 목표와 사거리를 **맨해튼 거리**로 판정 (벽·유닛 무시) |
| 2 | 한 클래스가 다 함 | `BattleState`: 상태 + 턴 + 카드 효과 `switch` + 데미지 + 의도 + 이벤트 9종. `BattleRunner`: 배선 + 입력 상태머신 + 카메라. `UnitView` 361줄 / `HudView`에 `CardView`·UI 헬퍼 동거 |
| 3 | 씬에서 수정 불가 | 씬엔 `Battle`, `Grid`, `Main Camera`뿐. 타일·유닛·HUD 전부 런타임 코드 생성. 카메라가 `HudView.BottomFraction = 0.27f` 상수에 묶임 |
| 4 | 데이터 방침 없음 | SO는 있으나 ID 없음(세이브 불가), 스테이지 배치를 좌표 숫자로 입력, 런 설정이 `RunRunner` 인스펙터 필드에 흩어짐 |

---

## 1. 전투 규칙 v1

### 1.1 핵심 불변식 — "보이는 대로 일어난다"

> **플레이어가 지금 턴을 끝내면, 적 턴은 화면에 표시된 계획과 정확히 같게 실행된다.**

- 적 턴 계획(`EnemyTurnPlan`)은 플레이어 턴 시작 시, 그리고 **플레이어의 모든 행동 직후** 다시 계산해 화면에 표시한다. 현재의 "적이 플레이어를 따라오는" 느낌은 그대로 유지된다.
- 적 턴에는 **다시 계획하지 않는다.** 마지막으로 표시된 `EnemyTurnPlan`을 그대로 실행한다.
- 이 불변식은 EditMode 테스트로 고정한다 (§5).

### 1.2 계획 방식 — 순차 시뮬레이션

적들이 서로 길을 막는 문제는 **실행 순서대로, 앞선 적의 결과를 반영하며 계획**하는 방식으로 해결한다.

```
EnemyPlanner.Plan(state):
    sim = state.Clone()                        // 가상 전투 상태
    for enemy in 행동 순서(§1.3):
        action = ChooseAction(sim, enemy)      // 이동 경로 + 공격 칸
        EnemyActionResolver.Apply(sim, action) // 실제 실행과 "같은 함수"로 가상 적용
        plan.Add(action)
        if sim.Player.IsDead: break
    return plan

RunEnemyTurn(state):
    for action in 마지막으로 표시된 plan:
        EnemyActionResolver.Apply(state, action)   // 같은 함수로 실제 적용
```

- 계획과 실행이 **같은 적용 함수**를 거치므로, 앞으로 넉백·소환·폭발 같은 효과가 추가되어도 불변식이 자동으로 유지된다.
- 앞선 적이 도착할 칸은 뒤따르는 적에게 이미 점유된 칸이다. 따라서 "둘이 같은 칸을 노리는" 상황은 계획 단계에서 해소되고, 뒤쪽 적은 다른 칸으로 우회하거나 이동만 하는 의도로 표시된다.

### 1.3 적 행동 순서

적 턴에는 몬스터가 한 마리씩 **행동 순서**대로 움직인다. 순서가 결과를 바꾸므로(누가 먼저 칸을 차지하는지) 화면에 순서 번호를 항상 표시한다.

| 규칙 | 내용 |
|---|---|
| 우선순위 값 | `UnitData.actionPriority` (정수). **0 = 미지정**, 1 이상은 **작을수록 먼저** 행동 |
| ① 지정 → 미지정 | 우선순위 값이 있는 몬스터가 모두 먼저 행동하고, 값이 없는 몬스터는 그 뒤에 행동 |
| ② 값이 같을 때 | 우선순위 값이 같은 몬스터끼리는(미지정끼리도 포함) 전투 시작 시 **무작위로 순서를 정한다** |
| ③ 고정 | 한 번 정해진 순서는 **그 스테이지가 끝날 때까지 바뀌지 않는다**. 몬스터가 죽으면 그 몬스터만 빠지고 나머지 순서는 그대로 유지 |
| 구현 | 스폰할 때 유닛마다 정렬 키 `(지정 여부, 우선순위, 무작위 타이브레이커)`를 한 번만 만들어 저장한다. 난수는 `BattleState` 생성 시 주입한 `System.Random`(테스트에서는 시드 고정)을 사용 |

### 1.4 적 행동 선택 규칙

| 단계 | 규칙 |
|---|---|
| 거리 | **경로 거리(BFS 칸 수)**. 4방향, 장애물·다른 유닛 통과 불가 (v0와 동일) |
| ① 제자리 공격 | 현재 위치에서 공격 패턴에 플레이어가 들어오면 이동 없이 공격 |
| ② 이동 후 공격 | 이동력 이내로 도달 가능한 칸 중 공격 가능한 칸이 있으면 → **이동 칸 수가 가장 적은 칸**으로 이동 후 공격 |
| ③ 접근 | 공격할 수 없으면 도달 가능한 칸 중 **플레이어까지의 경로 거리가 가장 짧은 칸**으로 이동. 이때 거리장은 다른 적을 통과 가능으로 보고 계산한다 (아군 뒤에 줄 서서 기다리는 모습이 자연스러움) |
| ④ 대기 | 가까워질 수 있는 칸이 없으면 대기 |
| 동점 처리 | 이동 칸 수 → 방향 우선순위(상·우·하·좌) 순. **항상 결정적**이어야 함 (랜덤 금지) |
| 공격 판정 | 적의 공격도 카드와 같은 **`TargetPattern`**(§2.3)으로 정의. 근접 = `Adjacent`, 원거리 = `Line`(장애물·유닛에서 멈춤). 공격은 **칸 단위**로 적용됨 (계획된 칸에 있는 유닛이 피해를 받음) |

### 1.5 화면 표시

| 표시 | 언제 | 내용 |
|---|---|---|
| 의도 배지 | 항상 | 적 머리 위: 아이콘(공격/이동/대기) + 피해량 + **실행 순서 번호**(①②③) |
| 이동 경로 | 항상 | 적 → 도착 칸까지 **그 적의 색**으로 된 선 + 화살촉, 도착 칸에 반투명 잔상(글자 + 순서 번호) |
| 공격 화살표 | 항상 | 공격 위치(도착 칸 또는 제자리) → 공격 칸까지 빨간 굵은 화살표 ("누가 누구를 치는지") |
| 공격 칸 | 항상 | 공격받을 칸을 빨간색으로 칠함. 플레이어 칸에는 **받을 피해 합계** 표시: `피해 12 (방어 5) → HP 30→23` |
| 위험 지역 | 적에 마우스를 올렸을 때 | 그 적의 이동 가능 범위(파랑) + 공격만 닿는 칸(분홍) + 스탯 툴팁(`이동 3 · 근접 · 공격 7`). **그 적의 차례가 됐을 때의 격자 기준**(앞선 적들의 계획을 먼저 적용)이라 계획된 도착·공격 칸이 항상 범위 안에 있음. 다른 적의 경로·잔상은 흐리게 |
| 전체 위험 지역 | 토글 키(Alt) / 버튼 | 모든 적의 공격 가능 칸 합집합 |
| 행동 미리보기 *(P3 구현 완료 → 플레이 테스트로 유지 여부 결정, `P` 키로 켜고 끔)* | 카드를 고르고 대상 칸에 마우스를 올렸을 때 | "이 칸에 쓰면 적 계획이 이렇게 바뀐다"를 미리 표시 (플레이어가 옮겨 갈 칸에 잔상, 쓰러질 적은 "처치"). `BattleController.PreviewCard`가 복사본에서 실제 사용과 같은 `ApplyCard`를 거쳐 계산 |

---

## 2. 로직 구조 (순수 C#)

### 2.1 폴더 / 네임스페이스 (P2에서 확정)

```
Scripts/
  Battle/            ProvidenceKnight.Battle
    GridMap, Unit(+ActionOrderKey), Targeting, CardCycle
    BattleState(+BattlePhase), BattleController, BattleRules, BattleEvents(이벤트 레코드·싱크)
  Battle/AI/         ProvidenceKnight.Battle.AI       EnemyPlanner, EnemyActionResolver, EnemyTurnPlan, EnemyAction
  Battle/Effects/    ProvidenceKnight.Battle.Effects  CardEffect(추상)+EffectContext, MoveEffect, DamageEffect, BlockEffect, GainMaxEnergyEffect
  Run/               ProvidenceKnight.Run             RunState(+시드), RewardPicker(System.Random 주입), RunSaveData(+RunSaveStore)
  Data/              ProvidenceKnight.Data            GameDataAsset(id), CardData, UnitData, StageData, DeckData, RewardPoolData, RunConfig, GameDatabase, DataValidator, TargetPattern(+TargetShape)
  View/, Input/      (§3)
  Editor/            ProvidenceKnight.EditorTools     CardDataEditor, StageDataEditor, BoardViewEditor, GameDataIdAssigner, GameDatabaseBuilder, ProjectDataValidator, DynamicFontSaveCleaner  ← 별도 asmdef ProvidenceKnight.Editor (Editor 전용)
```

- 처음 계획한 `Core/`는 만들지 않았다. `GridMap`이 `Unit`을 직접 들고 있어서 Core → Battle 역의존이 생기기 때문. `TargetPattern`·`Team`은 에셋에 직렬화되는 데이터 정의라 `Data`에 둔다.
- `[SerializeReference]` 에셋에는 효과 타입이 `ProvidenceKnight.Battle.Effects.<클래스명>`으로 저장된다. **효과 클래스의 이름·네임스페이스를 바꿀 때는 `[MovedFrom]`을 달아야 기존 카드 데이터가 깨지지 않는다.**

### 2.2 책임 분리

| 클래스 | 책임 | v0에서 가져온 것 |
|---|---|---|
| `BattleState` | **상태만**: Grid, Units, Player, Energy/MaxEnergy, Turn, Cards, `Phase`, `EnemyPlan`, `Revision`. `CloneForSimulation()`(이벤트 버림). 규칙·이벤트 발행 없음 | v0 `BattleState`의 필드 |
| `BattleController` | 외부 진입점. `Create`, `SpawnFromStage`, `StartPlayerTurn`, `EndPlayerTurn`, `RunEnemyTurn`, `EndTurn`(손패 교체 → 적 턴 → 다음 턴), `RefreshEnemyPlan`, `CanSelectCard`, `GetValidTargets`, `TryPlayCard`. 이벤트 로그 `Events` 보유 | v0 `BattleState`의 턴·카드 메서드, `BattleRunner.OnEndTurnClicked` |
| `BattleRules` | 상태를 바꾸는 유일한 경로: `SpawnUnit`, `TryMoveAlongPath`, `AttackCell`, `DealDamage`(방어도 흡수 → 사망 → 격자 제거 → 승패), `GainBlock`, `ResetBlock`, `SetPhase`, `CheckBattleEnd`. 바꿀 때마다 `Revision++` + 이벤트 기록. 카드 효과·적 행동·턴 진행이 모두 이것을 쓴다 | v0 `BattleState.DealDamage/Execute/Resolve` 일부 |
| `CardEffect` (추상) | `Resolve(in EffectContext)`, `Describe(CardData)`(설명 자동 생성), `Kind`(하이라이트 색 분류). 카드 = `[SerializeReference] List<CardEffect>` | `CardEffectType` enum + `Resolve` switch + `GetDescription` switch |
| `EnemyPlanner` / `EnemyActionResolver` | §1.2 계획 / 적용 | P1 |
| `Targeting` | `TargetPattern` → 칸 목록. 카드와 적이 공유 | v0 `CardTargeting` |
| `BattlePhase` | `NotStarted → PlayerTurn ⇄ EnemyTurn → Victory / Defeat`. 플레이어 턴이 아니면 카드 선택 불가 | — |

새 효과(넉백, 드로우, 상태이상 등)를 추가할 때는 `CardEffect` 하위 클래스 파일 하나만 만들면 된다. `CardDataEditor`의 "＋ 효과 추가" 메뉴에 자동으로 나타난다. `BattleState`와 `BattleController`는 수정하지 않는다.

### 2.3 이벤트 스트림 (C# event 9종 → 레코드 1줄기)

로직은 결과를 **순서가 있는 이벤트 레코드**로 `IBattleEventSink`에 기록한다. 실제 전투는 `BattleEventLog`(뷰는 `Emitted` 구독, 테스트는 `History` 검사), 시뮬레이션은 `NullEventSink`를 쓴다.

```csharp
abstract record BattleEvent;
record UnitSpawned(Unit Unit);
record UnitMoved(Unit Unit, IReadOnlyList<Vector2Int> Path);                 // 시작 칸 제외
record UnitAttacked(Unit Attacker, Unit Target, Vector2Int Cell, int Damage); // 피해 적용 직전
record UnitDamaged(Unit Unit, int HpLoss, int BlockLoss, int Hp, int Block);
record BlockChanged(Unit Unit, int Delta, int Block);                         // 획득(+) / 턴 시작 초기화(-)
record UnitDied(Unit Unit);
record CardPlayed(CardData Card, Vector2Int Target);
record ResourcesChanged;                                                      // 에너지 / 손패 / 턴
record EnemyPlanChanged(EnemyTurnPlan Plan);
record PhaseChanged(BattlePhase Phase);
record BattleEnded(bool PlayerWon);
```

- 처음 계획은 `UnitId`였지만 `Unit` 참조로 했다. 시뮬레이션 복사본은 이벤트를 버리므로 다른 상태의 유닛이 섞일 일이 없고, 뷰가 딕셔너리로 바로 찾을 수 있다.
- 이벤트 값에 결과 스냅샷(`Hp`, `Block`)을 담는다. 뷰(`UnitView.PlayDamaged/PlayBlockChanged`)는 로직 객체의 현재 값을 읽지 않으므로, 연출이 늦게 재생돼도 그 순간의 숫자가 정확하다.
- 현재(P2)는 `BattleRunner.OnBattleEvent`가 `switch`로 뷰 메서드를 부르고, 각 연출이 전역 `AnimationQueue`에 쌓인다. 승패 배너와 `BattleFinished`도 큐에 넣어 마지막 연출이 끝난 뒤 나온다. P3에서 `BattleEventPlayer`로 옮긴다.
- C# 9 `record`는 `System.Runtime.CompilerServices.IsExternalInit` 셈(`BattleEvents.cs` 하단)으로 사용한다.

---

## 3. 씬 / 뷰 구성

### 3.1 원칙

- 화면을 구성하는 것은 **씬에 배치된 GameObject와 프리팹**이다. 코드는 프리팹을 인스턴스화하고 값만 채운다.
- **월드 오브젝트**: 타일, 유닛, 유닛의 HP 바·방어도·의도 배지, 경로·공격 칸 오버레이, 팝업 숫자
- **HUD(Canvas)**: 손패, 에너지, 덱 미리보기, 턴 종료, 턴 표시, 토스트, 유닛 툴팁, 결과 배너, 보상 화면. 이 목록에 있는 것만 HUD로 둔다.
- 텍스트는 레거시 `TextMesh`/`Text` 대신 **TextMeshPro**를 쓴다. 폰트는 `Assets/Resources/Font/Warhaven/NEXON_Warhaven_{Regular,Bold}.ttf`로 TMP 폰트 에셋(Dynamic)을 만든다. TMP 에셋은 직접 참조하므로 원본 폰트가 Resources에 있을 필요는 없다. P3에서 `_Project/Fonts`로 옮기는 것을 권장한다 (Resources 안의 파일은 참조 여부와 관계없이 빌드에 모두 포함됨).

### 3.2 Battle.unity 하이어라키

P3에서 실제로 만든 구성 (루트는 씬 최상위):

```
Main Camera                    (CameraFramer: HUD의 BoardArea 사각형 안에 보드를 맞춤, 에디터에서도 동작)
Battle                         (BattleBootstrap: 조립 / RunController: RunConfig 에셋으로 스테이지·보상 흐름)
Board                          (BoardView: 좌표 변환, Tile·Unit 프리팹 생성. [인스펙터] "스테이지 미리보기" 버튼)
├─ Tiles                       ← Tile.prefab 인스턴스 (런타임 생성)
├─ Overlays                    (HighlightLayer: 카드 대상·위험 지역·호버)
│  ├─ Hover
│  └─ Plan                     (IntentOverlayView: 공격 칸·경로·잔상·공격 화살표, 프리팹 풀)
├─ Units                       ← Unit_Player / Unit_Enemy 인스턴스
└─ Effects                     (FloatingTextPool)
Systems
├─ EventPlayer                 (BattleEventPlayer: 이벤트 → 연출 큐)
├─ PlanPresenter               (EnemyPlanPresenter: 오버레이 + 의도 말풍선 + 피해 예고 + 행동 미리보기)
└─ Input                       (BoardPointer: 포인터→칸 / PlayerTurnInput: Idle·CardSelected·Busy)
HUD Canvas                     (BattleHud)
├─ BoardArea                   (빈 RectTransform — 필드가 들어갈 화면 영역. 여기를 옮기면 카메라가 따라옴)
├─ TopBar                      (TurnInfo, Toast)
├─ BottomPanel                 (CanvasGroup: Energy, Hand ← Card.prefab ×5, DeckPreview, EndTurnButton)
├─ UnitTooltip
└─ ResultBanner
Reward Canvas                  (RewardScreen, Panel 기본 비활성)
EventSystem                    (InputSystemUIInputModule)
```

### 3.3 프리팹

| 프리팹 | 구성 | 스크립트 |
|---|---|---|
| `Tile` | Floor + Threat(공격 예정 빨강) + Highlight 자식 | `TileView` (바닥 체크무늬·장애물 색) |
| `Unit` → `Unit_Player` / `Unit_Enemy` Variant | Body, Initial(아트 없을 때 글자), HpBar, BlockBadge, IntentBadge 자식을 **미리 배치**. IntentBadge는 적 = 의도, 플레이어 = 받을 피해 예고 (한 줄, 자기 칸 안에 들어가게) | `UnitView`, `HpBarView`, `BlockBadgeView`, `IntentBadgeView` |
| `Card` | 테두리·배경·코스트·이름·설명 (TMP UGUI) | `CardView` — 손패·보상 화면 공용 |
| `FloatingText` | TMP 월드 텍스트 | `FloatingText` + `FloatingTextPool` |
| `PlanSegment` / `PlanArrowHead` / `PlanGhost` | 경로·공격 화살표 선분, 화살촉, 도착 칸 잔상(몸체·글자·테두리·순서 배지) | `IntentOverlayView`가 풀링, `PlanGhostView` |

- `UnitData.viewPrefab`(선택)으로 몬스터별 전용 프리팹을 지정할 수 있다. 비어 있으면 기본 Variant에 `sprite`/`color`만 적용한다.
- 모든 연출 값(이동 속도, 런지 거리, 흔들림 세기, 색)은 스크립트 상수에서 **인스펙터 필드**로 옮긴다.

### 3.4 v0 → v1 뷰 대응

| v0 | v1 |
|---|---|
| `BattleRunner` | `BattleBootstrap`(조립) + `PlayerTurnInput`(카드→칸 선택) + `EnemyPlanPresenter`(계획 표시) + `CameraFramer` + `BattleEventPlayer` |
| `GridView` | `BoardView` + `TileView` + `HighlightLayer` + `IntentOverlayView` |
| `UnitView`(361줄, 코드 생성) | `Unit` 프리팹 + 작은 컴포넌트 4개 |
| `HudView` + `CardView` + `UI` 헬퍼 | 씬 Canvas + `BattleHud` + `HandView`/`CardView`/`EnergyView`/`DeckPreviewView`/`TurnInfoView`/`ToastView`/`UnitTooltipView`/`ResultBannerView` |
| `RewardView` | `RewardScreen` (씬에 배치, `Card` 프리팹 재사용) |
| `RunRunner` | `RunController` + `RunConfig` 에셋 (P4) |
| `TweenClock`(P1에서 삭제 → `AnimationQueue`), `SpriteFactory` | `BattleEventPlayer`(인스턴스 큐, `IsPlaying`), `Art/Sprites/{Square,Triangle,Circle}.png`, TMP 폰트 에셋 |

---

## 4. 데이터 관리

### 4.1 에셋 종류

| 에셋 | 내용 | 비고 |
|---|---|---|
| `GameDataAsset` (추상 베이스) | `string id` | 모든 데이터 SO의 부모. id는 생성 시 자동 부여, **중복 검증** |
| `CardData` | id, 이름, 코스트, `TargetPattern`, `[SerializeReference] List<CardEffect>`, exhaust, 아트, 설명(비우면 효과로 자동 생성) | 효과 추가는 인스펙터 "＋ Effect" 드롭다운 (커스텀 에디터) |
| `UnitData` | id, 이름, 팀, maxHp, 스프라이트/색/`viewPrefab`, **적 전용**: moveRange, 공격 `TargetPattern`, 피해, `actionPriority`(§1.3) | 나중에 `actionPattern`(행동 순환) 추가 자리 |
| `StageData` | id, width/height, 장애물, 플레이어 시작 위치, 몬스터 배치 (플레이어 유닛은 `RunConfig`) | **그리드 페인팅 인스펙터** (§4.2) |
| `DeckData`, `RewardPoolData` | 카드 리스트 | 현행 유지 |
| `RunConfig` *(신규)* | 플레이어 UnitData, 스테이지 순서, 시작 덱, 보상 풀, 손패 수, 시작 에너지, 보상 선택지 수 | `RunRunner`/`BattleRunner` 인스펙터에 흩어진 값을 한 곳으로 |
| `GameDatabase` *(신규)* | 모든 Card/Unit/Stage 목록 + `Get<T>(id)` | 데이터 에셋이 추가·삭제·이동되면 자동으로 다시 모음 (메뉴 `ProvidenceKnight/Rebuild Database`도 있음). 세이브 불러오기에서 id → 에셋 (Resources 폴더 미사용) |

### 4.2 스테이지 그리드 페인팅 인스펙터

- `StageData` 인스펙터에 칸 격자를 그려 **클릭/드래그로 칠한다**.
- 브러시: `장애물` / `플레이어 시작` / `몬스터(UnitData 선택)` / `지우개` (P4: `바닥`은 `지우개`와 하는 일이 같아 하나로 합침. 오른쪽 클릭 = 지우개)
- width/height를 바꾸면 격자 밖으로 나간 배치를 경고하고 잘라낸다.
- 칸마다 몬스터 이름 첫 글자와 색, 실행 순서 번호를 표시한다 (§1.3 동점 처리와 순서가 연결되므로).
- `Validate()` 결과를 인스펙터 상단에 빨간 박스로 띄운다.
- 선택 기능: 씬의 `Board`에서 "스테이지 미리보기"를 누르면 편집 중인 StageData로 타일과 유닛을 에디터 모드에서 생성해 보여준다 (저장하지 않는 임시 오브젝트).

### 4.3 검증 · 세이브

- **데이터 검증**: 메뉴 `ProvidenceKnight/Validate All Data`와, 같은 검사를 돌리는 EditMode 테스트 1개 (id 중복, 빈 참조, 스테이지 배치 오류, 효과 없는 카드).
- **세이브(런 이어하기)**: 이번 범위에서는 **구조만 대비**한다. 구현은 필요해질 때 한다.
  `RunSaveData { stageIndex, playerHp, maxEnergy, deckCardIds[], rngSeed }` → `JsonUtility` → `persistentDataPath`. 에셋은 id로만 참조해서 저장한다.
- **마이그레이션**: ✅ P2에서 완료 (카드 10장 `targeting` + `[SerializeReference] effects`로 변환, 옛 필드 제거).

---

## 5. 테스트 계획

- 기존 EditMode 테스트 42개는 API가 바뀌는 만큼 옮겨 유지한다 (CardCycle·GridMap 테스트는 거의 그대로).
- **신규 — 불변식**
  - `Plan_ThenExecute_ProducesSameMovesAndAttacks`: 여러 배치(좁은 통로, 적 3마리가 같은 칸을 노림, 벽 뒤 추격)에서 계획 이벤트와 실행 이벤트가 일치하는지.
  - `TwoEnemies_SameGoal_SecondTakesAlternateTile_OrMoveOnly`: v0 버그 재현 케이스.
  - `Chase_UsesPathDistance_NotManhattan`: 벽 뒤로 돌아가는지.
  - `Plan_IsDeterministic`: 같은 상태에서 계획을 두 번 세우면 결과가 같은지.
  - `Plan_DoesNotMutateState` / `Plan_EmitsNoEvents`: 시뮬레이션이 원본에 새지 않는지.
- **신규 — 행동 순서**
  - `Order_PrioritizedBeforeUnprioritized`, `Order_LowerPriorityValueActsFirst`
  - `Order_TiesAreShuffled_ButFixedForWholeStage`: 여러 턴이 지나고 몬스터가 죽어도 남은 몬스터의 상대 순서가 그대로인지 (시드 고정)
- **신규 — 데이터**: `ProjectData_HasNoIssues` (메뉴 Validate All Data와 같은 검사), 검증 항목별 테스트, 세이브 왕복 (`DataTests`, `SaveTests`)

---

## 6. 결정 사항 (2026-09-30 확정)

1. **한글 폰트**: NEXON Warhaven (`Assets/Resources/Font/Warhaven/`). 상업 배포 전에 넥슨 폰트 라이선스 조건을 확인할 것.
2. **적끼리 통과**: 불가 (v0와 동일). 계획 단계에서 막힘을 미리 반영한다.
3. **행동 순서**: 몬스터별 우선순위 값. 지정된 몬스터가 먼저 행동하고, 같은 값은 무작위로 정해 스테이지가 끝날 때까지 고정한다 (§1.3).
4. **행동 미리보기**(§1.5 마지막 줄): P3에 넣고, 플레이 테스트로 유지 여부를 판단한다.

---

## 7. 구현 단계

각 단계가 끝날 때마다 게임은 **플레이 가능한 상태**여야 하고, 테스트는 모두 통과해야 한다.

| 단계 | 내용 | 완료 기준 |
|---|---|---|
| **P1 규칙** | `EnemyPlanner`(순차 시뮬레이션) + `EnemyTurnPlan` + 경로 거리 + 계획 그대로 실행 + 행동 순서(우선순위). `TargetPattern`을 적 공격에 적용. 임시로 기존 `GridView`에 경로·공격 칸·순서 번호·위험 지역(적 호버) 표시 | §5 불변식 테스트 통과. 표시된 공격이 막혀서 불발되는 일이 없음 |
| **P2 로직 분리** ✅ | `BattleState`/`BattleController` 분리, 이벤트 스트림, `CardEffect` 다형성 + 카드 에셋 마이그레이션, `BattleRules` | 기존 테스트 이전 완료. `BattleState`에 효과 `switch` 없음 |
| **P3 씬/프리팹** ✅ | §3 하이어라키와 프리팹, TMP 폰트, `BattleEventPlayer`, `PlayerTurnInput`, `CameraFramer`, 위험 지역·툴팁·피해 예고 UI | 씬 뷰에서 HUD와 필드 레이아웃을 직접 편집 가능. `TweenClock`·`SpriteFactory`·`HudView` 삭제 |
| **P4 데이터** ✅ | `GameDataAsset` id, `GameDatabase`, `RunConfig`, 스테이지 그리드 페인팅 인스펙터, 데이터 검증 메뉴·테스트, (세이브 구조) | 새 스테이지를 인스펙터에서 칠해서 만들고 바로 플레이 가능 |

> P1을 먼저 하는 이유: 규칙이 확정되어야 P2의 이벤트 모양(`EnemyPlanChanged`, `UnitAttacked(Cells)`)과 P3의 표시 요소가 정해진다.

### P1 완료 (2026-09-30)

- **새 파일**: `Data/TargetPattern.cs`(모양 enum 이동 + 카드·적 공용 범위), `Logic/EnemyTurnPlan.cs`(`EnemyAction`, `EnemyTurnPlan`), `Logic/EnemyPlanner.cs`(`EnemyPlanner`, `EnemyActionResolver`), `Tests/EditMode/EnemyPlanTests.cs`
- **삭제**: `Logic/EnemyAI.cs`, `Logic/EnemyIntent.cs`
- **`BattleState`**: `Intents`/`IntentsChanged`/`RefreshIntents` → `EnemyPlan`/`EnemyPlanChanged`/`RefreshEnemyPlan`. `CloneForSimulation()`, `Revision`(계획이 최신인지 검사, 오래됐으면 경고 후 다시 계획), `EnemiesInActionOrder`, `TryMoveAlongPath`·`AttackCell`(카드와 적 공용). `Create(..., seed)`로 행동 순서 난수 시드 주입
- **`UnitData`**: `attackShape`(기본 Adjacent — 기존 에셋은 값이 없어서 자동으로 근접), `actionPriority`(0 = 미지정)
- **임시 표시**(P3에서 프리팹으로 교체): 공격 칸 빨강 바닥, 의도 배지 `① 이동 2 / 공격 7`, 플레이어 머리 위 `피해 N (방어 M) / HP a→b`, 적 호버 시 이동 범위(파랑) + 공격만 닿는 칸(분홍) + 오른쪽 위 스탯
- **경로 표시 개선**(`View/PlanOverlay.cs`): 처음엔 주황 점·사각형이었으나 "누가 어디로 가는지"가 안 읽혀서 교체 → 적 색 경로 선 + 화살촉, 도착 칸 잔상(글자 + 순서 번호), 빨간 공격 화살표, 호버한 적만 선명하게(나머지 흐리게). 위험 지역을 "그 적 차례 시점의 격자" 기준(`EnemyPlanner.GetThreatAreaAtTurn`)으로 바꿔 계획된 도착 칸이 범위 밖에 보이던 모순을 없앰 (테스트 `AssertPlanInsideThreatAreas`)
- **잔상 가려짐 수정**: 도착 칸에 지금 다른 유닛이 서 있으면(먼저 비켜 줄 적) 잔상이 가려졌음 → 잔상에 적 색 테두리(유닛 몸체 위·HP 바 아래)와 왼쪽 아래 순서 배지(맨 위)를 추가
- **이동 중 겹침 수정**: 연출이 유닛별 큐라 적들이 동시에 움직여 서로 겹쳤음 → `View/AnimationQueue.cs` 전역 큐로 로직 이벤트 순서 그대로 한 단계씩 재생(앞 적의 이동·공격이 끝나야 다음 적 시작). 재생 중엔 다음 턴 계획 표시를 숨겼다가 끝나면 표시. `TweenClock` 삭제(연출 도중 유닛이 파괴되면 카운터가 안 내려가 입력이 영구 잠길 수 있던 문제도 같이 해소 — `AnimationQueue.Abort`). P3 `BattleEventPlayer`의 초기 형태
- **테스트**: 42개 → 98개(파라미터 시드 포함) 전부 통과. Play 모드에서도 표시된 계획과 실제 이동이 일치하는 것을 확인
- 스테이지 전투는 시드를 넘기지 않으므로 전투마다 동점 순서가 달라진다 (스테이지 안에서는 고정)

### P2 완료 (2026-09-30)

- **구조**: §2.1~2.3 참고. `Logic/` 폴더를 `Battle/`, `Battle/AI/`, `Battle/Effects/`, `Run/`으로 분리. v0 `BattleState`(상태+턴+카드+효과 switch+데미지+이벤트 9종)가 `BattleState`(상태) / `BattleController`(진행) / `BattleRules`(규칙) / 효과 클래스 4개 / 이벤트 레코드로 나뉨
- **카드 에셋 마이그레이션**: `shape/range/requiresEnemy` → `targeting`(TargetPattern), enum 구조체 `effects` → `[SerializeReference] List<CardEffect>`. 필드 타입이 바뀌어 자동 이전이 안 되므로, 작업 전에 YAML에서 읽어 둔 값으로 10장 모두 다시 기록했다. 자동 생성 설명이 이전과 같은 것을 확인
- **CardData 인스펙터**(`Editor/CardDataEditor.cs`): "＋ 효과 추가" 메뉴(효과 클래스 자동 수집), ▲▼ 순서 변경, ✕ 삭제, 카드 설명 미리보기
- **뷰 적응**: `BattleRunner`가 `BattleController`를 들고 이벤트 스트림을 `switch`로 뷰에 연결. `UnitView`는 스냅샷 값으로 재생(`PlayDamaged`, `PlayBlockChanged`). 승패 배너·다음 흐름은 마지막 연출 뒤에 표시. 보상 뽑기는 `RewardPicker`(난수 주입)
- **규칙 변화**: 플레이어 턴이 아니면 카드를 쓸 수 없음(`"플레이어 턴이 아님"`). 카드 피해도 적 공격과 같이 **적대 유닛에게만** 적용(`BattleRules.AttackCell`)
- **테스트**: 98개 → 103개 전부 통과 (콤보 카드 효과 순서, 설명 자동 생성, 턴 상태머신, 플레이어 턴 외 카드 사용 금지, RewardPicker 추가). Play 모드에서 실제 카드 사용·4턴 진행 후 화면 HP와 로직 HP 일치 확인
- 작업 전 백업: 세션 scratchpad `backup_pre_p2/` (프로젝트가 git 저장소가 아님)

### P3 완료 (2026-09-30)

- **씬**: `Battle.unity`를 §3.2 하이어라키로 다시 구성. HUD(Canvas)·필드 레이아웃을 씬에서 직접 편집한다. `HUD Canvas/BoardArea`를 옮기거나 크기를 바꾸면 `CameraFramer`가 에디터에서도 카메라를 맞춘다. `Board` 인스펙터의 **스테이지 미리보기**로 `previewStage`의 타일·유닛을 깔아 볼 수 있음 (DontSave, Play 진입 시 자동 삭제)
- **프리팹**(`_Project/Prefabs`): Board/{Tile, PlanSegment, PlanArrowHead, PlanGhost}, Units/{Unit, Unit_Player, Unit_Enemy, FloatingText}, UI/Card. 연출 값(이동 속도, 런지, 흔들림, 색, 두께, 알파)은 모두 인스펙터 필드. `UnitData.viewPrefab` 추가
- **스크립트**(`View/Board`, `View/Units`, `View/Hud`, `View/Systems`, `Input`): 삭제 — `BattleRunner`, `GridView`, `PlanOverlay`, `UnitView`(v0), `HudView`(+`CardView`, `UI`), `RewardView`, `RunRunner`, `FloatingText`(v0), `AnimationQueue`, `SpriteFactory`, `BattleInputController`
- **연출 큐**: 정적 `AnimationQueue` → 씬 컴포넌트 `BattleEventPlayer`. 각 단계는 Tween을 돌려주고 `OnKill`로 다음 단계로 넘어가므로, 연출 도중 유닛이 파괴돼도 큐가 멈추지 않는다 (`Abort` 불필요). `playbackSpeed` 필드로 연출 속도 조절
- **텍스트**: 전부 TextMeshPro. `_Project/Fonts/Warhaven-{Regular,Bold} SDF`(Dynamic, 2048 멀티 아틀라스), TMP 기본 폰트로 지정, `→ ▲ ▼`는 LiberationSans 폴백. `①` 같은 원문자는 두 폰트 모두 없어서 순서 번호는 **원 배지 + 숫자**로 표시. 원본 TTF는 `Resources`에서 `_Project/Fonts`로 이동
  - Dynamic 폰트 에셋은 플레이할 때마다 아틀라스가 채워져 수 MB가 되므로, `Editor/DynamicFontSaveCleaner`가 저장 직전에 비운다 (저장소에는 항상 빈 ~6KB 상태)
- **행동 미리보기**(§1.5): `BattleController.PreviewCard(hand, cell)` → `CardPreview(State, Plan)`. 실제 사용과 같은 `ApplyCard`를 복사본에 적용하므로 미리보기 = 사용 후 계획 (테스트 `ActionPreviewTests`: 모든 대상 칸·시드에서 일치, 실제 상태·이벤트 불변, 처치 시 그 적 행동 제거). 표시: 적 경로·잔상·말풍선이 바뀐 계획으로, 플레이어가 옮겨 갈 칸에 플레이어 잔상, 쓰러질 적은 "처치". `P` 키로 켜고 끔 (`PlayerTurnInput.actionPreview`)
- **그 밖의 표시**: `Alt`를 누르고 있으면 전체 위험 지역(모든 적의 공격 가능 칸 합집합). 유닛 툴팁(오른쪽 위)은 플레이어도 표시 — 적 턴에 받을 피해 내역. 의도·피해 예고 말풍선은 **한 줄**로 줄여 자기 칸 안에 들어가게 함 (위아래로 붙은 유닛끼리 말풍선이 HP 바를 가리던 문제)
- **확인**: 테스트 103개 → 115개 전부 통과. Play 모드에서 카드 사용·행동 미리보기·3턴 진행(화면 위치와 로직 위치 일치)·승리 → 보상 선택 → 2스테이지(9x6) 시작과 카메라 재배치까지 확인
- **남은 것**: 카드 UI 호버로 `Self` 카드(방어 등) 미리보기, 유닛 이름이 영문 데이터(`Knight`, `Slime`) → P4 데이터 정리 때 한글화, 실제 창 포커스 상태에서의 연출 템포 확인

### P4 완료 (2026-09-30)

- **id**: 모든 데이터 SO가 `GameDataAsset`을 상속하고 `id`를 가진다. 에셋을 만들거나 복제하면 파일 이름으로 자동 부여 (`Card_EnergyAwakening` → `card_energy_awakening`, 복제본은 `_2` 등으로 새 id — `Editor/GameDataIdAssigner`). 파일 이름을 바꿔도 id는 그대로. 기존 에셋 18개에 id를 붙이고 유닛 이름을 한글로 바꿈 (기사·슬라임·고블린)
- **GameDatabase**(`Data/GameDatabase.asset`): 카드·유닛·스테이지·덱·보상 풀 목록 + `Get<T>(id)`. 데이터 에셋이 추가·삭제·이동되면 자동으로 다시 모은다 (`Editor/GameDatabaseBuilder`, `EditorApplication.delayCall`이라 에디터 창이 비활성이면 다음 갱신 때 반영)
- **RunConfig**(`Data/RunConfig.asset`): 플레이어·스테이지 순서·시작 덱·보상 풀·손패/에너지/보상 선택지 수. `RunController`는 이것만 참조. **플레이어 유닛이 `StageData`에서 `RunConfig`로 이동** — 스테이지에는 시작 위치만 남음 (`SpawnFromStage(stage, player, hp)`). `BattleBootstrap`의 단독 테스트 필드는 없애고 `BeginBattle(RunState)` 하나로
- **시드**: `RunState.Seed` + 스테이지별 파생 시드(`StageSeed(BattleSalt / RewardSalt)`)로 행동 순서 동점과 보상 선택지를 정한다 → 시드와 스테이지 번호만 저장하면 같은 결과가 재현된다
- **스테이지 페인팅 인스펙터**(`Editor/StageDataEditor`): §4.2. 드래그 한 번이 되돌리기(Ctrl+Z) 한 번. 크기를 줄이면 잘려 나갈 배치를 알려 주고 확인 후 잘라냄. 오류 칸은 빨간 테두리. **▶ 이 스테이지만 플레이** — Battle 씬을 열고 `RunConfig`의 플레이어·시작 덱으로 그 스테이지 한 판만 진행 (`SessionState` → `RunController`, 한 번만 적용). **씬 보드에 미리보기** — `BoardView` 미리보기로 깔기 (`BoardView.previewPlayer` 추가)
- **검증**: `StageData.Validate`에 몬스터 팀 검사·몬스터 없음 추가, `RunConfig.Validate`, 전체 검사 `DataValidator` + 메뉴 `ProvidenceKnight/Validate All Data`(콘솔 항목을 누르면 해당 에셋으로 이동). 테스트 어셈블리가 `ProvidenceKnight.Editor`를 참조해 메뉴와 같은 검사(`ProjectDataValidator.Run`)를 돌린다
- **세이브 구조**: `RunSaveData { version, stageIndex, hasPlayerHp, playerHp, maxEnergy, deckCardIds[], rngSeed }`, `RunState.ToSaveData` / `TryFromSaveData(data, RunConfig, GameDatabase)`, `RunSaveStore`(persistentDataPath JSON). 스테이지 사이에서만 저장하는 전제. 버튼은 아직 연결하지 않음
- **확인**: 테스트 115개 → 133개 전부 통과. 인스펙터의 "이 스테이지만 플레이"로 Stage_01 한 판 시작 확인, 칠하기 규칙(플레이어 칸 보호, 장애물→몬스터 대체, 몬스터 교체, 지우개)과 칠한 스테이지로 전투 시작 확인
- **남은 것**: 카드 UI 호버로 `Self` 카드 미리보기, 실제 창 포커스 상태에서의 연출 템포 확인, 이어하기 UI(세이브 연결), 카드 이름 필드 `cardName`과 유닛 `displayName` 이름 통일(선택)

## 상태이상과 턴 제한 턴 시작 효과 (feature/cardSpecialEffect)

**상태이상** (`Battle/Status.cs`, `ApplyStatusEffect`, `Unit.GetStatus`). 지속 시간은 "그 유닛의 턴" 단위.

| 상태 | 효과 | 지속 |
|---|---|---|
| 출혈 | 자기 턴 시작마다 강도만큼 피해, **방어도 무시** | 턴 수, 겹치면 강도 합 / 턴은 긴 쪽 |
| 화상 | 위와 같지만 **방어도가 막아줌** | 위와 같음 |
| 독 | 턴 시작마다 강도만큼 피해(방어도 무시) 후 강도 1 감소 | 강도가 0 이 될 때까지, 겹치면 강도 합 |
| 기절 | 그 턴 카드 사용 불가 (몬스터는 의도 "기절") | 턴 수 (긴 쪽) |
| 빙결 | 이동 효과가 있는 카드 사용 불가 | 턴 수 |
| 암흑 | 직선/범위 모양에 사거리 2 이상인 카드 사용 불가 (`TargetPattern.IsRanged`) | 턴 수 |

- 피해는 공격 무효화를 무시한다. 같은 카드의 피해가 무효화돼도 상태이상은 걸린다.
- 플레이어: 턴 시작에 피해(방어도 초기화 **전**), 턴 종료에 기절/빙결/암흑 1 감소.
- 몬스터: 적 턴이 시작될 때 전원 피해(`BattleRules.RunEnemyTurnStart`, 계획 시뮬레이션도 똑같이 먼저 실행 → 이 피해로 쓰러지면 계획에서 빠짐), 행동이 끝나면 제어 상태 1 감소.
- 사용 가능 여부는 `StatusRules.CanUse` 한 곳 (플레이어 `CanSelectCard`, 몬스터 `EnemyPlanner` 공용).
- `ApplyStatusEffect`: 기본은 대상 칸의 적에게(= Attack 종류), `onSelf` 면 시전자에게(턴 시작 효과에 사용 가능).

**턴 시작 효과 턴 제한** (`UnitData.turnStartEffects` = `TurnStartEntry` 목록): 효과마다 `turns` (0 = 전투 내내, N = 전투 N턴째까지). `BattleState.Turn` 기준.

# Providence Knight 2D — 핵심 시스템 설계 (프로토타입 v0)

> 목적: **재미 검증용 최소 전투 루프**를 빠르게 만든다. 연출·메타 진행·세이브는 범위 밖.
> 환경: Unity 6000.3 / URP 2D / 싱글 플레이.

---

## 1. 게임 루프 요약

```
[전투 시작] 스테이지 데이터 로드 → 필드 생성 → 몬스터/플레이어 배치
     │
     ▼
┌─▶ [플레이어 턴 시작]  에너지 회복, 방어도 초기화, 적 의도(Intent) 갱신
│        │
│        ▼
│   [플레이어 행동]  카드 선택 → 대상 칸 선택 → 에너지 소모 → 효과 실행   (반복)
│        │  (턴 종료 버튼 / 에너지·카드 소진)
│        ▼
│   [플레이어 턴 종료]  (손패는 유지)
│        │
│        ▼
│   [적 턴]  모든 적이 순서대로 한 번에 행동 (이동 → 공격)
│        │
│        ▼
│   [승패 판정]  적 전멸 = 승리 / 플레이어 HP 0 = 패배
└────────┘
```

- **선공권은 항상 플레이어.** 적은 플레이어 턴이 끝난 뒤 몰아서 행동한다.
- 권장: 적의 **다음 행동(의도)을 플레이어 턴에 미리 표시** (Slay the Spire / Into the Breach 방식). 
  "어디로 피할지, 막을지, 먼저 죽일지"를 고민하게 만드는 게 이 장르 재미의 핵심이라, 재미 검증 단계부터 넣는 걸 추천.

---

## 2. 핵심 규칙 (v0 기본값 — 조정 대상)

| 항목 | 기본값 | 비고 |
|---|---|---|
| 플레이어 유닛 수 | 1 | 이후 파티로 확장 가능하게 `Unit` 공통화 |
| 에너지 | 턴당 3, 이월 없음 | |
| 카드 순환 | **손패 5장 고정 + 덱은 순서 고정 큐(셔플 없음)**. 턴 중 카드를 쓰면 그 카드는 덱 맨 아래로, 덱 맨 위 카드가 손으로 들어옴(손패 유지). **턴이 끝나면 남은 손패를 전부 덱 맨 아래로 버리고 덱 맨 위 5장을 새로 뽑음** | 덱 순서 자체가 전략 요소. 다음 손패(덱 맨 위 5장)를 미리 보여줘 턴 종료 타이밍을 계획하게 함 |
| 방어도(Block) | 피해를 먼저 흡수, **내 턴 시작 시 0으로 초기화** | 확정 |
| 이동 | 카드로만 가능, v0는 4방향(상하좌우) 칸 단위, 유닛/장애물 통과 불가 | **대각선은 추후 추가** → `GridMap`의 이웃 방향을 설정값으로 둠 |
| 공격 범위 | 카드마다 패턴(오프셋 목록)으로 정의 | 근접 1칸, 직선 3칸, 십자 등 |
| 적 행동 | 의도 1개 결정 → 적 턴에 실행 | 이동 후 공격 가능 |

---

## 3. 아키텍처 원칙

1. **로직과 표현 분리** — 전투 규칙은 순수 C# 클래스(`BattleState` 등)로, MonoBehaviour는 화면 표시·입력만 담당.
   → 규칙을 EditMode 테스트로 빠르게 검증 가능, 연출을 나중에 갈아끼우기 쉬움.
2. **데이터 주도** — 카드/몬스터/스테이지는 ScriptableObject. 필드 크기·몬스터 배치 변경 = 에셋만 수정.
3. **이벤트 → 연출 큐** — 로직은 즉시 계산하고 `BattleEvent`(Moved, Damaged, Died…)를 발행.
   View는 이벤트를 큐에 쌓아 순서대로 애니메이션 재생. (적 턴 "몰아서 진행"을 깔끔하게 보여주는 핵심)
4. **카드 효과 = 효과 조합** — 카드 하나는 `CardEffect` 리스트. "이동 후 공격", "공격 + 방어" 카드를 코드 추가 없이 조합.

```
┌──────────── Data (ScriptableObject) ────────────┐
│ CardData   UnitData(Monster/Player)   StageData  │
└──────────────────────┬──────────────────────────┘
                       ▼
┌──────────── Logic (순수 C#) ─────────────────────┐
│ BattleState ─ GridMap ─ Unit ─ Deck/Hand         │
│ TurnController (상태머신)   EnemyAI               │
│ CardEffect.Resolve() ──▶ BattleEvent 발행         │
└──────────────────────┬──────────────────────────┘
                       ▼  (이벤트 구독)
┌──────────── Presentation (MonoBehaviour) ────────┐
│ BattleRunner(진입점)  GridView  UnitView          │
│ HandView/CardView  IntentView  EventPlayer(큐)    │
│ InputController (카드 클릭 → 타일 클릭)           │
└──────────────────────────────────────────────────┘
```

---

## 4. 데이터 정의

### StageData
```csharp
[CreateAssetMenu] class StageData : ScriptableObject {
    public int width, height;                 // 필드 크기 가변
    public Vector2Int playerStart;
    public List<Vector2Int> blockedTiles;     // 장애물
    public List<MonsterSpawn> monsters;       // 몬스터 배치도
}
[Serializable] struct MonsterSpawn { public UnitData data; public Vector2Int pos; }
```
> 배치도는 에디터에서 좌표 입력 → 추후 필요하면 그리드 클릭식 커스텀 에디터 추가.

### UnitData
```csharp
class UnitData : ScriptableObject {
    public string displayName; public Sprite sprite;
    public int maxHp;
    public List<EnemyActionData> actionPattern;   // 몬스터 전용: 순환 or 가중치 랜덤
}
```

### CardData
```csharp
class CardData : ScriptableObject {
    public string cardName; public Sprite art; [TextArea] public string desc;
    public int cost;
    public TargetRule target;                 // Self / Tile / Unit, + 범위 패턴
    [SerializeReference] public List<CardEffect> effects;
}
```

### CardEffect (조합형)
```csharp
[Serializable] abstract class CardEffect {
    public abstract void Resolve(BattleContext ctx, Unit caster, Vector2Int target);
}
class MoveEffect   : CardEffect { public int range; }          // 대상 칸으로 이동(경로 탐색)
class AttackEffect : CardEffect { public int damage; public List<Vector2Int> areaOffsets; }
class BlockEffect  : CardEffect { public int amount; }
// 이후: PushEffect(넉백), DrawEffect, StatusEffect ...
```

**v0 스타터 덱 예시 (10장)**

| 카드 | 코스트 | 효과 |
|---|---|---|
| 이동 ×3 | 1 | 2칸 이내 이동 |
| 베기 ×3 | 1 | 인접 1칸 6 피해 |
| 방어 ×3 | 1 | 방어도 5 |
| 돌진 ×1 | 2 | 직선 3칸 이동 후 인접 적 8 피해 |

---

## 5. 런타임 로직

| 클래스 | 책임 |
|---|---|
| `GridMap` | 칸 정보(통과 가능/점유 유닛), 좌표 유효성, BFS 경로·도달 범위 계산 |
| `Unit` | HP, Block, 위치, 팀(Player/Enemy), `TakeDamage()` |
| `CardCycle` | 손패(5칸) + 덱 큐. `Cycle(handIndex)` → 사용 카드는 덱 맨 아래, 덱 맨 위 카드를 손으로. `DiscardHandAndRefill()` → 손패 전부 버리고 덱 맨 위 5장으로 교체 (턴 종료 시) |
| `BattleState` | 위 전부 + 에너지, 턴 수, 승패 상태 보관 |
| `TurnController` | 상태머신: `PlayerTurnStart → PlayerAction → PlayerTurnEnd → EnemyTurn → CheckEnd` |
| `EnemyAI` | 의도 결정(`PlanIntent`) / 실행(`ExecuteIntent`). v0: 사거리 안이면 공격, 아니면 플레이어 쪽으로 이동 |
| `BattleEvents` | `OnUnitMoved, OnDamaged, OnBlockChanged, OnDied, OnCardPlayed, OnTurnChanged …` |

**카드 사용 흐름**
```
CardView 클릭 → InputController: 사용 가능? (에너지, 유효 대상 존재)
  → GridView에 유효 타일 하이라이트 (TargetRule + GridMap 계산)
  → 타일 클릭 → BattleState.PlayCard(card, target)
      → 에너지 차감 → effects 순차 Resolve → 이벤트 발행 → 카드 버린 더미로
  → EventPlayer가 연출 재생 (재생 중 입력 잠금)
```

---

## 6. 폴더 구조

```
Assets/_Project/
  Scripts/
    Data/          CardData, UnitData, StageData, CardEffect*
    Logic/         GridMap, Unit, Deck, BattleState, TurnController, EnemyAI, BattleEvents
    View/          BattleRunner, GridView, UnitView, HandView, CardView, IntentView, EventPlayer
    Input/         BattleInputController
  Data/
    Cards/  Monsters/  Stages/
  Prefabs/  Sprites/  Scenes/Battle.unity
  Tests/EditMode/  (GridMap, 데미지/방어도, 턴 흐름 테스트)
```

---

## 7. 구현 마일스톤 (재미 검증까지)

| 단계 | 내용 | 완료 기준 | 상태 |
|---|---|---|---|
| **M1 필드** | StageData → 그리드 생성, 유닛 스폰, 칸 클릭 좌표 인식 | 스테이지 에셋 바꾸면 크기/배치가 바뀜 | ✅ 완료 |
| **M2 카드** | 덱/손패/에너지, 이동·공격·방어 카드, 타겟 하이라이트 | 내 턴에 카드로 이동·공격 가능 | ✅ 완료 |
| **M3 턴** | 턴 상태머신, 적 AI 몰아서 행동, 승패 판정 | 한 판이 처음부터 끝까지 돌아감 | ✅ 완료 |
| **M4 가독성** | HP/방어도 UI, 이동·공격·피격·사망 트윈, 의도 라벨 겹침 정리 | 남이 플레이해도 상황이 읽힘 | ✅ 완료 |
| **M5 런 진행** | 스테이지 3개를 잇는 런, 체력 이어받기, 보상 카드 선택 | 스테이지1→보상→스테이지2→보상→스테이지3→런 클리어가 한 세션에서 돌아감 | ✅ 완료 |

> 그래픽은 색 네모 + 텍스트로 충분.
> **M3 구현 메모**
> - 적 행동은 **의도 공개**: 사거리 안이면 공격, 아니면 대상과의 거리를 줄이는 칸으로 이동(맨해튼 거리 기준, 필요하면 이동 후 공격까지 한 번에). 플레이어 턴 시작과 카드 사용 직후마다 다시 계산해서 항상 최신 상태를 보여줌.
> - 적 턴은 살아있는 적을 스폰 순서(Id)대로 한 번씩 실행. 각 적은 실행 시점에 즉시 재계산하므로 앞선 적의 이동으로 길이 막히면 자연스럽게 다른 경로를 탐색함.
> - 승패는 `BattleState.BattleEnded` 이벤트로 알림. 플레이어 사망 또는 적 전멸 시 손패/턴 종료 UI가 잠기고(`HudView.SetInteractable(false)`) 결과 배너가 계속 표시됨.
>
> **M4 구현 메모**
> - **DOTween**(`Assets/Plugins/Demigiant/DOTween`, 이미 프로젝트에 임포트되어 있던 것 사용)으로 연출. `SpriteRenderer.DOColor` 등 "Modules" 폴더의 확장 메서드는 asmdef 없는 loose 스크립트라 `ProvidenceKnight.asmdef`에서 안 보여서, 대신 핵심 `DOTween.To`로 직접 구현(색상 보간 등). Transform 계열(`DOMove`, `DOShakePosition`, `DOScale`, `DORotate`)은 DOTween 코어(DLL)에 있어 바로 사용 가능.
> - **유닛별 연출 큐**([UnitView.cs](../Assets/_Project/Scripts/View/UnitView.cs)): 이동 → 공격 런지 → 피격 반응 → 사망을 유닛마다 순서대로 재생. 서로 다른 유닛끼리는 병렬로 재생되어 적 여러 마리가 "몰아서" 움직이는 턴 흐름과 맞음. DOTween Sequence를 재생 중에 동적으로 이어 붙이는 건 지원되지 않아, 자체 `Queue<Action<Action>>` 로 순서를 보장.
> - **HP 바**: `DOTween.To`로 채움 비율을 부드럽게 보간, 피해를 입으면 몸체 색 플래시 + 흔들림 + 빨간 "-N" 팝업, 방어도를 얻으면 파란 뱃지 펀치 + "+N" 팝업.
> - **입력 잠금**: `TweenClock`(정적 카운터)으로 현재 재생 중인 연출 수를 세고, 연출 중엔 카드 선택·턴 종료 입력을 막음(`BattleRunner`, `HudView` 양쪽에서 체크).
> - **의도 라벨 겹침 수정**: 배경 판 추가 + "이동+공격 N" → 2줄("이동\n공격 N")로 줄여 가로 폭을 줄임. 실제 플레이로 확인 완료.
> - **테스트 환경 주의사항**: 이 MCP 자동화 환경은 Unity 에디터 창이 실제 OS 포커스를 갖지 못해 Play 모드의 프레임 진행이 멈추고(`Time.time`이 전혀 증가하지 않음), DOTween 트윈도 함께 멈춘다. 실제로 사용자가 포커스를 가진 채 플레이할 때는 정상 동작하며, `DOTween.ManualUpdate`로 시간을 강제로 흘려보내 큐/트윈 완료 로직 자체는 검증했다(이동→런지→피격→사망까지 정상적으로 순서대로 완료, HP/방어도/처치 로직도 모두 정확).
>
> **M5 구현 메모**
> - **런 구조**: `RunState`(순수 C#)가 스테이지 목록·현재 인덱스·덱(보상 포함)·체력·최대 에너지를 들고 있음. `BattleState`는 여전히 "한 판"만 알고, `RunState`가 매 스테이지 시작 시 필요한 값(체력, 덱, 최대 에너지)을 넘겨줌 — 로직 계층 분리 원칙 유지.
> - **체력 이어받기**: 자동 회복 없음. `Unit`/`BattleState.SpawnFromStage`에 `currentHp` 오버라이드를 추가해, 스테이지가 끝난 그대로의 체력으로 다음 스테이지가 시작됨(최대 체력을 넘지 않도록 clamp).
> - **보상 카드**: 스테이지 클리어 시(마지막 스테이지 제외) `RewardPoolData`에서 무작위 3장을 뽑아 `RewardView`(전체화면 오버레이)로 제시. 고른 카드는 런의 덱 맨 뒤에 추가됨(건너뛰기도 가능).
> - **"적당히 좋은" 보상 카드 설계**: ① 단순 상위호환(강타 2코스트 12뎀 vs 베기 1코스트 6뎀 — 코스트당 효율만 약간 좋음), ② 강하지만 **이번 전투 1회용**(필살기, `exhaust` 플래그로 사이클에서 제외), ③ 사용자가 예로 든 형태 — **기력 각성**(2코스트, 최대 에너지 +1을 이번 런 내내 유지하지만 `exhaust`라 같은 전투에서 반복 재생 불가, 스테이지가 바뀌면 다시 손에 들어와 또 쓸 수 있음). `CardData.exhaust` + `CardEffectType.EnergyUp`으로 구현.
> - **BattleRunner 재사용**: 씬을 새로 안 만들고 `BattleRunner.BeginBattle(...)`을 스테이지마다 다시 호출해 그리드/유닛/HUD를 갈아끼움. `RunRunner`가 스테이지 진행을 몰고, `BattleRunner.BattleFinished` 이벤트로 결과를 relay.
> - **버그로 배운 것**: `RewardView`를 `BattleRunner`와 같은 GameObject에 컴포넌트로 얹었는데, `Show()/Choose()`에서 `gameObject.SetActive()`를 호출하면 그게 RewardView 자신이 아니라 그 GameObject(=전투 루트) 전체를 꺼버려서 전투 전체가 멈추는 버그가 있었음 → 반드시 오버레이 캔버스를 별도 자식 GameObject로 만들어 그 자식만 토글하도록 수정.

---

## 8. 재미 검증 시 볼 포인트

- 매 턴 **"어떤 카드를 어떤 순서로 쓸까"** 고민이 생기는가? (자명한 최선수가 있으면 실패 신호)
- 이동 카드가 **공격·방어만큼 가치 있는가?** (위치 선정이 의미 있어야 그리드를 쓰는 이유가 됨)
- 적 몰아서 행동이 **예측 가능하면서도 위협적인가?**
- 필드 크기/배치 변화만으로 **다른 퍼즐처럼 느껴지는가?**
- 체력이 안 회복되고 이어지는 게 **다음 스테이지 진입을 긴장되게 만드는가**, 아니면 그냥 억울하기만 한가?
- 보상 카드 3장 중 고르는 순간이 **실제로 고민되는가**, 아니면 항상 같은 카드만 고르게 되는가?

---

## 9. 미결정 사항 (정하면 설계 업데이트)

1. 플레이어 유닛은 1명? 여러 명(파티)?
2. 필드 기믹(함정, 지형 효과, 넉백 충돌 피해) 넣을지
3. 몬스터 종류별 사거리(원거리 몬스터 등) 추가 여부 — v0는 전부 근접(사거리 1)
4. 런 도중 패배 시 재시작/이어하기 흐름 — v0는 패배하면 그냥 끝(재시작 UI 없음)
5. 스테이지 사이 회복 수단(휴식 카드, 소량 자동 회복 등)을 넣을지 — v0는 완전 무회복

**확정됨**: 카드 = 손패 5장 + 순서 고정 덱(순환) / 방어도 = 내 턴 시작 시 초기화 / 대각선 이동 = 추후 추가 / 적 의도 = 공개 / 스테이지 간 체력 = 이어받음(무회복) / 보상 카드 = 스테이지 클리어 후 3장 중 1장 선택(건너뛰기 가능)

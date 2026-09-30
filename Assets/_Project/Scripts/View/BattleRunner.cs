using System;
using System.Collections.Generic;
using System.Linq;
using ProvidenceKnight.Battle;
using ProvidenceKnight.Battle.AI;
using ProvidenceKnight.Battle.Effects;
using ProvidenceKnight.Data;
using ProvidenceKnight.Input;
using UnityEngine;

namespace ProvidenceKnight.View
{
    /// <summary>
    /// 전투 한 판의 진행자. BeginBattle 로 몇 번이고 새 스테이지를 시작할 수 있다 (RunRunner 가 스테이지마다 호출).
    /// 인스펙터에 StageData/DeckData 가 지정되어 있으면 단독 씬 테스트용으로 Start() 에서 스스로 한 판을 시작한다.
    /// BattleController 의 이벤트 스트림을 뷰 연출로 옮기고, 카드 선택 → 대상 칸 선택 → 사용 흐름을 관리한다.
    /// </summary>
    public class BattleRunner : MonoBehaviour
    {
        [Header("Data (단독 테스트용 - RunRunner 가 있으면 비워둬도 됨)")]
        [SerializeField] StageData stage;
        [SerializeField] DeckData deck;
        [SerializeField, Min(1)] int handSize = 5;
        [SerializeField, Min(0)] int maxEnergy = 3;

        [Header("Scene")]
        [SerializeField] GridView gridView;
        [SerializeField] HudView hud;
        [SerializeField] BattleInputController input;
        [SerializeField] Camera cam;
        [SerializeField] float cameraPadding = 0.6f;

        [Header("Highlight Colors")]
        [SerializeField] Color moveHighlight = new(0.3f, 0.6f, 1f, 0.45f);
        [SerializeField] Color attackHighlight = new(1f, 0.3f, 0.25f, 0.5f);
        [SerializeField] Color selfHighlight = new(0.4f, 1f, 0.5f, 0.4f);
        [SerializeField] Color dangerMoveHighlight = new(0.3f, 0.55f, 1f, 0.3f);
        [SerializeField] Color dangerAttackHighlight = new(1f, 0.25f, 0.6f, 0.45f);

        public BattleController Battle { get; private set; }
        public BattleState State => Battle?.State;

        /// <summary>이 판이 끝났을 때 (연출이 모두 끝난 뒤). RunRunner 가 구독해 다음 스테이지/보상 흐름을 진행한다.</summary>
        public event Action<bool> BattleFinished;

        readonly Dictionary<Unit, UnitView> _unitViews = new();
        Transform _unitRoot;
        int _selectedCard = -1;
        List<Vector2Int> _validTargets;
        Vector2Int? _hoveredCell;
        bool _planPending;   // 연출이 끝나면 적 계획을 다시 보여줘야 함
        bool _wired;

        void Start()
        {
            WireOnce();
            if (stage != null && deck != null)
                BeginBattle(stage, deck.cards, handSize, maxEnergy, null);
        }

        /// <summary>hud/input 배선은 씬 통틀어 한 번만 하면 된다 (스테이지가 바뀌어도 그대로 재사용).</summary>
        void WireOnce()
        {
            if (_wired) return;
            _wired = true;

            if (cam == null) cam = Camera.main;
            hud.Build(handSize);
            hud.CardClicked += OnCardClicked;
            hud.EndTurnClicked += OnEndTurnClicked;

            input.TileHovered += OnTileHovered;
            input.TileClicked += OnTileClicked;
            input.Cancelled += Deselect;

            _unitRoot = new GameObject("Units").transform;
            _unitRoot.SetParent(transform, false);
        }

        /// <summary>새 스테이지를 시작한다. deckCards 순서 그대로 손패/덱을 구성한다.</summary>
        public void BeginBattle(StageData stageData, IReadOnlyList<CardData> deckCards, int handSizeParam, int maxEnergyParam, int? playerHpOverride)
        {
            WireOnce();
            CleanupPreviousBattle();

            stage = stageData;
            handSize = handSizeParam;
            maxEnergy = maxEnergyParam;

            Battle = BattleController.Create(stageData, deckCards, handSizeParam, maxEnergyParam);
            gridView.Build(State.Grid);
            Battle.Events.Emitted += OnBattleEvent;

            Battle.SpawnFromStage(stageData, playerHpOverride);

            FitCamera();
            Battle.StartPlayerTurn();
            Debug.Log($"[BattleRunner] '{stageData.name}' {stageData.width}x{stageData.height}, 몬스터 {stageData.monsters.Count}마리, 덱 {deckCards.Count}장, 시작체력={(playerHpOverride?.ToString() ?? "풀피")}");
        }

        void CleanupPreviousBattle()
        {
            if (Battle != null)
            {
                Battle.Events.Emitted -= OnBattleEvent;
                Battle = null;
            }

            foreach (var view in _unitViews.Values)
                if (view != null) Destroy(view.gameObject);
            _unitViews.Clear();

            _selectedCard = -1;
            _validTargets = null;
            _planPending = false;
            hud.ResetForNewBattle();
        }

        void OnDestroy()
        {
            if (Battle != null) Battle.Events.Emitted -= OnBattleEvent;
            if (hud != null)
            {
                hud.CardClicked -= OnCardClicked;
                hud.EndTurnClicked -= OnEndTurnClicked;
            }
            if (input != null)
            {
                input.TileHovered -= OnTileHovered;
                input.TileClicked -= OnTileClicked;
                input.Cancelled -= Deselect;
            }
        }

        // ---------------- 이벤트 스트림 → 뷰 ----------------

        void OnBattleEvent(BattleEvent e)
        {
            switch (e)
            {
                case UnitSpawned s:
                    _unitViews[s.Unit] = UnitView.Create(s.Unit, gridView, _unitRoot);
                    break;
                case UnitMoved m:
                    ViewOf(m.Unit)?.PlayMove(m.Path);
                    break;
                case UnitAttacked a:
                    ViewOf(a.Attacker)?.PlayAttackLunge(gridView.GridToWorld(a.Cell));
                    break;
                case UnitDamaged d:
                    ViewOf(d.Unit)?.PlayDamaged(d.HpLoss, d.Hp, d.Block);
                    break;
                case BlockChanged b:
                    ViewOf(b.Unit)?.PlayBlockChanged(b.Delta, b.Block);
                    break;
                case UnitDied x:
                    if (_unitViews.Remove(x.Unit, out var dead))
                        dead.PlayDeath(() => Destroy(dead.gameObject));
                    break;
                case ResourcesChanged:
                    RefreshHud();
                    break;
                case EnemyPlanChanged:
                    RefreshPlanViews();
                    break;
                case BattleEnded end:
                    OnBattleEnded(end.PlayerWon);
                    break;
            }
        }

        UnitView ViewOf(Unit unit) => _unitViews.TryGetValue(unit, out var v) ? v : null;

        void RefreshHud() => hud.Refresh(State, _selectedCard);

        /// <summary>
        /// 연출이 재생 중이면 화면의 유닛 위치가 아직 로직을 따라오지 못한 상태라, 계획 표시를 숨겼다가
        /// 연출이 모두 끝난 뒤(Update) 보여준다. 그래야 경로·잔상이 실제 유닛 위치와 어긋나지 않는다.
        /// </summary>
        void RefreshPlanViews()
        {
            if (AnimationQueue.Busy)
            {
                _planPending = true;
                ApplyPlanViews(EnemyTurnPlan.Empty);
                return;
            }
            _planPending = false;
            ApplyPlanViews(State.EnemyPlan);
        }

        void Update()
        {
            if (_planPending && State != null && !AnimationQueue.Busy)
                RefreshPlanViews();
        }

        void ApplyPlanViews(EnemyTurnPlan plan)
        {
            foreach (var (unit, view) in _unitViews)
            {
                if (unit.Team == Team.Enemy) view.SetIntent(plan.For(unit.Id));
                else if (unit == State.Player) view.SetIncoming(plan);
            }
            gridView.ShowPlan(plan, State.GetUnit);
            RefreshDangerZone();   // 적이 움직였거나 죽었으면 호버 중인 위험 지역도 갱신
        }

        // ---------------- 위험 지역 (적 호버) ----------------

        void OnTileHovered(Vector2Int? cell)
        {
            _hoveredCell = cell;
            gridView.SetHover(cell);
            RefreshDangerZone();
        }

        /// <summary>카드를 고르지 않은 상태에서 적에 마우스를 올리면 그 적의 이동 범위와 공격 가능 칸을 보여준다.</summary>
        void RefreshDangerZone()
        {
            if (State == null || _selectedCard >= 0) return;

            var unit = _hoveredCell.HasValue && !_planPending ? State.Grid.GetUnit(_hoveredCell.Value) : null;
            if (unit == null || unit.IsDead || unit.Team != Team.Enemy)
            {
                gridView.ClearHighlights();
                gridView.SetPlanFocus(null);
                hud.SetHoverInfo(null);
                return;
            }

            gridView.SetPlanFocus(unit.Id);   // 이 적의 경로·공격만 선명하게

            var (moveCells, attackCells) = EnemyPlanner.GetThreatAreaAtTurn(State, unit);
            // 파랑 = 걸어갈 수 있는 칸, 분홍 = 그 너머로 공격만 닿는 칸 (겹치는 칸은 파랑이 보이도록)
            gridView.ShowHighlights(moveCells, dangerMoveHighlight);
            gridView.AddHighlights(attackCells.Where(c => !moveCells.Contains(c)), dangerAttackHighlight);

            var d = unit.Data;
            var action = State.EnemyPlan.For(unit.Id);
            var order = action != null ? $"이번 턴 {action.Order}번째" : "이번 턴 행동 없음";
            var priority = d.actionPriority > 0 ? $"우선순위 {d.actionPriority}" : "우선순위 미지정";
            hud.SetHoverInfo($"{d.displayName}  HP {unit.Hp}/{unit.MaxHp}\n이동 {d.moveRange} · {d.AttackPattern.Describe()} · 공격 {d.attackDamage}\n{order} ({priority})");
        }

        /// <summary>승패 배너와 다음 흐름(보상 등)은 마지막 공격·사망 연출이 끝난 뒤에 보여준다.</summary>
        void OnBattleEnded(bool playerWon)
        {
            Deselect();
            Debug.Log(playerWon ? "[BattleRunner] 승리" : "[BattleRunner] 패배");
            var battle = Battle;
            AnimationQueue.Enqueue(this, done =>
            {
                if (Battle == battle)   // 그사이 다음 전투로 넘어갔으면 무시
                {
                    hud.ShowResult(playerWon);
                    BattleFinished?.Invoke(playerWon);
                }
                done();
            });
        }

        // ---------------- Input ----------------

        void OnCardClicked(int index)
        {
            if (State.IsBattleOver || AnimationQueue.Busy) return;
            if (index == _selectedCard) { Deselect(); return; }

            if (!Battle.CanSelectCard(index, out var reason))
            {
                hud.ShowMessage(reason);
                Deselect();
                return;
            }

            var card = State.Cards.Hand[index];
            if (card.targeting.shape == TargetShape.Self)
            {
                Deselect();
                PlayCard(index, State.Player.Position);
                return;
            }

            _selectedCard = index;
            _validTargets = Battle.GetValidTargets(index);
            gridView.ShowHighlights(_validTargets, HighlightFor(card));
            RefreshHud();
        }

        void OnTileClicked(Vector2Int cell)
        {
            if (_selectedCard < 0 || AnimationQueue.Busy) return;

            if (_validTargets.Contains(cell))
            {
                int index = _selectedCard;
                Deselect();
                PlayCard(index, cell);
            }
            else
            {
                Deselect();
            }
        }

        void PlayCard(int index, Vector2Int target)
        {
            if (!Battle.TryPlayCard(index, target, out var reason))
                hud.ShowMessage(reason);
        }

        void OnEndTurnClicked()
        {
            if (State.IsBattleOver || AnimationQueue.Busy) return;
            Deselect();
            Battle.EndTurn();   // 손패 교체 → 적 턴(계획 그대로) → 다음 플레이어 턴
        }

        void Deselect()
        {
            _selectedCard = -1;
            _validTargets = null;
            gridView.ClearHighlights();
            if (State != null)
            {
                RefreshHud();
                RefreshDangerZone();
            }
        }

        Color HighlightFor(CardData card)
        {
            if (card.effects.Any(e => e?.Kind == EffectKind.Attack)) return attackHighlight;
            if (card.effects.Any(e => e?.Kind == EffectKind.Move)) return moveHighlight;
            return selfHighlight;
        }

        /// <summary>하단 HUD 영역을 피해 필드가 화면 위쪽에 들어오도록 카메라를 맞춘다.</summary>
        void FitCamera()
        {
            if (cam == null || !cam.orthographic) return;
            var size = gridView.WorldSize + Vector2.one * (cameraPadding * 2f);
            float usable = 1f - HudView.BottomFraction;
            cam.orthographicSize = Mathf.Max(size.y * 0.5f / usable, size.x * 0.5f / cam.aspect);
            float viewHeight = cam.orthographicSize * 2f;
            var center = gridView.transform.position;
            float yOffset = -viewHeight * HudView.BottomFraction * 0.5f;
            cam.transform.position = new Vector3(center.x, center.y + yOffset, cam.transform.position.z);
        }
    }
}

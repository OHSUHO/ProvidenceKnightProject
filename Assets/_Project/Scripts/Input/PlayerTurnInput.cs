using System.Collections.Generic;
using System.Linq;
using ProvidenceKnight.Battle;
using ProvidenceKnight.Battle.AI;
using ProvidenceKnight.Battle.Effects;
using ProvidenceKnight.Data;
using ProvidenceKnight.View;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProvidenceKnight.Input
{
    /// <summary>
    /// 플레이어 턴 입력. Idle(적 호버 → 위험 지역) / CardSelected(대상 칸 선택, 행동 미리보기) / Busy(연출 중·적 턴·전투 종료).
    /// </summary>
    public class PlayerTurnInput : MonoBehaviour
    {
        public enum Mode { Idle, CardSelected, Busy }

        [SerializeField] BoardPointer pointer;
        [SerializeField] BattleHud hud;
        [SerializeField] HighlightLayer highlights;
        [SerializeField] EnemyPlanPresenter plan;
        [SerializeField] BattleEventPlayer eventPlayer;

        [Header("행동 미리보기 (플레이 테스트 후 유지 여부 결정)")]
        [Tooltip("카드를 고른 뒤 대상 칸에 마우스를 올리면, 그 칸에 쓰면 바뀔 적 계획을 미리 보여준다")]
        [SerializeField] bool actionPreview = true;
        [SerializeField] Key previewToggleKey = Key.P;

        [Header("전체 위험 지역")]
        [SerializeField] Key allThreatsKey = Key.LeftAlt;

        public Mode Current => IsBusy ? Mode.Busy : _selectedCard >= 0 ? Mode.CardSelected : Mode.Idle;

        BattleController _battle;
        BattleState State => _battle?.State;
        int _selectedCard = -1;
        List<Vector2Int> _validTargets;
        Vector2Int? _previewCell;
        bool _allThreatsHeld;

        bool IsBusy => _battle == null || State.IsBattleOver || State.Phase != BattlePhase.PlayerTurn || eventPlayer.IsPlaying;

        void Awake()
        {
            pointer.Hovered += OnHovered;
            pointer.Clicked += OnClicked;
            pointer.Cancelled += Deselect;
            hud.CardClicked += OnCardClicked;
            hud.EndTurnClicked += OnEndTurnClicked;
            eventPlayer.ResourcesChanged += RefreshHud;
            eventPlayer.PlanChanged += RefreshHover;
            eventPlayer.PlaybackFinished += RefreshHover;
        }

        void OnDestroy()
        {
            if (pointer != null) { pointer.Hovered -= OnHovered; pointer.Clicked -= OnClicked; pointer.Cancelled -= Deselect; }
            if (hud != null) { hud.CardClicked -= OnCardClicked; hud.EndTurnClicked -= OnEndTurnClicked; }
            if (eventPlayer != null)
            {
                eventPlayer.ResourcesChanged -= RefreshHud;
                eventPlayer.PlanChanged -= RefreshHover;
                eventPlayer.PlaybackFinished -= RefreshHover;
            }
        }

        public void Bind(BattleController battle)
        {
            _battle = battle;
            _selectedCard = -1;
            _validTargets = null;
            _previewCell = null;
            highlights.Clear();
        }

        void Update()
        {
            if (_battle == null) return;
            hud.SetInteractable(!IsBusy);

            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb[previewToggleKey].wasPressedThisFrame)
            {
                actionPreview = !actionPreview;
                hud.ShowToast(actionPreview ? "행동 미리보기 켬" : "행동 미리보기 끔");
                _previewCell = null;
                RefreshHover();
            }

            bool held = kb[allThreatsKey].isPressed;
            if (held != _allThreatsHeld)
            {
                _allThreatsHeld = held;
                RefreshHover();
            }
        }

        // ---------------- 카드 → 대상 칸 ----------------

        void OnCardClicked(int index)
        {
            if (IsBusy) return;
            if (index == _selectedCard) { Deselect(); return; }

            if (!_battle.CanSelectCard(index, out var reason))
            {
                hud.ShowToast(reason);
                Deselect();
                return;
            }

            var card = State.Cards.Hand[index];
            if (card.targeting.shape == TargetShape.Self)
            {
                Deselect();
                Play(index, State.Player.Position);
                return;
            }

            _selectedCard = index;
            _validTargets = _battle.GetValidTargets(index);
            _previewCell = null;
            highlights.ShowCardTargets(_validTargets, KindOf(card));
            RefreshHud();
            RefreshHover();
        }

        void OnClicked(Vector2Int cell)
        {
            if (IsBusy || _selectedCard < 0) return;
            int index = _selectedCard;
            bool valid = _validTargets.Contains(cell);
            Deselect();
            if (valid) Play(index, cell);
        }

        void Play(int index, Vector2Int target)
        {
            if (!_battle.TryPlayCard(index, target, out var reason))
                hud.ShowToast(reason);
        }

        void OnEndTurnClicked()
        {
            if (IsBusy) return;
            Deselect();
            _battle.EndTurn();   // 손패 교체 → 적 턴(계획 그대로) → 다음 플레이어 턴
        }

        void Deselect()
        {
            _selectedCard = -1;
            _validTargets = null;
            _previewCell = null;
            plan.ClearPreview();
            highlights.Clear();
            RefreshHud();
            RefreshHover();
        }

        static EffectKind KindOf(CardData card)
        {
            if (card.effects.Any(e => e?.Kind == EffectKind.Attack)) return EffectKind.Attack;
            if (card.effects.Any(e => e?.Kind == EffectKind.Move)) return EffectKind.Move;
            return EffectKind.Other;
        }

        void RefreshHud()
        {
            if (State != null) hud.Refresh(State, _selectedCard);
        }

        // ---------------- 호버: 위험 지역 / 툴팁 / 행동 미리보기 ----------------

        void OnHovered(Vector2Int? cell) => RefreshHover();

        void RefreshHover()
        {
            if (State == null) return;
            var cell = pointer.Current;
            highlights.SetHover(cell);
            var unit = cell.HasValue ? State.Grid.GetUnit(cell.Value) : null;
            ShowTooltip(unit);

            if (_selectedCard >= 0 && !IsBusy)
            {
                RefreshPreview(cell);
                return;
            }

            if (IsBusy || plan.IsPending)
            {
                highlights.Clear();   // 연출 중엔 화면 위치가 로직과 달라 위험 지역이 어긋나 보임
                plan.SetFocus(null);
                return;
            }

            if (_allThreatsHeld)
            {
                // 모든 적의 공격 가능 칸 합집합 (각자 차례 시점 기준)
                var all = new HashSet<Vector2Int>();
                foreach (var enemy in State.Enemies)
                    all.UnionWith(EnemyPlanner.GetThreatAreaAtTurn(State, enemy).attackCells);
                highlights.ShowAllThreats(all);
                plan.SetFocus(null);
            }
            else if (unit is { Team: Team.Enemy, IsDead: false })
            {
                var (moveCells, attackCells) = EnemyPlanner.GetThreatAreaAtTurn(State, unit);
                highlights.ShowDangerZone(moveCells, attackCells);
                plan.SetFocus(unit.Id);
            }
            else
            {
                highlights.Clear();
                plan.SetFocus(null);
            }
        }

        void RefreshPreview(Vector2Int? cell)
        {
            bool show = actionPreview && cell.HasValue && _validTargets.Contains(cell.Value);
            if (!show)
            {
                _previewCell = null;
                plan.ClearPreview();
                return;
            }
            if (_previewCell == cell && plan.IsPreviewing) return;
            _previewCell = cell;
            plan.ShowPreview(_battle.PreviewCard(_selectedCard, cell.Value));
        }

        void ShowTooltip(Unit unit)
        {
            if (unit == null || unit.IsDead) { hud.HideTooltip(); return; }

            var d = unit.Data;
            var hp = $"HP {unit.Hp}/{unit.MaxHp}" + (unit.Block > 0 ? $" · 방어 {unit.Block}" : "")
                + (unit.Negate > 0 ? $" · 공격 무효화 {unit.Negate}회" : "");
            var statuses = string.Join(", ", StatusRules.All.Where(unit.Has).Select(t => StatusRules.Describe(t, unit.GetStatus(t))));
            if (statuses.Length > 0) hp += "\n상태이상: " + statuses;
            if (unit.Team != Team.Enemy)
            {
                var p = State.EnemyPlan;
                int hpLoss = unit.Hp - p.PredictedPlayerHp, blockLoss = unit.Block - p.PredictedPlayerBlock;
                var incoming = p.Actions.Count > 0 && hpLoss + blockLoss > 0
                    ? $"\n적 턴에 받을 피해 {hpLoss + blockLoss}" + (blockLoss > 0 ? $" (방어로 {blockLoss} 막음)" : "") + $" → HP {p.PredictedPlayerHp}"
                    : "\n적 턴에 받을 피해 없음";
                hud.ShowTooltip(d.displayName, hp + incoming);
                return;
            }

            var action = State.EnemyPlan.For(unit.Id);
            var order = action != null ? $"이번 턴 {action.Order}번째" : "이번 턴 행동 없음";
            var priority = d.actionPriority > 0 ? $"우선순위 {d.actionPriority}" : "우선순위 미지정";
            var next = action?.Card != null
                ? $"\n이번 턴 카드: {action.Card.cardName} — {action.Card.GetDescription().Replace('\n', ' ')}"
                : "";
            var owned = d.cards.Count > 0
                ? "\n보유 카드: " + string.Join(", ", d.cards.Where(c => c != null).Select(c => c.cardName))
                : "";
            // 턴 제한이 지나 사라진 효과는 보여주지 않는다
            var activeEffects = d.turnStartEffects.Where(e => e?.effect != null && e.IsActiveOnTurn(State.Turn)).ToList();
            var passive = activeEffects.Count > 0
                ? "\n턴 시작: " + string.Join(", ", activeEffects.Select(e => e.Describe()))
                : "";
            hud.ShowTooltip(d.displayName, $"{hp}\n{order} ({priority}){passive}{next}{owned}");
        }
    }
}

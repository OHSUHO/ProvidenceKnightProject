using System;
using System.Collections.Generic;
using System.Linq;
using ProvidenceKnight.Battle.AI;
using ProvidenceKnight.Battle.Effects;
using ProvidenceKnight.Data;
using UnityEngine;

namespace ProvidenceKnight.Battle
{
    /// <summary>
    /// 전투 한 판의 외부 진입점. 턴 흐름(PlayerTurn → EnemyTurn → …)과 카드 사용을 담당한다.
    /// 상태는 State 로 읽고, 결과는 Events 스트림으로 받는다.
    /// </summary>
    public class BattleController
    {
        public BattleState State { get; }
        public BattleEventLog Events { get; }

        BattleController(BattleState state, BattleEventLog events)
        {
            State = state;
            Events = events;
        }

        /// <param name="seed">행동 순서 동점 처리용 난수 시드. 생략하면 매번 다름.</param>
        public static BattleController Create(StageData stage, IEnumerable<CardData> deck, int handSize = 5, int maxEnergy = 3, int? seed = null)
        {
            if (stage == null) throw new ArgumentNullException(nameof(stage));
            var errors = stage.Validate();
            if (errors.Count > 0)
                throw new ArgumentException($"Invalid stage '{stage.name}':\n- " + string.Join("\n- ", errors));

            var grid = new GridMap(stage.width, stage.height);
            foreach (var b in stage.blockedTiles)
                grid.SetBlocked(b, true);

            var cards = new CardCycle(deck ?? Enumerable.Empty<CardData>(), handSize);
            var rng = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
            var events = new BattleEventLog();
            return new BattleController(new BattleState(grid, cards, maxEnergy, rng, events), events);
        }

        /// <summary>
        /// 스테이지 배치대로 유닛 생성. 뷰가 Events 를 구독한 뒤 호출한다.
        /// playerCurrentHp 를 주면 그 체력으로 플레이어가 시작한다 (이전 스테이지에서 이어받은 체력).
        /// </summary>
        public void SpawnFromStage(StageData stage, int? playerCurrentHp = null)
        {
            State.Player = BattleRules.SpawnUnit(State, stage.player, stage.playerStart, playerCurrentHp);
            foreach (var m in stage.monsters)
                BattleRules.SpawnUnit(State, m.unit, m.position);
        }

        // ---------------- Turn ----------------

        /// <summary>에너지 회복 + 플레이어 방어도 초기화 + 적 계획 갱신.</summary>
        public void StartPlayerTurn()
        {
            if (State.IsBattleOver) return;

            State.Turn++;
            State.Energy = State.MaxEnergy;
            if (State.Player != null) BattleRules.ResetBlock(State, State.Player);
            BattleRules.SetPhase(State, BattlePhase.PlayerTurn);
            State.Emit(new ResourcesChanged());
            RefreshEnemyPlan();
        }

        /// <summary>플레이어 턴 종료: 남은 손패를 전부 버리고 덱에서 새로 HandSize 장을 채운다.</summary>
        public void EndPlayerTurn()
        {
            if (State.IsBattleOver) return;
            State.Cards.DiscardHandAndRefill();
            State.Emit(new ResourcesChanged());
        }

        /// <summary>
        /// 적 턴: 마지막으로 보여준 계획을 다시 계산하지 않고 그대로 실행한다 (선공권은 항상 플레이어).
        /// </summary>
        public void RunEnemyTurn()
        {
            if (State.IsBattleOver) return;

            var plan = State.EnemyPlan;
            if (plan.Revision != State.Revision)
            {
                // 표시된 계획이 상태 변화 뒤에 갱신되지 않았다 → 호출 흐름 버그. 최신 상태로 다시 계획한다.
                Debug.LogWarning($"[BattleController] 적 계획이 최신이 아님 (plan r{plan.Revision}, state r{State.Revision}). 다시 계획합니다.");
                plan = EnemyPlanner.Plan(State);
            }

            BattleRules.SetPhase(State, BattlePhase.EnemyTurn);
            State.EnemyPlan = EnemyTurnPlan.Empty;
            State.Emit(new EnemyPlanChanged(State.EnemyPlan));

            foreach (var action in plan.Actions)
            {
                if (State.IsBattleOver) break;
                EnemyActionResolver.Apply(State, action);
            }
        }

        /// <summary>턴 종료 버튼: 손패 교체 → 적 턴 → (전투가 안 끝났으면) 다음 플레이어 턴.</summary>
        public void EndTurn()
        {
            EndPlayerTurn();
            RunEnemyTurn();
            if (!State.IsBattleOver) StartPlayerTurn();
        }

        /// <summary>현재 상태 기준으로 적 턴 계획을 다시 세워 알린다.</summary>
        public void RefreshEnemyPlan()
        {
            State.EnemyPlan = State.IsBattleOver ? EnemyTurnPlan.Empty : EnemyPlanner.Plan(State);
            State.Emit(new EnemyPlanChanged(State.EnemyPlan));
        }

        // ---------------- Cards ----------------

        public List<Vector2Int> GetValidTargets(int handIndex) =>
            Targeting.GetValidTargets(State.Grid, State.Player, State.Cards.Hand[handIndex]);

        /// <summary>대상 선택 전 단계의 사용 가능 여부.</summary>
        public bool CanSelectCard(int handIndex, out string reason)
        {
            reason = null;
            if (State.IsBattleOver) { reason = "전투 종료"; return false; }
            if (State.Phase != BattlePhase.PlayerTurn) { reason = "플레이어 턴이 아님"; return false; }
            if (State.Player == null || State.Player.IsDead) { reason = "플레이어 없음"; return false; }
            if (handIndex < 0 || handIndex >= State.Cards.Hand.Count) { reason = "잘못된 카드"; return false; }

            var card = State.Cards.Hand[handIndex];
            if (card.cost > State.Energy) { reason = "에너지 부족"; return false; }
            if (GetValidTargets(handIndex).Count == 0) { reason = "대상 없음"; return false; }
            return true;
        }

        public bool TryPlayCard(int handIndex, Vector2Int target, out string reason)
        {
            if (!CanSelectCard(handIndex, out reason)) return false;
            if (!GetValidTargets(handIndex).Contains(target)) { reason = "사용할 수 없는 칸"; return false; }

            ApplyCard(State, handIndex, target);
            if (!State.IsBattleOver) RefreshEnemyPlan();
            return true;
        }

        /// <summary>
        /// 행동 미리보기: 이 카드를 이 칸에 쓰면 적 계획이 어떻게 바뀌는지. 복사본에서 실제와 같은 ApplyCard 를 거치므로
        /// 결과가 사용 후 표시될 계획과 같다. 사용할 수 없으면 null.
        /// </summary>
        public CardPreview PreviewCard(int handIndex, Vector2Int target)
        {
            if (!CanSelectCard(handIndex, out _) || !GetValidTargets(handIndex).Contains(target)) return null;

            var sim = State.CloneForSimulation();
            ApplyCard(sim, handIndex, target);
            var plan = sim.IsBattleOver ? EnemyTurnPlan.Empty : EnemyPlanner.Plan(sim);
            return new CardPreview(sim, plan);
        }

        /// <summary>에너지 지불 → 카드 순환 → 효과 순서대로 적용. 실제 사용과 미리보기가 공유한다.</summary>
        static void ApplyCard(BattleState state, int handIndex, Vector2Int target)
        {
            var card = state.Cards.Hand[handIndex];
            state.Energy -= card.cost;
            state.Cards.Cycle(handIndex, card.exhaust);
            state.Emit(new CardPlayed(card, target));

            var ctx = new EffectContext(state, state.Player, target, card);
            foreach (var effect in card.effects)
            {
                if (state.IsBattleOver) break;
                effect?.Resolve(ctx);
            }

            state.Emit(new ResourcesChanged());
        }
    }

    /// <summary>카드 사용 미리보기 결과. State 는 카드를 쓴 뒤의 복사본(적 턴 전), Plan 은 그 상태의 적 계획.</summary>
    public sealed class CardPreview
    {
        public BattleState State { get; }
        public EnemyTurnPlan Plan { get; }

        public CardPreview(BattleState state, EnemyTurnPlan plan)
        {
            State = state;
            Plan = plan;
        }
    }
}

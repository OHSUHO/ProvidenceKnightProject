using System.Collections.Generic;
using System.Linq;
using ProvidenceKnight.Data;
using ProvidenceKnight.Battle.Effects;
using UnityEngine;

namespace ProvidenceKnight.Battle.AI
{
    /// <summary>
    /// 적 턴 계획. 전투 상태의 복사본 위에서 행동 순서대로 한 마리씩 행동을 고르고 곧바로 적용해 본다.
    /// 뒤의 몬스터는 앞선 몬스터가 옮겨 간 뒤의 격자를 보고 계획하므로, 실제 실행과 결과가 항상 같다.
    /// (계획과 실행 모두 EnemyActionResolver.Apply 를 거친다)
    /// </summary>
    public static class EnemyPlanner
    {
        public static EnemyTurnPlan Plan(BattleState state)
        {
            var sim = state.CloneForSimulation();
            var actions = new List<EnemyAction>();
            int order = 1;

            // 실제 적 턴처럼 상태이상 피해가 먼저 들어간다 (이 피해로 쓰러지는 몬스터는 계획에서 빠진다)
            BattleRules.RunEnemyTurnStart(sim);

            foreach (var enemy in sim.EnemiesInActionOrder.ToList())
            {
                if (sim.IsBattleOver) break;
                if (enemy.IsDead) continue;

                var action = ChooseAction(sim.Grid, enemy, sim.Player, order++);
                EnemyActionResolver.Apply(sim, action);
                actions.Add(action);
            }

            return new EnemyTurnPlan(actions, state.Revision,
                sim.Player?.Hp ?? 0, sim.Player?.Block ?? 0);
        }

        /// <summary>
        /// 몬스터 한 마리의 행동 선택 = 카드 한 장 (격자는 바꾸지 않는다).
        /// ① 지금 자리에서 플레이어를 칠 수 있는 공격 카드가 있으면 그중 피해가 가장 큰 카드
        /// ② 없으면 이동 카드로 플레이어까지 경로 거리가 가장 짧아지는 칸으로 이동 ③ 그것도 안 되면 대기.
        /// 동점은 카드 순서, 그다음 BFS 발견 순서(걸음 수 → 상·우·하·좌)로 정해진다.
        /// </summary>
        public static EnemyAction ChooseAction(GridMap grid, Unit enemy, Unit target, int order = 1)
        {
            if (target == null || target.IsDead) return EnemyAction.Wait(enemy, order);
            if (enemy.Has(StatusType.Stun)) return EnemyAction.Stunned(enemy, order);

            var cards = UsableCards(enemy).ToList();

            CardData bestAttack = null;
            foreach (var card in cards.Where(c => c.Has(EffectKind.Attack)))
            {
                if (!Targeting.GetCells(grid, enemy.Position, enemy.Team, card.targeting, enemy).Contains(target.Position)) continue;
                if (bestAttack == null || card.TotalDamage > bestAttack.TotalDamage) bestAttack = card;
            }
            if (bestAttack != null) return EnemyAction.Attack(enemy, order, bestAttack, target.Position);

            var field = grid.GetDistanceFieldIgnoringUnits(target.Position);
            int bestDist = field.TryGetValue(enemy.Position, out var cur) ? cur : int.MaxValue;
            CardData bestCard = null;
            Vector2Int bestCell = default;
            foreach (var card in cards.Where(c => c.Has(EffectKind.Move) && !c.Has(EffectKind.Attack)))
            {
                foreach (var cell in Targeting.GetCells(grid, enemy.Position, enemy.Team, card.targeting, enemy))
                {
                    if (field.TryGetValue(cell, out var d) && d < bestDist)
                    {
                        bestDist = d;
                        bestCard = card;
                        bestCell = cell;
                    }
                }
            }

            return bestCard != null
                ? EnemyAction.Move(enemy, order, bestCard, bestCell, grid.FindPath(enemy.Position, bestCell))
                : EnemyAction.Wait(enemy, order);
        }


        /// <summary>상태이상(기절·빙결·암흑)에 막히지 않은, 지금 쓸 수 있는 카드.</summary>
        static IEnumerable<CardData> UsableCards(Unit enemy) =>
            enemy.Data.cards.Where(c => c != null && StatusRules.CanUse(enemy, c));

        /// <summary>
        /// 위험 지역을 "이 몬스터 차례가 됐을 때의 격자" 기준으로 계산한다.
        /// 현재 계획에서 앞선 몬스터들의 행동을 복사본에 먼저 적용하므로, 계획된 도착 칸·공격 칸이 항상 이 범위 안에 들어간다.
        /// </summary>
        public static (List<Vector2Int> moveCells, HashSet<Vector2Int> attackCells) GetThreatAreaAtTurn(BattleState state, Unit enemy)
        {
            var sim = state.CloneForSimulation();
            foreach (var action in state.EnemyPlan.Actions)
            {
                if (action.ActorId == enemy.Id) break;
                EnemyActionResolver.Apply(sim, action);
            }
            return GetThreatArea(sim.Grid, sim.GetUnit(enemy.Id));
        }

        /// <summary>
        /// 위험 지역: 몬스터는 한 턴에 카드 한 장만 쓰므로, 이동 카드로 갈 수 있는 칸(moveCells)과
        /// 지금 자리에서 공격 카드로 칠 수 있는 칸(attackCells)을 따로 보여준다 (주어진 격자 그대로 기준).
        /// </summary>
        public static (List<Vector2Int> moveCells, HashSet<Vector2Int> attackCells) GetThreatArea(GridMap grid, Unit enemy)
        {
            var moveCells = new List<Vector2Int>();
            var attackCells = new HashSet<Vector2Int>();
            foreach (var card in UsableCards(enemy))
            {
                if (card.Has(EffectKind.Attack))
                {
                    foreach (var c in Targeting.GetCells(grid, enemy.Position, enemy.Team, card.targeting.AnyOccupant(), enemy))
                        if (c != enemy.Position) attackCells.Add(c);
                }
                else if (card.Has(EffectKind.Move))
                {
                    foreach (var c in Targeting.GetCells(grid, enemy.Position, enemy.Team, card.targeting, enemy))
                        if (!moveCells.Contains(c)) moveCells.Add(c);
                }
            }
            return (moveCells, attackCells);
        }
    }

    /// <summary>적 행동(카드 한 장)을 전투 상태에 적용한다. 계획(복사본)과 실제 실행이 같은 함수를 쓴다.</summary>
    public static class EnemyActionResolver
    {
        public static void Apply(BattleState state, EnemyAction action)
        {
            if (state.IsBattleOver) return;

            var unit = state.GetUnit(action.ActorId);
            if (unit == null || unit.IsDead) return;

            if (action.Card != null)
            {
                var ctx = new EffectContext(state, unit, action.Target, action.Card);
                foreach (var effect in action.Card.effects)
                {
                    if (state.IsBattleOver) break;
                    effect?.Resolve(ctx);
                }
            }

            // 행동이 끝나면 (기절해서 쉬었더라도) 자기 턴 하나가 지난 것 → 제어 상태이상 지속 턴 감소
            if (!state.IsBattleOver) BattleRules.EndOfTurn(state, unit);
        }
    }
}

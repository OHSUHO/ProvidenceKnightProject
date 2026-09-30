using System.Collections.Generic;
using System.Linq;
using ProvidenceKnight.Data;
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
        /// 몬스터 한 마리의 행동 선택 (격자는 바꾸지 않는다).
        /// ① 제자리에서 공격 가능 → 공격 ② 이동력 안에서 공격 가능한 칸 중 가장 가까운 칸으로 이동 후 공격
        /// ③ 플레이어까지 경로 거리가 가장 짧아지는 칸으로 이동 ④ 대기.
        /// 동점은 BFS 발견 순서(걸음 수 → 상·우·하·좌)로 정해진다.
        /// </summary>
        public static EnemyAction ChooseAction(GridMap grid, Unit enemy, Unit target, int order = 1)
        {
            if (target == null || target.IsDead) return EnemyAction.Wait(enemy, order);

            var pattern = enemy.Data.AttackPattern;
            int damage = enemy.Data.attackDamage;
            var hit = new[] { target.Position };

            if (CanHitFrom(grid, enemy.Position, enemy, pattern, target))
                return EnemyAction.Attack(enemy, order, null, hit, damage);

            if (enemy.Data.moveRange <= 0) return EnemyAction.Wait(enemy, order);

            var reachable = grid.GetReachableOrdered(enemy.Position, enemy.Data.moveRange);

            foreach (var (cell, _) in reachable)
            {
                if (CanHitFrom(grid, cell, enemy, pattern, target))
                    return EnemyAction.Attack(enemy, order, grid.FindPath(enemy.Position, cell), hit, damage);
            }

            var field = grid.GetDistanceFieldIgnoringUnits(target.Position);
            int bestDist = field.TryGetValue(enemy.Position, out var cur) ? cur : int.MaxValue;
            Vector2Int? best = null;
            foreach (var (cell, _) in reachable)
            {
                if (field.TryGetValue(cell, out var d) && d < bestDist)
                {
                    bestDist = d;
                    best = cell;
                }
            }

            return best.HasValue
                ? EnemyAction.Move(enemy, order, grid.FindPath(enemy.Position, best.Value))
                : EnemyAction.Wait(enemy, order);
        }

        static bool CanHitFrom(GridMap grid, Vector2Int origin, Unit enemy, TargetPattern pattern, Unit target) =>
            Targeting.GetCells(grid, origin, enemy.Team, pattern, enemy).Contains(target.Position);

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
        /// 위험 지역: 이 몬스터가 걸어갈 수 있는 칸과, 그 칸들 중 어디서든 공격할 수 있는 모든 칸 (주어진 격자 그대로 기준).
        /// </summary>
        public static (List<Vector2Int> moveCells, HashSet<Vector2Int> attackCells) GetThreatArea(GridMap grid, Unit enemy)
        {
            var moveCells = new List<Vector2Int>();
            var origins = new List<Vector2Int> { enemy.Position };
            if (enemy.Data.moveRange > 0)
            {
                foreach (var (cell, _) in grid.GetReachableOrdered(enemy.Position, enemy.Data.moveRange))
                {
                    moveCells.Add(cell);
                    origins.Add(cell);
                }
            }

            var pattern = enemy.Data.AttackPattern.AnyOccupant();
            var attackCells = new HashSet<Vector2Int>();
            foreach (var o in origins)
                foreach (var c in Targeting.GetCells(grid, o, enemy.Team, pattern, enemy))
                    if (c != enemy.Position) attackCells.Add(c);
            return (moveCells, attackCells);
        }
    }

    /// <summary>적 행동 하나를 전투 상태에 적용한다. 계획(복사본)과 실제 실행이 같은 함수를 쓴다.</summary>
    public static class EnemyActionResolver
    {
        public static void Apply(BattleState state, EnemyAction action)
        {
            if (state.IsBattleOver || action.Type == IntentType.Wait) return;

            var unit = state.GetUnit(action.ActorId);
            if (unit == null || unit.IsDead) return;

            if (action.Path.Count > 0 && !BattleRules.TryMoveAlongPath(state, unit, action.Path))
            {
                Debug.LogError($"[EnemyActionResolver] 계획된 경로로 이동할 수 없음: {action}");
                return;
            }

            if (action.Type != IntentType.Attack) return;
            foreach (var cell in action.AttackCells)
            {
                if (state.IsBattleOver) break;
                BattleRules.AttackCell(state, unit, cell, action.Damage);
            }
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using ProvidenceKnight.Battle.AI;
using ProvidenceKnight.Data;
using UnityEngine;

namespace ProvidenceKnight.Battle
{
    /// <summary>
    /// 전투 상태를 바꾸는 기본 규칙 모음. 카드 효과·적 행동·턴 진행이 모두 이 함수들을 거친다.
    /// 상태를 바꿀 때마다 Revision 을 올리고 결과 이벤트를 state 의 싱크로 기록한다.
    /// </summary>
    public static class BattleRules
    {
        public static Unit SpawnUnit(BattleState state, UnitData data, Vector2Int position, int? currentHp = null)
        {
            var unit = new Unit(state.NextUnitId(), data, position, currentHp);
            // 행동 순서 동점 처리용 난수는 스폰할 때 한 번만 뽑는다 → 이 전투가 끝날 때까지 순서 고정.
            unit.ActionOrder = new ActionOrderKey(data.actionPriority, state.Rng.Next(), unit.Id);
            state.Grid.PlaceUnit(unit, position);
            state.AddUnit(unit);
            state.Touch();
            state.Emit(new UnitSpawned(unit));
            return unit;
        }

        /// <summary>경로를 따라 이동. 경로의 모든 칸이 비어 있고 이어져 있어야 한다. 실패하면 아무것도 바꾸지 않는다.</summary>
        public static bool TryMoveAlongPath(BattleState state, Unit unit, IReadOnlyList<Vector2Int> path)
        {
            if (path == null || path.Count == 0) return false;
            var grid = state.Grid;
            var prev = unit.Position;
            foreach (var cell in path)
            {
                bool adjacent = grid.MoveDirections.Contains(cell - prev);
                if (!adjacent || !grid.IsWalkable(cell)) return false;
                prev = cell;
            }

            grid.MoveUnit(unit, path[path.Count - 1]);
            state.Touch();
            state.Emit(new UnitMoved(unit, path));
            return true;
        }

        /// <summary>칸 하나에 공격. 그 칸에 적대 유닛이 있을 때만 피해를 준다 (아군 오사 없음).</summary>
        public static void AttackCell(BattleState state, Unit attacker, Vector2Int cell, int damage)
        {
            var victim = state.Grid.GetUnit(cell);
            if (!Targeting.IsEnemyOf(victim, attacker)) return;
            state.Emit(new UnitAttacked(attacker, victim, cell, damage));
            DealDamage(state, victim, damage);
        }

        /// <summary>방어도로 먼저 흡수 → HP 감소 → 사망 시 격자에서 제거 → 승패 판정.</summary>
        public static void DealDamage(BattleState state, Unit victim, int amount)
        {
            if (victim.IsDead) return;
            int blockBefore = victim.Block;
            int hpLoss = victim.TakeDamage(amount);
            state.Touch();
            state.Emit(new UnitDamaged(victim, hpLoss, blockBefore - victim.Block, victim.Hp, victim.Block));

            if (victim.IsDead)
            {
                state.Grid.RemoveUnit(victim);
                state.Emit(new UnitDied(victim));
            }
            CheckBattleEnd(state);
        }

        public static void GainBlock(BattleState state, Unit unit, int amount)
        {
            if (amount <= 0) return;
            unit.AddBlock(amount);
            state.Touch();
            state.Emit(new BlockChanged(unit, amount, unit.Block));
        }

        public static void ResetBlock(BattleState state, Unit unit)
        {
            if (unit.Block <= 0) return;
            int lost = unit.Block;
            unit.ResetBlock();
            state.Touch();
            state.Emit(new BlockChanged(unit, -lost, 0));
        }

        public static void SetPhase(BattleState state, BattlePhase phase)
        {
            if (state.Phase == phase) return;
            state.Phase = phase;
            state.Emit(new PhaseChanged(phase));
        }

        /// <summary>플레이어 사망 = 패배, 적 전멸 = 승리. 전투가 끝나면 적 계획을 비운다.</summary>
        public static void CheckBattleEnd(BattleState state)
        {
            if (state.IsBattleOver || state.Player == null) return;

            bool playerDead = state.Player.IsDead;
            bool enemiesWiped = !state.Enemies.Any();
            if (!playerDead && !enemiesWiped) return;

            SetPhase(state, playerDead ? BattlePhase.Defeat : BattlePhase.Victory);
            if (state.EnemyPlan.Actions.Count > 0)
            {
                state.EnemyPlan = EnemyTurnPlan.Empty;
                state.Emit(new EnemyPlanChanged(state.EnemyPlan));
            }
            state.Emit(new BattleEnded(!playerDead));
        }
    }
}

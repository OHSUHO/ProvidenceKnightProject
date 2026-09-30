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
                bool adjacent = GridMap.Directions4.Contains(cell - prev);
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

            // 무효화가 남아 있으면 이 공격은 피해 0 (방어도도 그대로)
            if (damage > 0 && victim.ConsumeNegate())
            {
                state.Touch();
                state.Emit(new NegateChanged(victim, -1, victim.Negate));
                return;
            }
            DealDamage(state, victim, damage);
        }

        public static void GainNegate(BattleState state, Unit unit, int amount, int maxStacks = 0)
        {
            int gained = unit.AddNegate(amount, maxStacks);
            if (gained <= 0) return;
            state.Touch();
            state.Emit(new NegateChanged(unit, gained, unit.Negate));
        }

        /// <summary>
        /// 몬스터의 턴 시작 효과. 방어도를 먼저 초기화한 뒤(몬스터도 플레이어처럼 한 턴 지나면 사라진다)
        /// 정의된 효과를 위에서부터 차례로 시전자=자기 자신으로 실행한다. 행동 순서대로 한 마리씩.
        /// </summary>
        public static void RunTurnStart(BattleState state, Unit unit)
        {
            if (unit.IsDead) return;
            ResetBlock(state, unit);

            var entries = unit.Data.turnStartEffects;
            if (entries == null) return;
            var ctx = new Effects.EffectContext(state, unit, unit.Position, null);
            foreach (var entry in entries)
            {
                if (state.IsBattleOver) break;
                if (entry?.effect == null || !entry.IsActiveOnTurn(state.Turn)) continue;   // 턴 제한이 지난 효과는 건너뜀
                entry.effect.Resolve(ctx);
            }
        }

        // ---------------- 상태이상 ----------------

        /// <summary>
        /// 상태이상 부여. 같은 상태가 이미 있으면 겹친다: 독/출혈/화상은 강도가 더해지고(출혈·화상은 지속 턴은 긴 쪽),
        /// 기절/빙결/암흑은 지속 턴이 긴 쪽으로. amount 는 피해형에서만, turns 는 독을 뺀 나머지에서만 쓴다.
        /// </summary>
        public static void ApplyStatus(BattleState state, Unit unit, StatusType type, int amount, int turns)
        {
            if (unit == null || unit.IsDead) return;
            var cur = unit.GetStatus(type);
            int newAmount = cur.Amount, newTurns = cur.Turns;

            if (type == StatusType.Poison)
            {
                if (amount <= 0) return;
                newAmount += amount;
                newTurns = 0;
            }
            else if (StatusRules.IsDamageOverTime(type))
            {
                if (amount <= 0 || turns <= 0) return;
                newAmount += amount;
                newTurns = Mathf.Max(cur.Turns, turns);
            }
            else
            {
                if (turns <= 0) return;
                newTurns = Mathf.Max(cur.Turns, turns);
            }

            SetStatus(state, unit, type, newAmount, newTurns, applied: true);
        }

        static void SetStatus(BattleState state, Unit unit, StatusType type, int amount, int turns, bool applied)
        {
            if (!StatusRules.IsActive(type, new StatusState(amount, turns))) { amount = 0; turns = 0; }
            unit.SetStatus(type, amount, turns);
            state.Touch();
            state.Emit(new StatusChanged(unit, type, amount, turns, applied));
        }

        /// <summary>
        /// 유닛 자기 턴 시작: 피해형 상태이상(독 → 출혈 → 화상 순)이 피해를 주고 한 턴씩 줄어든다.
        /// 출혈·독은 방어도를 무시하고 화상은 방어도가 막아준다. 공격 무효화도 소용없다.
        /// </summary>
        public static void TickStartOfTurn(BattleState state, Unit unit)
        {
            foreach (var type in new[] { StatusType.Poison, StatusType.Bleed, StatusType.Burn })
            {
                if (state.IsBattleOver || unit.IsDead) return;
                if (!unit.Has(type)) continue;

                var s = unit.GetStatus(type);
                DealDamage(state, unit, s.Amount, StatusRules.IgnoresBlock(type));
                if (unit.IsDead) return;

                if (type == StatusType.Poison) SetStatus(state, unit, type, s.Amount - 1, 0, applied: false);
                else SetStatus(state, unit, type, s.Amount, s.Turns - 1, applied: false);
            }
        }

        /// <summary>유닛 자기 턴 종료: 기절/빙결/암흑이 한 턴씩 줄어든다.</summary>
        public static void EndOfTurn(BattleState state, Unit unit)
        {
            if (unit == null || unit.IsDead) return;
            foreach (var type in new[] { StatusType.Stun, StatusType.Freeze, StatusType.Darkness })
            {
                if (!unit.Has(type)) continue;
                SetStatus(state, unit, type, 0, unit.GetStatus(type).Turns - 1, applied: false);
            }
        }

        /// <summary>적 턴이 시작될 때 몬스터 전원의 상태이상 피해 (행동 순서대로). 계획 시뮬레이션과 실제 실행이 같이 쓴다.</summary>
        public static void RunEnemyTurnStart(BattleState state)
        {
            foreach (var enemy in state.EnemiesInActionOrder.ToList())
            {
                if (state.IsBattleOver) break;
                TickStartOfTurn(state, enemy);
            }
        }

        /// <summary>방어도로 먼저 흡수 → HP 감소 → 사망 시 격자에서 제거 → 승패 판정.</summary>
        public static void DealDamage(BattleState state, Unit victim, int amount, bool ignoreBlock = false)
        {
            if (victim.IsDead) return;
            int blockBefore = victim.Block;
            int hpLoss = victim.TakeDamage(amount, ignoreBlock);
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

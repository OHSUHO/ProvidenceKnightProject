using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProvidenceKnight.Battle.AI
{
    public enum IntentType
    {
        Wait,    // 할 수 있는 행동이 없음
        Move,    // 대상에게 가까워지려고 이동만 함
        Attack   // (필요하면 이동 후) 공격
    }

    /// <summary>
    /// 몬스터 한 마리의 이번 적 턴 행동. 유닛은 Id 로만 참조한다
    /// (같은 행동을 시뮬레이션용 복사본과 실제 전투 양쪽에 똑같이 적용하기 위해).
    /// </summary>
    public sealed class EnemyAction
    {
        public int ActorId { get; }
        public int Order { get; }                              // 1부터 시작하는 행동 순서
        public IntentType Type { get; }
        public Vector2Int From { get; }
        public IReadOnlyList<Vector2Int> Path { get; }         // 시작 칸 제외, 도착 칸 포함. 이동 없으면 비어 있음
        public IReadOnlyList<Vector2Int> AttackCells { get; }  // 공격이 적용될 칸
        public int Damage { get; }

        public Vector2Int Destination => Path.Count > 0 ? Path[Path.Count - 1] : From;
        public int Steps => Path.Count;

        EnemyAction(int actorId, int order, IntentType type, Vector2Int from,
            IReadOnlyList<Vector2Int> path, IReadOnlyList<Vector2Int> attackCells, int damage)
        {
            ActorId = actorId;
            Order = order;
            Type = type;
            From = from;
            Path = path ?? Array.Empty<Vector2Int>();
            AttackCells = attackCells ?? Array.Empty<Vector2Int>();
            Damage = damage;
        }

        public static EnemyAction Wait(Unit actor, int order) =>
            new(actor.Id, order, IntentType.Wait, actor.Position, null, null, 0);

        public static EnemyAction Move(Unit actor, int order, IReadOnlyList<Vector2Int> path) =>
            new(actor.Id, order, IntentType.Move, actor.Position, path, null, 0);

        public static EnemyAction Attack(Unit actor, int order, IReadOnlyList<Vector2Int> path, IReadOnlyList<Vector2Int> cells, int damage) =>
            new(actor.Id, order, IntentType.Attack, actor.Position, path, cells, damage);

        public override string ToString() =>
            $"#{ActorId} ({Order}) {Type} {From}->{Destination}" + (Type == IntentType.Attack ? $" hit [{string.Join(",", AttackCells)}] {Damage}" : "");
    }

    /// <summary>
    /// 이번 적 턴 전체 계획. 화면에 보여준 이 계획이 그대로 실행된다.
    /// 시뮬레이션 결과(플레이어의 예상 HP/방어도)도 함께 들고 있어 피해 예고에 쓴다.
    /// </summary>
    public sealed class EnemyTurnPlan
    {
        public static readonly EnemyTurnPlan Empty = new(Array.Empty<EnemyAction>(), -1, 0, 0);

        public IReadOnlyList<EnemyAction> Actions { get; }
        public int Revision { get; }               // 계획을 세운 시점의 BattleState.Revision
        public int PredictedPlayerHp { get; }
        public int PredictedPlayerBlock { get; }

        public EnemyTurnPlan(IReadOnlyList<EnemyAction> actions, int revision, int predictedPlayerHp, int predictedPlayerBlock)
        {
            Actions = actions;
            Revision = revision;
            PredictedPlayerHp = predictedPlayerHp;
            PredictedPlayerBlock = predictedPlayerBlock;
        }

        public EnemyAction For(int unitId) => Actions.FirstOrDefault(a => a.ActorId == unitId);

        public IEnumerable<Vector2Int> AllAttackCells => Actions.SelectMany(a => a.AttackCells).Distinct();
    }
}

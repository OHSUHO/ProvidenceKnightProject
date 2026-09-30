using System;
using System.Collections.Generic;
using ProvidenceKnight.Battle.AI;
using ProvidenceKnight.Data;
using UnityEngine;

namespace ProvidenceKnight.Battle
{
    /// <summary>
    /// 로직이 기록하는 결과 한 건. 뷰는 이 스트림을 순서대로 재생한다.
    /// HP/방어도 같은 값은 이벤트 시점의 스냅샷을 담는다 → 연출이 로직보다 늦게 재생돼도 화면 값이 어긋나지 않는다.
    /// </summary>
    public abstract record BattleEvent;

    public sealed record UnitSpawned(Unit Unit) : BattleEvent;
    public sealed record UnitMoved(Unit Unit, IReadOnlyList<Vector2Int> Path) : BattleEvent;      // 시작 칸 제외, 도착 칸 포함
    public sealed record UnitAttacked(Unit Attacker, Unit Target, Vector2Int Cell, int Damage) : BattleEvent;   // 피해 적용 직전
    public sealed record UnitDamaged(Unit Unit, int HpLoss, int BlockLoss, int Hp, int Block) : BattleEvent;
    public sealed record BlockChanged(Unit Unit, int Delta, int Block) : BattleEvent;            // 획득(+) 또는 턴 시작 초기화(-)
    public sealed record NegateChanged(Unit Unit, int Delta, int Negate) : BattleEvent;          // 획득(+) 또는 공격을 막아 소모(-)
    /// <summary>상태이상 변화. Amount/Turns 는 변화 직후 값 (둘 다 비활성이면 사라진 것). Applied = 새로 걸림(true) / 틱·만료(false).</summary>
    public sealed record StatusChanged(Unit Unit, StatusType Status, int Amount, int Turns, bool Applied) : BattleEvent;
    public sealed record UnitDied(Unit Unit) : BattleEvent;
    public sealed record CardPlayed(CardData Card, Vector2Int Target) : BattleEvent;
    public sealed record ResourcesChanged : BattleEvent;                                         // 에너지 / 손패 / 턴
    public sealed record EnemyPlanChanged(EnemyTurnPlan Plan) : BattleEvent;
    public sealed record PhaseChanged(BattlePhase Phase) : BattleEvent;
    public sealed record BattleEnded(bool PlayerWon) : BattleEvent;

    public interface IBattleEventSink
    {
        void Emit(BattleEvent e);
    }

    /// <summary>시뮬레이션(적 계획, 행동 미리보기)용. 아무것도 기록하지 않는다.</summary>
    public sealed class NullEventSink : IBattleEventSink
    {
        public static readonly NullEventSink Instance = new();
        NullEventSink() { }
        public void Emit(BattleEvent e) { }
    }

    /// <summary>실제 전투의 이벤트 기록. 뷰는 Emitted 를 구독하고, 테스트는 History 를 검사한다.</summary>
    public sealed class BattleEventLog : IBattleEventSink
    {
        readonly List<BattleEvent> _history = new();

        public IReadOnlyList<BattleEvent> History => _history;
        public event Action<BattleEvent> Emitted;

        public void Emit(BattleEvent e)
        {
            _history.Add(e);
            Emitted?.Invoke(e);
        }
    }
}

namespace System.Runtime.CompilerServices
{
    /// <summary>C# 9 record / init 접근자용 (Unity 의 .NET 프로필에 없음).</summary>
    internal static class IsExternalInit { }
}

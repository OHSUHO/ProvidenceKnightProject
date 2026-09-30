using System;
using ProvidenceKnight.Data;
using UnityEngine;

namespace ProvidenceKnight.Battle
{
    /// <summary>
    /// 적 턴 행동 순서의 정렬 키. 스폰 시 한 번 정해지고 그 전투가 끝날 때까지 바뀌지 않는다.
    /// 우선순위 지정(1 이상) → 미지정(0) 순, 지정끼리는 값이 작을수록 먼저, 같으면 무작위 타이브레이커, 마지막으로 Id.
    /// </summary>
    public readonly struct ActionOrderKey : IComparable<ActionOrderKey>
    {
        public int Priority { get; }     // 0 = 미지정
        public int TieBreaker { get; }
        public int UnitId { get; }

        public ActionOrderKey(int priority, int tieBreaker, int unitId)
        {
            Priority = Mathf.Max(0, priority);
            TieBreaker = tieBreaker;
            UnitId = unitId;
        }

        public bool HasPriority => Priority > 0;

        public int CompareTo(ActionOrderKey other)
        {
            int c = other.HasPriority.CompareTo(HasPriority);   // 지정된 쪽이 먼저
            if (c != 0) return c;
            c = Priority.CompareTo(other.Priority);
            if (c != 0) return c;
            c = TieBreaker.CompareTo(other.TieBreaker);
            return c != 0 ? c : UnitId.CompareTo(other.UnitId);
        }
    }

    /// <summary>전투 중 유닛의 런타임 상태.</summary>
    public class Unit
    {
        public int Id { get; }
        public UnitData Data { get; }
        public Team Team => Data.team;
        public Vector2Int Position { get; internal set; }

        public int MaxHp { get; }
        public int Hp { get; private set; }
        public int Block { get; private set; }
        public bool IsDead => Hp <= 0;

        public ActionOrderKey ActionOrder { get; internal set; }

        /// <summary>currentHp 를 주면 그 값으로 시작한다 (스테이지 간 체력 이어가기용). 생략하면 풀피.</summary>
        public Unit(int id, UnitData data, Vector2Int position, int? currentHp = null)
        {
            Id = id;
            Data = data;
            Position = position;
            MaxHp = data.maxHp;
            Hp = currentHp.HasValue ? Mathf.Clamp(currentHp.Value, 0, MaxHp) : MaxHp;
            ActionOrder = new ActionOrderKey(data.actionPriority, 0, id);
        }

        /// <summary>시뮬레이션용 복사본 (같은 Id).</summary>
        public Unit Clone()
        {
            var copy = new Unit(Id, Data, Position, Hp) { Block = Block, ActionOrder = ActionOrder };
            return copy;
        }

        public void AddBlock(int amount) => Block += Mathf.Max(0, amount);
        public void ResetBlock() => Block = 0;

        /// <summary>방어도로 먼저 흡수 후 남은 피해를 HP에 적용. 실제 HP 감소량을 반환.</summary>
        public int TakeDamage(int amount)
        {
            if (amount <= 0 || IsDead) return 0;
            int absorbed = Mathf.Min(Block, amount);
            Block -= absorbed;
            int hpLoss = Mathf.Min(Hp, amount - absorbed);
            Hp -= hpLoss;
            return hpLoss;
        }

        public override string ToString() => $"{Data.displayName}#{Id} @{Position} HP {Hp}/{MaxHp}";
    }
}

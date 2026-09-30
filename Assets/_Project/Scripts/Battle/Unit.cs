using System;
using System.Linq;
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

        /// <summary>남은 공격 무효화 횟수. 공격 한 번을 통째로 막을 때마다 1 줄고, 턴이 지나도 유지된다.</summary>
        public int Negate { get; private set; }

        public ActionOrderKey ActionOrder { get; internal set; }

        StatusState[] _statuses = new StatusState[StatusRules.All.Length];

        public StatusState GetStatus(StatusType type) => _statuses[(int)type];
        public bool Has(StatusType type) => StatusRules.IsActive(type, _statuses[(int)type]);
        public bool HasAnyStatus => StatusRules.All.Any(Has);
        internal void SetStatus(StatusType type, int amount, int turns) => _statuses[(int)type] = new StatusState(amount, turns);

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
            var copy = new Unit(Id, Data, Position, Hp) { Block = Block, Negate = Negate, ActionOrder = ActionOrder };
            copy._statuses = (StatusState[])_statuses.Clone();
            return copy;
        }

        public void AddBlock(int amount) => Block += Mathf.Max(0, amount);
        public void ResetBlock() => Block = 0;

        /// <summary>무효화 횟수 추가 (maxStacks &gt; 0 이면 그 값까지만). 실제로 늘어난 양을 반환.</summary>
        public int AddNegate(int amount, int maxStacks = 0)
        {
            int target = Negate + Mathf.Max(0, amount);
            if (maxStacks > 0) target = Mathf.Min(target, Mathf.Max(Negate, maxStacks));
            int gained = target - Negate;
            Negate = target;
            return gained;
        }

        /// <summary>무효화 1회 소모. 남은 게 없으면 false.</summary>
        public bool ConsumeNegate()
        {
            if (Negate <= 0) return false;
            Negate--;
            return true;
        }

        /// <summary>방어도로 먼저 흡수 후 남은 피해를 HP에 적용 (ignoreBlock 이면 방어도 무시). 실제 HP 감소량을 반환.</summary>
        public int TakeDamage(int amount, bool ignoreBlock = false)
        {
            if (amount <= 0 || IsDead) return 0;
            int absorbed = ignoreBlock ? 0 : Mathf.Min(Block, amount);
            Block -= absorbed;
            int hpLoss = Mathf.Min(Hp, amount - absorbed);
            Hp -= hpLoss;
            return hpLoss;
        }

        public override string ToString() => $"{Data.displayName}#{Id} @{Position} HP {Hp}/{MaxHp}";
    }
}

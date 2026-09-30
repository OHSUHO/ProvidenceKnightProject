using System;
using UnityEngine;

namespace ProvidenceKnight.Data
{
    /// <summary>대상으로 고를 수 있는 칸의 모양.</summary>
    public enum TargetShape
    {
        Self,       // 대상 선택 없이 즉시 사용
        Walk,       // 걸어서 range 칸 이내 도달 가능한 빈 칸 (이동 카드)
        Adjacent,   // 인접 칸 (현재 이동 방향 세트 기준)
        Line,       // 상하좌우 직선 range 칸, 장애물/유닛에서 멈춤
        Diamond     // 맨해튼 거리 range 이내 (시야 무시)
    }

    /// <summary>카드 대상 / 몬스터 공격 범위를 같은 규칙으로 정의한다.</summary>
    [Serializable]
    public struct TargetPattern
    {
        public TargetShape shape;
        [Min(1)] public int range;
        [Tooltip("대상 칸에 적대 유닛이 있어야만 선택 가능")]
        public bool requiresEnemy;

        public TargetPattern(TargetShape shape, int range, bool requiresEnemy)
        {
            this.shape = shape;
            this.range = Mathf.Max(1, range);
            this.requiresEnemy = requiresEnemy;
        }

        /// <summary>같은 모양이지만 칸에 누가 있든 상관없이 전부 (위험 지역 표시용).</summary>
        public TargetPattern AnyOccupant() => new(shape, range, false);

        public string Describe() => shape switch
        {
            TargetShape.Self => "자신",
            TargetShape.Walk => $"{range}칸 이동",
            TargetShape.Adjacent => "인접",
            TargetShape.Line => $"직선 {range}칸",
            TargetShape.Diamond => $"{range}칸 이내",
            _ => ""
        };
    }
}

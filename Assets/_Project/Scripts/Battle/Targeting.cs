using System.Collections.Generic;
using ProvidenceKnight.Data;
using UnityEngine;

namespace ProvidenceKnight.Battle
{
    /// <summary>TargetPattern 으로부터 선택 가능한 칸을 계산한다. 카드 대상과 몬스터 공격 범위가 같은 규칙을 쓴다.</summary>
    public static class Targeting
    {
        public static List<Vector2Int> GetValidTargets(GridMap grid, Unit caster, CardData card) =>
            GetCells(grid, caster.Position, caster.Team, card.targeting, caster);

        /// <summary>
        /// origin 에서 pattern 으로 고를 수 있는 칸. origin 은 caster 의 실제 위치가 아니어도 된다
        /// ("저 칸으로 가면 때릴 수 있나?" 판정용). self 가 서 있는 칸은 비어 있는 것으로 본다.
        /// </summary>
        public static List<Vector2Int> GetCells(GridMap grid, Vector2Int origin, Team team, TargetPattern pattern, Unit self = null)
        {
            var result = new List<Vector2Int>();

            switch (pattern.shape)
            {
                case TargetShape.Self:
                    result.Add(origin);
                    return result;

                case TargetShape.Walk:
                    foreach (var (cell, _) in grid.GetReachableOrdered(origin, pattern.range))
                        result.Add(cell);
                    return result;

                case TargetShape.Adjacent:
                    foreach (var d in GridMap.Directions4)
                        AddIfTargetable(grid, origin + d, result);
                    break;

                case TargetShape.Line:
                    foreach (var d in GridMap.Directions4)
                    {
                        for (int i = 1; i <= pattern.range; i++)
                        {
                            var p = origin + d * i;
                            if (grid.IsBlocked(p)) break;
                            result.Add(p);
                            var occupant = grid.GetUnit(p);
                            if (occupant != null && occupant != self) break;   // 관통 불가
                        }
                    }
                    break;

                case TargetShape.Diamond:
                    for (int dx = -pattern.range; dx <= pattern.range; dx++)
                    for (int dy = -pattern.range; dy <= pattern.range; dy++)
                    {
                        int dist = Mathf.Abs(dx) + Mathf.Abs(dy);
                        if (dist == 0 || dist > pattern.range) continue;
                        AddIfTargetable(grid, origin + new Vector2Int(dx, dy), result);
                    }
                    break;
            }

            if (pattern.requiresEnemy)
                result.RemoveAll(p => !IsHostile(grid.GetUnit(p), team));
            return result;
        }

        static void AddIfTargetable(GridMap grid, Vector2Int p, List<Vector2Int> result)
        {
            if (!grid.IsBlocked(p)) result.Add(p);
        }

        public static bool IsHostile(Unit other, Team team) =>
            other != null && !other.IsDead && other.Team != team;

        public static bool IsEnemyOf(Unit other, Unit self) => IsHostile(other, self.Team);
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProvidenceKnight.Battle
{
    /// <summary>칸 단위 필드. 통과 가능 여부와 점유 유닛을 관리한다.</summary>
    public class GridMap
    {
        public static readonly Vector2Int[] Directions4 =
        {
            Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left
        };

        public static readonly Vector2Int[] Directions8 =
        {
            Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left,
            new(1, 1), new(1, -1), new(-1, -1), new(-1, 1)
        };

        public int Width { get; }
        public int Height { get; }

        /// <summary>이동 방향 세트. 대각선 이동을 켜려면 Directions8 로 교체.</summary>
        public Vector2Int[] MoveDirections { get; set; } = Directions4;

        readonly bool[] _blocked;
        readonly Unit[] _occupants;

        public GridMap(int width, int height)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentException($"Invalid grid size {width}x{height}");
            Width = width;
            Height = height;
            _blocked = new bool[width * height];
            _occupants = new Unit[width * height];
        }

        int Index(Vector2Int p) => p.y * Width + p.x;

        public bool InBounds(Vector2Int p) => p.x >= 0 && p.y >= 0 && p.x < Width && p.y < Height;

        public bool IsBlocked(Vector2Int p) => !InBounds(p) || _blocked[Index(p)];

        public void SetBlocked(Vector2Int p, bool blocked)
        {
            if (InBounds(p)) _blocked[Index(p)] = blocked;
        }

        public Unit GetUnit(Vector2Int p) => InBounds(p) ? _occupants[Index(p)] : null;

        /// <summary>필드 안 + 장애물 아님 + 비어 있음.</summary>
        public bool IsWalkable(Vector2Int p) => InBounds(p) && !_blocked[Index(p)] && _occupants[Index(p)] == null;

        public void PlaceUnit(Unit unit, Vector2Int p)
        {
            if (!IsWalkable(p))
                throw new InvalidOperationException($"Cannot place {unit} at {p}");
            _occupants[Index(p)] = unit;
            unit.Position = p;
        }

        public void RemoveUnit(Unit unit)
        {
            if (GetUnit(unit.Position) == unit)
                _occupants[Index(unit.Position)] = null;
        }

        public void MoveUnit(Unit unit, Vector2Int to)
        {
            if (!IsWalkable(to))
                throw new InvalidOperationException($"Cannot move {unit} to {to}");
            RemoveUnit(unit);
            _occupants[Index(to)] = unit;
            unit.Position = to;
        }

        public IEnumerable<Vector2Int> AllPositions()
        {
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    yield return new Vector2Int(x, y);
        }

        /// <summary>
        /// origin 에서 maxSteps 이내로 걸어서 도달 가능한 칸과 거리(BFS).
        /// 유닛/장애물은 통과 불가. origin 자체는 포함하지 않는다.
        /// </summary>
        public Dictionary<Vector2Int, int> GetReachable(Vector2Int origin, int maxSteps)
        {
            var dist = new Dictionary<Vector2Int, int>();
            foreach (var (cell, steps) in GetReachableOrdered(origin, maxSteps))
                dist[cell] = steps;
            return dist;
        }

        /// <summary>
        /// GetReachable 과 같지만 BFS 발견 순서(걸음 수 → MoveDirections 순서)를 그대로 유지한다.
        /// 적 AI 의 동점 처리가 이 순서를 따르므로 결과가 항상 결정적이다.
        /// </summary>
        public List<(Vector2Int cell, int steps)> GetReachableOrdered(Vector2Int origin, int maxSteps)
        {
            var result = new List<(Vector2Int, int)>();
            var dist = new Dictionary<Vector2Int, int> { [origin] = 0 };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(origin);

            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                int d = dist[cur];
                if (d >= maxSteps) continue;

                foreach (var dir in MoveDirections)
                {
                    var next = cur + dir;
                    if (dist.ContainsKey(next) || !IsWalkable(next)) continue;
                    dist[next] = d + 1;
                    result.Add((next, d + 1));
                    queue.Enqueue(next);
                }
            }
            return result;
        }

        /// <summary>
        /// origin 에서 각 칸까지의 걸음 수 (origin 포함, 0). 장애물만 막고 유닛은 통과 가능한 것으로 본다.
        /// 적이 플레이어에게 다가갈 때 "아군 뒤에 줄 서기"가 자연스럽도록 쓰는 거리장.
        /// </summary>
        public Dictionary<Vector2Int, int> GetDistanceFieldIgnoringUnits(Vector2Int origin)
        {
            var dist = new Dictionary<Vector2Int, int> { [origin] = 0 };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(origin);

            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                foreach (var dir in MoveDirections)
                {
                    var next = cur + dir;
                    if (dist.ContainsKey(next) || IsBlocked(next)) continue;
                    dist[next] = dist[cur] + 1;
                    queue.Enqueue(next);
                }
            }
            return dist;
        }

        /// <summary>BFS 최단 경로 (start 제외, goal 포함). 도달 불가면 null.</summary>
        public List<Vector2Int> FindPath(Vector2Int start, Vector2Int goal)
        {
            if (!IsWalkable(goal)) return null;

            var prev = new Dictionary<Vector2Int, Vector2Int>();
            var visited = new HashSet<Vector2Int> { start };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                if (cur == goal)
                {
                    var path = new List<Vector2Int>();
                    for (var p = goal; p != start; p = prev[p]) path.Add(p);
                    path.Reverse();
                    return path;
                }

                foreach (var dir in MoveDirections)
                {
                    var next = cur + dir;
                    if (visited.Contains(next) || !IsWalkable(next)) continue;
                    visited.Add(next);
                    prev[next] = cur;
                    queue.Enqueue(next);
                }
            }
            return null;
        }
    }
}

using System;
using System.Collections.Generic;
using ProvidenceKnight.Battle;
using ProvidenceKnight.Battle.AI;
using UnityEngine;

namespace ProvidenceKnight.View
{
    /// <summary>GridMap 을 칸 스프라이트로 그리고, 월드 좌표 ↔ 칸 좌표를 변환한다.</summary>
    public class GridView : MonoBehaviour
    {
        const int OrderTile = 0, OrderThreat = 1, OrderHighlight = 2, OrderHover = 4;   // 3, 5~7 = PlanOverlay

        [SerializeField] float cellSize = 1f;
        [SerializeField, Range(0.5f, 1f)] float tileFill = 0.94f;

        [Header("Colors")]
        [SerializeField] Color tileColorA = new(0.22f, 0.24f, 0.28f);
        [SerializeField] Color tileColorB = new(0.26f, 0.28f, 0.33f);
        [SerializeField] Color blockedColor = new(0.08f, 0.08f, 0.1f);
        [SerializeField] Color hoverColor = new(1f, 1f, 1f, 0.25f);

        [Header("Enemy Plan")]
        [SerializeField] Color plannedAttackColor = new(0.95f, 0.2f, 0.15f, 0.38f);
        [SerializeField, Range(0f, 1f)] float ghostAlpha = 0.4f;

        public float CellSize => cellSize;
        public Vector2 WorldSize => _grid == null ? Vector2.zero : new Vector2(_grid.Width, _grid.Height) * cellSize;

        GridMap _grid;
        SpriteRenderer[,] _highlights;
        SpriteRenderer[,] _threat;
        readonly List<Vector2Int> _activeHighlights = new();
        readonly List<Vector2Int> _activeThreat = new();
        PlanOverlay _planOverlay;
        int? _planFocus;
        SpriteRenderer _hover;

        public void Build(GridMap grid)
        {
            _grid = grid;
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);
            _activeHighlights.Clear();
            _activeThreat.Clear();
            _planFocus = null;

            _highlights = new SpriteRenderer[grid.Width, grid.Height];
            _threat = new SpriteRenderer[grid.Width, grid.Height];
            foreach (var p in grid.AllPositions())
            {
                var baseColor = grid.IsBlocked(p) ? blockedColor : ((p.x + p.y) % 2 == 0 ? tileColorA : tileColorB);
                CreateQuad($"Tile {p.x},{p.y}", p, baseColor, OrderTile, tileFill);

                var th = CreateQuad($"Threat {p.x},{p.y}", p, plannedAttackColor, OrderThreat, tileFill);
                th.enabled = false;
                _threat[p.x, p.y] = th;

                var hl = CreateQuad($"Highlight {p.x},{p.y}", p, Color.clear, OrderHighlight, tileFill);
                hl.enabled = false;
                _highlights[p.x, p.y] = hl;
            }

            _hover = CreateQuad("Hover", Vector2Int.zero, hoverColor, OrderHover, tileFill);
            _hover.enabled = false;

            var overlayRoot = new GameObject("Plan Overlay").transform;
            overlayRoot.SetParent(transform, false);
            _planOverlay = new PlanOverlay(this, overlayRoot);
        }

        SpriteRenderer CreateQuad(string objName, Vector2Int cell, Color color, int order, float fill)
        {
            var go = new GameObject(objName);
            go.transform.SetParent(transform, false);
            go.transform.position = GridToWorld(cell);
            go.transform.localScale = Vector3.one * (cellSize * fill);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Square;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        /// <summary>칸 중심의 월드 좌표. 필드 전체가 이 오브젝트 위치를 중심으로 놓인다.</summary>
        public Vector3 GridToWorld(Vector2Int cell)
        {
            var origin = (Vector2)transform.position - WorldSize * 0.5f;
            var pos = origin + ((Vector2)cell + Vector2.one * 0.5f) * cellSize;
            return new Vector3(pos.x, pos.y, 0f);
        }

        public bool TryWorldToGrid(Vector3 world, out Vector2Int cell)
        {
            var origin = (Vector2)transform.position - WorldSize * 0.5f;
            var local = ((Vector2)world - origin) / cellSize;
            cell = new Vector2Int(Mathf.FloorToInt(local.x), Mathf.FloorToInt(local.y));
            return _grid != null && _grid.InBounds(cell);
        }

        public void SetHover(Vector2Int? cell)
        {
            if (_hover == null) return;
            _hover.enabled = cell.HasValue;
            if (cell.HasValue) _hover.transform.position = GridToWorld(cell.Value);
        }

        // ---------------- 선택 / 위험 지역 하이라이트 ----------------

        public void ShowHighlights(IEnumerable<Vector2Int> cells, Color color)
        {
            ClearHighlights();
            AddHighlights(cells, color);
        }

        /// <summary>기존 하이라이트를 지우지 않고 덧칠한다 (같은 칸이면 나중 색이 이김).</summary>
        public void AddHighlights(IEnumerable<Vector2Int> cells, Color color)
        {
            foreach (var c in cells)
            {
                if (!_grid.InBounds(c)) continue;
                var hl = _highlights[c.x, c.y];
                hl.color = color;
                if (!hl.enabled) _activeHighlights.Add(c);
                hl.enabled = true;
            }
        }

        public void ClearHighlights()
        {
            foreach (var c in _activeHighlights)
                _highlights[c.x, c.y].enabled = false;
            _activeHighlights.Clear();
        }

        // ---------------- 적 계획 오버레이 ----------------

        /// <summary>
        /// 계획된 공격 칸(빨강 바닥) + 경로 화살표 / 도착 잔상 / 공격 화살표(PlanOverlay).
        /// unitLookup 으로 잔상의 색·글자를 가져온다.
        /// </summary>
        public void ShowPlan(EnemyTurnPlan plan, Func<int, Unit> unitLookup)
        {
            if (_grid == null) return;

            foreach (var c in _activeThreat) _threat[c.x, c.y].enabled = false;
            _activeThreat.Clear();

            if (plan != null)
            {
                foreach (var c in plan.AllAttackCells)
                {
                    if (!_grid.InBounds(c)) continue;
                    _threat[c.x, c.y].enabled = true;
                    _activeThreat.Add(c);
                }
            }

            _planOverlay.Show(plan, unitLookup, ghostAlpha);
            _planOverlay.SetFocus(_planFocus);
        }

        /// <summary>한 적의 경로·잔상·공격만 선명하게 (null = 모두 보통).</summary>
        public void SetPlanFocus(int? actorId)
        {
            _planFocus = actorId;
            _planOverlay?.SetFocus(actorId);
        }
    }
}

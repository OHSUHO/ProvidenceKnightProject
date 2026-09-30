using System;
using System.Collections.Generic;
using ProvidenceKnight.Battle;
using ProvidenceKnight.Battle.AI;
using UnityEngine;

namespace ProvidenceKnight.View
{
    /// <summary>
    /// 적 계획 중 "누가 어디로 가서 누구를 치는지"를 그린다.
    /// - 공격 칸: 빨강 바닥 (TileView.threat)
    /// - 경로: 적 → 도착 칸까지 그 적의 색으로 된 선 + 화살촉
    /// - 도착 칸: 그 적의 잔상 (PlanGhostView)
    /// - 공격: 공격 위치(도착 칸 또는 제자리) → 공격 칸까지 빨간 굵은 화살표
    /// SetFocus 로 한 적만 선명하게, 나머지는 흐리게 한다.
    /// </summary>
    public class IntentOverlayView : MonoBehaviour
    {
        [SerializeField] BoardView board;

        [Header("Prefabs")]
        [SerializeField] SpriteRenderer segmentPrefab;
        [SerializeField] SpriteRenderer arrowHeadPrefab;
        [SerializeField] PlanGhostView ghostPrefab;

        [Header("Sorting (유닛 몸체 = 10)")]
        [SerializeField] int pathOrder = 3;
        [SerializeField] int strikeOrder = 7;

        [Header("Look (칸 크기 대비)")]
        [SerializeField] float pathWidth = 0.07f;
        [SerializeField] float strikeWidth = 0.1f;
        [SerializeField] float arrowSize = 0.26f;
        [SerializeField] float ghostHalf = 0.25f;        // 잔상 몸체 절반 크기 (경로 화살 끝이 여기서 멈춤)
        [SerializeField] float bodyHalf = 0.31f;         // 유닛 몸체 절반 크기 (공격 화살 끝이 여기서 멈춤)
        [SerializeField, Range(0f, 1f)] float pathLighten = 0.3f;
        [SerializeField, Range(0f, 1f)] float ghostAlpha = 0.4f;
        [SerializeField, Range(0f, 1f)] float unfocusedAlpha = 0.18f;
        [SerializeField] Color strikeColor = new(1f, 0.25f, 0.2f, 0.95f);

        PrefabPool<SpriteRenderer> _segments, _arrows;
        PrefabPool<PlanGhostView> _ghosts;
        readonly List<Vector2Int> _threatCells = new();

        // 호버 강조용: 그린 요소마다 주인(적 Id)과 원래 색
        readonly List<(int actorId, SpriteRenderer sr, Color color)> _drawn = new();
        readonly List<(int actorId, PlanGhostView ghost)> _drawnGhosts = new();

        float Cell => board.CellSize;

        void Awake()
        {
            _segments = new PrefabPool<SpriteRenderer>(segmentPrefab, transform);
            _arrows = new PrefabPool<SpriteRenderer>(arrowHeadPrefab, transform);
            _ghosts = new PrefabPool<PlanGhostView>(ghostPrefab, transform);
        }

        /// <param name="unitLookup">잔상의 색·글자를 가져올 유닛 (Id → Unit)</param>
        /// <param name="playerGhost">행동 미리보기에서 플레이어가 옮겨 갈 칸 (없으면 null)</param>
        public void Show(EnemyTurnPlan plan, Func<int, Unit> unitLookup, (Unit unit, Vector2Int cell)? playerGhost = null)
        {
            Clear();
            if (plan != null)
            {
                foreach (var action in plan.Actions)
                {
                    var unit = unitLookup?.Invoke(action.ActorId);
                    if (unit == null) continue;

                    if (action.Path.Count > 0)
                    {
                        var pathColor = Color.Lerp(unit.Data.color, Color.white, pathLighten);
                        pathColor.a = 0.9f;
                        DrawPath(action, pathColor);
                        DrawGhost(action.ActorId, unit, action.Destination, action.Order);
                    }

                    if (action.Type == IntentType.Attack)
                    {
                        float startInset = action.Path.Count > 0 ? ghostHalf : bodyHalf;
                        foreach (var cell in action.AttackCells)
                        {
                            DrawStrike(action.ActorId, action.Destination, cell, startInset);
                            MarkThreat(cell);
                        }
                    }
                }
            }

            if (playerGhost is { } pg) DrawGhost(-1, pg.unit, pg.cell, 0);
        }

        /// <summary>null 이면 전부 원래대로, 값이 있으면 그 적의 표시만 선명하게.</summary>
        public void SetFocus(int? actorId)
        {
            foreach (var (id, sr, color) in _drawn)
            {
                var c = color;
                if (actorId.HasValue && id != actorId.Value) c.a *= unfocusedAlpha;
                sr.color = c;
            }
            foreach (var (id, ghost) in _drawnGhosts)
                ghost.SetDim(actorId.HasValue && id != actorId.Value && id >= 0 ? unfocusedAlpha : 1f);
        }

        public void Clear()
        {
            _segments?.ReleaseAll();
            _arrows?.ReleaseAll();
            _ghosts?.ReleaseAll();
            _drawn.Clear();
            _drawnGhosts.Clear();
            foreach (var c in _threatCells) board.TileAt(c)?.SetThreat(false);
            _threatCells.Clear();
        }

        // ---------------- 그리기 ----------------

        void MarkThreat(Vector2Int cell)
        {
            var tile = board.TileAt(cell);
            if (tile == null) return;
            tile.SetThreat(true);
            _threatCells.Add(cell);
        }

        void DrawPath(EnemyAction action, Color color)
        {
            // 같은 방향으로 이어지는 칸은 한 선분으로 합친다
            var corners = new List<Vector3> { board.GridToWorld(action.From) };
            var prevDir = Vector2Int.zero;
            var prev = action.From;
            foreach (var cell in action.Path)
            {
                var dir = cell - prev;
                if (dir != prevDir && prevDir != Vector2Int.zero) corners.Add(board.GridToWorld(prev));
                prevDir = dir;
                prev = cell;
            }

            var dest = board.GridToWorld(action.Destination);
            var tip = dest - (dest - corners[^1]).normalized * (ghostHalf * Cell);
            DrawPolylineArrow(action.ActorId, corners, tip, pathWidth, color, pathOrder);
        }

        void DrawStrike(int actorId, Vector2Int from, Vector2Int target, float startInset)
        {
            var a = board.GridToWorld(from);
            var b = board.GridToWorld(target);
            var dir = (b - a).normalized;
            var start = a + dir * (startInset * Cell);
            var tip = b - dir * (bodyHalf * Cell);
            DrawPolylineArrow(actorId, new List<Vector3> { start }, tip, strikeWidth, strikeColor, strikeOrder);
        }

        /// <summary>points 를 차례로 잇고 마지막 점 → tip 방향으로 화살촉을 붙인다 (tip 이 화살 끝).</summary>
        void DrawPolylineArrow(int actorId, List<Vector3> points, Vector3 tip, float width, Color color, int order)
        {
            var dir = (tip - points[^1]).normalized;
            float arrowLen = arrowSize * Cell;
            var arrowCenter = tip - dir * (arrowLen * 0.5f);

            var pts = new List<Vector3>(points) { arrowCenter };   // 선은 화살촉 가운데까지 (밑동과 겹쳐 이음매가 안 보임)
            for (int i = 0; i < pts.Count - 1; i++)
            {
                var delta = pts[i + 1] - pts[i];
                if (delta.sqrMagnitude < 1e-6f) continue;
                var seg = Take(_segments, actorId, color, order);
                seg.transform.SetPositionAndRotation((pts[i] + pts[i + 1]) * 0.5f, Quaternion.Euler(0, 0, Angle(delta)));
                seg.transform.localScale = new Vector3(delta.magnitude + width * Cell, width * Cell, 1f);   // 모서리가 비지 않게 폭만큼 연장
            }

            var arrow = Take(_arrows, actorId, color, order);
            arrow.transform.SetPositionAndRotation(arrowCenter, Quaternion.Euler(0, 0, Angle(dir)));
            arrow.transform.localScale = Vector3.one * arrowLen;
        }

        void DrawGhost(int actorId, Unit unit, Vector2Int cell, int order)
        {
            var ghost = _ghosts.Take();
            ghost.transform.position = board.GridToWorld(cell);
            ghost.transform.localScale = Vector3.one * Cell;
            var name = unit.Data.displayName;
            ghost.Set(unit.Data.color, name.Length > 0 ? name.Substring(0, 1) : "?", order, ghostAlpha);
            _drawnGhosts.Add((actorId, ghost));
        }

        SpriteRenderer Take(PrefabPool<SpriteRenderer> pool, int actorId, Color color, int order)
        {
            var sr = pool.Take();
            sr.color = color;
            sr.sortingOrder = order;
            _drawn.Add((actorId, sr, color));
            return sr;
        }

        static float Angle(Vector3 v) => Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
    }
}
